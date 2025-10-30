import axiosClient from "../api/axiosClient";

const BADGE_ENDPOINTS = {
  ROOT: "badges",
  PRODUCT: "badges/products",
};

export const BadgesApi = {
  // Luôn trả mảng
  list: (params = {}) =>
    axiosClient.get(BADGE_ENDPOINTS.ROOT, { params })
      .then((res) => res?.items ?? res ?? []),

  // Trả phân trang
  listPaged: (params = {}) =>
    axiosClient.get(BADGE_ENDPOINTS.ROOT, { params }),

  get: (code) => axiosClient.get(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}`),
  create: (payload) => axiosClient.post(BADGE_ENDPOINTS.ROOT, payload),
  update: (code, payload) => axiosClient.put(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}`, payload),
  remove: (code) => axiosClient.delete(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}`),
  toggle: (code) => axiosClient.patch(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}/toggle`),

  // BE yêu cầu body là bool thuần (không phải {active})
  setStatus: (code, active) =>
    axiosClient.patch(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}/status`, active, {
      headers: { "Content-Type": "application/json" },
    }),

  setForProduct: (productId, codes) =>
    axiosClient.post(`${BADGE_ENDPOINTS.PRODUCT}/${productId}`, codes),
};
