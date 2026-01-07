using System;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Services.Background
{
    /// <summary>
    /// Background worker: reads NotificationDispatchQueue and pushes via SignalR.
    /// Best-effort: any failure will be logged but will not stop the service.
    /// </summary>
    public class NotificationDispatchBackgroundService : BackgroundService
    {
        private readonly INotificationDispatchQueue _queue;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ILogger<NotificationDispatchBackgroundService> _logger;

        public NotificationDispatchBackgroundService(
            INotificationDispatchQueue queue,
            IHubContext<NotificationHub> hub,
            ILogger<NotificationDispatchBackgroundService> logger)
        {
            _queue = queue;
            _hub = hub;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested
                   && await _queue.Reader.WaitToReadAsync(stoppingToken))
            {
                while (_queue.Reader.TryRead(out var item))
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(item.GroupName))
                        {
                            await _hub.Clients.Group(item.GroupName!).SendAsync(
                                item.MethodName,
                                item.Payload,
                                stoppingToken
                            );
                        }
                        else if (item.UserId.HasValue)
                        {
                            await _hub.Clients.Group(NotificationHub.UserGroup(item.UserId.Value)).SendAsync(
                                item.MethodName,
                                item.Payload,
                                stoppingToken
                            );
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // ignore on shutdown
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Notification dispatch failed. Method={Method}, Group={Group}, UserId={UserId}",
                            item.MethodName,
                            item.GroupName,
                            item.UserId
                        );
                    }
                }

                // Yield a bit to avoid tight loop under huge backlog.
                // (Cheap throttling when many recipients)
                try
                {
                    await Task.Delay(2, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // ignore
                }
            }
        }
    }
}
