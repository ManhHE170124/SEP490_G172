// services/paymentApi.js
import axiosClient from "../api/axiosClient";

const END = { PAYMENTS: "payments" };

export const paymentApi = {
  // params: status, provider, email, transactionType, sortBy, sortDir, ...
  list: (params = {}) => axiosClient.get(END.PAYMENTS, { params }),

  get: (id) => axiosClient.get(`${END.PAYMENTS}/${id}`),
};
