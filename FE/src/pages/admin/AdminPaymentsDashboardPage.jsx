// File: src/pages/admin/AdminPaymentsDashboardPage.jsx
import React, { useEffect, useMemo, useState, useCallback } from "react";
import { Link } from "react-router-dom";
import { paymentsDashboardAdminApi } from "../../services/paymentsDashboardAdminApi";
import "./AdminPaymentsDashboardPage.css";

const nfInt = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 0 });
const nfMoney = new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" });

const fmtInt = (n) => nfInt.format(Number(n || 0));
const fmtMoney = (n) => nfMoney.format(Number(n || 0));

const fmtPct = (v) => {
  if (v === null || v === undefined || Number.isNaN(Number(v))) return "—";
  return `${(Number(v) * 100).toFixed(1)}%`;
};

const fmtDurationVi = (sec) => {
  if (sec === null || sec === undefined) return "—";
  const s = Math.max(0, Math.floor(Number(sec)));
  if (s < 60) return `${s} giây`;
  const m = Math.floor(s / 60);
  const r = s % 60;
  if (m < 60) return `${m} phút ${String(r).padStart(2, "0")} giây`;
  const h = Math.floor(m / 60);
  const mm = m % 60;
  return `${h} giờ ${mm} phút`;
};

const toIsoUtc = (d) => new Date(d).toISOString();

const startOfLocalDay = (d) => {
  const x = new Date(d);
  x.setHours(0, 0, 0, 0);
  return x;
};

const endExclusiveOfLocalDay = (d) => {
  const x = startOfLocalDay(d);
  x.setDate(x.getDate() + 1);
  return x;
};

const startOfLocalMonth = (d) => {
  const x = new Date(d);
  x.setDate(1);
  x.setHours(0, 0, 0, 0);
  return x;
};

const endExclusiveOfLocalMonth = (d) => {
  const x = startOfLocalMonth(d);
  x.setMonth(x.getMonth() + 1);
  return x;
};

const startOfLocalYear = (year) => {
  const x = new Date(year, 0, 1);
  x.setHours(0, 0, 0, 0);
  return x;
};

const endExclusiveOfLocalYear = (year) => {
  const x = new Date(year + 1, 0, 1);
  x.setHours(0, 0, 0, 0);
  return x;
};

const fmtDateShortVi = (isoOrDate, mode = "day") => {
  try {
    const d = new Date(isoOrDate);
    if (mode === "month") return d.toLocaleDateString("vi-VN", { month: "2-digit", year: "numeric" }); // MM/YYYY
    return d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" }); // dd/MM
  } catch {
    return String(isoOrDate || "").slice(5, 10);
  }
};

