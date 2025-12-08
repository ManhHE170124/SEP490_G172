// File: DTOs/Support/SupportDashboardDtos.cs
using System;

namespace Keytietkiem.DTOs.Support
{
    // ===== 1. Overview =====
    public sealed class SupportOverviewDto
    {
        public int OpenTicketsNow { get; set; }
        public int NewTicketsLastNDays { get; set; }
        public int ClosedTicketsLastNDays { get; set; }
        public int NewChatSessionsLastNDays { get; set; }
        public decimal? TicketResponseSlaRateLastNDays { get; set; }
        public decimal? TicketResolutionSlaRateLastNDays { get; set; }
        public System.Collections.Generic.List<DailyTrendPointDto> DailyTrend { get; set; } = new();
        public System.Collections.Generic.List<WeeklyTicketChatPointDto> WeeklyTicketChat { get; set; } = new();
    }

    public sealed class DailyTrendPointDto
    {
        public DateOnly Date { get; set; }
        public int NewTickets { get; set; }
        public int ClosedTickets { get; set; }
        public int NewChatSessions { get; set; }
        public int OpenTicketsEndOfDay { get; set; }
    }

    public sealed class WeeklyTicketChatPointDto
    {
        public DateOnly WeekStartDate { get; set; }
        public int TicketCount { get; set; }
        public int ChatSessionCount { get; set; }
    }

    // ===== 2. Ticket KPIs =====

    public sealed class TicketDailyKpiDto
    {
        public DateOnly StatDate { get; set; }
        public int NewTicketsCount { get; set; }
        public int ClosedTicketsCount { get; set; }
        public decimal? AvgFirstResponseMinutes { get; set; }
        public decimal? AvgResolutionMinutes { get; set; }
        public decimal? AvgFirstResponseSlaRatio { get; set; }
        public decimal? AvgResolutionSlaRatio { get; set; }
        public int ResponseSlaMetCount { get; set; }
        public int ResponseSlaTotalCount { get; set; }
        public int ResolutionSlaMetCount { get; set; }
        public int ResolutionSlaTotalCount { get; set; }
    }

    public sealed class TicketSeverityPriorityWeeklyDto
    {
        public DateOnly WeekStartDate { get; set; }
        public string Severity { get; set; } = "";
        public int PriorityLevel { get; set; }
        public int TicketsCount { get; set; }
        public int ResponseSlaMetCount { get; set; }
        public int ResponseSlaTotalCount { get; set; }
        public int ResolutionSlaMetCount { get; set; }
        public int ResolutionSlaTotalCount { get; set; }
        public decimal? AvgFirstResponseMinutes { get; set; }
        public decimal? AvgResolutionMinutes { get; set; }
    }

    public sealed class TicketPriorityDistributionDto
    {
        public int PriorityLevel { get; set; }
        public int TicketCount { get; set; }
    }

    // ===== 3. Chat KPIs =====

    public sealed class ChatDailyKpiDto
    {
        public DateOnly StatDate { get; set; }
        public int NewChatSessionsCount { get; set; }
        public decimal? AvgFirstResponseMinutes { get; set; }
        public decimal? AvgDurationMinutes { get; set; }
        public decimal? AvgMessagesPerSession { get; set; }
    }

    public sealed class ChatPriorityWeeklyDto
    {
        public DateOnly WeekStartDate { get; set; }
        public int PriorityLevel { get; set; }
        public int SessionsCount { get; set; }
        public decimal? AvgFirstResponseMinutes { get; set; }
        public decimal? AvgDurationMinutes { get; set; }
        public int Duration05Count { get; set; }
        public int Duration510Count { get; set; }
        public int Duration1020Count { get; set; }
        public int Duration20plusCount { get; set; }
    }

    // ===== 4. Staff Performance =====

    public sealed class StaffPerformanceSummaryDto
    {
        public Guid StaffId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public int TicketsAssignedCount { get; set; }
        public int TicketsResolvedCount { get; set; }
        public decimal? AvgTicketFirstResponseMinutes { get; set; }
        public decimal? AvgTicketResolutionMinutes { get; set; }
        public decimal? TicketResponseSlaRate { get; set; }
        public decimal? TicketResolutionSlaRate { get; set; }
        public int ChatSessionsHandledCount { get; set; }
        public decimal? AvgChatFirstResponseMinutes { get; set; }
        public decimal? AvgChatDurationMinutes { get; set; }
        public int TicketStaffMessagesCount { get; set; }
        public int ChatStaffMessagesCount { get; set; }
    }

    // ===== 5. Support Plan / Segments =====

    public sealed class ActiveSupportPlanDistributionDto
    {
        public int SupportPlanId { get; set; }
        public string PlanName { get; set; } = "";
        public int PriorityLevel { get; set; }
        public decimal Price { get; set; }
        public int ActiveSubscriptionsCount { get; set; }
    }

    public sealed class SupportPlanMonthlyStatDto
    {
        public string YearMonth { get; set; } = "";
        public int SupportPlanId { get; set; }
        public string PlanName { get; set; } = "";
        public int PriorityLevel { get; set; }
        public decimal Price { get; set; }
        public int ActiveSubscriptionsCount { get; set; }
        public int NewSubscriptionsCount { get; set; }
        public decimal SupportPlanRevenue { get; set; }
        public int TicketsCount { get; set; }
        public int ChatSessionsCount { get; set; }
    }

    public sealed class PriorityDistributionDto
    {
        public int PriorityLevel { get; set; }
        public int UserCount { get; set; }
    }

    public sealed class PrioritySupportVolumeDto
    {
        public int PriorityLevel { get; set; }
        public int TicketCount { get; set; }
        public decimal? AvgTicketFirstResponseMinutes { get; set; }
        public decimal? AvgTicketResolutionMinutes { get; set; }
        public decimal? TicketResponseSlaRate { get; set; }
        public decimal? TicketResolutionSlaRate { get; set; }
        public int ChatSessionsCount { get; set; }
        public decimal? AvgChatFirstResponseMinutes { get; set; }
        public decimal? AvgChatDurationMinutes { get; set; }
    }
}
