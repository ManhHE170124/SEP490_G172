import api from "../apiClient"

export const CategoryApi = {
  list: (params={}) => api.get("/categories", { params }).then(r => r.data),
  get: (id) => api.get(`/categories/${id}`).then(r => r.data),
  create: (payload) => api.post("/categories", payload).then(r => r.data),
  update: (id, payload) => api.put(`/categories/${id}`, payload).then(r => r.data),
  remove: (id) => api.delete(`/categories/${id}`).then(r => r.data),
  toggle: (id) => api.patch(`/categories/${id}/toggle`).then(r => r.data),
};
export const CategoryCsv = {
  exportCsv: () =>
    api.get("/categories/export.csv", { responseType: "blob" }).then(r => r.data),

  importCsv: (file) => {
    const form = new FormData();
    form.append("file", file);
    return api.post("/categories/import.csv", form, {
      headers: { "Content-Type": "multipart/form-data" },
    }).then(r => r.data);
  },
};
