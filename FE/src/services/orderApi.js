// File: src/services/orderApi.js
import axiosClient from "../api/axiosClient";

const END = {
  ORDERS: "orders",
};

const unwrap = (res) => res?.data ?? res;

const normalizePaged = (res) => {
  const data = unwrap(res) || {};
  return {
    pageIndex: data.pageIndex ?? data.PageIndex ?? 1,
    pageSize: data.pageSize ?? data.PageSize ?? 20,
    totalItems: data.totalItems ?? data.TotalItems ?? 0,
    items: Array.isArray(data.items ?? data.Items) ? (data.items ?? data.Items) : [],
  };
};

const listPaged = (params = {}) => {
  return axiosClient.get(END.ORDERS, { params }).then(normalizePaged);
};

export const orderApi = {
  /**
   * ✅ Admin list (paged)
   * params (BE): search, createdFrom, createdTo, orderStatus, minTotal, maxTotal, sortBy, sortDir, pageIndex, pageSize
   * response: { pageIndex, pageSize, totalItems, items }
   */
  listPaged,

  /** (compat) trả items[] thôi (nếu nơi khác đang dùng) */
  list: (params = {}) => listPaged(params).then((x) => x.items),

  /** Fetch paginated order history for a specific user */
  history: (userId, params = {}) => {
    if (!userId) return Promise.reject(new Error("UserId is required"));
    return axiosClient.get(`${END.ORDERS}/history`, { params: { userId, ...params } });
  },

  /**
   * ✅ Admin detail: BE trả { order, orderItems, pageIndex, pageSize, totalItems }
   * query: { includePaymentAttempts, includeCheckoutUrl, search, minPrice, maxPrice, sortBy, sortDir, pageIndex, pageSize }
   */
  get: (id, params) => {
    const cfg = params ? { params } : undefined;
    return axiosClient.get(`${END.ORDERS}/${id}`, cfg);
  },

  /**
   * ✅ Paged items-only (ổn định cho màn chi tiết nếu muốn gọi riêng /details)
   */
  detailsPaged: (id, params = {}) => {
    return axiosClient.get(`${END.ORDERS}/${id}/details`, { params }).then(normalizePaged);
  },

  /** (compat) raw axios response */
  getDetails: (id, params) => {
    const cfg = params ? { params } : undefined;
    return axiosClient.get(`${END.ORDERS}/${id}/details`, cfg);
  },

  getDetailCredentials: (orderId, orderDetailId, params) => {
    const cfg = params ? { params } : undefined;
    return axiosClient.get(`${END.ORDERS}/${orderId}/details/${orderDetailId}/credentials`, cfg);
  },
};
