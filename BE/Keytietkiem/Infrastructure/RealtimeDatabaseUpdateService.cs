// File: Infrastructure/RealtimeDatabaseUpdateService.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Infrastructure
{
    /// <summary>
    /// Chứa các hàm auto-update DB:
    ///  - Tự huỷ payment Pending quá hạn.
    ///  - Đồng bộ trạng thái product / variant theo tồn kho.
    /// Có thể dùng từ HostedService hoặc controller (nếu muốn).
    /// </summary>
    public interface IRealtimeDatabaseUpdateService
    {
        Task<int> AutoCancelExpiredPendingPaymentsAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        Task<int> SyncProductStockAndStatusAsync(
            CancellationToken cancellationToken = default);
    }

    public class RealtimeDatabaseUpdateService : IRealtimeDatabaseUpdateService
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly ILogger<RealtimeDatabaseUpdateService> _logger;

        private static readonly TimeSpan DefaultPendingPaymentTimeout = TimeSpan.FromMinutes(5);

        public RealtimeDatabaseUpdateService(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            ILogger<RealtimeDatabaseUpdateService> logger)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Huỷ các payment Status = "Pending" quá timeout (mặc định 5 phút).
        /// </summary>
        public async Task<int> AutoCancelExpiredPendingPaymentsAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = DateTime.UtcNow;
            var pendingTimeout = timeout ?? DefaultPendingPaymentTimeout;
            var threshold = nowUtc - pendingTimeout;

            var pendingList = await db.Payments
                .Where(p => p.Status == "Pending" && p.CreatedAt < threshold)
                .ToListAsync(cancellationToken);

            if (!pendingList.Any())
                return 0;

            foreach (var payment in pendingList)
            {
                if (string.Equals(payment.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    payment.Status = "Cancelled";
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "AutoCancelExpiredPendingPaymentsAsync: auto-cancel {Count} pending payment(s) older than {Minutes} minutes.",
                pendingList.Count, pendingTimeout.TotalMinutes);

            return pendingList.Count;
        }

        /// <summary>
        /// Đồng bộ trạng thái product / variant theo tồn kho.
        /// - Variant:
        ///     + stock <= 0  => OUT_OF_STOCK
        ///     + stock > 0 & đang OUT_OF_STOCK => ACTIVE
        ///     + INACTIVE (do admin tắt) thì giữ nguyên.
        /// - Product:
        ///     + tổng stock <= 0 => OUT_OF_STOCK
        ///     + tổng stock > 0 & đang OUT_OF_STOCK => ACTIVE
        ///     + INACTIVE (do admin tắt) thì giữ nguyên.
        /// </summary>
        public async Task<int> SyncProductStockAndStatusAsync(
            CancellationToken cancellationToken = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var changed = 0;

            // ===== 1) Cập nhật trạng thái cho tất cả variants theo StockQty =====
            var variants = await db.ProductVariants.ToListAsync(cancellationToken);

            foreach (var v in variants)
            {
                var stock = v.StockQty;
                var status = (v.Status ?? string.Empty).Trim().ToUpperInvariant();

                if (stock <= 0)
                {
                    // Hết hàng -> luôn OUT_OF_STOCK
                    if (status != "OUT_OF_STOCK")
                    {
                        v.Status = "OUT_OF_STOCK";
                        changed++;
                    }
                }
                else
                {
                    // Còn hàng:
                    //  - nếu đang OUT_OF_STOCK thì chuyển về ACTIVE
                    //  - nếu đang INACTIVE thì giữ nguyên (admin tắt thủ công)
                    if (status == "OUT_OF_STOCK")
                    {
                        v.Status = "ACTIVE";
                        changed++;
                    }
                }
            }

            // ===== 2) Cập nhật trạng thái Product theo tổng stock các variants =====
            var products = await db.Products
                .Include(p => p.ProductVariants)
                .ToListAsync(cancellationToken);

            foreach (var p in products)
            {
                var totalStock = p.ProductVariants.Sum(v => (int?)v.StockQty) ?? 0;
                var status = (p.Status ?? string.Empty).Trim().ToUpperInvariant();

                if (totalStock <= 0)
                {
                    // Không còn hàng ở bất kỳ biến thể nào => OUT_OF_STOCK
                    if (status != "OUT_OF_STOCK")
                    {
                        p.Status = "OUT_OF_STOCK";
                        changed++;
                    }
                }
                else
                {
                    // Còn hàng:
                    //  - nếu đang OUT_OF_STOCK thì mở lại ACTIVE
                    //  - nếu INACTIVE thì coi là admin tắt thủ công -> không đụng tới
                    if (status == "OUT_OF_STOCK")
                    {
                        p.Status = "ACTIVE";
                        changed++;
                    }
                }
            }

            if (changed > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "SyncProductStockAndStatusAsync: updated {Count} record(s).",
                changed);

            return changed;
        }
    }
}
