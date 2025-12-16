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
   * Fetch paginated order history for a specific user
   */
  history: (userId, params = {}) => {
    if (!userId) {
      return Promise.reject(new Error("UserId is required"));
    }
    return axiosClient.get(`${END.ORDERS}/history`, {
      params: { userId, ...params },
    });
  },

  /**
   * Xem chi tiết 1 đơn (OrderDTO)
   * ✅ Cho phép truyền query params để khớp controller mới:
   * { includePaymentAttempts, includeCheckoutUrl, ... }
   */
  get: (id, params) => {
    const cfg = params ? { params } : undefined;
    return axiosClient.get(`${END.ORDERS}/${id}`, cfg);
  },

  /**
   * Chỉ lấy phần chi tiết items của 1 đơn
   */
  getDetails: (id, params) => {
    const cfg = params ? { params } : undefined;
    return axiosClient.get(`${END.ORDERS}/${id}/details`, cfg);
  },

  getDetailCredentials: (orderId, orderDetailId, params) => {
    const cfg = params ? { params } : undefined;
    return axiosClient.get(
      `${END.ORDERS}/${orderId}/details/${orderDetailId}/credentials`,
      cfg
    );
  },
};
