using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class SupportStaffDailyStat
{
    public DateOnly StatDate { get; set; }

    public Guid StaffId { get; set; }

    public int TicketsResolvedCount { get; set; }

    public int TicketsAssignedCount { get; set; }

    public decimal? AvgTicketFirstResponseMinutes { get; set; }

    public decimal? AvgTicketResolutionMinutes { get; set; }

    public int TicketResponseSlaMetCount { get; set; }

    public int TicketResponseSlaTotalCount { get; set; }

    public int TicketResolutionSlaMetCount { get; set; }

    public int TicketResolutionSlaTotalCount { get; set; }

    public int ChatSessionsHandledCount { get; set; }

    public decimal? AvgChatFirstResponseMinutes { get; set; }

    public decimal? AvgChatDurationMinutes { get; set; }

    public int TicketStaffMessagesCount { get; set; }

    public int ChatStaffMessagesCount { get; set; }
}
