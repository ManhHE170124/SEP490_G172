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

// Helper functions to extract account/key info from orderDetail (same as admin)
const pickArr = (v) => (Array.isArray(v) ? v : []);

const pickAccounts = (it) => {
  const accounts = pickArr(it?.accounts ?? it?.Accounts);

  // fallback single
  const singleEmail = it?.accountEmail ?? it?.AccountEmail;
  const singleUsername = it?.accountUsername ?? it?.AccountUsername;
  const singlePassword = it?.accountPassword ?? it?.AccountPassword;

  if (accounts.length > 0) return accounts;

  if (singleEmail || singleUsername || singlePassword) {
    return [
      {
        Email: singleEmail,
        Username: singleUsername,
        Password: singlePassword,
        email: singleEmail,
        username: singleUsername,
        password: singlePassword,
      },
    ];
  }

  return [];
};

const pickKeys = (it) => {
  const list = pickArr(it?.keyStrings ?? it?.KeyStrings);
  const single = it?.keyString ?? it?.KeyString;
  if (list.length > 0) return list;
  if (single) return [single];
  return [];
};

const getInfoKind = (it) => {
  const pType = String(it?.productType ?? it?.ProductType ?? "").trim().toLowerCase();
  const keys = pickKeys(it);
  const accounts = pickAccounts(it);

  // ưu tiên KEY nếu có keyStrings
  if (keys.length > 0) return { kind: "key", keys, accounts: [] };

  // chỉ coi là account khi không phải key
  if (accounts.length > 0) return { kind: "account", keys: [], accounts };

  // fallback theo ProductType
  if (pType.includes("key")) return { kind: "key", keys: [], accounts: [] };
  if (pType.includes("account") || pType.includes("tài khoản") || pType.includes("shared"))
    return { kind: "account", keys: [], accounts: [] };

  return { kind: "none", keys: [], accounts: [] };
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
  const [modalReveal, setModalReveal] = useState(false); // For account password
  const [keyRevealStates, setKeyRevealStates] = useState({}); // For individual keys: { keyIndex: boolean }
  
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
      // API now returns { order, orderItems, pageIndex, pageSize, totalItems }
      // For customer view, we want all order details, so use a large pageSize
      const response = await orderApi.get(id, { pageSize: 1000 });
      const data = response?.data ?? response;
      
      // Extract order from new response structure
      const order = data?.order ?? data?.Order ?? data;
      
      // If orderDetails are paged, ensure we have all of them
      // The backend already includes all orderDetails in order.orderDetails (paged)
      // But we might need to merge orderItems if they're separate
      if (order && data?.orderItems && Array.isArray(data.orderItems)) {
        // If orderItems are provided separately and orderDetails is empty/paged, use orderItems
        if (!order.orderDetails || order.orderDetails.length === 0) {
          order.orderDetails = data.orderItems;
        }
      }
      
      setOrder(order);
    } catch (err) {
      
      if (err?.response?.status === 403 || err?.response?.status === 404) {
        setError("Đơn hàng không tồn tại.");
      } else {
        const message =
          err?.response?.data?.message ||
          err?.message ||
          "Không thể tải thông tin đơn hàng.";
        setError(message);
      }
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    loadOrder();
  }, [loadOrder]);

  const handleGetCredentials = useCallback((detail) => {
    // Get account/key info directly from orderDetail (same as admin)
    if (!detail) return;
    setModalData(detail);
  }, []);

  const handleCloseModal = () => {
    setModalData(null);
    setShowPassword(false);
    setModalReveal(false);
    setKeyRevealStates({});
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
                      const info = getInfoKind(detail);
                      const hasCredentials = (info.kind === "key" && info.keys.length > 0) || 
                                           (info.kind === "account" && info.accounts.length > 0);
                      
                      return (
                        <tr key={detail.orderDetailId}>
                          <td>{detail.productName || "—"}</td>
                          <td>{detail.variantTitle || "—"}</td>
                          <td className="order-detail-text-right">{detail.quantity || 0}</td>
                          <td className="order-detail-text-right">{formatMoney(detail.unitPrice)}</td>
                          <td className="order-detail-text-right">{formatMoney(detail.subTotal)}</td>
                          <td>
                            {isPaid && hasCredentials ? (
                              <button
                                type="button"
                                className="order-detail-btn-credential"
                                onClick={() => handleGetCredentials(detail)}
                                disabled={modalLoading}
                              >
                                {info.kind === "account" ? "Xem thông tin tài khoản" : "Xem mã kích hoạt"}
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
      {modalData && (() => {
        const info = getInfoKind(modalData);
        const productName = modalData.productName ?? modalData.ProductName ?? "—";
        const variantTitle = modalData.variantTitle ?? modalData.VariantTitle ?? "—";
        
        return (
          <div className="order-detail-modal-backdrop" onClick={handleCloseModal}>
            <div className="order-detail-modal" onClick={(e) => e.stopPropagation()}>
              <div className="order-detail-modal-header">
                <h3>{variantTitle}</h3>
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
                  <div className="order-detail-modal-value">{productName}</div>
                </div>

                {info.kind === "none" ? (
                  <div className="order-detail-modal-field">
                    <div style={{ padding: "16px", textAlign: "center", color: "#6b7280" }}>
                      Sản phẩm này không có thông tin Mã kích hoạt/Tài khoản để hiển thị.
                    </div>
                  </div>
                ) : info.kind === "key" ? (
                  <div className="order-detail-modal-field">
                    <label className="order-detail-modal-label">Mã kích hoạt sản phẩm</label>
                    {info.keys.length === 0 ? (
                      <div style={{ padding: "16px", textAlign: "center", color: "#6b7280" }}>
                        Không có mã kích hoạt.
                      </div>
                    ) : (
                      info.keys.map((key, idx) => {
                        const isRevealed = keyRevealStates[idx] || false;
                        return (
                          <div key={idx} className="order-detail-modal-input-group" style={{ marginBottom: "12px" }}>
                            <label className="order-detail-modal-sublabel">Key {idx + 1}</label>
                            <div className="order-detail-modal-input-wrapper">
                              <input
                                type={isRevealed ? "text" : "password"}
                                className="order-detail-modal-input"
                                value={String(key)}
                                readOnly
                              />
                              <button
                                type="button"
                                className="order-detail-modal-eye-btn"
                                onClick={() => setKeyRevealStates(prev => ({ ...prev, [idx]: !isRevealed }))}
                                title={isRevealed ? "Ẩn mã kích hoạt" : "Hiện mã kích hoạt"}
                              >
                                {isRevealed ? (
                                  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
                                    <circle cx="12" cy="12" r="3"></circle>
                                  </svg>
                                ) : (
                                  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                    <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"></path>
                                    <line x1="1" y1="1" x2="23" y2="23"></line>
                                  </svg>
                                )}
                              </button>
                              <button
                                type="button"
                                className="order-detail-modal-copy-btn"
                                onClick={() => handleCopy(String(key))}
                              >
                                Sao chép
                              </button>
                            </div>
                          </div>
                        );
                      })
                    )}
                  </div>
                ) : (
                  <div className="order-detail-modal-field">
                    <label className="order-detail-modal-label">Tài khoản sản phẩm</label>
                    {info.accounts.length === 0 ? (
                      <div style={{ padding: "16px", textAlign: "center", color: "#6b7280" }}>
                        Không có tài khoản.
                      </div>
                    ) : (
                      info.accounts.map((account, idx) => {
                        const emailA = account.email ?? account.Email ?? "—";
                        const passA = account.password ?? account.Password ?? "—";
                        
                        return (
                          <div key={idx} style={{ marginBottom: "16px", padding: "12px", border: "1px solid #e5e7eb", borderRadius: "8px" }}>
                            <div className="order-detail-modal-input-group">
                              <label className="order-detail-modal-sublabel">Email</label>
                              <div className="order-detail-modal-input-wrapper">
                                <input
                                  type="text"
                                  className="order-detail-modal-input"
                                  value={String(emailA)}
                                  readOnly
                                />
                                <button
                                  type="button"
                                  className="order-detail-modal-copy-btn"
                                  onClick={() => handleCopy(String(emailA))}
                                >
                                  Sao chép
                                </button>
                              </div>
                            </div>
                            <div className="order-detail-modal-input-group">
                              <label className="order-detail-modal-sublabel">Mật khẩu</label>
                              <div className="order-detail-modal-input-wrapper">
                                <input
                                  type={modalReveal ? "text" : "password"}
                                  className="order-detail-modal-input"
                                  value={String(passA)}
                                  readOnly
                                />
                                <button
                                  type="button"
                                  className="order-detail-modal-eye-btn"
                                  onClick={() => setModalReveal(!modalReveal)}
                                  title={modalReveal ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                                >
                                  {modalReveal ? (
                                    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
                                      <circle cx="12" cy="12" r="3"></circle>
                                    </svg>
                                  ) : (
                                    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"></path>
                                      <line x1="1" y1="1" x2="23" y2="23"></line>
                                    </svg>
                                  )}
                                </button>
                                <button
                                  type="button"
                                  className="order-detail-modal-copy-btn"
                                  onClick={() => handleCopy(String(passA))}
                                >
                                  Sao chép
                                </button>
                              </div>
                            </div>
                          </div>
                        );
                      })
                    )}
                  </div>
                )}

                <div className="order-detail-modal-warning">
                  <span className="order-detail-modal-warning-icon">⚠️</span>
                  <span className="order-detail-modal-warning-text">
                    Vì lý do bảo mật, vui lòng không chia sẻ những thông tin đăng nhập này.
                  </span>
                </div>
              </div>
            </div>
          </div>
        );
      })()}

      {/* Toast Container */}
      <ToastContainer toasts={toasts} onRemove={removeToast} />
    </div>
  );
}

