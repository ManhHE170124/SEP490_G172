using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services.Interfaces;
using Keytietkiem.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Services
{
    public class InventoryReservationService : IInventoryReservationService
    {
        private const string ResStatusReserved = "Reserved";
        private const string ResStatusReleased = "Released";
        private const string ResStatusFinalized = "Finalized";
        private const string ResStatusConsumed = "Consumed";

        private static async Task<IDbContextTransaction?> BeginSerializableIfNoTxAsync(
            KeytietkiemDbContext db,
            CancellationToken ct)
        {
            // Nếu caller đã mở transaction thì không mở thêm
            if (db.Database.CurrentTransaction != null) return null;

            // Serializable để tránh race: 2 order cùng reserve -> âm stock
            return await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        }

        private static void TrySetUpdatedAt(object entity, DateTime nowUtc)
        {
            var prop = entity.GetType().GetProperty("UpdatedAt", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(DateTime))
                prop.SetValue(entity, nowUtc);
        }

        private static void TrySetStockQty(object entity, int stockQty)
        {
            var prop = entity.GetType().GetProperty("StockQty", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite) return;

            var t = prop.PropertyType;

            if (t == typeof(int)) { prop.SetValue(entity, stockQty); return; }
            if (t == typeof(int?)) { prop.SetValue(entity, (int?)stockQty); return; }
            if (t == typeof(long)) { prop.SetValue(entity, (long)stockQty); return; }
            if (t == typeof(long?)) { prop.SetValue(entity, (long?)stockQty); return; }
            if (t == typeof(short)) { prop.SetValue(entity, (short)stockQty); return; }
            if (t == typeof(short?)) { prop.SetValue(entity, (short?)stockQty); return; }

            try
            {
                var targetType = Nullable.GetUnderlyingType(t) ?? t;
                var converted = Convert.ChangeType(stockQty, targetType);
                prop.SetValue(entity, converted);
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Sau khi reserve/extend/release/expire/consume: sync lại stock + status:
        /// - Key/Account variants: dùng VariantStockRecalculator (raw inventory - reservations)
        /// - Variant.Status & Product.Status auto theo stock:
        ///     + stock <= 0 => OUT_OF_STOCK
        ///     + stock > 0  => giữ INACTIVE nếu đang INACTIVE, còn lại => ACTIVE
        /// - Product.StockQty = sum(variant.StockQty)
        /// </summary>
        public static async Task SyncStockAndStatusesAsync(
            KeytietkiemDbContext db,
            IEnumerable<Guid> variantIds,
            DateTime nowUtc,
            CancellationToken ct)
        {
            var ids = (variantIds ?? Array.Empty<Guid>())
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList();

            if (ids.Count == 0) return;

            // 1) Với key/account: sync stock chuẩn theo keys/accounts - reservations
            // (Non-supported types => return null, no change)
            await VariantStockRecalculator.SyncVariantStockAndStatusAsync(db, ids, nowUtc, ct);

            // 2) Enforce status rule for ALL affected variants + update product totals
            var variants = await db.ProductVariants
                .Include(v => v.Product)
                .Where(v => ids.Contains(v.VariantId))
                .ToListAsync(ct);

            var affectedProductIds = new HashSet<Guid>();

            foreach (var v in variants)
            {
                affectedProductIds.Add(v.ProductId);

                int stock = 0;
                try { stock = Convert.ToInt32(v.StockQty); } catch { stock = 0; }

                var cur = (v.Status ?? string.Empty).Trim().ToUpperInvariant();

                if (stock <= 0)
                {
                    v.Status = "OUT_OF_STOCK";
                }
                else
                {
                    v.Status = (cur == "INACTIVE") ? "INACTIVE" : "ACTIVE";
                }

                TrySetUpdatedAt(v, nowUtc);
            }

            if (affectedProductIds.Count > 0)
            {
                var pids = affectedProductIds.ToList();

                var totals = await db.ProductVariants
                    .AsNoTracking()
                    .Where(v => pids.Contains(v.ProductId))
                    .GroupBy(v => v.ProductId)
                    .Select(g => new { ProductId = g.Key, Total = g.Sum(x => (int?)x.StockQty) ?? 0 })
                    .ToDictionaryAsync(x => x.ProductId, x => x.Total, ct);

                var products = await db.Products
                    .Where(p => pids.Contains(p.ProductId))
                    .ToListAsync(ct);

                foreach (var p in products)
                {
                    var total = totals.TryGetValue(p.ProductId, out var t) ? t : 0;
                    if (total < 0) total = 0;

                    TrySetStockQty(p, total);

                    var cur = (p.Status ?? string.Empty).Trim().ToUpperInvariant();

                    if (total <= 0)
                    {
                        p.Status = "OUT_OF_STOCK";
                    }
                    else
                    {
                        p.Status = (cur == "INACTIVE") ? "INACTIVE" : "ACTIVE";
                    }

                    TrySetUpdatedAt(p, nowUtc);
                }
            }

            await db.SaveChangesAsync(ct);
        }

        public async Task ReserveForOrderAsync(
            KeytietkiemDbContext db,
            Guid orderId,
            IReadOnlyCollection<(Guid VariantId, int Quantity)> lines,
            DateTime nowUtc,
            DateTime reservedUntilUtc,
            CancellationToken ct = default)
        {
            if (!db.Database.IsRelational()) return; // InMemory/unit test

            await using var tx = await BeginSerializableIfNoTxAsync(db, ct);

            var affectedVariantIds = new List<Guid>();

            foreach (var (variantId, qtyRaw) in lines)
            {
                var desiredQty = qtyRaw <= 0 ? 0 : qtyRaw;
                if (desiredQty == 0) continue;

                affectedVariantIds.Add(variantId);

                var existing = await db.OrderInventoryReservations
                    .FromSqlInterpolated($@"
SELECT *
FROM dbo.OrderInventoryReservation WITH (UPDLOCK, HOLDLOCK)
WHERE OrderId = {orderId} AND VariantId = {variantId}
")
                    .AsTracking()
                    .FirstOrDefaultAsync(ct);

                if (existing == null)
                {
                    // 1) Trừ StockQty của variant (atomic, không cho âm)
                    // (StockQty là cache; với key/account sẽ được Sync lại ngay sau reserve)
                    var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.ProductVariant
SET StockQty = StockQty - {desiredQty}
WHERE VariantId = {variantId}
  AND StockQty >= {desiredQty};
", ct);

                    if (affected != 1)
                        throw new InvalidOperationException("Không đủ tồn kho ProductVariant để giữ chỗ (StockQty không đủ).");

                    // 2) Insert reservation row
                    db.OrderInventoryReservations.Add(new OrderInventoryReservation
                    {
                        OrderId = orderId,
                        VariantId = variantId,
                        Quantity = desiredQty,
                        Status = ResStatusReserved,
                        ReservedUntilUtc = reservedUntilUtc,
                        CreatedAtUtc = nowUtc,
                        UpdatedAtUtc = nowUtc
                    });

                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    // Nếu trước đó Released/Finalized/Consumed mà gọi reserve lại -> reserve lại,
                    // nhưng tồn kho chỉ trừ theo chênh lệch so với lượng RESERVED hiện tại.
                    var oldQty = existing.Status == ResStatusReserved ? existing.Quantity : 0;
                    var diff = desiredQty - oldQty;

                    if (diff > 0)
                    {
                        var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.ProductVariant
SET StockQty = StockQty - {diff}
WHERE VariantId = {variantId}
  AND StockQty >= {diff};
", ct);

                        if (affected != 1)
                            throw new InvalidOperationException("Không đủ tồn kho ProductVariant để tăng giữ chỗ (StockQty không đủ).");
                    }
                    else if (diff < 0)
                    {
                        var inc = -diff;
                        await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.ProductVariant
SET StockQty = StockQty + {inc}
WHERE VariantId = {variantId};
", ct);
                    }

                    existing.Quantity = desiredQty;
                    existing.Status = ResStatusReserved;
                    existing.ReservedUntilUtc = reservedUntilUtc;
                    existing.UpdatedAtUtc = nowUtc;

                    await db.SaveChangesAsync(ct);
                }
            }

            // ✅ Sync stock + status ngay sau reserve (variant + product)
            await SyncStockAndStatusesAsync(db, affectedVariantIds, nowUtc, ct);

            if (tx != null)
                await tx.CommitAsync(ct);
        }

        public async Task ExtendReservationAsync(
            KeytietkiemDbContext db,
            Guid orderId,
            DateTime newReservedUntilUtc,
            DateTime nowUtc,
            CancellationToken ct = default)
        {
            if (!db.Database.IsRelational()) return;

            // Lấy variantIds để sync (extend không đổi qty nhưng ảnh hưởng validity)
            var affectedVariantIds = await db.Set<OrderInventoryReservation>()
                .AsNoTracking()
                .Where(r => r.OrderId == orderId && r.Status == ResStatusReserved)
                .Select(r => r.VariantId)
                .Distinct()
                .ToListAsync(ct);

            var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.OrderInventoryReservation
SET ReservedUntilUtc = {newReservedUntilUtc},
    UpdatedAtUtc = {nowUtc}
WHERE OrderId = {orderId}
  AND Status = {ResStatusReserved};
", ct);

            if (rows == 0)
                throw new InvalidOperationException("No active reservation to extend.");

            // ✅ Sync stock + status ngay sau extend
            await SyncStockAndStatusesAsync(db, affectedVariantIds, nowUtc, ct);
        }

        public async Task ReleaseReservationAsync(
            KeytietkiemDbContext db,
            Guid orderId,
            DateTime nowUtc,
            CancellationToken ct = default)
        {
            if (!db.Database.IsRelational()) return;

            await using var tx = await BeginSerializableIfNoTxAsync(db, ct);

            var affectedVariantIds = await db.Set<OrderInventoryReservation>()
                .AsNoTracking()
                .Where(r => r.OrderId == orderId
                            && r.Status == ResStatusReserved
                            && r.Quantity > 0)
                .Select(r => r.VariantId)
                .Distinct()
                .ToListAsync(ct);

            // 1) Cộng lại StockQty cho các dòng đang Reserved
            await db.Database.ExecuteSqlInterpolatedAsync($@"
;WITH r AS (
    SELECT VariantId, Quantity
    FROM dbo.OrderInventoryReservation WITH (UPDLOCK, HOLDLOCK)
    WHERE OrderId = {orderId}
      AND Status = {ResStatusReserved}
      AND Quantity > 0
)
UPDATE pv
SET pv.StockQty = pv.StockQty + r.Quantity
FROM dbo.ProductVariant pv
INNER JOIN r ON r.VariantId = pv.VariantId;
", ct);

            // 2) Mark Released (idempotent)
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.OrderInventoryReservation
SET Status = {ResStatusReleased},
    UpdatedAtUtc = {nowUtc}
WHERE OrderId = {orderId}
  AND Status = {ResStatusReserved};
", ct);

            // ✅ Sync stock + status ngay sau release
            await SyncStockAndStatusesAsync(db, affectedVariantIds, nowUtc, ct);

            if (tx != null)
                await tx.CommitAsync(ct);
        }

        public async Task ReleaseExpiredReservationsAsync(
            KeytietkiemDbContext db,
            DateTime nowUtc,
            CancellationToken ct = default)
        {
            if (!db.Database.IsRelational()) return;

            await using var tx = await BeginSerializableIfNoTxAsync(db, ct);

            var affectedVariantIds = await db.Set<OrderInventoryReservation>()
                .AsNoTracking()
                .Where(r => r.Status == ResStatusReserved
                            && r.ReservedUntilUtc < nowUtc
                            && r.Quantity > 0)
                .Select(r => r.VariantId)
                .Distinct()
                .ToListAsync(ct);

            await db.Database.ExecuteSqlInterpolatedAsync($@"
;WITH r AS (
    SELECT OrderId, VariantId, Quantity
    FROM dbo.OrderInventoryReservation WITH (UPDLOCK, HOLDLOCK)
    WHERE Status = {ResStatusReserved}
      AND ReservedUntilUtc < {nowUtc}
      AND Quantity > 0
)
UPDATE pv
SET pv.StockQty = pv.StockQty + r.Quantity
FROM dbo.ProductVariant pv
INNER JOIN r ON r.VariantId = pv.VariantId;
", ct);

            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.OrderInventoryReservation
SET Status = {ResStatusReleased},
    UpdatedAtUtc = {nowUtc}
WHERE Status = {ResStatusReserved}
  AND ReservedUntilUtc < {nowUtc};
", ct);

            // ✅ Sync stock + status ngay sau release expired
            await SyncStockAndStatusesAsync(db, affectedVariantIds, nowUtc, ct);

            if (tx != null)
                await tx.CommitAsync(ct);
        }

        public Task FinalizeReservationAsync(
            KeytietkiemDbContext db,
            Guid orderId,
            DateTime nowUtc,
            CancellationToken ct = default)
        {
            if (!db.Database.IsRelational()) return Task.CompletedTask;

            // Finalize = Paid but not yet fulfilled.
            // NOTE: do NOT sync here to avoid brief "stock jump" window if recalculator only subtracts Reserved.
            // Our VariantStockRecalculator already subtracts Finalized, so stock stays locked.
            return db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.OrderInventoryReservation
SET Status = {ResStatusFinalized},
    UpdatedAtUtc = {nowUtc}
WHERE OrderId = {orderId}
  AND Status = {ResStatusReserved};
", ct);
        }

        /// <summary>
        /// ✅ After fulfill success:
        /// Mark reservations (Finalized/Reserved) as Consumed so they no longer subtract stock.
        /// Raw inventory already decreased (keys sold / account slots assigned), so we must NOT add stock back.
        /// </summary>
        public async Task ConsumeReservationAfterFulfillAsync(
            KeytietkiemDbContext db,
            Guid orderId,
            DateTime nowUtc,
            CancellationToken ct = default)
        {
            if (!db.Database.IsRelational()) return;

            await using var tx = await BeginSerializableIfNoTxAsync(db, ct);

            var affectedVariantIds = await db.Set<OrderInventoryReservation>()
                .AsNoTracking()
                .Where(r => r.OrderId == orderId
                            && r.Quantity > 0
                            && (
                                r.Status == ResStatusFinalized || r.Status == "FINALIZED"
                                || r.Status == ResStatusReserved || r.Status == "RESERVED"
                            ))
                .Select(r => r.VariantId)
                .Distinct()
                .ToListAsync(ct);

            // Mark Consumed (idempotent-ish)
            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.OrderInventoryReservation
SET Status = {ResStatusConsumed},
    UpdatedAtUtc = {nowUtc}
WHERE OrderId = {orderId}
  AND (
        Status = {ResStatusFinalized} OR Status = 'FINALIZED'
        OR Status = {ResStatusReserved} OR Status = 'RESERVED'
      );
", ct);

            // Sync stock/status after consume
            await SyncStockAndStatusesAsync(db, affectedVariantIds, nowUtc, ct);

            if (tx != null)
                await tx.CommitAsync(ct);
        }
    }
}
