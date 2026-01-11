// File: DTOs/SystemInsightsDashboardDtos.cs
using System;
using System.Collections.Generic;

namespace Keytietkiem.DTOs.SystemInsights
{
    public sealed class SystemInsightsOverviewResponse
    {
        public string Timezone { get; set; } = "Asia/Bangkok";
        public string Bucket { get; set; } = "day"; // day | hour

        // Local time (UTC+7) to display
        public DateTime FromLocal { get; set; }
        public DateTime ToLocal { get; set; } // exclusive

        public SystemActivityKpis SystemActivity { get; set; } = new();
        public NotificationsHealthKpis NotificationsHealth { get; set; } = new();

        // ===== Audit charts =====
        public List<TimePointDto> AuditActionsSeries { get; set; } = new(); // line
        public List<StackedTimePointDto> AuditActionsByRoleSeries { get; set; } = new(); // stacked
        public List<NameCountDto> TopAuditActions { get; set; } = new(); // bar top 10
        public List<NameCountDto> TopAuditEntityTypes { get; set; } = new(); // bar top 10
        public List<HeatmapCellDto> AuditHeatmap { get; set; } = new(); // 7x24 cells
        public List<NameCountDto> TopAuditIpAddresses { get; set; } = new(); // bar top 10

        // ===== Notification charts =====
        public List<NotificationsDailyDto> NotificationsDaily { get; set; } = new(); // line total + system/manual
        public List<NotificationsSeverityDailyDto> NotificationsSeverityDaily { get; set; } = new(); // stacked severity
        public NotificationScopeBreakdownDto NotificationScope { get; set; } = new(); // donut
        public List<NameCountDto> TopNotificationTypes { get; set; } = new(); // bar top 10
        public List<ReadRateDailyDto> NotificationReadRateDaily { get; set; } = new(); // line read rate
        public List<HistogramBucketDto> NotificationRecipientsHistogram { get; set; } = new(); // histogram
    }

    public sealed class SystemActivityKpis
    {
        public int TotalActions { get; set; }
        public int UniqueActors { get; set; }
        public int SystemActions { get; set; }
        public double SystemActionRate { get; set; } // 0..1
    }

    public sealed class NotificationsHealthKpis
    {
        public int TotalNotifications { get; set; }

        public int SystemGeneratedCount { get; set; }
        public int ManualCount { get; set; }
        public double SystemGeneratedRate { get; set; } // 0..1

        public int GlobalCount { get; set; }
        public int TargetedCount { get; set; }
        public double GlobalRate { get; set; } // 0..1

        public long TotalTargetUsersSum { get; set; }
        public long ReadCountSum { get; set; }
        public double OverallReadRate { get; set; } // 0..1 (ΣRead / ΣTarget)
    }

    public sealed class TimePointDto
    {
        // ISO-like local string to display, e.g. "2026-01-01" (day) or "2026-01-01T14:00:00" (hour)
        public string BucketStartLocal { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class StackedTimePointDto
    {
        public string BucketStartLocal { get; set; } = string.Empty;
        public Dictionary<string, int> RoleCounts { get; set; } = new();
    }

    public sealed class NameCountDto
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class HeatmapCellDto
    {
        // 0..6 (Mon..Sun) - FE render
        public int DayIndex { get; set; }
        // 0..23
        public int Hour { get; set; }
        public int Count { get; set; }
    }

    public sealed class NotificationsDailyDto
    {
        public string DateLocal { get; set; } = string.Empty; // "YYYY-MM-DD"
        public int Total { get; set; }
        public int System { get; set; }
        public int Manual { get; set; }
    }

    public sealed class NotificationsSeverityDailyDto
    {
        public string DateLocal { get; set; } = string.Empty;
        public int Info { get; set; }    // severity 0
        public int Success { get; set; } // severity 1
        public int Warning { get; set; } // severity 2
        public int Error { get; set; }   // severity 3
    }

    public sealed class NotificationScopeBreakdownDto
    {
        public int Global { get; set; }
        public int RoleTargeted { get; set; }
        public int UserTargeted { get; set; }
    }

    public sealed class ReadRateDailyDto
    {
        public string DateLocal { get; set; } = string.Empty;
        public long TotalTargetUsers { get; set; }
        public long ReadCount { get; set; }
        public double ReadRate { get; set; } // 0..1
    }

    public sealed class HistogramBucketDto
    {
        public string Label { get; set; } = string.Empty; // e.g. "0", "1-9", "10-49"...
        public int Count { get; set; }
    }
}
