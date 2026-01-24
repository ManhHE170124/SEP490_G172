// File: src/pages/admin/AdminPaymentsDashboardPage.jsx
import React, { useCallback, useEffect, useMemo, useState, useRef } from "react";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";

import {
  ResponsiveContainer,
  ComposedChart,
  BarChart,
  LineChart,
  PieChart,
  Pie,
  Cell,
  Bar,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  Brush,
} from "recharts";

import "./AdminPaymentsDashboardPage.css";
import { paymentsDashboardAdminApi } from "../../services/paymentsDashboardAdminApi";

// ===== helpers =====
const pick = (obj, ...keys) => {
  for (const k of keys) {
    if (obj && obj[k] !== undefined && obj[k] !== null) return obj[k];
  }
  return undefined;
};

const asNum = (v, d = 0) => {
  const n = Number(v);
  return Number.isFinite(n) ? n : d;
};

const clamp = (n, a, b) => Math.max(a, Math.min(b, n));

const toVnd = (n) => {
  const v = asNum(n, 0);
  return v.toLocaleString("vi-VN") + " đ";
};

const toPct = (n) => {
  if (n === null || n === undefined) return "—";
  const v = asNum(n, NaN);
  if (!Number.isFinite(v)) return "—";
  return `${v.toFixed(1)}%`;
};

const secToHuman = (sec) => {
  if (sec === null || sec === undefined) return "—";
  const s = asNum(sec, NaN);
  if (!Number.isFinite(s)) return "—";
  if (s < 60) return `${Math.round(s)} giây`;
  const m = s / 60;
  if (m < 60) return `${m.toFixed(1)} phút`;
  const h = m / 60;
  return `${h.toFixed(1)} giờ`;
};

const pad2 = (x) => String(x).padStart(2, "0");
const fmtDDMM = (d) => `${pad2(d.getDate())}/${pad2(d.getMonth() + 1)}`;
const fmtDDMMYYYY = (d) =>
  `${pad2(d.getDate())}/${pad2(d.getMonth() + 1)}/${d.getFullYear()}`;
const fmtMMYYYY = (d) => `${pad2(d.getMonth() + 1)}/${d.getFullYear()}`;

const startOfDayLocal = (d) =>
  new Date(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0, 0);
const addDaysLocal = (d, days) =>
  new Date(d.getFullYear(), d.getMonth(), d.getDate() + days, 0, 0, 0, 0);

const startOfWeekMonLocal = (d) => {
  const dd = startOfDayLocal(d);
  const jsDow = dd.getDay(); // 0=Sun..6=Sat
  const monFirst = jsDow === 0 ? 6 : jsDow - 1; // Monday=0..Sunday=6
  return addDaysLocal(dd, -monFirst);
};

const startOfMonthLocal = (d) =>
  new Date(d.getFullYear(), d.getMonth(), 1, 0, 0, 0, 0);
const startOfNextMonthLocal = (d) =>
  new Date(d.getFullYear(), d.getMonth() + 1, 1, 0, 0, 0, 0);

const addMonthsLocal = (d, m) => new Date(d.getFullYear(), d.getMonth() + m, 1, 0, 0, 0, 0);
const addYearsLocal = (d, y) => new Date(d.getFullYear() + y, 0, 1, 0, 0, 0, 0);

const startOfYearLocal = (y) => new Date(y, 0, 1, 0, 0, 0, 0);
const startOfNextYearLocal = (y) => new Date(y + 1, 0, 1, 0, 0, 0, 0);

const isoUtc = (localDate) => localDate.toISOString();
const getTzOffsetMinutesAdd = () => -new Date().getTimezoneOffset();

const chooseGrain = (days) => {
  if (days <= 45) return "day";
  if (days <= 180) return "week";
  if (days <= 730) return "month";
  return "quarter";
};

const quarterOf = (d) => Math.floor(d.getMonth() / 3) + 1;

