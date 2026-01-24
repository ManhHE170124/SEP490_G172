// File: Utils/VariantStockRecalculator.cs
// Purpose: Recalculate stock for key/account variants based on real inventory (not expired, not assigned to order)
//          and persist to DB: ProductVariant.StockQty, ProductVariant.Status, Product.Status.
// Notes:
// - Keeps INACTIVE if admin explicitly set (only when stock > 0).
// - Uses OUT_OF_STOCK when stock <= 0 (always overrides INACTIVE).

using Keytietkiem.Utils.Constants;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Utils
{
    public class VariantStockSyncResult
    {
        public Guid VariantId { get; set; }
        public Guid ProductId { get; set; }
        public string? ProductType { get; set; }

        public int OldStockQty { get; set; }
        public int NewStockQty { get; set; }

        public string? OldVariantStatus { get; set; }
        public string? NewVariantStatus { get; set; }

        public string? OldProductStatus { get; set; }
        public string? NewProductStatus { get; set; }

        public int ReservedQty { get; set; }
        public int RawQty { get; set; }
    }

    public static class VariantStockRecalculator
    {
        private static void TrySetUpdatedAt(object entity, DateTime nowUtc)
        {
            var prop = entity.GetType().GetProperty("UpdatedAt", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(DateTime))
            {
                prop.SetValue(entity, nowUtc);
            }
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

        public static async Task SyncVariantStockAndStatusAsync(
            KeytietkiemDbContext db,
            IEnumerable<Guid> variantIds,
            DateTime nowUtc,
            CancellationToken ct = default)
        {
            if (variantIds == null) return;

            var ids = variantIds.Where(x => x != Guid.Empty).Distinct().ToList();
            foreach (var vid in ids)
            {
                ct.ThrowIfCancellationRequested();
                await SyncVariantStockAndStatusAsync(db, vid, nowUtc, ct);
            }
        }

        public static async Task<VariantStockSyncResult?> SyncVariantStockAndStatusAsync(
            KeytietkiemDbContext db,
            Guid variantId,
            DateTime nowUtc,
            CancellationToken ct = default)
        {
            var variant = await db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.VariantId == variantId, ct);

            if (variant == null || variant.Product == null)
                return null;

            var product = variant.Product;
            var productType = (product.ProductType ?? string.Empty).Trim();

            // ✅ Support BOTH PERSONAL_KEY and SHARED_KEY if your system uses it
            var isSupported = productType.Equals(ProductEnums.PERSONAL_KEY, StringComparison.OrdinalIgnoreCase)
                              || productType.Equals(ProductEnums.SHARED_KEY, StringComparison.OrdinalIgnoreCase)
                              || productType.Equals(ProductEnums.PERSONAL_ACCOUNT, StringComparison.OrdinalIgnoreCase)
                              || productType.Equals(ProductEnums.SHARED_ACCOUNT, StringComparison.OrdinalIgnoreCase);

            if (!isSupported)
                return null;

            // ✅ Reserved quantity:
            // - Reserved: only counts when ReservedUntilUtc > nowUtc
            // - Finalized: always counts until consumed after fulfill
            var reservedQty = await db.Set<OrderInventoryReservation>()
                .AsNoTracking()
                .Where(r => r.VariantId == variantId
                            && r.Quantity > 0
                            && (
                                (
                                    (r.Status == "Reserved" || r.Status == "RESERVED")
                                    && r.ReservedUntilUtc > nowUtc
                                )
                                ||
                                (r.Status == "Finalized" || r.Status == "FINALIZED")
                            ))
                .SumAsync(r => (int?)r.Quantity, ct) ?? 0;

            int rawQty = 0;

            // ✅ Keys
            if (productType.Equals(ProductEnums.PERSONAL_KEY, StringComparison.OrdinalIgnoreCase)
                || productType.Equals(ProductEnums.SHARED_KEY, StringComparison.OrdinalIgnoreCase))
            {
                rawQty = await db.Set<ProductKey>()
                    .AsNoTracking()
                    .Where(k => k.VariantId == variantId
                                && k.Status == "Available"
                                && k.AssignedToOrderId == null
                                && (!k.ExpiryDate.HasValue || k.ExpiryDate.Value >= nowUtc))
                    .CountAsync(ct);
            }
            // ✅ Personal account = count accounts free (MaxUsers=1, no active customer)
            else if (productType.Equals(ProductEnums.PERSONAL_ACCOUNT, StringComparison.OrdinalIgnoreCase))
            {
                rawQty = await db.Set<ProductAccount>()
                    .AsNoTracking()
                    .Where(pa => pa.VariantId == variantId
                                 && pa.Status == "Active"
                                 && pa.MaxUsers == 1
                                 && (!pa.ExpiryDate.HasValue || pa.ExpiryDate.Value >= nowUtc)
                                 && !pa.ProductAccountCustomers.Any(pac => pac.IsActive))
                    .CountAsync(ct);
            }
            // ✅ Shared account = sum free slots
            else if (productType.Equals(ProductEnums.SHARED_ACCOUNT, StringComparison.OrdinalIgnoreCase))
            {
                rawQty = await db.Set<ProductAccount>()
                    .AsNoTracking()
                    .Where(pa => pa.VariantId == variantId
                                 && pa.Status == "Active"
                                 && pa.MaxUsers > 1
                                 && (!pa.ExpiryDate.HasValue || pa.ExpiryDate.Value >= nowUtc))
                    .Select(pa => pa.MaxUsers - pa.ProductAccountCustomers.Count(pac => pac.IsActive))
                    .Where(av => av > 0)
                    .SumAsync(ct);
            }

            var newStock = rawQty - reservedQty;
            if (newStock < 0) newStock = 0;

            int oldStock = 0;
            try { oldStock = Convert.ToInt32(variant.StockQty); } catch { oldStock = 0; }

            var res = new VariantStockSyncResult
            {
                VariantId = variant.VariantId,
                ProductId = variant.ProductId,
                ProductType = productType,
                OldStockQty = oldStock,
                NewStockQty = newStock,
                OldVariantStatus = variant.Status,
                OldProductStatus = product.Status,
                ReservedQty = reservedQty,
                RawQty = rawQty
            };

            // Update variant stock
            variant.StockQty = newStock;

            // ✅ Variant status rule (ACTIVE/OUT_OF_STOCK/INACTIVE)
            var curVariantStatus = (variant.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (newStock <= 0)
            {
                variant.Status = "OUT_OF_STOCK";
            }
            else
            {
                variant.Status = (curVariantStatus == "INACTIVE") ? "INACTIVE" : "ACTIVE";
            }

            // Recalc product total stock
            var otherStock = await db.ProductVariants
                .AsNoTracking()
                .Where(v => v.ProductId == product.ProductId && v.VariantId != variantId)
                .SumAsync(v => (int?)v.StockQty, ct) ?? 0;

            var totalStock = otherStock + newStock;
            if (totalStock < 0) totalStock = 0;

            TrySetStockQty(product, totalStock);

            // ✅ Product status rule
            var curProductStatus = (product.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (totalStock <= 0)
            {
                product.Status = "OUT_OF_STOCK";
            }
            else
            {
                product.Status = (curProductStatus == "INACTIVE") ? "INACTIVE" : "ACTIVE";
            }

            TrySetUpdatedAt(variant, nowUtc);
            TrySetUpdatedAt(product, nowUtc);

            await db.SaveChangesAsync(ct);

            res.NewVariantStatus = variant.Status;
            res.NewProductStatus = product.Status;

            return res;
        }
    }
}
