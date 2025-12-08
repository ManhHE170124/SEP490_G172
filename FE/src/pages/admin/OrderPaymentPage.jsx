// src/pages/admin/OrderPaymentPage.jsx
import React from "react";
import { orderApi } from "../../services/orderApi";
import { paymentApi } from "../../services/paymentApi";
import ToastContainer from "../../components/Toast/ToastContainer";
import PermissionGuard from "../../components/PermissionGuard";
import { usePermission } from "../../hooks/usePermission";
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

/* Toggle sort state */
const toggleSortState = (current, key) =>
  current.sortBy === key
    ? { sortBy: key, sortDir: current.sortDir === "asc" ? "desc" : "asc" }
    : { sortBy: key, sortDir: "asc" };

const renderSortIndicator = (current, key) => {
  if (!current || current.sortBy !== key) return null;
  return current.sortDir === "asc" ? " ▲" : " ▼";
};

/**
 * Trạng thái PAYMENT
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

/* Loại thanh toán (transactionType) */
const PAYMENT_TYPE_OPTIONS = [
  { value: "", valueLabel: "ALL", label: "Tất cả loại" },
  { value: "ORDER_CART", label: "Thanh toán giỏ hàng" },
  { value: "ORDER_PAYMENT", label: "Thanh toán đơn hàng (cũ)" },
  { value: "SUPPORT_PLAN", label: "Thanh toán gói hỗ trợ" },
];

/* Map status -> class cho badge (payment) */
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

/* Map status -> label tiếng Việt (payment) */
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

// Label tiếng Việt cho loại thanh toán
const getTransactionTypeLabel = (type) => {
  const t = (type || "").toUpperCase();
  switch (t) {
    case "ORDER_CART":
      return "Thanh toán giỏ hàng";
    case "ORDER_PAYMENT":
      return "Thanh toán đơn hàng";
    case "SUPPORT_PLAN":
      return "Thanh toán gói hỗ trợ";
    default:
      return type || "Không xác định";
  }
};

// Dùng riêng cho filter trạng thái payment
const PAYMENT_STATUS_FILTER_OPTIONS = [
  { value: "Pending", label: getPaymentStatusLabel("Pending") },
  { value: "Paid", label: getPaymentStatusLabel("Paid") },
  { value: "Cancelled", label: getPaymentStatusLabel("Cancelled") },
  { value: "Failed", label: getPaymentStatusLabel("Failed") },
  { value: "Refunded", label: getPaymentStatusLabel("Refunded") },
];

/* ===== Modal: Order Detail (chỉ xem) ===== */

