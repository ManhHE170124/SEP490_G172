// File: Infrastructure/SupportStatsBackgroundJob.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Infrastructure
{
    /// <summary>
    /// Service tính toán & cập nhật số liệu thống kê cho support dashboard:
    /// - SupportDailyStat
    /// - SupportStaffDailyStat
    /// - SupportTicketSeverityPriorityWeeklyStat
    /// - SupportChatPriorityWeeklyStat
    /// - SupportPlanMonthlyStat
    ///
    /// Logic được thiết kế bám theo "Support Dashboard Note".
    /// </summary>
    public interface ISupportStatsUpdateService
    {
        /// <summary>
        /// Rebuild tất cả các bảng thống kê cho 1 khoảng thời gian gần đây
        /// (daily / weekly / monthly window).
        /// </summary>
        Task RebuildAllAsync(CancellationToken cancellationToken = default);
    }

    public sealed class SupportStatsUpdateService : ISupportStatsUpdateService
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;
        private readonly ILogger<SupportStatsUpdateService> _logger;

        // Có thể chỉnh cho phù hợp với tải hệ thống
        private const int DailyWindowDays = 7;   // Tính lại daily stats cho 7 ngày gần nhất
        private const int WeeklyWindowWeeks = 4; // 4 tuần gần nhất
        private const int MonthlyWindowMonths = 6; // 6 tháng gần nhất

        public SupportStatsUpdateService(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IClock clock,
            ILogger<SupportStatsUpdateService> logger)
        {
            _dbFactory = dbFactory;
            _clock = clock;
            _logger = logger;
        }

        public async Task RebuildAllAsync(CancellationToken cancellationToken = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var today = DateOnly.FromDateTime(nowUtc.Date);
            var weekStart = GetWeekStart(today);
            var monthAnchor = new DateOnly(today.Year, today.Month, 1);

            _logger.LogInformation("SupportStatsUpdateService: rebuild stats start at {NowUtc}", nowUtc);

            // DAILY: support overview + chat overview
            for (int i = 0; i < DailyWindowDays; i++)
            {
                var date = today.AddDays(-i);
                await RebuildSupportDailyStatAsync(db, date, cancellationToken);
                await RebuildSupportStaffDailyStatsAsync(db, date, cancellationToken);
            }

            // WEEKLY: SLA theo severity/priority & chat theo priority
            var ws = weekStart;
            for (int i = 0; i < WeeklyWindowWeeks; i++)
            {
                await RebuildTicketSeverityPriorityWeeklyStatAsync(db, ws, cancellationToken);
                await RebuildChatPriorityWeeklyStatAsync(db, ws, cancellationToken);
                ws = ws.AddDays(-7);
            }

            // MONTHLY: support plan + loyalty
            var month = monthAnchor;
            for (int i = 0; i < MonthlyWindowMonths; i++)
            {
                var yearMonth = $"{month.Year:D4}-{month.Month:D2}";
                await RebuildSupportPlanMonthlyStatsAsync(db, yearMonth, cancellationToken);
                month = month.AddMonths(-1);
            }

            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("SupportStatsUpdateService: rebuild stats finished at {NowUtc}", _clock.UtcNow);
        }

        // ==========================
        // =   DAILY - OVERVIEW     =
        // ==========================

        private static (DateTime Start, DateTime End) GetDayRange(DateOnly date)
        {
            var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            return (start, end);
        }

        private static (DateTime Start, DateTime End) GetWeekRange(DateOnly weekStartDate)
        {
            var start = weekStartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(7);
            return (start, end);
        }

        private static DateOnly GetWeekStart(DateOnly date)
        {
            // Tuần bắt đầu từ Monday
            var diff = (int)date.DayOfWeek - (int)DayOfWeek.Monday;
            if (diff < 0) diff += 7;
            return date.AddDays(-diff);
        }

        /// <summary>
        /// Tính & upsert SupportDailyStat cho 1 ngày.
        /// Bám các KPI: new/closed/open ticket, new chat session,
        /// avg FRT/Resolution ticket, SLA ratio, chat FRT / duration / messages.
        /// </summary>
        private async Task RebuildSupportDailyStatAsync(
            KeytietkiemDbContext db,
            DateOnly statDate,
            CancellationToken ct)
        {
            var (dayStart, dayEnd) = GetDayRange(statDate);

            // --- Ticket new/closed ---
            var ticketsCreated = await db.Tickets
                .Where(t => t.CreatedAt >= dayStart && t.CreatedAt < dayEnd)
                .ToListAsync(ct);

            var ticketsResolved = await db.Tickets
                .Where(t => t.ResolvedAt >= dayStart && t.ResolvedAt < dayEnd)
                .ToListAsync(ct);

            // Backlog: open ticket tại thời điểm cuối ngày
            var openTicketsAtEnd = await db.Tickets
                .Where(t => t.CreatedAt < dayEnd &&
                            (t.ResolvedAt == null || t.ResolvedAt >= dayEnd))
                .CountAsync(ct);

            // FRT & Resolution time
            var frtTickets = ticketsCreated
                .Where(t => t.FirstRespondedAt.HasValue)
                .ToList();

            var resolvedTickets = ticketsCreated
                .Where(t => t.ResolvedAt.HasValue)
                .ToList();

            decimal? avgFrtMinutes = null;
            if (frtTickets.Any())
            {
                var avg = frtTickets.Average(t =>
                    (t.FirstRespondedAt!.Value - t.CreatedAt).TotalMinutes);
                avgFrtMinutes = (decimal)avg;
            }

            decimal? avgResolutionMinutes = null;
            if (resolvedTickets.Any())
            {
                var avg = resolvedTickets.Average(t =>
                    (t.ResolvedAt!.Value - t.CreatedAt).TotalMinutes);
                avgResolutionMinutes = (decimal)avg;
            }

            // FRT / Resolution ratio vs SLA
            var frtRatioList = new List<double>();
            foreach (var t in frtTickets)
            {
                if (t.FirstResponseDueAt.HasValue &&
                    t.FirstResponseDueAt.Value > t.CreatedAt)
                {
                    var num = (t.FirstRespondedAt!.Value - t.CreatedAt).TotalMinutes;
                    var den = (t.FirstResponseDueAt.Value - t.CreatedAt).TotalMinutes;
                    if (den > 0)
                    {
                        frtRatioList.Add(num / den);
                    }
                }
            }

            decimal? avgFrtRatio = frtRatioList.Any()
                ? (decimal?)frtRatioList.Average()
                : null;

            var resolutionRatioList = new List<double>();
            foreach (var t in resolvedTickets)
            {
                if (t.ResolutionDueAt.HasValue &&
                    t.ResolutionDueAt.Value > t.CreatedAt)
                {
                    var num = (t.ResolvedAt!.Value - t.CreatedAt).TotalMinutes;
                    var den = (t.ResolutionDueAt.Value - t.CreatedAt).TotalMinutes;
                    if (den > 0)
                    {
                        resolutionRatioList.Add(num / den);
                    }
                }
            }

            decimal? avgResolutionRatio = resolutionRatioList.Any()
                ? (decimal?)resolutionRatioList.Average()
                : null;

            // SLA counts (response / resolution)
            var frtSlaTotal = frtTickets
                .Count(t => t.FirstResponseDueAt.HasValue);
            var frtSlaMet = frtTickets
                .Count(t => t.FirstResponseDueAt.HasValue &&
                            t.FirstRespondedAt!.Value <= t.FirstResponseDueAt.Value);

            var resolutionSlaTotal = resolvedTickets
                .Count(t => t.ResolutionDueAt.HasValue);
            var resolutionSlaMet = resolvedTickets
                .Count(t => t.ResolutionDueAt.HasValue &&
                            t.ResolvedAt!.Value <= t.ResolutionDueAt.Value);

            // --- Chat sessions & messages ---
            var sessionsToday = await db.SupportChatSessions
                .Where(s => s.StartedAt >= dayStart && s.StartedAt < dayEnd)
                .ToListAsync(ct);

            var newChatSessionsCount = sessionsToday.Count;

            decimal? avgChatFrt = null;
            decimal? avgChatDuration = null;
            decimal? avgChatMessagesPerSession = null;

            if (sessionsToday.Any())
            {
                var sessionIds = sessionsToday
                    .Select(s => s.ChatSessionId)
                    .ToList();

                var messages = await db.SupportChatMessages
                    .Where(m => sessionIds.Contains(m.ChatSessionId))
                    .OrderBy(m => m.SentAt)
                    .ToListAsync(ct);

                var msgBySession = messages
                    .GroupBy(m => m.ChatSessionId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var frtList = new List<double>();
                var durationList = new List<double>();
                var msgCountList = new List<int>();

                foreach (var session in sessionsToday)
                {
                    msgBySession.TryGetValue(session.ChatSessionId, out var sessionMsgs);
                    sessionMsgs ??= new List<SupportChatMessage>();

                    // First response time trong chat
                    var customerFirst = sessionMsgs.FirstOrDefault(m => !m.IsFromStaff);
                    if (customerFirst != null)
                    {
                        var staffFirst = sessionMsgs.FirstOrDefault(m =>
                            m.IsFromStaff && m.SentAt >= customerFirst.SentAt);
                        if (staffFirst != null)
                        {
                            var minutes = (staffFirst.SentAt - customerFirst.SentAt).TotalMinutes;
                            if (minutes >= 0)
                                frtList.Add(minutes);
                        }
                    }

                    // Duration = ClosedAt / LastMessageAt - StartedAt
                    var end = session.ClosedAt ?? session.LastMessageAt ?? session.StartedAt;
                    var dur = (end - session.StartedAt).TotalMinutes;
                    if (dur < 0) dur = 0;
                    durationList.Add(dur);

                    msgCountList.Add(sessionMsgs.Count);
                }

                avgChatFrt = frtList.Any() ? (decimal?)frtList.Average() : null;
                avgChatDuration = durationList.Any()
                    ? (decimal?)durationList.Average()
                    : null;
                avgChatMessagesPerSession = msgCountList.Any()
                    ? (decimal?)msgCountList.Average()
                    : null;
            }

            // Upsert SupportDailyStat
            var stat = await db.SupportDailyStats
                .FirstOrDefaultAsync(s => s.StatDate == statDate, ct);

            if (stat == null)
            {
                stat = new SupportDailyStat { StatDate = statDate };
                db.SupportDailyStats.Add(stat);
            }

            stat.NewTicketsCount = ticketsCreated.Count;
            stat.ClosedTicketsCount = ticketsResolved.Count;
            stat.OpenTicketsCountEndOfDay = openTicketsAtEnd;
            stat.NewChatSessionsCount = newChatSessionsCount;

            stat.AvgTicketFirstResponseMinutes = avgFrtMinutes;
            stat.AvgTicketResolutionMinutes = avgResolutionMinutes;
            stat.AvgTicketFirstResponseSlaRatio = avgFrtRatio;
            stat.AvgTicketResolutionSlaRatio = avgResolutionRatio;

            stat.TicketResponseSlaMetCount = frtSlaMet;
            stat.TicketResponseSlaTotalCount = frtSlaTotal;
            stat.TicketResolutionSlaMetCount = resolutionSlaMet;
            stat.TicketResolutionSlaTotalCount = resolutionSlaTotal;

            stat.AvgChatFirstResponseMinutes = avgChatFrt;
            stat.AvgChatDurationMinutes = avgChatDuration;
            stat.AvgChatMessagesPerSession = avgChatMessagesPerSession;
        }

        // ==========================
        // =   DAILY - BY STAFF     =
        // ==========================

        private async Task RebuildSupportStaffDailyStatsAsync(
            KeytietkiemDbContext db,
            DateOnly statDate,
            CancellationToken ct)
        {
            var (dayStart, dayEnd) = GetDayRange(statDate);

            // Tickets assigned / resolved trong ngày
            var ticketsAssigned = await db.Tickets
                .Where(t => t.AssigneeId != null &&
                            t.CreatedAt >= dayStart && t.CreatedAt < dayEnd)
                .ToListAsync(ct);

            var ticketsResolved = await db.Tickets
                .Where(t => t.AssigneeId != null &&
                            t.ResolvedAt >= dayStart && t.ResolvedAt < dayEnd)
                .ToListAsync(ct);

            // Chat sessions staff handle trong ngày
            var chatSessions = await db.SupportChatSessions
                .Where(s => s.AssignedStaffId != null &&
                            s.StartedAt >= dayStart && s.StartedAt < dayEnd)
                .ToListAsync(ct);

            // Messages của staff trong ngày
            var ticketReplies = await db.TicketReplies
                .Where(r => r.IsStaffReply &&
                            r.SentAt >= dayStart && r.SentAt < dayEnd)
                .ToListAsync(ct);

            var staffChatMessages = await db.SupportChatMessages
                .Where(m => m.IsFromStaff &&
                            m.SentAt >= dayStart && m.SentAt < dayEnd)
                .ToListAsync(ct);

            // Các staff xuất hiện trong ticket / chat / reply
            var staffIds = new HashSet<Guid>();

            staffIds.UnionWith(ticketsAssigned
                .Where(t => t.AssigneeId.HasValue)
                .Select(t => t.AssigneeId!.Value));

            staffIds.UnionWith(ticketsResolved
                .Where(t => t.AssigneeId.HasValue)
                .Select(t => t.AssigneeId!.Value));

            staffIds.UnionWith(chatSessions
                .Where(s => s.AssignedStaffId.HasValue)
                .Select(s => s.AssignedStaffId!.Value));

            staffIds.UnionWith(ticketReplies
                .Select(r => r.SenderId));

            staffIds.UnionWith(staffChatMessages
                .Select(m => m.SenderId));

            if (!staffIds.Any())
            {
                // Không có activity nào trong ngày, không cần update
                return;
            }

            // Preload chat messages cho các session trong ngày (để tính FRT/duration per staff)
            var allSessionIds = chatSessions
                .Select(s => s.ChatSessionId)
                .ToList();

            var allChatMessagesForDay = allSessionIds.Count == 0
                ? new List<SupportChatMessage>()
                : await db.SupportChatMessages
                    .Where(m => allSessionIds.Contains(m.ChatSessionId))
                    .OrderBy(m => m.SentAt)
                    .ToListAsync(ct);

            var chatMsgsBySession = allChatMessagesForDay
                .GroupBy(m => m.ChatSessionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var staffId in staffIds)
            {
                var ticketsAssignedForStaff = ticketsAssigned
                    .Where(t => t.AssigneeId == staffId)
                    .ToList();

                var ticketsResolvedForStaff = ticketsResolved
                    .Where(t => t.AssigneeId == staffId)
                    .ToList();

                // FRT & resolution theo staff (tính trên ticketsAssignedForStaff)
                var frtTickets = ticketsAssignedForStaff
                    .Where(t => t.FirstRespondedAt.HasValue)
                    .ToList();

                var resolvedTickets = ticketsAssignedForStaff
                    .Where(t => t.ResolvedAt.HasValue)
                    .ToList();

                decimal? avgFrt = null;
                if (frtTickets.Any())
                {
                    var avg = frtTickets.Average(t =>
                        (t.FirstRespondedAt!.Value - t.CreatedAt).TotalMinutes);
                    avgFrt = (decimal)avg;
                }

                decimal? avgResolution = null;
                if (resolvedTickets.Any())
                {
                    var avg = resolvedTickets.Average(t =>
                        (t.ResolvedAt!.Value - t.CreatedAt).TotalMinutes);
                    avgResolution = (decimal)avg;
                }

                var frtSlaTotal = frtTickets
                    .Count(t => t.FirstResponseDueAt.HasValue);
                var frtSlaMet = frtTickets
                    .Count(t => t.FirstResponseDueAt.HasValue &&
                                t.FirstRespondedAt!.Value <= t.FirstResponseDueAt.Value);

                var resolutionSlaTotal = resolvedTickets
                    .Count(t => t.ResolutionDueAt.HasValue);
                var resolutionSlaMet = resolvedTickets
                    .Count(t => t.ResolutionDueAt.HasValue &&
                                t.ResolvedAt!.Value <= t.ResolutionDueAt.Value);

                // Chat per staff
                var staffSessions = chatSessions
                    .Where(s => s.AssignedStaffId == staffId)
                    .ToList();

                var chatSessionsCount = staffSessions.Count;
                decimal? avgChatFrt = null;
                decimal? avgChatDuration = null;

                if (chatSessionsCount > 0)
                {
                    var frtList = new List<double>();
                    var durationList = new List<double>();

                    foreach (var session in staffSessions)
                    {
                        chatMsgsBySession.TryGetValue(session.ChatSessionId, out var msgs);
                        msgs ??= new List<SupportChatMessage>();

                        var customerFirst = msgs.FirstOrDefault(m => !m.IsFromStaff);
                        if (customerFirst != null)
                        {
                            var staffFirst = msgs.FirstOrDefault(m =>
                                m.IsFromStaff && m.SentAt >= customerFirst.SentAt);
                            if (staffFirst != null)
                            {
                                var minutes = (staffFirst.SentAt - customerFirst.SentAt).TotalMinutes;
                                if (minutes >= 0)
                                    frtList.Add(minutes);
                            }
                        }

                        var end = session.ClosedAt ?? session.LastMessageAt ?? session.StartedAt;
                        var dur = (end - session.StartedAt).TotalMinutes;
                        if (dur < 0) dur = 0;
                        durationList.Add(dur);
                    }

                    avgChatFrt = frtList.Any() ? (decimal?)frtList.Average() : null;
                    avgChatDuration = durationList.Any() ? (decimal?)durationList.Average() : null;
                }

                // Messages của staff
                var ticketStaffMsgCount = ticketReplies
                    .Count(r => r.SenderId == staffId);

                var chatStaffMsgCount = staffChatMessages
                    .Count(m => m.SenderId == staffId);

                // Upsert SupportStaffDailyStat
                var stat = await db.SupportStaffDailyStats
                    .FirstOrDefaultAsync(s => s.StatDate == statDate && s.StaffId == staffId, ct);

                if (stat == null)
                {
                    stat = new SupportStaffDailyStat
                    {
                        StatDate = statDate,
                        StaffId = staffId
                    };
                    db.SupportStaffDailyStats.Add(stat);
                }

                stat.TicketsAssignedCount = ticketsAssignedForStaff.Count;
                stat.TicketsResolvedCount = ticketsResolvedForStaff.Count;

                stat.AvgTicketFirstResponseMinutes = avgFrt;
                stat.AvgTicketResolutionMinutes = avgResolution;

                stat.TicketResponseSlaMetCount = frtSlaMet;
                stat.TicketResponseSlaTotalCount = frtSlaTotal;
                stat.TicketResolutionSlaMetCount = resolutionSlaMet;
                stat.TicketResolutionSlaTotalCount = resolutionSlaTotal;

                stat.ChatSessionsHandledCount = chatSessionsCount;
                stat.AvgChatFirstResponseMinutes = avgChatFrt;
                stat.AvgChatDurationMinutes = avgChatDuration;

                stat.TicketStaffMessagesCount = ticketStaffMsgCount;
                stat.ChatStaffMessagesCount = chatStaffMsgCount;
            }
        }

        // =======================================
        // =   WEEKLY - TICKET SLA BY SEVERITY  =
        // =======================================

        private async Task RebuildTicketSeverityPriorityWeeklyStatAsync(
            KeytietkiemDbContext db,
            DateOnly weekStartDate,
            CancellationToken ct)
        {
            var (weekStart, weekEnd) = GetWeekRange(weekStartDate);

            var tickets = await db.Tickets
                .Where(t => t.CreatedAt >= weekStart && t.CreatedAt < weekEnd)
                .ToListAsync(ct);

            if (!tickets.Any())
                return;

            var groups = tickets
                .GroupBy(t => new { Severity = t.Severity ?? "Unknown", t.PriorityLevel });

            foreach (var g in groups)
            {
                var severity = g.Key.Severity;
                var priority = g.Key.PriorityLevel;

                var totalTickets = g.Count();

                var frtTickets = g
                    .Where(t => t.FirstRespondedAt.HasValue)
                    .ToList();

                var resolvedTickets = g
                    .Where(t => t.ResolvedAt.HasValue)
                    .ToList();

                decimal? avgFrt = null;
                if (frtTickets.Any())
                {
                    avgFrt = (decimal)frtTickets.Average(t =>
                        (t.FirstRespondedAt!.Value - t.CreatedAt).TotalMinutes);
                }

                decimal? avgResolution = null;
                if (resolvedTickets.Any())
                {
                    avgResolution = (decimal)resolvedTickets.Average(t =>
                        (t.ResolvedAt!.Value - t.CreatedAt).TotalMinutes);
                }

                var responseSlaTotal = frtTickets
                    .Count(t => t.FirstResponseDueAt.HasValue);
                var responseSlaMet = frtTickets
                    .Count(t => t.FirstResponseDueAt.HasValue &&
                                t.FirstRespondedAt!.Value <= t.FirstResponseDueAt.Value);

                var resolutionSlaTotal = resolvedTickets
                    .Count(t => t.ResolutionDueAt.HasValue);
                var resolutionSlaMet = resolvedTickets
                    .Count(t => t.ResolutionDueAt.HasValue &&
                                t.ResolvedAt!.Value <= t.ResolutionDueAt.Value);

                var stat = await db.SupportTicketSeverityPriorityWeeklyStats
                    .FirstOrDefaultAsync(s =>
                        s.WeekStartDate == weekStartDate &&
                        s.Severity == severity &&
                        s.PriorityLevel == priority,
                        ct);

                if (stat == null)
                {
                    stat = new SupportTicketSeverityPriorityWeeklyStat
                    {
                        WeekStartDate = weekStartDate,
                        Severity = severity,
                        PriorityLevel = priority
                    };
                    db.SupportTicketSeverityPriorityWeeklyStats.Add(stat);
                }

                stat.TicketsCount = totalTickets;
                stat.ResponseSlaMetCount = responseSlaMet;
                stat.ResponseSlaTotalCount = responseSlaTotal;
                stat.ResolutionSlaMetCount = resolutionSlaMet;
                stat.ResolutionSlaTotalCount = resolutionSlaTotal;
                stat.AvgFirstResponseMinutes = avgFrt;
                stat.AvgResolutionMinutes = avgResolution;
            }
        }

        // =======================================
        // =   WEEKLY - CHAT BY PRIORITY        =
        // =======================================

        private async Task RebuildChatPriorityWeeklyStatAsync(
            KeytietkiemDbContext db,
            DateOnly weekStartDate,
            CancellationToken ct)
        {
            var (weekStart, weekEnd) = GetWeekRange(weekStartDate);

            var sessions = await db.SupportChatSessions
                .Where(s => s.StartedAt >= weekStart && s.StartedAt < weekEnd)
                .ToListAsync(ct);

            if (!sessions.Any())
                return;

            var sessionIds = sessions.Select(s => s.ChatSessionId).ToList();

            var messages = await db.SupportChatMessages
                .Where(m => sessionIds.Contains(m.ChatSessionId))
                .OrderBy(m => m.SentAt)
                .ToListAsync(ct);

            var msgsBySession = messages
                .GroupBy(m => m.ChatSessionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var groups = sessions.GroupBy(s => s.PriorityLevel);

            foreach (var g in groups)
            {
                var priority = g.Key;
                var sessionsForPriority = g.ToList();

                var frtList = new List<double>();
                var durationList = new List<double>();
                var b0_5 = 0;
                var b5_10 = 0;
                var b10_20 = 0;
                var b20Plus = 0;

                foreach (var session in sessionsForPriority)
                {
                    msgsBySession.TryGetValue(session.ChatSessionId, out var sessionMsgs);
                    sessionMsgs ??= new List<SupportChatMessage>();

                    var customerFirst = sessionMsgs.FirstOrDefault(m => !m.IsFromStaff);
                    if (customerFirst != null)
                    {
                        var staffFirst = sessionMsgs.FirstOrDefault(m =>
                            m.IsFromStaff && m.SentAt >= customerFirst.SentAt);
                        if (staffFirst != null)
                        {
                            var minutes = (staffFirst.SentAt - customerFirst.SentAt).TotalMinutes;
                            if (minutes >= 0)
                                frtList.Add(minutes);
                        }
                    }

                    var end = session.ClosedAt ?? session.LastMessageAt ?? session.StartedAt;
                    var dur = (end - session.StartedAt).TotalMinutes;
                    if (dur < 0) dur = 0;
                    durationList.Add(dur);

                    // Histogram buckets
                    if (dur < 5) b0_5++;
                    else if (dur < 10) b5_10++;
                    else if (dur < 20) b10_20++;
                    else b20Plus++;
                }

                decimal? avgFrt = frtList.Any() ? (decimal?)frtList.Average() : null;
                decimal? avgDur = durationList.Any() ? (decimal?)durationList.Average() : null;

                var stat = await db.SupportChatPriorityWeeklyStats
                    .FirstOrDefaultAsync(s =>
                        s.WeekStartDate == weekStartDate &&
                        s.PriorityLevel == priority,
                        ct);

                if (stat == null)
                {
                    stat = new SupportChatPriorityWeeklyStat
                    {
                        WeekStartDate = weekStartDate,
                        PriorityLevel = priority
                    };
                    db.SupportChatPriorityWeeklyStats.Add(stat);
                }

                stat.SessionsCount = sessionsForPriority.Count;
                stat.AvgFirstResponseMinutes = avgFrt;
                stat.AvgDurationMinutes = avgDur;
                stat.Duration05Count = b0_5;
                stat.Duration510Count = b5_10;
                stat.Duration1020Count = b10_20;
                stat.Duration20plusCount = b20Plus;
            }
        }

        // =======================================
        // =   MONTHLY - SUPPORT PLAN / LOYALTY  =
        // =======================================

        private static (DateTime Start, DateTime End) GetMonthRange(string yearMonth)
        {
            // yearMonth: "YYYY-MM"
            if (string.IsNullOrWhiteSpace(yearMonth) || yearMonth.Length != 7)
                throw new ArgumentException("Invalid YearMonth format, expected 'YYYY-MM'.", nameof(yearMonth));

            var year = int.Parse(yearMonth[..4]);
            var month = int.Parse(yearMonth[5..7]);
            var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);
            return (start, end);
        }

        private static bool IsSubscriptionActiveAt(UserSupportPlanSubscription sub, DateTime at)
        {
            // Tuỳ hệ thống: chỉnh lại cho đúng status thực tế
            if (!string.Equals(sub.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(sub.Status, "Trial", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (sub.ExpiresAt == null) return true;
            return sub.ExpiresAt.Value > at;
        }

        private async Task RebuildSupportPlanMonthlyStatsAsync(
            KeytietkiemDbContext db,
            string yearMonth,
            CancellationToken ct)
        {
            var (monthStart, monthEnd) = GetMonthRange(yearMonth);

            var plans = await db.SupportPlans
                .Where(p => p.IsActive)
                .ToListAsync(ct);

            if (!plans.Any())
                return;

            // Subscriptions liên quan tới khoảng thời gian này
            var subsInWindow = await db.UserSupportPlanSubscriptions
                .Include(s => s.SupportPlan)
                .Include(s => s.Payment)
                .Where(s =>
                    s.StartedAt < monthEnd &&
                    (s.ExpiresAt == null || s.ExpiresAt >= monthStart))
                .ToListAsync(ct);

            // Tickets & chat trong tháng
            var ticketsInMonth = await db.Tickets
                .Where(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd)
                .ToListAsync(ct);

            var chatsInMonth = await db.SupportChatSessions
                .Where(s => s.StartedAt >= monthStart && s.StartedAt < monthEnd)
                .ToListAsync(ct);

            foreach (var plan in plans)
            {
                var subsForPlan = subsInWindow
                    .Where(s => s.SupportPlanId == plan.SupportPlanId)
                    .ToList();

                if (!subsForPlan.Any())
                {
                    // Vẫn upsert record với 0 để FE dễ query
                    var emptyStat = await db.SupportPlanMonthlyStats
                        .FirstOrDefaultAsync(x =>
                            x.YearMonth == yearMonth &&
                            x.SupportPlanId == plan.SupportPlanId,
                            ct);

                    if (emptyStat == null)
                    {
                        emptyStat = new SupportPlanMonthlyStat
                        {
                            YearMonth = yearMonth,
                            SupportPlanId = plan.SupportPlanId,
                            ActiveSubscriptionsCount = 0,
                            NewSubscriptionsCount = 0,
                            SupportPlanRevenue = 0,
                            TicketsCount = 0,
                            ChatSessionsCount = 0
                        };
                        db.SupportPlanMonthlyStats.Add(emptyStat);
                    }
                    else
                    {
                        emptyStat.ActiveSubscriptionsCount = 0;
                        emptyStat.NewSubscriptionsCount = 0;
                        emptyStat.SupportPlanRevenue = 0;
                        emptyStat.TicketsCount = 0;
                        emptyStat.ChatSessionsCount = 0;
                    }

                    continue;
                }

                var activeAtEnd = subsForPlan
                    .Count(s => IsSubscriptionActiveAt(s, monthEnd));

                var newSubsCount = subsForPlan
                    .Count(s => s.StartedAt >= monthStart && s.StartedAt < monthEnd);

                // Revenue từ payment gắn với subscription
                var revenue = subsForPlan
                    .Where(s => s.Payment != null)
                    .Where(s =>
                        s.Payment!.TransactionType == "SERVICE_PAYMENT" &&
                        s.Payment.Status == "Paid" &&
                        s.Payment.CreatedAt >= monthStart &&
                        s.Payment.CreatedAt < monthEnd)
                    .Sum(s => s.Payment!.Amount);

                // UserIds có plan này trong tháng (simple window overlap)
                var userIds = subsForPlan
                    .Select(s => s.UserId)
                    .Distinct()
                    .ToList();

                var ticketsCount = ticketsInMonth
                    .Count(t => userIds.Contains(t.UserId));

                var chatCount = chatsInMonth
                    .Count(s => userIds.Contains(s.CustomerId));

                var stat = await db.SupportPlanMonthlyStats
                    .FirstOrDefaultAsync(x =>
                        x.YearMonth == yearMonth &&
                        x.SupportPlanId == plan.SupportPlanId,
                        ct);

                if (stat == null)
                {
                    stat = new SupportPlanMonthlyStat
                    {
                        YearMonth = yearMonth,
                        SupportPlanId = plan.SupportPlanId
                    };
                    db.SupportPlanMonthlyStats.Add(stat);
                }

                stat.ActiveSubscriptionsCount = activeAtEnd;
                stat.NewSubscriptionsCount = newSubsCount;
                stat.SupportPlanRevenue = revenue;
                stat.TicketsCount = ticketsCount;
                stat.ChatSessionsCount = chatCount;
            }
        }
    }

    /// <summary>
    /// Job cho BackgroundJobScheduler: định kỳ gọi SupportStatsUpdateService.RebuildAllAsync().
    /// </summary>
    public sealed class SupportStatsBackgroundJob : IBackgroundJob
    {
        public string Name => "SupportStatsBackgroundJob";

        // Điều chỉnh thời gian chạy lại (ví dụ: 5 phút 1 lần)
        public TimeSpan Interval => TimeSpan.FromMinutes(5);

        public async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<SupportStatsBackgroundJob>>();
            var updater = serviceProvider.GetRequiredService<ISupportStatsUpdateService>();

            logger.LogInformation("SupportStatsBackgroundJob: executing at {NowUtc}",
                serviceProvider.GetRequiredService<IClock>().UtcNow);

            await updater.RebuildAllAsync(cancellationToken);
        }
    }
}
