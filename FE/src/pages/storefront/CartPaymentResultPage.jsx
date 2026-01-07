// File: src/pages/storefront/CartPaymentResultPage.jsx
import React, { useEffect, useMemo } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import StorefrontPayOSReturnApi from "../../services/storefrontPayOSReturnService";
import "./StorefrontCartPage.css";

const CHECKOUT_LOCK_KEY = "ktk_checkout_lock";

const clearCheckoutLock = () => {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.removeItem(CHECKOUT_LOCK_KEY);
  } catch {}
};

const useQuery = () => {
  const { search } = useLocation();
  return useMemo(() => new URLSearchParams(search), [search]);
};

const hasLoggedInUser = () => {
  if (typeof window === "undefined") return false;
  try {
    const raw = window.localStorage.getItem("user");
    if (!raw) return false;
    const parsed = JSON.parse(raw);
    // chỉ cần có object là coi như đã đăng nhập (tránh phụ thuộc schema cụ thể)
    return !!parsed && typeof parsed === "object";
  } catch {
    try {
      window.localStorage.removeItem("user");
    } catch {}
    return false;
  }
};

const CartPaymentResultPage = () => {
  const query = useQuery();
  const navigate = useNavigate();

  const paymentId = query.get("paymentId") || "";

  useEffect(() => {
    let cancelled = false;

    const run = async () => {
      try {
        if (paymentId) {
          await StorefrontPayOSReturnApi.confirmOrderPaymentFromReturn(paymentId);
        }
      } catch (err) {
        // không show gì theo yêu cầu, chỉ log để debug
        console.error("Confirm payment from return failed:", err);
      } finally {
        clearCheckoutLock();

        const target = hasLoggedInUser() ? "/profile" : "/";
        if (!cancelled) {
          // redirect nhanh, không để back quay lại trang return
          navigate(target, { replace: true });
        }
      }
    };

    run();

    return () => {
      cancelled = true;
    };
  }, [paymentId, navigate]);

  // Không hiển thị gì (chỉ “nháy” rồi redirect)
  return <main className="sf-cart-page" />;
};

export default CartPaymentResultPage;
