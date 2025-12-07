import React, { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { orderApi } from "../../services/orderApi";
import useToast from "../../hooks/useToast";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./OrderHistoryDetailPage.css";

const formatDate = (value, fallback = "—") => {
  if (!value) return fallback;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return fallback;
  return date.toLocaleDateString("vi-VN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
};

const formatMoney = (value) => {
  if (value === null || value === undefined || value === "") return "—";
  const numeric = Number(value);
  if (Number.isNaN(numeric)) return "—";
  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  })
    .format(numeric)
    .replace(/\s?₫/, "đ");
};

const getStatusLabel = (status = "") => {
  const statusMap = {
    paid: "Đã thanh toán",
    cancelled: "Đã hủy",
  };
  const normalized = status.toString().toLowerCase().trim();
  // Chỉ hiển thị 2 trạng thái: Đã thanh toán và Đã hủy
  if (normalized === "paid") return "Đã thanh toán";
  if (normalized === "cancelled") return "Đã hủy";
  // Mặc định là "Đã hủy" nếu không khớp
  return "Đã hủy";
};

const getStatusTone = (status = "") => {
  const text = status.toString().toLowerCase().trim();
  // Chỉ có 2 trạng thái: Paid (success) và Cancelled (danger)
  if (text === "paid") return "success";
  if (text === "cancelled") return "danger";
  // Mặc định là danger (Đã hủy)
  return "danger";
};

const formatOrderNumber = (orderId, createdAt) => {
  if (!orderId || !createdAt) return orderId || "—";
  const date = new Date(createdAt);
  const dateStr = date.toISOString().slice(0, 10).replace(/-/g, "");
  const orderIdStr = orderId.toString().replace(/-/g, "").substring(0, 4).toUpperCase();
  return `ORD-${dateStr}-${orderIdStr}`;
};

