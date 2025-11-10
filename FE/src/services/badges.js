import axiosClient from "../api/axiosClient";

const BADGE_ENDPOINTS = {
  ROOT: "badges",
  PRODUCT: "badges/products",
};

export const BadgesApi = {
  // Trả về luôn là array
  list: async (params = {}) => {
    const data = await axiosClient.get(BADGE_ENDPOINTS.ROOT, { params });
    if (Array.isArray(data)) return data;
    if (Array.isArray(data?.items)) return data.items;
    if (Array.isArray(data?.data)) return data.data;
    if (Array.isArray(data?.result)) return data.result;
    return [];
  },

  listPaged: async (params = {}) => {
    const data = await axiosClient.get(BADGE_ENDPOINTS.ROOT, { params });
    if (Array.isArray(data)) {
      const items = data;
      const pageNumber = params.page || params.pageNumber || 1;
      const pageSize = params.pageSize || items.length;
      return { items, total: items.length, pageNumber, pageSize };
    }
    return data;
  },

  get: (code) =>
    axiosClient.get(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}`),
  create: (payload) => axiosClient.post(BADGE_ENDPOINTS.ROOT, payload),
  update: (code, payload) => axiosClient.put(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}`, payload),
  remove: (code) => axiosClient.delete(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}`),
  toggle: (code) => axiosClient.patch(`${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}/toggle`),

  setStatus: (code, active) =>
    axiosClient.patch(
      `${BADGE_ENDPOINTS.ROOT}/${encodeURIComponent(code)}/status`,
      { active }
    ),

  setForProduct: (productId, codes) =>
    axiosClient.post(`${BADGE_ENDPOINTS.PRODUCT}/${productId}`, codes),
};