function MiniLine({
  data = [],
  valueKey = "value",
  xKey = "date",
  height = 62,
  fixedMin = null,
  fixedMax = null,
  axisMode = "day", // "day"|"month"
  axisPoints = 3, // 2 or 3 labels
}) {
  const w = 240;
  const h = height;

  const pts = useMemo(() => {
    return (Array.isArray(data) ? data : []).map((x, i) => ({
      x: i,
      y: Number(x?.[valueKey] ?? 0),
      rawX: x?.[xKey],
    }));
  }, [data, valueKey, xKey]);

  const { minY, maxY } = useMemo(() => {
    if (!pts.length) return { minY: 0, maxY: 1 };

    // ✅ “Đỡ dối”: với success rate cố định 0..1 để chart không phóng đại dao động
    if (fixedMin !== null && fixedMax !== null) return { minY: fixedMin, maxY: fixedMax };

    let min = pts[0].y;
    let max = pts[0].y;
    for (const p of pts) {
      if (p.y < min) min = p.y;
      if (p.y > max) max = p.y;
    }

    // ✅ amount: ép min >= 0
    if (fixedMin !== null && fixedMax === null) {
      min = fixedMin;
    }

    if (min === max) max = min + 1;
    return { minY: min, maxY: max };
  }, [pts, fixedMin, fixedMax]);

  const pathD = useMemo(() => {
    if (!pts.length) return "";
    const pad = 6;
    const xScale = (i) => pad + (i * (w - pad * 2)) / Math.max(1, pts.length - 1);
    const yScale = (v) => {
      const t = (v - minY) / (maxY - minY);
      return h - pad - t * (h - pad * 2);
    };

    return pts
      .map((p, i) => `${i === 0 ? "M" : "L"} ${xScale(p.x).toFixed(2)} ${yScale(p.y).toFixed(2)}`)
      .join(" ");
  }, [pts, minY, maxY, h]);

  const axisLabels = useMemo(() => {
    if (!pts.length) return { a: "—", b: "", c: "—" };
    const a = pts[0].rawX;
    const c = pts[pts.length - 1].rawX;
    const b = pts[Math.floor((pts.length - 1) / 2)]?.rawX;

    const f = (v) => fmtDateShortVi(v, axisMode);
    return {
      a: f(a),
      b: axisPoints >= 3 ? f(b) : "",
      c: f(c),
    };
  }, [pts, axisMode, axisPoints]);

  return (
    <div className="apd-sparkwrap">
      <svg className="apd-spark" viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" aria-hidden="true">
        <path d={pathD} fill="none" stroke="currentColor" strokeWidth="2" />
      </svg>
      <div className={`apd-axis ${axisPoints >= 3 ? "is-3" : "is-2"}`}>
        <span className="apd-axis-l">{axisLabels.a}</span>
        {axisPoints >= 3 ? <span className="apd-axis-m">{axisLabels.b}</span> : null}
        <span className="apd-axis-r">{axisLabels.c}</span>
      </div>
    </div>
  );
}

function StackedBars({ points = [] }) {
  const maxTotal = useMemo(() => {
    let m = 1;
    for (const p of points) m = Math.max(m, Number(p.totalCreated || 0));
    return m;
  }, [points]);

  const stepLabel = useMemo(() => {
    const n = points.length || 0;
    if (n <= 20) return 1;
    if (n <= 45) return 2;
    if (n <= 90) return 5;
    return 7; // range lớn thì giảm nhãn để đỡ “loạn”
  }, [points.length]);

  return (
    <div className="apd-stacked">
      {points.map((p, idx) => {
        const total = Number(p.totalCreated || 0);
        const hPct = (total / maxTotal) * 100;

        const success = Number(p.successCount || 0);
        const pending = Number(p.pendingCount || 0);
        const timeout = Number(p.timeoutCount || 0);
        const cancelled = Number(p.cancelledCount || 0);
        const failed = Number(p.failedCount || 0);
        const other = Math.max(0, total - success - pending - timeout - cancelled - failed);

        const seg = (v) => (total > 0 ? (v / total) * 100 : 0);

        const showLabel = idx === 0 || idx === points.length - 1 || idx % stepLabel === 0;

        return (
          <div key={String(p.localDate)} className="apd-bar">
            <div
              className="apd-bar-col"
              style={{ height: `${hPct}%` }}
              title={`${fmtDateShortVi(p.localDate)}: ${total} giao dịch`}
            >
              <div className="apd-stackseg apd-seg-success" style={{ height: `${seg(success)}%` }} />
              <div className="apd-stackseg apd-seg-pending" style={{ height: `${seg(pending)}%` }} />
              <div className="apd-stackseg apd-seg-timeout" style={{ height: `${seg(timeout)}%` }} />
              <div className="apd-stackseg apd-seg-cancel" style={{ height: `${seg(cancelled)}%` }} />
              <div className="apd-stackseg apd-seg-failed" style={{ height: `${seg(failed)}%` }} />
              <div className="apd-stackseg apd-seg-other" style={{ height: `${seg(other)}%` }} />
            </div>
            <div className={`apd-bar-label ${showLabel ? "" : "is-hide"}`}>{showLabel ? fmtDateShortVi(p.localDate) : ""}</div>
          </div>
        );
      })}
    </div>
  );
}

