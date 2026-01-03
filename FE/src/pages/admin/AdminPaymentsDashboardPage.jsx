// File: src/pages/admin/AdminPaymentsDashboardPage.jsx
import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";

import {
  ResponsiveContainer,
  ComposedChart,
  BarChart,
  LineChart,
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

// ✅ palette (để chart có màu + legend/tooltip rõ)
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
};

const CustomTooltip = ({ active, payload, label, valueFmt }) => {
  if (!active || !payload || payload.length === 0) return null;
  return (
    <div className="apd-tooltip">
      <div className="apd-tooltip-title">{label}</div>
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

export default function AdminPaymentsDashboardPage() {
  const [provider, setProvider] = useState("PayOS");
  const [targetType, setTargetType] = useState("");
  const [pendingOverdueMinutes, setPendingOverdueMinutes] = useState(5);

  const [rangePreset, setRangePreset] = useState("today"); // today | week | month | custom
  const [customMode, setCustomMode] = useState("day"); // day | month | year

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

  const tzOffsetMinutesAdd = useMemo(() => getTzOffsetMinutesAdd(), []);

  const computeRequestedRangeLocal = useCallback(() => {
    const now = new Date();

    if (rangePreset === "today") {
      const f = startOfDayLocal(now);
      const t = addDaysLocal(f, 1);
      return { fromLocal: f, toLocal: t };
    }

    if (rangePreset === "week") {
      const f = startOfWeekMonLocal(now);
      const t = addDaysLocal(f, 7);
      return { fromLocal: f, toLocal: t };
    }

    if (rangePreset === "month") {
      const f = startOfMonthLocal(now);
      const t = startOfNextMonthLocal(now);
      return { fromLocal: f, toLocal: t };
    }

    let f = fromPick ? startOfDayLocal(fromPick) : startOfDayLocal(now);
    let t = toPick ? startOfDayLocal(toPick) : addDaysLocal(f, 1);

    if (customMode === "month") {
      const f2 = fromPick ? startOfMonthLocal(fromPick) : startOfMonthLocal(now);
      const t2 = toPick ? startOfNextMonthLocal(toPick) : startOfNextMonthLocal(f2);
      f = f2;
      t = t2;
    }

    if (customMode === "year") {
      const fy = (fromPick || now).getFullYear();
      const ty = (toPick || new Date(fy, 0, 1)).getFullYear();
      const yFrom = Math.min(fy, ty);
      const yTo = Math.max(fy, ty);
      f = startOfYearLocal(yFrom);
      t = startOfNextYearLocal(yTo);
    }

    if (t <= f) t = addDaysLocal(f, 1);
    return { fromLocal: f, toLocal: t };
  }, [rangePreset, fromPick, toPick, customMode]);

  const loadAll = useCallback(async () => {
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

      const [trend, ttp, att, hm, fr] = await Promise.all([
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
      ]);

      setDailyRaw(Array.isArray(trend) ? trend : []);
      setTimeToPay(ttp || null);
      setAttempts(att || null);
      setHeatmap(hm || null);
      setReasons(Array.isArray(fr) ? fr : []);
    } catch (e) {
      setErr(e?.message || "Không thể tải dữ liệu dashboard thanh toán.");
    } finally {
      setLoading(false);
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

  const onRefresh = async () => {
    await loadAll();
  };

  const onApplyCustom = async () => {
    setRangePreset("custom");
    await loadAll();
  };

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

  return (
    <div className="apd-page">
      <div className="apd-head">
        <div>
          <h2 className="apd-title">Dashboard thanh toán</h2>
          <div className="apd-sub">
            <span>• Kỳ lọc: </span>
            <span className="apd-subrange">{effectiveRangeText || "—"}</span>
            <span className="apd-subhint">
              {grain === "day"
                ? "(theo ngày)"
                : grain === "week"
                ? "(gộp theo tuần)"
                : grain === "month"
                ? "(gộp theo tháng)"
                : "(gộp theo quý)"}
            </span>
          </div>
        </div>

        <div className="apd-actions">
          <Link className="apd-link" to="/admin/payments">
            Danh sách giao dịch
          </Link>
          <button className="apd-btn" onClick={onRefresh} disabled={loading}>
            {loading ? "Đang tải..." : "Tải lại"}
          </button>
        </div>
      </div>

      {/* Filters */}
      <div className="apd-section">
        <div className="apd-filters">
          <div className="apd-filter apd-filter-wide">
            <div className="apd-label">Khoảng thời gian</div>
            <div className="apd-seg">
              <button
                className={"apd-segbtn " + (rangePreset === "today" ? "is-on" : "")}
                onClick={() => onPreset("today")}
              >
                Hôm nay
              </button>
              <button
                className={"apd-segbtn " + (rangePreset === "week" ? "is-on" : "")}
                onClick={() => onPreset("week")}
              >
                Tuần này
              </button>
              <button
                className={"apd-segbtn " + (rangePreset === "month" ? "is-on" : "")}
                onClick={() => onPreset("month")}
              >
                Tháng này
              </button>
              <button
                className={"apd-segbtn " + (rangePreset === "custom" ? "is-on" : "")}
                onClick={() => onPreset("custom")}
              >
                Tùy chọn
              </button>
            </div>

            {rangePreset === "custom" && (
              <div className="apd-range-extra">
                <div className="apd-range-item">
                  <div className="apd-label-mini">Kiểu lọc</div>
                  <div className="apd-seg">
                    <button
                      className={"apd-segbtn " + (customMode === "day" ? "is-on" : "")}
                      onClick={() => setCustomMode("day")}
                    >
                      Ngày
                    </button>
                    <button
                      className={"apd-segbtn " + (customMode === "month" ? "is-on" : "")}
                      onClick={() => setCustomMode("month")}
                    >
                      Tháng
                    </button>
                    <button
                      className={"apd-segbtn " + (customMode === "year" ? "is-on" : "")}
                      onClick={() => setCustomMode("year")}
                    >
                      Năm
                    </button>
                  </div>
                </div>

                <div className="apd-range-item">
                  <div className="apd-label-mini">Từ</div>
                  <DatePicker
                    className="apd-input apd-date"
                    selected={fromPick}
                    onChange={(d) => setFromPick(d || new Date())}
                    dateFormat={
                      customMode === "day"
                        ? "dd/MM/yyyy"
                        : customMode === "month"
                        ? "MM/yyyy"
                        : "yyyy"
                    }
                    showMonthYearPicker={customMode === "month"}
                    showYearPicker={customMode === "year"}
                  />
                </div>

                <div className="apd-range-item">
                  <div className="apd-label-mini">Đến</div>
                  <DatePicker
                    className="apd-input apd-date"
                    selected={toPick}
                    onChange={(d) => setToPick(d || new Date())}
                    dateFormat={
                      customMode === "day"
                        ? "dd/MM/yyyy"
                        : customMode === "month"
                        ? "MM/yyyy"
                        : "yyyy"
                    }
                    showMonthYearPicker={customMode === "month"}
                    showYearPicker={customMode === "year"}
                  />
                </div>

                <div className="apd-range-item">
                  <button className="apd-btn" onClick={onApplyCustom} disabled={loading}>
                    Áp dụng
                  </button>
                </div>
              </div>
            )}

            {rangePreset === "custom" && (
              <div className="apd-hint">
                Mẹo: lọc dài sẽ tự gộp theo tuần/tháng/quý để chart không bị kéo dài.
              </div>
            )}
          </div>

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
            <div className="apd-card-foot">Paid / Success / Completed</div>
          </div>

          <div className="apd-card">
            <div className="apd-card-label">Tổng tiền thu</div>
            <div className="apd-card-val">
              {toVnd(pick(summary, "totalAmountCollected", "TotalAmountCollected"))}
            </div>
            <div className="apd-card-foot">Tổng amount của giao dịch thành công</div>
          </div>

          <div className="apd-card">
            <div className="apd-card-label">Hết hạn (timeout)</div>
            <div className="apd-card-val">
              {asNum(pick(summary, "timeoutCount", "TimeoutCount"), 0)}
              <span className={"apd-badge apd-badge-warn"}>
                {timeoutRateValue === null ? "—" : toPct(timeoutRateValue)}
              </span>
            </div>
            <div className="apd-card-foot">Hết hạn thanh toán</div>
          </div>

          <div className="apd-card">
            <div className="apd-card-label">Đã hủy</div>
            <div className="apd-card-val">
              {asNum(pick(summary, "cancelledCount", "CancelledCount"), 0)}
              <span className={"apd-badge apd-badge-mute"}>
                {cancelRateValue === null ? "—" : toPct(cancelRateValue)}
              </span>
            </div>
            <div className="apd-card-foot">Người dùng hủy / hủy hệ thống</div>
          </div>

          <div className="apd-card">
            <div className="apd-card-label">Chờ quá hạn</div>
            <div className="apd-card-val">
              {asNum(pick(summary, "pendingOverdueCount", "PendingOverdueCount"), 0)}
            </div>
            <div className="apd-card-foot">
              Quá {asNum(pick(summary, "pendingOverdueMinutes", "PendingOverdueMinutes"), 5)} phút
            </div>
          </div>

          <div className="apd-card">
            <div className="apd-card-label">Trung vị thời gian thanh toán</div>
            <div className="apd-card-val">
              {secToHuman(pick(summary, "medianTimeToPaySeconds", "MedianTimeToPaySeconds"))}
            </div>
            <div className="apd-card-foot">P50 time-to-pay (từ AuditLogs Paid)</div>
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
          {/* BIG: Stacked + Success rate */}
          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Giao dịch theo trạng thái</div>
              <div className="apd-panel-sub">Zoom + tự gộp theo {grain}</div>
            </div>

            <div className="apd-chart apd-rechart">
              <ResponsiveContainer width="100%" height={260}>
                <ComposedChart data={trendPoints} margin={{ top: 8, right: 10, left: 0, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="label" minTickGap={16} interval="preserveStartEnd" tick={{ fontSize: 11 }} />
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
                        valueFmt={(v, key) => (key === "successRate" ? `${asNum(v, 0).toFixed(1)}%` : String(asNum(v, 0)))}
                      />
                    }
                  />
                  <Legend iconType="circle" />

                  <Bar yAxisId="cnt" dataKey="success" name="Thành công" stackId="st" fill={C.success} isAnimationActive={false} />
                  <Bar yAxisId="cnt" dataKey="pending" name="Đang chờ" stackId="st" fill={C.pending} isAnimationActive={false} />
                  <Bar yAxisId="cnt" dataKey="timeout" name="Hết hạn" stackId="st" fill={C.timeout} isAnimationActive={false} />
                  <Bar yAxisId="cnt" dataKey="cancelled" name="Đã hủy" stackId="st" fill={C.cancelled} isAnimationActive={false} />
                  <Bar yAxisId="cnt" dataKey="failed" name="Lỗi" stackId="st" fill={C.failed} isAnimationActive={false} />
                  <Bar yAxisId="cnt" dataKey="other" name="Khác" stackId="st" fill={C.other} isAnimationActive={false} />

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

          {/* Small: success rate trend */}
          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Xu hướng tỷ lệ thành công</div>
              <div className="apd-panel-sub">0–100%</div>
            </div>

            <div className="apd-metric-row">
              <div className="apd-big">{successRateValue === null ? "—" : toPct(successRateValue)}</div>
            </div>

            <div className="apd-chart apd-rechart">
              <ResponsiveContainer width="100%" height={180}>
                <LineChart data={trendPoints} margin={{ top: 12, right: 10, left: 0, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="label" minTickGap={18} tick={{ fontSize: 11 }} interval="preserveStartEnd" />
                  <YAxis domain={[0, 100]} tick={{ fontSize: 11 }} tickFormatter={(v) => `${v}%`} />
                  <Tooltip content={<CustomTooltip valueFmt={(v) => `${asNum(v, 0).toFixed(1)}%`} />} />
                  <Legend iconType="circle" />
                  <Line type="monotone" dataKey="successRate" name="Tỷ lệ thành công (%)" stroke={C.rate} strokeWidth={2} dot={false} isAnimationActive={false} />
                  <Brush dataKey="label" height={20} stroke="#94a3b8" travellerWidth={10} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>

          {/* Small: amount trend */}
          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Xu hướng tiền thu</div>
              <div className="apd-panel-sub apd-unit">VND</div>
            </div>

            <div className="apd-metric-row">
              <div className="apd-big">{toVnd(pick(summary, "totalAmountCollected", "TotalAmountCollected"))}</div>
            </div>

            <div className="apd-chart apd-rechart">
              <ResponsiveContainer width="100%" height={180}>
                <LineChart data={trendPoints} margin={{ top: 12, right: 10, left: 0, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="label" minTickGap={18} tick={{ fontSize: 11 }} interval="preserveStartEnd" />
                  <YAxis tick={{ fontSize: 11 }} tickFormatter={fmtYAxisVnd} />
                  <Tooltip content={<CustomTooltip valueFmt={(v) => toVnd(v)} />} />
                  <Legend iconType="circle" />
                  <Line type="monotone" dataKey="amount" name="Tiền thu" stroke={C.amount} strokeWidth={2} dot={false} isAnimationActive={false} />
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
                <BarChart
                  data={Array.isArray(pick(timeToPay, "histogram", "Histogram")) ? pick(timeToPay, "histogram", "Histogram") : []}
                  margin={{ top: 8, right: 10, left: 0, bottom: 0 }}
                >
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip content={<CustomTooltip valueFmt={(v) => String(asNum(v, 0))} />} />
                  <Legend iconType="circle" />
                  <Bar dataKey="count" name="Số giao dịch" fill={C.neutralBar} isAnimationActive={false} />
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
                <BarChart
                  data={Array.isArray(pick(attempts, "attemptBuckets", "AttemptBuckets")) ? pick(attempts, "attemptBuckets", "AttemptBuckets") : []}
                  margin={{ top: 8, right: 10, left: 0, bottom: 0 }}
                >
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip content={<CustomTooltip valueFmt={(v) => String(asNum(v, 0))} />} />
                  <Legend iconType="circle" />
                  <Bar dataKey="count" name="Số đối tượng" fill={C.pending} isAnimationActive={false} />
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
              <div className="apd-panel-sub">Hiển thị thuần Việt</div>
            </div>

            <div className="apd-chart apd-rechart">
              <ResponsiveContainer width="100%" height={220}>
                <BarChart
                  layout="vertical"
                  data={Array.isArray(reasons) ? reasons : []}
                  margin={{ top: 8, right: 10, left: 10, bottom: 0 }}
                >
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis type="number" tick={{ fontSize: 11 }} />
                  <YAxis
                    type="category"
                    dataKey={(x) => pick(x, "reason", "Reason")}
                    tick={{ fontSize: 11 }}
                    width={110}
                  />
                  <Tooltip content={<CustomTooltip valueFmt={(v) => String(asNum(v, 0))} />} />
                  <Legend iconType="circle" />
                  <Bar dataKey={(x) => pick(x, "count", "Count")} name="Số lượng" fill={C.failed} isAnimationActive={false} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>
        </div>
      </div>

      {/* Heatmap */}
      <div className="apd-section">
        <div className="apd-grid-1">
          <div className="apd-panel apd-panel-full">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Giao dịch thành công theo giờ</div>
              <div className="apd-panel-sub">Theo múi giờ trình duyệt (UTC+{Math.round(tzOffsetMinutesAdd / 60)})</div>
            </div>

            <div className="apd-heatmap">
              <div className="apd-heatmap-head">
                <div className="apd-heatmap-spacer" />
                {Array.from({ length: 24 }).map((_, h) => (
                  <div key={h} className="apd-heatmap-hour">{h}</div>
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
        </div>
      </div>
    </div>
  );
}
