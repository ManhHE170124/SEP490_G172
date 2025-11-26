// src/pages/storefront/PaymentCancelPage.jsx
import React, { useState, useCallback } from "react";
import { useLocation, useNavigate, Link } from "react-router-dom";
import axiosClient from "../../api/axiosClient";
import ConfirmDialog from "../../components/Toast/ConfirmDialog";
import Toast from "../../components/Toast/Toast";

const PaymentCancelPage = () => {
  const location = useLocation();
  const navigate = useNavigate();

  const searchParams = new URLSearchParams(location.search);
  const orderId = searchParams.get("orderId");

  const [confirmOpen, setConfirmOpen] = useState(true);
  const [loading, setLoading] = useState(false);
  const [toasts, setToasts] = useState([]);

  const addToast = useCallback((type, title, message) => {
    const id = Date.now() + Math.random();
    setToasts((prev) => [...prev, { id, type, title, message }]);
    setTimeout(
      () => setToasts((prev) => prev.filter((t) => t.id !== id)),
      4000
    );
  }, []);

  const handleCancelOrder = async () => {
    if (!orderId) {
      navigate("/");
      return;
    }

    setLoading(true);
    try {
      await axiosClient.post(`/orders/${orderId}/cancel`);
      addToast(
        "success",
        "Đã huỷ đơn hàng",
        "Đơn hàng đã được huỷ và sản phẩm đã được trả lại kho."
      );
      navigate("/");
    } catch (err) {
      console.error("Cancel order failed:", err);
      const msg = err?.response?.data?.message;
      addToast(
        "error",
        "Huỷ đơn thất bại",
        msg || "Không thể huỷ đơn hàng. Vui lòng thử lại."
      );
      navigate("/");
    } finally {
      setLoading(false);
    }
  };

  const handleKeepOrder = () => {
    setConfirmOpen(false);
    // Tuỳ bạn: chuyển về lịch sử đơn hoặc homepage
    navigate("/orders");
  };

  return (
    <main className="sf-cart-page">
      <div className="toast-container">
        {toasts.map((t) => (
          <Toast key={t.id} toast={t} onRemove={() => {}} />
        ))}
      </div>

      {/* Nếu thiếu orderId thì báo lỗi đơn giản */}
      {!orderId && (
        <div style={{ padding: 24, textAlign: "center" }}>
          <p>Không tìm thấy mã đơn hàng. Quay lại trang chủ.</p>
          <Link to="/" className="sf-btn sf-btn-primary">
            Về trang chủ
          </Link>
        </div>
      )}

      {orderId && (
        <ConfirmDialog
          isOpen={confirmOpen}
          title="Huỷ thanh toán và huỷ đơn hàng?"
          message="Bạn đã rời khỏi trang thanh toán. Bạn có muốn huỷ luôn đơn hàng này và trả sản phẩm về kho?"
          onConfirm={handleCancelOrder}
          onCancel={handleKeepOrder}
          confirmText={loading ? "Đang huỷ..." : "Huỷ đơn hàng"}
          cancelText="Giữ đơn"
        />
      )}
    </main>
  );
};

export default PaymentCancelPage;
