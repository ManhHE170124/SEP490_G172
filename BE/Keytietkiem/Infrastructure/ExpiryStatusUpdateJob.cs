using Keytietkiem.Models;
using Keytietkiem.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Infrastructure
{
    public class ExpiryStatusUpdateJob : IBackgroundJob
    {
        private readonly ILogger<ExpiryStatusUpdateJob> _logger;

        public ExpiryStatusUpdateJob(ILogger<ExpiryStatusUpdateJob> logger)
        {
            _logger = logger;
        }

        public string Name => "ExpiryStatusUpdateJob";

        // Chạy mỗi 1 tiếng
        public TimeSpan Interval => TimeSpan.FromHours(0.1);

        public async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            try
            {
                var dbFactory = serviceProvider.GetRequiredService<IDbContextFactory<KeytietkiemDbContext>>();
                using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

                var now = DateTime.UtcNow;

                // 1) Update ProductAccount -> Expired
                var expiredAccounts = await context.ProductAccounts
                    .Where(a => a.Status != "Expired"
                             && a.ExpiryDate != null
                             && a.ExpiryDate < now)
                    .ToListAsync(cancellationToken);

                if (expiredAccounts.Any())
                {
                    foreach (var acc in expiredAccounts)
                    {
                        acc.Status = "Expired";
                        acc.UpdatedAt = now;
                    }

                    _logger.LogInformation(
                        "ExpiryStatusUpdateJob: Found {Count} expired ProductAccounts. Updating status...",
                        expiredAccounts.Count);
                }

                // 2) Update ProductKey -> Expired
                var expiredKeys = await context.ProductKeys
                    .Where(k => k.Status != "Expired"
                             && k.ExpiryDate != null
                             && k.ExpiryDate < now)
                    .ToListAsync(cancellationToken);

                if (expiredKeys.Any())
                {
                    foreach (var key in expiredKeys)
                    {
                        key.Status = "Expired";
                        key.UpdatedAt = now;
                    }

                    _logger.LogInformation(
                        "ExpiryStatusUpdateJob: Found {Count} expired ProductKeys. Updating status...",
                        expiredKeys.Count);
                }

                if (expiredAccounts.Any() || expiredKeys.Any())
                {
                    await context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "ExpiryStatusUpdateJob: Successfully updated {AccountCount} accounts and {KeyCount} keys to Expired.",
                        expiredAccounts.Count, expiredKeys.Count);

                    // ✅ NEW: Sync stock/status realtime cho các variant bị ảnh hưởng
                    var affectedVariantIds = expiredAccounts
                        .Select(a => a.VariantId)
                        .Concat(expiredKeys.Select(k => k.VariantId))
                        .Where(id => id != Guid.Empty)
                        .Distinct()
                        .ToList();

                    if (affectedVariantIds.Count > 0)
                    {
                        _logger.LogInformation(
                            "ExpiryStatusUpdateJob: Syncing stock/status for {VariantCount} affected variants...",
                            affectedVariantIds.Count);

                        await VariantStockRecalculator.SyncVariantStockAndStatusAsync(
                            context,
                            affectedVariantIds,
                            now,
                            cancellationToken);
                    }
                }
                else
                {
                    _logger.LogInformation("ExpiryStatusUpdateJob: No expired items found.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing ExpiryStatusUpdateJob.");
                throw; // Rethrow to let scheduler handle/log as failed
            }
        }
    }
}
