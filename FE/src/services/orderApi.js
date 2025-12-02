// services/orderApi.js

import axiosClient from "../api/axiosClient";

const END = {
  ORDERS: "orders",
  PAYMENTS: "payments",
};

export const orderApi = {
  /**
   * List all orders (admin) - filtering done in FE
   * @returns {Promise} Axios response with list of OrderListItemDTO
   */
  list: (params) => {
    return axiosClient.get(END.ORDERS, { params });
  },

  history: (userId) => {
    if (!userId) {
      return Promise.reject(new Error("UserId is required"));
    }
    return axiosClient.get(`${END.ORDERS}/history`, {
      params: { userId },
    });
  },

  get: (id) => axiosClient.get(`${END.ORDERS}/${id}`),

  create: (data) => axiosClient.post(END.ORDERS, data),

  update: (id, data) => axiosClient.put(`${END.ORDERS}/${id}`, data),

  delete: (id) => axiosClient.delete(`${END.ORDERS}/${id}`),

  cancel: (id) => axiosClient.post(`${END.ORDERS}/${id}/cancel`),

  getDetails: (id) => axiosClient.get(`${END.ORDERS}/${id}/details`),
};
