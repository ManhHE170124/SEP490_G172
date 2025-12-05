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

  /**
   * Fetch paginated order history for a specific user
   * @param {string} userId - Required user ID
   * @param {object} params - Optional filters: keyword, minAmount, maxAmount, fromDate, toDate, status, sortBy, sortDir, page, pageSize
   * @returns {Promise<{items: array, totalItems: number, page: number, pageSize: number, totalPages: number}>}
   */
  history: (userId, params = {}) => {
    if (!userId) {
      return Promise.reject(new Error("UserId is required"));
    }
    return axiosClient.get(`${END.ORDERS}/history`, {
      params: { userId, ...params },
    });
  },

  get: (id) => axiosClient.get(`${END.ORDERS}/${id}`),

  create: (data) => axiosClient.post(END.ORDERS, data),

  update: (id, data) => axiosClient.put(`${END.ORDERS}/${id}`, data),

  delete: (id) => axiosClient.delete(`${END.ORDERS}/${id}`),

  cancel: (id) => axiosClient.post(`${END.ORDERS}/${id}/cancel`),

  getDetails: (id) => axiosClient.get(`${END.ORDERS}/${id}/details`),

  getDetailCredentials: (orderId, orderDetailId) =>
    axiosClient.get(`${END.ORDERS}/${orderId}/details/${orderDetailId}/credentials`),
};
