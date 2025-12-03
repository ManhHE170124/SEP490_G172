import axiosClient from "./axiosClient";

const supportPlanPaymentApi = {
  createPayOSPayment: (supportPlanId) =>
    axiosClient.post("/payments/payos/create-support-plan", {
      supportPlanId,
    }),

  confirmSupportPlanPayment: (payload) =>
    axiosClient.post("/supportplans/confirm-payment", payload),
};

export default supportPlanPaymentApi;
