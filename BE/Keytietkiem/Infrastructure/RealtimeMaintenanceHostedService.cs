// File: Infrastructure/RealtimeMaintenanceHostedService.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Infrastructure
{
    /// <summary>
    /// Worker chạy nền:
    ///   - Mỗi 1 phút:
    ///       + Auto-cancel payment Pending quá 5 phút.
    ///       + Đồng bộ tồn kho -> trạng thái product/variant.
    /// Không cần controller gọi, tự chạy khi app khởi động.
    /// </summary>
    public class RealtimeMaintenanceHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RealtimeMaintenanceHostedService> _logger;

        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(5);

        public RealtimeMaintenanceHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<RealtimeMaintenanceHostedService> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RealtimeMaintenanceHostedService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var realtimeService = scope.ServiceProvider
                        .GetRequiredService<IRealtimeDatabaseUpdateService>();

                    // 1) Auto-cancel payment pending quá 5 phút
                    await realtimeService.AutoCancelExpiredPendingPaymentsAsync(
                        PendingTimeout, stoppingToken);

                    // 2) Đồng bộ trạng thái product/variant theo tồn kho
                    await realtimeService.SyncProductStockAndStatusAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "RealtimeMaintenanceHostedService: error when running maintenance tasks.");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("RealtimeMaintenanceHostedService stopped.");
        }
    }
}
