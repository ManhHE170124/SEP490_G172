import axiosClient from "../api/axiosClient";

const SUPPLIER_ENDPOINTS = {
  ROOT: "/supplier",
  CHECK_NAME: "/supplier/check-name",
  VALIDATE_DEACTIVATION: (id) => `/supplier/${id}/validate-deactivation`,
  BY_PRODUCT: (productId) => `/supplier/by-product/${productId}`,
};

export const SupplierApi = {
  // ===== CRUD Operations =====
  list: (params = {}) =>
    axiosClient.get(SUPPLIER_ENDPOINTS.ROOT, { params }),

  get: (id) =>
    axiosClient.get(`${SUPPLIER_ENDPOINTS.ROOT}/${id}`),

  create: (payload) =>
    axiosClient.post(SUPPLIER_ENDPOINTS.ROOT, payload),

  update: (id, payload) =>
    axiosClient.put(`${SUPPLIER_ENDPOINTS.ROOT}/${id}`, payload),

  deactivate: (id, payload) =>
    axiosClient.delete(`${SUPPLIER_ENDPOINTS.ROOT}/${id}`, { data: payload }),

  toggleStatus: (id) =>
    axiosClient.patch(`${SUPPLIER_ENDPOINTS.ROOT}/${id}/toggle-status`),

  // ===== Validation =====
  checkName: (name, excludeId = null) =>
    axiosClient.get(SUPPLIER_ENDPOINTS.CHECK_NAME, {
      params: { name, excludeId }
    }),

  validateDeactivation: (id) =>
    axiosClient.get(SUPPLIER_ENDPOINTS.VALIDATE_DEACTIVATION(id)),

  listByProduct: (productId) =>
    axiosClient.get(SUPPLIER_ENDPOINTS.BY_PRODUCT(productId)),
};

export default SupplierApi;
