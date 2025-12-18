// File: src/pages/admin/AdminOrderListPage.jsx
import React, { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { orderApi } from "../../services/orderApi";
import "./AdminOrderListPage.css";

const formatMoneyVnd = (n) => {
  const x = Number(n || 0);
  return x.toLocaleString("vi-VN") + " đ";
};

const formatDateTime = (iso) => {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "—";
  const pad = (v) => String(v).padStart(2, "0");
  return `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())} ${pad(
    d.getDate()
  )}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;
};

const normalizeStatusKey = (s) => String(s || "").trim().toLowerCase();

const getOrderStatusLabel = (status) => {
  const s = normalizeStatusKey(status);
  if (s === "paid" || s === "completed" || s === "success") return "Đã thanh toán";
  if (s === "pending") return "Đang chờ";
  if (s === "cancelledbytimeout") return "Hủy do quá hạn";
  if (s === "cancelled" || s === "canceled") return "Đã hủy";
  if (s === "failed") return "Thất bại";
  if (s === "refunded") return "Đã hoàn tiền";
  if (!s) return "Không rõ";
  return status; // fallback
};

const statusPillClass = (status) => {
  const s = normalizeStatusKey(status);
  if (s === "paid" || s === "completed" || s === "success") return "payment-paid";
  if (s === "pending") return "payment-pending";
  if (s === "cancelled" || s === "canceled" || s === "cancelledbytimeout") return "payment-cancelled";
  if (s === "failed") return "payment-failed";
  if (s === "refunded") return "payment-refunded";
  return "payment-unknown";
};

const ORDER_STATUS_OPTIONS = [
  { value: "", label: "Tất cả trạng thái" },
  { value: "Paid", label: "Đã thanh toán" },
  { value: "Pending", label: "Đang chờ" },
  { value: "Cancelled", label: "Đã hủy" },
  { value: "CancelledByTimeout", label: "Hủy do quá hạn" },
  { value: "Failed", label: "Thất bại" },
  { value: "Refunded", label: "Đã hoàn tiền" },
];

const DEFAULT_FILTERS = {
  search: "",
  createdFrom: "",
  createdTo: "",
  orderStatus: "",
  minTotal: "",
  maxTotal: "",
};

const DEFAULT_SORT = { sortBy: "createdat", sortDir: "desc" };
const DEFAULT_PAGE_SIZE = 10;

export default function AdminOrderListPage() {
  const navigate = useNavigate();

  // ✅ tách draft (UI) và filters (đã áp dụng) để search hoạt động chắc chắn khi bấm Lọc/Enter
  const [draft, setDraft] = useState(DEFAULT_FILTERS);
  const [filters, setFilters] = useState(DEFAULT_FILTERS);

  const [sort, setSort] = useState(DEFAULT_SORT);
  const [paged, setPaged] = useState({
    pageIndex: 1,
    pageSize: DEFAULT_PAGE_SIZE,
    totalItems: 0,
    items: [],
  });

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const totalPages = useMemo(() => {
    const t = Math.max(0, Number(paged.totalItems || 0));
    const s = Math.max(1, Number(paged.pageSize || DEFAULT_PAGE_SIZE));
    return Math.max(1, Math.ceil(t / s));
  }, [paged.totalItems, paged.pageSize]);

  const fetchOrders = async () => {
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
        totalItems: res?.totalItems ?? 0,
        items: Array.isArray(res?.items) ? res.items : [],
      }));
    } catch (e) {
      setError(e?.message || "Không tải được danh sách đơn hàng");
      setPaged((p) => ({ ...p, totalItems: 0, items: [] }));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchOrders();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters, sort.sortBy, sort.sortDir, paged.pageIndex, paged.pageSize]);

  const onDraftChange = (key) => (e) => {
    const v = e?.target?.value ?? "";
    setDraft((d) => ({ ...d, [key]: v }));
  };

  const applyFilters = (e) => {
    e?.preventDefault?.();
    setPaged((p) => ({ ...p, pageIndex: 1 }));
    setFilters({ ...draft });
  };

  const resetAll = () => {
    setDraft(DEFAULT_FILTERS);
    setFilters(DEFAULT_FILTERS);
    setSort(DEFAULT_SORT);
    setPaged((p) => ({ ...p, pageIndex: 1, pageSize: DEFAULT_PAGE_SIZE }));
  };

  const toggleSort = (field) => {
    setPaged((p) => ({ ...p, pageIndex: 1 }));
    setSort((s) => {
      if (s.sortBy === field) {
        return { ...s, sortDir: s.sortDir === "asc" ? "desc" : "asc" };
      }
      return { sortBy: field, sortDir: "asc" };
    });
  };

  const renderSortCaret = (field) => {
    if (sort.sortBy !== field) return null;
    return sort.sortDir === "asc" ? "▲" : "▼";
  };

  const pageButtons = useMemo(() => {
    const cur = Number(paged.pageIndex || 1);
    const total = Number(totalPages || 1);

    const btns = [];
    const push = (n) =>
      btns.push(
        <button
          key={n}
          type="button"
          className={`aol-pageBtn ${n === cur ? "active" : ""}`}
          onClick={() => setPaged((p) => ({ ...p, pageIndex: n }))}
        >
          {n}
        </button>
      );

    if (total <= 7) {
      for (let i = 1; i <= total; i++) push(i);
      return btns;
    }

    push(1);

    const left = Math.max(2, cur - 1);
    const right = Math.min(total - 1, cur + 1);

    if (left > 2) btns.push(<span key="ld" className="aol-dots">…</span>);
    for (let i = left; i <= right; i++) push(i);
    if (right < total - 1) btns.push(<span key="rd" className="aol-dots">…</span>);

    push(total);
    return btns;
  }, [paged.pageIndex, totalPages]);

  return (
    <div className="aol-page">
      <div className="aol-card">
        <div className="order-payment-header">
          <h2>Danh sách đơn hàng</h2>
        </div>

        <div className="op-toolbar">
          <form className="op-filters" onSubmit={applyFilters}>
            <div className="op-group">
              <span>Tìm kiếm (Mã đơn / Email)</span>
              <input
                value={draft.search}
                onChange={onDraftChange("search")}
                placeholder="VD: 1ffa... hoặc mail@example.com"
              />
            </div>

            <div className="op-group">
              <span>Từ ngày</span>
              <input type="date" value={draft.createdFrom} onChange={onDraftChange("createdFrom")} />
            </div>

            <div className="op-group">
              <span>Đến ngày</span>
              <input type="date" value={draft.createdTo} onChange={onDraftChange("createdTo")} />
            </div>

            <div className="op-group">
              <span>Trạng thái</span>
              <select value={draft.orderStatus} onChange={onDraftChange("orderStatus")}>
                {ORDER_STATUS_OPTIONS.map((x) => (
                  <option key={x.value || "__all"} value={x.value}>
                    {x.label}
                  </option>
                ))}
              </select>
            </div>

            <div className="op-group">
              <span>Tổng tiền</span>
              <div className="aol-amountRange">
                <input
                  type="number"
                  min="0"
                  value={draft.minTotal}
                  onChange={onDraftChange("minTotal")}
                  placeholder="Từ"
                />
                <input
                  type="number"
                  min="0"
                  value={draft.maxTotal}
                  onChange={onDraftChange("maxTotal")}
                  placeholder="Đến"
                />
              </div>
            </div>

            <div className="aol-filterActions">
              <button type="submit" className="aol-btn aol-btnPrimary">
                Lọc
              </button>
              <button type="button" className="aol-btn aol-btnGhost" onClick={resetAll}>
                Đặt lại
              </button>
            </div>
          </form>

          <div className="aol-topInfo">
            Tổng: {paged.totalItems} • Trang {paged.pageIndex}/{totalPages}
          </div>
        </div>

        <div className="aol-tableWrap">
          <table className="op-table table">
            <thead>
              <tr>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() => toggleSort("orderid")}
                  >
                    Mã đơn {renderSortCaret("orderid")}
                  </button>
                </th>
                <th>Người mua</th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() => toggleSort("amount")}
                  >
                    Tổng tiền {renderSortCaret("amount")}
                  </button>
                </th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() => toggleSort("status")}
                  >
                    Trạng thái {renderSortCaret("status")}
                  </button>
                </th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() => toggleSort("createdat")}
                  >
                    Ngày tạo {renderSortCaret("createdat")}
                  </button>
                </th>
                <th className="op-th-actions">Chi tiết</th>
              </tr>
            </thead>

            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={6} className="op-cell-sub">
                    Đang tải...
                  </td>
                </tr>
              ) : error ? (
                <tr>
                  <td colSpan={6} className="op-cell-sub">
                    {error}
                  </td>
                </tr>
              ) : (paged.items || []).length === 0 ? (
                <tr>
                  <td colSpan={6} className="op-cell-sub">
                    Không có dữ liệu
                  </td>
                </tr>
              ) : (
                paged.items.map((row) => {
                  const orderIdStr = String(row?.orderId || "");
                  const buyerName = row?.userName || "—";
                  const buyerEmail = row?.userEmail || row?.email || "—";

                  const total = Number(row?.totalAmount ?? 0);
                  const final = Number(row?.finalAmount ?? total);
                  const hasDiscount = Math.abs(total - final) > 0.0001;

                  return (
                    <tr key={orderIdStr || Math.random()}>
                      <td>
                        <div className="op-cell-main">
                          <div className="op-cell-title">
                            <span className="aol-orderId mono">{orderIdStr || "—"}</span>
                          </div>
                        </div>
                      </td>

                      <td>
                        <div className="op-cell-main">
                          <div className="op-cell-title">{buyerName}</div>
                          <div className="op-cell-sub">{buyerEmail}</div>
                        </div>
                      </td>

                      <td>
                        <div className="aol-price">
                          <div className="aol-price-new">{formatMoneyVnd(final)}</div>
                          {hasDiscount && <div className="aol-price-old">{formatMoneyVnd(total)}</div>}
                        </div>
                      </td>

                      <td>
                        <span className={`status-pill ${statusPillClass(row?.status)}`}>
                          {getOrderStatusLabel(row?.status)}
                        </span>
                      </td>

                      <td className="mono">{formatDateTime(row?.createdAt)}</td>

                      <td className="op-td-actions">
                        <button
                          type="button"
                          className="op-icon-btn"
                          title="Xem chi tiết"
                          onClick={() => navigate(`/admin/orders/${orderIdStr}`)}
                        >
                          <i className="fa fa-eye" aria-hidden="true" />
                        </button>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        <div className="aol-pager">
          <div className="aol-pager-center">
            <button
              type="button"
              className="aol-navBtn"
              disabled={paged.pageIndex <= 1}
              onClick={() => setPaged((p) => ({ ...p, pageIndex: Math.max(1, p.pageIndex - 1) }))}
            >
              ← Trước
            </button>

            {pageButtons}

            <button
              type="button"
              className="aol-navBtn"
              disabled={paged.pageIndex >= totalPages}
              onClick={() =>
                setPaged((p) => ({ ...p, pageIndex: Math.min(totalPages, p.pageIndex + 1) }))
              }
            >
              Sau →
            </button>
          </div>

          <div className="aol-pager-right">
            <select
              className="aol-pageSizeSelect"
              value={paged.pageSize}
              onChange={(e) => {
                const v = Number(e.target.value || DEFAULT_PAGE_SIZE);
                setPaged((p) => ({ ...p, pageIndex: 1, pageSize: v }));
              }}
              title="Kích thước trang"
            >
              {[5, 10, 20, 50].map((n) => (
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