const aggregateTrend = (points, grain) => {
  if (!Array.isArray(points) || points.length === 0) return [];

  if (grain === "day") {
    return points.map((p) => ({
      ...p,
      label: fmtDDMM(p.date),
      sortKey: p.date.getTime(),
      successRate: p.total > 0 ? (p.success / p.total) * 100 : null,
    }));
  }

  const map = new Map();

  const getBucketStart = (date) => {
    if (grain === "week") return startOfWeekMonLocal(date);
    if (grain === "month") return startOfMonthLocal(date);
    const y = date.getFullYear();
    const q = quarterOf(date);
    const startMonth = (q - 1) * 3;
    return new Date(y, startMonth, 1, 0, 0, 0, 0);
  };

  const getKey = (bucketStart) => {
    if (grain === "week")
      return `W:${bucketStart.getFullYear()}-${bucketStart.getMonth()}-${bucketStart.getDate()}`;
    if (grain === "month")
      return `M:${bucketStart.getFullYear()}-${bucketStart.getMonth()}`;
    const q = quarterOf(bucketStart);
    return `Q:${bucketStart.getFullYear()}-${q}`;
  };

  for (const p of points) {
    const bs = getBucketStart(p.date);
    const key = getKey(bs);
    if (!map.has(key)) {
      map.set(key, {
        bucketStart: bs,
        total: 0,
        success: 0,
        pending: 0,
        timeout: 0,
        cancelled: 0,
        failed: 0,
        other: 0,
        amount: 0,
      });
    }
    const a = map.get(key);
    a.total += p.total;
    a.success += p.success;
    a.pending += p.pending;
    a.timeout += p.timeout;
    a.cancelled += p.cancelled;
    a.failed += p.failed;
    a.other += p.other;
    a.amount += p.amount;
  }

  return Array.from(map.values())
    .sort((a, b) => a.bucketStart.getTime() - b.bucketStart.getTime())
    .map((a) => {
      let label = "";
      if (grain === "week") label = `Tuần ${fmtDDMM(a.bucketStart)}`;
      else if (grain === "month") label = fmtMMYYYY(a.bucketStart);
      else label = `Q${quarterOf(a.bucketStart)}/${a.bucketStart.getFullYear()}`;

      const successRate = a.total > 0 ? (a.success / a.total) * 100 : null;

      return {
        date: a.bucketStart,
        label,
        sortKey: a.bucketStart.getTime(),
        total: a.total,
        success: a.success,
        pending: a.pending,
        timeout: a.timeout,
        cancelled: a.cancelled,
        failed: a.failed,
        other: a.other,
        amount: a.amount,
        successRate,
      };
    });
};

const normalizeTrendPoint = (p) => {
  const dt = new Date(pick(p, "localDate", "LocalDate"));
  return {
    date: dt,
    total: asNum(pick(p, "totalCreated", "TotalCreated"), 0),
    success: asNum(pick(p, "successCount", "SuccessCount"), 0),
    pending: asNum(pick(p, "pendingCount", "PendingCount"), 0),
    timeout: asNum(pick(p, "timeoutCount", "TimeoutCount"), 0),
    cancelled: asNum(pick(p, "cancelledCount", "CancelledCount"), 0),
    failed: asNum(pick(p, "failedCount", "FailedCount"), 0),
    other: asNum(pick(p, "otherCount", "OtherCount"), 0),
    amount: asNum(pick(p, "amountCollected", "AmountCollected"), 0),
  };
};

// ✅ palette
const C = {
  success: "#16a34a",
  pending: "#2563eb",
  timeout: "#f59e0b",
  cancelled: "#6b7280",
  failed: "#ef4444",
  other: "#cbd5e1",
  rate: "#0ea5e9",
  amount: "#22c55e",
  neutralBar: "#111827",

  typeOrder: "#7c3aed",
  typeSupport: "#10b981",
  typeOther: "#94a3b8",
};

