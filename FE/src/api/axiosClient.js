/**
 * File: axiosClient.js
 * Purpose: Axios instance with auth header, automatic token refresh, and normalized error messages.
 */
import axios from "axios";
import qs from "qs";

const baseURL =
  process.env.REACT_APP_API_URL // CRA
  || import.meta.env?.VITE_API_BASE_URL // Vite (phòng hờ)
  || "https://localhost:7292/api"; // fallback theo port mới của bạn

console.log("[axiosClient] baseURL =", baseURL);

/* ====================== CLIENT SESSION ID HELPER ====================== */

const CLIENT_ID_COOKIE_KEY = "ktk_client_id";

/**
 * Đọc cookie theo tên đơn giản.
 */
function getCookie(name) {
  if (typeof document === "undefined") return null;
  const match = document.cookie.match(
    new RegExp("(?:^|;\\s*)" + name.replace(/[-[\]/{}()*+?.\\^$|]/g, "\\$&") + "=([^;]*)")
  );
  return match ? decodeURIComponent(match[1]) : null;
}

/**
 * Tạo một client id ngẫu nhiên (ưu tiên crypto.randomUUID).
 */
function generateClientId() {
  if (typeof crypto !== "undefined" && crypto.randomUUID) {
    return crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

/**
 * Lấy hoặc tạo mới client id, lưu vào cookie `ktk_client_id` để
 * phía BE (AuditLogger) đọc được qua header/cookie.
 */
function getOrInitClientId() {
  if (typeof window === "undefined") return null;

  // 1. Ưu tiên cookie (để đồng bộ với BE: GetSessionId -> cookie "ktk_client_id")
  let id = getCookie(CLIENT_ID_COOKIE_KEY);

  // 2. Nếu chưa có cookie thì tạo mới
  if (!id) {
    id = generateClientId();

    // Lưu vào cookie 1 năm, path=/, SameSite=Lax
    const oneYearSeconds = 365 * 24 * 60 * 60;
    document.cookie = `${CLIENT_ID_COOKIE_KEY}=${encodeURIComponent(
      id
    )}; max-age=${oneYearSeconds}; path=/; SameSite=Lax`;
  }

  return id;
}

const CLIENT_ID = getOrInitClientId();

/* ====================== AXIOS INSTANCE ====================== */

const axiosClient = axios.create({
  baseURL,
  timeout: 15000,
  // 🟢 BẮT BUỘC: cho phép gửi/nhận cookie (ktk_anon_cart, ktk_client_id, ...)
  withCredentials: true,
  headers: { "Content-Type": "application/json" },
  paramsSerializer: (params) => qs.stringify(params, { arrayFormat: "repeat" }),
});

// Flag to prevent multiple refresh attempts
let isRefreshing = false;
let failedQueue = [];

const processQueue = (error, token = null) => {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error);
    } else {
      prom.resolve(token);
    }
  });

  failedQueue = [];
};

axiosClient.interceptors.request.use(
  (config) => {
    // Gắn Bearer token nếu có
    const token = localStorage.getItem("access_token");
    if (token) config.headers.Authorization = `Bearer ${token}`;

    // 🟢 Gắn Session Id để AuditLogger đọc được (X-Client-Id)
    if (CLIENT_ID) {
      config.headers["X-Client-Id"] = CLIENT_ID;
    }

    // (Không cần đụng tới User-Agent, browser tự gửi rồi.
    // Nếu muốn log riêng có thể gửi thêm header custom, VD:
    // if (typeof navigator !== "undefined" && navigator.userAgent) {
    //   config.headers["X-Client-UA"] = navigator.userAgent;
    // })

    // log full URL để so khớp nhanh
    const fullUrl = (config.baseURL || "") + (config.url || "");
    console.log("[API REQUEST]", config.method?.toUpperCase(), fullUrl);
    return config;
  },
  (error) => Promise.reject(error)
);

axiosClient.interceptors.response.use(
  (res) => res.data ?? res,
  async (error) => {
    const originalRequest = error.config;

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
      const networkError = new Error("Lỗi kết nối đến máy chủ");
      networkError.isNetworkError = true;
      return Promise.reject(networkError);
    }

    // Handle 401 Unauthorized - Token expired
    if (error.response?.status === 401 && !originalRequest._retry) {
      // Don't retry refresh-token endpoint itself
      if (originalRequest.url?.includes("/account/refresh-token")) {
        // Clear tokens and redirect to login
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        localStorage.removeItem("user");
        window.location.href = "/login";
        return Promise.reject(error);
      }

      if (isRefreshing) {
        // If already refreshing, queue this request
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        })
          .then((token) => {
            originalRequest.headers.Authorization = `Bearer ${token}`;
            return axiosClient(originalRequest);
          })
          .catch((err) => Promise.reject(err));
      }

      originalRequest._retry = true;
      isRefreshing = true;

      const refreshToken = localStorage.getItem("refresh_token");

      if (!refreshToken) {
        // No refresh token, redirect to login
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        localStorage.removeItem("user");
        window.location.href = "/login";
        return Promise.reject(error);
      }

      try {
        // Call refresh token endpoint
        const response = await axios.post(`${baseURL}/account/refresh-token`, {
          refreshToken: refreshToken,
        });

        const { accessToken, refreshToken: newRefreshToken } = response.data;

        // Update tokens in localStorage
        localStorage.setItem("access_token", accessToken);
        localStorage.setItem("refresh_token", newRefreshToken);

        // Update authorization header
        axiosClient.defaults.headers.common[
          "Authorization"
        ] = `Bearer ${accessToken}`;
        originalRequest.headers.Authorization = `Bearer ${accessToken}`;

        // Process queued requests
        processQueue(null, accessToken);

        // Retry original request
        return axiosClient(originalRequest);
      } catch (refreshError) {
        // Refresh failed, clear tokens and redirect to login
        processQueue(refreshError, null);
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        localStorage.removeItem("user");
        window.location.href = "/login";
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    // Preserve the original error object so components can access error.response.data
    return Promise.reject(error);
  }
);

export default axiosClient;
