// File: src/services/storefrontPaymentService.js
import axiosClient from "../api/axiosClient";

const StorefrontPaymentApi = {
  createPayOSPayment: async (orderId) => {
    if (!orderId) throw new Error("orderId is required");

    const payload = { orderId };
    const axiosRes = await axiosClient.post("/payments/payos/create", payload);
    const raw = axiosRes?.data ?? axiosRes;

    const paymentUrl =
      raw.paymentUrl ||
      raw.PaymentUrl ||
      raw.checkoutUrl ||
      raw.CheckoutUrl ||
      "";

    const orderIdResp = raw.orderId || raw.OrderId || orderId;
    const paymentId = raw.paymentId || raw.PaymentId || null;

    if (!paymentUrl) {
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
