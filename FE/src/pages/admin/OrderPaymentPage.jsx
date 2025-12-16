import React from "react";
import { orderApi } from "../../services/orderApi";
import { paymentApi } from "../../services/paymentApi";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./OrderPaymentPage.css";

/* ===== Helpers ===== */

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

/* ===== Payment meta ===== */

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
];

const PAYMENT_PROVIDER_OPTIONS = ["", "PayOS"];

const PAYMENT_TARGET_TYPE_OPTIONS = [
  { value: "", label: "Tất cả loại" },
  { value: "Order", label: "Thanh toán đơn hàng" },
  { value: "SupportPlan", label: "Thanh toán gói hỗ trợ" },
];

const getPaymentStatusClass = (status) => {
  const s = (status || "").toLowerCase();
  if (["paid", "success", "completed", "refunded"].includes(s))
    return "status-pill payment-paid";
  if (["pending"].includes(s)) return "status-pill payment-pending";
  if (["cancelled", "timeout", "dupcancelled", "failed"].includes(s))
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

/* ===== UI blocks ===== */

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
    case "compare":
      return (
        <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
          <path
            fill="currentColor"
            d="M10.6 13.4 9.2 12l4.3-4.3a4 4 0 0 1 5.7 0 4 4 0 0 1 0 5.7l-3.4 3.4a4 4 0 0 1-5.7 0l-.8-.8 1.4-1.4.8.8a2 2 0 0 0 2.8 0l3.4-3.4a2 2 0 1 0-2.8-2.8l-4.3 4.3zm2.8-2.8 1.4 1.4-4.3 4.3a4 4 0 0 1-5.7 0 4 4 0 0 1 0-5.7l3.4-3.4a4 4 0 0 1 5.7 0l.8.8-1.4 1.4-.8-.8a2 2 0 1 0-2.8 2.8l-3.4 3.4a2 2 0 1 0 2.8 2.8l4.3-4.3z"
          />
        </svg>
      );
    case "filter":
      return (
        <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
          <path fill="currentColor" d="M3 5h18l-7 8v5l-4 2v-7L3 5z" />
        </svg>
      );
    case "receipt":
      return (
        <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
          <path
            fill="currentColor"
            d="M6 2h12v20l-2-1-2 1-2-1-2 1-2-1-2 1V2zm3 6h6v2H9V8zm0 4h6v2H9v-2z"
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
    case "sliders":
      return (
        <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
          <path
            fill="currentColor"
            d="M4 7h10v2H4V7zm0 8h16v2H4v-2zm14-9h2v4h-2V6zm-6 6h2v4h-2v-4z"
          />
        </svg>
      );
    case "x":
      return (
        <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
          <path
            fill="currentColor"
            d="M18.3 5.7 12 12l6.3 6.3-1.4 1.4L10.6 13.4 4.3 19.7 2.9 18.3 9.2 12 2.9 5.7 4.3 4.3l6.3 6.3 6.3-6.3 1.4 1.4z"
          />
        </svg>
      );
    default:
      return null;
  }
}

