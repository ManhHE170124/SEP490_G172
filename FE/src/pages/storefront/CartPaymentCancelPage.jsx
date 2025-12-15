// File: src/pages/storefront/CartPaymentCancelPage.jsx
import React, { useMemo, useEffect, useState, useCallback } from "react";
import { Link, useLocation } from "react-router-dom";
import Toast from "../../components/Toast/Toast";
import StorefrontPayOSReturnApi from "../../services/storefrontPayOSReturnService";
import StorefrontPaymentApi from "../../services/storefrontPaymentService";
import "./StorefrontCartPage.css";

const useQuery = () => {
  const { search } = useLocation();
  return useMemo(() => new URLSearchParams(search), [search]);
};

const normalizeStatus = (s) => String(s || "").trim().toLowerCase();

const CartPaymentCancelPage = () => {
  const query = useQuery();
  const paymentId = query.get("paymentId") || "";

  const [toasts, setToasts] = useState([]);
  const [loading, setLoading] = useState(false);

  const [status, setStatus] = useState("");
  const [orderId, setOrderId] = useState("");

  const removeToast = useCallback((id) => setToasts((prev) => prev.filter((t) => t.id !== id)), []);
  const addToast = useCallback(
    (type, title, message) => {
      const id = Date.now() + Math.random();
      setToasts((prev) => [...prev, { id, type, title, message }]);
      setTimeout(() => removeToast(id), 4500);
    },
    [removeToast]
  );

  useEffect(() => {
    if (!paymentId) {
      addToast("warning", "Thiếu paymentId", "Không tìm thấy paymentId từ cổng thanh toán.");
      return;
    }

    let cancelled = false;

    const run = async () => {
      setLoading(true);
      try {
        const res = await StorefrontPayOSReturnApi.cancelOrderPaymentFromReturn(paymentId);
        const data = res?.data ?? res;

        setStatus(data?.status || "");
        setOrderId(data?.orderId || data?.targetId || "");
      } catch (err) {
        console.error("Cancel payment from return failed:", err);
        addToast("error", "Không thể cập nhật trạng thái huỷ", err?.message || "Vui lòng thử tải lại trang.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    run();
    return () => {
      cancelled = true;
    };
  }, [paymentId, addToast]);

  const s = normalizeStatus(status);
  const isPaid = s === "paid" || s === "success";

  const title = isPaid ? "Thanh toán đã được ghi nhận" : "Bạn đã huỷ thanh toán";
  const subtitle = isPaid
    ? "Có thể bạn đã huỷ ở bước cuối nhưng giao dịch vẫn thành công. Hãy kiểm tra email/đơn hàng."
    : "Bạn có thể thanh toán lại từ đơn hàng hoặc tiếp tục mua sắm.";

  const handlePayAgain = async () => {
    if (!orderId) {
      addToast("warning", "Thiếu mã đơn hàng", "Không tìm thấy orderId để tạo link thanh toán lại.");
      return;
    }
    try {
      setLoading(true);
      const res = await StorefrontPaymentApi.createPayOSPayment(orderId);
      if (res?.paymentUrl) window.location.href = res.paymentUrl;
    } catch (err) {
      console.error("Pay again failed:", err);
      addToast("error", "Không thể tạo link thanh toán lại", err?.message || "Vui lòng thử lại sau.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="sf-cart-page">
      <div className="toast-container">
        {toasts.map((t) => (
          <Toast key={t.id} toast={t} onRemove={removeToast} />
        ))}
      </div>

      <div className="sf-cart-container">
        <header className="sf-cart-header">
          <h1 className="sf-cart-title">{title}</h1>
          <p className="sf-cart-subtitle">{subtitle}</p>
        </header>

        <div className="sf-cart-empty" style={{ textAlign: "left" }}>
          <p>
            Mã giao dịch: <strong>{paymentId || "—"}</strong>
          </p>

          {orderId && (
            <p>
              Mã đơn hàng: <strong>{orderId}</strong>
            </p>
          )}

          <div className="sf-cart-empty-actions" style={{ gap: 10, display: "flex", flexWrap: "wrap" }}>
            <Link className="sf-btn sf-btn-primary" to="/products">
              Tiếp tục mua sắm
            </Link>

            <Link className="sf-btn sf-btn-outline" to="/orders">
              Xem đơn hàng
            </Link>

            {!isPaid && orderId && (
              <button className="sf-btn sf-btn-outline" type="button" onClick={handlePayAgain} disabled={loading}>
                Thanh toán lại
              </button>
            )}

            <Link className="sf-btn sf-btn-outline" to="/support/tickets">
              Liên hệ hỗ trợ
            </Link>

            <Link className="sf-btn sf-btn-outline" to="/cart">
              Quay lại giỏ hàng
            </Link>
          </div>
        </div>
      </div>
    </main>
  );
};

export default CartPaymentCancelPage;
