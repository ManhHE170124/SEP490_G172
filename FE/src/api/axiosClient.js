/**
 * File: src/api/axiosClient.js
 * Purpose: Axios instance with auth header, automatic token refresh, normalized error messages,
 *          + ALWAYS send guest cart identity header (X-Guest-Cart-Id) for server-side cart.
 */
import axios from "axios";
import qs from "qs";

const isBrowser = typeof window !== "undefined";

const envBase =
  process.env.REACT_APP_API_URL // CRA
  || import.meta.env?.VITE_API_BASE_URL; // Vite

// Auto detect when env is not set:
// - Local FE (localhost:3000) => use local BE https://localhost:7292/api
// - Production (keytietkiem.com or IP) => use same-origin /api
const autoBase = isBrowser
  ? ((window.location.hostname === "localhost")
      ? "https://localhost:7292/api"
      : `${window.location.origin}/api`)
  : "https://localhost:7292/api";

const baseURL = envBase || autoBase;

console.log("[axiosClient] baseURL =", baseURL);

/* ====================== COOKIE HELPERS ====================== */

const CLIENT_ID_COOKIE_KEY = "ktk_client_id";

function getCookie(name) {
  if (typeof document === "undefined") return null;
  const match = document.cookie.match(
    new RegExp("(?:^|;\\s*)" + name.replace(/[-[\]/{}()*+?.\\^$|]/g, "\\$&") + "=([^;]*)")
  );
  return match ? decodeURIComponent(match[1]) : null;
}

