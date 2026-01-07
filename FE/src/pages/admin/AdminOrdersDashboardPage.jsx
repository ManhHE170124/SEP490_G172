// File: src/pages/admin/AdminOrdersDashboardPage.jsx
import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";

import {
  ResponsiveContainer,
  ComposedChart,
  CartesianGrid,
  XAxis,
  YAxis,
  Tooltip,
  Legend,
  Line,
  Bar,
  Brush,
  PieChart,
  Pie,
  Cell,
} from "recharts";

import OrdersDashboardAdminApi from "../../services/ordersDashboardAdminApi";
import "./AdminOrdersDashboardPage.css";

/** ===== helpers ===== */
const pad2 = (n) => String(n).padStart(2, "0");
const fmtInt = (n) => new Intl.NumberFormat("vi-VN").format(Number(n || 0));
const fmtVnd = (n) =>
  new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(Number(n || 0));

/** UTC boundary helpers (tránh lệch timezone khi BE dùng UTC) */
const utcStartOfDay = (d) => new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0, 0));
const utcStartOfMonth = (d) => new Date(Date.UTC(d.getFullYear(), d.getMonth(), 1, 0, 0, 0, 0));
const utcStartOfYear = (d) => new Date(Date.UTC(d.getFullYear(), 0, 1, 0, 0, 0, 0));

const addDaysUtc = (d, days) => new Date(d.getTime() + days * 86400000);
const addMonthsUtc = (d, m) => new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + m, 1, 0, 0, 0, 0));
const addYearsUtc = (d, y) => new Date(Date.UTC(d.getUTCFullYear() + y, 0, 1, 0, 0, 0, 0));

const viOrderStatus = (s) => {
  const x = String(s || "").trim();
  switch (x) {
    case "Paid":
      return "Đã thanh toán";
    case "PendingPayment":
      return "Chờ thanh toán";
    case "Cancelled":
      return "Đã hủy";
    case "CancelledByTimeout":
      return "Hết hạn";
    case "NeedsManualAction":
      return "Cần xử lý";
    default:
      return x ? `Khác (${x})` : "Khác";
  }
};

const bucketLabel = (bucket, startUtcIso) => {
  const d = new Date(startUtcIso);
  const dd = pad2(d.getUTCDate());
  const mm = pad2(d.getUTCMonth() + 1);
  const yyyy = d.getUTCFullYear();

  if (bucket === "day") return `${dd}/${mm}`;
  if (bucket === "week") return `${dd}/${mm}`;
  if (bucket === "month") return `${mm}/${yyyy}`;
  if (bucket === "quarter") {
    const q = Math.floor(d.getUTCMonth() / 3) + 1;
    return `Q${q}/${yyyy}`;
  }
  if (bucket === "year") return `${yyyy}`;
  return `${dd}/${mm}`;
};

const OrdersTooltip = ({ active, payload, label }) => {
  if (!active || !payload || !payload.length) return null;

  const row = payload?.[0]?.payload || {};
  const start = row?.startUtc ? new Date(row.startUtc) : null;
  const end = row?.endUtc ? new Date(row.endUtc) : null;

  const title =
    start && end
      ? `${pad2(start.getUTCDate())}/${pad2(start.getUTCMonth() + 1)}/${start.getUTCFullYear()} → ${pad2(
          end.getUTCDate()
        )}/${pad2(end.getUTCMonth() + 1)}/${end.getUTCFullYear()}`
      : label;

  return (
    <div className="aod-tooltip">
      <div className="aod-tooltip-title">{title}</div>
      <div className="aod-tooltip-body">
        <div className="aod-tooltip-row">
          <span className="aod-tooltip-k">Đơn hàng</span>
          <span className="aod-tooltip-v">{fmtInt(row.orders)}</span>
        </div>
        <div className="aod-tooltip-row">
          <span className="aod-tooltip-k">Đã thanh toán</span>
          <span className="aod-tooltip-v">{fmtInt(row.paidOrders)}</span>
        </div>
        <div className="aod-tooltip-row">
          <span className="aod-tooltip-k">Doanh thu</span>
          <span className="aod-tooltip-v">{fmtVnd(row.revenue)}</span>
        </div>
      </div>
    </div>
  );
};

