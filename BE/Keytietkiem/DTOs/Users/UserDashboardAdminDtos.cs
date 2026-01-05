using System;
using System.Collections.Generic;

namespace Keytietkiem.DTOs.Users
{
    public class UserGrowthDashboardDto
    {
        public UserGrowthFilterEchoDto Filter { get; set; } = new();
        public UserGrowthKpisDto Kpis { get; set; } = new();
        public List<UserGrowthSeriesPointDto> Series { get; set; } = new();
    }

    public class UserGrowthFilterEchoDto
    {
        // yyyy-MM
        public string Month { get; set; } = "";

        // yyyy-MM-dd (as-of local date)
        public string AsOfDate { get; set; } = "";

        // day|week
        public string GroupBy { get; set; } = "day";

        public string TimeZone { get; set; } = "Asia/Bangkok";
    }

    public class UserGrowthKpisDto
    {
        // within selected month (from month start to as-of)
        public long NewUsersInMonth { get; set; }

        // total users as-of (cumulative) - isTemp=false
        public long TotalUsersAsOf { get; set; }

        // increase compared to end of previous month
        public long TotalUsersChangeVsPrevMonth { get; set; }
    }

    public class UserGrowthSeriesPointDto
    {
        // bucket date (day bucket or week-start date)
        public DateOnly BucketDate { get; set; }

        // new registered users (isTemp=false) inside bucket
        public int NewUsers { get; set; }
    }
}