function Histogram({ buckets = [] }) {
  const max = useMemo(() => Math.max(1, ...buckets.map((b) => Number(b.count || 0))), [buckets]);

  return (
    <div className="apd-hist">
      {buckets.map((b) => {
        const h = (Number(b.count || 0) / max) * 100;
        return (
          <div key={b.label} className="apd-hist-bar" title={`${b.label}: ${b.count || 0}`}>
            <div className="apd-hist-fill" style={{ height: `${h}%` }} />
            <div className="apd-hist-label">{b.label}</div>
          </div>
        );
      })}
    </div>
  );
}

function Heatmap({ rows = [] }) {
  const flat = useMemo(() => {
    const vals = [];
    for (const r of rows) for (const v of r.valuesByHour || []) vals.push(Number(v || 0));
    return vals;
  }, [rows]);

  const max = useMemo(() => Math.max(1, ...flat), [flat]);

  const dayNamesVi = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

  return (
    <div className="apd-heatmap">
      <div className="apd-heatmap-head">
        <div className="apd-heatmap-spacer" />
        {Array.from({ length: 24 }).map((_, h) => (
          <div key={h} className="apd-heatmap-hour">
            {h}
          </div>
        ))}
      </div>

      {rows.map((r) => (
        <div key={r.dayIndexMonFirst} className="apd-heatmap-row">
          <div className="apd-heatmap-day">{dayNamesVi[r.dayIndexMonFirst] || r.dayIndexMonFirst}</div>
          {(r.valuesByHour || []).map((v, h) => {
            const n = Number(v || 0);
            const a = n / max; // 0..1

            const bg = n === 0 ? "#e5e7eb" : "#111827";
            const op = n === 0 ? 1 : 0.15 + 0.85 * a;

            return (
              <div
                key={h}
                className="apd-heatmap-cell"
                style={{ background: bg, opacity: op }}
                title={`${dayNamesVi[r.dayIndexMonFirst]} ${h}:00 = ${n}`}
              />
            );
          })}
        </div>
      ))}
    </div>
  );
}

