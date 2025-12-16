using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Services
{
    public class InventoryReservationService : IInventoryReservationService
    {
        private const string ResStatusReserved = "Reserved";
        private const string ResStatusReleased = "Released";
        private const string ResStatusFinalized = "Finalized";

        private static async Task<IDbContextTransaction?> BeginSerializableIfNoTxAsync(
            KeytietkiemDbContext db,
            CancellationToken ct)
        {
            // Nếu caller đã mở transaction thì không mở thêm
            if (db.Database.CurrentTransaction != null) return null;

            // Serializable để tránh race: 2 order cùng reserve -> âm stock
            return await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
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

            foreach (var (variantId, qtyRaw) in lines)
            {
                var desiredQty = qtyRaw <= 0 ? 0 : qtyRaw;
                if (desiredQty == 0) continue;

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
                    // Nếu trước đó Released/Finalized mà gọi reserve lại -> coi như reserve lại,
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

            var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.OrderInventoryReservation
SET ReservedUntilUtc = {newReservedUntilUtc},
    UpdatedAtUtc = {nowUtc}
WHERE OrderId = {orderId}
  AND Status = {ResStatusReserved};
", ct);

            // ✅ quan trọng: nếu không có reservation Reserved nào -> coi như "extend fail"
            if (rows == 0)
                throw new InvalidOperationException("No active reservation to extend.");
        }


        public async Task ReleaseReservationAsync(
            KeytietkiemDbContext db,
            Guid orderId,
            DateTime nowUtc,
            CancellationToken ct = default)
        {
            if (!db.Database.IsRelational()) return;

            await using var tx = await BeginSerializableIfNoTxAsync(db, ct);

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

            // Finalize = giữ nguyên StockQty đã trừ (coi như đã bán),
            // việc gắn key/account cụ thể sẽ làm ở bước fulfillment sau khi Paid.
            return db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.OrderInventoryReservation
SET Status = {ResStatusFinalized},
    UpdatedAtUtc = {nowUtc}
WHERE OrderId = {orderId}
  AND Status = {ResStatusReserved};
", ct);
        }
    }
}
