// File: src/services/storefrontPayOSReturnService.js
import axiosClient from "../api/axiosClient";

/**
 * BE có endpoint confirm/cancel khi người dùng quay về từ PayOS.
 * (giúp cập nhật Payment/Order nhanh hơn thay vì chỉ chờ webhook)
 */
const StorefrontPayOSReturnApi = {
  confirmOrderPaymentFromReturn: async (paymentId) => {
    if (!paymentId) throw new Error("paymentId is required");
    return axiosClient.post("/payments/order/confirm-from-return", { paymentId });
  },

  cancelOrderPaymentFromReturn: async (paymentId) => {
    if (!paymentId) throw new Error("paymentId is required");
    return axiosClient.post("/payments/order/cancel-from-return", { paymentId });
  },
};

export default StorefrontPayOSReturnApi;