export default function OrderHistoryDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [order, setOrder] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [modalData, setModalData] = useState(null);
  const [modalLoading, setModalLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  
  // Toast notification
  const { toasts, removeToast, showError, showSuccess } = useToast();

  const loadOrder = useCallback(async () => {
    if (!id) {
      setError("Không tìm thấy mã đơn hàng.");
      setLoading(false);
      return;
    }

    setLoading(true);
    setError("");
    try {
      const response = await orderApi.get(id);
      const data = response?.data ?? response;
      setOrder(data);
    } catch (err) {
      const message =
        err?.response?.data?.message ||
        err?.message ||
        "Không thể tải thông tin đơn hàng.";
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    loadOrder();
  }, [loadOrder]);

  const handleGetCredentials = useCallback(async (orderDetailId) => {
    if (!id || !orderDetailId) return;

    setModalLoading(true);
    try {
      const response = await orderApi.getDetailCredentials(id, orderDetailId);
      const data = response?.data ?? response;
      setModalData(data);
    } catch (err) {
      const message =
        err?.response?.data?.message ||
        err?.message ||
        "Không thể tải thông tin tài khoản.";
      showError("Lỗi", message);
    } finally {
      setModalLoading(false);
    }
  }, [id]);

  const handleCloseModal = () => {
    setModalData(null);
    setShowPassword(false);
  };

  const handleCopy = (text) => {
    navigator.clipboard.writeText(text).then(() => {
      showSuccess("Thành công", "Đã sao chép!");
    }).catch(() => {
      showError("Lỗi", "Không thể sao chép");
    });
  };

  if (loading) {
    return (
      <div className="order-detail-page">
        <div className="order-detail-container">
          <div className="order-detail-loading">
            <div className="order-detail-spinner" />
            <div>Đang tải thông tin đơn hàng...</div>
          </div>
        </div>
      </div>
    );
  }

  if (error || !order) {
    return (
      <div className="order-detail-page">
        <div className="order-detail-container">
          <div className="order-detail-error">
            <div>{error || "Không tìm thấy đơn hàng"}</div>
            <button
              type="button"
              className="order-detail-btn order-detail-btn-primary"
              onClick={() => navigate("/account/profile")}
            >
              Quay lại
            </button>
          </div>
        </div>
      </div>
    );
  }

  const orderNumber = formatOrderNumber(order.orderId, order.createdAt);

  return (
    <div className="order-detail-page">
      <div className="order-detail-container">
        <div className="order-detail-header">
          <div>
            <h1 className="order-detail-title">Chi tiết đơn hàng</h1>
            <div className="order-detail-subtitle">Mã đơn: {orderNumber}</div>
          </div>
          <button
            type="button"
            className="order-detail-btn order-detail-btn-secondary"
            onClick={() => navigate("/account/profile")}
          >
            ← Quay lại
          </button>
        </div>

        <div className="order-detail-card">
          <div className="order-detail-section">
            <h3 className="order-detail-section-title">Thông tin đơn hàng</h3>
            <div className="order-detail-grid">
              <div className="order-detail-field">
                <label className="order-detail-label">Mã đơn hàng</label>
                <div className="order-detail-value">{orderNumber}</div>
              </div>
              <div className="order-detail-field">
                <label className="order-detail-label">Ngày tạo</label>
                <div className="order-detail-value">{formatDate(order.createdAt)}</div>
              </div>
              <div className="order-detail-field">
                <label className="order-detail-label">Trạng thái</label>
                <div className="order-detail-value">
                  <span className={`order-detail-pill order-detail-pill-${getStatusTone(order.status)}`}>
                    {getStatusLabel(order.status)}
                  </span>
                </div>
              </div>
            </div>
          </div>

          <div className="order-detail-section">
            <h3 className="order-detail-section-title">Thông tin khách hàng</h3>
            <div className="order-detail-grid">
              <div className="order-detail-field">
                <label className="order-detail-label">Tên người nhận</label>
                <div className="order-detail-value">{order.userName || "—"}</div>
              </div>
              <div className="order-detail-field">
                <label className="order-detail-label">Email</label>
                <div className="order-detail-value">{order.userEmail || order.email || "—"}</div>
              </div>
              <div className="order-detail-field">
                <label className="order-detail-label">Số điện thoại</label>
                <div className="order-detail-value">{order.userPhone || "—"}</div>
              </div>
            </div>
          </div>

          <div className="order-detail-section">
            <h3 className="order-detail-section-title">Sản phẩm</h3>
            {order.orderDetails && order.orderDetails.length > 0 ? (
              <div className="order-detail-table-wrapper">
                <table className="order-detail-table">
                  <thead>
                    <tr>
                      <th>Sản phẩm</th>
                      <th>Gói</th>
                      <th>Số lượng</th>
                      <th>Đơn giá</th>
                      <th>Thành tiền</th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {order.orderDetails.map((detail) => {
                      const isPaid = order.status?.toLowerCase() === "paid";
                      return (
                        <tr key={detail.orderDetailId}>
                          <td>{detail.productName || "—"}</td>
                          <td>{detail.variantTitle || "—"}</td>
                          <td className="order-detail-text-right">{detail.quantity || 0}</td>
                          <td className="order-detail-text-right">{formatMoney(detail.unitPrice)}</td>
                          <td className="order-detail-text-right">{formatMoney(detail.subTotal)}</td>
                          <td>
                            {isPaid ? (
                              <button
                                type="button"
                                className="order-detail-btn-credential"
                                onClick={() => handleGetCredentials(detail.orderDetailId)}
                                disabled={modalLoading}
                              >
                                Lấy thông tin tài khoản
                              </button>
                            ) : (
                              <span className="order-detail-text-muted">—</span>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="order-detail-empty">Không có sản phẩm nào</div>
            )}
          </div>

          <div className="order-detail-section">
            <h3 className="order-detail-section-title">Tổng thanh toán</h3>
            <div className="order-detail-summary">
              <div className="order-detail-summary-row">
                <span className="order-detail-summary-label">Tổng tiền:</span>
                <span className="order-detail-summary-value">{formatMoney(order.totalAmount)}</span>
              </div>
              {order.discountAmount > 0 && (
                <div className="order-detail-summary-row">
                  <span className="order-detail-summary-label">Giảm giá:</span>
                  <span className="order-detail-summary-value order-detail-discount">
                    -{formatMoney(order.discountAmount)}
                  </span>
                </div>
              )}
              <div className="order-detail-summary-row order-detail-summary-total">
                <span className="order-detail-summary-label">Thành tiền:</span>
                <span className="order-detail-summary-value order-detail-total">
                  {formatMoney(order.finalAmount ?? order.totalAmount - order.discountAmount)}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Modal hiển thị thông tin tài khoản/key */}
      {modalData && (
        <div className="order-detail-modal-backdrop" onClick={handleCloseModal}>
          <div className="order-detail-modal" onClick={(e) => e.stopPropagation()}>
            <div className="order-detail-modal-header">
              <h3>Thông tin {modalData.productType === "ACCOUNT" ? "tài khoản" : "mã kích hoạt"}</h3>
              <button
                type="button"
                className="order-detail-modal-close"
                onClick={handleCloseModal}
              >
                ×
              </button>
            </div>
            <div className="order-detail-modal-body">
              <div className="order-detail-modal-field">
                <label className="order-detail-modal-label">Tên sản phẩm</label>
                <div className="order-detail-modal-value">{modalData.productName || "—"}</div>
              </div>

              <div className="order-detail-modal-field">
                <label className="order-detail-modal-label">
                  {modalData.productType === "ACCOUNT" ? "Tài khoản" : "Mã kích hoạt"}
                </label>
                {modalData.productType === "ACCOUNT" ? (
                  <>
                    <div className="order-detail-modal-input-group">
                      <label className="order-detail-modal-sublabel">Email tài khoản</label>
                      <div className="order-detail-modal-input-wrapper">
                        <input
                          type="text"
                          className="order-detail-modal-input"
                          value={modalData.accountEmail || ""}
                          readOnly
                        />
                        <button
                          type="button"
                          className="order-detail-modal-copy-btn"
                          onClick={() => handleCopy(modalData.accountEmail || "")}
                        >
                          Sao chép
                        </button>
                      </div>
                    </div>
                    <div className="order-detail-modal-input-group">
                      <label className="order-detail-modal-sublabel">Mật khẩu</label>
                      <div className="order-detail-modal-input-wrapper">
                        <input
                          type={showPassword ? "text" : "password"}
                          className="order-detail-modal-input"
                          value={modalData.accountPassword || ""}
                          readOnly
                        />
                        <button
                          type="button"
                          className="order-detail-modal-eye-btn"
                          onClick={() => setShowPassword(!showPassword)}
                        >
                          {showPassword ? "👁️" : "👁️‍🗨️"}
                        </button>
                        <button
                          type="button"
                          className="order-detail-modal-copy-btn"
                          onClick={() => handleCopy(modalData.accountPassword || "")}
                        >
                          Sao chép
                        </button>
                      </div>
                    </div>
                  </>
                ) : (
                  <div className="order-detail-modal-input-group">
                    <div className="order-detail-modal-input-wrapper">
                      <input
                        type={showPassword ? "text" : "password"}
                        className="order-detail-modal-input"
                        value={modalData.keyString || ""}
                        readOnly
                      />
                      <button
                        type="button"
                        className="order-detail-modal-eye-btn"
                        onClick={() => setShowPassword(!showPassword)}
                      >
                        {showPassword ? "👁️" : "👁️‍🗨️"}
                      </button>
                      <button
                        type="button"
                        className="order-detail-modal-copy-btn"
                        onClick={() => handleCopy(modalData.keyString || "")}
                      >
                        Sao chép
                      </button>
                    </div>
                  </div>
                )}
              </div>

              <div className="order-detail-modal-warning">
                <span className="order-detail-modal-warning-icon">⚠️</span>
                <span className="order-detail-modal-warning-text">
                  Vì lý do bảo mật, vui lòng không chia sẻ những thông tin đăng nhập này.
                </span>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Toast Container */}
      <ToastContainer toasts={toasts} onRemove={removeToast} />
    </div>
  );
}