function generateClientId() {
  if (typeof crypto !== "undefined" && crypto.randomUUID) {
    return crypto.randomUUID();
  }
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function getOrInitClientId() {
  if (typeof window === "undefined") return null;

  let id = getCookie(CLIENT_ID_COOKIE_KEY);
  if (!id) {
    id = generateClientId();
    const oneYearSeconds = 365 * 24 * 60 * 60;
    document.cookie = `${CLIENT_ID_COOKIE_KEY}=${encodeURIComponent(
      id
    )}; max-age=${oneYearSeconds}; path=/; SameSite=Lax`;
  }
  return id;
}

const CLIENT_ID = getOrInitClientId();

/* ====================== GUEST CART ID (HEADER) ====================== */

const GUEST_CART_STORAGE_KEY = "ktk_guest_cart_id";
const GUEST_CART_HEADER_NAME = "X-Guest-Cart-Id";

/**
 * Guest cart của BE nhận diện ưu tiên bằng:
 * - cookie HttpOnly (BE set), hoặc
 * - header X-Guest-Cart-Id (FE gửi).
 *
 * Vì FE không đọc được HttpOnly cookie, ta luôn gửi header ổn định theo localStorage.
 *
 * NOTE:
 * - Đọc localStorage MỖI REQUEST để tránh desync nếu localStorage thay đổi trong session.
 */
function getOrInitGuestCartId() {
  if (typeof window === "undefined") return null;
  try {
    let id = window.localStorage.getItem(GUEST_CART_STORAGE_KEY);
    if (!id) {
      if (window.crypto && typeof window.crypto.randomUUID === "function") {
        id = window.crypto.randomUUID();
      } else {
        id = `${Date.now().toString(16)}-${Math.random().toString(16).slice(2)}`;
      }
      window.localStorage.setItem(GUEST_CART_STORAGE_KEY, id);
    }
    return id;
  } catch (e) {
    console.error("Failed to init guest cart id", e);
    return null;
  }
}

/* ====================== AXIOS INSTANCE ====================== */

const axiosClient = axios.create({
  baseURL,
  timeout: 15000,
  withCredentials: true, // ✅ BẮT BUỘC để BE set cookie HttpOnly (anon cart id)
  headers: { "Content-Type": "application/json" },
  paramsSerializer: (params) => qs.stringify(params, { arrayFormat: "repeat" }),
});

let isRefreshing = false;
let failedQueue = [];

const processQueue = (error, token = null) => {
  failedQueue.forEach((prom) => {
    if (error) prom.reject(error);
    else prom.resolve(token);
  });
  failedQueue = [];
};

axiosClient.interceptors.request.use(
  (config) => {
    // ✅ Normalize url: ensure it starts with "/" when using baseURL
    // Avoid touching absolute URLs (http/https).
    if (config.url && typeof config.url === "string") {
      const u = config.url.trim();
      const isAbsolute = /^https?:\/\//i.test(u);
      if (!isAbsolute) {
        config.url = u.startsWith("/") ? u : `/${u}`;
      } else {
        config.url = u;
      }
    }

    const token = localStorage.getItem("access_token");
    if (token) config.headers.Authorization = `Bearer ${token}`;

    // ✅ audit/session header
    if (CLIENT_ID) config.headers["X-Client-Id"] = CLIENT_ID;

    // ✅ guest cart identity header (always send)
    const guestId = getOrInitGuestCartId();
    if (guestId) config.headers[GUEST_CART_HEADER_NAME] = guestId;

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

    // ✅ 429 Too Many Requests (CartPolicy)
    if (error.response?.status === 429) {
      const msg =
        error.response?.data?.message ||
        "Bạn thao tác quá nhanh. Vui lòng thử lại sau vài giây.";
      const rateErr = new Error(msg);
      rateErr.response = error.response;
      rateErr.code = error.code;
      rateErr.isRateLimited = true;
      return Promise.reject(rateErr);
    }

    // 403
    if (error.response?.status === 403) {
      const viMessage =
        error.response?.data?.message ||
        "Bạn không có quyền truy cập chức năng này. Vui lòng kiểm tra quyền hạn hoặc liên hệ quản trị viên.";
      const forbiddenError = new Error(viMessage);
      forbiddenError.response = error.response;
      forbiddenError.code = error.code;
      return Promise.reject(forbiddenError);
    }

    // network
    if (error.code === "ERR_NETWORK") {
      const networkError = new Error("Lỗi kết nối đến máy chủ");
      networkError.isNetworkError = true;
      return Promise.reject(networkError);
    }

    // 401 token expired
    if (error.response?.status === 401 && !originalRequest._retry) {
      if (originalRequest.url?.includes("/account/login")) {
        return Promise.reject(error);
      }

      const publicPaths = ["/login", "/register", "/forgot-password", "/check-reset-email", "/reset-password"];
      const currentPath = window.location.pathname;
      const isPublicPage = publicPaths.some((p) => currentPath.startsWith(p));

      if (originalRequest.url?.includes("/account/refresh-token")) {
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        localStorage.removeItem("user");

        const isProtectedRoute =
          currentPath.startsWith("/admin") ||
          currentPath.startsWith("/profile") ||
          currentPath.startsWith("/account") ||
          currentPath.startsWith("/post-dashboard") ||
          currentPath.startsWith("/orders");

        if (!isPublicPage && isProtectedRoute) window.location.href = "/login";
        return Promise.reject(error);
      }

      if (isRefreshing) {
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
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        localStorage.removeItem("user");

        const isProtectedRoute =
          currentPath.startsWith("/admin") ||
          currentPath.startsWith("/profile") ||
          currentPath.startsWith("/account") ||
          currentPath.startsWith("/post-dashboard") ||
          currentPath.startsWith("/orders");

        if (!isPublicPage && isProtectedRoute) window.location.href = "/login";
        return Promise.reject(error);
      }

      try {
        const response = await axios.post(`${baseURL}/account/refresh-token`, {
          refreshToken,
        });

        const { accessToken, refreshToken: newRefreshToken } = response.data;

        localStorage.setItem("access_token", accessToken);
        localStorage.setItem("refresh_token", newRefreshToken);

        axiosClient.defaults.headers.common["Authorization"] = `Bearer ${accessToken}`;
        originalRequest.headers.Authorization = `Bearer ${accessToken}`;

        processQueue(null, accessToken);
        return axiosClient(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError, null);
        localStorage.removeItem("access_token");
        localStorage.removeItem("refresh_token");
        localStorage.removeItem("user");

        const isProtectedRoute =
          currentPath.startsWith("/admin") ||
          currentPath.startsWith("/profile") ||
          currentPath.startsWith("/account") ||
          currentPath.startsWith("/post-dashboard") ||
          currentPath.startsWith("/orders");

        if (!isPublicPage && isProtectedRoute) window.location.href = "/login";
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  }
);

export default axiosClient;
