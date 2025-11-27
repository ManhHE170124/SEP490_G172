// src/services/storefrontPaymentService.js
import axiosClient from "../api/axiosClient";

/**
 * Service làm việc với thanh toán (Payment) phía storefront.
 * Luồng chính: tạo Payment PayOS từ 1 Order đã Pending.
 */
const StorefrontPaymentApi = {
  /**
   * Tạo Payment Pending + lấy PayOS checkoutUrl cho 1 Order.
   * Backend: POST /api/payments/payos/create
   * @param {string} orderId
   * @returns {Promise<{orderId: string, paymentId: string | null, paymentUrl: string}>}
   */
  createPayOSPayment: async (orderId) => {
    if (!orderId) {
      throw new Error("orderId is required");
    }

    const payload = { orderId };
    const axiosRes = await axiosClient.post("/payments/payos/create", payload);
    const raw = axiosRes?.data ?? axiosRes;

    // Chấp nhận cả camelCase và PascalCase từ backend
    const paymentUrl = raw.paymentUrl || raw.PaymentUrl || "";
    const orderIdResp = raw.orderId || raw.OrderId || orderId;
    const paymentId = raw.paymentId || raw.PaymentId || null;

    if (!paymentUrl) {
      // Ném lỗi để FE show toast thay vì redirect sang /undefined
      throw new Error("paymentUrl is empty in /payments/payos/create response");
    }

    return {
      orderId: orderIdResp,
      paymentId,
      paymentUrl,
    };
  },
};

export default StorefrontPaymentApi;
