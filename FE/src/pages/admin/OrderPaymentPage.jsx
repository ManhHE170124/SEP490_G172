// src/pages/admin/OrderPaymentPage.jsx
import React from "react";
import { orderApi } from "../../services/orderApi";
import { paymentApi } from "../../services/paymentApi";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./OrderPaymentPage.css";

/* ===== Helpers nhỏ ===== */

const EllipsisCell = ({ children, title, maxWidth = 260, mono = false }) => (
  <div
    className={mono ? "mono" : undefined}
    title={title ?? (typeof children === "string" ? children : "")}
    style={{
      maxWidth,
      whiteSpace: "nowrap",
      overflow: "hidden",
      textOverflow: "ellipsis",
    }}
  >
    {children}
  </div>
);

const formatVnDateTime = (value) => {
  if (!value) return "";
  const d = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleString("vi-VN");
};

/**
 * Các trạng thái ĐƠN HÀNG đang dùng trong hệ thống:
 *   - Pending   : chờ xử lý
 *   - Paid      : đã thanh toán
 *   - Failed    : thất bại
 *   - Cancelled : đã hủy
 * -> giữ nguyên value tiếng Anh để khớp API, chỉ đổi label sang tiếng Việt.
 */
const ORDER_STATUS_OPTIONS = [
  { value: "Pending", label: "Chờ xử lý" },
  { value: "Paid", label: "Đã thanh toán" },
  { value: "Failed", label: "Thất bại" },
  { value: "Cancelled", label: "Đã hủy" },
];

/**
 * Trạng thái PAYMENT (transaction) – bám theo PayOS / hệ thống:
 *   - Pending    : chờ thanh toán
 *   - Paid       : đã thanh toán
 *   - Success    : thành công
 *   - Completed  : hoàn tất
 *   - Cancelled  : đã hủy
 *   - Failed     : thất bại
 *   - Refunded   : hoàn tiền
 */
const PAYMENT_STATUS_OPTIONS = [
  "Pending",
  "Paid",
  "Success",
  "Completed",
  "Cancelled",
  "Failed",
  "Refunded",
];

const PAYMENT_PROVIDER_OPTIONS = ["", "PayOS"];

/* Map status -> class cho badge */
const getOrderStatusClass = (status) => {
  const s = (status || "").toLowerCase();
  if (s === "pending") return "status-pill order-pending";
  if (s === "paid") return "status-pill order-paid";
  if (s === "cancelled" || s === "failed")
    return "status-pill order-cancelled";
  return "status-pill order-unknown";
};

const getPaymentStatusClass = (status) => {
  const s = (status || "").toLowerCase();
  if (s === "pending" || s === "unpaid" || s === "partial")
    return "status-pill payment-pending";
  if (s === "paid" || s === "success" || s === "completed")
    return "status-pill payment-paid";
  if (s === "cancelled" || s === "failed")
    return "status-pill payment-cancelled";
  if (s === "refunded") return "status-pill payment-refunded";
  return "status-pill payment-unknown";
};

/* Map status -> label tiếng Việt */

const getOrderStatusLabel = (status) => {
  const s = (status || "").toLowerCase();
  switch (s) {
    case "pending":
      return "Chờ xử lý";
    case "paid":
      return "Đã thanh toán";
    case "failed":
      return "Thất bại";
    case "cancelled":
      return "Đã hủy";
    default:
      return status || "Không xác định";
  }
};

// Trạng thái tổng hợp thanh toán của ĐƠN HÀNG (Unpaid, Partial, Paid, Refunded)
const getOrderPaymentStatusLabel = (status) => {
  const s = (status || "").toLowerCase();
  switch (s) {
    case "unpaid":
      return "Chưa thanh toán";
    case "partial":
      return "Thanh toán một phần";
    case "paid":
      return "Đã thanh toán";
    case "refunded":
      return "Đã hoàn tiền";
    default:
      return getPaymentStatusLabel(status);
  }
};

const getPaymentStatusLabel = (status) => {
  const s = (status || "").toLowerCase();
  switch (s) {
    case "pending":
      return "Chờ thanh toán";
    case "paid":
    case "success":
    case "completed":
      return "Đã thanh toán";
    case "cancelled":
      return "Đã hủy";
    case "failed":
      return "Thất bại";
    case "refunded":
      return "Đã hoàn tiền";
    default:
      return status || "Không xác định";
  }
};

// Dùng riêng cho filter trạng thái payment, tránh trùng label
const PAYMENT_STATUS_FILTER_OPTIONS = [
  { value: "Pending", label: getPaymentStatusLabel("Pending") },
  { value: "Paid", label: getPaymentStatusLabel("Paid") },
  { value: "Cancelled", label: getPaymentStatusLabel("Cancelled") },
  { value: "Failed", label: getPaymentStatusLabel("Failed") },
  { value: "Refunded", label: getPaymentStatusLabel("Refunded") },
];

