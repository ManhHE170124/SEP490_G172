import React from "react";
import { useNavigate, useParams } from "react-router-dom";
import { orderApi } from "../../services/orderApi";
import "./OrderDetailPage.css";

const ORDER_STATUS_LABEL = {
  Pending: "Đang chờ",
  Processing: "Đang xử lý",
  Completed: "Đã xử lý",
  Cancelled: "Đã hủy",
  Refunded: "Đã hoàn",
};

const PAYMENT_STATUS_LABEL = {
  Unpaid: "Chưa thanh toán",
  Partial: "Thanh toán một phần",
  Paid: "Đã thanh toán",
  Refunded: "Đã hoàn tiền",
};

const PAYMENT_METHOD_LABEL = {
  "Bank Transfer": "Chuyển khoản ngân hàng",
  "Credit Card": "Thẻ tín dụng",
  "Debit Card": "Thẻ ghi nợ",
  "E-Wallet": "Ví điện tử",
  "Cash": "Tiền mặt",
  "Other": "Khác",
};

const formatCurrency = (value) =>
  typeof value === "number"
    ? new Intl.NumberFormat("vi-VN", {
        style: "currency",
        currency: "VND",
        maximumFractionDigits: 0,
      }).format(value)
    : "-";

const formatDateTime = (value) => {
  if (!value) {
    return "-";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value ?? "-";
  }
  return date.toLocaleString("vi-VN", {
    hour12: false,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
};

// Format order number from orderId and createdAt (same as backend)
const formatOrderNumber = (orderId, createdAt) => {
  if (!orderId || !createdAt) return "";
  const date = new Date(createdAt);
  const dateStr = date.toISOString().slice(0, 10).replace(/-/g, "");
  const orderIdStr = String(orderId).replace(/-/g, "").substring(0, 4).toUpperCase();
  return `ORD-${dateStr}-${orderIdStr}`;
};

export default function OrderDetailPage() {
  const navigate = useNavigate();
  const { id } = useParams();
  const [order, setOrder] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState(null);

  // Get user info from localStorage
  const userInfo = React.useMemo(() => {
    try {
      const userStr = localStorage.getItem("user");
      if (!userStr) return null;
      return JSON.parse(userStr);
    } catch (error) {
      console.error("Failed to parse user from localStorage:", error);
      return null;
    }
  }, []);

  // Fetch order from API
  React.useEffect(() => {
    const fetchOrder = async () => {
      if (!id) {
        setLoading(false);
        setError("Không có ID đơn hàng");
        return;
      }

      try {
        setLoading(true);
        setError(null);
        const response = await orderApi.get(id);
        // axiosClient interceptor already unwraps response.data
        const data = response || {};
        
        console.log("Order detail API response:", data);
        
        // Map API response to component format
        const mappedOrder = {
          orderId: data.orderId || data.OrderId,
          userId: data.userId || data.UserId,
          orderNumber: formatOrderNumber(
            data.orderId || data.OrderId,
            data.createdAt || data.CreatedAt
          ),
          totalAmount: data.totalAmount || data.TotalAmount || 0,
          discountAmount: data.discountAmount ?? data.DiscountAmount ?? 0,
          finalAmount: data.finalAmount ?? data.FinalAmount ?? null,
          status: data.status || data.Status || "Pending",
          createdAt: data.createdAt || data.CreatedAt,
          userEmail: data.userEmail || data.UserEmail || "",
          userName: data.userName || data.UserName || "",
          userPhone: data.userPhone || data.UserPhone || "",
          paymentStatus: data.paymentStatus || data.PaymentStatus || "Unpaid",
          orderDetails: (data.orderDetails || data.OrderDetails || []).map((od) => ({
            orderDetailId: od.orderDetailId || od.OrderDetailId,
            productId: od.productId || od.ProductId,
            productName: od.productName || od.ProductName || "",
            productCode: od.productCode || od.ProductCode || null,
            productType: od.productType || od.ProductType || null,
            thumbnailUrl: od.thumbnailUrl || od.ThumbnailUrl || null,
            quantity: od.quantity || od.Quantity || 0,
            unitPrice: od.unitPrice || od.UnitPrice || 0,
            keyId: od.keyId || od.KeyId || null,
            keyString: od.keyString || od.KeyString || null,
            subTotal: od.subTotal || od.SubTotal || 0,
          })),
          payments: (data.payments || data.Payments || []).map((p) => ({
            paymentId: p.paymentId || p.PaymentId,
            amount: p.amount || p.Amount || 0,
            status: p.status || p.Status || "Pending",
            createdAt: p.createdAt || p.CreatedAt,
          })),
        };
        
        setOrder(mappedOrder);
      } catch (err) {
        console.error("Failed to fetch order:", err);
        setError(err.response?.data?.message || err.message || "Không thể tải thông tin đơn hàng");
        setOrder(null);
      } finally {
        setLoading(false);
      }
    };

    fetchOrder();
  }, [id]);

  if (loading) {
    return (
      <div className="od-wrapper">
        <header className="od-header">
          <div>
            <h1>Đang tải...</h1>
            <p>Vui lòng đợi trong giây lát</p>
          </div>
        </header>
      </div>
    );
  }

  if (error || !order) {
    return (
      <div className="od-wrapper">
        <header className="od-header od-empty-state">
          <div>
            <h1>Không tìm thấy đơn hàng</h1>
            <p>{error || "Đơn hàng không tồn tại hoặc đã bị xóa."}</p>
          </div>
          <button
            type="button"
            className="od-secondary"
            onClick={() => navigate("/orders/history")}
          >
            Quay lại lịch sử
          </button>
        </header>
      </div>
    );
  }

  // Get payment status label
  const paymentStatusLabel = PAYMENT_STATUS_LABEL[order.paymentStatus] || order.paymentStatus;

  // Copy to clipboard function
  const copyToClipboard = (text) => {
    navigator.clipboard
      .writeText(text)
      .then(() => {
        // Could add toast notification here
        console.log("Copied to clipboard:", text);
      })
      .catch((err) => {
        console.error("Failed to copy:", err);
      });
  };

  return (
    <div className="od-wrapper">
      <header className="od-header">
        <div>
          <div className="od-breadcrumb">
            <button
              type="button"
              className="od-secondary"
              onClick={() => navigate("/orders/history")}
            >
              ← Lịch sử đơn hàng
            </button>
          </div>
          <h1>Chi tiết đơn hàng {order.orderNumber}</h1>
          <p>Hiển thị thông tin các sản phẩm bạn đã mua tại Keytietkiem.</p>
        </div>
        <button type="button" className="od-primary">
          Mua lại đơn hàng
        </button>
      </header>

      <section className="od-info-card">
        <div className="od-info-block">
          <h2>Thông tin đơn hàng</h2>
          <dl>
            <div>
              <dt>Mã đơn hàng</dt>
              <dd>{order.orderNumber}</dd>
            </div>
            <div>
              <dt>Ngày tạo</dt>
              <dd>{formatDateTime(order.createdAt)}</dd>
            </div>
            <div>
              <dt>Trạng thái</dt>
              <dd>
                <span className={`od-status od-${order.status?.toLowerCase() ?? ""}`}>
                  {ORDER_STATUS_LABEL[order.status] ?? order.status}
                </span>
              </dd>
            </div>
            <div>
              <dt>Người nhận</dt>
              <dd>{order.userEmail || "—"}</dd>
            </div>
          </dl>
        </div>

        {userInfo && (
          <div className="od-info-block">
            <h2>Thông tin người dùng</h2>
            <dl>
              <div>
                <dt>Họ tên</dt>
                <dd>{order.userName || userInfo.fullName || userInfo.username || "—"}</dd>
              </div>
              {(order.userEmail || userInfo.email) && (
                <div>
                  <dt>Email</dt>
                  <dd>{order.userEmail || userInfo.email}</dd>
                </div>
              )}
              {(order.userPhone || userInfo.phone) && (
                <div>
                  <dt>Số điện thoại</dt>
                  <dd>{order.userPhone || userInfo.phone}</dd>
                </div>
              )}
            </dl>
          </div>
        )}

        <div className="od-info-block">
          <h2>Giá trị đơn hàng</h2>
          <dl>
            <div>
              <dt>Tổng giá trị sản phẩm</dt>
              <dd>{formatCurrency(order.totalAmount)}</dd>
            </div>
            <div>
              <dt>Giảm giá</dt>
              <dd>
                {order.discountAmount
                  ? `- ${formatCurrency(order.discountAmount)}`
                  : "0đ"}
              </dd>
            </div>
            <div>
              <dt>Thành tiền</dt>
              <dd>{formatCurrency(order.finalAmount ?? order.totalAmount - order.discountAmount)}</dd>
            </div>
            <div>
              <dt>Trạng thái thanh toán</dt>
              <dd>{paymentStatusLabel}</dd>
            </div>
          </dl>
        </div>
      </section>

      <section className="od-product-card">
        <div className="od-product-header">
          <h2>Sản phẩm trong đơn</h2>
          <span>{order.orderDetails?.length || 0} sản phẩm</span>
        </div>
        {order.orderDetails && order.orderDetails.length > 0 ? (
          <table>
            <thead>
              <tr>
                <th>Sản phẩm</th>
                <th>Mã sản phẩm</th>
                <th>Loại</th>
                <th className="od-right">Số lượng</th>
                <th className="od-right">Đơn giá</th>
                <th>Thông tin tài khoản/Key</th>
              </tr>
            </thead>
            <tbody>
              {order.orderDetails.map((detail) => (
                <tr key={detail.orderDetailId}>
                  <td>
                    <strong>{detail.productName}</strong>
                  </td>
                  <td>{detail.productCode ?? "—"}</td>
                  <td>{detail.productType ?? "—"}</td>
                  <td className="od-right">{detail.quantity}</td>
                  <td className="od-right">{formatCurrency(detail.unitPrice)}</td>
                  <td>
                    {(() => {
                      if (detail.accountEmail && detail.accountPassword) {
                        return (
                          <div className="od-account-info-cell">
                            <div className="od-account-credential">
                              <span>
                                Tài khoản: {detail.accountEmail} | Mật khẩu:{" "}
                                {detail.accountPassword}
                              </span>
                              <button
                                type="button"
                                className="od-copy-btn"
                                onClick={() =>
                                  copyToClipboard(
                                    `Tài khoản: ${detail.accountEmail} | Mật khẩu: ${detail.accountPassword}`
                                  )
                                }
                              >
                                Sao chép
                              </button>
                            </div>
                          </div>
                        );
                      }
                      if (detail.keyString) {
                        return (
                          <div className="od-account-info-cell">
                            <div className="od-account-credential">
                              <span>{detail.keyString}</span>
                              <button
                                type="button"
                                className="od-copy-btn"
                                onClick={() => copyToClipboard(detail.keyString)}
                              >
                                Sao chép
                              </button>
                            </div>
                          </div>
                        );
                      }
                      return <span className="od-no-info">—</span>;
                    })()}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <div style={{ padding: "2rem", textAlign: "center" }}>
            <p>Không có sản phẩm nào trong đơn hàng này</p>
          </div>
        )}
      </section>
    </div>
  );
}

