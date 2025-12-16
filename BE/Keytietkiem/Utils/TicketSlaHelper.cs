// File: Controllers/TicketSlaHelper.cs
using System;
using System.Linq;
using Keytietkiem.DTOs.Tickets;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Utils
{
    /// <summary>
    /// Helper tập trung toàn bộ logic SLA cho Ticket.
    /// </summary>
    public static class TicketSlaHelper
    {
        /// <summary>
        /// Áp dụng SLA khi tạo mới ticket:
        /// - Gán Severity chuẩn hoá.
        /// - Tính PriorityLevel từ customerPriorityLevel (giữ nguyên giá trị từ user).
        /// - Chọn SlaRule tương ứng (severity + priority).
        /// - Tính FirstResponseDueAt, ResolutionDueAt.
        /// - Reset FirstRespondedAt, ResolvedAt và cập nhật SlaStatus.
        /// </summary>
        public static void ApplyOnCreate(
            KeytietkiemDbContext db,
            Ticket ticket,
            string? severityRaw,
            int? customerPriorityLevel,
            DateTime nowUtc)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));

            // 1. Severity: chuẩn hoá về enum TicketSeverity (Low/Medium/High/Critical)
            var severityEnum = TicketSeverity.Medium;
            if (!string.IsNullOrWhiteSpace(severityRaw) &&
                Enum.TryParse<TicketSeverity>(severityRaw, true, out var parsed))
            {
                severityEnum = parsed;
            }
            var severity = severityEnum.ToString();
            ticket.Severity = severity;

            // 2. Priority level: lấy 100% theo user, không clamp
            //    (chỉ fallback = 0 nếu null để tránh null reference)
            var priority = customerPriorityLevel ?? 0;
            ticket.PriorityLevel = priority;

            // 3. Chọn rule phù hợp
            var rule = db.SlaRules
                .AsNoTracking()
                .Where(r => r.IsActive && r.Severity == severity && r.PriorityLevel == priority)
                .OrderBy(r => r.SlaRuleId)
                .FirstOrDefault();

            if (rule != null)
            {
                ticket.SlaRuleId = rule.SlaRuleId;
                ticket.FirstResponseDueAt = nowUtc.AddMinutes(rule.FirstResponseMinutes);
                ticket.ResolutionDueAt = nowUtc.AddMinutes(rule.ResolutionMinutes);
            }
            else
            {
                // Không tìm thấy rule: vẫn set PriorityLevel nhưng không chạy SLA thời gian
                ticket.SlaRuleId = null;
                ticket.FirstResponseDueAt = null;
                ticket.ResolutionDueAt = null;
            }

            ticket.FirstRespondedAt = null;
            ticket.ResolvedAt = null;

            // SlaStatus ban đầu luôn là OK
            UpdateSlaStatus(ticket, nowUtc);
        }

        public static void UpdateSlaStatus(Ticket ticket)
            => UpdateSlaStatus(ticket, DateTime.UtcNow);

        /// <summary>
        /// Tính lại SlaStatus (OK / Warning / Overdue) dựa trên:
        /// - CreatedAt
        /// - FirstResponseDueAt / FirstRespondedAt
        /// - ResolutionDueAt / ResolvedAt
        /// </summary>
        public static void UpdateSlaStatus(Ticket ticket, DateTime nowUtc)
        {
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));

            // Không cấu hình rule => xem như OK (không track SLA)
            if (ticket.SlaRuleId == null)
            {
                ticket.SlaStatus = SlaState.OK.ToString();
                return;
            }

            var state = SlaState.OK;

            // Helper để lấy mức độ "nặng" nhất
            static SlaState Max(SlaState a, SlaState b)
                => (SlaState)Math.Max((int)a, (int)b);

            // FIRST RESPONSE SLA
            if (ticket.FirstResponseDueAt.HasValue)
            {
                var start = ticket.CreatedAt;
                var due = ticket.FirstResponseDueAt.Value;

                if (due < start) due = start;

                var total = due - start;
                var warnAt = start + TimeSpan.FromTicks((long)(total.Ticks * 0.75)); // 75% thời gian

                if (ticket.FirstRespondedAt.HasValue)
                {
                    var actual = ticket.FirstRespondedAt.Value;
                    if (actual > due)
                    {
                        state = Max(state, SlaState.Overdue);
                    }
                }
                else
                {
                    if (nowUtc > due)
                    {
                        state = Max(state, SlaState.Overdue);
                    }
                    else if (nowUtc >= warnAt)
                    {
                        state = Max(state, SlaState.Warning);
                    }
                }
            }

            // RESOLUTION SLA
            if (ticket.ResolutionDueAt.HasValue)
            {
                var start = ticket.CreatedAt;
                var due = ticket.ResolutionDueAt.Value;

                if (due < start) due = start;

                var total = due - start;
                var warnAt = start + TimeSpan.FromTicks((long)(total.Ticks * 0.75)); // 75% thời gian

                if (ticket.ResolvedAt.HasValue)
                {
                    var actual = ticket.ResolvedAt.Value;
                    if (actual > due)
                    {
                        state = Max(state, SlaState.Overdue);
                    }
                }
                else
                {
                    if (nowUtc > due)
                    {
                        state = Max(state, SlaState.Overdue);
                    }
                    else if (nowUtc >= warnAt)
                    {
                        state = Max(state, SlaState.Warning);
                    }
                }
            }

            ticket.SlaStatus = state.ToString();
        }
    }
}