export default function AdminPaymentsDashboardPage() {
  // ✅ chuẩn: day | month | year | custom
  const [period, setPeriod] = useState("month");
  const now = useMemo(() => new Date(), []);
  const [monthPick, setMonthPick] = useState(() => {
    const y = now.getFullYear();
    const m = String(now.getMonth() + 1).padStart(2, "0");
    return `${y}-${m}`;
  });
  const [yearPick, setYearPick] = useState(() => now.getFullYear());
  const [customFrom, setCustomFrom] = useState("");
  const [customTo, setCustomTo] = useState("");

  const [provider, setProvider] = useState("PayOS");
  const [targetType, setTargetType] = useState(""); // "" all, Order, SupportPlan
  const [pendingOverdueMinutes, setPendingOverdueMinutes] = useState(5);

  const timezoneOffsetMinutes = useMemo(() => {
    const off = -new Date().getTimezoneOffset(); // minutes east of UTC
    return Number.isFinite(off) ? off : 420;
  }, []);

  const range = useMemo(() => {
    const now2 = new Date();

    if (period === "day") {
      return { from: startOfLocalDay(now2), to: new Date(now2) };
    }

    if (period === "month") {
      // monthPick: YYYY-MM
      const [yy, mm] = String(monthPick || "").split("-").map((x) => Number(x));
      const base = yy && mm ? new Date(yy, mm - 1, 1) : now2;
      const from = startOfLocalMonth(base);

      const isCurrent = yy === now2.getFullYear() && (mm - 1) === now2.getMonth();
      const to = isCurrent ? new Date(now2) : endExclusiveOfLocalMonth(base);

      return { from, to };
    }

    if (period === "year") {
      const y = Number(yearPick || now2.getFullYear());
      const from = startOfLocalYear(y);
      const isCurrent = y === now2.getFullYear();
      const to = isCurrent ? new Date(now2) : endExclusiveOfLocalYear(y);
      return { from, to };
    }

    // custom
    if (!customFrom || !customTo) {
      // fallback: tháng hiện tại
      const from = startOfLocalMonth(now2);
      return { from, to: new Date(now2) };
    }
    const from = startOfLocalDay(new Date(customFrom));
    const to = endExclusiveOfLocalDay(new Date(customTo)); // exclusive end
    return { from, to };
  }, [period, monthPick, yearPick, customFrom, customTo]);

  const days = useMemo(() => {
    const ms = range.to - range.from;
    const d = Math.max(1, Math.ceil(ms / (24 * 3600 * 1000)));
    // FE cho phép tới 366; nếu BE cap 180 thì BE tự cắt
    return Math.min(d, 366);
  }, [range]);

  const axisMode = useMemo(() => (days > 90 ? "month" : "day"), [days]);

  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState("");

  const [summary, setSummary] = useState(null);
  const [daily, setDaily] = useState([]);
  const [ttp, setTtp] = useState(null);
  const [attempts, setAttempts] = useState(null);
  const [heat, setHeat] = useState(null);
  const [reasons, setReasons] = useState([]);

  const fetchAll = useCallback(async () => {
    setLoading(true);
    setErr("");

    const fromUtc = toIsoUtc(range.from);
    const toUtc = toIsoUtc(range.to);

    try {
      const [s, d, t, a, h, r] = await Promise.all([
        paymentsDashboardAdminApi.summary({
          fromUtc,
          toUtc,
          provider,
          targetType: targetType || undefined,
          pendingOverdueMinutes,
        }),
        paymentsDashboardAdminApi.dailyTrends({
          days,
          provider,
          targetType: targetType || undefined,
          timezoneOffsetMinutes,
        }),
        paymentsDashboardAdminApi.timeToPay({
          fromUtc,
          toUtc,
          provider,
          targetType: targetType || undefined,
        }),
        paymentsDashboardAdminApi.attempts({ days, provider }),
        paymentsDashboardAdminApi.heatmap({
          days,
          provider,
          metric: "success",
          timezoneOffsetMinutes,
        }),
        paymentsDashboardAdminApi.failureReasons({
          days,
          provider,
          targetType: targetType || undefined,
          top: 10,
        }),
      ]);

      setSummary(s || null);
      setDaily(Array.isArray(d) ? d : []);
      setTtp(t || null);
      setAttempts(a || null);
      setHeat(h || null);
      setReasons(Array.isArray(r) ? r : []);
    } catch (e) {
      setErr(e?.response?.data?.message || e?.message || "Không tải được dashboard thanh toán.");
    } finally {
      setLoading(false);
    }
  }, [range, provider, targetType, pendingOverdueMinutes, days, timezoneOffsetMinutes]);

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  const successRateSeries = useMemo(
    () =>
      daily.map((x) => ({
        date: x.localDate,
        value: Number(x.successRate ?? 0),
      })),
    [daily]
  );

  const amountSeries = useMemo(
    () =>
      daily.map((x) => ({
        date: x.localDate,
        value: Number(x.amountCollected ?? 0),
      })),
    [daily]
  );

  const attemptBuckets = attempts?.attemptBuckets || attempts?.AttemptBuckets || [];
  const histogram = ttp?.histogram || ttp?.Histogram || [];
  const alerts = summary?.alerts || summary?.Alerts || [];

  const rangeLabel = useMemo(() => {
    const from = range?.from ? range.from.toLocaleDateString("vi-VN") : "";
    const to = range?.to ? new Date(range.to.getTime() - 1).toLocaleDateString("vi-VN") : ""; // inclusive display
    return from && to ? `${from} → ${to}` : "";
  }, [range]);

  const failedCount = summary?.failedCount ?? summary?.FailedCount ?? 0;

  const yearOptions = useMemo(() => {
    const y = new Date().getFullYear();
    return [y - 3, y - 2, y - 1, y, y + 1];
  }, []);

  return (
    <div className="apd-page">
      <div className="apd-head">
        <div>
          <h1 className="apd-title">Dashboard thanh toán</h1>
          <div className="apd-sub">{rangeLabel ? <span className="apd-subrange">• Kỳ lọc: {rangeLabel}</span> : null}</div>
        </div>

        <div className="apd-actions">
          <Link className="apd-link" to="/admin/payments">
            Danh sách giao dịch
          </Link>
          <button className="apd-btn" onClick={fetchAll} disabled={loading}>
            {loading ? "Đang tải..." : "Tải lại"}
          </button>
        </div>
      </div>

      <div className="apd-filters">
        <div className="apd-filter apd-filter-wide">
          <div className="apd-label">Thời gian</div>

          <div className="apd-seg">
            <button className={`apd-segbtn ${period === "day" ? "is-on" : ""}`} onClick={() => setPeriod("day")}>
              Ngày
            </button>
            <button className={`apd-segbtn ${period === "month" ? "is-on" : ""}`} onClick={() => setPeriod("month")}>
              Tháng
            </button>
            <button className={`apd-segbtn ${period === "year" ? "is-on" : ""}`} onClick={() => setPeriod("year")}>
              Năm
            </button>
            <button className={`apd-segbtn ${period === "custom" ? "is-on" : ""}`} onClick={() => setPeriod("custom")}>
              Tuỳ chọn
            </button>
          </div>

          {period === "month" && (
            <div className="apd-range-extra">
              <div className="apd-range-item">
                <div className="apd-label apd-label-mini">Chọn tháng</div>
                <input className="apd-input" type="month" value={monthPick} onChange={(e) => setMonthPick(e.target.value)} />
              </div>
            </div>
          )}

          {period === "year" && (
            <div className="apd-range-extra">
              <div className="apd-range-item">
                <div className="apd-label apd-label-mini">Chọn năm</div>
                <select className="apd-input" value={yearPick} onChange={(e) => setYearPick(Number(e.target.value))}>
                  {yearOptions.map((y) => (
                    <option key={y} value={y}>
                      {y}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          )}

          {period === "custom" && (
            <div className="apd-custom">
              <div>
                <div className="apd-label apd-label-mini">Từ ngày</div>
                <input className="apd-input" type="date" value={customFrom} onChange={(e) => setCustomFrom(e.target.value)} />
              </div>
              <div>
                <div className="apd-label apd-label-mini">Đến ngày</div>
                <input className="apd-input" type="date" value={customTo} onChange={(e) => setCustomTo(e.target.value)} />
              </div>
            </div>
          )}
        </div>

        <div className="apd-filter">
          <div className="apd-label">Cổng thanh toán</div>
          <select className="apd-input" value={provider} onChange={(e) => setProvider(e.target.value)}>
            <option value="PayOS">PayOS</option>
          </select>
        </div>

        <div className="apd-filter">
          <div className="apd-label">Loại giao dịch</div>
          <select className="apd-input" value={targetType} onChange={(e) => setTargetType(e.target.value)}>
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
            min={1}
            max={180}
            value={pendingOverdueMinutes}
            onChange={(e) => setPendingOverdueMinutes(Number(e.target.value || 5))}
          />
        </div>
      </div>

      {err ? <div className="apd-error">{err}</div> : null}

      {/* KPI Cards */}
      <div className="apd-kpis">
        <div className="apd-card">
          <div className="apd-card-label">Tổng giao dịch</div>
          <div className="apd-card-val">{fmtInt(summary?.totalPaymentsCreated ?? summary?.TotalPaymentsCreated)}</div>
        </div>

        <div className="apd-card">
          <div className="apd-card-label">Thành công</div>
          <div className="apd-card-val">
            {fmtInt(summary?.totalSuccessful ?? summary?.TotalSuccessful)}{" "}
            <span className="apd-badge apd-badge-ok">{fmtPct(summary?.successRate ?? summary?.SuccessRate)}</span>
          </div>
        </div>

        <div className="apd-card">
          <div className="apd-card-label">Tổng tiền thu</div>
          <div className="apd-card-val">{fmtMoney(summary?.totalAmountCollected ?? summary?.TotalAmountCollected)}</div>
        </div>

        <div className="apd-card">
          <div className="apd-card-label">Hết hạn (timeout)</div>
          <div className="apd-card-val">
            {fmtInt(summary?.timeoutCount ?? summary?.TimeoutCount)}{" "}
            <span className="apd-badge apd-badge-warn">{fmtPct(summary?.timeoutRate ?? summary?.TimeoutRate)}</span>
          </div>
        </div>

        <div className="apd-card">
          <div className="apd-card-label">Đã huỷ</div>
          <div className="apd-card-val">
            {fmtInt(summary?.cancelledCount ?? summary?.CancelledCount)}{" "}
            <span className="apd-badge apd-badge-mute">{fmtPct(summary?.cancelRate ?? summary?.CancelRate)}</span>
          </div>
          <div className="apd-card-foot">Thất bại: {fmtInt(failedCount)}</div>
        </div>

        <div className="apd-card">
          <div className="apd-card-label">Chờ quá hạn</div>
          <div className="apd-card-val">{fmtInt(summary?.pendingOverdueCount ?? summary?.PendingOverdueCount)}</div>
          <div className="apd-card-foot">Lớn hơn {pendingOverdueMinutes} phút</div>
        </div>

        <div className="apd-card">
          <div className="apd-card-label">Trung vị thời gian thanh toán</div>
          <div className="apd-card-val">{fmtDurationVi(summary?.medianTimeToPaySeconds ?? summary?.MedianTimeToPaySeconds)}</div>
        </div>

        <div className="apd-card">
          <div className="apd-card-label">P95 thời gian thanh toán</div>
          <div className="apd-card-val">{fmtDurationVi(summary?.p95TimeToPaySeconds ?? summary?.P95TimeToPaySeconds)}</div>
          <div className="apd-card-foot">Nhóm giao dịch chậm nhất (5%)</div>
        </div>
      </div>

      {/* Alerts */}
      {alerts.length > 0 && (
        <div className="apd-alerts">
          {alerts.map((a, idx) => (
            <div key={idx} className={`apd-alert apd-alert-${String(a.severity || a.Severity || "Info").toLowerCase()}`}>
              <div className="apd-alert-code">{a.code || a.Code}</div>
              <div className="apd-alert-msg">{a.message || a.Message}</div>
            </div>
          ))}
        </div>
      )}

      {/* ✅ Layout mới: 2 cột + các panel “full width” để tránh tràn */}
      <div className="apd-grid">
        {/* 1) Full width: stacked by status */}
        <div className="apd-panel apd-panel-wide">
          <div className="apd-panel-head">
            <div className="apd-panel-title">Giao dịch theo trạng thái (theo ngày)</div>
            <div className="apd-panel-sub">Cuộn ngang khi kỳ lọc dài</div>
          </div>

          <StackedBars points={daily} />

          <div className="apd-legend">
            <span className="apd-dot apd-dot-success" /> Thành công
            <span className="apd-dot apd-dot-pending" /> Đang chờ
            <span className="apd-dot apd-dot-timeout" /> Hết hạn
            <span className="apd-dot apd-dot-cancel" /> Huỷ
            <span className="apd-dot apd-dot-failed" /> Lỗi
            <span className="apd-dot apd-dot-other" /> Khác
          </div>
        </div>

        {/* 2) success rate trend */}
        <div className="apd-panel">
          <div className="apd-panel-head">
            <div className="apd-panel-title">Xu hướng tỷ lệ thành công</div>
            <div className="apd-panel-sub">0 → 100% (có mốc thời gian)</div>
          </div>

          <div className="apd-metric-row">
            <div className="apd-big">{fmtPct(summary?.successRate ?? summary?.SuccessRate)}</div>
            <MiniLine data={successRateSeries} valueKey="value" xKey="date" height={68} fixedMin={0} fixedMax={1} axisMode={axisMode} />
          </div>

          <div className="apd-note">Mốc thời gian giúp đánh giá xu hướng theo kỳ lọc.</div>
        </div>

        {/* 3) amount trend */}
        <div className="apd-panel">
          <div className="apd-panel-head">
            <div className="apd-panel-title">Xu hướng tiền thu theo ngày</div>
            <div className="apd-panel-sub">VND (có mốc thời gian)</div>
          </div>

          <div className="apd-metric-row">
            <div className="apd-big">{fmtMoney(summary?.totalAmountCollected ?? summary?.TotalAmountCollected)}</div>
            <MiniLine data={amountSeries} valueKey="value" xKey="date" height={68} fixedMin={0} axisMode={axisMode} />
          </div>

          <div className="apd-note">Tổng tiền từ giao dịch thành công theo ngày.</div>
        </div>

        {/* 4) histogram left */}
        <div className="apd-panel">
          <div className="apd-panel-head">
            <div className="apd-panel-title">Phân bố thời gian thanh toán</div>
            <div className="apd-panel-sub">
              p50 {fmtDurationVi(ttp?.p50Seconds ?? ttp?.P50Seconds)} • p95 {fmtDurationVi(ttp?.p95Seconds ?? ttp?.P95Seconds)}
            </div>
          </div>
          {histogram.length === 0 ? <div className="apd-empty">Chưa có dữ liệu thời gian thanh toán.</div> : <Histogram buckets={histogram} />}
        </div>

        {/* 5) right stack (attempts + reasons) */}
        <div className="apd-colstack">
          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Số lần tạo link theo đối tượng</div>
              <div className="apd-panel-sub">Bắt multi-tab / spam</div>
            </div>

            <div className="apd-attempts">
              {attemptBuckets.map((b) => (
                <div key={b.label || b.Label} className="apd-attempt">
                  <div className="apd-attempt-k">{(b.label || b.Label) === "4+" ? "4 lần trở lên" : `${b.label || b.Label} lần`}</div>
                  <div className="apd-attempt-v">{fmtInt(b.count ?? b.Count)}</div>
                </div>
              ))}
            </div>

            <div className="apd-note">
              Đối tượng có ≥ 3 lần tạo link: <b>{fmtInt(attempts?.targetsWithAttemptGte3 ?? attempts?.TargetsWithAttemptGte3)}</b>
            </div>
          </div>

          <div className="apd-panel">
            <div className="apd-panel-head">
              <div className="apd-panel-title">Top lý do thất bại</div>
            </div>

            <div className="apd-reasons">
              {reasons.length === 0 ? (
                <div className="apd-empty">Chưa có dữ liệu lý do.</div>
              ) : (
                reasons.map((x, idx) => (
                  <div key={idx} className="apd-reason">
                    <div className="apd-reason-name">{x.reason || x.Reason}</div>
                    <div className="apd-reason-count">{fmtInt(x.count ?? x.Count)}</div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>

        {/* 6) heatmap full width */}
        <div className="apd-panel apd-panel-wide">
          <div className="apd-panel-head">
            <div className="apd-panel-title">Giao dịch thành công theo giờ</div>
            <div className="apd-panel-sub">
              Theo múi giờ trình duyệt (UTC{timezoneOffsetMinutes >= 0 ? "+" : ""}
              {Math.round(timezoneOffsetMinutes / 60)})
            </div>
          </div>
          <Heatmap rows={heat?.rows || heat?.Rows || []} />
        </div>
      </div>
    </div>
  );
}
