/**
 * File: axiosClient.js
 * Purpose: Axios instance with auth header and normalized error messages.
 */
import axios from "axios";

const baseURL =
  process.env.REACT_APP_API_URL // CRA
  || import.meta?.env?.VITE_API_BASE_URL // Vite (phòng hờ)
  || "https://localhost:7292/api"; // fallback theo port mới của bạn

console.log("[axiosClient] baseURL =", baseURL);

const axiosClient = axios.create({
  baseURL,
  timeout: 15000,
  headers: { "Content-Type": "application/json" },
});

axiosClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem("token");
    if (token) config.headers.Authorization = `Bearer ${token}`;
    // log full URL để so khớp nhanh
    const fullUrl = (config.baseURL || "") + (config.url || "");
    console.log("[API REQUEST]", config.method?.toUpperCase(), fullUrl);
    return config;
  },
  (error) => Promise.reject(error)
);

axiosClient.interceptors.response.use(
  (res) => res.data ?? res,
  (error) => {
    console.error("[API ERROR]", {
      url: error.config?.baseURL + error.config?.url,
      method: error.config?.method,
      code: error.code,
      status: error.response?.status,
      data: error.response?.data,
      message: error.message,
    });
    // ERR_CONNECTION_REFUSED / ERR_NETWORK -> không vào được server
    if (error.code === "ERR_NETWORK") {
      return Promise.reject(new Error("Lỗi kết nối đến máy chủ"));
    }
    const message =
      error.response?.data?.message ||
      error.response?.statusText ||
      "Lỗi kết nối đến máy chủ";
    return Promise.reject(new Error(message));
  }
);

export default axiosClient;
