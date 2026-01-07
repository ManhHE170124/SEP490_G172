// File: Infrastructure/TicketSlaBackgroundJob.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Models;        // KeytietkiemDbContext, Ticket
using Keytietkiem.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Infrastructure
{
    /// <summary>
    /// Background job: mỗi 5 phút quét các ticket đang mở / đang xử lý
    /// và cập nhật lại SlaStatus (OK / Warning / Overdue) dựa trên
    /// CreatedAt, FirstResponseDueAt, FirstRespondedAt, ResolutionDueAt, ResolvedAt.
    ///
    /// KHÔNG áp lại SlaRule: chỉ dùng dữ liệu đã lưu trong Ticket.
    /// </summary>
    public sealed class TicketSlaBackgroundJob : IBackgroundJob
    {
        public string Name => "TicketSlaBackgroundJob";

        /// <summary>
        /// Chạy mỗi 5 phút.
        /// </summary>
        public TimeSpan Interval => TimeSpan.FromMinutes(5);

        public async Task ExecuteAsync(
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            var logger = serviceProvider
                .GetRequiredService<ILogger<TicketSlaBackgroundJob>>();

            // ❗ Lấy DbContext qua IDbContextFactory, đúng với Program.cs
            var dbFactory = serviceProvider
                .GetRequiredService<IDbContextFactory<KeytietkiemDbContext>>();

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = DateTime.UtcNow;

            logger.LogInformation(
                "TicketSlaBackgroundJob: start run at {NowUtc}.",
                nowUtc);

            // Các trạng thái được xem là "đang sống"
            var activeStatuses = new[] { "New", "Open", "InProgress" };

            // Chỉ lấy ticket có SlaRuleId (tức là có cấu hình SLA time-based)
            var tickets = await db.Tickets
                .Where(t =>
                    t.SlaRuleId != null &&
                    activeStatuses.Contains((t.Status ?? "").Trim()))
                .ToListAsync(cancellationToken);

            if (tickets.Count == 0)
            {
                logger.LogDebug(
                    "TicketSlaBackgroundJob: no active tickets to update at {NowUtc}.",
                    nowUtc);
                return;
            }

            var changedCount = 0;

            foreach (var ticket in tickets)
            {
                var oldSlaStatus = ticket.SlaStatus ?? string.Empty;

                // Cập nhật lại SlaStatus dựa trên mốc thời gian đã có trong ticket
                TicketSlaHelper.UpdateSlaStatus(ticket, nowUtc);

                if (!string.Equals(oldSlaStatus, ticket.SlaStatus, StringComparison.Ordinal))
                {
                    changedCount++;
                }
            }

            if (changedCount > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            logger.LogInformation(
                "TicketSlaBackgroundJob: evaluated {Total} tickets, changed SLA status for {Changed} tickets.",
                tickets.Count,
                changedCount);
        }
    }
}
