import axiosClient from "../api/axiosClient";

const BADGE_ENDPOINTS = {
  ROOT: "badges",
  PRODUCT: "badges/products", // dùng badges/products/{productId}
};

export const BadgesApi = {
  list: (params = {}) => axiosClient.get(BADGE_ENDPOINTS.ROOT, { params }),
  get: (code) => axiosClient.get(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}`),
  create: (payload) => axiosClient.post(BADGE_ENDPOINTS.ROOT, payload),
  update: (code, payload) =>
    axiosClient.put(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}`, payload),
  remove: (code) =>
    axiosClient.delete(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}`),
  toggle: (code) =>
    axiosClient.patch(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}/toggle`),

  // Nếu BE nhận JSON { active: boolean }
  setStatus: (code, active) =>
    axiosClient.patch(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}/status`, { active }),

  // Gán nhiều badge codes cho 1 product
  setForProduct: (productId, codes) =>
    axiosClient.post(`${BADGE_ENDPOINTS.PRODUCT}/${productId}`, codes),
};

export default BadgesApi;
