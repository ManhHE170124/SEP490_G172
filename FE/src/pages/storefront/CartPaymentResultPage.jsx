// File: src/pages/storefront/CartPaymentResultPage.jsx
import React, { useEffect, useMemo, useState, useCallback } from "react";
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

const CHECKOUT_LOCK_KEY = "ktk_checkout_lock";

const clearCheckoutLock = () => {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.removeItem(CHECKOUT_LOCK_KEY);
  } catch {}
};

const saveCheckoutLock = ({ paymentId, orderId, paymentUrl, expiresAtUtc }) => {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(
      CHECKOUT_LOCK_KEY,
      JSON.stringify({ paymentId, orderId, paymentUrl, expiresAtUtc })
    );
  } catch {}
};

const CartPaymentResultPage = () => {
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

  const confirmOnce = useCallback(async () => {
    if (!paymentId) return null;
    const res = await StorefrontPayOSReturnApi.confirmOrderPaymentFromReturn(paymentId);
    const data = res?.data ?? res;

    setStatus(data?.status || "");
    setOrderId(data?.orderId || data?.targetId || "");

    return data;
  }, [paymentId]);

  useEffect(() => {
    if (!paymentId) {
      addToast("warning", "Thiếu thông tin thanh toán", "Không tìm thấy paymentId từ cổng thanh toán.");
      return;
    }

    let cancelled = false;

    const run = async () => {
      setLoading(true);
      try {
        const data = await confirmOnce();
        if (cancelled) return;

        const s = normalizeStatus(data?.status);

        if (s === "pending" || s === "pendingpayment" || s === "processing") {
          for (let i = 0; i < 8; i++) {
            await new Promise((r) => setTimeout(r, 1500));
            const d2 = await confirmOnce();
            if (cancelled) return;

            const s2 = normalizeStatus(d2?.status);
            if (s2 && s2 !== "pending" && s2 !== "pendingpayment" && s2 !== "processing") break;
          }
        }
      } catch (err) {
        console.error("Confirm payment from return failed:", err);
        addToast("error", "Không thể xác nhận thanh toán", err?.message || "Vui lòng thử tải lại trang.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    run();
    return () => {
      cancelled = true;
    };
  }, [paymentId, addToast, confirmOnce]);

  const s = normalizeStatus(status);
  const isPaid = s === "paid" || s === "success";
  const isCancelled = s === "cancelled" || s === "canceled";
  const isTimeout = s === "timeout" || s === "expired";
  const isPending = !s || s === "pending" || s === "pendingpayment" || s === "processing";

  useEffect(() => {
    const st = normalizeStatus(status);
    if (!st) return;

    const terminal =
      st === "paid" ||
      st === "success" ||
      st === "failed" ||
      st === "cancelled" ||
      st === "canceled" ||
      st === "timeout" ||
      st === "expired" ||
      st === "refunded";

    if (terminal) clearCheckoutLock();
  }, [status]);

  const title = isPaid
    ? "Thanh toán thành công"
    : isCancelled
    ? "Thanh toán đã bị huỷ"
    : isTimeout
    ? "Thanh toán đã hết hạn"
    : isPending
    ? "Đang xác nhận thanh toán..."
    : "Trạng thái thanh toán";

  const subtitle = isPaid
    ? "Hệ thống sẽ gửi key/tài khoản qua email. Nếu chưa thấy ngay, hãy đợi 1–2 phút và kiểm tra Spam/Promotions."
    : isPending
    ? "Nếu bạn vừa thanh toán xong, hệ thống có thể cần vài giây để xác nhận. Bạn có thể bấm “Xác nhận lại”."
    : "Bạn có thể quay lại đơn hàng để thanh toán lại hoặc liên hệ hỗ trợ.";

  const handleReConfirm = async () => {
    try {
      setLoading(true);
      await confirmOnce();
    } catch (err) {
      addToast("error", "Xác nhận lại thất bại", err?.message || "Vui lòng thử lại.");
    } finally {
      setLoading(false);
    }
  };

  const handlePayAgain = async () => {
    if (!orderId) {
      addToast("warning", "Thiếu mã đơn hàng", "Không tìm thấy orderId để tạo link thanh toán lại.");
      return;
    }
    try {
      setLoading(true);
      const res = await StorefrontPaymentApi.createPayOSPayment(orderId);

      if (res?.paymentUrl) {
        // createPayOSPayment hiện chưa trả expiresAtUtc => set TTL 5 phút để lock multi-tab
        const expiresAtUtc = new Date(Date.now() + 5 * 60 * 1000).toISOString();
        saveCheckoutLock({
          paymentId: res.paymentId,
          orderId: res.orderId || orderId,
          paymentUrl: res.paymentUrl,
          expiresAtUtc,
        });

        window.location.href = res.paymentUrl;
      }
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

          {isPending && (
            <p style={{ marginTop: 10, color: "#6b7280" }}>
              {loading ? "Đang kiểm tra trạng thái..." : "Bạn có thể xác nhận lại nếu đã thanh toán."}
            </p>
          )}

          <div className="sf-cart-empty-actions" style={{ gap: 10, display: "flex", flexWrap: "wrap" }}>
            <Link className="sf-btn sf-btn-primary" to="/products">
              Tiếp tục mua sắm
            </Link>

            <Link className="sf-btn sf-btn-outline" to="/orders">
              Xem đơn hàng
            </Link>

            {isPending && (
              <button className="sf-btn sf-btn-outline" type="button" onClick={handleReConfirm} disabled={loading}>
                Xác nhận lại
              </button>
            )}

            {!isPaid && orderId && (
              <button className="sf-btn sf-btn-outline" type="button" onClick={handlePayAgain} disabled={loading}>
                Thanh toán lại
              </button>
            )}

            <Link className="sf-btn sf-btn-outline" to="/support/tickets">
              Liên hệ hỗ trợ
            </Link>
          </div>
        </div>
      </div>
    </main>
  );
};

export default CartPaymentResultPage;
