import React, { useState, useCallback, useEffect } from "react";
import { useLocation, useNavigate, Link } from "react-router-dom";
import axiosClient from "../../api/axiosClient";
import Toast from "../../components/Toast/Toast";

const PaymentCancelPage = () => {
  const location = useLocation();
  const navigate = useNavigate();

  const searchParams = new URLSearchParams(location.search);
  const paymentId = searchParams.get("paymentId");  // flow Cart
  const payStatus = searchParams.get("status");
  const payCode = searchParams.get("code");

  const [toasts, setToasts] = useState([]);

  const addToast = useCallback((type, title, message) => {
    const id = Date.now() + Math.random();
    setToasts((prev) => [...prev, { id, type, title, message }]);
    setTimeout(
      () => setToasts((prev) => prev.filter((t) => t.id !== id)),
      4000
    );
  }, []);

  // ===== Flow Cart: có paymentId =====
  useEffect(() => {
    // Không có paymentId -> chỉ hiển thị lỗi, không gọi API
    if (!paymentId) return;

    let cancelled = false;

    const run = async () => {
      try {
        await axiosClient.post("/payments/cart/cancel-from-return", {
          paymentId,
          code: payCode,
          status: payStatus,
        });
      } catch (err) {
        console.error("Cancel cart payment failed:", err);
        addToast(
          "error",
          "Huỷ thanh toán thất bại",
          "Không thể huỷ thanh toán. Vui lòng kiểm tra lại sau."
        );
      } finally {
        if (!cancelled) {
          navigate("/");
        }
      }
    };

    run();

    return () => {
      cancelled = true;
    };
  }, [paymentId, payCode, payStatus, navigate, addToast]);

  return (
    <main className="sf-cart-page">
      <div className="toast-container">
        {toasts.map((t) => (
          <Toast key={t.id} toast={t} onRemove={() => {}} />
        ))}
      </div>

      {/* Flow Cart: đang xử lý huỷ paymentId */}
      {paymentId && (
        <div style={{ padding: 24, textAlign: "center" }}>
          <p>Đang huỷ thanh toán, vui lòng chờ...</p>
        </div>
      )}

      {/* Không có paymentId */}
      {!paymentId && (
        <div style={{ padding: 24, textAlign: "center" }}>
          <p>Không tìm thấy thông tin thanh toán. Quay lại trang chủ.</p>
          <Link to="/" className="sf-btn sf-btn-primary">
            Về trang chủ
          </Link>
        </div>
      )}
    </main>
  );
};

export default PaymentCancelPage;
