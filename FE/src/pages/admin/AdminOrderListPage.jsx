// File: src/pages/admin/AdminOrderListPage.jsx
import React, { useEffect, useMemo, useState, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { orderApi } from "../../services/orderApi";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";
import "./AdminOrderListPage.css";
import formatDatetime from "../../utils/formatDatetime";

const formatMoneyVnd = (v) => {
  const n = Number(v ?? 0);
  return n.toLocaleString("vi-VN") + " đ";
};

const formatDateTime = (iso) => formatDatetime(iso);

/**
 * ✅ Order statuses đúng theo BE:
 * PendingPayment, Paid, Cancelled, CancelledByTimeout, NeedsManualAction
 */
const ORDER_STATUS_OPTIONS = [
  { value: "", label: "Tất cả trạng thái" },
  { value: "PendingPayment", label: "Chờ thanh toán" },
  { value: "Paid", label: "Đã thanh toán" },
  { value: "NeedsManualAction", label: "Cần xử lý thủ công" },
  { value: "CancelledByTimeout", label: "Đã hủy do quá hạn" },
  { value: "Cancelled", label: "Đã hủy" },
];

const normalizeStatusKey = (s) => String(s || "").trim().toUpperCase();

const getOrderStatusLabel = (statusRaw) => {
  const s = String(statusRaw || "").trim();
  const hit = ORDER_STATUS_OPTIONS.find((x) => x.value === s);
  return hit?.label || "Không rõ";
};

const statusPillClass = (statusRaw) => {
  const v = normalizeStatusKey(statusRaw);

  if (v === "PAID" || v === "SUCCESS" || v === "COMPLETED") return "payment-paid";

  if (v === "PENDINGPAYMENT" || v === "PENDING") return "payment-pending";
  if (v === "NEEDSMANUALACTION") return "payment-pending";

  if (v === "CANCELLEDBYTIMEOUT" || v === "TIMEOUT") return "payment-timeout";
  if (v === "CANCELLED") return "payment-cancelled";

  return "payment-unknown";
};

const Icon = ({ name, size = 18 }) => {
  const common = {
    width: size,
    height: size,
    viewBox: "0 0 24 24",
    fill: "none",
    xmlns: "http://www.w3.org/2000/svg",
  };

  if (name === "eye") {
    return (
      <svg {...common}>
        <path
          d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z"
          stroke="currentColor"
          strokeWidth="2"
        />
        <path
          d="M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z"
          stroke="currentColor"
          strokeWidth="2"
        />
      </svg>
    );
  }

  if (name === "filter") {
    return (
      <svg {...common}>
        <path
          d="M4 6h16M7 12h10M10 18h4"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
        />
      </svg>
    );
  }

  // reset
  return (
    <svg {...common}>
      <path
        d="M21 12a9 9 0 1 1-3-6.7"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
      <path
        d="M21 3v7h-7"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
};

export default function AdminOrderListPage() {
  const nav = useNavigate();

  const DEFAULT_FILTERS = useMemo(
    () => ({
      search: "",
      createdFrom: null,
      createdTo: null,
      orderStatus: "",
      minTotal: "",
      maxTotal: "",
    }),
    []
  );

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // ✅ draft để người dùng nhập, bấm icon “lọc” mới áp dụng
  const [draft, setDraft] = useState(DEFAULT_FILTERS);
  const [filters, setFilters] = useState(DEFAULT_FILTERS);

  const [sort, setSort] = useState({ sortBy: "createdat", sortDir: "desc" });
  const [paged, setPaged] = useState({
    pageIndex: 1,
    pageSize: 10,
    totalItems: 0,
    items: [],
  });

  const totalPages = useMemo(() => {
    const ps = Math.max(1, Number(paged.pageSize || 10));
    return Math.max(1, Math.ceil((paged.totalItems || 0) / ps));
  }, [paged.pageSize, paged.totalItems]);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await orderApi.listPaged({
        ...filters,
        sortBy: sort.sortBy,
        sortDir: sort.sortDir,
        pageIndex: paged.pageIndex,
        pageSize: paged.pageSize,
      });

      setPaged((p) => ({
        ...p,
        totalItems: res.totalItems ?? 0,
        items: res.items ?? [],
      }));
    } catch (e) {
      setError(e?.message || "Không thể tải danh sách đơn hàng");
    } finally {
      setLoading(false);
    }
  }, [filters, sort.sortBy, sort.sortDir, paged.pageIndex, paged.pageSize]);

  useEffect(() => {
    load();
  }, [load]);

  const applyFilters = () => {
    setPaged((p) => ({ ...p, pageIndex: 1 }));
    const fmtYmd = (d) => {
      if (!d) return "";
      const dt = new Date(d);
      if (Number.isNaN(dt.getTime())) return "";
      const yy = dt.getFullYear();
      const mm = String(dt.getMonth() + 1).padStart(2, "0");
      const dd = String(dt.getDate()).padStart(2, "0");
      return `${yy}-${mm}-${dd}`;
    };

    setFilters({
      ...draft,
      search: String(draft.search || "").trim(),
      createdFrom: fmtYmd(draft.createdFrom),
      createdTo: fmtYmd(draft.createdTo),
    });
  };

  const resetFilters = () => {
    setSort({ sortBy: "createdat", sortDir: "desc" });
    setPaged((p) => ({ ...p, pageIndex: 1, pageSize: 10 }));
    setDraft(DEFAULT_FILTERS);
    setFilters(DEFAULT_FILTERS);
  };

  const onDraftChange = (key, val) => {
    setDraft((f) => ({ ...f, [key]: val }));
  };

  const toggleSort = (sortBy) => {
    setPaged((p) => ({ ...p, pageIndex: 1 }));
    setSort((s) => {
      const nextDir = s.sortBy === sortBy ? (s.sortDir === "asc" ? "desc" : "asc") : "desc";
      return { sortBy, sortDir: nextDir };
    });
  };

  const buildPageButtons = () => {
    const current = paged.pageIndex;
    const total = totalPages;
    const btns = [];

    const pushBtn = (n) => {
      btns.push(
        <button
          key={`p-${n}`}
          className={`aol-pageBtn ${n === current ? "active" : ""}`}
          onClick={() => setPaged((p) => ({ ...p, pageIndex: n }))}
          type="button"
        >
          {n}
        </button>
      );
    };

    const pushDots = (key) => {
      btns.push(
        <span key={key} className="aol-dots">
          …
        </span>
      );
    };

    if (total <= 7) {
      for (let i = 1; i <= total; i++) pushBtn(i);
      return btns;
    }

    pushBtn(1);

    if (current > 3) pushDots("d1");

    const start = Math.max(2, current - 1);
    const end = Math.min(total - 1, current + 1);
    for (let i = start; i <= end; i++) pushBtn(i);

    if (current < total - 2) pushDots("d2");

    pushBtn(total);
    return btns;
  };

  const onEnterApply = (e) => {
    if (e.key === "Enter") applyFilters();
  };

  return (
    <div className="aol-page">
      <div className="order-payment-header">
        <h2>Danh sách đơn hàng</h2>
      </div>

      <div className="aol-card">
        <div className="op-toolbar">
          <div className="op-filters">
            <div className="op-group">
              <span>Tìm kiếm (mã đơn / email)</span>
              <input
                value={draft.search}
                onChange={(e) => onDraftChange("search", e.target.value)}
                onKeyDown={onEnterApply}
                placeholder="VD: 1ffa... hoặc mail@example.com"
              />
            </div>

            <div className="op-group">
              <span>Từ ngày</span>
              <DatePicker
                selected={draft.createdFrom}
                onChange={(d) => onDraftChange("createdFrom", d)}
                className="aol-dateInput"
                dateFormat="dd/MM/yyyy"
                placeholderText="Chọn ngày"
              />
            </div>

            <div className="op-group">
              <span>Đến ngày</span>
              <DatePicker
                selected={draft.createdTo}
                onChange={(d) => onDraftChange("createdTo", d)}
                className="aol-dateInput"
                dateFormat="dd/MM/yyyy"
                placeholderText="Chọn ngày"
              />
            </div>

            <div className="op-group">
              <span>Trạng thái</span>
              <select
                value={draft.orderStatus}
                onChange={(e) => onDraftChange("orderStatus", e.target.value)}
              >
                {ORDER_STATUS_OPTIONS.map((o) => (
                  <option key={o.value || "all"} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </div>

            <div className="op-group">
              <span>Tổng tiền</span>
              <div className="aol-amountRange">
                <input
                  inputMode="numeric"
                  placeholder="Từ"
                  value={draft.minTotal}
                  onChange={(e) => onDraftChange("minTotal", e.target.value)}
                  onKeyDown={onEnterApply}
                />
                <input
                  inputMode="numeric"
                  placeholder="Đến"
                  value={draft.maxTotal}
                  onChange={(e) => onDraftChange("maxTotal", e.target.value)}
                  onKeyDown={onEnterApply}
                />
              </div>
            </div>

            <div className="aol-filterActions">
              <button
                type="button"
                className="aol-iconActionBtn primary"
                onClick={applyFilters}
                title="Lọc"
                aria-label="Lọc"
              >
                <Icon name="filter" />
              </button>
              <button
                type="button"
                className="aol-iconActionBtn"
                onClick={resetFilters}
                title="Đặt lại"
                aria-label="Đặt lại"
              >
                <Icon name="reset" />
              </button>
            </div>
          </div>
        </div>

        <div className="aol-topInfo">
          {loading ? "Đang tải..." : `Tổng: ${paged.totalItems} • Trang ${paged.pageIndex}/${totalPages}`}
        </div>

        {error ? (
          <div style={{ marginTop: 10, color: "#b91c1c", fontWeight: 800 }}>{error}</div>
        ) : null}

        <div className="aol-tableWrap">
          <table className="op-table table">
            <thead>
              <tr>
                <th>Mã đơn</th>
                <th>Người mua</th>
                <th>Tổng tiền</th>
                <th>Trạng thái</th>
                <th>
                  <button
                    className="table-sort-header"
                    type="button"
                    onClick={() => toggleSort("createdat")}
                    title="Sắp xếp theo ngày tạo"
                  >
                    Ngày tạo {sort.sortBy === "createdat" ? (sort.sortDir === "asc" ? "▲" : "▼") : ""}
                  </button>
                </th>
                <th className="op-th-actions">Chi tiết</th>
              </tr>
            </thead>

            <tbody>
              {(paged.items || []).map((o) => {
                const orderId = String(o.orderId ?? o.OrderId ?? "");
                const buyerName = o.userName ?? o.UserName ?? "—";
                const buyerEmail = o.userEmail ?? o.UserEmail ?? o.email ?? o.Email ?? "";

                const totalAmount = Number(o.totalAmount ?? o.TotalAmount ?? 0);
                const finalAmount = Number(o.finalAmount ?? o.FinalAmount ?? totalAmount);

                const status = o.status ?? o.Status ?? "";
                const createdAt = o.createdAt ?? o.CreatedAt;

                return (
                  <tr key={orderId || Math.random()}>
                    <td>
                      <div className="op-cell-main">
                        <div className="op-cell-title">
                          <span className="aol-orderId mono">{orderId || "—"}</span>
                        </div>
                      </div>
                    </td>

                    <td>
                      <div className="op-cell-main">
                        <div className="op-cell-title" style={{ fontWeight: 800 }}>{buyerName}</div>
                        <div className="op-cell-sub">{buyerEmail || "—"}</div>
                      </div>
                    </td>

                    <td>
                      <div className="aol-price">
                        <div className="aol-price-new">{formatMoneyVnd(finalAmount)}</div>
                        {totalAmount && totalAmount !== finalAmount ? (
                          <div className="aol-price-old">{formatMoneyVnd(totalAmount)}</div>
                        ) : null}
                      </div>
                    </td>

                    <td>
                      <span className={`status-pill ${statusPillClass(status)}`}>
                        {getOrderStatusLabel(status)}
                      </span>
                    </td>

                    <td className="mono">{formatDateTime(createdAt)}</td>

                    <td className="op-td-actions">
                      <button
                        className="op-icon-btn"
                        type="button"
                        title="Xem chi tiết"
                        onClick={() => nav(`/admin/orders/${orderId}`)}
                        disabled={!orderId}
                      >
                        <Icon name="eye" />
                      </button>
                    </td>
                  </tr>
                );
              })}

              {!loading && (!paged.items || paged.items.length === 0) ? (
                <tr>
                  <td colSpan={6} style={{ padding: 16, color: "#6b7280", fontWeight: 700 }}>
                    Không có dữ liệu
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>

        <div className="aol-pager">
          <div className="aol-pager-center">
            <button
              className="aol-navBtn"
              type="button"
              onClick={() => setPaged((p) => ({ ...p, pageIndex: Math.max(1, p.pageIndex - 1) }))}
              disabled={paged.pageIndex <= 1}
              title="Trang trước"
            >
              ← Trước
            </button>

            {buildPageButtons()}

            <button
              className="aol-navBtn"
              type="button"
              onClick={() => setPaged((p) => ({ ...p, pageIndex: Math.min(totalPages, p.pageIndex + 1) }))}
              disabled={paged.pageIndex >= totalPages}
              title="Trang sau"
            >
              Sau →
            </button>
          </div>

          <div className="aol-pager-right">
            <select
              className="aol-pageSizeSelect"
              value={paged.pageSize}
              onChange={(e) =>
                setPaged((p) => ({
                  ...p,
                  pageIndex: 1,
                  pageSize: Number(e.target.value || 10),
                }))
              }
              title="Số dòng mỗi trang"
            >
              {[10, 20, 30, 50].map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>
    </div>
  );
}
