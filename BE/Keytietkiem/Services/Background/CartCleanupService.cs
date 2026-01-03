using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Services.Background
{
    public class CartCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CartCleanupService> _logger;

        // Docs ghi chạy weekly (có thể đổi sang daily nếu muốn)
        private readonly TimeSpan _period = TimeSpan.FromDays(1);

        public CartCleanupService(IServiceProvider serviceProvider, ILogger<CartCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cart Cleanup Service is starting.");

            // ✅ PATCH: chạy ngay khi app start
            try
            {
                await CleanupCartsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during initial cart cleanup.");
            }

            using var timer = new PeriodicTimer(_period);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CleanupCartsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during cart cleanup.");
                }
            }
        }


        // CartCleanupService.cs  (chỉ thay phần SQL trong CleanupCartsAsync)
        private async Task CleanupCartsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<KeytietkiemDbContext>>();
            using var context = dbFactory.CreateDbContext();

            var now = DateTime.UtcNow;

            var guestExpiredThreshold = now.AddDays(-7);
            var userExpiredThreshold = now.AddDays(-30);

            // ✅ Active guest: UpdatedAt + TTL < now OR ExpiresAt < now
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE [dbo].[Cart] SET [Status] = N'Expired', [UpdatedAt] = {0}, [ExpiresAt] = {0} " +
                "WHERE [Status] = N'Active' AND [UserId] IS NULL " +
                "  AND ( [UpdatedAt] < {1} OR ([ExpiresAt] IS NOT NULL AND [ExpiresAt] < {0}) )",
                new object[] { now, guestExpiredThreshold }, stoppingToken);

            // ✅ Active user: UpdatedAt + TTL < now OR ExpiresAt < now
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE [dbo].[Cart] SET [Status] = N'Expired', [UpdatedAt] = {0}, [ExpiresAt] = {0} " +
                "WHERE [Status] = N'Active' AND [UserId] IS NOT NULL " +
                "  AND ( [UpdatedAt] < {1} OR ([ExpiresAt] IS NOT NULL AND [ExpiresAt] < {0}) )",
                new object[] { now, userExpiredThreshold }, stoppingToken);

            // Hard delete expired quá 1 ngày
            var hardDeleteThreshold = now.AddDays(-1);
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM [dbo].[Cart] WHERE [Status] = N'Expired' AND [UpdatedAt] < {0}",
                new object[] { hardDeleteThreshold }, stoppingToken);

            _logger.LogInformation("Cart cleanup completed at: {time}", DateTimeOffset.Now);
        }

    }
}
