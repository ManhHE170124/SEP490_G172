// File: src/pages/admin/AdminPaymentListPage.jsx
import React from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { paymentApi } from "../../services/paymentApi";
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

const PAYMENT_STATUS_FILTER_OPTIONS = [
  { value: "", label: "Tất cả trạng thái" },
  { value: "Pending", label: "Chờ thanh toán" },
  { value: "Paid", label: "Đã thanh toán" },
  { value: "Success", label: "Success" },
  { value: "Completed", label: "Completed" },
  { value: "Cancelled", label: "Đã hủy" },
  { value: "Failed", label: "Thất bại" },
  { value: "Refunded", label: "Hoàn tiền" },
  { value: "Timeout", label: "Hết hạn" },
  { value: "DupCancelled", label: "Hủy do tạo phiên mới" },
  { value: "NeedReview", label: "Cần kiểm tra" },
  { value: "Replaced", label: "Replaced" },
];

const TARGET_TYPE_OPTIONS = [
  { value: "", label: "Tất cả loại" },
  { value: "Order", label: "Đơn hàng" },
  { value: "SupportPlan", label: "Gói hỗ trợ" },
];

const getPaymentStatusClass = (status) => {
  const s = (status || "").toLowerCase();
  if (["paid", "success", "completed", "refunded"].includes(s)) return "status-pill payment-paid";
  if (["pending"].includes(s)) return "status-pill payment-pending";
  if (["cancelled", "timeout", "dupcancelled", "failed", "replaced"].includes(s))
    return "status-pill payment-cancelled";
  return "status-pill payment-unknown";
};

const getPaymentStatusLabel = (status) => {
  const s = (status || "").toLowerCase();
  switch (s) {
    case "pending":
      return "Chờ thanh toán";
    case "paid":
      return "Đã thanh toán";
    case "success":
      return "Success";
    case "completed":
      return "Completed";
    case "cancelled":
      return "Đã hủy";
    case "failed":
      return "Thất bại";
    case "refunded":
      return "Hoàn tiền";
    case "timeout":
      return "Hết hạn";
    case "dupcancelled":
      return "Hủy do tạo phiên mới";
    case "needreview":
      return "Cần kiểm tra";
    case "replaced":
      return "Replaced";
    default:
      return status || "Không xác định";
  }
};

const getTargetTypeLabel = (type) => {
  const t = (type || "").toLowerCase();
  if (t === "order") return "Đơn hàng";
  if (t === "supportplan") return "Gói hỗ trợ";
  return type || "—";
};

const getTargetTypeChipClass = (type) => {
  const t = (type || "").toLowerCase();
  if (t === "order") return "op-chip op-chip-order";
  if (t === "supportplan") return "op-chip op-chip-plan";
  return "op-chip";
};

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