const CustomTooltip = ({ active, payload, label, valueFmt }) => {
  if (!active || !payload || payload.length === 0) return null;
  const title =
    label || payload?.[0]?.payload?.name || payload?.[0]?.name || "Chi tiết";
  return (
    <div className="apd-tooltip">
      <div className="apd-tooltip-title">{title}</div>
      <div className="apd-tooltip-body">
        {payload.map((x) => (
          <div key={x.dataKey ?? x.name} className="apd-tooltip-row">
            <span className="apd-tooltip-k">{x.name}:</span>
            <span className="apd-tooltip-v">
              {valueFmt ? valueFmt(x.value, x.dataKey) : x.value}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
};

const typeLabel = (tt) => {
  if (tt === "Order") return "Đơn hàng";
  if (tt === "SupportPlan") return "Gói hỗ trợ";
  return "Khác";
};

export default function AdminPaymentsDashboardPage() {
  const [provider, setProvider] = useState("PayOS");
  const [targetType, setTargetType] = useState("");
  const [pendingOverdueMinutes, setPendingOverdueMinutes] = useState(5);

  const [rangePreset, setRangePreset] = useState("last30"); // today | last7 | last30 | month | year | custom
  const [customMode, setCustomMode] = useState("day"); // day | month | year

  const [monthAnchor, setMonthAnchor] = useState(() => startOfMonthLocal(new Date()));
  const [yearAnchor, setYearAnchor] = useState(() => startOfYearLocal(new Date().getFullYear()));

  const [fromPick, setFromPick] = useState(startOfDayLocal(new Date()));
  const [toPick, setToPick] = useState(addDaysLocal(startOfDayLocal(new Date()), 1));

  const [effectiveFromUtc, setEffectiveFromUtc] = useState(null);
  const [effectiveToUtc, setEffectiveToUtc] = useState(null);

  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState("");

  const [summary, setSummary] = useState(null);
  const [dailyRaw, setDailyRaw] = useState([]);
  const [timeToPay, setTimeToPay] = useState(null);
  const [attempts, setAttempts] = useState(null);
  const [heatmap, setHeatmap] = useState(null);
  const [reasons, setReasons] = useState([]);

  const [typeSplit, setTypeSplit] = useState({ order: 0, support: 0 });

  const tzOffsetMinutesAdd = useMemo(() => getTzOffsetMinutesAdd(), []);
  const loadRef = useRef(false);

  const computeRequestedRangeLocal = useCallback(() => {
    const now = new Date();

    if (rangePreset === "today") {
      const from = startOfDayLocal(now);
      const to = addDaysLocal(from, 1);
      return { fromLocal: from, toLocal: to };
    }

    if (rangePreset === "last7") {
      const to = addDaysLocal(startOfDayLocal(now), 1);
      const from = addDaysLocal(to, -7);
      return { fromLocal: from, toLocal: to };
    }

    if (rangePreset === "last30") {
      const to = addDaysLocal(startOfDayLocal(now), 1);
      const from = addDaysLocal(to, -30);
      return { fromLocal: from, toLocal: to };
    }

    if (rangePreset === "month") {
      const from = startOfMonthLocal(monthAnchor);
      const to = addMonthsLocal(from, 1);
      return { fromLocal: from, toLocal: to };
    }

    if (rangePreset === "year") {
      const from = startOfYearLocal(yearAnchor.getFullYear());
      const to = addYearsLocal(from, 1);
      return { fromLocal: from, toLocal: to };
    }

    // custom
    let s = fromPick ? startOfDayLocal(fromPick) : startOfDayLocal(now);
    let e = toPick ? startOfDayLocal(toPick) : s;

    if (customMode === "day") {
      const from = startOfDayLocal(s);
      const to = addDaysLocal(startOfDayLocal(e), 1);
      if (to <= from) return { fromLocal: from, toLocal: addDaysLocal(from, 1) };
      return { fromLocal: from, toLocal: to };
    }

    if (customMode === "month") {
      const mf = startOfMonthLocal(fromPick || now);
      const mt = startOfMonthLocal(toPick || fromPick || now);
      const from = mf;
      const to = addMonthsLocal(mt, 1);
      if (to <= from) return { fromLocal: from, toLocal: addMonthsLocal(from, 1) };
      return { fromLocal: from, toLocal: to };
    }

    // year
    const yf = startOfYearLocal((fromPick || now).getFullYear());
    const yt = startOfYearLocal((toPick || fromPick || now).getFullYear());
    const from = yf;
    const to = addYearsLocal(yt, 1);
    if (to <= from) return { fromLocal: from, toLocal: addYearsLocal(from, 1) };
    return { fromLocal: from, toLocal: to };
  }, [rangePreset, monthAnchor, yearAnchor, customMode, fromPick, toPick]);

  const loadAll = useCallback(async () => {
    if (loadRef.current) return; // prevent concurrent loads
    loadRef.current = true;
    setLoading(true);
    setErr("");

    try {
      const { fromLocal, toLocal } = computeRequestedRangeLocal();

      const providerParam = provider === "ALL" ? "" : provider;
      const targetParam = targetType || "";

      const s = await paymentsDashboardAdminApi.summary({
        fromUtc: isoUtc(fromLocal),
        toUtc: isoUtc(toLocal),
        provider: providerParam || undefined,
        targetType: targetParam || undefined,
        pendingOverdueMinutes: clamp(asNum(pendingOverdueMinutes, 5), 1, 240),
      });

      setSummary(s);

      const effFrom = new Date(pick(s, "rangeFromUtc", "RangeFromUtc") || isoUtc(fromLocal));
      const effTo = new Date(pick(s, "rangeToUtc", "RangeToUtc") || isoUtc(toLocal));
      setEffectiveFromUtc(effFrom);
      setEffectiveToUtc(effTo);

      const days = clamp(
        Math.ceil((effTo.getTime() - effFrom.getTime()) / 86400000),
        1,
        3660
      );

      const [trend, ttp, att, hm, fr, sumOrder, sumSupport] = await Promise.all([
        paymentsDashboardAdminApi.dailyTrends({
          fromUtc: isoUtc(effFrom),
          toUtc: isoUtc(effTo),
          provider: providerParam || undefined,
          targetType: targetParam || undefined,
          timezoneOffsetMinutes: tzOffsetMinutesAdd,
        }),
        paymentsDashboardAdminApi.timeToPay({
          fromUtc: isoUtc(effFrom),
          toUtc: isoUtc(effTo),
          provider: providerParam || undefined,
          targetType: targetParam || undefined,
        }),
        paymentsDashboardAdminApi.attempts({
          days,
          provider: providerParam || undefined,
        }),
        paymentsDashboardAdminApi.heatmap({
          days,
          provider: providerParam || undefined,
          metric: "success",
          timezoneOffsetMinutes: tzOffsetMinutesAdd,
        }),
        paymentsDashboardAdminApi.failureReasons({
          days,
          provider: providerParam || undefined,
          targetType: targetParam || undefined,
          top: 10,
        }),

        // ✅ pie split: Order / SupportPlan
        paymentsDashboardAdminApi.summary({
          fromUtc: isoUtc(effFrom),
          toUtc: isoUtc(effTo),
          provider: providerParam || undefined,
          targetType: "Order",
          pendingOverdueMinutes: clamp(asNum(pendingOverdueMinutes, 5), 1, 240),
        }),
        paymentsDashboardAdminApi.summary({
          fromUtc: isoUtc(effFrom),
          toUtc: isoUtc(effTo),
          provider: providerParam || undefined,
          targetType: "SupportPlan",
          pendingOverdueMinutes: clamp(asNum(pendingOverdueMinutes, 5), 1, 240),
        }),
      ]);

      setDailyRaw(Array.isArray(trend) ? trend : []);
      setTimeToPay(ttp || null);
      setAttempts(att || null);
      setHeatmap(hm || null);
      setReasons(Array.isArray(fr) ? fr : []);

      setTypeSplit({
        order: asNum(pick(sumOrder, "totalPaymentsCreated", "TotalPaymentsCreated"), 0),
        support: asNum(pick(sumSupport, "totalPaymentsCreated", "TotalPaymentsCreated"), 0),
      });
    } catch (e) {
      setErr(e?.message || "Không thể tải dữ liệu dashboard thanh toán.");
    } finally {
      setLoading(false);
      loadRef.current = false;
    }
  }, [
    provider,
    targetType,
    pendingOverdueMinutes,
    tzOffsetMinutesAdd,
    computeRequestedRangeLocal,
  ]);

  useEffect(() => {
    loadAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Auto-apply when filters change (debounced)
  useEffect(() => {
    const id = setTimeout(() => {
      loadAll();
    }, 250);
    return () => clearTimeout(id);
  }, [rangePreset, customMode, fromPick, toPick, provider, targetType, pendingOverdueMinutes, loadAll]);

  const effectiveRangeText = useMemo(() => {
    if (!effectiveFromUtc || !effectiveToUtc) return "";
    const fromLocal = new Date(effectiveFromUtc);
    const toLocalExclusive = new Date(effectiveToUtc);
    const toLocalInclusive = new Date(toLocalExclusive.getTime() - 1);
    return `${fmtDDMMYYYY(fromLocal)} — ${fmtDDMMYYYY(toLocalInclusive)}`;
  }, [effectiveFromUtc, effectiveToUtc]);

  const rangeDays = useMemo(() => {
    if (!effectiveFromUtc || !effectiveToUtc) return 0;
    return Math.ceil((effectiveToUtc.getTime() - effectiveFromUtc.getTime()) / 86400000);
  }, [effectiveFromUtc, effectiveToUtc]);

  const grain = useMemo(() => chooseGrain(rangeDays || 1), [rangeDays]);

  const trendPoints = useMemo(() => {
    const raw = Array.isArray(dailyRaw) ? dailyRaw : [];
    const normalized = raw.map(normalizeTrendPoint).sort((a, b) => a.date - b.date);
    const agg = aggregateTrend(normalized, grain);

    const MAX_POINTS = 120;
    if (agg.length <= MAX_POINTS) return agg;

    const step = Math.ceil(agg.length / MAX_POINTS);
    return agg.filter((_, idx) => idx % step === 0);
  }, [dailyRaw, grain]);

  const histData = useMemo(() => {
    const arr = pick(timeToPay, "histogram", "Histogram");
    const list = Array.isArray(arr) ? arr : [];
    return list.map((x) => ({
      label: pick(x, "label", "Label") ?? "",
      count: asNum(pick(x, "count", "Count"), 0),
    }));
  }, [timeToPay]);

  const attemptData = useMemo(() => {
    const arr = pick(attempts, "attemptBuckets", "AttemptBuckets");
    const list = Array.isArray(arr) ? arr : [];
    return list.map((x) => ({
      label: pick(x, "label", "Label") ?? "",
      count: asNum(pick(x, "count", "Count"), 0),
    }));
  }, [attempts]);

  const reasonsData = useMemo(() => {
    const list = Array.isArray(reasons) ? reasons : [];
    return list.map((x) => ({
      reason: pick(x, "reason", "Reason") ?? "—",
      count: asNum(pick(x, "count", "Count"), 0),
      code: pick(x, "code", "Code") ?? "",
    }));
  }, [reasons]);

  const successRateValue = useMemo(() => {
    const r = pick(summary, "successRate", "SuccessRate");
    if (r === null || r === undefined) return null;
    return asNum(r, 0) * 100;
  }, [summary]);

  const timeoutRateValue = useMemo(() => {
    const r = pick(summary, "timeoutRate", "TimeoutRate");
    if (r === null || r === undefined) return null;
    return asNum(r, 0) * 100;
  }, [summary]);

  const cancelRateValue = useMemo(() => {
    const r = pick(summary, "cancelRate", "CancelRate");
    if (r === null || r === undefined) return null;
    return asNum(r, 0) * 100;
  }, [summary]);

  const alerts = useMemo(() => {
    const a = pick(summary, "alerts", "Alerts");
    return Array.isArray(a) ? a : [];
  }, [summary]);

  const onPreset = (p) => setRangePreset(p);
  const onApply = async () => loadAll();

  const resetFilters = useCallback(() => {
    const now = new Date();
    setRangePreset("today");
    setCustomMode("day");
    setFromPick(startOfDayLocal(now));
    setToPick(addDaysLocal(startOfDayLocal(now), 1));
    setProvider("PayOS");
    setTargetType("");
    setPendingOverdueMinutes(5);
    loadAll();
  }, [loadAll]);

  const fmtYAxisVnd = (v) => {
    const n = asNum(v, 0);
    if (n >= 1_000_000_000) return (n / 1_000_000_000).toFixed(1) + "B";
    if (n >= 1_000_000) return (n / 1_000_000).toFixed(1) + "M";
    if (n >= 1_000) return (n / 1_000).toFixed(1) + "K";
    return String(n);
  };

  const maxHeat = useMemo(() => {
    const rows = pick(heatmap, "rows", "Rows");
    if (!Array.isArray(rows)) return 0;
    let m = 0;
    for (const r of rows) {
      const arr = pick(r, "valuesByHour", "ValuesByHour") || [];
      for (const v of arr) m = Math.max(m, asNum(v, 0));
    }
    return m;
  }, [heatmap]);

  const typePieData = useMemo(() => {
    const totalFiltered = asNum(
      pick(summary, "totalPaymentsCreated", "TotalPaymentsCreated"),
      0
    );
    const order = asNum(typeSplit.order, 0);
    const support = asNum(typeSplit.support, 0);

    if (targetType === "Order") {
      return [{ name: typeLabel("Order"), value: totalFiltered || order }];
    }
    if (targetType === "SupportPlan") {
      return [{ name: typeLabel("SupportPlan"), value: totalFiltered || support }];
    }

    const other = Math.max(0, totalFiltered - order - support);

    const arr = [
      { name: typeLabel("Order"), value: order },
      { name: typeLabel("SupportPlan"), value: support },
    ];
    if (other > 0) arr.push({ name: typeLabel("Other"), value: other });

    return arr.filter((x) => x.value > 0);
  }, [summary, typeSplit, targetType]);

  const typePieTotal = useMemo(
    () => typePieData.reduce((s, x) => s + asNum(x.value, 0), 0),
    [typePieData]
  );

  const typePieColor = (name) => {
    if (name === typeLabel("Order")) return C.typeOrder;
    if (name === typeLabel("SupportPlan")) return C.typeSupport;
    return C.typeOther;
  };

  return (
    <div className="apd-page">
      <div className="apd-head">
        <div>
          <h2 className="apd-title">Dashboard thanh toán</h2>
          <div className="apd-sub">
            <span>• Kỳ lọc: </span>
            <span className="apd-subrange">{effectiveRangeText || "—"}</span>
          </div>
        </div>
        <div className="apd-actions">
          <button
            className="apd-btn apd-btn-ghost"
            onClick={resetFilters}
            disabled={loading}
            type="button"
          >
            Reset
          </button>
        </div>
      </div>

      {/* Filters */}
      <div className="apd-section">
        <div className="apd-filters">
          <div className="apd-filter apd-filter-wide">
            <div className="apd-label">Khoảng thời gian</div>

            <div className="apd-time-row">
              <div className="apd-seg apd-time-seg">
                <button
                  type="button"
                  className={"apd-segbtn " + (rangePreset === "today" ? "is-on" : "")}
                  onClick={() => onPreset("today")}
                >
                  Hôm nay
                </button>
                <button
                  type="button"
                  className={"apd-segbtn " + (rangePreset === "last7" ? "is-on" : "")}
                  onClick={() => onPreset("last7")}
                >
                  7 ngày
                </button>
                <button
                  type="button"
                  className={"apd-segbtn " + (rangePreset === "last30" ? "is-on" : "")}
                  onClick={() => onPreset("last30")}
                >
                  30 ngày
                </button>
                <button
                  type="button"
                  className={"apd-segbtn " + (rangePreset === "month" ? "is-on" : "")}
                  onClick={() => onPreset("month")}
                >
                  Theo tháng
                </button>
                <button
                  type="button"
                  className={"apd-segbtn " + (rangePreset === "year" ? "is-on" : "")}
                  onClick={() => onPreset("year")}
                >
                  Theo năm
                </button>
                
              </div>

              <div className="apd-compact">
                {rangePreset === "month" ? (
                  <div className="apd-compact-row">
                    <button
                      className="apd-navbtn"
                      onClick={() => setMonthAnchor(addMonthsLocal(startOfMonthLocal(monthAnchor), -1))}
                      title="Tháng trước"
                      type="button"
                    >
                      ‹
                    </button>
                    <DatePicker
                      selected={new Date(monthAnchor)}
                      onChange={(d) => d && setMonthAnchor(startOfMonthLocal(d))}
                      className="apd-input apd-mini"
                      dateFormat="MM/yyyy"
                      showMonthYearPicker
                    />
                    <button
                      className="apd-navbtn"
                      onClick={() => setMonthAnchor(addMonthsLocal(startOfMonthLocal(monthAnchor), 1))}
                      title="Tháng sau"
                      type="button"
                    >
                      ›
                    </button>
                  </div>
                ) : null}

                {rangePreset === "year" ? (
                  <div className="apd-compact-row">
                    <button
                      className="apd-navbtn"
                      onClick={() => setYearAnchor(addYearsLocal(startOfYearLocal(yearAnchor.getFullYear()), -1))}
                      title="Năm trước"
                      type="button"
                    >
                      ‹
                    </button>
                    <DatePicker
                      selected={new Date(yearAnchor)}
                      onChange={(d) => d && setYearAnchor(startOfYearLocal(d.getFullYear()))}
                      className="apd-input apd-mini"
                      dateFormat="yyyy"
                      showYearPicker
                    />
                    <button
                      className="apd-navbtn"
                      onClick={() => setYearAnchor(addYearsLocal(startOfYearLocal(yearAnchor.getFullYear()), 1))}
                      title="Năm sau"
                      type="button"
                    >
                      ›
                    </button>
                  </div>
                ) : null}
              </div>
            </div>
          </div>

          {/* bottom filters moved below to keep time controls separate */}
          {/* bottom filters: provider, targetType, pendingOverdueMinutes */}
          <div className="apd-bottom-filters">
            <div className="apd-filter">
              <div className="apd-label">Cổng thanh toán</div>
              <select
                className="apd-input"
                value={provider}
                onChange={(e) => setProvider(e.target.value)}
              >
                <option value="PayOS">PayOS</option>
                <option value="ALL">Tất cả</option>
              </select>
            </div>

            <div className="apd-filter">
              <div className="apd-label">Loại giao dịch</div>
              <select
                className="apd-input"
                value={targetType}
                onChange={(e) => setTargetType(e.target.value)}
              >
                <option value="">Tất cả</option>
                <option value="Order">Đơn hàng</option>
                <option value="SupportPlan">Gói hỗ trợ</option>
              </select>
            </div>

            <div className="apd-filter">
              <div className="apd-label">Chờ quá hạn (phút)</div>
              <input
                className="apd-input"
                type="number"
                value={pendingOverdueMinutes}
                min={1}
                max={240}
                onChange={(e) => setPendingOverdueMinutes(e.target.value)}
              />
            </div>
          </div>

        </div>

        {err && <div className="apd-error">{err}</div>}
      </div>

      {/* KPI */}
      <div className="apd-section">
        <div className="apd-kpis">
          <div className="apd-card">
            <div className="apd-card-label">Tổng giao dịch</div>
            <div className="apd-card-val">
              {asNum(pick(summary, "totalPaymentsCreated", "TotalPaymentsCreated"), 0)}
            </div>
            <div className="apd-card-foot">Theo kỳ lọc</div>
          </div>

          <div className="apd-card">
            <div className="apd-card-label">Thành công</div>
            <div className="apd-card-val">
              {asNum(pick(summary, "totalSuccessful", "TotalSuccessful"), 0)}
              <span className={"apd-badge apd-badge-ok"}>
                {successRateValue === null ? "—" : toPct(successRateValue)}
              </span>
            </div>
            
          </div>

          <div className="apd-card">
            <div className="apd-card-label">Tổng tiền thu</div>
            <div className="apd-card-val">
              {toVnd(pick(summary, "totalAmountCollected", "TotalAmountCollected"))}
            </div>
            
          </div>

          <div className="apd-card">
            <div className="apd-card-label">Hết hạn (timeout)</div>
            <div className="apd-card-val">
              {asNum(pick(summary, "timeoutCount", "TimeoutCount"), 0)}
              <span className={"apd-badge apd-badge-warn"}>
                {timeoutRateValue === null ? "—" : toPct(timeoutRateValue)}
              </span>
            </div>
            
          </div>

          <div className="apd-card">
            <div className="apd-card-label">Đã hủy</div>
            <div className="apd-card-val">
              {asNum(pick(summary, "cancelledCount", "CancelledCount"), 0)}
              <span className={"apd-badge apd-badge-mute"}>
                {cancelRateValue === null ? "—" : toPct(cancelRateValue)}
              </span>
            </div>
            
          </div>

          <div className="apd-card">
            <div className="apd-card-label">Chờ quá hạn</div>
            <div className="apd-card-val">
              {asNum(pick(summary, "pendingOverdueCount", "PendingOverdueCount"), 0)}
            </div>
            <div className="apd-card-foot">
              Quá{" "}
              {asNum(pick(summary, "pendingOverdueMinutes", "PendingOverdueMinutes"), 5)}{" "}
              phút
            </div>
          </div>

          <div className="apd-card">
            <div className="apd-card-label">Trung vị thời gian thanh toán</div>
            <div className="apd-card-val">
              {secToHuman(pick(summary, "medianTimeToPaySeconds", "MedianTimeToPaySeconds"))}
            </div>
            <div className="apd-card-foot">P50 thời gian thanh toán</div>
          </div>

          <div className="apd-card">
            <div className="apd-card-label">P95 thời gian thanh toán</div>
            <div className="apd-card-val">
              {secToHuman(pick(summary, "p95TimeToPaySeconds", "P95TimeToPaySeconds"))}
            </div>
            <div className="apd-card-foot">Nhóm chậm nhất (5%)</div>
          </div>
        </div>

        {!!alerts.length && (
          <div className="apd-alerts">
            {alerts.map((a, idx) => {
              const sev = (pick(a, "severity", "Severity") || "Info").toLowerCase();
              const cls =
                sev === "error"
                  ? "apd-alert-error"
                  : sev === "warning"
                  ? "apd-alert-warning"
                  : "apd-alert-info";
              return (
                <div key={idx} className={"apd-alert " + cls}>
                  <div className="apd-alert-code">{pick(a, "code", "Code")}</div>
                  <div className="apd-alert-msg">{pick(a, "message", "Message")}</div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Charts */}
      <div className="apd-section">
        <div className="apd-section-title">Biểu đồ & xu hướng</div>

        <div className="apd-grid-3">
          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Giao dịch theo trạng thái</div>
            </div>

            <div className="apd-chart apd-rechart">
              <ResponsiveContainer width="100%" height={260}>
                <ComposedChart
                  data={trendPoints}
                  margin={{ top: 8, right: 10, left: 0, bottom: 0 }}
                >
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis
                    dataKey="label"
                    minTickGap={16}
                    interval="preserveStartEnd"
                    tick={{ fontSize: 11 }}
                  />
                  <YAxis yAxisId="cnt" tick={{ fontSize: 11 }} />
                  <YAxis
                    yAxisId="pct"
                    orientation="right"
                    domain={[0, 100]}
                    tick={{ fontSize: 11 }}
                    tickFormatter={(v) => `${v}%`}
                  />
                  <Tooltip
                    content={
                      <CustomTooltip
                        valueFmt={(v, key) =>
                          key === "successRate"
                            ? `${asNum(v, 0).toFixed(1)}%`
                            : String(asNum(v, 0))
                        }
                      />
                    }
                  />
                  <Legend iconType="circle" />

                  <Bar
                    yAxisId="cnt"
                    dataKey="success"
                    name="Thành công"
                    stackId="st"
                    fill={C.success}
                    isAnimationActive={false}
                  />
                  <Bar
                    yAxisId="cnt"
                    dataKey="pending"
                    name="Đang chờ"
                    stackId="st"
                    fill={C.pending}
                    isAnimationActive={false}
                  />
                  <Bar
                    yAxisId="cnt"
                    dataKey="timeout"
                    name="Hết hạn"
                    stackId="st"
                    fill={C.timeout}
                    isAnimationActive={false}
                  />
                  <Bar
                    yAxisId="cnt"
                    dataKey="cancelled"
                    name="Đã hủy"
                    stackId="st"
                    fill={C.cancelled}
                    isAnimationActive={false}
                  />
                  <Bar
                    yAxisId="cnt"
                    dataKey="failed"
                    name="Lỗi"
                    stackId="st"
                    fill={C.failed}
                    isAnimationActive={false}
                  />
                  <Bar
                    yAxisId="cnt"
                    dataKey="other"
                    name="Khác"
                    stackId="st"
                    fill={C.other}
                    isAnimationActive={false}
                  />

                  <Line
                    yAxisId="pct"
                    type="monotone"
                    dataKey="successRate"
                    name="Tỷ lệ thành công (%)"
                    stroke={C.rate}
                    strokeWidth={2}
                    dot={false}
                    isAnimationActive={false}
                  />

                  <Brush dataKey="label" height={26} stroke="#94a3b8" travellerWidth={10} />
                </ComposedChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Xu hướng tỷ lệ thành công</div>
              <div className="apd-panel-sub">0–100%</div>
            </div>

            <div className="apd-metric-row">
              <div className="apd-big">
                {successRateValue === null ? "—" : toPct(successRateValue)}
              </div>
            </div>

            <div className="apd-chart apd-rechart">
              <ResponsiveContainer width="100%" height={180}>
                <LineChart
                  data={trendPoints}
                  margin={{ top: 12, right: 10, left: 0, bottom: 0 }}
                >
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis
                    dataKey="label"
                    minTickGap={18}
                    tick={{ fontSize: 11 }}
                    interval="preserveStartEnd"
                  />
                  <YAxis
                    domain={[0, 100]}
                    tick={{ fontSize: 11 }}
                    tickFormatter={(v) => `${v}%`}
                  />
                  <Tooltip
                    content={<CustomTooltip valueFmt={(v) => `${asNum(v, 0).toFixed(1)}%`} />}
                  />
                  <Legend iconType="circle" />
                  <Line
                    type="monotone"
                    dataKey="successRate"
                    name="Tỷ lệ thành công (%)"
                    stroke={C.rate}
                    strokeWidth={2}
                    dot={false}
                    isAnimationActive={false}
                  />
                  <Brush dataKey="label" height={20} stroke="#94a3b8" travellerWidth={10} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Xu hướng tiền thu</div>
              <div className="apd-panel-sub apd-unit">VND</div>
            </div>

            <div className="apd-metric-row">
              <div className="apd-big">
                {toVnd(pick(summary, "totalAmountCollected", "TotalAmountCollected"))}
              </div>
            </div>

            <div className="apd-chart apd-rechart">
              <ResponsiveContainer width="100%" height={180}>
                <LineChart
                  data={trendPoints}
                  margin={{ top: 12, right: 10, left: 0, bottom: 0 }}
                >
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis
                    dataKey="label"
                    minTickGap={18}
                    tick={{ fontSize: 11 }}
                    interval="preserveStartEnd"
                  />
                  <YAxis tick={{ fontSize: 11 }} tickFormatter={fmtYAxisVnd} />
                  <Tooltip content={<CustomTooltip valueFmt={(v) => toVnd(v)} />} />
                  <Legend iconType="circle" />
                  <Line
                    type="monotone"
                    dataKey="amount"
                    name="Tiền thu"
                    stroke={C.amount}
                    strokeWidth={2}
                    dot={false}
                    isAnimationActive={false}
                  />
                  <Brush dataKey="label" height={20} stroke="#94a3b8" travellerWidth={10} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>
        </div>
      </div>

      {/* Distribution & Attempts & Reasons */}
      <div className="apd-section">
        <div className="apd-grid-3">
          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Phân bố thời gian thanh toán</div>
              <div className="apd-panel-sub">
                p50: {secToHuman(pick(timeToPay, "p50Seconds", "P50Seconds"))} — p95:{" "}
                {secToHuman(pick(timeToPay, "p95Seconds", "P95Seconds"))}
              </div>
            </div>

            <div className="apd-chart apd-rechart">
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={histData} margin={{ top: 8, right: 10, left: 0, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip content={<CustomTooltip valueFmt={(v) => String(asNum(v, 0))} />} />
                  <Legend iconType="circle" />
                  <Bar
                    dataKey="count"
                    name="Số giao dịch"
                    fill={C.neutralBar}
                    isAnimationActive={false}
                  />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Số lần tạo link theo đối tượng</div>
              <div className="apd-panel-sub">Bắt multi-tab / spam</div>
            </div>

            <div className="apd-chart apd-rechart">
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={attemptData} margin={{ top: 8, right: 10, left: 0, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip content={<CustomTooltip valueFmt={(v) => String(asNum(v, 0))} />} />
                  <Legend iconType="circle" />
                  <Bar
                    dataKey="count"
                    name="Số đối tượng"
                    fill={C.pending}
                    isAnimationActive={false}
                  />
                </BarChart>
              </ResponsiveContainer>
            </div>

            <div className="apd-note">
              Đối tượng có ≥ 3 lần tạo link:{" "}
              <b>{asNum(pick(attempts, "targetsWithAttemptGte3", "TargetsWithAttemptGte3"), 0)}</b>
            </div>
          </div>

          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Top lý do thất bại</div>
            </div>

            <div className="apd-chart apd-rechart">
              <ResponsiveContainer width="100%" height={220}>
                <BarChart
                  layout="vertical"
                  data={reasonsData}
                  margin={{ top: 8, right: 10, left: 10, bottom: 0 }}
                >
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis type="number" tick={{ fontSize: 11 }} />
                  <YAxis type="category" dataKey="reason" tick={{ fontSize: 11 }} width={110} />
                  <Tooltip content={<CustomTooltip valueFmt={(v) => String(asNum(v, 0))} />} />
                  <Legend iconType="circle" />
                  <Bar dataKey="count" name="Số lượng" fill={C.failed} isAnimationActive={false} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>
        </div>
      </div>

      {/* Bottom row: Heatmap + Pie cùng hàng */}
      <div className="apd-section">
        <div className="apd-grid-2">
          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Giao dịch thành công theo giờ</div>
              <div className="apd-panel-sub">
              </div>
            </div>

            <div className="apd-heatmap">
              <div className="apd-heatmap-head">
                <div className="apd-heatmap-spacer" />
                {Array.from({ length: 24 }).map((_, h) => (
                  <div key={h} className="apd-heatmap-hour">
                    {h}
                  </div>
                ))}
              </div>

              {(() => {
                const rows = pick(heatmap, "rows", "Rows");
                const list = Array.isArray(rows) ? rows : [];
                const dayNames = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

                return list.map((r, idx) => {
                  const arr = pick(r, "valuesByHour", "ValuesByHour") || [];
                  const dIdx = asNum(pick(r, "dayIndexMonFirst", "DayIndexMonFirst"), idx);
                  return (
                    <div key={idx} className="apd-heatmap-row">
                      <div className="apd-heatmap-day">{dayNames[dIdx] || "—"}</div>
                      {Array.from({ length: 24 }).map((_, h) => {
                        const v = asNum(arr[h], 0);
                        const alpha = maxHeat > 0 ? 0.08 + (v / maxHeat) * 0.92 : 0.08;
                        return (
                          <div
                            key={h}
                            className="apd-heatmap-cell"
                            title={`${dayNames[dIdx]} ${h}:00 — ${v} giao dịch`}
                            style={{ opacity: alpha, background: C.success }}
                          />
                        );
                      })}
                    </div>
                  );
                });
              })()}
            </div>
          </div>

          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Tỷ lệ theo loại thanh toán</div>
              <div className="apd-panel-sub">Đơn hàng vs Gói hỗ trợ</div>
            </div>

            <div className="apd-chart apd-rechart">
              <ResponsiveContainer width="100%" height={260}>
                <PieChart>
                  <Tooltip content={<CustomTooltip valueFmt={(v) => String(asNum(v, 0))} />} />
                  <Legend iconType="circle" />
                  <Pie
                    data={typePieData}
                    dataKey="value"
                    nameKey="name"
                    innerRadius={58}
                    outerRadius={88}
                    paddingAngle={2}
                    isAnimationActive={false}
                    labelLine={false}
                    label={({ percent }) => `${Math.round((percent || 0) * 100)}%`}
                  >
                    {typePieData.map((e, i) => (
                      <Cell key={i} fill={typePieColor(e.name)} />
                    ))}
                  </Pie>
                </PieChart>
              </ResponsiveContainer>
            </div>

            <div className="apd-note">
              Tổng: <b>{typePieTotal}</b> giao dịch
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
