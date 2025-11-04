import axiosClient from "../api/axiosClient";

const CATEGORY_ENDPOINTS = {
  ROOT: "categories",
  EXPORT: "categories/export.csv",
  IMPORT: "categories/import.csv",
};

export const CategoryApi = {
  // Trả về mảng categories
  list: (params = {}) => axiosClient.get(CATEGORY_ENDPOINTS.ROOT, { params }),

  // Trả về dạng phân trang: { items, total, pageNumber, pageSize }
  listPaged: async (params = {}) => {
    const res = await axiosClient.get(CATEGORY_ENDPOINTS.ROOT, { params });

    // BE hiện tại đang trả về mảng thuần => tự bọc lại
    if (Array.isArray(res)) {
      const items = res;
      const pageNumber = params.page || params.pageNumber || 1;
      const pageSize = params.pageSize || items.length;

      return {
        items,
        total: items.length,
        pageNumber,
        pageSize,
      };
    }

    // Nếu sau này BE trả về dạng { items, total, ... } thì dùng luôn
    return res;
  },

  get: (id) => axiosClient.get(`${CATEGORY_ENDPOINTS.ROOT}/${id}`),
  create: (payload) => axiosClient.post(CATEGORY_ENDPOINTS.ROOT, payload),
  update: (id, payload) => axiosClient.put(`${CATEGORY_ENDPOINTS.ROOT}/${id}`, payload),
  remove: (id) => axiosClient.delete(`${CATEGORY_ENDPOINTS.ROOT}/${id}`),
  toggle: (id) => axiosClient.patch(`${CATEGORY_ENDPOINTS.ROOT}/${id}/toggle`),
};

export const CategoryCsv = {
  exportCsv: () =>
    axiosClient.get(CATEGORY_ENDPOINTS.EXPORT, { responseType: "blob" }),

  importCsv: (file) => {
    const form = new FormData();
    form.append("file", file);
    return axiosClient.post(CATEGORY_ENDPOINTS.IMPORT, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
};
