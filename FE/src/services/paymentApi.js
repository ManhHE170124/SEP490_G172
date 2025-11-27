// services/paymentApi.js
import axiosClient from "../api/axiosClient";

const END = { PAYMENTS: "payments" };

export const paymentApi = {
  list: (params) => axiosClient.get(END.PAYMENTS, { params }), // status, provider, orderId
  get: (id) => axiosClient.get(`${END.PAYMENTS}/${id}`),
  getByOrder: (orderId) =>
    axiosClient.get(`${END.PAYMENTS}/order/${orderId}`),
  updateStatus: (id, status) =>
    axiosClient.put(`${END.PAYMENTS}/${id}/status`, { status }),
};
