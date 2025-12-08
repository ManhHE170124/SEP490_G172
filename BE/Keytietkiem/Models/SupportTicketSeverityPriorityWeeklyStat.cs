using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class SupportTicketSeverityPriorityWeeklyStat
{
    public DateOnly WeekStartDate { get; set; }

    public string Severity { get; set; } = null!;

    public int PriorityLevel { get; set; }

    public int TicketsCount { get; set; }

    public int ResponseSlaMetCount { get; set; }

    public int ResponseSlaTotalCount { get; set; }

    public int ResolutionSlaMetCount { get; set; }

    public int ResolutionSlaTotalCount { get; set; }

    public decimal? AvgFirstResponseMinutes { get; set; }

    public decimal? AvgResolutionMinutes { get; set; }
}
