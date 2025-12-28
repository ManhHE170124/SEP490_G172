import axiosClient from "./axiosClient";

const supportPlanPaymentApi = {
  createPayOSPayment: (supportPlanId) =>
    axiosClient.post("/payments/payos/create-support-plan", {
      supportPlanId,
    }),

  // ✅ FE gọi endpoint này để xem trạng thái payment sau khi PayOS redirect
  // Tránh race-condition: webhook có thể đến chậm => payment vẫn Pending vài giây
  confirmSupportPlanPaymentFromReturn: (paymentId) =>
    axiosClient.post("/payments/support-plan/confirm-from-return", {
      paymentId,
    }),

  // ✅ Khi user bấm "Cancel" trên PayOS (hoặc redirect cancel=true)
  cancelSupportPlanPaymentFromReturn: (paymentId) =>
    axiosClient.post("/payments/support-plan/cancel-from-return", {
      paymentId,
    }),

  // ✅ Endpoint cũ: dùng để final-confirm/tạo subscription (nếu BE flow đang dùng)
  confirmSupportPlanPayment: (payload) =>
    axiosClient.post("/supportplans/confirm-payment", payload),
};

export default supportPlanPaymentApi;
