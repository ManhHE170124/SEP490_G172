// src/pages/storefront/PaymentResultPage.jsx
import React, { useEffect, useState } from "react";
import { useLocation, useNavigate, Link } from "react-router-dom";
import axiosClient from "../../api/axiosClient";

const PaymentResultPage = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const searchParams = new URLSearchParams(location.search);
  const orderId = searchParams.get("orderId");

  const [status, setStatus] = useState("loading");
  const [message, setMessage] = useState("");

  useEffect(() => {
    if (!orderId) {
      setStatus("invalid");
      setMessage("Không tìm thấy mã đơn hàng.");
      return;
    }

    let cancelled = false;
    let attempts = 0;

    const fetchStatus = async () => {
      if (cancelled) return;

      try {
        const res = await axiosClient.get(`/orders/${orderId}`);
        const order = res.data ?? res;
        const st = (order.status || order.Status || "").toLowerCase();

        if (st === "paid") {
          setStatus("paid");
          setMessage(
            "Thanh toán thành công. Cảm ơn bạn đã mua hàng tại Keytietkiem!"
          );
          // Sau 3s quay lại homepage
          setTimeout(() => {
            if (!cancelled) navigate("/");
          }, 3000);
        } else if (st === "cancelled") {
          setStatus("cancelled");
          setMessage(
            "Đơn hàng của bạn đã bị huỷ (có thể do bạn huỷ thanh toán hoặc quá thời gian)."
          );
        } else if (st === "pending" && attempts < 5) {
          attempts += 1;
          setTimeout(fetchStatus, 1500);
        } else {
          setStatus("pending");
          setMessage(
            "Thanh toán đang được xử lý. Vui lòng kiểm tra lịch sử đơn hàng sau ít phút."
          );
        }
      } catch (err) {
        console.error("Load order failed:", err);
        setStatus("error");
        setMessage(
          "Không tải được trạng thái đơn hàng. Vui lòng kiểm tra lại sau."
        );
      }
    };

    fetchStatus();

    return () => {
      cancelled = true;
    };
  }, [orderId, navigate]);

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
          <p className="sf-cart-subtitle">{message}</p>
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
