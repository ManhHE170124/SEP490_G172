// File: src/pages/admin/AdminOrderListPage.jsx
import React from "react";
import { useNavigate } from "react-router-dom";
import { orderApi } from "../../services/orderApi";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./OrderPaymentPage.css";

/* ===== Helpers (giữ style cũ) ===== */
const unwrap = (res) => res?.data ?? res;

const formatVnDateTime = (value) => {
  if (!value) return "—";
  const d = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(d.getTime())) return "—";
  return d.toLocaleString("vi-VN");
};

const formatMoney = (n) => {
  if (n === null || n === undefined) return "—";
  const num = Number(n);
  if (Number.isNaN(num)) return "—";
  return `${num.toLocaleString("vi-VN")} đ`;
};

const shortId = (s, head = 6, tail = 4) => {
  const v = String(s || "");
  if (!v) return "—";
  if (v.length <= head + tail + 3) return v;
  return `${v.slice(0, head)}…${v.slice(-tail)}`;
};

const copyText = async (text) => {
  try {
    if (!text) return false;
    await navigator.clipboard.writeText(String(text));
    return true;
  } catch {
    return false;
  }
};

function useDebouncedValue(value, delay = 350) {
  const [debounced, setDebounced] = React.useState(value);
  React.useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}

const toggleSortState = (current, key) =>
  current.sortBy === key
    ? { sortBy: key, sortDir: current.sortDir === "asc" ? "desc" : "asc" }
    : { sortBy: key, sortDir: "asc" };

const renderSortIndicator = (current, key) => {
  if (!current || current.sortBy !== key) return null;
  return current.sortDir === "asc" ? " ▲" : " ▼";
};

/**
 * Convert date-only (yyyy-mm-dd) theo TZ +07:00 -> ISO UTC string để lọc CreatedAt (UTC) ổn định hơn.
 */
const toUtcIsoFromDateOnly = (dateStr, endOfDay = false) => {
  if (!dateStr) return "";
  const time = endOfDay ? "23:59:59" : "00:00:00";
  const d = new Date(`${dateStr}T${time}+07:00`);
  if (Number.isNaN(d.getTime())) return "";
  return d.toISOString();
};

const ORDER_STATUS_FILTER_OPTIONS = [
  { value: "", label: "Tất cả trạng thái" },
  { value: "PendingPayment", label: "Chờ thanh toán" },
  { value: "Paid", label: "Đã thanh toán" },
  { value: "Completed", label: "Hoàn tất" },
  { value: "Cancelled", label: "Đã hủy" },
  { value: "CancelledByTimeout", label: "Hủy do timeout" },
  { value: "NeedsManualAction", label: "Cần xử lý thủ công" },
];

function Icon({ name }) {
  switch (name) {
    case "eye":
      return (
        <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
          <path
            fill="currentColor"
            d="M12 5c5.5 0 9.5 4.5 10.5 6-1 1.5-5 6-10.5 6S2.5 12.5 1.5 11C2.5 9.5 6.5 5 12 5zm0 10a4 4 0 1 0 0-8 4 4 0 0 0 0 8zm0-2.2a1.8 1.8 0 1 1 0-3.6 1.8 1.8 0 0 1 0 3.6z"
          />
        </svg>
      );
    case "copy":
      return (
        <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
          <path
            fill="currentColor"
            d="M8 7a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2h-9a2 2 0 0 1-2-2V7zm-3 10V4a2 2 0 0 1 2-2h10v2H7v13H5z"
          />
        </svg>
      );
    default:
      return null;
  }
}

function IconButton({ title, ariaLabel, onClick, disabled, variant = "default", children }) {
  return (
    <button
      type="button"
      className={`op-icon-btn ${variant}`}
      onClick={onClick}
      disabled={disabled}
      title={title}
      aria-label={ariaLabel || title}
    >
      {children}
    </button>
  );
}

function PrimaryCell({ title, sub, mono = false, onCopy, copyValue }) {
  return (
    <div className="op-cell-main">
      <div className={`op-cell-title ${mono ? "mono" : ""}`} title={title}>
        {title}
        {onCopy && copyValue ? (
          <span className="op-inline-actions">
            <IconButton
              title="Copy"
              variant="ghost"
              onClick={(e) => {
                e.stopPropagation();
                onCopy(copyValue);
              }}
            >
              <Icon name="copy" />
            </IconButton>
          </span>
        ) : null}
      </div>
      {sub ? (
        <div className="op-cell-sub" title={sub}>
          {sub}
        </div>
      ) : null}
    </div>
  );
}

