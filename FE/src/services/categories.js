import axiosClient from "../api/axiosClient";

const CATEGORY_ENDPOINTS = {
  ROOT: "categories",
  EXPORT: "categories/export.csv",
  IMPORT: "categories/import.csv",
};

export const CategoryApi = {
  // Luôn trả về mảng: res.items ?? res
  list: (params = {}) =>
    axiosClient.get(CATEGORY_ENDPOINTS.ROOT, { params })
      .then((res) => res?.items ?? res ?? []),

  // Trả nguyên object phân trang
  listPaged: (params = {}) =>
    axiosClient.get(CATEGORY_ENDPOINTS.ROOT, { params }),

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
