/**
 * File: orderApi.js
 * Purpose: REST client for Order endpoints.
 *          Provides API methods for managing orders, order history, and order details.
 * Endpoints:
 *   - GET    /api/orders                 : List all orders (admin)
 *   - GET    /api/orders/history         : Get order history for current user
 *   - GET    /api/orders/{id}            : Get order by id
 *   - POST   /api/orders                 : Create an order
 *   - PUT    /api/orders/{id}            : Update an order
 *   - DELETE /api/orders/{id}            : Delete an order
 *   - GET    /api/orders/{id}/details    : Get order details
 *   - GET    /api/orders/{id}/payments   : Get payments for an order
 */
import axiosClient from "../api/axiosClient";

const END = { ORDERS: "orders" };

const buildQuery = (p = {}) =>
  Object.entries(p)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(([k, v]) => {
      // Handle Date objects
      if (v instanceof Date) {
        return `${encodeURIComponent(k)}=${encodeURIComponent(v.toISOString())}`;
      }
      return `${encodeURIComponent(k)}=${encodeURIComponent(v)}`;
    })
    .join("&");

export const orderApi = {
  /**
   * List all orders (admin) - filtering done in FE
   * @returns {Promise} Axios response with list of OrderListItemDTO
   */
  list: () => {
    return axiosClient.get(END.ORDERS);
  },

  /**
   * Get order history for current user
   * @param {string|Guid} userId - User identifier
   * @returns {Promise} Axios response with list of OrderHistoryItemDTO
   */
  history: (userId) => {
    if (!userId) {
      return Promise.reject(new Error("UserId is required"));
    }
    return axiosClient.get(`${END.ORDERS}/history`, {
      params: { userId }
    });
  },

  /**
   * Get order by id
   * @param {string|Guid} id - Order identifier
   * @returns {Promise} Axios response with OrderDTO
   */
  get: (id) => axiosClient.get(`${END.ORDERS}/${id}`),

  /**
   * Create a new order
   * @param {Object} data - CreateOrderDTO
   * @returns {Promise} Axios response with OrderDTO
   */
  create: (data) => axiosClient.post(END.ORDERS, data),

  /**
   * Update an order
   * @param {string|Guid} id - Order identifier
   * @param {Object} data - UpdateOrderDTO
   * @returns {Promise} Axios response
   */
  update: (id, data) => axiosClient.put(`${END.ORDERS}/${id}`, data),

  /**
   * Delete an order
   * @param {string|Guid} id - Order identifier
   * @returns {Promise} Axios response
   */
  delete: (id) => axiosClient.delete(`${END.ORDERS}/${id}`),

  /**
   * Get order details for an order
   * @param {string|Guid} id - Order identifier
   * @returns {Promise} Axios response with list of OrderDetailDTO
   */
  getDetails: (id) => axiosClient.get(`${END.ORDERS}/${id}/details`),

  /**
   * Get payments for an order
   * @param {string|Guid} id - Order identifier
   * @returns {Promise} Axios response with list of PaymentDTO
   */
  getPayments: (id) => axiosClient.get(`${END.ORDERS}/${id}/payments`),
};

