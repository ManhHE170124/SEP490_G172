using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class SupportPlanMonthlyStat
{
    public string YearMonth { get; set; } = null!;

    public int SupportPlanId { get; set; }

    public int ActiveSubscriptionsCount { get; set; }

    public int NewSubscriptionsCount { get; set; }

    public decimal SupportPlanRevenue { get; set; }

    public int TicketsCount { get; set; }

    public int ChatSessionsCount { get; set; }
}
