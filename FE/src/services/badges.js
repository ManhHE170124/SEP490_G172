import api from "../apiClient"

export const BadgesApi = {
  list: (params = {}) => api.get("/badges", { params }).then(r => r.data),
  get: (code) => api.get(`/badges/${encodeURIComponent(code)}`).then(r => r.data),
  create: (payload) => api.post(`/badges`, payload).then(r => r.data),
  update: (code, payload) => api.put(`/badges/${encodeURIComponent(code)}`, payload).then(r => r.data),
  remove: (code) => api.delete(`/badges/${encodeURIComponent(code)}`).then(r => r.data),
  toggle: (code) => api.patch(`/badges/${encodeURIComponent(code)}/toggle`).then(r => r.data),
  setStatus: (code, active) => api.patch(`/badges/${encodeURIComponent(code)}/status`, JSON.stringify(active), { headers: { 'Content-Type': 'application/json' } }).then(r => r.data),
  setForProduct: (productId, codes) => api.post(`/badges/products/${productId}`, codes).then(r => r.data),
}
