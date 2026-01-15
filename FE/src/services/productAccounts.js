import axiosClient from "../api/axiosClient";

const PRODUCT_ACCOUNT_ENDPOINTS = {
  ROOT: "/ProductAccount",
  BY_ID: (id) => `/ProductAccount/${id}`,
  PASSWORD: (id) => `/ProductAccount/${id}/password`,
  CUSTOMERS: (id) => `/ProductAccount/${id}/customers`,
  REMOVE_CUSTOMER: (id) => `/ProductAccount/${id}/customers/remove`,
  HISTORY: (id) => `/ProductAccount/${id}/history`,
  HISTORY: (id) => `/ProductAccount/${id}/history`,
  EXTEND_EXPIRY: (id) => `/ProductAccount/${id}/extend-expiry`,
  EDIT_EXPIRY: (id) => `/ProductAccount/${id}/edit-expiry`,
};

export const ProductAccountApi = {
  // Get list of product accounts with filters and pagination
  list: (params) => axiosClient.get(PRODUCT_ACCOUNT_ENDPOINTS.ROOT, { params }),

  // Get a specific product account by ID (password masked)
  get: (id) => axiosClient.get(PRODUCT_ACCOUNT_ENDPOINTS.BY_ID(id)),

  // Get decrypted password for a product account (requires authorization)
  getPassword: (id) => axiosClient.get(PRODUCT_ACCOUNT_ENDPOINTS.PASSWORD(id)),

  // Create a new product account
  create: (data) => axiosClient.post(PRODUCT_ACCOUNT_ENDPOINTS.ROOT, data),

  // Update an existing product account
  update: (id, data) =>
    axiosClient.put(PRODUCT_ACCOUNT_ENDPOINTS.BY_ID(id), data),

  // Delete a product account
  delete: (id) => axiosClient.delete(PRODUCT_ACCOUNT_ENDPOINTS.BY_ID(id)),

  // Add a customer to a product account
  addCustomer: (id, data) =>
    axiosClient.post(PRODUCT_ACCOUNT_ENDPOINTS.CUSTOMERS(id), data),

  // Remove a customer from a product account
  removeCustomer: (id, data) =>
    axiosClient.post(PRODUCT_ACCOUNT_ENDPOINTS.REMOVE_CUSTOMER(id), data),

  // Get history of a product account
  getHistory: (id) => axiosClient.get(PRODUCT_ACCOUNT_ENDPOINTS.HISTORY(id)),

  // Extend expiry date of a product account
  extendExpiry: (id, data) =>
    axiosClient.post(PRODUCT_ACCOUNT_ENDPOINTS.EXTEND_EXPIRY(id), data),

  // Manually edit expiry date (admin override)
  editExpiryDate: (id, data) =>
    axiosClient.put(PRODUCT_ACCOUNT_ENDPOINTS.EDIT_EXPIRY(id), data),

  // Get product accounts expiring soon (within specified days)
  getExpiringSoon: (days = 5) =>
    axiosClient.get(`${PRODUCT_ACCOUNT_ENDPOINTS.ROOT}/expiring-soon`, {
      params: { days },
    }),

  // Get expired product accounts
  getExpired: (params) =>
    axiosClient.get(`${PRODUCT_ACCOUNT_ENDPOINTS.ROOT}/expired`, { params }),
};
