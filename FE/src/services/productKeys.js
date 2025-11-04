import axiosClient from "../api/axiosClient";

const PRODUCT_KEY_ENDPOINTS = {
  ROOT: "/ProductKey",
  BY_ID: (id) => `/ProductKey/${id}`,
  ASSIGN: "/ProductKey/assign",
  UNASSIGN: (id) => `/ProductKey/${id}/unassign`,
  BULK_UPDATE: "/ProductKey/bulk-update-status",
  EXPORT: "/ProductKey/export",
};

export const ProductKeyApi = {
  // Get list of product keys with filters and pagination
  list: (params) => axiosClient.get(PRODUCT_KEY_ENDPOINTS.ROOT, { params }),

  // Get a specific product key by ID
  get: (id) => axiosClient.get(PRODUCT_KEY_ENDPOINTS.BY_ID(id)),

  // Create a new product key
  create: (data) => axiosClient.post(PRODUCT_KEY_ENDPOINTS.ROOT, data),

  // Update an existing product key
  update: (id, data) => axiosClient.put(PRODUCT_KEY_ENDPOINTS.BY_ID(id), data),

  // Delete a product key
  delete: (id) => axiosClient.delete(PRODUCT_KEY_ENDPOINTS.BY_ID(id)),

  // Assign key to order
  assignToOrder: (data) => axiosClient.post(PRODUCT_KEY_ENDPOINTS.ASSIGN, data),

  // Unassign key from order
  unassignFromOrder: (id) =>
    axiosClient.post(PRODUCT_KEY_ENDPOINTS.UNASSIGN(id)),

  // Bulk update status
  bulkUpdateStatus: (data) =>
    axiosClient.post(PRODUCT_KEY_ENDPOINTS.BULK_UPDATE, data),

  // Export to CSV
  export: (params) =>
    axiosClient.get(PRODUCT_KEY_ENDPOINTS.EXPORT, {
      params,
      responseType: "blob",
    }),
};
