import axios from "axios"

const base = (import.meta.env?.VITE_API_BASE_URL || "http://localhost:5000").replace(/\/$/, "")
const prefix = import.meta.env?.VITE_API_PREFIX || "/api"

export const API_BASE = base + prefix

const api = axios.create({
  baseURL: API_BASE,
  withCredentials: true
})

api.interceptors.request.use(cfg => {
  const token = localStorage.getItem("access_token")
  if (token) cfg.headers.Authorization = `Bearer ${token}`
  return cfg
})

export default api