/* ===== Modal: Payment detail (giữ style/flow cũ) ===== */
function PaymentDetailModal({ open, paymentId, onClose, addToast, onOpenOrder }) {
  const [loading, setLoading] = React.useState(false);
  const [payment, setPayment] = React.useState(null);

  React.useEffect(() => {
    if (!open || !paymentId) return;

    setLoading(true);
    paymentApi
      .get(paymentId, { includeCheckoutUrl: true, includeAttempts: true })
      .then((res) => setPayment(unwrap(res)))
      .catch((err) => {
        console.error(err);
        addToast?.("error", err?.response?.data?.message || "Không tải được chi tiết thanh toán.", "Lỗi");
      })
      .finally(() => setLoading(false));
  }, [open, paymentId, addToast]);

  if (!open) return null;

  const ts = payment?.targetSnapshot || payment?.TargetSnapshot || null;
  const attempts = Array.isArray(payment?.attempts)
    ? payment.attempts
    : Array.isArray(payment?.Attempts)
    ? payment.Attempts
    : [];

  const targetType = payment?.targetType || payment?.TargetType;
  const targetDisplayId =
    payment?.targetDisplayId ||
    payment?.TargetDisplayId ||
    payment?.targetId ||
    payment?.TargetId ||
    (targetType && String(targetType).toLowerCase() === "order" ? ts?.orderId : ts?.userId) ||
    null;

  const isOrderTarget = String(targetType || "").toLowerCase() === "order";
  const guessOrderId = isOrderTarget ? targetDisplayId || ts?.orderId || null : null;

  const openCheckoutUrl = () => {
    if (!payment?.checkoutUrl) return;
    window.open(payment.checkoutUrl, "_blank", "noopener,noreferrer");
  };

  const onCopy = async (text) => {
    const ok = await copyText(text);
    addToast(ok ? "success" : "error", ok ? "Đã copy" : "Copy thất bại", ok ? "OK" : "Lỗi");
  };

  return (
    <div className="cat-modal-backdrop">
      <div className="cat-modal-card">
        <div className="cat-modal-header">
          <h3>Chi tiết thanh toán</h3>
          {payment?.status ? (
            <span className={getPaymentStatusClass(payment.status)}>{getPaymentStatusLabel(payment.status)}</span>
          ) : null}
        </div>

        <div className="cat-modal-body">
          {loading && <span className="badge gray">Đang tải…</span>}

          {!loading && payment && (
            <>
              <div className="detail-section-title">Tổng quan</div>
              <div className="detail-section-box op-2col">
                <div>
                  <div className="detail-label">PaymentId</div>
                  <div className="detail-value mono">
                    {payment.paymentId}{" "}
                    <IconButton title="Copy" variant="ghost" onClick={() => onCopy(payment.paymentId)}>
                      <Icon name="copy" />
                    </IconButton>
                  </div>
                </div>
                <div>
                  <div className="detail-label">Ngày tạo</div>
                  <div className="detail-value">{formatVnDateTime(payment.createdAt)}</div>
                </div>
                <div>
                  <div className="detail-label">Email thanh toán</div>
                  <div className="detail-value">{payment.email || ts?.userEmail || ts?.orderEmail || "—"}</div>
                </div>
                <div>
                  <div className="detail-label">Số tiền</div>
                  <div className="detail-value text-mono">{formatMoney(payment.amount)}</div>
                </div>
              </div>

              <div className="detail-section-title">Target</div>
              <div className="detail-section-box op-2col">
                <div>
                  <div className="detail-label">Loại</div>
                  <div className="detail-value">
                    <span className={getTargetTypeChipClass(targetType)}>{getTargetTypeLabel(targetType)}</span>
                  </div>
                </div>
                <div>
                  <div className="detail-label">TargetId (display)</div>
                  <div className="detail-value mono">{targetDisplayId || "—"}</div>
                </div>

                {guessOrderId ? (
                  <div style={{ gridColumn: "1 / -1", display: "flex", gap: 8, flexWrap: "wrap", marginTop: 6 }}>
                    <button type="button" className="btn" onClick={() => onOpenOrder?.(guessOrderId)}>
                      Mở Order
                    </button>
                  </div>
                ) : null}
              </div>

              <div className="detail-section-title">PayOS</div>
              <div className="detail-section-box op-2col">
                <div>
                  <div className="detail-label">Provider</div>
                  <div className="detail-value">{payment.provider || "PayOS"}</div>
                </div>
                <div>
                  <div className="detail-label">ProviderOrderCode</div>
                  <div className="detail-value mono">{payment.providerOrderCode ?? "—"}</div>
                </div>
                <div>
                  <div className="detail-label">PaymentLinkId</div>
                  <div className="detail-value mono">{payment.paymentLinkId || "—"}</div>
                </div>
                <div>
                  <div className="detail-label">ExpiresAt</div>
                  <div className="detail-value">{formatVnDateTime(payment.expiresAtUtc)}</div>
                </div>

                <div style={{ gridColumn: "1 / -1", display: "flex", gap: 8, flexWrap: "wrap", marginTop: 6 }}>
                  <button type="button" className="btn" disabled={!payment.checkoutUrl} onClick={openCheckoutUrl}>
                    Mở checkoutUrl
                  </button>
                </div>
              </div>

              {attempts.length > 0 ? (
                <>
                  <div className="detail-section-title">Attempts (cùng target)</div>
                  <div className="detail-section-box" style={{ overflowX: "auto" }}>
                    <table className="order-items-table">
                      <thead>
                        <tr>
                          <th>PaymentId</th>
                          <th>Trạng thái</th>
                          <th>ProviderOrderCode</th>
                          <th>Ngày tạo</th>
                          <th>Hết hạn</th>
                        </tr>
                      </thead>
                      <tbody>
                        {attempts.map((a) => (
                          <tr key={a.paymentId}>
                            <td className="mono">{shortId(a.paymentId, 10, 6)}</td>
                            <td>
                              <span className={getPaymentStatusClass(a.status)}>{getPaymentStatusLabel(a.status)}</span>
                              {a.isExpired ? <span className="badge gray" style={{ marginLeft: 8 }}>Expired</span> : null}
                            </td>
                            <td className="mono">{a.providerOrderCode || "—"}</td>
                            <td>{formatVnDateTime(a.createdAt)}</td>
                            <td>{formatVnDateTime(a.expiresAtUtc)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </>
              ) : null}
            </>
          )}
        </div>

        <div className="cat-modal-footer">
          <button type="button" className="btn ghost" onClick={onClose}>
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
}

export default function AdminPaymentListPage() {
  const nav = useNavigate();
  const loc = useLocation();

  const query = React.useMemo(() => new URLSearchParams(loc.search), [loc.search]);
  const initialSearch = query.get("search") || "";

  // Toast
  const [toasts, setToasts] = React.useState([]);
  const addToast = React.useCallback((type, message, title) => {
    const tid = `${Date.now()}_${Math.random().toString(16).slice(2)}`;
    setToasts((prev) => [...prev, { id: tid, type, message, title }]);
    return tid;
  }, []);
  const removeToast = React.useCallback((tid) => {
    setToasts((prev) => prev.filter((t) => t.id !== tid));
  }, []);

  // list state
  const [loading, setLoading] = React.useState(false);
  const [paged, setPaged] = React.useState({ pageIndex: 1, pageSize: 10, totalItems: 0, items: [] });
  const [sort, setSort] = React.useState({ sortBy: "createdAt", sortDir: "desc" });

  const [filter, setFilter] = React.useState({
    search: initialSearch,
    createdFrom: "",
    createdTo: "",
    paymentStatus: "",
    targetType: "",
    minAmount: "",
    maxAmount: "",
  });

  const debouncedSearch = useDebouncedValue(filter.search, 350);

  // modal
  const [paymentModal, setPaymentModal] = React.useState({ open: false, id: null });
  const openPayment = (pid) => setPaymentModal({ open: true, id: pid || null });
  const closePayment = () => setPaymentModal({ open: false, id: null });

  const goToPage = (pageIndex) => setPaged((p) => ({ ...p, pageIndex: Math.max(1, Number(pageIndex) || 1) }));
  const totalPages = Math.max(1, Math.ceil((paged.totalItems || 0) / (paged.pageSize || 10)));

  React.useEffect(() => {
    setLoading(true);

    const params = {
      search: (debouncedSearch || "").trim() || undefined,
      createdFrom: toUtcIsoFromDateOnly(filter.createdFrom, false) || undefined,
      createdTo: toUtcIsoFromDateOnly(filter.createdTo, true) || undefined,
      paymentStatus: filter.paymentStatus || undefined,
      targetType: filter.targetType || undefined, // BE mới: loại giao dịch
      minAmount: filter.minAmount !== "" ? Number(filter.minAmount) : undefined,
      maxAmount: filter.maxAmount !== "" ? Number(filter.maxAmount) : undefined,
      sortBy: sort.sortBy,
      sortDir: sort.sortDir,
      pageIndex: paged.pageIndex,
      pageSize: paged.pageSize,
    };

    paymentApi
      .listPaged(params)
      .then((x) => setPaged((p) => ({ ...p, ...x })))
      .catch((err) => {
        console.error(err);
        addToast("error", err?.response?.data?.message || "Không tải được danh sách payments.", "Lỗi");
      })
      .finally(() => setLoading(false));
  }, [
    debouncedSearch,
    filter.createdFrom,
    filter.createdTo,
    filter.paymentStatus,
    filter.targetType,
    filter.minAmount,
    filter.maxAmount,
    sort.sortBy,
    sort.sortDir,
    paged.pageIndex,
    paged.pageSize,
    addToast,
  ]);

  const openOrderFromPayment = (orderId) => {
    if (!orderId) return;
    nav(`/admin/orders/${orderId}`);
  };

  const onCopy = async (text) => {
    const ok = await copyText(text);
    addToast(ok ? "success" : "error", ok ? "Đã copy" : "Copy thất bại", ok ? "OK" : "Lỗi");
  };

  return (
    <div className="op-page">
      <ToastContainer toasts={toasts} onClose={removeToast} />

      <div className="order-payment-header">
        <h2>Payments (Admin)</h2>
        <div className="op-inline-actions">
          <button type="button" className="btn ghost" onClick={() => nav("/admin/orders")}>
            Sang Orders
          </button>
        </div>
      </div>

      <div className="op-toolbar">
        <div className="op-filters">
          <div className="op-group">
            <span>Search (PaymentId / OrderId / UserId)</span>
            <input
              value={filter.search}
              onChange={(e) => {
                setFilter((f) => ({ ...f, search: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
              placeholder="PaymentId / OrderId / UserId"
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
              value={filter.paymentStatus}
              onChange={(e) => {
                setFilter((f) => ({ ...f, paymentStatus: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
            >
              {PAYMENT_STATUS_FILTER_OPTIONS.map((o) => (
                <option key={o.value || "_"} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>

          <div className="op-group">
            <span>Loại giao dịch</span>
            <select
              value={filter.targetType}
              onChange={(e) => {
                setFilter((f) => ({ ...f, targetType: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
            >
              {TARGET_TYPE_OPTIONS.map((o) => (
                <option key={o.value || "_"} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>

          <div className="op-group">
            <span>Số tiền từ</span>
            <input
              inputMode="numeric"
              value={filter.minAmount}
              onChange={(e) => {
                setFilter((f) => ({ ...f, minAmount: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
              placeholder="0"
            />
          </div>

          <div className="op-group">
            <span>Đến</span>
            <input
              inputMode="numeric"
              value={filter.maxAmount}
              onChange={(e) => {
                setFilter((f) => ({ ...f, maxAmount: e.target.value }));
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
          <div>Danh sách payments</div>
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
                        setSort((s) => toggleSortState(s, "paymentId"));
                        setPaged((p) => ({ ...p, pageIndex: 1 }));
                      }}
                    >
                      PaymentId{renderSortIndicator(sort, "paymentId")}
                    </button>
                  </th>
                  <th>
                    <button
                      className="table-sort-header"
                      onClick={() => {
                        setSort((s) => toggleSortState(s, "targetType"));
                        setPaged((p) => ({ ...p, pageIndex: 1 }));
                      }}
                    >
                      Target{renderSortIndicator(sort, "targetType")}
                    </button>
                  </th>
                  <th className="text-right">
                    <button
                      className="table-sort-header"
                      onClick={() => {
                        setSort((s) => toggleSortState(s, "amount"));
                        setPaged((p) => ({ ...p, pageIndex: 1 }));
                      }}
                    >
                      Số tiền{renderSortIndicator(sort, "amount")}
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
                {paged.items.map((p) => {
                  const paymentId = p.paymentId || p.PaymentId;
                  const targetType = p.targetType || p.TargetType;
                  const targetDisplayId =
                    p.targetDisplayId || p.TargetDisplayId || p.targetId || p.TargetId || "—";
                  const status = p.status || p.Status;
                  const createdAt = p.createdAt || p.CreatedAt;

                  const isOrderTarget = String(targetType || "").toLowerCase() === "order";

                  return (
                    <tr key={paymentId}>
                      <td>
                        <div className="op-cell-main">
                          <div className="op-cell-title mono" title={paymentId}>
                            {shortId(paymentId, 10, 6)}
                            <span className="op-inline-actions">
                              <IconButton title="Copy" variant="ghost" onClick={() => onCopy(paymentId)}>
                                <Icon name="copy" />
                              </IconButton>
                            </span>
                          </div>
                          <div className="op-cell-sub">
                            ProviderOrderCode: <span className="mono">{p.providerOrderCode ?? "—"}</span>
                          </div>
                        </div>
                      </td>

                      <td>
                        <div className="op-target-cell">
                          <div className="op-target-sub">
                            <span className={getTargetTypeChipClass(targetType)}>{getTargetTypeLabel(targetType)}</span>
                          </div>
                          <div className="op-submono">{String(targetDisplayId)}</div>
                        </div>
                      </td>

                      <td className="text-right">
                        <div className="op-cell-main">
                          <div className="op-cell-title text-mono">{formatMoney(p.amount)}</div>
                          <div className="op-cell-sub">{p.email || "—"}</div>
                        </div>
                      </td>

                      <td>
                        <span className={getPaymentStatusClass(status)}>{getPaymentStatusLabel(status)}</span>
                        {p.isExpired ? <span className="badge gray" style={{ marginLeft: 8 }}>Expired</span> : null}
                      </td>

                      <td>{formatVnDateTime(createdAt)}</td>

                      <td className="op-td-actions">
                        <div className="op-actions">
                          <IconButton title="Xem chi tiết" variant="primary" onClick={() => openPayment(paymentId)}>
                            <Icon name="eye" />
                          </IconButton>

                          {isOrderTarget && targetDisplayId && targetDisplayId !== "—" ? (
                            <button
                              type="button"
                              className="btn ghost"
                              onClick={() => openOrderFromPayment(String(targetDisplayId))}
                            >
                              Mở Order
                            </button>
                          ) : null}
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
            <input style={{ width: 90, height: 36 }} value={paged.pageIndex} onChange={(e) => goToPage(e.target.value)} />
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

      <PaymentDetailModal
        open={paymentModal.open}
        paymentId={paymentModal.id}
        onClose={closePayment}
        addToast={addToast}
        onOpenOrder={openOrderFromPayment}
      />
    </div>
  );
}