function OrderDetailModal({ open, orderId, onClose, addToast }) {
  const [loading, setLoading] = React.useState(false);
  const [order, setOrder] = React.useState(null);

  React.useEffect(() => {
    if (!open || !orderId) return;

    setLoading(true);
    orderApi
      .get(orderId)
      .then((res) => {
        const data = res?.data ?? res;
        setOrder(data);
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

  if (!open) return null;

  const effectiveFinal =
    order != null
      ? order.finalAmount ??
        (order.totalAmount != null && order.discountAmount != null
          ? order.totalAmount - order.discountAmount
          : order.totalAmount)
      : 0;

  return (
    <div className="cat-modal-backdrop">
      <div className="cat-modal-card">
        <div className="cat-modal-header">
          <h3>Chi tiết đơn hàng</h3>
          {/* Không còn status badge vì Order hiện chỉ là log immutable */}
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

                  <div className="detail-label" style={{ marginTop: 6 }}>
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

                  <div className="detail-label" style={{ marginTop: 6 }}>
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

              {/* Sản phẩm */}
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
          <button
            type="button"
            className="btn ghost"
            onClick={onClose}
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
}

/* ===== Modal: Payment Detail (chỉ xem, status auto) ===== */

function PaymentDetailModal({
  open,
  paymentId,
  onClose,
  onUpdated,   // vẫn nhận props nhưng không dùng, để tránh lỗi chỗ gọi
  addToast,
  openConfirm, // vẫn nhận props nhưng không dùng
}) {
  const [loading, setLoading] = React.useState(false);
  const [payment, setPayment] = React.useState(null);

  React.useEffect(() => {
    if (!open || !paymentId) return;

    setLoading(true);
    paymentApi
      .get(paymentId)
      .then((res) => {
        const data = res?.data ?? res;
        setPayment(data);
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

  if (!open) return null;

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
              {/* Meta chung */}
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
                    {getTransactionTypeLabel(payment.transactionType)}
                  </div>
                </div>
              </div>

              {/* Số tiền */}
              <div className="detail-section-title">Số tiền</div>
              <div className="detail-section-box">
                <div className="detail-label">Số tiền thanh toán</div>
                <div className="detail-value text-mono">
                  {payment.amount?.toLocaleString("vi-VN")} đ
                </div>
              </div>

              {/* Cổng thanh toán (không còn khối trạng thái ở đây) */}
              <div className="detail-section-title">Cổng thanh toán</div>
              <div className="detail-section-box">
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(2, minmax(0,1fr))",
                    gap: 16,
                    alignItems: "center",
                  }}
                >
                  <div>
                    <div className="detail-label">Cổng thanh toán</div>
                    <div className="detail-value">
                      {payment.provider || "PayOS"}
                    </div>
                  </div>
                  <div>
                    <div className="detail-label">Mã giao dịch</div>
                    <div className="detail-value mono">
                      {payment.providerOrderCode != null
                        ? payment.providerOrderCode
                        : "—"}
                    </div>
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
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
}

/* ===== MAIN PAGE ===== */

export default function OrderPaymentPage() {
  const { hasPermission: hasViewDetailPermission } = usePermission("PRODUCT_MANAGER", "VIEW_DETAIL");

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
  });
  const [orderSort, setOrderSort] = React.useState({
    sortBy: "createdAt",
    sortDir: "desc",
  });
  const [orderPage, setOrderPage] = React.useState(1);
  const [orderPageSize] = React.useState(10);

  const [orderModal, setOrderModal] = React.useState({
    open: false,
    id: null,
  });

  const loadOrders = React.useCallback(() => {
    setOrderLoading(true);
    const params = {};
    if (orderSort.sortBy) params.sortBy = orderSort.sortBy;
    if (orderSort.sortDir) params.sortDir = orderSort.sortDir;

    orderApi
      .list(params)
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
  }, [orderSort]);

  React.useEffect(() => {
    loadOrders();
  }, [loadOrders]);

  React.useEffect(() => {
    setOrderPage(1);
  }, [orderFilter.keyword]);

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

    // Không sort lại ở FE – giữ nguyên thứ tự từ BE (đã sort)
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

  /* ===== Payments state ===== */
  const [payments, setPayments] = React.useState([]);
  const [paymentLoading, setPaymentLoading] = React.useState(false);
  const [paymentFilter, setPaymentFilter] = React.useState({
    keyword: "",
    status: "",
    provider: "",
    transactionType: "", // mặc định: xem tất cả loại
  });
  const [paymentSort, setPaymentSort] = React.useState({
    sortBy: "createdAt",
    sortDir: "desc",
  });
  const [paymentPage, setPaymentPage] = React.useState(1);
  const [paymentPageSize] = React.useState(10);

  const [paymentModal, setPaymentModal] = React.useState({
    open: false,
    id: null,
  });

  const loadPayments = React.useCallback(() => {
    setPaymentLoading(true);
    const params = {};

    if (paymentFilter.transactionType) {
      params.transactionType = paymentFilter.transactionType;
    }
    if (paymentFilter.status) params.status = paymentFilter.status;
    if (paymentFilter.provider) params.provider = paymentFilter.provider;

    if (paymentSort.sortBy) params.sortBy = paymentSort.sortBy;
    if (paymentSort.sortDir) params.sortDir = paymentSort.sortDir;

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
  }, [
    paymentFilter.transactionType,
    paymentFilter.status,
    paymentFilter.provider,
    paymentSort,
  ]);

  React.useEffect(() => {
    const t = setTimeout(loadPayments, 300);
    return () => clearTimeout(t);
  }, [loadPayments]);

  React.useEffect(() => {
    setPaymentPage(1);
  }, [
    paymentFilter.keyword,
    paymentFilter.status,
    paymentFilter.provider,
    paymentFilter.transactionType,
  ]);

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

    // Không sort lại ở FE – giữ nguyên thứ tự từ BE
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

          {/* Filters đơn hàng */}
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
            <button
              className="btn"
              type="button"
              onClick={() =>
                setOrderFilter({ keyword: "" })
              }
            >
              Đặt lại
            </button>
          </div>

          {/* Table Orders */}
          <table className="table" style={{ marginTop: 10 }}>
            <thead>
              <tr>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() =>
                      setOrderSort((cur) =>
                        toggleSortState(cur, "orderId")
                      )
                    }
                  >
                    Mã đơn
                    {renderSortIndicator(orderSort, "orderId")}
                  </button>
                </th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() =>
                      setOrderSort((cur) =>
                        toggleSortState(cur, "customer")
                      )
                    }
                  >
                    Khách hàng
                    {renderSortIndicator(orderSort, "customer")}
                  </button>
                </th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() =>
                      setOrderSort((cur) =>
                        toggleSortState(cur, "email")
                      )
                    }
                  >
                    Email
                    {renderSortIndicator(orderSort, "email")}
                  </button>
                </th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() =>
                      setOrderSort((cur) =>
                        toggleSortState(cur, "itemCount")
                      )
                    }
                  >
                    Số SP
                    {renderSortIndicator(orderSort, "itemCount")}
                  </button>
                </th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() =>
                      setOrderSort((cur) =>
                        toggleSortState(cur, "finalAmount")
                      )
                    }
                  >
                    Thành tiền
                    {renderSortIndicator(orderSort, "finalAmount")}
                  </button>
                </th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() =>
                      setOrderSort((cur) =>
                        toggleSortState(cur, "createdAt")
                      )
                    }
                  >
                    Ngày tạo
                    {renderSortIndicator(orderSort, "createdAt")}
                  </button>
                </th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {orderPageItems.map((o) => {
                const effectiveFinal =
                  o.finalAmount ?? o.totalAmount;
                return (
                  <tr key={o.orderId}>
                    <td>
                      <EllipsisCell
                        mono
                        maxWidth={160}
                        title={o.orderId}
                      >
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
                    <td>{formatVnDateTime(o.createdAt)}</td>
                    <td>
                      <div className="action-buttons">
                        <PermissionGuard moduleCode="PRODUCT_MANAGER" permissionCode="VIEW_DETAIL" fallback={
                          <button
                            className="action-btn edit-btn disabled"
                            type="button"
                            title="Bạn không có quyền xem chi tiết đơn hàng"
                            disabled
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
                        }>
                          <button
                            className="action-btn edit-btn"
                            type="button"
                            title="Xem chi tiết đơn hàng"
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
                        </PermissionGuard>
                      </div>
                    </td>
                  </tr>
                );
              })}
              {orderPageItems.length === 0 && (
                <tr>
                  <td colSpan={7} style={{ padding: 12 }}>
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
            <div className="group" style={{ minWidth: 200 }}>
              <span>Loại thanh toán</span>
              <select
                value={paymentFilter.transactionType}
                onChange={(e) =>
                  setPaymentFilter((s) => ({
                    ...s,
                    transactionType: e.target.value,
                  }))
                }
              >
                {PAYMENT_TYPE_OPTIONS.map((t) => (
                  <option key={t.value || "all"} value={t.value}>
                    {t.label}
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
                  transactionType: "",
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
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() =>
                      setPaymentSort((cur) =>
                        toggleSortState(cur, "paymentId")
                      )
                    }
                  >
                    Mã thanh toán
                    {renderSortIndicator(paymentSort, "paymentId")}
                  </button>
                </th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() =>
                      setPaymentSort((cur) =>
                        toggleSortState(cur, "provider")
                      )
                    }
                  >
                    Cổng thanh toán
                    {renderSortIndicator(paymentSort, "provider")}
                  </button>
                </th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() =>
                      setPaymentSort((cur) =>
                        toggleSortState(cur, "transactionType")
                      )
                    }
                  >
                    Loại thanh toán
                    {renderSortIndicator(
                      paymentSort,
                      "transactionType"
                    )}
                  </button>
                </th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() =>
                      setPaymentSort((cur) =>
                        toggleSortState(cur, "amount")
                      )
                    }
                  >
                    Số tiền
                    {renderSortIndicator(paymentSort, "amount")}
                  </button>
                </th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() =>
                      setPaymentSort((cur) =>
                        toggleSortState(cur, "status")
                      )
                    }
                  >
                    Trạng thái
                    {renderSortIndicator(paymentSort, "status")}
                  </button>
                </th>
                <th>
                  <button
                    type="button"
                    className="table-sort-header"
                    onClick={() =>
                      setPaymentSort((cur) =>
                        toggleSortState(cur, "createdAt")
                      )
                    }
                  >
                    Ngày tạo
                    {renderSortIndicator(paymentSort, "createdAt")}
                  </button>
                </th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {paymentPageItems.map((p) => (
                <tr key={p.paymentId}>
                  <td>
                    <EllipsisCell
                      mono
                      maxWidth={160}
                      title={p.paymentId}
                    >
                      {p.paymentId}
                    </EllipsisCell>
                  </td>
                  <td>
                    <EllipsisCell maxWidth={180}>
                      {p.provider || "PayOS"}
                    </EllipsisCell>
                  </td>
                  <td>
                    <EllipsisCell maxWidth={200}>
                      {getTransactionTypeLabel(p.transactionType)}
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
                      <PermissionGuard moduleCode="PRODUCT_MANAGER" permissionCode="VIEW_DETAIL" fallback={
                        <button
                          className="action-btn edit-btn disabled"
                          type="button"
                          title="Bạn không có quyền xem chi tiết thanh toán"
                          disabled
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
                      }>
                        <button
                          className="action-btn edit-btn"
                          type="button"
                          title="Xem chi tiết / đối chiếu"
                          onClick={() =>
                            openPaymentDetail(p.paymentId)
                          }
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
                      </PermissionGuard>
                    </div>
                  </td>
                </tr>
              ))}
              {paymentPageItems.length === 0 && (
                <tr>
                  <td colSpan={7} style={{ padding: 12 }}>
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
        addToast={addToast}
      />

      <PaymentDetailModal
        open={paymentModal.open}
        paymentId={paymentModal.id}
        onClose={closePaymentDetail}
        addToast={addToast}
      />

      {/* Toast + ConfirmDialog (confirm hiện không dùng ở page này nhưng giữ để đồng bộ với ToastContainer) */}
      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </>
  );
}
