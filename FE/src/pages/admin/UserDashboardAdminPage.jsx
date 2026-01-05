// File: src/pages/admin/UserDashboardAdminPage.jsx
import React, { useEffect, useMemo, useState } from "react";
import "../../styles/UserDashboardAdminPage.css";
import { userDashboardAdminApi } from "../../api/userDashboardAdminApi";

import {
  ResponsiveContainer,
  BarChart,
  Bar,
  LineChart,
  Line,
  CartesianGrid,
  XAxis,
  YAxis,
  Tooltip,
  Legend,
} from "recharts";

function pad2(n) {
  return `${n}`.padStart(2, "0");
}

function getCurrentMonthYyyyMm() {
  const d = new Date();
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}`;
}

function formatBucketLabel(bucketYmd, groupBy) {
  // bucketYmd: "YYYY-MM-DD"
  if (!bucketYmd) return "";
  const parts = String(bucketYmd).split("-");
  if (parts.length !== 3) return bucketYmd;
  const [, m, d] = parts;

  if (groupBy === "week") {
    // show week start dd/MM
    return `${d}/${m}`;
  }
  // day => show only day
  return `${d}`;
}

function pick(obj, camel, pascal, fallback) {
  if (!obj) return fallback;
  if (obj[camel] !== undefined) return obj[camel];
  if (obj[pascal] !== undefined) return obj[pascal];
  return fallback;
}

function KpiCard({ label, value, sub }) {
  return (
    <div className="udg-kpi">
      <div className="udg-kpi-label">{label}</div>
      <div className="udg-kpi-value">{value}</div>
      {sub ? <div className="udg-kpi-sub">{sub}</div> : null}
    </div>
  );
}

export default function UserDashboardAdminPage() {
  const [month, setMonth] = useState(getCurrentMonthYyyyMm());
  const [groupBy, setGroupBy] = useState("day"); // day|week
  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState("");
  const [data, setData] = useState(null);
  const [refreshTick, setRefreshTick] = useState(0);

  useEffect(() => {
    let mounted = true;

    async function run() {
      setLoading(true);
      setErr("");
      try {
        const res = await userDashboardAdminApi.getGrowthOverview({ month, groupBy });
        if (!mounted) return;
        setData(res || null);
      } catch (e) {
        console.error(e);
        if (!mounted) return;
        const msg =
          e?.response?.data?.message ||
          e?.response?.data?.title ||
          e?.message ||
          "Không thể tải dữ liệu dashboard.";
        setErr(msg);
      } finally {
        if (mounted) setLoading(false);
      }
    }

    run();
    return () => {
      mounted = false;
    };
  }, [month, groupBy, refreshTick]);

  const filter = pick(data, "filter", "Filter", null);
  const kpis = pick(data, "kpis", "Kpis", null);
  const series = pick(data, "series", "Series", []) || [];

  const newUsersInMonth = pick(kpis, "newUsersInMonth", "NewUsersInMonth", 0);
  const totalUsersAsOf = pick(kpis, "totalUsersAsOf", "TotalUsersAsOf", 0);
  const changeVsPrevMonth = pick(
    kpis,
    "totalUsersChangeVsPrevMonth",
    "TotalUsersChangeVsPrevMonth",
    0
  );

  const asOfDate = pick(filter, "asOfDate", "AsOfDate", "");
  const tz = pick(filter, "timeZone", "TimeZone", "Asia/Bangkok");

  const chartData = useMemo(() => {
    return series.map((p) => {
      const bucket = pick(p, "bucketDate", "BucketDate", "");
      const newUsers = pick(p, "newUsers", "NewUsers", 0);
      return {
        bucket,
        label: formatBucketLabel(bucket, groupBy),
        newUsers,
      };
    });
  }, [series, groupBy]);

  const changeText = useMemo(() => {
    const v = Number(changeVsPrevMonth || 0);
    const sign = v > 0 ? "+" : "";
    return `${sign}${v.toLocaleString()}`;
  }, [changeVsPrevMonth]);

  const titleRange = useMemo(() => {
    // month: YYYY-MM -> show as "MM/YYYY"
    const parts = String(month).split("-");
    if (parts.length === 2) {
      return `${parts[1]}/${parts[0]}`;
    }
    return month;
  }, [month]);

  return (
    <div className="udg-page">
      <div className="udg-head">
        <div>
          <h1 className="udg-title">User Growth Dashboard</h1>
          <div className="udg-sub">
            Month: <b>{titleRange}</b>
            {asOfDate ? (
              <>
                {" "}
                · As-of: <b>{asOfDate}</b>
              </>
            ) : null}
            {tz ? <span className="udg-subhint">({tz})</span> : null}
          </div>
        </div>

        <div className="udg-actions">
          <button className="udg-btn" onClick={() => setRefreshTick((x) => x + 1)} disabled={loading}>
            Refresh
          </button>
        </div>
      </div>

      {/* Filters */}
      <div className="udg-filters">
        <div className="udg-filter">
          <div className="udg-filter-label">Month</div>
          <input
            type="month"
            value={month}
            onChange={(e) => setMonth(e.target.value)}
            disabled={loading}
          />
        </div>

        <div className="udg-filter">
          <div className="udg-filter-label">Group by</div>
          <select value={groupBy} onChange={(e) => setGroupBy(e.target.value)} disabled={loading}>
            <option value="day">Day</option>
            <option value="week">Week</option>
          </select>
        </div>
      </div>

      {err ? <div className="udg-error">{err}</div> : null}

      {/* KPIs */}
      <div className="udg-kpis">
        <KpiCard
          label="New users (this month)"
          value={loading ? "…" : Number(newUsersInMonth || 0).toLocaleString()}
          sub="(isTemp = false)"
        />
        <KpiCard
          label="Total users (as-of)"
          value={loading ? "…" : Number(totalUsersAsOf || 0).toLocaleString()}
          sub="(isTemp = false)"
        />
        <KpiCard
          label="Change vs previous month"
          value={loading ? "…" : changeText}
          sub="(Total as-of - End of prev month)"
        />
      </div>

      {/* Chart */}
      <div className="udg-card">
        <div className="udg-card-title">
          New users trend ({groupBy === "week" ? "Weekly" : "Daily"})
        </div>

        {loading ? (
          <div className="udg-empty">Loading…</div>
        ) : chartData.length === 0 ? (
          <div className="udg-empty">No data.</div>
        ) : (
          <div className="udg-chart">
            {/* Bạn thích Bar hay Line thì dùng cái đó. Mình để cả 2, comment cái không dùng
            <ResponsiveContainer width="100%" height={320}>
              <BarChart data={chartData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" />
                <YAxis />
                <Tooltip />
                <Legend />
                <Bar dataKey="newUsers" name="New users" />
              </BarChart>
            </ResponsiveContainer> */}

            {/* Nếu muốn dạng line, uncomment khối này và comment BarChart ở trên */}
            
            <ResponsiveContainer width="100%" height={320}>
              <LineChart data={chartData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" />
                <YAxis />
                <Tooltip />
                <Legend />
                <Line type="monotone" dataKey="newUsers" name="New users" />
              </LineChart>
            </ResponsiveContainer>
           
          </div>
        )}
      </div>
    </div>
  );
}
