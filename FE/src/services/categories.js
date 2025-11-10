import axiosClient from "../api/axiosClient";

const CATEGORY_ENDPOINTS = {
  ROOT: "categories",
  EXPORT: "categories/export.csv",
  IMPORT: "categories/import.csv",
};

export const CategoryApi = {
  // Trả về mảng categories
 list: async (params = {}) => {
    const data = await axiosClient.get(CATEGORY_ENDPOINTS.ROOT, { params });
    // data có thể là [] hoặc { items: [...] } hoặc { data: [...] } hoặc { result: [...] }
    if (Array.isArray(data)) return data;
    if (Array.isArray(data?.items)) return data.items;
    if (Array.isArray(data?.data)) return data.data;
    if (Array.isArray(data?.result)) return data.result;
    return []; // fallback an toàn
  },

  listPaged: async (params = {}) => {
    const data = await axiosClient.get(CATEGORY_ENDPOINTS.ROOT, { params });
    if (Array.isArray(data)) {
      const items = data;
      const pageNumber = params.page || params.pageNumber || 1;
      const pageSize = params.pageSize || items.length;
      return { items, total: items.length, pageNumber, pageSize };
    }
    // Nếu BE trả sẵn { items, total, ... } thì trả nguyên
    return data;
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
