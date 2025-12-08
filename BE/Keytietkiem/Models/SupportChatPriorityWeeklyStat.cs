using System;
using System.Collections.Generic;

namespace Keytietkiem.Models;

public partial class SupportChatPriorityWeeklyStat
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
