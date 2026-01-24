// File: Infrastructure/ExpiryStatusUpdateJob.cs
// ✅ Reused as the SINGLE maintenance job for key/account inventory
// - Keep: auto-expire keys/accounts + sync Product/ProductVariant StockQty/Status by REAL inventory
// - AFTER expiring: count ACTIVE inventory and persist StockQty + Status
//   ✅ IMPORTANT FIX: for SHARED_KEY / SHARED_ACCOUNT => count by AVAILABLE SLOTS (capacity - used)
// - IMPORTANT: do NOT override Product/ProductVariant status if they were manually set to INACTIVE

using Keytietkiem.Utils.Constants;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Infrastructure
{
    public class ExpiryStatusUpdateJob : IBackgroundJob
    {
        private readonly ILogger<ExpiryStatusUpdateJob> _logger;

        // Tune if needed
        private const int BatchSize = 200;

        // ✅ Status conventions (MATCH ProductsController + DB CHECK constraint)
        private const string StatusActive = "ACTIVE";
        private const string StatusOutOfStock = "OUT_OF_STOCK";
        private const string StatusInactive = "INACTIVE";

        // Reflection cache for slot props
        private static readonly ConcurrentDictionary<string, PropertyInfo?> _propCache = new();

        public ExpiryStatusUpdateJob(ILogger<ExpiryStatusUpdateJob> logger)
        {
            _logger = logger;
        }

        public string Name => "ExpiryStatusUpdateJob";

        // ✅ Near-realtime sync (adjust if system is heavy)
        public TimeSpan Interval => TimeSpan.FromSeconds(30);

        public async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            try
            {
                var dbFactory = serviceProvider.GetRequiredService<IDbContextFactory<KeytietkiemDbContext>>();
                var nowUtc = DateTime.UtcNow;

                // 1) Auto-expire ProductAccounts + ProductKeys (for admin visibility)
                var (expiredAccountVariants, expiredKeyVariants, expiredAccountsCount, expiredKeysCount)
                    = await AutoExpireKeysAndAccountsAsync(dbFactory, nowUtc, cancellationToken);

                // 2) AFTER expiring: recount active keys/accounts (shared => slots) and persist StockQty + Status
                var syncedVariants = await RecountAndSyncKeyAccountInventoryAsync(dbFactory, nowUtc, cancellationToken);

                if (expiredAccountsCount > 0 || expiredKeysCount > 0 || syncedVariants > 0)
                {
                    _logger.LogInformation(
                        "ExpiryStatusUpdateJob: expiredAccounts={ExpiredAccounts}, expiredKeys={ExpiredKeys}, affectedVariants(Expire)={AffectedVariants}, syncedVariants(Recount)={SyncedVariants}",
                        expiredAccountsCount,
                        expiredKeysCount,
                        expiredAccountVariants.Count + expiredKeyVariants.Count,
                        syncedVariants);
                }
                else
                {
                    _logger.LogInformation("ExpiryStatusUpdateJob: no changes.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing ExpiryStatusUpdateJob.");
                throw;
            }
        }

        private static bool IsInactiveLike(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            var s = status.Trim();

            // ✅ match ProductsController + keep backward compatibility
            return s.Equals(StatusInactive, StringComparison.OrdinalIgnoreCase)
                || s.Equals("Inactive", StringComparison.OrdinalIgnoreCase);
        }

        // ✅ match ProductsController rule:
        // - INACTIVE => keep
        // - stock <= 0 => OUT_OF_STOCK
        // - stock > 0 => ACTIVE
        private static string ResolveStatusFromStock(int stock, string? currentStatus)
        {
            var cur = (currentStatus ?? string.Empty).Trim();
            if (IsInactiveLike(cur))
            {
                // keep original (normalize to DB convention if needed)
                if (cur.Equals("Inactive", StringComparison.OrdinalIgnoreCase)) return StatusInactive;
                return cur.ToUpperInvariant();
            }

            return stock <= 0 ? StatusOutOfStock : StatusActive;
        }

        private static bool IsSharedAccountType(string? productType)
            => !string.IsNullOrWhiteSpace(productType)
               && productType.Trim().Equals(ProductEnums.SHARED_ACCOUNT, StringComparison.OrdinalIgnoreCase);

        private static bool IsSharedKeyType(string? productType)
            => !string.IsNullOrWhiteSpace(productType)
               && productType.Trim().Equals(ProductEnums.SHARED_KEY, StringComparison.OrdinalIgnoreCase);

        private static bool IsAccountType(string? productType)
            => !string.IsNullOrWhiteSpace(productType)
               && (productType.Trim().Equals(ProductEnums.PERSONAL_ACCOUNT, StringComparison.OrdinalIgnoreCase)
                   || productType.Trim().Equals(ProductEnums.SHARED_ACCOUNT, StringComparison.OrdinalIgnoreCase));

        private static bool IsKeyType(string? productType)
            => !string.IsNullOrWhiteSpace(productType)
               && (productType.Trim().Equals(ProductEnums.PERSONAL_KEY, StringComparison.OrdinalIgnoreCase)
                   || productType.Trim().Equals(ProductEnums.SHARED_KEY, StringComparison.OrdinalIgnoreCase));

        private static string? FindFirstProp(IEntityType? et, params string[] candidates)
        {
            if (et == null) return null;
            foreach (var c in candidates)
            {
                if (et.FindProperty(c) != null) return c;
            }
            return null;
        }

        // ✅ FIX CS8116: không pattern match nullable (int?/long?) trong "is"
        // Boxed Nullable<T> có value sẽ thành T, null thì val==null
        private static int GetIntPropCached(object obj, string propName, int fallback)
        {
            if (obj == null || string.IsNullOrWhiteSpace(propName)) return fallback;

            var type = obj.GetType();
            var cacheKey = $"{type.FullName}:{propName}";

            var prop = _propCache.GetOrAdd(
                cacheKey,
                _ => type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase));

            if (prop == null) return fallback;

            var val = prop.GetValue(obj);
            if (val == null) return fallback;

            try
            {
                if (val is int i) return i;

                if (val is long l)
                {
                    if (l > int.MaxValue) return int.MaxValue;
                    if (l < int.MinValue) return int.MinValue;
                    return (int)l;
                }

                if (val is short s) return s;
                if (val is byte b) return b;

                if (val is decimal d)
                {
                    if (d > int.MaxValue) return int.MaxValue;
                    if (d < int.MinValue) return int.MinValue;
                    return (int)d;
                }

                if (val is double dd)
                {
                    if (dd > int.MaxValue) return int.MaxValue;
                    if (dd < int.MinValue) return int.MinValue;
                    return (int)dd;
                }

                if (val is float ff)
                {
                    if (ff > int.MaxValue) return int.MaxValue;
                    if (ff < int.MinValue) return int.MinValue;
                    return (int)ff;
                }

                // fallback parse
                if (int.TryParse(val.ToString(), out var parsed)) return parsed;

                // last resort for convertible types
                if (val is IConvertible)
                {
                    var conv = Convert.ToInt32(val);
                    return conv;
                }
            }
            catch
            {
                // ignore
            }

            return fallback;
        }

        private static async Task<(HashSet<Guid> ExpiredAccountVariantIds,
                                   HashSet<Guid> ExpiredKeyVariantIds,
                                   int ExpiredAccountsCount,
                                   int ExpiredKeysCount)>
            AutoExpireKeysAndAccountsAsync(
                IDbContextFactory<KeytietkiemDbContext> dbFactory,
                DateTime nowUtc,
                CancellationToken ct)
        {
            await using var context = await dbFactory.CreateDbContextAsync(ct);

            var expiredAccountVariantIds = new HashSet<Guid>();
            var expiredKeyVariantIds = new HashSet<Guid>();

            // 1) ProductAccount -> Expired
            var expiredAccounts = await context.ProductAccounts
                .Where(a => a.Status != nameof(ProductAccountStatus.Expired)
                            && a.ExpiryDate != null
                            && a.ExpiryDate < nowUtc)
                .Select(a => new { a.ProductAccountId, a.VariantId })
                .ToListAsync(ct);

            if (expiredAccounts.Count > 0)
            {
                foreach (var x in expiredAccounts)
                    if (x.VariantId != Guid.Empty) expiredAccountVariantIds.Add(x.VariantId);

                var ids = expiredAccounts.Select(x => x.ProductAccountId).ToList();

                var tracked = await context.ProductAccounts
                    .Where(a => ids.Contains(a.ProductAccountId))
                    .ToListAsync(ct);

                foreach (var acc in tracked)
                {
                    acc.Status = nameof(ProductAccountStatus.Expired);
                    acc.UpdatedAt = nowUtc;
                }

                await context.SaveChangesAsync(ct);
            }

            // 2) ProductKey -> Expired
            var expiredKeys = await context.ProductKeys
                .Where(k => k.Status != nameof(ProductKeyStatus.Expired)
                            && k.ExpiryDate != null
                            && k.ExpiryDate < nowUtc)
                .Select(k => new { k.KeyId, k.VariantId })
                .ToListAsync(ct);

            if (expiredKeys.Count > 0)
            {
                foreach (var x in expiredKeys)
                    if (x.VariantId != Guid.Empty) expiredKeyVariantIds.Add(x.VariantId);

                var ids = expiredKeys.Select(x => x.KeyId).ToList();

                var tracked = await context.ProductKeys
                    .Where(k => ids.Contains(k.KeyId))
                    .ToListAsync(ct);

                foreach (var key in tracked)
                {
                    key.Status = nameof(ProductKeyStatus.Expired);
                    key.UpdatedAt = nowUtc;
                }

                await context.SaveChangesAsync(ct);
            }

            return (expiredAccountVariantIds, expiredKeyVariantIds, expiredAccounts.Count, expiredKeys.Count);
        }

        private static async Task<int> RecountAndSyncKeyAccountInventoryAsync(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            DateTime nowUtc,
            CancellationToken ct)
        {
            // ✅ Load ALL variantIds that belong to key/account product types
            List<Guid> variantIds;
            await using (var db = await dbFactory.CreateDbContextAsync(ct))
            {
                var keyAccountTypes = new[]
                {
                    ProductEnums.PERSONAL_KEY,
                    ProductEnums.SHARED_KEY,
                    ProductEnums.PERSONAL_ACCOUNT,
                    ProductEnums.SHARED_ACCOUNT
                };

                variantIds = await db.ProductVariants
                    .AsNoTracking()
                    .Where(v =>
                        v.Product != null &&
                        v.Product.ProductType != null &&
                        keyAccountTypes.Contains(v.Product.ProductType))
                    .Select(v => v.VariantId)
                    .Distinct()
                    .ToListAsync(ct);
            }

            if (variantIds.Count == 0) return 0;

            var total = variantIds.Count;
            var touchedVariantCount = 0;

            // ✅ Recount & update VARIANTS in batches
            for (var i = 0; i < total; i += BatchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batchIds = variantIds.Skip(i).Take(BatchSize).ToList();

                await using var dbBatch = await dbFactory.CreateDbContextAsync(ct);

                // detect slot props (optional) - if your entities have these columns, we use them
                var paEntity = dbBatch.Model.FindEntityType(typeof(ProductAccount));
                var pkEntity = dbBatch.Model.FindEntityType(typeof(ProductKey));

                // capacity candidates (shared accounts/keys)
                var accountCapacityProp = FindFirstProp(paEntity,
                    "SlotLimit", "MaxSlots", "TotalSlots", "Capacity", "SlotCount", "MaxUsers", "MaxUser", "MaxSharing");

                var keyCapacityProp = FindFirstProp(pkEntity,
                    "SlotLimit", "MaxSlots", "TotalSlots", "Capacity", "SlotCount", "MaxActivations", "ActivationLimit", "MaxUses");

                // used candidates for shared key (if you store used count directly on ProductKey)
                var keyUsedProp = FindFirstProp(pkEntity,
                    "UsedSlots", "UsedCount", "ActivatedCount", "ConsumedSlots", "CurrentUsed", "SoldCount", "AssignedCount");

                // track variants + product so we can decide whether to change Status (skip if inactive)
                var variants = await dbBatch.ProductVariants
                    .Include(v => v.Product)
                    .Where(v => batchIds.Contains(v.VariantId))
                    .ToListAsync(ct);

                if (variants.Count == 0) continue;

                var productTypeByVariantId = variants.ToDictionary(
                    v => v.VariantId,
                    v => v.Product?.ProductType ?? string.Empty);

                // ✅ reservation (subtract like ProductsController)
                var reservedByVariantId = await dbBatch.Set<OrderInventoryReservation>()
                    .AsNoTracking()
                    .Where(r => batchIds.Contains(r.VariantId)
                                && r.ReservedUntilUtc > nowUtc
                                && r.Status == "Reserved")
                    .GroupBy(r => r.VariantId)
                    .Select(g => new { VariantId = g.Key, Qty = g.Sum(x => x.Quantity) })
                    .ToDictionaryAsync(x => x.VariantId, x => x.Qty, ct);

                // ===== Load KEYS for batch (single fetch) =====
                // ✅ Align to controller: only Available, then we handle personal/shared rules in compute
                var keys = await dbBatch.ProductKeys
                    .AsNoTracking()
                    .Where(k =>
                        batchIds.Contains(k.VariantId)
                        && k.Status == nameof(ProductKeyStatus.Available)
                        && (!k.ExpiryDate.HasValue || k.ExpiryDate.Value >= nowUtc))
                    .ToListAsync(ct);

                var keysByVariant = keys
                    .GroupBy(k => k.VariantId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // ===== Load ACCOUNTS for batch (single fetch) =====
                // ✅ Align to controller: only Active
                var accounts = await dbBatch.ProductAccounts
                    .AsNoTracking()
                    .Where(a =>
                        batchIds.Contains(a.VariantId)
                        && a.Status == nameof(ProductAccountStatus.Active)
                        && (!a.ExpiryDate.HasValue || a.ExpiryDate.Value >= nowUtc))
                    .ToListAsync(ct);

                var accountsByVariant = accounts
                    .GroupBy(a => a.VariantId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // ===== used count for accounts via ProductAccountCustomers (single query) =====
                // ✅ Align to controller: count ONLY active customers
                var accountIds = accounts.Select(a => a.ProductAccountId).Distinct().ToList();
                var usedByAccountId = new Dictionary<Guid, int>();

                if (accountIds.Count > 0)
                {
                    usedByAccountId = await dbBatch.ProductAccountCustomers
                        .AsNoTracking()
                        .Where(pac => accountIds.Contains(pac.ProductAccountId) && pac.IsActive)
                        .GroupBy(pac => pac.ProductAccountId)
                        .Select(g => new { ProductAccountId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.ProductAccountId, x => x.Count, ct);
                }

                // ===== Compute stockByVariant (PERSONAL => count items, SHARED => count remaining slots) =====
                var stockByVariantId = new Dictionary<Guid, int>();

                foreach (var vid in batchIds)
                {
                    ct.ThrowIfCancellationRequested();

                    productTypeByVariantId.TryGetValue(vid, out var pt);
                    pt ??= string.Empty;

                    var raw = 0;

                    if (IsKeyType(pt))
                    {
                        if (keysByVariant.TryGetValue(vid, out var list) && list != null && list.Count > 0)
                        {
                            var isShared = IsSharedKeyType(pt);

                            foreach (var k in list)
                            {
                                // PERSONAL_KEY
                                if (!isShared)
                                {
                                    if (k.AssignedToOrderId == null) raw += 1;
                                    continue;
                                }

                                // SHARED_KEY: count by slots remaining
                                var cap = keyCapacityProp != null ? GetIntPropCached(k, keyCapacityProp, 1) : 1;
                                if (cap < 1) cap = 1;

                                int used;
                                if (keyUsedProp != null)
                                {
                                    used = GetIntPropCached(k, keyUsedProp, 0);
                                    if (used < 0) used = 0;
                                }
                                else
                                {
                                    // fallback: treat as used=1 when assigned
                                    used = k.AssignedToOrderId != null ? 1 : 0;
                                }

                                var remaining = cap - used;
                                if (remaining < 0) remaining = 0;

                                raw += remaining;
                            }
                        }
                    }
                    else if (IsAccountType(pt))
                    {
                        if (accountsByVariant.TryGetValue(vid, out var list) && list != null && list.Count > 0)
                        {
                            var isShared = IsSharedAccountType(pt);

                            foreach (var a in list)
                            {
                                usedByAccountId.TryGetValue(a.ProductAccountId, out var used);

                                if (!isShared)
                                {
                                    // PERSONAL_ACCOUNT: available if no active customer
                                    if (used <= 0) raw += 1;
                                    continue;
                                }

                                // SHARED_ACCOUNT: count by slots remaining
                                var cap = accountCapacityProp != null ? GetIntPropCached(a, accountCapacityProp, 1) : 1;
                                if (cap < 1) cap = 1;

                                var remaining = cap - Math.Max(used, 0);
                                if (remaining < 0) remaining = 0;

                                raw += remaining;
                            }
                        }
                    }

                    // ✅ subtract reservation like ProductsController
                    var reserved = reservedByVariantId.TryGetValue(vid, out var rq) ? rq : 0;
                    var available = raw - reserved;
                    if (available < 0) available = 0;

                    stockByVariantId[vid] = available;
                }

                // ===== Persist StockQty + Status (do not override inactive
                var anyChange = false;

                foreach (var v in variants)
                {
                    ct.ThrowIfCancellationRequested();

                    var stock = stockByVariantId.TryGetValue(v.VariantId, out var s) ? s : 0;

                    // StockQty: always persist (even if inactive)
                    if (v.StockQty != stock)
                    {
                        v.StockQty = stock;
                        anyChange = true;
                    }

                    // Status: ONLY update if BOTH variant & product are not inactive
                    var variantInactive = IsInactiveLike(v.Status);
                    var productInactive = IsInactiveLike(v.Product?.Status);

                    if (!variantInactive && !productInactive)
                    {
                        var newStatus = ResolveStatusFromStock(stock, v.Status);
                        if (!string.Equals(v.Status, newStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            v.Status = newStatus;
                            anyChange = true;
                        }
                    }
                }

                if (anyChange)
                {
                    await dbBatch.SaveChangesAsync(ct);
                    touchedVariantCount += variants.Count;
                }
            }

            // ✅ Recount & update PRODUCTS: StockQty = sum(variant.StockQty) for those key/account variants
            await using (var dbProd = await dbFactory.CreateDbContextAsync(ct))
            {
                var prodSums = await dbProd.ProductVariants
                    .AsNoTracking()
                    .Where(v => variantIds.Contains(v.VariantId))
                    .GroupBy(v => v.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        Stock = g.Sum(x => (int?)x.StockQty) ?? 0
                    })
                    .ToListAsync(ct);

                if (prodSums.Count > 0)
                {
                    var productIds = prodSums.Select(x => x.ProductId).Distinct().ToList();
                    var stockMap = prodSums.ToDictionary(x => x.ProductId, x => x.Stock);

                    var products = await dbProd.Products
                        .Where(p => productIds.Contains(p.ProductId))
                        .ToListAsync(ct);

                    var anyChange = false;

                    foreach (var p in products)
                    {
                        ct.ThrowIfCancellationRequested();

                        var stock = stockMap.TryGetValue(p.ProductId, out var s) ? s : 0;

                        if (p.StockQty != stock)
                        {
                            p.StockQty = stock;
                            anyChange = true;
                        }

                        // Status: do not override if product inactive
                        if (!IsInactiveLike(p.Status))
                        {
                            var newStatus = ResolveStatusFromStock(stock, p.Status);
                            if (!string.Equals(p.Status, newStatus, StringComparison.OrdinalIgnoreCase))
                            {
                                p.Status = newStatus;
                                anyChange = true;
                            }
                        }
                    }

                    if (anyChange)
                        await dbProd.SaveChangesAsync(ct);
                }
            }

            return touchedVariantCount;
        }
    }
}
