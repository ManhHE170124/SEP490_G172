using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class SupportDailyStat
{
    public DateOnly StatDate { get; set; }

    public int NewTicketsCount { get; set; }

    public int ClosedTicketsCount { get; set; }

    public int OpenTicketsCountEndOfDay { get; set; }

    public int NewChatSessionsCount { get; set; }

    public decimal? AvgTicketFirstResponseMinutes { get; set; }

    public decimal? AvgTicketResolutionMinutes { get; set; }

    public decimal? AvgTicketFirstResponseSlaRatio { get; set; }

    public decimal? AvgTicketResolutionSlaRatio { get; set; }

    public int TicketResponseSlaMetCount { get; set; }

    public int TicketResponseSlaTotalCount { get; set; }

    public int TicketResolutionSlaMetCount { get; set; }

    public int TicketResolutionSlaTotalCount { get; set; }

    public decimal? AvgChatFirstResponseMinutes { get; set; }

    public decimal? AvgChatDurationMinutes { get; set; }

    public decimal? AvgChatMessagesPerSession { get; set; }
}
