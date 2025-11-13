import React from "react";
import { useNavigate, useParams } from "react-router-dom";
import { ORDER_DETAIL_MOCK } from "./orderMocks";
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

export default function OrderDetailPage() {
  const navigate = useNavigate();
  const { id } = useParams();

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

  const allOrders = React.useMemo(
    () => Object.values(ORDER_DETAIL_MOCK),
    []
  );

  const currentOrder =
    (id && ORDER_DETAIL_MOCK[id]) || allOrders[0] || undefined;

  if (!currentOrder) {
    return (
      <div className="od-wrapper">
        <header className="od-header od-empty-state">
          <div>
            <h1>Không tìm thấy dữ liệu đơn hàng</h1>
            <p>
              Hiện chưa có thông tin mô phỏng cho đơn hàng này. Vui lòng trở về
              lịch sử đơn hàng để chọn đơn khác.
            </p>
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

  // Get payment method from first completed payment, or first payment if no completed
  const paymentMethod =
    currentOrder.payments?.find((p) => p.status === "Completed")
      ?.paymentMethod ||
    currentOrder.payments?.[0]?.paymentMethod ||
    null;

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
          <h1>Chi tiết đơn hàng {currentOrder.orderNumber}</h1>
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
              <dd>{currentOrder.orderNumber}</dd>
            </div>
            <div>
              <dt>Ngày tạo</dt>
              <dd>{formatDateTime(currentOrder.createdAt)}</dd>
            </div>
            <div>
              <dt>Trạng thái</dt>
              <dd>
                <span className={`od-status od-${currentOrder.status ?? ""}`}>
                  {ORDER_STATUS_LABEL[currentOrder.status] ??
                    currentOrder.status}
                </span>
              </dd>
            </div>
            <div>
              <dt>Người nhận</dt>
              <dd>{currentOrder.userEmail}</dd>
            </div>
          </dl>
        </div>

        {userInfo && (
          <div className="od-info-block">
            <h2>Thông tin người dùng</h2>
            <dl>
              <div>
                <dt>Họ tên</dt>
                <dd>{userInfo.fullName || userInfo.username || "—"}</dd>
              </div>
              {userInfo.email && (
                <div>
                  <dt>Email</dt>
                  <dd>{userInfo.email}</dd>
                </div>
              )}
              {userInfo.phone && (
                <div>
                  <dt>Số điện thoại</dt>
                  <dd>{userInfo.phone}</dd>
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
              <dd>{formatCurrency(currentOrder.totalAmount)}</dd>
            </div>
            <div>
              <dt>Giảm giá</dt>
              <dd>
                {currentOrder.discountAmount
                  ? `- ${formatCurrency(currentOrder.discountAmount)}`
                  : "0đ"}
              </dd>
            </div>
            <div>
              <dt>Thành tiền</dt>
              <dd>{formatCurrency(currentOrder.finalAmount)}</dd>
            </div>
            <div>
              <dt>Phương thức thanh toán</dt>
              <dd>
                {(() => {
                  if (paymentMethod) {
                    return PAYMENT_METHOD_LABEL[paymentMethod] || paymentMethod;
                  }
                  if (currentOrder.paymentStatus === "Unpaid") {
                    return "Chưa thanh toán";
                  }
                  return "—";
                })()}
              </dd>
            </div>
          </dl>
        </div>
      </section>

      <section className="od-product-card">
        <div className="od-product-header">
          <h2>Sản phẩm trong đơn</h2>
          <span>{currentOrder.orderDetails.length} sản phẩm</span>
        </div>
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
            {currentOrder.orderDetails.map((detail) => (
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
      </section>
    </div>
  );
}