/** ===== Top table (Top 5, sort by Quantity/Orders/Revenue) ===== */
const sortIcon = (isOn, dir) => {
  if (!isOn) return "↕";
  return dir === "asc" ? "▲" : "▼";
};

function TopTable({ kind, items, limit = 5 }) {
  const list = Array.isArray(items) ? items : [];
  const [sort, setSort] = useState({ key: "quantitySold", dir: "desc" }); // default: SL desc
  const [expanded, setExpanded] = useState(false);

  const onSort = useCallback((key) => {
    setSort((prev) => {
      if (prev.key === key) return { key, dir: prev.dir === "asc" ? "desc" : "asc" };
      return { key, dir: "desc" };
    });
  }, []);

  const sorted = useMemo(() => {
    const key = sort.key;
    const dir = sort.dir;
    const arr = list.slice();

    arr.sort((a, b) => {
      const av = Number(a?.[key] || 0);
      const bv = Number(b?.[key] || 0);
      if (av === bv) return 0;
      return dir === "asc" ? av - bv : bv - av;
    });

    return arr;
  }, [list, sort]);

  if (!sorted.length) return <div className="aod-empty">Chưa có dữ liệu</div>;

  const isVariant = kind === "variant";

  return (
    <div className="aod-tablewrap">
      <table className="aod-table">
        {/* ✅ ép độ rộng cột để fit panel (không bị “mất cột”) */}
        <colgroup>
          {/* Further reduce first columns so revenue column can show */}
          <col style={{ width: '4%' }} />
          <col style={{ width: '26%' }} />
          <col style={{ width: '10%' }} />
          <col style={{ width: '10%' }} />
          <col style={{ width: '50%' }} />
        </colgroup>


        <thead>
          <tr>
            <th className="aod-th-rank">#</th>

            <th>
              <div className="aod-thbtn aod-thbtn-left" style={{ justifyContent: "flex-start" }}>
                {isVariant ? "Biến thể" : "Sản phẩm"}
              </div>
            </th>

            <th className="aod-th-num">
              <button title="Số lượng" className="aod-thbtn" onClick={() => onSort("quantitySold")} type="button">
                <span>SL</span>
                <span className={`aod-sort ${sort.key === "quantitySold" ? "is-on" : ""}`}>
                  {sortIcon(sort.key === "quantitySold", sort.dir)}
                </span>
              </button>
            </th>

            <th className="aod-th-num">
              <button className="aod-thbtn" onClick={() => onSort("ordersCount")} type="button">
                <span>Đơn</span>
                <span className={`aod-sort ${sort.key === "ordersCount" ? "is-on" : ""}`}>
                  {sortIcon(sort.key === "ordersCount", sort.dir)}
                </span>
              </button>
            </th>

            <th className="aod-th-num">
              <button className="aod-thbtn" onClick={() => onSort("revenue")} type="button">
                <span>Doanh thu</span>
                <span className={`aod-sort ${sort.key === "revenue" ? "is-on" : ""}`}>
                  {sortIcon(sort.key === "revenue", sort.dir)}
                </span>
              </button>
            </th>
          </tr>
        </thead>

        <tbody>
          {(expanded ? sorted : sorted.slice(0, limit)).map((x, idx) => {
            const main = isVariant ? x.variantTitle : x.productName;
            const sub = null; // ✅ theo yêu cầu: biến thể không show sub

            return (
              <tr key={idx}>
                <td className="aod-td-rank">{idx + 1}</td>

                <td className="aod-namecell">
                  <div className="aod-namecell-main" title={main}>
                    {main || "—"}
                  </div>
                  {sub ? (
                    <div className="aod-namecell-sub" title={sub}>
                      {sub}
                    </div>
                  ) : null}
                </td>

                <td className="aod-td-num">{fmtInt(x.quantitySold)}</td>
                <td className="aod-td-num">{fmtInt(x.ordersCount)}</td>
                <td className="aod-td-num">{fmtVnd(x.revenue)}</td>
              </tr>
            );
          })}
        </tbody>
        {sorted.length > limit ? (
          <tfoot>
            <tr>
              <td colSpan={5} style={{ padding: 8, textAlign: "center" }}>
                <button
                  className="aod-btn aod-btn-ghost"
                  type="button"
                  onClick={() => setExpanded((v) => !v)}
                >
                  {expanded ? `Thu gọn` : `Xem thêm ${sorted.length - limit} mục`}
                </button>
              </td>
            </tr>
          </tfoot>
        ) : null}
      </table>
    </div>
  );
}