export default function AdminOrderListPage() {
  const nav = useNavigate();

  // Toast
  const [toasts, setToasts] = React.useState([]);
  const addToast = React.useCallback((type, message, title) => {
    const id = `${Date.now()}_${Math.random().toString(16).slice(2)}`;
    setToasts((prev) => [...prev, { id, type, message, title }]);
    return id;
  }, []);
  const removeToast = React.useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const [loading, setLoading] = React.useState(false);
  const [paged, setPaged] = React.useState({
    pageIndex: 1,
    pageSize: 10,
    totalItems: 0,
    items: [],
  });

  const [sort, setSort] = React.useState({ sortBy: "createdAt", sortDir: "desc" });

  const [filter, setFilter] = React.useState({
    search: "",
    createdFrom: "",
    createdTo: "",
    orderStatus: "",
    minTotal: "",
    maxTotal: "",
  });

  const debouncedSearch = useDebouncedValue(filter.search, 350);

  const goToPage = (pageIndex) => {
    setPaged((p) => ({ ...p, pageIndex: Math.max(1, Number(pageIndex) || 1) }));
  };

  React.useEffect(() => {
    setLoading(true);

    const params = {
      search: (debouncedSearch || "").trim() || undefined,
      createdFrom: toUtcIsoFromDateOnly(filter.createdFrom, false) || undefined,
      createdTo: toUtcIsoFromDateOnly(filter.createdTo, true) || undefined,
      orderStatus: filter.orderStatus || undefined,
      minTotal: filter.minTotal !== "" ? Number(filter.minTotal) : undefined,
      maxTotal: filter.maxTotal !== "" ? Number(filter.maxTotal) : undefined,
      sortBy: sort.sortBy,
      sortDir: sort.sortDir,
      pageIndex: paged.pageIndex,
      pageSize: paged.pageSize,
    };

    orderApi
      .listPaged(params)
      .then((x) => setPaged((p) => ({ ...p, ...x })))
      .catch((err) => {
        console.error(err);
        addToast("error", err?.response?.data?.message || "Không tải được danh sách đơn hàng.", "Lỗi");
      })
      .finally(() => setLoading(false));
  }, [
    debouncedSearch,
    filter.createdFrom,
    filter.createdTo,
    filter.orderStatus,
    filter.minTotal,
    filter.maxTotal,
    sort.sortBy,
    sort.sortDir,
    paged.pageIndex,
    paged.pageSize,
    addToast,
  ]);

  const totalPages = Math.max(1, Math.ceil((paged.totalItems || 0) / (paged.pageSize || 10)));

  const onCopy = async (text) => {
    const ok = await copyText(text);
    addToast(ok ? "success" : "error", ok ? "Đã copy" : "Copy thất bại", ok ? "OK" : "Lỗi");
  };

  return (
    <div className="op-page">
      <ToastContainer toasts={toasts} onClose={removeToast} />

      <div className="order-payment-header">
        <h2>Đơn hàng (Admin)</h2>
        <div className="op-inline-actions">
          <button type="button" className="btn ghost" onClick={() => nav("/admin/payments")}>
            Sang Payments
          </button>
        </div>
      </div>

      <div className="op-toolbar">
        <div className="op-filters">
          <div className="op-group">
            <span>Search (OrderId / Email)</span>
            <input
              value={filter.search}
              onChange={(e) => {
                setFilter((f) => ({ ...f, search: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
              placeholder="VD: 3fae... hoặc mail@example.com"
            />
          </div>

          <div className="op-group">
            <span>Từ ngày</span>
            <input
              type="date"
              value={filter.createdFrom}
              onChange={(e) => {
                setFilter((f) => ({ ...f, createdFrom: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
            />
          </div>

          <div className="op-group">
            <span>Đến ngày</span>
            <input
              type="date"
              value={filter.createdTo}
              onChange={(e) => {
                setFilter((f) => ({ ...f, createdTo: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
            />
          </div>

          <div className="op-group">
            <span>Trạng thái</span>
            <select
              value={filter.orderStatus}
              onChange={(e) => {
                setFilter((f) => ({ ...f, orderStatus: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
            >
              {ORDER_STATUS_FILTER_OPTIONS.map((o) => (
                <option key={o.value || "_"} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>

          <div className="op-group">
            <span>Tổng tiền từ</span>
            <input
              inputMode="numeric"
              value={filter.minTotal}
              onChange={(e) => {
                setFilter((f) => ({ ...f, minTotal: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
              placeholder="0"
            />
          </div>

          <div className="op-group">
            <span>Đến</span>
            <input
              inputMode="numeric"
              value={filter.maxTotal}
              onChange={(e) => {
                setFilter((f) => ({ ...f, maxTotal: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
              placeholder="1000000"
            />
          </div>

          <div className="op-group">
            <span>Page size</span>
            <select
              value={paged.pageSize}
              onChange={(e) => setPaged((p) => ({ ...p, pageSize: Number(e.target.value) || 10, pageIndex: 1 }))}
            >
              {[10, 20, 50].map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      <div className="cat-card" style={{ marginTop: 12 }}>
        <div className="cat-card-title" style={{ display: "flex", justifyContent: "space-between", gap: 10 }}>
          <div>Danh sách đơn hàng</div>
          <div className="badge gray">
            {paged.totalItems || 0} items • page {paged.pageIndex}/{totalPages}
          </div>
        </div>

        {loading ? <div className="op-empty">Đang tải…</div> : null}

        {!loading && (
          <div style={{ overflowX: "auto" }}>
            <table className="op-table">
              <thead>
                <tr>
                  <th>
                    <button
                      className="table-sort-header"
                      onClick={() => {
                        setSort((s) => toggleSortState(s, "orderId"));
                        setPaged((p) => ({ ...p, pageIndex: 1 }));
                      }}
                    >
                      OrderId{renderSortIndicator(sort, "orderId")}
                    </button>
                  </th>
                  <th>Người mua</th>
                  <th className="text-right">
                    <button
                      className="table-sort-header"
                      onClick={() => {
                        setSort((s) => toggleSortState(s, "amount"));
                        setPaged((p) => ({ ...p, pageIndex: 1 }));
                      }}
                    >
                      Tổng/Final{renderSortIndicator(sort, "amount")}
                    </button>
                  </th>
                  <th>
                    <button
                      className="table-sort-header"
                      onClick={() => {
                        setSort((s) => toggleSortState(s, "status"));
                        setPaged((p) => ({ ...p, pageIndex: 1 }));
                      }}
                    >
                      Trạng thái{renderSortIndicator(sort, "status")}
                    </button>
                  </th>
                  <th>
                    <button
                      className="table-sort-header"
                      onClick={() => {
                        setSort((s) => toggleSortState(s, "createdAt"));
                        setPaged((p) => ({ ...p, pageIndex: 1 }));
                      }}
                    >
                      Ngày tạo{renderSortIndicator(sort, "createdAt")}
                    </button>
                  </th>
                  <th className="op-th-actions">Actions</th>
                </tr>
              </thead>
              <tbody>
                {paged.items.map((o) => {
                  const orderId = o.orderId || o.OrderId;
                  const orderNumber = o.orderNumber || o.OrderNumber;
                  const email = o.email || o.Email || o.userEmail || o.UserEmail;
                  const userName = o.userName || o.UserName;
                  const createdAt = o.createdAt || o.CreatedAt;
                  const status = o.status || o.Status;

                  const totalAmount = o.totalAmount ?? o.TotalAmount;
                  const finalAmount = o.finalAmount ?? o.FinalAmount;

                  return (
                    <tr key={orderId} className="op-row-click">
                      <td>
                        <PrimaryCell
                          title={orderNumber || shortId(orderId, 10, 6)}
                          sub={orderId}
                          mono
                          onCopy={onCopy}
                          copyValue={orderId}
                        />
                      </td>
                      <td>
                        <div className="op-target-cell">
                          <div className="op-target-sub">
                            <span className="op-subtext">{userName || "—"}</span>
                          </div>
                          <div className="op-submono">{email || "—"}</div>
                        </div>
                      </td>
                      <td className="text-right">
                        <div className="op-cell-main">
                          <div className="op-cell-title text-mono">{formatMoney(finalAmount ?? totalAmount)}</div>
                          <div className="op-cell-sub">Total: {formatMoney(totalAmount)}</div>
                        </div>
                      </td>
                      <td>{status ? <span className="badge gray">{status}</span> : "—"}</td>
                      <td>{formatVnDateTime(createdAt)}</td>
                      <td className="op-td-actions">
                        <div className="op-actions">
                          <IconButton title="Chi tiết" onClick={() => nav(`/admin/orders/${orderId}`)} variant="primary">
                            <Icon name="eye" />
                          </IconButton>
                          <button
                            type="button"
                            className="btn ghost"
                            onClick={() => nav(`/admin/payments?search=${encodeURIComponent(orderId)}`)}
                          >
                            Payments
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}

                {paged.items.length === 0 ? (
                  <tr>
                    <td colSpan={6}>
                      <div className="op-empty">Không có dữ liệu.</div>
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "space-between", gap: 10, marginTop: 12, flexWrap: "wrap" }}>
          <button
            type="button"
            className="btn ghost"
            disabled={paged.pageIndex <= 1}
            onClick={() => goToPage(paged.pageIndex - 1)}
          >
            Trang trước
          </button>

          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <span className="badge gray">Page</span>
            <input
              style={{ width: 90, height: 36 }}
              value={paged.pageIndex}
              onChange={(e) => goToPage(e.target.value)}
            />
            <span className="badge gray">/ {totalPages}</span>
          </div>

          <button
            type="button"
            className="btn ghost"
            disabled={paged.pageIndex >= totalPages}
            onClick={() => goToPage(paged.pageIndex + 1)}
          >
            Trang sau
          </button>
        </div>
      </div>
    </div>
  );
}
