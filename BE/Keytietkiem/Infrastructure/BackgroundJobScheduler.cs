// File: Infrastructure/BackgroundJobScheduler.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Infrastructure
{
    /// <summary>
    /// Định nghĩa 1 "job" chạy nền theo chu kỳ.
    /// - Name: dùng để log.
    /// - Interval: khoảng thời gian giữa 2 lần chạy.
    /// - ExecuteAsync: logic chính của job (dùng ServiceProvider để resolve DbContext, service, v.v.).
    /// </summary>
    public interface IBackgroundJob
    {
        string Name { get; }

        /// <summary>
        /// Khoảng thời gian giữa 2 lần chạy.
        /// Ví dụ: TimeSpan.FromMinutes(5)
        /// </summary>
        TimeSpan Interval { get; }

        /// <summary>
        /// Logic chính cần chạy định kỳ.
        /// Lưu ý: không giữ state trong job, mọi thứ resolve từ serviceProvider.
        /// </summary>
        Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Service nền: nhận danh sách IBackgroundJob, tự điều phối chạy chúng theo Interval.
    /// Đăng ký 1 lần bằng AddHostedService, còn job thì AddSingleton&lt;IBackgroundJob, YourJob&gt;.
    /// </summary>
    public sealed class BackgroundJobScheduler : BackgroundService
    {
        private readonly IServiceProvider _rootProvider;
        private readonly IEnumerable<IBackgroundJob> _jobs;
        private readonly IClock _clock;
        private readonly ILogger<BackgroundJobScheduler> _logger;

        private sealed class JobState
        {
            public IBackgroundJob Job { get; }
            public DateTime NextRunUtc { get; set; }

            public JobState(IBackgroundJob job, DateTime firstRunUtc)
            {
                Job = job;
                NextRunUtc = firstRunUtc;
            }
        }

        public BackgroundJobScheduler(
            IServiceProvider rootProvider,
            IEnumerable<IBackgroundJob> jobs,
            IClock clock,
            ILogger<BackgroundJobScheduler> logger)
        {
            _rootProvider = rootProvider;
            _jobs = jobs ?? Array.Empty<IBackgroundJob>();
            _clock = clock;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_jobs.Any())
            {
                _logger.LogInformation("BackgroundJobScheduler: no jobs registered, nothing to run.");
                return;
            }

            // Mỗi job giữ trạng thái NextRunUtc riêng
            var now = _clock.UtcNow;
            var schedule = _jobs
                .Select(job => new JobState(job, firstRunUtc: now)) // lần đầu chạy ngay
                .ToList();

            _logger.LogInformation("BackgroundJobScheduler: started with {JobCount} jobs.", schedule.Count);

            while (!stoppingToken.IsCancellationRequested)
            {
                now = _clock.UtcNow;

                // Tìm thời điểm chạy gần nhất
                var nextRunUtc = schedule.Min(s => s.NextRunUtc);
                var delay = nextRunUtc - now;

                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // ignore khi stoppingToken bị cancel
                        break;
                    }
                }

                now = _clock.UtcNow;

                // Chạy tất cả job đã tới hạn
                foreach (var state in schedule.Where(s => s.NextRunUtc <= now))
                {
                    await RunJobSafelyAsync(state.Job, stoppingToken);

                    // Lần chạy tiếp theo = now + Interval
                    state.NextRunUtc = _clock.UtcNow + state.Job.Interval;
                }
            }

            _logger.LogInformation("BackgroundJobScheduler: stopping.");
        }

        private async Task RunJobSafelyAsync(IBackgroundJob job, CancellationToken stoppingToken)
        {
            var start = _clock.UtcNow;
            _logger.LogInformation("Background job {JobName} started at {StartUtc}.", job.Name, start);

            try
            {
                // Mỗi lần chạy job tạo 1 scope DI mới
                using var scope = _rootProvider.CreateScope();
                await job.ExecuteAsync(scope.ServiceProvider, stoppingToken);

                var end = _clock.UtcNow;
                _logger.LogInformation("Background job {JobName} finished in {Duration} ms.",
                    job.Name,
                    (end - start).TotalMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Background job {JobName} was cancelled.", job.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background job {JobName} failed with exception.", job.Name);
                // TODO: nếu muốn, có thể log thêm vào bảng AuditLog / BackgroundJobLog ở đây.
            }
        }
    }
}