function IconButton({
  title,
  ariaLabel,
  onClick,
  disabled,
  variant = "default",
  children,
}) {
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

/* ===== Modal: Order detail ===== */

function OrderDetailModal({
  open,
  orderId,
  onClose,
  addToast,
  onOpenPayment,
  onQuickFilterPayments,
}) {
  const [loading, setLoading] = React.useState(false);
  const [order, setOrder] = React.useState(null);

  React.useEffect(() => {
    if (!open || !orderId) return;

    setLoading(true);
    orderApi
      .get(orderId, { includePaymentAttempts: true, includeCheckoutUrl: false })
      .then((res) => setOrder(unwrap(res)))
      .catch((err) => {
        console.error(err);
        addToast?.(
          "error",
          err?.response?.data?.message || "Không tải được chi tiết đơn hàng.",
          "Lỗi"
        );
      })
      .finally(() => setLoading(false));
  }, [open, orderId, addToast]);

  if (!open) return null;

  const effectiveFinal =
    order != null
      ? order.finalAmount ??
        (order.totalAmount != null && order.discountAmount != null
          ? order.totalAmount - order.discountAmount
          : order.totalAmount)
      : 0;

  const payment = order?.payment || null;
  const attempts = Array.isArray(order?.paymentAttempts) ? order.paymentAttempts : [];
  const items = Array.isArray(order?.orderDetails) ? order.orderDetails : [];

  return (
    <div className="cat-modal-backdrop">
      <div className="cat-modal-card">
        <div className="cat-modal-header">
          <h3>Chi tiết đơn hàng</h3>
          {order?.status ? <span className="badge gray">{order.status}</span> : null}
        </div>

        <div className="cat-modal-body">
          {loading && <span className="badge gray">Đang tải…</span>}

          {!loading && order && (
            <>
              <div className="detail-section-title">Tổng quan</div>
              <div className="detail-section-box op-2col">
                <div>
                  <div className="detail-label">OrderNumber</div>
                  <div className="detail-value mono">
                    {order.orderNumber || shortId(order.orderId)}
                  </div>
                </div>
                <div>
                  <div className="detail-label">OrderId</div>
                  <div className="detail-value mono">{order.orderId}</div>
                </div>
                <div>
                  <div className="detail-label">Ngày tạo</div>
                  <div className="detail-value">{formatVnDateTime(order.createdAt)}</div>
                </div>
                <div>
                  <div className="detail-label">Email đơn hàng</div>
                  <div className="detail-value">{order.email || "—"}</div>
                </div>
              </div>

              <div className="detail-section-title">Số tiền</div>
              <div className="detail-section-box op-3col">
                <div>
                  <div className="detail-label">Total</div>
                  <div className="detail-value text-mono">{formatMoney(order.totalAmount)}</div>
                </div>
                <div>
                  <div className="detail-label">Discount</div>
                  <div className="detail-value text-mono">{formatMoney(order.discountAmount)}</div>
                </div>
                <div>
                  <div className="detail-label">Final</div>
                  <div className="detail-value text-mono">{formatMoney(effectiveFinal)}</div>
                </div>
              </div>

              {items.length > 0 && (
                <>
                  <div className="detail-section-title">Items</div>
                  <div className="detail-section-box" style={{ overflowX: "auto" }}>
                    <table className="order-items-table">
                      <thead>
                        <tr>
                          <th>Sản phẩm</th>
                          <th>Biến thể</th>
                          <th className="text-right">SL</th>
                          <th className="text-right">Đơn giá</th>
                          <th className="text-right">Thành tiền</th>
                        </tr>
                      </thead>
                      <tbody>
                        {items.map((it) => (
                          <tr key={it.orderDetailId}>
                            <td>{it.productName || "—"}</td>
                            <td>{it.variantTitle || "—"}</td>
                            <td className="text-right">{it.quantity}</td>
                            <td className="text-right">{formatMoney(it.unitPrice)}</td>
                            <td className="text-right">{formatMoney(it.subTotal)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </>
              )}

              <div className="detail-section-title">Thanh toán (latest)</div>
              <div className="detail-section-box">
                {payment ? (
                  <div className="op-2col">
                    <div>
                      <div className="detail-label">PaymentId</div>
                      <div className="detail-value mono">{payment.paymentId}</div>
                    </div>
                    <div>
                      <div className="detail-label">Trạng thái</div>
                      <div className="detail-value">
                        <span className={getPaymentStatusClass(payment.status)}>
                          {getPaymentStatusLabel(payment.status)}
                        </span>
                        {payment.isExpired ? (
                          <span className="badge gray" style={{ marginLeft: 8 }}>
                            Expired
                          </span>
                        ) : null}
                      </div>
                    </div>
                    <div>
                      <div className="detail-label">Số tiền</div>
                      <div className="detail-value text-mono">{formatMoney(payment.amount)}</div>
                    </div>
                    <div>
                      <div className="detail-label">ProviderOrderCode</div>
                      <div className="detail-value mono">
                        {payment.providerOrderCode != null ? payment.providerOrderCode : "—"}
                      </div>
                    </div>

                    <div style={{ gridColumn: "1 / -1", display: "flex", gap: 8, flexWrap: "wrap", marginTop: 6 }}>
                      <button
                        type="button"
                        className="btn"
                        onClick={() => onOpenPayment?.(payment.paymentId)}
                      >
                        Mở chi tiết payment
                      </button>
                      <button
                        type="button"
                        className="btn ghost"
                        onClick={() => onQuickFilterPayments?.(order.orderId)}
                      >
                        Lọc payments theo OrderId
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="op-empty">Đơn này chưa có payment.</div>
                )}
              </div>

              {attempts.length > 0 && (
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
                          <th></th>
                        </tr>
                      </thead>
                      <tbody>
                        {attempts.map((a) => (
                          <tr key={a.paymentId}>
                            <td className="mono">{shortId(a.paymentId, 10, 6)}</td>
                            <td>
                              <span className={getPaymentStatusClass(a.status)}>
                                {getPaymentStatusLabel(a.status)}
                              </span>
                              {a.isExpired ? (
                                <span className="badge gray" style={{ marginLeft: 8 }}>
                                  Expired
                                </span>
                              ) : null}
                            </td>
                            <td className="mono">{a.providerOrderCode || "—"}</td>
                            <td>{formatVnDateTime(a.createdAt)}</td>
                            <td>{formatVnDateTime(a.expiresAtUtc)}</td>
                            <td style={{ whiteSpace: "nowrap", textAlign: "right" }}>
                              <IconButton
                                title="Mở payment"
                                onClick={() => onOpenPayment?.(a.paymentId)}
                              >
                                <Icon name="eye" />
                              </IconButton>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </>
              )}
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

/* ===== Modal: Payment detail ===== */

function PaymentDetailModal({
  open,
  paymentId,
  onClose,
  addToast,
  onOpenOrder,
  onQuickFilterPayments,
  onOpenPayment,
}) {
  const [loading, setLoading] = React.useState(false);
  const [payment, setPayment] = React.useState(null);

  React.useEffect(() => {
    if (!open || !paymentId) return;

    setLoading(true);
    paymentApi
      .get(paymentId, {
        includeCheckoutUrl: true,
        includeAttempts: true,
        includeTargetInfo: true,
      })
      .then((res) => setPayment(unwrap(res)))
      .catch((err) => {
        console.error(err);
        addToast?.(
          "error",
          err?.response?.data?.message || "Không tải được chi tiết thanh toán.",
          "Lỗi"
        );
      })
      .finally(() => setLoading(false));
  }, [open, paymentId, addToast]);

  if (!open) return null;

  const ts = payment?.targetSnapshot || null;
  const attempts = Array.isArray(payment?.attempts) ? payment.attempts : [];

  const isOrderTarget = (payment?.targetType || "").toLowerCase() === "order";
  const guessOrderId =
    isOrderTarget ? payment?.targetId || ts?.orderId || null : null;

  const openCheckoutUrl = () => {
    if (!payment?.checkoutUrl) return;
    window.open(payment.checkoutUrl, "_blank", "noopener,noreferrer");
  };

  return (
    <div className="cat-modal-backdrop">
      <div className="cat-modal-card">
        <div className="cat-modal-header">
          <h3>Chi tiết thanh toán</h3>
          {payment?.status ? (
            <span className={getPaymentStatusClass(payment.status)}>
              {getPaymentStatusLabel(payment.status)}
            </span>
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
                  <div className="detail-value mono">{payment.paymentId}</div>
                </div>
                <div>
                  <div className="detail-label">Ngày tạo</div>
                  <div className="detail-value">{formatVnDateTime(payment.createdAt)}</div>
                </div>
                <div>
                  <div className="detail-label">Email thanh toán</div>
                  <div className="detail-value">
                    {payment.email || ts?.userEmail || ts?.orderEmail || "—"}
                  </div>
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
                    <span className={getTargetTypeChipClass(payment.targetType)}>
                      {getTargetTypeLabel(payment.targetType)}
                    </span>
                  </div>
                </div>
                <div>
                  <div className="detail-label">TargetId</div>
                  <div className="detail-value mono">{payment.targetId || "—"}</div>
                </div>

                {(guessOrderId || "").toString().trim() ? (
                  <div style={{ gridColumn: "1 / -1", display: "flex", gap: 8, flexWrap: "wrap", marginTop: 6 }}>
                    <button
                      type="button"
                      className="btn"
                      onClick={() => onOpenOrder?.(guessOrderId)}
                    >
                      Mở Order
                    </button>
                    <button
                      type="button"
                      className="btn ghost"
                      onClick={() => onQuickFilterPayments?.(guessOrderId)}
                    >
                      Lọc payments theo OrderId
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
                  <div className="detail-value mono">
                    {payment.providerOrderCode != null ? payment.providerOrderCode : "—"}
                  </div>
                </div>
                <div>
                  <div className="detail-label">PaymentLinkId</div>
                  <div className="detail-value mono">{payment.paymentLinkId || "—"}</div>
                </div>
                <div>
                  <div className="detail-label">Hết hạn</div>
                  <div className="detail-value">
                    {formatVnDateTime(payment.expiresAtUtc)}
                    {payment.isExpired ? (
                      <span className="badge gray" style={{ marginLeft: 8 }}>
                        Expired
                      </span>
                    ) : null}
                  </div>
                </div>

                {payment.checkoutUrl ? (
                  <div style={{ gridColumn: "1 / -1", display: "flex", gap: 8, flexWrap: "wrap", marginTop: 6 }}>
                    <button type="button" className="btn" onClick={openCheckoutUrl}>
                      Mở link/QR
                    </button>
                  </div>
                ) : null}
              </div>

              {ts && (
                <>
                  <div className="detail-section-title">Snapshot</div>
                  <div className="detail-section-box">
                    <div className="op-3col">
                      <div>
                        <div className="detail-label">User</div>
                        <div className="detail-value">
                          {ts.userName || "—"}
                          {ts.userEmail ? (
                            <div className="op-subtext">{ts.userEmail}</div>
                          ) : null}
                        </div>
                      </div>

                      <div>
                        <div className="detail-label">Order status</div>
                        <div className="detail-value">{ts.orderStatus || "—"}</div>
                      </div>

                      <div>
                        <div className="detail-label">Support plan</div>
                        <div className="detail-value">{ts.supportPlanName || "—"}</div>
                      </div>
                    </div>

                    {(ts.orderId || ts.orderEmail || ts.orderFinalAmount != null) ? (
                      <div className="op-2col" style={{ marginTop: 10 }}>
                        <div>
                          <div className="detail-label">OrderId</div>
                          <div className="detail-value mono">{ts.orderId || "—"}</div>
                        </div>
                        <div>
                          <div className="detail-label">Order email</div>
                          <div className="detail-value">{ts.orderEmail || "—"}</div>
                        </div>
                        <div>
                          <div className="detail-label">Order total</div>
                          <div className="detail-value text-mono">{formatMoney(ts.orderTotalAmount)}</div>
                        </div>
                        <div>
                          <div className="detail-label">Order final</div>
                          <div className="detail-value text-mono">{formatMoney(ts.orderFinalAmount)}</div>
                        </div>
                      </div>
                    ) : null}
                  </div>
                </>
              )}

              {attempts.length > 0 && (
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
                          <th></th>
                        </tr>
                      </thead>
                      <tbody>
                        {attempts.map((a) => (
                          <tr key={a.paymentId}>
                            <td className="mono">{shortId(a.paymentId, 10, 6)}</td>
                            <td>
                              <span className={getPaymentStatusClass(a.status)}>
                                {getPaymentStatusLabel(a.status)}
                              </span>
                              {a.isExpired ? (
                                <span className="badge gray" style={{ marginLeft: 8 }}>
                                  Expired
                                </span>
                              ) : null}
                            </td>
                            <td className="mono">{a.providerOrderCode || "—"}</td>
                            <td>{formatVnDateTime(a.createdAt)}</td>
                            <td>{formatVnDateTime(a.expiresAtUtc)}</td>
                            <td style={{ whiteSpace: "nowrap", textAlign: "right" }}>
                              <IconButton
                                title="Mở payment attempt"
                                onClick={() => onOpenPayment?.(a.paymentId)}
                              >
                                <Icon name="eye" />
                              </IconButton>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </>
              )}
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

/* ===== MAIN PAGE ===== */

export default function OrderPaymentPage() {
  const ordersCardRef = React.useRef(null);
  const paymentsCardRef = React.useRef(null);

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

  // Orders state
  const [orders, setOrders] = React.useState([]);
  const [orderLoading, setOrderLoading] = React.useState(false);
  const [orderSort, setOrderSort] = React.useState({ sortBy: "createdAt", sortDir: "desc" });
  const [orderFilter, setOrderFilter] = React.useState({ keyword: "" });
  const debouncedOrderKeyword = useDebouncedValue(orderFilter.keyword, 250);
  const [orderPage, setOrderPage] = React.useState(1);
  const orderPageSize = 10;

  // Payments state
  const [payments, setPayments] = React.useState([]);
  const [paymentLoading, setPaymentLoading] = React.useState(false);
  const [paymentSort, setPaymentSort] = React.useState({ sortBy: "createdAt", sortDir: "desc" });
  const [paymentFilter, setPaymentFilter] = React.useState({
    keyword: "",
    email: "",
    status: "",
    provider: "",
    targetType: "",
    targetId: "",
  });
  const [showPaymentMoreFilters, setShowPaymentMoreFilters] = React.useState(false);
  const debouncedPaymentFilter = useDebouncedValue(paymentFilter, 350);
  const [paymentPage, setPaymentPage] = React.useState(1);
  const paymentPageSize = 10;

  // Modals
  const [orderModal, setOrderModal] = React.useState({ open: false, id: null });
  const [paymentModal, setPaymentModal] = React.useState({ open: false, id: null });

  const openOrderDetail = (id) => setOrderModal({ open: true, id: id || null });
  const closeOrderDetail = () => setOrderModal((m) => ({ ...m, open: false }));

  const openPaymentDetail = (id) => setPaymentModal({ open: true, id: id || null });
  const closePaymentDetail = () => setPaymentModal((m) => ({ ...m, open: false }));

  // Quick compare (pin)
  const [compareOrderId, setCompareOrderId] = React.useState(null);
  const [comparePaymentId, setComparePaymentId] = React.useState(null);
  const [compareOrder, setCompareOrder] = React.useState(null);
  const [comparePayment, setComparePayment] = React.useState(null);
  const [compareLoading, setCompareLoading] = React.useState({ order: false, payment: false });

  React.useEffect(() => {
    let alive = true;
    if (!compareOrderId) {
      setCompareOrder(null);
      return;
    }
    setCompareLoading((s) => ({ ...s, order: true }));
    orderApi
      .get(compareOrderId, { includePaymentAttempts: false, includeCheckoutUrl: false })
      .then((res) => alive && setCompareOrder(unwrap(res)))
      .catch(() => {})
      .finally(() => alive && setCompareLoading((s) => ({ ...s, order: false })));
    return () => {
      alive = false;
    };
  }, [compareOrderId]);

  React.useEffect(() => {
    let alive = true;
    if (!comparePaymentId) {
      setComparePayment(null);
      return;
    }
    setCompareLoading((s) => ({ ...s, payment: true }));
    paymentApi
      .get(comparePaymentId, { includeCheckoutUrl: false, includeAttempts: false, includeTargetInfo: false })
      .then((res) => alive && setComparePayment(unwrap(res)))
      .catch(() => {})
      .finally(() => alive && setCompareLoading((s) => ({ ...s, payment: false })));
    return () => {
      alive = false;
    };
  }, [comparePaymentId]);

  const quickFilterPaymentsByOrder = React.useCallback((orderId) => {
    if (!orderId) return;
    setPaymentFilter((s) => ({
      ...s,
      targetType: "Order",
      targetId: orderId,
      keyword: "",
    }));
    setPaymentPage(1);
    paymentsCardRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, []);

  // Load orders
  React.useEffect(() => {
    setOrderLoading(true);
    orderApi
      .list({ sortBy: orderSort.sortBy, sortDir: orderSort.sortDir })
      .then((res) => {
        const data = unwrap(res);
        setOrders(Array.isArray(data) ? data : []);
      })
      .catch((err) => {
        console.error(err);
        addToast("error", err?.response?.data?.message || "Không tải được danh sách đơn hàng.", "Lỗi");
      })
      .finally(() => setOrderLoading(false));
  }, [orderSort, addToast]);

  // Load payments (server-side filters)
  React.useEffect(() => {
    const p = debouncedPaymentFilter || {};
    setPaymentLoading(true);

    paymentApi
      .list({
        q: p.keyword || "",
        email: p.email || "",
        status: p.status || "",
        provider: p.provider || "",
        targetType: p.targetType || "",
        targetId: p.targetId || "",
        // ✅ list nhẹ: không cần includeTargetInfo
        sortBy: paymentSort.sortBy,
        sortDir: paymentSort.sortDir,
      })
      .then((res) => {
        const data = unwrap(res);
        setPayments(Array.isArray(data) ? data : []);
      })
      .catch((err) => {
        console.error(err);
        addToast("error", err?.response?.data?.message || "Không tải được danh sách thanh toán.", "Lỗi");
      })
      .finally(() => setPaymentLoading(false));
  }, [debouncedPaymentFilter, paymentSort, addToast]);

  // Reset pages when filters change
  React.useEffect(() => setOrderPage(1), [debouncedOrderKeyword]);
  React.useEffect(() => setPaymentPage(1), [debouncedPaymentFilter]);

  const filteredOrders = React.useMemo(() => {
    const keyword = (debouncedOrderKeyword || "").trim().toLowerCase();
    if (!keyword) return orders;

    // ✅ controller mới list nhẹ: bỏ lookup payment fields trong list :contentReference[oaicite:2]{index=2}
    return (orders || []).filter((o) => {
      const haystack = [
        o.orderNumber,
        o.orderId,
        o.status,
        o.userName,
        o.userEmail,
        o.email,
      ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();

      return haystack.includes(keyword);
    });
  }, [orders, debouncedOrderKeyword]);

  const orderTotalPages = Math.max(1, Math.ceil((filteredOrders?.length || 0) / orderPageSize));
  const pagedOrders = React.useMemo(() => {
    const start = (orderPage - 1) * orderPageSize;
    return filteredOrders.slice(start, start + orderPageSize);
  }, [filteredOrders, orderPage]);

  const paymentTotalPages = Math.max(1, Math.ceil((payments?.length || 0) / paymentPageSize));
  const pagedPayments = React.useMemo(() => {
    const start = (paymentPage - 1) * paymentPageSize;
    return payments.slice(start, start + paymentPageSize);
  }, [payments, paymentPage]);

  const clearCompare = () => {
    setCompareOrderId(null);
    setComparePaymentId(null);
  };

  const copyWithToast = async (val) => {
    const ok = await copyText(val);
    addToast(ok ? "success" : "error", ok ? "Đã copy" : "Copy thất bại");
  };

  return (
    <div className="op-page">
      <ToastContainer toasts={toasts} onRemove={removeToast} />

      {/* ===== Compare card ===== */}
      <div className="cat-card op-compare-card">
        <div className="cat-card-header order-payment-header">
          <div className="op-title-wrap">
            <h2>Đối chiếu nhanh (Order → Payment)</h2>
            {(compareOrderId || comparePaymentId) ? (
              <span className="badge gray">Đang ghim</span>
            ) : null}
          </div>
          <button type="button" className="btn ghost" onClick={clearCompare} disabled={!compareOrderId && !comparePaymentId}>
            Xóa chọn
          </button>
        </div>

        <div className="cat-card-body">
          <div className="op-compare-grid">
            <div className="op-compare-panel">
              <div className="op-compare-head">
                <div className="op-compare-label">Order</div>
                <div className="op-actions">
                  {compareOrderId ? (
                    <>
                      <IconButton title="Mở chi tiết order" onClick={() => openOrderDetail(compareOrderId)}>
                        <Icon name="eye" />
                      </IconButton>
                      <IconButton title="Lọc payments theo order" onClick={() => quickFilterPaymentsByOrder(compareOrderId)}>
                        <Icon name="filter" />
                      </IconButton>
                      <IconButton title="Bỏ ghim order" variant="ghost" onClick={() => setCompareOrderId(null)}>
                        <Icon name="x" />
                      </IconButton>
                    </>
                  ) : null}
                </div>
              </div>

              {!compareOrderId ? (
                <div className="op-empty">Chưa chọn order.</div>
              ) : (
                <>
                  <PrimaryCell
                    title={compareOrder?.orderNumber || shortId(compareOrderId)}
                    sub={compareOrder?.userEmail || compareOrder?.email || shortId(compareOrderId, 12, 6)}
                    mono
                    onCopy={copyWithToast}
                    copyValue={compareOrderId}
                  />
                  <div className="op-compare-meta">
                    {compareLoading.order ? <span className="badge gray">Đang tải…</span> : null}
                    {compareOrder?.status ? <span className="badge gray">{compareOrder.status}</span> : null}
                    {compareOrder?.finalAmount != null ? (
                      <span className="op-amount">{formatMoney(compareOrder.finalAmount)}</span>
                    ) : null}
                  </div>
                </>
              )}
            </div>

            <div className="op-compare-panel">
              <div className="op-compare-head">
                <div className="op-compare-label">Payment</div>
                <div className="op-actions">
                  {comparePaymentId ? (
                    <>
                      <IconButton title="Mở chi tiết payment" onClick={() => openPaymentDetail(comparePaymentId)}>
                        <Icon name="eye" />
                      </IconButton>
                      <IconButton title="Bỏ ghim payment" variant="ghost" onClick={() => setComparePaymentId(null)}>
                        <Icon name="x" />
                      </IconButton>
                    </>
                  ) : null}
                </div>
              </div>

              {!comparePaymentId ? (
                <div className="op-empty">Chưa chọn payment.</div>
              ) : (
                <>
                  <PrimaryCell
                    title={shortId(comparePaymentId, 10, 6)}
                    sub={
                      comparePayment?.providerOrderCode != null
                        ? `ProviderOrderCode: ${comparePayment.providerOrderCode}`
                        : "—"
                    }
                    mono
                    onCopy={copyWithToast}
                    copyValue={comparePaymentId}
                  />
                  <div className="op-compare-meta">
                    {compareLoading.payment ? <span className="badge gray">Đang tải…</span> : null}
                    {comparePayment?.status ? (
                      <span className={getPaymentStatusClass(comparePayment.status)}>
                        {getPaymentStatusLabel(comparePayment.status)}
                      </span>
                    ) : null}
                    {comparePayment?.amount != null ? (
                      <span className="op-amount">{formatMoney(comparePayment.amount)}</span>
                    ) : null}
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* ===== Orders ===== */}
      <div className="cat-card" ref={ordersCardRef} style={{ marginTop: 14 }}>
        <div className="cat-card-header order-payment-header">
          <h2>Đơn hàng</h2>
          <div className="op-actions">
            <IconButton
              title="Tải lại orders"
              onClick={() => {
                setOrderSort((s) => ({ ...s }));
              }}
            >
              <Icon name="sliders" />
            </IconButton>
          </div>
        </div>

        <div className="cat-card-body">
          <div className="op-toolbar">
            <div className="op-filters">
              <div className="op-group" style={{ minWidth: 260 }}>
                <span>Tìm</span>
                <input
                  className="input"
                  placeholder="orderId, orderNumber, email..."
                  value={orderFilter.keyword}
                  onChange={(e) => setOrderFilter((s) => ({ ...s, keyword: e.target.value }))}
                />
              </div>

              <div className="op-group">
                <span>Sắp xếp</span>
                <select
                  className="select"
                  value={orderSort.sortBy}
                  onChange={(e) => setOrderSort((s) => ({ ...s, sortBy: e.target.value }))}
                >
                  <option value="createdAt">Ngày tạo</option>
                  <option value="finalAmount">FinalAmount</option>
                  <option value="totalAmount">TotalAmount</option>
                </select>
              </div>

              <div className="op-group">
                <span>Hướng</span>
                <select
                  className="select"
                  value={orderSort.sortDir}
                  onChange={(e) => setOrderSort((s) => ({ ...s, sortDir: e.target.value }))}
                >
                  <option value="desc">Giảm dần</option>
                  <option value="asc">Tăng dần</option>
                </select>
              </div>

              {orderLoading ? <span className="badge gray">Đang tải…</span> : null}
              <span className="badge gray">{filteredOrders.length} đơn</span>
            </div>
          </div>

          <div style={{ overflowX: "auto", marginTop: 10 }}>
            <table className="table op-table">
              <thead>
                <tr>
                  <th>
                    <button
                      className="table-sort-header"
                      onClick={() => setOrderSort((s) => toggleSortState(s, "createdAt"))}
                    >
                      Order{renderSortIndicator(orderSort, "createdAt")}
                    </button>
                  </th>
                  <th>Khách</th>
                  <th className="text-right">Final</th>
                  <th>Trạng thái</th>
                  <th>Ngày tạo</th>
                  <th className="op-th-actions"></th>
                </tr>
              </thead>

              <tbody>
                {pagedOrders.map((o) => (
                  <tr key={o.orderId} className="op-row-click">
                    <td>
                      <PrimaryCell
                        title={o.orderNumber || shortId(o.orderId)}
                        sub={shortId(o.orderId, 10, 6)}
                        mono
                        onCopy={copyWithToast}
                        copyValue={o.orderId}
                      />
                    </td>

                    <td>
                      <div className="op-cell-main">
                        <div className="op-cell-title">{o.userName || "—"}</div>
                        <div className="op-cell-sub">{o.userEmail || o.email || "—"}</div>
                      </div>
                    </td>

                    <td className="text-right text-mono">{formatMoney(o.finalAmount)}</td>

                    <td>
                      <span className="badge gray">{o.status || "—"}</span>
                    </td>

                    <td>{formatVnDateTime(o.createdAt)}</td>

                    <td className="op-td-actions">
                      <div className="op-actions">
                        <IconButton title="Xem chi tiết" onClick={() => openOrderDetail(o.orderId)}>
                          <Icon name="eye" />
                        </IconButton>

                        <IconButton
                          title="Ghim & lọc payments theo order"
                          variant="primary"
                          onClick={() => {
                            setCompareOrderId(o.orderId);
                            quickFilterPaymentsByOrder(o.orderId);
                          }}
                        >
                          <Icon name="compare" />
                        </IconButton>

                        <IconButton
                          title="Lọc payments theo order"
                          onClick={() => quickFilterPaymentsByOrder(o.orderId)}
                        >
                          <Icon name="filter" />
                        </IconButton>
                      </div>
                    </td>
                  </tr>
                ))}

                {pagedOrders.length === 0 && (
                  <tr>
                    <td colSpan={6} className="muted">
                      Không có đơn hàng.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="pager" style={{ marginTop: 10, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <div className="muted">
              Trang {orderPage}/{orderTotalPages}
            </div>
            <div style={{ display: "flex", gap: 8 }}>
              <button className="btn ghost" disabled={orderPage <= 1} onClick={() => setOrderPage((p) => Math.max(1, p - 1))}>
                Trước
              </button>
              <button
                className="btn ghost"
                disabled={orderPage >= orderTotalPages}
                onClick={() => setOrderPage((p) => Math.min(orderTotalPages, p + 1))}
              >
                Tiếp
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* ===== Payments ===== */}
      <div className="cat-card" ref={paymentsCardRef} style={{ marginTop: 14 }}>
        <div className="cat-card-header order-payment-header">
          <h2>Thanh toán</h2>
          <div className="op-actions">
            <IconButton
              title={showPaymentMoreFilters ? "Ẩn lọc nâng cao" : "Hiện lọc nâng cao"}
              onClick={() => setShowPaymentMoreFilters((s) => !s)}
            >
              <Icon name="sliders" />
            </IconButton>
          </div>
        </div>

        <div className="cat-card-body">
          <div className="op-toolbar">
            <div className="op-filters">
              <div className="op-group" style={{ minWidth: 220 }}>
                <span>Tìm (q)</span>
                <input
                  className="input"
                  placeholder="paymentId, providerOrderCode..."
                  value={paymentFilter.keyword}
                  onChange={(e) => setPaymentFilter((s) => ({ ...s, keyword: e.target.value }))}
                />
              </div>

              <div className="op-group" style={{ minWidth: 200 }}>
                <span>Email</span>
                <input
                  className="input"
                  placeholder="lọc theo email"
                  value={paymentFilter.email}
                  onChange={(e) => setPaymentFilter((s) => ({ ...s, email: e.target.value }))}
                />
              </div>

              <div className="op-group">
                <span>Trạng thái</span>
                <select
                  className="select"
                  value={paymentFilter.status}
                  onChange={(e) => setPaymentFilter((s) => ({ ...s, status: e.target.value }))}
                >
                  {PAYMENT_STATUS_FILTER_OPTIONS.map((x) => (
                    <option key={x.value} value={x.value}>
                      {x.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="op-group">
                <span>Provider</span>
                <select
                  className="select"
                  value={paymentFilter.provider}
                  onChange={(e) => setPaymentFilter((s) => ({ ...s, provider: e.target.value }))}
                >
                  {PAYMENT_PROVIDER_OPTIONS.map((x) => (
                    <option key={x} value={x}>
                      {x || "Tất cả"}
                    </option>
                  ))}
                </select>
              </div>

              {paymentLoading ? <span className="badge gray">Đang tải…</span> : null}
              <span className="badge gray">{payments.length} payments</span>
            </div>

            {showPaymentMoreFilters && (
              <div className="op-filters op-filters-advanced">
                <div className="op-group">
                  <span>TargetType</span>
                  <select
                    className="select"
                    value={paymentFilter.targetType}
                    onChange={(e) => setPaymentFilter((s) => ({ ...s, targetType: e.target.value }))}
                  >
                    {PAYMENT_TARGET_TYPE_OPTIONS.map((x) => (
                      <option key={x.value} value={x.value}>
                        {x.label}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="op-group" style={{ minWidth: 260 }}>
                  <span>TargetId</span>
                  <input
                    className="input"
                    placeholder="orderId / supportPlanId..."
                    value={paymentFilter.targetId}
                    onChange={(e) => setPaymentFilter((s) => ({ ...s, targetId: e.target.value }))}
                  />
                </div>

                <div className="op-group">
                  <span>Sắp xếp</span>
                  <select
                    className="select"
                    value={paymentSort.sortBy}
                    onChange={(e) => setPaymentSort((s) => ({ ...s, sortBy: e.target.value }))}
                  >
                    <option value="createdAt">Ngày tạo</option>
                    <option value="amount">Số tiền</option>
                  </select>
                </div>

                <div className="op-group">
                  <span>Hướng</span>
                  <select
                    className="select"
                    value={paymentSort.sortDir}
                    onChange={(e) => setPaymentSort((s) => ({ ...s, sortDir: e.target.value }))}
                  >
                    <option value="desc">Giảm dần</option>
                    <option value="asc">Tăng dần</option>
                  </select>
                </div>
              </div>
            )}
          </div>

          <div style={{ overflowX: "auto", marginTop: 10 }}>
            <table className="table op-table">
              <thead>
                <tr>
                  <th>Payment</th>
                  <th>Target</th>
                  <th className="text-right">Số tiền</th>
                  <th>Trạng thái</th>
                  <th>Ngày tạo</th>
                  <th className="op-th-actions"></th>
                </tr>
              </thead>

              <tbody>
                {pagedPayments.map((p) => {
                  const isOrderTarget = (p.targetType || "").toLowerCase() === "order";
                  const orderId = isOrderTarget ? (p.targetId || null) : null;

                  return (
                    <tr key={p.paymentId} className="op-row-click">
                      <td>
                        <PrimaryCell
                          title={shortId(p.paymentId, 10, 6)}
                          sub={p.providerOrderCode != null ? `#${p.providerOrderCode}` : (p.paymentLinkId ? shortId(p.paymentLinkId, 10, 6) : "—")}
                          mono
                          onCopy={copyWithToast}
                          copyValue={p.paymentId}
                        />
                      </td>

                      <td>
                        <div className="op-target-cell">
                          <div className="op-target-sub">
                            <span className={getTargetTypeChipClass(p.targetType)}>
                              {getTargetTypeLabel(p.targetType)}
                            </span>
                            <span className="op-submono" title={p.targetId || ""}>
                              {p.targetId ? shortId(p.targetId, 10, 6) : "—"}
                            </span>

                            {p.email ? (
                              <span className="op-subtext" title={p.email}>
                                {p.email}
                              </span>
                            ) : null}
                          </div>
                        </div>
                      </td>

                      <td className="text-right text-mono">{formatMoney(p.amount)}</td>

                      <td>
                        <span className={getPaymentStatusClass(p.status)}>
                          {getPaymentStatusLabel(p.status)}
                        </span>
                        {p.isExpired ? (
                          <span className="badge gray" style={{ marginLeft: 8 }}>
                            Expired
                          </span>
                        ) : null}
                        {p.isLatestAttemptForTarget ? (
                          <span className="badge gray" style={{ marginLeft: 8 }}>
                            Latest
                          </span>
                        ) : null}
                      </td>

                      <td>{formatVnDateTime(p.createdAt)}</td>

                      <td className="op-td-actions">
                        <div className="op-actions">
                          <IconButton title="Xem chi tiết" onClick={() => openPaymentDetail(p.paymentId)}>
                            <Icon name="eye" />
                          </IconButton>

                          <IconButton
                            title="Ghim payment"
                            variant="primary"
                            onClick={() => setComparePaymentId(p.paymentId)}
                          >
                            <Icon name="compare" />
                          </IconButton>

                          {orderId ? (
                            <>
                              <IconButton title="Mở order" onClick={() => openOrderDetail(orderId)}>
                                <Icon name="receipt" />
                              </IconButton>

                              <IconButton title="Lọc payments theo order" onClick={() => quickFilterPaymentsByOrder(orderId)}>
                                <Icon name="filter" />
                              </IconButton>
                            </>
                          ) : null}
                        </div>
                      </td>
                    </tr>
                  );
                })}

                {pagedPayments.length === 0 && (
                  <tr>
                    <td colSpan={6} className="muted">
                      Không có thanh toán.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="pager" style={{ marginTop: 10, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <div className="muted">
              Trang {paymentPage}/{paymentTotalPages}
            </div>
            <div style={{ display: "flex", gap: 8 }}>
              <button className="btn ghost" disabled={paymentPage <= 1} onClick={() => setPaymentPage((p) => Math.max(1, p - 1))}>
                Trước
              </button>
              <button
                className="btn ghost"
                disabled={paymentPage >= paymentTotalPages}
                onClick={() => setPaymentPage((p) => Math.min(paymentTotalPages, p + 1))}
              >
                Tiếp
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Modals */}
      <OrderDetailModal
        open={orderModal.open}
        orderId={orderModal.id}
        onClose={closeOrderDetail}
        addToast={addToast}
        onOpenPayment={openPaymentDetail}
        onQuickFilterPayments={quickFilterPaymentsByOrder}
      />

      <PaymentDetailModal
        open={paymentModal.open}
        paymentId={paymentModal.id}
        onClose={closePaymentDetail}
        addToast={addToast}
        onOpenOrder={openOrderDetail}
        onQuickFilterPayments={quickFilterPaymentsByOrder}
        onOpenPayment={openPaymentDetail}
      />
    </div>
  );
}