export default function AdminOrdersDashboardPage() {
  /**
   * Preset ranges (ít input nhất):
   * - today | last7 | last30 | month | year | custom
   */
  const [preset, setPreset] = useState("last30");

  // month/year anchors (1 picker + prev/next)
  const [monthAnchor, setMonthAnchor] = useState(() => utcStartOfMonth(new Date()));
  const [yearAnchor, setYearAnchor] = useState(() => utcStartOfYear(new Date()));

  // custom mode + values
  const [customMode, setCustomMode] = useState("day"); // day|month|year

  // custom day uses 1 range picker (1 input)
  const [customDayRange, setCustomDayRange] = useState(() => {
    const to = addDaysUtc(utcStartOfDay(new Date()), 1);
    const from = addDaysUtc(to, -7);
    return [from, addDaysUtc(to, -1)]; // store end as "date" (not exclusive) for UI
  });

  // custom month/year uses 2 pickers (tối giản theo khả năng)
  const [customMonthFrom, setCustomMonthFrom] = useState(() => utcStartOfMonth(new Date()));
  const [customMonthTo, setCustomMonthTo] = useState(() => utcStartOfMonth(new Date()));
  const [customYearFrom, setCustomYearFrom] = useState(() => utcStartOfYear(new Date()));
  const [customYearTo, setCustomYearTo] = useState(() => utcStartOfYear(new Date()));

  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState("");
  const [dash, setDash] = useState(null);

  const bucketUsed = dash?.bucket || "auto";
  const kpi = dash?.kpi || {};

  /** Compute final {from,to} UTC for API (to is exclusive) */
  const range = useMemo(() => {
    const now = new Date();
    const today0 = utcStartOfDay(now);

    if (preset === "today") {
      const from = today0;
      const to = addDaysUtc(from, 1);
      return { from, to };
    }

    if (preset === "last7") {
      const to = addDaysUtc(today0, 1);
      const from = addDaysUtc(to, -7);
      return { from, to };
    }

    if (preset === "last30") {
      const to = addDaysUtc(today0, 1);
      const from = addDaysUtc(to, -30);
      return { from, to };
    }

    if (preset === "month") {
      const from = utcStartOfMonth(monthAnchor);
      const to = addMonthsUtc(from, 1);
      return { from, to };
    }

    if (preset === "year") {
      const from = utcStartOfYear(yearAnchor);
      const to = addYearsUtc(from, 1);
      return { from, to };
    }

    // custom
    if (customMode === "day") {
      const s = customDayRange?.[0] ? new Date(customDayRange[0]) : today0;
      const e = customDayRange?.[1] ? new Date(customDayRange[1]) : s;

      const from = utcStartOfDay(s);
      // end is inclusive in UI => exclusive = end + 1 day
      const to = addDaysUtc(utcStartOfDay(e), 1);

      if (to <= from) return { from, to: addDaysUtc(from, 1) };
      return { from, to };
    }

    if (customMode === "month") {
      const mf = utcStartOfMonth(customMonthFrom || new Date());
      const mt = utcStartOfMonth(customMonthTo || customMonthFrom || new Date());
      const from = mf;
      const to = addMonthsUtc(mt, 1);
      if (to <= from) return { from, to: addMonthsUtc(from, 1) };
      return { from, to };
    }

    // year
    const yf = utcStartOfYear(customYearFrom || new Date());
    const yt = utcStartOfYear(customYearTo || customYearFrom || new Date());
    const from = yf;
    const to = addYearsUtc(yt, 1);
    if (to <= from) return { from, to: addYearsUtc(from, 1) };
    return { from, to };
  }, [
    preset,
    monthAnchor,
    yearAnchor,
    customMode,
    customDayRange,
    customMonthFrom,
    customMonthTo,
    customYearFrom,
    customYearTo,
  ]);

  const rangeText = useMemo(() => {
    const f = `${pad2(range.from.getUTCDate())}/${pad2(range.from.getUTCMonth() + 1)}/${range.from.getUTCFullYear()}`;
    const t = `${pad2(range.to.getUTCDate())}/${pad2(range.to.getUTCMonth() + 1)}/${range.to.getUTCFullYear()}`;
    return `${f} → ${t}`;
  }, [range]);

  const trendData = useMemo(() => {
    const arr = Array.isArray(dash?.trend) ? dash.trend : [];
    return arr.map((x) => ({
      startUtc: x.startUtc,
      endUtc: x.endUtc,
      label: bucketLabel(bucketUsed, x.startUtc),
      orders: Number(x.orders || 0),
      paidOrders: Number(x.paidOrders || 0),
      revenue: Number(x.revenue || 0),
    }));
  }, [dash, bucketUsed]);

  const statusPie = useMemo(() => {
    const arr = Array.isArray(dash?.statusBreakdown) ? dash.statusBreakdown : [];
    return arr
      .map((x) => ({
        status: x.status,
        name: viOrderStatus(x.status),
        value: Number(x.count || 0),
      }))
      .filter((x) => x.value > 0);
  }, [dash]);

  // Colors
  const C_ORDERS = "#2563eb";
  const C_PAID = "#16a34a";
  const C_REV = "#f59e0b";

  const load = useCallback(async (r) => {
    setErr("");
    setLoading(true);
    try {
      const res = await OrdersDashboardAdminApi.getDashboard({
        fromUtc: r.from.toISOString(),
        toUtc: r.to.toISOString(),
        bucket: "auto",
      });

      const data = res?.data || res;
      setDash(data);
    } catch (e) {
      setErr(e?.response?.data?.message || e?.message || "Không tải được dashboard đơn hàng.");
      setDash(null);
    } finally {
      setLoading(false);
    }
  }, []);

  // ✅ Auto apply (debounce) khi đổi filter
  const debounceRef = useRef(null);
  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => load(range), 250);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [range, load]);

  const resetFilters = useCallback(() => {
    const now = new Date();
    setPreset("last30");

    setMonthAnchor(utcStartOfMonth(now));
    setYearAnchor(utcStartOfYear(now));

    setCustomMode("day");
    const to = addDaysUtc(utcStartOfDay(now), 1);
    const from = addDaysUtc(to, -7);
    setCustomDayRange([from, addDaysUtc(to, -1)]);
    setCustomMonthFrom(utcStartOfMonth(now));
    setCustomMonthTo(utcStartOfMonth(now));
    setCustomYearFrom(utcStartOfYear(now));
    setCustomYearTo(utcStartOfYear(now));
  }, []);

  const monthText = useMemo(() => {
    const d = utcStartOfMonth(monthAnchor);
    return `${pad2(d.getUTCMonth() + 1)}/${d.getUTCFullYear()}`;
  }, [monthAnchor]);

  const yearText = useMemo(() => {
    const d = utcStartOfYear(yearAnchor);
    return `${d.getUTCFullYear()}`;
  }, [yearAnchor]);

  return (
    <div className="aod-page">
      <div className="aod-head">
        <div>
          <h1 className="aod-title">Dashboard đơn hàng</h1>
          <div className="aod-sub">
            <span className="aod-subrange">{rangeText}</span>
            {loading ? <span className="aod-loadingdot"> • Đang tải…</span> : null}
          </div>
        </div>

        <div className="aod-actions">
          <button className="aod-btn aod-btn-ghost" onClick={resetFilters} disabled={loading} type="button">
            Reset
          </button>
        </div>
      </div>

      {/* Filters */}
      <section className="aod-section">
        <div className="aod-section-title">Bộ lọc thời gian</div>

        <div className="aod-filters">
          <div className="aod-filter aod-filter-wide">
            <div className="aod-filterbar2">
              <div className="aod-seg aod-seg-wide">
                <button className={`aod-segbtn ${preset === "today" ? "is-on" : ""}`} onClick={() => setPreset("today")} type="button">
                  Hôm nay
                </button>
                <button className={`aod-segbtn ${preset === "last7" ? "is-on" : ""}`} onClick={() => setPreset("last7")} type="button">
                  7 ngày
                </button>
                <button className={`aod-segbtn ${preset === "last30" ? "is-on" : ""}`} onClick={() => setPreset("last30")} type="button">
                  30 ngày
                </button>
                <button className={`aod-segbtn ${preset === "month" ? "is-on" : ""}`} onClick={() => setPreset("month")} type="button">
                  Theo tháng
                </button>
                <button className={`aod-segbtn ${preset === "year" ? "is-on" : ""}`} onClick={() => setPreset("year")} type="button">
                  Theo năm
                </button>
                <button className={`aod-segbtn ${preset === "custom" ? "is-on" : ""}`} onClick={() => setPreset("custom")} type="button">
                  Tuỳ chọn
                </button>
              </div>

              {/* Right-side compact controls */}
              <div className="aod-compact">
                {preset === "month" ? (
                  <div className="aod-compact-row">
                    <button
                      className="aod-navbtn"
                      onClick={() => setMonthAnchor(addMonthsUtc(utcStartOfMonth(monthAnchor), -1))}
                      title="Tháng trước"
                      type="button"
                    >
                      ‹
                    </button>
                    <DatePicker
                      selected={new Date(monthAnchor)}
                      onChange={(d) => d && setMonthAnchor(utcStartOfMonth(d))}
                      className="aod-input aod-mini"
                      dateFormat="MM/yyyy"
                      showMonthYearPicker
                    />
                    <button
                      className="aod-navbtn"
                      onClick={() => setMonthAnchor(addMonthsUtc(utcStartOfMonth(monthAnchor), 1))}
                      title="Tháng sau"
                      type="button"
                    >
                      ›
                    </button>
                    
                  </div>
                ) : null}

                {preset === "year" ? (
                  <div className="aod-compact-row">
                    <button
                      className="aod-navbtn"
                      onClick={() => setYearAnchor(addYearsUtc(utcStartOfYear(yearAnchor), -1))}
                      title="Năm trước"
                      type="button"
                    >
                      ‹
                    </button>
                    <DatePicker
                      selected={new Date(yearAnchor)}
                      onChange={(d) => d && setYearAnchor(utcStartOfYear(d))}
                      className="aod-input aod-mini"
                      dateFormat="yyyy"
                      showYearPicker
                    />
                    <button
                      className="aod-navbtn"
                      onClick={() => setYearAnchor(addYearsUtc(utcStartOfYear(yearAnchor), 1))}
                      title="Năm sau"
                      type="button"
                    >
                      ›
                    </button>
                    
                  </div>
                ) : null}

                {preset === "custom" ? (
                  <div className="aod-custom">
                    <div className="aod-seg">
                      <button
                        className={`aod-segbtn ${customMode === "day" ? "is-on" : ""}`}
                        onClick={() => setCustomMode("day")}
                        title="Chọn theo ngày"
                        type="button"
                      >
                        Ngày
                      </button>
                      <button
                        className={`aod-segbtn ${customMode === "month" ? "is-on" : ""}`}
                        onClick={() => setCustomMode("month")}
                        title="Chọn theo tháng"
                        type="button"
                      >
                        Tháng
                      </button>
                      <button
                        className={`aod-segbtn ${customMode === "year" ? "is-on" : ""}`}
                        onClick={() => setCustomMode("year")}
                        title="Chọn theo năm"
                        type="button"
                      >
                        Năm
                      </button>
                    </div>

                    {customMode === "day" ? (
                      <div className="aod-compact-row">
                        <DatePicker
                          selectsRange
                          startDate={customDayRange?.[0] ? new Date(customDayRange[0]) : null}
                          endDate={customDayRange?.[1] ? new Date(customDayRange[1]) : null}
                          onChange={(update) => setCustomDayRange(update)}
                          className="aod-input aod-range-one"
                          dateFormat="dd/MM/yyyy"
                          placeholderText="Chọn khoảng ngày"
                        />
                      </div>
                    ) : null}

                    {customMode === "month" ? (
                      <div className="aod-compact-row aod-2pick">
                        <DatePicker
                          selected={new Date(customMonthFrom)}
                          onChange={(d) => d && setCustomMonthFrom(utcStartOfMonth(d))}
                          className="aod-input aod-mini"
                          dateFormat="MM/yyyy"
                          showMonthYearPicker
                        />
                        <span className="aod-mid">→</span>
                        <DatePicker
                          selected={new Date(customMonthTo)}
                          onChange={(d) => d && setCustomMonthTo(utcStartOfMonth(d))}
                          className="aod-input aod-mini"
                          dateFormat="MM/yyyy"
                          showMonthYearPicker
                        />
                      </div>
                    ) : null}

                    {customMode === "year" ? (
                      <div className="aod-compact-row aod-2pick">
                        <DatePicker
                          selected={new Date(customYearFrom)}
                          onChange={(d) => d && setCustomYearFrom(utcStartOfYear(d))}
                          className="aod-input aod-mini"
                          dateFormat="yyyy"
                          showYearPicker
                        />
                        <span className="aod-mid">→</span>
                        <DatePicker
                          selected={new Date(customYearTo)}
                          onChange={(d) => d && setCustomYearTo(utcStartOfYear(d))}
                          className="aod-input aod-mini"
                          dateFormat="yyyy"
                          showYearPicker
                        />
                      </div>
                    ) : null}
                  </div>
                ) : null}
              </div>
            </div>
          </div>
        </div>

        {err ? <div className="aod-error">{err}</div> : null}
      </section>

      {/* KPI */}
      <section className="aod-section">
        <div className="aod-kpis">
          <div className="aod-card">
            <div className="aod-card-label">Tổng đơn</div>
            <div className="aod-card-val">{fmtInt(kpi.totalOrders)}</div>
            <div className="aod-card-foot">Trong khoảng thời gian đã chọn</div>
          </div>

          <div className="aod-card">
            <div className="aod-card-label">Đã thanh toán</div>
            <div className="aod-card-val">{fmtInt(kpi.paidOrders)}</div>
            <div className="aod-card-foot">
              Tỉ lệ: <b>{((Number(kpi.conversionRate || 0) * 100) || 0).toFixed(1)}%</b>
            </div>
          </div>

          <div className="aod-card">
            <div className="aod-card-label">Doanh thu</div>
            <div className="aod-card-val">{fmtVnd(kpi.revenue)}</div>
            <div className="aod-card-foot">Chỉ tính đơn đã thanh toán</div>
          </div>

          <div className="aod-card">
            <div className="aod-card-label">Giá trị TB / đơn</div>
            <div className="aod-card-val">{fmtVnd(kpi.avgPaidOrderValue)}</div>
            <div className="aod-card-foot">Trên đơn đã thanh toán</div>
          </div>
        </div>

        {!!dash?.alerts?.length && (
          <div className="aod-alerts">
            {dash.alerts.map((a, idx) => (
              <div
                key={idx}
                className={`aod-alert ${
                  a.severity === 2 ? "aod-alert-error" : a.severity === 1 ? "aod-alert-warning" : "aod-alert-info"
                }`}
              >
                <div className="aod-alert-code">{a.code}</div>
                <div className="aod-alert-msg">{a.message}</div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* Charts */}
      <section className="aod-section">
        <div className="aod-grid-3">
          {/* Trend */}
          <div className="aod-panel">
            <div className="aod-panel-head">
              <div>
                <div className="aod-panel-title">Xu hướng đơn hàng</div>
              </div>
              <div className="aod-unit">Đơn / Doanh thu</div>
            </div>

            <div className="aod-chart">
              <ResponsiveContainer width="100%" height={280}>
                <ComposedChart data={trendData} margin={{ top: 8, right: 10, bottom: 0, left: 0 }}>
                  <CartesianGrid stroke="#eef2f7" />
                  <XAxis dataKey="label" interval="preserveStartEnd" tickMargin={8} />
                  <YAxis yAxisId="left" allowDecimals={false} />
                  <YAxis yAxisId="right" orientation="right" tickFormatter={(v) => (v >= 1e6 ? `${Math.round(v / 1e6)}tr` : v)} />
                  <Tooltip content={<OrdersTooltip />} />
                  <Legend />
                  <Bar yAxisId="right" dataKey="revenue" name="Doanh thu" fill={C_REV} isAnimationActive={false} barSize={18} />
                  <Line yAxisId="left" type="monotone" dataKey="orders" name="Đơn hàng" stroke={C_ORDERS} strokeWidth={2} dot={false} isAnimationActive={false} />
                  <Line yAxisId="left" type="monotone" dataKey="paidOrders" name="Đã thanh toán" stroke={C_PAID} strokeWidth={2} dot={false} isAnimationActive={false} />
                  {trendData.length > 20 ? <Brush dataKey="label" height={20} travellerWidth={10} /> : null}
                </ComposedChart>
              </ResponsiveContainer>
            </div>
          </div>

          {/* Pie */}
          <div className="aod-panel">
            <div className="aod-panel-head">
              <div>
                <div className="aod-panel-title">Tỉ lệ trạng thái đơn</div>
              </div>
            </div>

            <div className="aod-chart">
              <ResponsiveContainer width="100%" height={280}>
                <PieChart>
                  <Tooltip formatter={(value, name) => [fmtInt(value), name]} />
                  <Legend />
                  <Pie data={statusPie} dataKey="value" nameKey="name" innerRadius={55} outerRadius={85} paddingAngle={2} labelLine={false}>
                    {statusPie.map((_, idx) => (
                      <Cell key={idx} fill={["#2563eb", "#16a34a", "#f59e0b", "#ef4444", "#6b7280", "#a78bfa"][idx % 6]} />
                    ))}
                  </Pie>
                </PieChart>
              </ResponsiveContainer>
            </div>

            
          </div>

          {/* Status list */}
          <div className="aod-panel">
            <div className="aod-panel-head">
              <div>
                <div className="aod-panel-title">Top trạng thái</div>
              </div>
            </div>

            <div className="aod-list">
              {statusPie.length ? (
                statusPie
                  .slice()
                  .sort((a, b) => b.value - a.value)
                  .map((x, idx) => (
                    <div key={idx} className="aod-item">
                      <div className="aod-item-name">{x.name}</div>
                      <div className="aod-item-val">{fmtInt(x.value)}</div>
                    </div>
                  ))
              ) : (
                <div className="aod-empty">Chưa có dữ liệu</div>
              )}
            </div>
          </div>
        </div>
      </section>

      {/* Bestsellers */}
      <section className="aod-section">
        <div className="aod-section-title">Sản phẩm bán chạy nhất (đơn đã thanh toán)</div>

        <div className="aod-grid-2">
          <div className="aod-panel">
            <div className="aod-panel-head">
              <div>
                <div className="aod-panel-title">Top sản phẩm</div>
              </div>
            </div>
            <TopTable kind="product" items={dash?.topProducts} />
          </div>

          <div className="aod-panel">
            <div className="aod-panel-head">
              <div>
                <div className="aod-panel-title">Top biến thể</div>
              </div>
            </div>
            <TopTable kind="variant" items={dash?.topVariants} />
          </div>
        </div>
      </section>
    </div>
  );
}
