import React, { useEffect, useState } from "react";
import { useLocation, useNavigate, Link } from "react-router-dom";
import axiosClient from "../../api/axiosClient";

// Helper: decode payload của JWT (base64url)
function decodeJwtPayload(token) {
  try {
    const parts = token.split(".");
    if (parts.length < 2) return null;

    const base64Url = parts[1];
    const base64 = base64Url.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64.padEnd(
      base64.length + ((4 - (base64.length % 4)) % 4),
      "="
    );
    const json = atob(padded);
    return JSON.parse(json);
  } catch (err) {
    console.error("Decode JWT payload failed:", err);
    return null;
  }
}

// Helper: lấy accessToken từ axiosClient hoặc localStorage
function getAccessTokenFromClient() {
  try {
    // Ưu tiên đọc từ header của axiosClient (nơi bạn thường set "Bearer xxx")
    const authHeader =
      axiosClient.defaults.headers.common["Authorization"] ||
      axiosClient.defaults.headers.common["authorization"];

    if (authHeader && typeof authHeader === "string") {
      const parts = authHeader.split(" ");
      if (parts.length === 2 && /^Bearer$/i.test(parts[0])) {
        return parts[1];
      }
    }

    // Fallback: thử một số key phổ biến trong localStorage
    const possibleKeys = ["accessToken", "AccessToken", "ktk_access_token"];
    for (const key of possibleKeys) {
      const token = localStorage.getItem(key);
      if (token) return token;
    }

    return null;
  } catch (err) {
    console.error("Get access token from client failed:", err);
    return null;
  }
}

// Helper: check user đã đăng nhập hay chưa dựa trên accessToken + exp
function isUserLoggedIn() {
  try {
    const token = getAccessTokenFromClient();
    if (!token) return false;

    const payload = decodeJwtPayload(token);
    if (!payload || !payload.exp) return false;

    const nowSeconds = Date.now() / 1000;
    // exp là unix time (seconds) do BE set khi generate JWT
    return payload.exp > nowSeconds;
  } catch (err) {
    console.error("Check login from token failed:", err);
    return false;
  }
}

const PaymentResultPage = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const searchParams = new URLSearchParams(location.search);

  const paymentId = searchParams.get("paymentId"); // flow Cart
  const payStatus = searchParams.get("status");
  const payCode = searchParams.get("code");

  const [status, setStatus] = useState("loading"); // loading | paid | cancelled | pending | error | invalid
  const [message, setMessage] = useState("");

  useEffect(() => {
    // Không có paymentId -> invalid
    if (!paymentId) {
      setStatus("invalid");
      setMessage("Không tìm thấy thông tin thanh toán.");
      return;
    }

    let cancelled = false;

    const run = async () => {
      try {
        const res = await axiosClient.post(
          "/payments/cart/confirm-from-return",
          {
            paymentId,
            code: payCode,
            status: payStatus,
          }
        );

        const backendStatus =
          (res?.data?.status || res?.data?.Status || "").toLowerCase();

        if (backendStatus === "paid") {
          // Thanh toán thành công
          setStatus("paid");
          setMessage(
            "Thanh toán thành công. Cảm ơn bạn đã mua hàng tại Keytietkiem!"
          );

          // Dựa vào JWT accessToken để check đã đăng nhập hay chưa
          const loggedIn = isUserLoggedIn();
          const targetPath = loggedIn ? "/account/profile" : "/";

          // "Nháy" 1 phát rồi chuyển trang (khoảng 500ms)
          setTimeout(() => {
            if (!cancelled) navigate(targetPath);
          }, 500);
        } else if (backendStatus === "cancelled") {
          setStatus("cancelled");
          setMessage(
            "Thanh toán đã bị huỷ hoặc hết hạn. Nếu bạn đã bị trừ tiền, vui lòng liên hệ hỗ trợ."
          );

          // Không lỗi => redirect nhanh (không chờ 3 giây)
          setTimeout(() => {
            if (!cancelled) navigate("/");
          }, 500);
        } else {
          // pending hoặc trạng thái khác nhưng không phải lỗi
          setStatus("pending");
          setMessage(
            "Thanh toán đang được xử lý. Vui lòng kiểm tra lịch sử đơn hàng sau ít phút."
          );

          // Cũng redirect nhanh
          setTimeout(() => {
            if (!cancelled) navigate("/");
          }, 500);
        }
      } catch (err) {
        console.error("Confirm cart payment failed:", err);
        setStatus("error");
        setMessage(
          "Không xác nhận được trạng thái thanh toán. Vui lòng kiểm tra lại sau hoặc liên hệ hỗ trợ."
        );

        // Chỉ khi thật sự lỗi mới cho user đọc 3 giây
        setTimeout(() => {
          if (!cancelled) navigate("/");
        }, 3000);
      }
    };

    run();

    return () => {
      cancelled = true;
    };
  }, [paymentId, payCode, payStatus, navigate]);

  if (status === "invalid") {
    return (
      <main className="sf-cart-page">
        <div style={{ padding: 24, textAlign: "center" }}>
          <p>{message}</p>
          <Link to="/" className="sf-btn sf-btn-primary">
            Về trang chủ
          </Link>
        </div>
      </main>
    );
  }

  return (
    <main className="sf-cart-page">
      <div className="sf-cart-container">
        <div className="sf-cart-breadcrumb">
          <Link to="/">Trang chủ</Link>
          <span>/</span>
          <span>Kết quả thanh toán</span>
        </div>

        <header className="sf-cart-header">
          <h1 className="sf-cart-title">Kết quả thanh toán</h1>
          <p className="sf-cart-subtitle">
            {message || "Đang kiểm tra trạng thái thanh toán..."}
          </p>
        </header>

        {(status === "cancelled" || status === "error") && (
          <div style={{ textAlign: "center", marginTop: 24 }}>
            <Link to="/" className="sf-btn sf-btn-primary">
              Quay lại trang chủ
            </Link>
          </div>
        )}
      </div>
    </main>
  );
};

export default PaymentResultPage;
