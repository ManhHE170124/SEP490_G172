// File: Infrastructure/StartupStockSyncHostedService.cs
// Purpose: One-time (per startup) stock backfill to sync ProductVariant.StockQty/Status and Product.Status
//          for key/account product types. Useful when the database initially contains stale stock.
//
// How to enable:
// - Add to appsettings.json:
//   "StockSyncOnStartup": { "Enabled": true, "DelaySeconds": 3, "BatchSize": 200 }
// - Register in Program.cs:
//   builder.Services.AddHostedService<StartupStockSyncHostedService>();
// - After it runs successfully once, set Enabled=false.

using Keytietkiem.Constants;
using Keytietkiem.DTOs.Products;
using Keytietkiem.Models;
using Keytietkiem.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Keytietkiem.Infrastructure
{
    public class StartupStockSyncHostedService : BackgroundService
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;
        private readonly ILogger<StartupStockSyncHostedService> _logger;
        private readonly IConfiguration _config;

        public StartupStockSyncHostedService(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IClock clock,
            ILogger<StartupStockSyncHostedService> logger,
            IConfiguration config)
        {
            _dbFactory = dbFactory;
            _clock = clock;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enabled = _config.GetValue<bool>("StockSyncOnStartup:Enabled");
            if (!enabled)
            {
                _logger.LogInformation("[StockSyncOnStartup] Disabled.");
                return;
            }

            var delaySeconds = _config.GetValue<int?>("StockSyncOnStartup:DelaySeconds") ?? 3;
            var batchSize = _config.GetValue<int?>("StockSyncOnStartup:BatchSize") ?? 200;

            if (delaySeconds > 0)
            {
                _logger.LogInformation("[StockSyncOnStartup] Waiting {DelaySeconds}s before starting...", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }

            var nowUtc = _clock.UtcNow;

            List<Guid> variantIds;
            await using (var db = await _dbFactory.CreateDbContextAsync(stoppingToken))
            {
                variantIds = await db.ProductVariants
                    .AsNoTracking()
                    .Where(v => v.Product != null &&
                                (v.Product.ProductType == ProductEnums.PERSONAL_KEY
                                 || v.Product.ProductType == ProductEnums.PERSONAL_ACCOUNT
                                 || v.Product.ProductType == ProductEnums.SHARED_ACCOUNT))
                    .Select(v => v.VariantId)
                    .Distinct()
                    .ToListAsync(stoppingToken);
            }

            if (variantIds.Count == 0)
            {
                _logger.LogInformation("[StockSyncOnStartup] No key/account variants found. Nothing to sync.");
                return;
            }

            _logger.LogInformation("[StockSyncOnStartup] Start syncing {Count} variants (BatchSize={BatchSize}).", variantIds.Count, batchSize);

            var total = variantIds.Count;
            var done = 0;

            for (var i = 0; i < total; i += batchSize)
            {
                stoppingToken.ThrowIfCancellationRequested();

                var batch = variantIds.Skip(i).Take(batchSize).ToList();

                await using var db = await _dbFactory.CreateDbContextAsync(stoppingToken);

                foreach (var vid in batch)
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    try
                    {
                        await VariantStockRecalculator.SyncVariantStockAndStatusAsync(db, vid, nowUtc, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        // Don't crash the app due to 1 bad record; log and continue.
                        _logger.LogError(ex, "[StockSyncOnStartup] Failed to sync variant {VariantId}", vid);
                    }

                    done++;
                }

                _logger.LogInformation("[StockSyncOnStartup] Progress {Done}/{Total}", done, total);
            }

            _logger.LogInformation("[StockSyncOnStartup] Completed. Synced {Done} variants. You should set StockSyncOnStartup:Enabled=false now.", done);
        }
    }
}