/* ===== Modal: Order Detail + Update Status + Cancel ===== */

function OrderDetailModal({
  open,
  orderId,
  onClose,
  onUpdated,
  addToast,
  openConfirm,
}) {
  const [loading, setLoading] = React.useState(false);
  const [saving, setSaving] = React.useState(false);
  const [order, setOrder] = React.useState(null);
  const [status, setStatus] = React.useState("");

  React.useEffect(() => {
    if (!open || !orderId) return;

    setLoading(true);
    orderApi
      .get(orderId)
      .then((res) => {
        const data = res?.data ?? res;
        setOrder(data);
        setStatus(data?.status || "");
      })
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

  // Rule mới: chỉ được chỉnh tay khi đang Pending / Failed
  const currentStatus = (order?.status || "").toLowerCase();
  const canManuallyChangeStatus =
    currentStatus === "pending" || currentStatus === "failed";

  // Chỉ cho chọn: trạng thái hiện tại + Paid + Cancelled (khi đang Pending/Failed)
  // Nếu đã Paid/Cancelled/... thì dropdown chỉ hiển thị trạng thái hiện tại (read-only)
  const orderSelectOptions = ORDER_STATUS_OPTIONS.filter((opt) => {
    const v = opt.value.toLowerCase();
    if (!order) return false;

    if (!canManuallyChangeStatus) {
      return v === currentStatus;
    }

    // cho giữ nguyên hoặc chuyển sang Paid/Cancelled
    if (v === currentStatus) return true;
    return v === "paid" || v === "cancelled";
  });

  if (!open) return null;

  const handleSaveStatus = async () => {
    if (!order || !status) return;
    if (status === order.status) {
      addToast?.("info", "Trạng thái không thay đổi.", "Không có thay đổi");
      return;
    }

    const current = (order.status || "").toLowerCase();
    const next = (status || "").toLowerCase();
    const isFromPendingOrFailed =
      current === "pending" || current === "failed";
    const isTargetAllowed = next === "paid" || next === "cancelled";

    if (!isFromPendingOrFailed) {
      addToast?.(
        "warning",
        "Chỉ đơn đang ở trạng thái Chờ xử lý hoặc Thất bại mới được đổi trạng thái tay.",
        "Không được phép"
      );
      return;
    }

    if (!isTargetAllowed) {
      addToast?.(
        "warning",
        "Bạn chỉ được chuyển đơn từ Chờ xử lý/Thất bại sang Đã thanh toán hoặc Đã hủy.",
        "Không được phép"
      );
      return;
    }

    setSaving(true);
    try {
      await orderApi.update(order.orderId, { status });
      addToast?.("success", "Đã cập nhật trạng thái đơn hàng.", "Thành công");
      onUpdated?.();
      onClose?.();
    } catch (err) {
      console.error(err);
      addToast?.(
        "error",
        err?.response?.data?.message || "Cập nhật trạng thái thất bại.",
        "Lỗi"
      );
    } finally {
      setSaving(false);
    }
  };

  const handleCancelOrder = () => {
    if (!order) return;
    openConfirm?.({
      title: "Hủy đơn hàng?",
      message:
        "Hủy đơn sẽ hoàn kho và hủy các thanh toán đang ở trạng thái chờ. Bạn có chắc chắn muốn tiếp tục?",
      onConfirm: async () => {
        try {
          await orderApi.cancel(order.orderId);
          addToast?.("success", "Đã hủy đơn hàng.", "Thành công");
          onUpdated?.();
          onClose?.();
        } catch (err) {
          console.error(err);
          addToast?.(
            "error",
            err?.response?.data?.message || "Hủy đơn hàng thất bại.",
            "Lỗi"
          );
        }
      },
    });
  };

  const effectiveFinal =
    order?.finalAmount ?? (order ? order.totalAmount - order.discountAmount : 0);

  const canCancel =
    order && (order.status || "").toLowerCase() === "pending";

  return (
    <div className="cat-modal-backdrop">
      <div className="cat-modal-card">
        <div className="cat-modal-header">
          <h3>Chi tiết đơn hàng</h3>
          {order && (
            <span className={getOrderStatusClass(order.status)}>
              {getOrderStatusLabel(order.status)}
            </span>
          )}
        </div>

        <div className="cat-modal-body">
          {loading && <span className="badge gray">Đang tải…</span>}
          {!loading && order && (
            <>
              {/* Meta info */}
              <div className="order-meta-grid">
                <div className="detail-section-box">
                  <div className="detail-label">Mã đơn hàng</div>
                  <div className="detail-value mono">
                    {order.orderId || ""}
                  </div>

                  <div
                    className="detail-label"
                    style={{ marginTop: 6 }}
                  >
                    Ngày tạo
                  </div>
                  <div className="detail-value">
                    {formatVnDateTime(order.createdAt)}
                  </div>
                </div>

                <div className="detail-section-box">
                  <div className="detail-label">Khách hàng</div>
                  <div className="detail-value">
                    {order.userName || order.userEmail || order.email || "—"}
                  </div>

                  <div
                    className="detail-label"
                    style={{ marginTop: 6 }}
                  >
                    Email đơn hàng
                  </div>
                  <div className="detail-value">
                    {order.email || order.userEmail || "—"}
                  </div>
                </div>
              </div>

              {/* Tổng tiền */}
              <div className="detail-section-title">
                Tổng tiền & chiết khấu
              </div>
              <div className="detail-section-box">
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(3, minmax(0,1fr))",
                    gap: 8,
                  }}
                >
                  <div>
                    <div className="detail-label">Tổng tiền</div>
                    <div className="detail-value text-mono">
                      {order.totalAmount?.toLocaleString("vi-VN")} đ
                    </div>
                  </div>
                  <div>
                    <div className="detail-label">Giảm giá</div>
                    <div className="detail-value text-mono">
                      {order.discountAmount?.toLocaleString("vi-VN")} đ
                    </div>
                  </div>
                  <div>
                    <div className="detail-label">Thành tiền</div>
                    <div className="detail-value text-mono">
                      {effectiveFinal?.toLocaleString("vi-VN")} đ
                    </div>
                  </div>
                </div>
              </div>

              {/* Status + PaymentStatus */}
              <div className="detail-section-title">Trạng thái</div>
              <div className="detail-section-box">
                <div className="order-meta-grid">
                  <div>
                    <div className="detail-label">Trạng thái đơn</div>
                    <select
                      value={status}
                      onChange={(e) => setStatus(e.target.value)}
                      style={{ marginTop: 4 }}
                      disabled={!canManuallyChangeStatus}
                    >
                      {orderSelectOptions.map((s) => (
                        <option key={s.value} value={s.value}>
                          {s.label}
                        </option>
                      ))}
                    </select>
                    {!canManuallyChangeStatus && (
                      <div
                        style={{
                          marginTop: 4,
                          fontSize: 11,
                          color: "var(--muted)",
                        }}
                      >
                        Đơn đã ở trạng thái cuối (Đã thanh toán / Đã hủy),
                        không thể chỉnh tay.
                      </div>
                    )}
                  </div>
                  <div>
                    <div className="detail-label">
                      Trạng thái thanh toán (tổng)
                    </div>
                    <div
                      className={getPaymentStatusClass(
                        order.paymentStatus
                      )}
                      style={{ marginTop: 6 }}
                    >
                      {order.paymentStatus
                        ? getOrderPaymentStatusLabel(order.paymentStatus)
                        : "Chưa thanh toán"}
                    </div>
                  </div>
                </div>
              </div>

              {/* Items */}
              <div className="detail-section-title">Sản phẩm</div>
              <div className="detail-section-box">
                {order.orderDetails && order.orderDetails.length > 0 ? (
                  <table className="order-items-table">
                    <thead>
                      <tr>
                        <th>Sản phẩm</th>
                        <th>Gói</th>
                        <th>SL</th>
                        <th>Đơn giá</th>
                        <th>Thành tiền</th>
                      </tr>
                    </thead>
                    <tbody>
                      {order.orderDetails.map((it) => (
                        <tr key={it.orderDetailId}>
                          <td>
                            <EllipsisCell maxWidth={260}>
                              {it.productName || "—"}
                            </EllipsisCell>
                          </td>
                          <td>
                            <EllipsisCell maxWidth={220}>
                              {it.variantTitle || "—"}
                            </EllipsisCell>
                          </td>
                          <td className="text-right text-mono">
                            {it.quantity}
                          </td>
                          <td className="text-right text-mono">
                            {it.unitPrice?.toLocaleString("vi-VN")} đ
                          </td>
                          <td className="text-right text-mono">
                            {it.subTotal?.toLocaleString("vi-VN")} đ
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                ) : (
                  <div className="detail-label">
                    Không có sản phẩm trong đơn.
                  </div>
                )}
              </div>
            </>
          )}
        </div>

        <div className="cat-modal-footer">
          {canCancel && (
            <button
              type="button"
              className="btn"
              style={{
                borderColor: "var(--danger)",
                color: "var(--danger)",
              }}
              onClick={handleCancelOrder}
              disabled={saving}
            >
              Hủy đơn
            </button>
          )}
          <button
            type="button"
            className="btn ghost"
            onClick={onClose}
            disabled={saving}
          >
            Đóng
          </button>
          <button
            type="button"
            className="btn primary"
            onClick={handleSaveStatus}
            disabled={saving || !order || !canManuallyChangeStatus}
          >
            {saving ? "Đang lưu…" : "Lưu trạng thái"}
          </button>
        </div>
      </div>
    </div>
  );
}

/* ===== Modal: Payment Detail + (U hạn chế) UpdatePaymentStatus ===== */

function PaymentDetailModal({
  open,
  paymentId,
  onClose,
  onUpdated,
  addToast,
  openConfirm,
}) {
  const [loading, setLoading] = React.useState(false);
  const [saving, setSaving] = React.useState(false);
  const [payment, setPayment] = React.useState(null);
  const [status, setStatus] = React.useState("");

  React.useEffect(() => {
    if (!open || !paymentId) return;

    setLoading(true);
    paymentApi
      .get(paymentId)
      .then((res) => {
        const data = res?.data ?? res;
        setPayment(data);
        setStatus(data?.status || "");
      })
      .catch((err) => {
        console.error(err);
        addToast?.(
          "error",
          err?.response?.data?.message ||
            "Không tải được chi tiết thanh toán.",
          "Lỗi"
        );
      })
      .finally(() => setLoading(false));
  }, [open, paymentId, addToast]);

  // Rule mới: chỉ được chỉnh tay khi payment đang Pending / Failed
  const currentStatus = (payment?.status || "").toLowerCase();
  const canManuallyChangeStatus =
    currentStatus === "pending" || currentStatus === "failed";

  const paymentSelectOptions = PAYMENT_STATUS_OPTIONS.filter((s) => {
    const v = s.toLowerCase();
    if (!payment) return false;

    if (!canManuallyChangeStatus) {
      return v === currentStatus;
    }

    if (v === currentStatus) return true;
    return v === "paid" || v === "cancelled";
  });

  if (!open) return null;

  const handleSaveStatus = () => {
    if (!payment || !status) return;
    if (status === payment.status) {
      addToast?.("info", "Trạng thái không thay đổi.", "Không có thay đổi");
      return;
    }

    const current = (payment.status || "").toLowerCase();
    const next = (status || "").toLowerCase();
    const isFromPendingOrFailed =
      current === "pending" || current === "failed";
    const isTargetAllowed = next === "paid" || next === "cancelled";

    if (!isFromPendingOrFailed) {
      addToast?.(
        "warning",
        "Chỉ các thanh toán đang ở trạng thái Chờ thanh toán hoặc Thất bại mới được chỉnh tay.",
        "Không được phép"
      );
      return;
    }

    if (!isTargetAllowed) {
      addToast?.(
        "warning",
        "Bạn chỉ được chuyển thanh toán từ Chờ thanh toán/Thất bại sang Đã thanh toán hoặc Đã hủy.",
        "Không được phép"
      );
      return;
    }

    // U (hạn chế): confirm rất rõ ràng trước khi gọi updateStatus
    openConfirm?.({
      title: "Cập nhật trạng thái thanh toán?",
      message:
        "Thao tác này chỉ nên dùng khi đối chiếu với log từ cổng thanh toán. Việc chỉnh tay có thể làm trạng thái đơn không khớp.\n\nBạn chắc chắn muốn cập nhật?",
      onConfirm: async () => {
        setSaving(true);
        try {
          await paymentApi.updateStatus(payment.paymentId, status);
          addToast?.(
            "success",
            "Đã cập nhật trạng thái thanh toán.",
            "Thành công"
          );
          onUpdated?.();
          onClose?.();
        } catch (err) {
          console.error(err);
          addToast?.(
            "error",
            err?.response?.data?.message ||
              "Cập nhật trạng thái thanh toán thất bại.",
            "Lỗi"
          );
        } finally {
          setSaving(false);
        }
      },
    });
  };

  const effectiveFinal =
    payment?.orderFinalAmount ?? payment?.orderTotalAmount ?? 0;

  return (
    <div className="cat-modal-backdrop">
      <div className="cat-modal-card">
        <div className="cat-modal-header">
          <h3>Chi tiết thanh toán</h3>
          {payment && (
            <span className={getPaymentStatusClass(payment.status)}>
              {getPaymentStatusLabel(payment.status)}
            </span>
          )}
        </div>

        <div className="cat-modal-body">
          {loading && <span className="badge gray">Đang tải…</span>}
          {!loading && payment && (
            <>
              <div className="payment-meta-grid">
                <div className="detail-section-box">
                  <div className="detail-label">Mã thanh toán</div>
                  <div className="detail-value mono">
                    {payment.paymentId}
                  </div>

                  <div
                    className="detail-label"
                    style={{ marginTop: 6 }}
                  >
                    Ngày tạo
                  </div>
                  <div className="detail-value">
                    {formatVnDateTime(payment.createdAt)}
                  </div>
                </div>

                <div className="detail-section-box">
                  <div className="detail-label">Email thanh toán</div>
                  <div className="detail-value">
                    {payment.email || "—"}
                  </div>

                  <div
                    className="detail-label"
                    style={{ marginTop: 6 }}
                  >
                    Loại giao dịch
                  </div>
                  <div className="detail-value">
                    {payment.transactionType || "—"}
                  </div>
                </div>
              </div>

              {/* Thông tin tiền */}
              <div className="detail-section-title">Số tiền</div>
              <div className="detail-section-box">
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(3, minmax(0,1fr))",
                    gap: 8,
                  }}
                >
                  <div>
                    <div className="detail-label">
                      Số tiền thanh toán
                    </div>
                    <div className="detail-value text-mono">
                      {payment.amount?.toLocaleString("vi-VN")} đ
                    </div>
                  </div>
                  <div>
                    <div className="detail-label">Tổng đơn</div>
                    <div className="detail-value text-mono">
                      {payment.orderTotalAmount?.toLocaleString(
                        "vi-VN"
                      )}{" "}
                      đ
                    </div>
                  </div>
                  <div>
                    <div className="detail-label">Thành tiền (đơn)</div>
                    <div className="detail-value text-mono">
                      {effectiveFinal?.toLocaleString("vi-VN")} đ
                    </div>
                  </div>
                </div>
              </div>

              {/* Provider + status chỉnh tay */}
              <div className="detail-section-title">
                Cổng thanh toán & trạng thái
              </div>
              <div className="detail-section-box payment-meta-grid">
                <div>
                  <div className="detail-label">Cổng thanh toán</div>
                  <div className="detail-value">
                    {payment.provider || "PayOS"}
                    {payment.providerOrderCode != null && (
                      <span className="mono">
                        {" "}
                        · Mã giao dịch: {payment.providerOrderCode}
                      </span>
                    )}
                  </div>
                </div>
                <div>
                  <div className="detail-label">
                    Trạng thái (chỉnh tay – hạn chế)
                  </div>
                  <select
                    value={status}
                    onChange={(e) => setStatus(e.target.value)}
                    style={{ marginTop: 4 }}
                    disabled={!canManuallyChangeStatus}
                  >
                    {paymentSelectOptions.map((s) => (
                      <option key={s} value={s}>
                        {getPaymentStatusLabel(s)}
                      </option>
                    ))}
                  </select>
                  <div
                    style={{
                      fontSize: 11,
                      color: "var(--muted)",
                      marginTop: 4,
                    }}
                  >
                    {canManuallyChangeStatus
                      ? "Chỉ dùng khi đã đối chiếu với PayOS / log hệ thống. Chỉ được chuyển từ Chờ thanh toán / Thất bại sang Đã thanh toán hoặc Đã hủy."
                      : "Giao dịch đã ở trạng thái cuối, không cho phép chỉnh tay."}
                  </div>
                </div>
              </div>
            </>
          )}
        </div>

        <div className="cat-modal-footer">
          <button
            type="button"
            className="btn ghost"
            onClick={onClose}
            disabled={saving}
          >
            Đóng
          </button>
          <button
            type="button"
            className="btn primary"
            onClick={handleSaveStatus}
            disabled={saving || !payment || !canManuallyChangeStatus}
          >
            {saving ? "Đang lưu…" : "Lưu trạng thái"}
          </button>
        </div>
      </div>
    </div>
  );
}

/* ===== MAIN PAGE: List Order + Payment (dùng chung) ===== */

export default function OrderPaymentPage() {
  /* Toast + ConfirmDialog (reuse pattern CategoryPage) */
  const [toasts, setToasts] = React.useState([]);
  const [confirmDialog, setConfirmDialog] = React.useState(null);
  const toastIdRef = React.useRef(1);

  const removeToast = (id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  const addToast = (type, message, title) => {
    const id = toastIdRef.current++;
    setToasts((prev) => [
      ...prev,
      { id, type, message, title: title || undefined },
    ]);
    setTimeout(() => removeToast(id), 5000);
    return id;
  };

  const openConfirm = ({ title, message, onConfirm }) => {
    setConfirmDialog({
      title,
      message,
      onConfirm: async () => {
        setConfirmDialog(null);
        await onConfirm?.();
      },
      onCancel: () => setConfirmDialog(null),
    });
  };

  /* ===== Orders state ===== */
  const [orders, setOrders] = React.useState([]);
  const [orderLoading, setOrderLoading] = React.useState(false);
  const [orderFilter, setOrderFilter] = React.useState({
    keyword: "",
    status: "",
  });
  const [orderPage, setOrderPage] = React.useState(1);
  const [orderPageSize] = React.useState(10);

  const [orderModal, setOrderModal] = React.useState({
    open: false,
    id: null,
  });

  const loadOrders = React.useCallback(() => {
    setOrderLoading(true);
    orderApi
      .list()
      .then((res) => {
        const data = res?.data ?? res ?? [];
        setOrders(Array.isArray(data) ? data : []);
      })
      .catch((err) => {
        console.error(err);
        addToast(
          "error",
          err?.response?.data?.message || "Không tải được danh sách đơn hàng.",
          "Lỗi"
        );
      })
      .finally(() => setOrderLoading(false));
  }, []);

  React.useEffect(() => {
    loadOrders();
  }, [loadOrders]);

  React.useEffect(() => {
    setOrderPage(1);
  }, [orderFilter.keyword, orderFilter.status]);

  const filteredOrders = React.useMemo(() => {
    let list = Array.isArray(orders) ? orders : [];
    const keyword = orderFilter.keyword.trim().toLowerCase();

    if (keyword) {
      list = list.filter((o) => {
        const haystack = [
          o.orderId,
          o.email,
          o.userEmail,
          o.userName,
        ]
          .filter(Boolean)
          .join(" ")
          .toLowerCase();
        return haystack.includes(keyword);
      });
    }

    if (orderFilter.status) {
      list = list.filter((o) => o.status === orderFilter.status);
    }

    // Sort mới nhất đầu
    list = [...list].sort((a, b) => {
      const da = new Date(a.createdAt || 0).getTime();
      const db = new Date(b.createdAt || 0).getTime();
      return db - da;
    });

    return list;
  }, [orders, orderFilter]);

  const orderTotal = filteredOrders.length;
  const orderPageItems = filteredOrders.slice(
    (orderPage - 1) * orderPageSize,
    orderPage * orderPageSize
  );

  const openOrderDetail = (id) =>
    setOrderModal({ open: true, id: id || null });

  const closeOrderDetail = () =>
    setOrderModal((m) => ({ ...m, open: false }));

  const handleOrderCancelInline = (order) => {
    if (!order) return;
    openConfirm({
      title: "Hủy đơn hàng?",
      message:
        "Hủy đơn sẽ hoàn kho và hủy các thanh toán đang ở trạng thái chờ. Tiếp tục?",
      onConfirm: async () => {
        try {
          await orderApi.cancel(order.orderId);
          addToast("success", "Đã hủy đơn hàng.", "Thành công");
          loadOrders();
        } catch (err) {
          console.error(err);
          addToast(
            "error",
            err?.response?.data?.message || "Hủy đơn hàng thất bại.",
            "Lỗi"
          );
        }
      },
    });
  };

  /* ===== Payments state ===== */
  const [payments, setPayments] = React.useState([]);
  const [paymentLoading, setPaymentLoading] = React.useState(false);
  const [paymentFilter, setPaymentFilter] = React.useState({
    keyword: "",
    status: "",
    provider: "",
  });
  const [paymentPage, setPaymentPage] = React.useState(1);
  const [paymentPageSize] = React.useState(10);

  const [paymentModal, setPaymentModal] = React.useState({
    open: false,
    id: null,
  });

  const loadPayments = React.useCallback(() => {
    setPaymentLoading(true);
    const params = {
      transactionType: "ORDER_PAYMENT",
    };
    if (paymentFilter.status) params.status = paymentFilter.status;
    if (paymentFilter.provider) params.provider = paymentFilter.provider;
    // keyword filter thực hiện ở FE

    paymentApi
      .list(params)
      .then((res) => {
        const data = res?.data ?? res ?? [];
        setPayments(Array.isArray(data) ? data : []);
      })
      .catch((err) => {
        console.error(err);
        addToast(
          "error",
          err?.response?.data?.message ||
            "Không tải được danh sách thanh toán.",
          "Lỗi"
        );
      })
      .finally(() => setPaymentLoading(false));
  }, [paymentFilter.status, paymentFilter.provider]);

  React.useEffect(() => {
    const t = setTimeout(loadPayments, 300); // debounce nhẹ
    return () => clearTimeout(t);
  }, [loadPayments]);

  React.useEffect(() => {
    setPaymentPage(1);
  }, [paymentFilter.keyword, paymentFilter.status, paymentFilter.provider]);

  const filteredPayments = React.useMemo(() => {
    let list = Array.isArray(payments) ? payments : [];
    const keyword = paymentFilter.keyword.trim().toLowerCase();

    if (keyword) {
      list = list.filter((p) => {
        const haystack = [
          p.paymentId,
          p.email,
          p.provider,
          p.providerOrderCode,
          p.transactionType,
        ]
          .filter(Boolean)
          .join(" ")
          .toLowerCase();
        return haystack.includes(keyword);
      });
    }

    if (paymentFilter.status) {
      list = list.filter((p) => p.status === paymentFilter.status);
    }
    if (paymentFilter.provider) {
      list = list.filter((p) => p.provider === paymentFilter.provider);
    }

    // mới nhất đầu
    list = [...list].sort((a, b) => {
      const da = new Date(a.createdAt || 0).getTime();
      const db = new Date(b.createdAt || 0).getTime();
      return db - da;
    });

    return list;
  }, [payments, paymentFilter]);

  const paymentTotal = filteredPayments.length;
  const paymentPageItems = filteredPayments.slice(
    (paymentPage - 1) * paymentPageSize,
    paymentPage * paymentPageSize
  );

  const openPaymentDetail = (id) =>
    setPaymentModal({ open: true, id: id || null });
  const closePaymentDetail = () =>
    setPaymentModal((m) => ({ ...m, open: false }));

  /* ===== RENDER ===== */

  return (
    <>
      <div className="page">
        {/* ===== Card: Đơn hàng ===== */}
        <div className="card">
          <div className="order-payment-header">
            <h2>Đơn hàng</h2>
            {orderLoading && (
              <span className="badge gray">Đang tải danh sách…</span>
            )}
          </div>

          {/* Filters */}
          <div className="order-filters input-group">
            <div
              className="group"
              style={{ minWidth: 260, maxWidth: 480 }}
            >
              <span>Tìm kiếm</span>
              <input
                value={orderFilter.keyword}
                onChange={(e) =>
                  setOrderFilter((s) => ({
                    ...s,
                    keyword: e.target.value,
                  }))
                }
                placeholder="Tìm theo mã đơn, email, tên khách…"
              />
            </div>
            <div className="group" style={{ minWidth: 160 }}>
              <span>Trạng thái</span>
              <select
                value={orderFilter.status}
                onChange={(e) =>
                  setOrderFilter((s) => ({
                    ...s,
                    status: e.target.value,
                  }))
                }
              >
                <option value="">Tất cả</option>
                {ORDER_STATUS_OPTIONS.map((s) => (
                  <option key={s.value} value={s.value}>
                    {s.label}
                  </option>
                ))}
              </select>
            </div>
            <button
              className="btn"
              type="button"
              onClick={() =>
                setOrderFilter({ keyword: "", status: "" })
              }
            >
              Đặt lại
            </button>
          </div>

          {/* Table Orders */}
          <table className="table" style={{ marginTop: 10 }}>
            <thead>
              <tr>
                <th>Mã đơn</th>
                <th>Khách hàng</th>
                <th>Email</th>
                <th>Số SP</th>
                <th>Thành tiền</th>
                <th>Trạng thái</th>
                <th>Ngày tạo</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {orderPageItems.map((o) => {
                const effectiveFinal =
                  o.finalAmount ?? o.totalAmount - (o.discountAmount || 0);
                const canCancel =
                  (o.status || "").toLowerCase() === "pending";
                return (
                  <tr key={o.orderId}>
                    <td>
                      <EllipsisCell mono maxWidth={160} title={o.orderId}>
                        {o.orderId}
                      </EllipsisCell>
                    </td>
                    <td>
                      <EllipsisCell maxWidth={180}>
                        {o.userName || o.userEmail || "—"}
                      </EllipsisCell>
                    </td>
                    <td>
                      <EllipsisCell maxWidth={200} title={o.email}>
                        {o.email}
                      </EllipsisCell>
                    </td>
                    <td className="text-right text-mono">
                      {o.itemCount ?? 0}
                    </td>
                    <td className="text-right text-mono">
                      {effectiveFinal?.toLocaleString("vi-VN")} đ
                    </td>
                    <td>
                      <span className={getOrderStatusClass(o.status)}>
                        {getOrderStatusLabel(o.status)}
                      </span>
                    </td>
                    <td>{formatVnDateTime(o.createdAt)}</td>
                    <td>
                      <div className="action-buttons">
                        <button
                          className="action-btn edit-btn"
                          type="button"
                          title="Xem chi tiết / cập nhật trạng thái"
                          onClick={() => openOrderDetail(o.orderId)}
                        >
                          <svg
                            viewBox="0 0 24 24"
                            width="16"
                            height="16"
                            fill="currentColor"
                            aria-hidden="true"
                          >
                            <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                            <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                          </svg>
                        </button>
                        {canCancel && (
                          <button
                            className="action-btn delete-btn"
                            type="button"
                            title="Hủy đơn (Hoàn kho + Hủy thanh toán đang chờ)"
                            onClick={() => handleOrderCancelInline(o)}
                          >
                            <svg
                              viewBox="0 0 24 24"
                              width="16"
                              height="16"
                              fill="currentColor"
                              aria-hidden="true"
                            >
                              <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                            </svg>
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
              {orderPageItems.length === 0 && (
                <tr>
                  <td colSpan={8} style={{ padding: 12 }}>
                    Không có đơn hàng nào phù hợp bộ lọc.
                  </td>
                </tr>
              )}
            </tbody>
          </table>

          {/* Pagination Orders */}
          <div className="pager">
            <button
              disabled={orderPage <= 1}
              onClick={() =>
                setOrderPage((p) => Math.max(1, p - 1))
              }
            >
              Trước
            </button>
            <span style={{ padding: "0 8px" }}>
              Trang {orderPage} /{" "}
              {Math.max(1, Math.ceil(orderTotal / orderPageSize))}
            </span>
            <button
              disabled={orderPage * orderPageSize >= orderTotal}
              onClick={() => setOrderPage((p) => p + 1)}
            >
              Tiếp
            </button>
          </div>
        </div>

        {/* ===== Card: Thanh toán ===== */}
        <div className="card" style={{ marginTop: 14 }}>
          <div className="order-payment-header">
            <h2>Thanh toán</h2>
            {paymentLoading && (
              <span className="badge gray">
                Đang tải danh sách thanh toán…
              </span>
            )}
          </div>

          {/* Filters payment */}
          <div className="payment-filters input-group">
            <div
              className="group"
              style={{ minWidth: 260, maxWidth: 480 }}
            >
              <span>Tìm kiếm</span>
              <input
                value={paymentFilter.keyword}
                onChange={(e) =>
                  setPaymentFilter((s) => ({
                    ...s,
                    keyword: e.target.value,
                  }))
                }
                placeholder="Tìm theo mã thanh toán, email, cổng thanh toán…"
              />
            </div>
            <div className="group" style={{ minWidth: 160 }}>
              <span>Trạng thái</span>
              <select
                value={paymentFilter.status}
                onChange={(e) =>
                  setPaymentFilter((s) => ({
                    ...s,
                    status: e.target.value,
                  }))
                }
              >
                <option value="">Tất cả</option>
                {PAYMENT_STATUS_FILTER_OPTIONS.map((s) => (
                  <option key={s.value} value={s.value}>
                    {s.label}
                  </option>
                ))}
              </select>
            </div>
            <div className="group" style={{ minWidth: 160 }}>
              <span>Cổng thanh toán</span>
              <select
                value={paymentFilter.provider}
                onChange={(e) =>
                  setPaymentFilter((s) => ({
                    ...s,
                    provider: e.target.value,
                  }))
                }
              >
                {PAYMENT_PROVIDER_OPTIONS.map((p) => (
                  <option key={p || "all"} value={p}>
                    {p || "Tất cả"}
                  </option>
                ))}
              </select>
            </div>
            <button
              className="btn"
              type="button"
              onClick={() =>
                setPaymentFilter({
                  keyword: "",
                  status: "",
                  provider: "",
                })
              }
            >
              Đặt lại
            </button>
          </div>

          {/* Table payments */}
          <table className="table" style={{ marginTop: 10 }}>
            <thead>
              <tr>
                <th>Mã thanh toán</th>
                <th>Cổng thanh toán</th>
                <th>Số tiền</th>
                <th>Trạng thái</th>
                <th>Ngày tạo</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {paymentPageItems.map((p) => (
                <tr key={p.paymentId}>
                  <td>
                    <EllipsisCell mono maxWidth={160} title={p.paymentId}>
                      {p.paymentId}
                    </EllipsisCell>
                  </td>
                  <td>
                    <EllipsisCell maxWidth={180}>
                      {p.provider || "PayOS"}
                    </EllipsisCell>
                  </td>
                  <td className="text-right text-mono">
                    {p.amount?.toLocaleString("vi-VN")} đ
                  </td>
                  <td>
                    <span className={getPaymentStatusClass(p.status)}>
                      {getPaymentStatusLabel(p.status)}
                    </span>
                  </td>
                  <td>{formatVnDateTime(p.createdAt)}</td>
                  <td>
                    <div className="action-buttons">
                      <button
                        className="action-btn edit-btn"
                        type="button"
                        title="Xem chi tiết / đối chiếu"
                        onClick={() => openPaymentDetail(p.paymentId)}
                      >
                        <svg
                          viewBox="0 0 24 24"
                          width="16"
                          height="16"
                          fill="currentColor"
                          aria-hidden="true"
                        >
                          <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                          <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                        </svg>
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {paymentPageItems.length === 0 && (
                <tr>
                  <td colSpan={6} style={{ padding: 12 }}>
                    Không có thanh toán nào phù hợp bộ lọc.
                  </td>
                </tr>
              )}
            </tbody>
          </table>

          {/* Pagination Payments */}
          <div className="pager">
            <button
              disabled={paymentPage <= 1}
              onClick={() =>
                setPaymentPage((p) => Math.max(1, p - 1))
              }
            >
              Trước
            </button>
            <span style={{ padding: "0 8px" }}>
              Trang {paymentPage} /{" "}
              {Math.max(
                1,
                Math.ceil(paymentTotal / paymentPageSize)
              )}
            </span>
            <button
              disabled={paymentPage * paymentPageSize >= paymentTotal}
              onClick={() => setPaymentPage((p) => p + 1)}
            >
              Tiếp
            </button>
          </div>
        </div>
      </div>

      {/* Modals */}
      <OrderDetailModal
        open={orderModal.open}
        orderId={orderModal.id}
        onClose={closeOrderDetail}
        onUpdated={() => loadOrders()}
        addToast={addToast}
        openConfirm={openConfirm}
      />

      <PaymentDetailModal
        open={paymentModal.open}
        paymentId={paymentModal.id}
        onClose={closePaymentDetail}
        onUpdated={() => loadPayments()}
        addToast={addToast}
        openConfirm={openConfirm}
      />

      {/* Toast + ConfirmDialog (dùng chung với các page khác) */}
      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </>
  );
}
