using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Services.Background
{
    public class PaymentTimeoutService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<PaymentTimeoutService> _logger;

        private static readonly TimeSpan PaymentTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ConvertingLockTimeout = TimeSpan.FromMinutes(5);

        // DB mới: Payment.Status check constraint có Pending/Paid/Cancelled/Failed/Success/Completed/Timeout...
        private const string PaymentStatusPending = "Pending";
        private const string PaymentStatusPaid = "Paid";
        private const string PaymentStatusSuccess = "Success";
        private const string PaymentStatusCompleted = "Completed";
        private const string PaymentStatusCancelled = "Cancelled";
        private const string PaymentStatusTimeout = "Timeout";

        private const string TargetTypeOrder = "Order";
        private const string TargetTypeSupportPlan = "SupportPlan";

        public PaymentTimeoutService(IServiceProvider sp, ILogger<PaymentTimeoutService> logger)
        {
            _sp = sp;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _sp.CreateAsyncScope();
                    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<KeytietkiemDbContext>>();
                    var inventoryReservation = scope.ServiceProvider.GetRequiredService<IInventoryReservationService>();
                    var payOs = scope.ServiceProvider.GetRequiredService<PayOSService>();
                    await using var db = dbFactory.CreateDbContext();

                    var nowUtc = DateTime.UtcNow;

                    // (Tuỳ bạn) Nếu DB vẫn dùng Converting thì giữ; nếu CK_Cart_Status chưa cho Converting,
                    // hãy sửa constraint hoặc bỏ block này.
                    await RecoverStuckConvertingCartsAsync(db, nowUtc, stoppingToken);

                    // Release các reservation hết hạn (theo Variant Qty)
                    await inventoryReservation.ReleaseExpiredReservationsAsync(db, nowUtc, stoppingToken);

                    var cutoff = nowUtc - PaymentTimeout;

                    // ✅ FIX: timeout cho cả Order + SupportPlan (targetId của SupportPlan là userId theo thiết kế mới của bạn)
                    var expiredPending = await db.Payments
                        .Where(p =>
                            p.Provider == "PayOS" &&
                            p.Status == PaymentStatusPending &&
                            p.CreatedAt < cutoff &&
                            p.TargetId != null &&
                            (p.TargetType == TargetTypeOrder || p.TargetType == TargetTypeSupportPlan))
                        .ToListAsync(stoppingToken);

                    if (expiredPending.Count > 0)
                    {
                        foreach (var pay in expiredPending)
                        {
                            // ✅ Cancel payment link trên PayOS để QR không còn quét được
                            if (!string.IsNullOrWhiteSpace(pay.PaymentLinkId))
                            {
                                try
                                {
                                    await payOs.CancelPaymentLink(pay.PaymentLinkId!, "Timeout");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex,
                                        "Failed to cancel PayOS payment link. PaymentId={PaymentId}, PaymentLinkId={PaymentLinkId}",
                                        pay.PaymentId, pay.PaymentLinkId);
                                }
                            }

                            pay.Status = PaymentStatusTimeout;

                            // ===== Nếu là Order => có thể cancel order + release inventory (như logic cũ) =====
                            if (string.Equals(pay.TargetType, TargetTypeOrder, StringComparison.OrdinalIgnoreCase)
                                && Guid.TryParse(pay.TargetId, out var orderId))
                            {
                                var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, stoppingToken);
                                if (order != null && string.Equals(order.Status, "PendingPayment", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Nếu có attempt khác còn Pending hoặc đã Paid/Success/Completed thì KHÔNG cancel order
                                    var hasOtherActiveAttempt = await db.Payments.AnyAsync(p =>
                                        p.TargetType == TargetTypeOrder &&
                                        p.TargetId == pay.TargetId &&
                                        p.PaymentId != pay.PaymentId &&
                                        (
                                            p.Status == PaymentStatusPending ||
                                            p.Status == PaymentStatusPaid ||
                                            p.Status == PaymentStatusSuccess ||
                                            p.Status == PaymentStatusCompleted
                                        ), stoppingToken);

                                    if (!hasOtherActiveAttempt)
                                    {
                                        order.Status = "CancelledByTimeout";

                                        // Release tồn kho theo Variant reservation
                                        await inventoryReservation.ReleaseReservationAsync(db, orderId, nowUtc, stoppingToken);
                                    }
                                }
                            }

                            // ===== Nếu là SupportPlan => chỉ timeout payment + cancel link (KHÔNG đụng inventory/order) =====
                            // (Không cần làm gì thêm ở đây.)
                        }

                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PaymentTimeoutService error");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private static async Task RecoverStuckConvertingCartsAsync(KeytietkiemDbContext db, DateTime nowUtc, CancellationToken ct)
        {
            if (!db.Database.IsRelational()) return;

            var cutoff = nowUtc - ConvertingLockTimeout;

            await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [dbo].[Cart]
SET [Status] = {"Active"},
    [UpdatedAt] = {nowUtc},
    [ExpiresAt] = CASE WHEN [UserId] IS NULL THEN DATEADD(day, 7, {nowUtc})
                       ELSE DATEADD(day, 30, {nowUtc}) END
WHERE [Status] = {"Converting"}
  AND [ConvertedOrderId] IS NULL
  AND [UpdatedAt] < {cutoff}
", ct);
        }
    }
}
