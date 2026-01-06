// File: Utils/VariantStockRecalculator.cs
// Purpose: Recalculate stock for key/account variants based on real inventory (not expired, not assigned to order)
//          and persist to DB: ProductVariant.StockQty, ProductVariant.Status, Product.Status.
// Notes:
// - Keeps INACTIVE if admin explicitly set.
// - Uses OUT_OF_STOCK when stock <= 0 (never auto set INACTIVE).

using Keytietkiem.Constants;
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
            // Avoid compile-time dependency on UpdatedAt property.
            var prop = entity.GetType().GetProperty("UpdatedAt", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(DateTime))
            {
                prop.SetValue(entity, nowUtc);
            }
        }

        private static void TrySetStockQty(object entity, int stockQty)
        {
            // Avoid compile-time dependency on StockQty property.
            var prop = entity.GetType().GetProperty("StockQty", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite) return;

            var t = prop.PropertyType;

            // Common cases
            if (t == typeof(int)) { prop.SetValue(entity, stockQty); return; }
            if (t == typeof(int?)) { prop.SetValue(entity, (int?)stockQty); return; }
            if (t == typeof(long)) { prop.SetValue(entity, (long)stockQty); return; }
            if (t == typeof(long?)) { prop.SetValue(entity, (long?)stockQty); return; }
            if (t == typeof(short)) { prop.SetValue(entity, (short)stockQty); return; }
            if (t == typeof(short?)) { prop.SetValue(entity, (short?)stockQty); return; }

            // Fallback: try Convert.ChangeType for other numeric types (decimal, etc.)
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
        /// Bulk sync convenience wrapper.
        /// Fixes ProductKeyController cases that pass a list of VariantId.
        /// </summary>
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

        /// <summary>
        /// Sync stock for a single variant and persist:
        /// - ProductVariant.StockQty
        /// - ProductVariant.Status (ACTIVE / OUT_OF_STOCK; keep INACTIVE)
        /// - Product.Status (ACTIVE / OUT_OF_STOCK; keep INACTIVE)
        ///
        /// Rules:
        /// PERSONAL_KEY      : count keys Available, not assigned, not expired
        /// PERSONAL_ACCOUNT  : count accounts Active, MaxUsers=1, not expired, no active customer
        /// SHARED_ACCOUNT    : sum available slots of Active accounts MaxUsers>1, not expired
        /// Then subtract reservations (OrderInventoryReservation) still valid.
        /// </summary>
        public static async Task<VariantStockSyncResult?> SyncVariantStockAndStatusAsync(
            KeytietkiemDbContext db,
            Guid variantId,
            DateTime nowUtc,
            CancellationToken ct = default)
        {
            // Load tracked variant + product
            var variant = await db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.VariantId == variantId, ct);

            if (variant == null || variant.Product == null)
                return null;

            var product = variant.Product;
            var productType = (product.ProductType ?? string.Empty).Trim();

            // Only support key/account product types
            var isSupported = productType == ProductEnums.PERSONAL_KEY
                              || productType == ProductEnums.PERSONAL_ACCOUNT
                              || productType == ProductEnums.SHARED_ACCOUNT;

            if (!isSupported)
                return null;

            // Reserved quantity (pending checkout)
            // NOTE: Keep it SQL-translatable (avoid StringComparison).
            var reservedQty = await db.Set<OrderInventoryReservation>()
                .AsNoTracking()
                .Where(r => r.VariantId == variantId
                            && r.ReservedUntilUtc > nowUtc
                            && (r.Status == "Reserved" || r.Status == "RESERVED"))
                .SumAsync(r => (int?)r.Quantity, ct) ?? 0;

            int rawQty = 0;

            if (productType == ProductEnums.PERSONAL_KEY)
            {
                rawQty = await db.Set<ProductKey>()
                    .AsNoTracking()
                    .Where(k => k.VariantId == variantId
                                && k.Status == "Available"
                                && k.AssignedToOrderId == null
                                && (!k.ExpiryDate.HasValue || k.ExpiryDate.Value >= nowUtc))
                    .CountAsync(ct);
            }
            else if (productType == ProductEnums.PERSONAL_ACCOUNT)
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
            else if (productType == ProductEnums.SHARED_ACCOUNT)
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

            var res = new VariantStockSyncResult
            {
                VariantId = variant.VariantId,
                ProductId = variant.ProductId,
                ProductType = productType,
                OldStockQty = (int)variant.StockQty,
                NewStockQty = newStock,
                OldVariantStatus = variant.Status,
                OldProductStatus = product.Status,
                ReservedQty = reservedQty,
                RawQty = rawQty
            };

            // Update variant stock
            variant.StockQty = newStock;

            // Update variant status (keep INACTIVE)
            var curVariantStatus = (variant.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (curVariantStatus != "INACTIVE")
            {
                if (newStock <= 0)
                {
                    variant.Status = "OUT_OF_STOCK";
                }
                else
                {
                    // When back in stock, don't keep OUT_OF_STOCK.
                    // Keep other valid statuses if any, otherwise set ACTIVE.
                    if (!string.IsNullOrWhiteSpace(curVariantStatus)
                        && ProductEnums.Statuses.Contains(curVariantStatus)
                        && curVariantStatus != "OUT_OF_STOCK"
                        && curVariantStatus != "INACTIVE")
                    {
                        variant.Status = curVariantStatus;
                    }
                    else
                    {
                        variant.Status = "ACTIVE";
                    }
                }
            }

            // Recalc product total stock (sum of all variants) and persist to DB if Product has StockQty.
            // NOTE: we use "otherStock + newStock" so the current variant uses the freshly computed value.
            var otherStock = await db.ProductVariants
                .AsNoTracking()
                .Where(v => v.ProductId == product.ProductId && v.VariantId != variantId)
                .SumAsync(v => (int?)v.StockQty, ct) ?? 0;

            var totalStock = otherStock + newStock;
            if (totalStock < 0) totalStock = 0;

            TrySetStockQty(product, totalStock);

            // Recalc product status from total stock
            var curProductStatus = (product.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (curProductStatus != "INACTIVE")
            {
                product.Status = totalStock <= 0 ? "OUT_OF_STOCK" : "ACTIVE";
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
