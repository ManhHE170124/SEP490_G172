// services/orderApi.js 
import axiosClient from "../api/axiosClient";

const END = {
  ORDERS: "orders",
  PAYMENTS: "payments",
};

export const orderApi = {
  /**
   * List all orders (admin) - sorting ở BE
   * params: sortBy, sortDir
   */
  list: (params) => {
    return axiosClient.get(END.ORDERS, { params });
  },

  /**
   * Lịch sử đơn hàng của 1 user
   */
  history: (userId) => {
    if (!userId) {
      return Promise.reject(new Error("UserId is required"));
    }
    return axiosClient.get(`${END.ORDERS}/history`, {
      params: { userId },
    });
  },

  /**
   * Xem chi tiết 1 đơn (OrderDTO)
   */
  get: (id) => axiosClient.get(`${END.ORDERS}/${id}`),

  /**
   * Chỉ lấy phần chi tiết items của 1 đơn
   */
  getDetails: (id) => axiosClient.get(`${END.ORDERS}/${id}/details`),
};
