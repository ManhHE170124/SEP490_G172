// File: Controllers/SupportDashboardAdminController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.DTOs.Support;
using Keytietkiem.Infrastructure;
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using static Keytietkiem.Constants.ModuleCodes;
using static Keytietkiem.Constants.PermissionCodes;
using Keytietkiem.DTOs.Common;
using Keytietkiem.DTOs.Support;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Keytietkiem.Controllers
{
    /// <summary>
    /// API cho màn Support Dashboard (Admin / Customer Care Manager).
    /// Đọc dữ liệu từ các bảng thống kê:
    ///   - SupportDailyStats
    ///   - SupportStaffDailyStats
    ///   - SupportTicketSeverityPriorityWeeklyStats
    ///   - SupportChatPriorityWeeklyStats
    ///   - SupportPlanMonthlyStats
    /// Và một phần từ bảng gốc (Ticket, SupportChatSession, User, UserSupportPlanSubscription, SupportPlan).
    /// </summary>
    [ApiController]
    [Route("api/support-dashboard-admin")]
    public class SupportDashboardAdminController : ControllerBase
    {
        private readonly IDbContextFactory<KeytietkiemDbContext> _dbFactory;
        private readonly IClock _clock;
        private readonly ILogger<SupportDashboardAdminController> _logger;

        public SupportDashboardAdminController(
            IDbContextFactory<KeytietkiemDbContext> dbFactory,
            IClock clock,
            ILogger<SupportDashboardAdminController> logger)
        {
            _dbFactory = dbFactory;
            _clock = clock;
            _logger = logger;
        }

        // ============================================================
        // = 1. OVERVIEW - TỔNG QUAN                                 =
        // ============================================================

        /// <summary>
        /// Tổng quan tình hình hỗ trợ:
        /// - Open tickets hiện tại (bảng Ticket)
        /// - New / Closed tickets + chat sessions trong N ngày gần nhất (SupportDailyStats)
        /// - Tỉ lệ SLA phản hồi / xử lý trong N ngày
        /// - Line chart daily trend
        /// - Ticket vs Chat theo tuần trong tháng (dựa theo SupportDailyStats)
        /// </summary>
        [HttpGet("overview")]
        [RequirePermission(ModuleCodes.SUPPORT_DASHBOARD, PermissionCodes.VIEW_LIST)]
        public async Task<ActionResult<SupportOverviewDto>> GetOverview(
            [FromQuery] int days = 7,
            [FromQuery] string? yearMonth = null,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0) days = 7;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var today = DateOnly.FromDateTime(nowUtc.Date);
            var fromDate = today.AddDays(1 - days);

            // 1) Open tickets hiện tại (backlog)
            string[] openStatuses =
            {
                "New",
                "InProgress",
                "Pending",
                "WaitingCustomer",
                "WaitingThirdParty"
            };

            var openTicketsNow = await db.Tickets
                .Where(t => openStatuses.Contains(t.Status))
                .CountAsync(cancellationToken);

            // 2) Daily stats trong N ngày
            var dailyStats = await db.SupportDailyStats
                .Where(s => s.StatDate >= fromDate && s.StatDate <= today)
                .OrderBy(s => s.StatDate)
                .ToListAsync(cancellationToken);

            var newTicketsLastNDays = dailyStats.Sum(s => s.NewTicketsCount);
            var closedTicketsLastNDays = dailyStats.Sum(s => s.ClosedTicketsCount);
            var newChatSessionsLastNDays = dailyStats.Sum(s => s.NewChatSessionsCount);

            var responseSlaMet = dailyStats.Sum(s => s.TicketResponseSlaMetCount);
            var responseSlaTotal = dailyStats.Sum(s => s.TicketResponseSlaTotalCount);
            var resolutionSlaMet = dailyStats.Sum(s => s.TicketResolutionSlaMetCount);
            var resolutionSlaTotal = dailyStats.Sum(s => s.TicketResolutionSlaTotalCount);

            decimal? responseSlaRate = responseSlaTotal > 0
                ? (decimal)responseSlaMet / responseSlaTotal
                : null;

            decimal? resolutionSlaRate = resolutionSlaTotal > 0
                ? (decimal)resolutionSlaMet / resolutionSlaTotal
                : null;

            var dailyTrend = dailyStats
                .Select(s => new DailyTrendPointDto
                {
                    Date = s.StatDate,
                    NewTickets = s.NewTicketsCount,
                    ClosedTickets = s.ClosedTicketsCount,
                    NewChatSessions = s.NewChatSessionsCount,
                    OpenTicketsEndOfDay = s.OpenTicketsCountEndOfDay
                })
                .ToList();

            // 3) Ticket vs Chat theo tuần trong tháng (đang chọn)
            DateOnly monthStart;
            if (!string.IsNullOrWhiteSpace(yearMonth))
            {
                if (!TryParseYearMonth(yearMonth, out monthStart))
                {
                    return BadRequest("Invalid yearMonth format. Expected 'YYYY-MM'.");
                }
            }
            else
            {
                monthStart = new DateOnly(today.Year, today.Month, 1);
            }

            var monthEnd = monthStart.AddMonths(1);

            var monthStats = await db.SupportDailyStats
                .Where(s => s.StatDate >= monthStart && s.StatDate < monthEnd)
                .ToListAsync(cancellationToken);

            var weeklyTicketChat = monthStats
                .GroupBy(s => GetWeekStart(s.StatDate))
                .OrderBy(g => g.Key)
                .Select(g => new WeeklyTicketChatPointDto
                {
                    WeekStartDate = g.Key,
                    TicketCount = g.Sum(x => x.NewTicketsCount),
                    ChatSessionCount = g.Sum(x => x.NewChatSessionsCount)
                })
                .ToList();

            var dto = new SupportOverviewDto
            {
                OpenTicketsNow = openTicketsNow,
                NewTicketsLastNDays = newTicketsLastNDays,
                ClosedTicketsLastNDays = closedTicketsLastNDays,
                NewChatSessionsLastNDays = newChatSessionsLastNDays,
                TicketResponseSlaRateLastNDays = responseSlaRate,
                TicketResolutionSlaRateLastNDays = resolutionSlaRate,
                DailyTrend = dailyTrend,
                WeeklyTicketChat = weeklyTicketChat
            };

            return Ok(dto);
        }

        // ============================================================
        // = 2. TICKET & SLA                                          =
        // ============================================================

        [HttpGet("tickets/daily")]
        [RequirePermission(ModuleCodes.SUPPORT_DASHBOARD, PermissionCodes.VIEW_LIST)]
        public async Task<ActionResult<List<TicketDailyKpiDto>>> GetTicketDailyKpi(
            [FromQuery] int days = 30,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0) days = 30;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var today = DateOnly.FromDateTime(nowUtc.Date);
            var fromDate = today.AddDays(1 - days);

            var dailyStats = await db.SupportDailyStats
                .Where(s => s.StatDate >= fromDate && s.StatDate <= today)
                .OrderBy(s => s.StatDate)
                .ToListAsync(cancellationToken);

            var result = dailyStats
                .Select(s => new TicketDailyKpiDto
                {
                    StatDate = s.StatDate,
                    NewTicketsCount = s.NewTicketsCount,
                    ClosedTicketsCount = s.ClosedTicketsCount,
                    AvgFirstResponseMinutes = s.AvgTicketFirstResponseMinutes,
                    AvgResolutionMinutes = s.AvgTicketResolutionMinutes,
                    AvgFirstResponseSlaRatio = s.AvgTicketFirstResponseSlaRatio,
                    AvgResolutionSlaRatio = s.AvgTicketResolutionSlaRatio,
                    ResponseSlaMetCount = s.TicketResponseSlaMetCount,
                    ResponseSlaTotalCount = s.TicketResponseSlaTotalCount,
                    ResolutionSlaMetCount = s.TicketResolutionSlaMetCount,
                    ResolutionSlaTotalCount = s.TicketResolutionSlaTotalCount
                })
                .ToList();

            return Ok(result);
        }

        [HttpGet("tickets/weekly-severity-priority")]
        [RequirePermission(ModuleCodes.SUPPORT_DASHBOARD, PermissionCodes.VIEW_LIST)]
        public async Task<ActionResult<List<TicketSeverityPriorityWeeklyDto>>> GetTicketSeverityPriorityWeekly(
            [FromQuery] int weeks = 8,
            CancellationToken cancellationToken = default)
        {
            if (weeks <= 0) weeks = 8;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var today = DateOnly.FromDateTime(nowUtc.Date);
            var fromDate = today.AddDays(-7 * weeks);

            var stats = await db.SupportTicketSeverityPriorityWeeklyStats
                .Where(s => s.WeekStartDate >= fromDate)
                .OrderBy(s => s.WeekStartDate)
                .ThenBy(s => s.Severity)
                .ThenBy(s => s.PriorityLevel)
                .ToListAsync(cancellationToken);

            var result = stats
                .Select(s => new TicketSeverityPriorityWeeklyDto
                {
                    WeekStartDate = s.WeekStartDate,
                    Severity = s.Severity,
                    PriorityLevel = s.PriorityLevel,
                    TicketsCount = s.TicketsCount,
                    ResponseSlaMetCount = s.ResponseSlaMetCount,
                    ResponseSlaTotalCount = s.ResponseSlaTotalCount,
                    ResolutionSlaMetCount = s.ResolutionSlaMetCount,
                    ResolutionSlaTotalCount = s.ResolutionSlaTotalCount,
                    AvgFirstResponseMinutes = s.AvgFirstResponseMinutes,
                    AvgResolutionMinutes = s.AvgResolutionMinutes
                })
                .ToList();

            return Ok(result);
        }

        [HttpGet("tickets/priority-distribution")]
        [RequirePermission(ModuleCodes.SUPPORT_DASHBOARD, PermissionCodes.VIEW_LIST)]
        public async Task<ActionResult<List<TicketPriorityDistributionDto>>> GetTicketPriorityDistribution(
            [FromQuery] int days = 30,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0) days = 30;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var fromUtc = nowUtc.Date.AddDays(-days);

            var data = await db.Tickets
                .Where(t => t.CreatedAt >= fromUtc && t.CreatedAt <= nowUtc)
                .GroupBy(t => t.PriorityLevel)
                .Select(g => new TicketPriorityDistributionDto
                {
                    PriorityLevel = g.Key,
                    TicketCount = g.Count()
                })
                .OrderBy(x => x.PriorityLevel)
                .ToListAsync(cancellationToken);

            return Ok(data);
        }

        // ============================================================
        // = 3. LIVE CHAT KPI                                         =
        // ============================================================

        [HttpGet("chat/daily")]
        [RequirePermission(ModuleCodes.SUPPORT_DASHBOARD, PermissionCodes.VIEW_LIST)]
        public async Task<ActionResult<List<ChatDailyKpiDto>>> GetChatDailyKpi(
            [FromQuery] int days = 30,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0) days = 30;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var today = DateOnly.FromDateTime(nowUtc.Date);
            var fromDate = today.AddDays(1 - days);

            var dailyStats = await db.SupportDailyStats
                .Where(s => s.StatDate >= fromDate && s.StatDate <= today)
                .OrderBy(s => s.StatDate)
                .ToListAsync(cancellationToken);

            var result = dailyStats
                .Select(s => new ChatDailyKpiDto
                {
                    StatDate = s.StatDate,
                    NewChatSessionsCount = s.NewChatSessionsCount,
                    AvgFirstResponseMinutes = s.AvgChatFirstResponseMinutes,
                    AvgDurationMinutes = s.AvgChatDurationMinutes,
                    AvgMessagesPerSession = s.AvgChatMessagesPerSession
                })
                .ToList();

            return Ok(result);
        }

        [HttpGet("chat/weekly-priority")]
        [RequirePermission(ModuleCodes.SUPPORT_DASHBOARD, PermissionCodes.VIEW_LIST)]
        public async Task<ActionResult<List<ChatPriorityWeeklyDto>>> GetChatPriorityWeekly(
            [FromQuery] int weeks = 8,
            CancellationToken cancellationToken = default)
        {
            if (weeks <= 0) weeks = 8;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var today = DateOnly.FromDateTime(nowUtc.Date);
            var fromDate = today.AddDays(-7 * weeks);

            var stats = await db.SupportChatPriorityWeeklyStats
                .Where(s => s.WeekStartDate >= fromDate)
                .OrderBy(s => s.WeekStartDate)
                .ThenBy(s => s.PriorityLevel)
                .ToListAsync(cancellationToken);

            var result = stats
                .Select(s => new ChatPriorityWeeklyDto
                {
                    WeekStartDate = s.WeekStartDate,
                    PriorityLevel = s.PriorityLevel,
                    SessionsCount = s.SessionsCount,
                    AvgFirstResponseMinutes = s.AvgFirstResponseMinutes,
                    AvgDurationMinutes = s.AvgDurationMinutes,
                    Duration05Count = s.Duration05Count,
                    Duration510Count = s.Duration510Count,
                    Duration1020Count = s.Duration1020Count,
                    Duration20plusCount = s.Duration20plusCount
                })
                .ToList();

            return Ok(result);
        }

        // ============================================================
        // = 4. STAFF PERFORMANCE                                     =
        // ============================================================

        [HttpGet("staff/performance")]
        [RequirePermission(ModuleCodes.SUPPORT_DASHBOARD, PermissionCodes.VIEW_LIST)]
        public async Task<ActionResult<List<StaffPerformanceSummaryDto>>> GetStaffPerformance(
            [FromQuery] int days = 30,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0) days = 30;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var today = DateOnly.FromDateTime(nowUtc.Date);
            var fromDate = today.AddDays(1 - days);

            var stats = await db.SupportStaffDailyStats
                .Where(s => s.StatDate >= fromDate && s.StatDate <= today)
                .ToListAsync(cancellationToken);

            if (!stats.Any())
            {
                return Ok(new List<StaffPerformanceSummaryDto>());
            }

            var grouped = stats.GroupBy(s => s.StaffId).ToList();
            var staffIds = grouped.Select(g => g.Key).ToList();

            var staffInfos = await db.Users
                .Where(u => staffIds.Contains(u.UserId))
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email
                })
                .ToListAsync(cancellationToken);

            var staffDict = staffInfos.ToDictionary(x => x.UserId, x => x);

            var result = new List<StaffPerformanceSummaryDto>();

            foreach (var g in grouped)
            {
                var staffId = g.Key;

                var ticketsAssigned = g.Sum(x => x.TicketsAssignedCount);
                var ticketsResolved = g.Sum(x => x.TicketsResolvedCount);

                decimal? avgFrt = null;
                decimal? avgResolution = null;

                if (ticketsAssigned > 0)
                {
                    var frtWeightedSum = g
                        .Where(x => x.AvgTicketFirstResponseMinutes.HasValue && x.TicketsAssignedCount > 0)
                        .Sum(x => x.AvgTicketFirstResponseMinutes!.Value * x.TicketsAssignedCount);

                    if (frtWeightedSum > 0)
                        avgFrt = frtWeightedSum / ticketsAssigned;

                    var resWeightedSum = g
                        .Where(x => x.AvgTicketResolutionMinutes.HasValue && x.TicketsAssignedCount > 0)
                        .Sum(x => x.AvgTicketResolutionMinutes!.Value * x.TicketsAssignedCount);

                    if (resWeightedSum > 0)
                        avgResolution = resWeightedSum / ticketsAssigned;
                }

                var responseSlaMet = g.Sum(x => x.TicketResponseSlaMetCount);
                var responseSlaTotal = g.Sum(x => x.TicketResponseSlaTotalCount);
                var resolutionSlaMet = g.Sum(x => x.TicketResolutionSlaMetCount);
                var resolutionSlaTotal = g.Sum(x => x.TicketResolutionSlaTotalCount);

                decimal? responseSlaRate = responseSlaTotal > 0
                    ? (decimal)responseSlaMet / responseSlaTotal
                    : null;

                decimal? resolutionSlaRate = resolutionSlaTotal > 0
                    ? (decimal)resolutionSlaMet / resolutionSlaTotal
                    : null;

                var chatSessionsHandled = g.Sum(x => x.ChatSessionsHandledCount);

                decimal? avgChatFrt = null;
                decimal? avgChatDuration = null;

                if (chatSessionsHandled > 0)
                {
                    var chatFrtWeightedSum = g
                        .Where(x => x.AvgChatFirstResponseMinutes.HasValue && x.ChatSessionsHandledCount > 0)
                        .Sum(x => x.AvgChatFirstResponseMinutes!.Value * x.ChatSessionsHandledCount);

                    if (chatFrtWeightedSum > 0)
                        avgChatFrt = chatFrtWeightedSum / chatSessionsHandled;

                    var chatDurWeightedSum = g
                        .Where(x => x.AvgChatDurationMinutes.HasValue && x.ChatSessionsHandledCount > 0)
                        .Sum(x => x.AvgChatDurationMinutes!.Value * x.ChatSessionsHandledCount);

                    if (chatDurWeightedSum > 0)
                        avgChatDuration = chatDurWeightedSum / chatSessionsHandled;
                }

                var ticketStaffMessages = g.Sum(x => x.TicketStaffMessagesCount);
                var chatStaffMessages = g.Sum(x => x.ChatStaffMessagesCount);

                staffDict.TryGetValue(staffId, out var staffInfo);

                result.Add(new StaffPerformanceSummaryDto
                {
                    StaffId = staffId,
                    FullName = staffInfo?.FullName ?? "",
                    Email = staffInfo?.Email ?? "",
                    TicketsAssignedCount = ticketsAssigned,
                    TicketsResolvedCount = ticketsResolved,
                    AvgTicketFirstResponseMinutes = avgFrt,
                    AvgTicketResolutionMinutes = avgResolution,
                    TicketResponseSlaRate = responseSlaRate,
                    TicketResolutionSlaRate = resolutionSlaRate,
                    ChatSessionsHandledCount = chatSessionsHandled,
                    AvgChatFirstResponseMinutes = avgChatFrt,
                    AvgChatDurationMinutes = avgChatDuration,
                    TicketStaffMessagesCount = ticketStaffMessages,
                    ChatStaffMessagesCount = chatStaffMessages
                });
            }

            result = result
                .OrderByDescending(x => x.TicketsResolvedCount)
                .ToList();

            return Ok(result);
        }

        // ============================================================
        // = 5. SUPPORT PLAN & LOYALTY                                =
        // ============================================================

        [HttpGet("plans/active-distribution")]
        [RequirePermission(ModuleCodes.SUPPORT_DASHBOARD, PermissionCodes.VIEW_LIST)]
        public async Task<ActionResult<List<ActiveSupportPlanDistributionDto>>> GetActiveSupportPlanDistribution(
            CancellationToken cancellationToken = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            string[] activeStatuses = { "Active", "Trial" };

            var subs = await db.UserSupportPlanSubscriptions
                .Where(s =>
                    activeStatuses.Contains(s.Status) &&
                    (s.ExpiresAt == null || s.ExpiresAt > nowUtc))
                .GroupBy(s => s.SupportPlanId)
                .Select(g => new
                {
                    SupportPlanId = g.Key,
                    ActiveSubscriptions = g.Count()
                })
                .ToListAsync(cancellationToken);

            if (!subs.Any())
            {
                return Ok(new List<ActiveSupportPlanDistributionDto>());
            }

            var planIds = subs.Select(s => s.SupportPlanId).ToList();

            var plans = await db.SupportPlans
                .Where(p => planIds.Contains(p.SupportPlanId))
                .Select(p => new
                {
                    p.SupportPlanId,
                    p.Name,
                    p.PriorityLevel,
                    p.Price
                })
                .ToListAsync(cancellationToken);

            var planDict = plans.ToDictionary(p => p.SupportPlanId, p => p);

            var result = subs
                .Select(s =>
                {
                    planDict.TryGetValue(s.SupportPlanId, out var p);
                    return new ActiveSupportPlanDistributionDto
                    {
                        SupportPlanId = s.SupportPlanId,
                        PlanName = p?.Name ?? "",
                        PriorityLevel = p?.PriorityLevel ?? 0,
                        Price = p?.Price ?? 0,
                        ActiveSubscriptionsCount = s.ActiveSubscriptions
                    };
                })
                .OrderByDescending(x => x.ActiveSubscriptionsCount)
                .ToList();

            return Ok(result);
        }

        [HttpGet("plans/monthly-stats")]
        [RequirePermission(ModuleCodes.SUPPORT_DASHBOARD, PermissionCodes.VIEW_LIST)]
        public async Task<ActionResult<List<SupportPlanMonthlyStatDto>>> GetSupportPlanMonthlyStats(
            [FromQuery] int months = 6,
            CancellationToken cancellationToken = default)
        {
            if (months <= 0) months = 6;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var today = DateOnly.FromDateTime(nowUtc.Date);
            var anchorMonth = new DateOnly(today.Year, today.Month, 1);

            var yearMonths = new List<string>();
            for (int i = 0; i < months; i++)
            {
                var m = anchorMonth.AddMonths(-i);
                yearMonths.Add($"{m.Year:D4}-{m.Month:D2}");
            }

            var stats = await db.SupportPlanMonthlyStats
                .Where(s => yearMonths.Contains(s.YearMonth))
                .ToListAsync(cancellationToken);

            if (!stats.Any())
            {
                return Ok(new List<SupportPlanMonthlyStatDto>());
            }

            var planIds = stats
                .Select(s => s.SupportPlanId)
                .Distinct()
                .ToList();

            var plans = await db.SupportPlans
                .Where(p => planIds.Contains(p.SupportPlanId))
                .Select(p => new
                {
                    p.SupportPlanId,
                    p.Name,
                    p.PriorityLevel,
                    p.Price
                })
                .ToListAsync(cancellationToken);

            var planDict = plans.ToDictionary(p => p.SupportPlanId, p => p);

            var result = stats
                .Select(s =>
                {
                    planDict.TryGetValue(s.SupportPlanId, out var p);

                    return new SupportPlanMonthlyStatDto
                    {
                        YearMonth = s.YearMonth,
                        SupportPlanId = s.SupportPlanId,
                        PlanName = p?.Name ?? "",
                        PriorityLevel = p?.PriorityLevel ?? 0,
                        Price = p?.Price ?? 0,
                        ActiveSubscriptionsCount = s.ActiveSubscriptionsCount,
                        NewSubscriptionsCount = s.NewSubscriptionsCount,
                        SupportPlanRevenue = s.SupportPlanRevenue,
                        TicketsCount = s.TicketsCount,
                        ChatSessionsCount = s.ChatSessionsCount
                    };
                })
                .OrderBy(x => x.YearMonth)
                .ThenBy(x => x.PriorityLevel)
                .ToList();

            return Ok(result);
        }

        [HttpGet("segments/priority-distribution")]
        [RequirePermission(ModuleCodes.SUPPORT_DASHBOARD, PermissionCodes.VIEW_LIST)]
        public async Task<ActionResult<List<PriorityDistributionDto>>> GetPriorityDistribution(
            CancellationToken cancellationToken = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var users = await db.Users
                .Where(u => !u.IsTemp)
                .GroupBy(u => u.SupportPriorityLevel)
                .Select(g => new PriorityDistributionDto
                {
                    PriorityLevel = g.Key,
                    UserCount = g.Count()
                })
                .OrderBy(x => x.PriorityLevel)
                .ToListAsync(cancellationToken);

            return Ok(users);
        }

        [HttpGet("segments/priority-support-volume")]
        [RequirePermission(ModuleCodes.SUPPORT_DASHBOARD, PermissionCodes.VIEW_LIST)]
        public async Task<ActionResult<List<PrioritySupportVolumeDto>>> GetPrioritySupportVolume(
            [FromQuery] int weeks = 8,
            CancellationToken cancellationToken = default)
        {
            if (weeks <= 0) weeks = 8;

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var nowUtc = _clock.UtcNow;
            var today = DateOnly.FromDateTime(nowUtc.Date);
            var fromDate = today.AddDays(-7 * weeks);

            var ticketStats = await db.SupportTicketSeverityPriorityWeeklyStats
                .Where(s => s.WeekStartDate >= fromDate)
                .ToListAsync(cancellationToken);

            var chatStats = await db.SupportChatPriorityWeeklyStats
                .Where(s => s.WeekStartDate >= fromDate)
                .ToListAsync(cancellationToken);

            var priorityLevels = ticketStats
                .Select(s => s.PriorityLevel)
                .Concat(chatStats.Select(c => c.PriorityLevel))
                .Distinct()
                .ToList();

            var result = new List<PrioritySupportVolumeDto>();

            foreach (var priority in priorityLevels.OrderBy(x => x))
            {
                var ticketGroup = ticketStats
                    .Where(s => s.PriorityLevel == priority)
                    .ToList();

                var chatGroup = chatStats
                    .Where(s => s.PriorityLevel == priority)
                    .ToList();

                var ticketCount = ticketGroup.Sum(x => x.TicketsCount);

                decimal? avgTicketFrt = null;
                decimal? avgTicketResolution = null;

                if (ticketCount > 0)
                {
                    var frtWeightedSum = ticketGroup
                        .Where(x => x.AvgFirstResponseMinutes.HasValue && x.TicketsCount > 0)
                        .Sum(x => x.AvgFirstResponseMinutes!.Value * x.TicketsCount);

                    if (frtWeightedSum > 0)
                        avgTicketFrt = frtWeightedSum / ticketCount;

                    var resWeightedSum = ticketGroup
                        .Where(x => x.AvgResolutionMinutes.HasValue && x.TicketsCount > 0)
                        .Sum(x => x.AvgResolutionMinutes!.Value * x.TicketsCount);

                    if (resWeightedSum > 0)
                        avgTicketResolution = resWeightedSum / ticketCount;
                }

                var responseSlaMet = ticketGroup.Sum(x => x.ResponseSlaMetCount);
                var responseSlaTotal = ticketGroup.Sum(x => x.ResponseSlaTotalCount);
                var resolutionSlaMet = ticketGroup.Sum(x => x.ResolutionSlaMetCount);
                var resolutionSlaTotal = ticketGroup.Sum(x => x.ResolutionSlaTotalCount);

                decimal? ticketResponseSlaRate = responseSlaTotal > 0
                    ? (decimal)responseSlaMet / responseSlaTotal
                    : null;

                decimal? ticketResolutionSlaRate = resolutionSlaTotal > 0
                    ? (decimal)resolutionSlaMet / resolutionSlaTotal
                    : null;

                var chatSessionsCount = chatGroup.Sum(x => x.SessionsCount);

                decimal? avgChatFrt = null;
                decimal? avgChatDuration = null;

                if (chatSessionsCount > 0)
                {
                    var frtWeightedSum = chatGroup
                        .Where(x => x.AvgFirstResponseMinutes.HasValue && x.SessionsCount > 0)
                        .Sum(x => x.AvgFirstResponseMinutes!.Value * x.SessionsCount);

                    if (frtWeightedSum > 0)
                        avgChatFrt = frtWeightedSum / chatSessionsCount;

                    var durWeightedSum = chatGroup
                        .Where(x => x.AvgDurationMinutes.HasValue && x.SessionsCount > 0)
                        .Sum(x => x.AvgDurationMinutes!.Value * x.SessionsCount);

                    if (durWeightedSum > 0)
                        avgChatDuration = durWeightedSum / chatSessionsCount;
                }

                result.Add(new PrioritySupportVolumeDto
                {
                    PriorityLevel = priority,
                    TicketCount = ticketCount,
                    AvgTicketFirstResponseMinutes = avgTicketFrt,
                    AvgTicketResolutionMinutes = avgTicketResolution,
                    TicketResponseSlaRate = ticketResponseSlaRate,
                    TicketResolutionSlaRate = ticketResolutionSlaRate,
                    ChatSessionsCount = chatSessionsCount,
                    AvgChatFirstResponseMinutes = avgChatFrt,
                    AvgChatDurationMinutes = avgChatDuration
                });
            }

            return Ok(result);
        }

        // ============================================================
        // = Helpers (giữ lại trong file controller)                   =
        // ============================================================

        private static DateOnly GetWeekStart(DateOnly date)
        {
            var diff = (int)date.DayOfWeek - (int)DayOfWeek.Monday;
            if (diff < 0) diff += 7;
            return date.AddDays(-diff);
        }

        private static bool TryParseYearMonth(string yearMonth, out DateOnly monthStart)
        {
            monthStart = default;

            if (string.IsNullOrWhiteSpace(yearMonth) ||
                yearMonth.Length != 7 ||
                yearMonth[4] != '-')
            {
                return false;
            }

            if (!int.TryParse(yearMonth[..4], out var year))
                return false;

            if (!int.TryParse(yearMonth.Substring(5, 2), out var month))
                return false;

            try
            {
                monthStart = new DateOnly(year, month, 1);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
