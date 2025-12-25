// src/services/notifications.js
// API client cho Notifications (thông báo hệ thống + lịch sử user)

import axiosClient from "../api/axiosClient";

const NOTIFICATION_ENDPOINTS = {
  ROOT: "/notifications", // /api/notifications
  MY: "/notifications/my", // /api/notifications/my
  MY_UNREAD_COUNT: "/notifications/my/unread-count", // (optional) /api/notifications/my/unread-count
  MANUAL_TARGET_OPTIONS: "/notifications/manual-target-options", // /api/notifications/manual-target-options
};

let _hasUnreadCountEndpoint = null; // null = unknown, true/false = cached

const normalizePaged = (data, params = {}) => {
  const items = Array.isArray(data?.items) ? data.items : [];
  const total =
    typeof data?.totalCount === "number"
      ? data.totalCount
      : typeof data?.totalItems === "number"
      ? data.totalItems
      : items.length;

  const pageNumber =
    typeof data?.page === "number"
      ? data.page
      : typeof data?.pageNumber === "number"
      ? data.pageNumber
      : params.page ?? params.pageNumber ?? 1;

  const pageSize =
    typeof data?.pageSize === "number"
      ? data.pageSize
      : params.pageSize ?? items.length ?? 20;

  return { items, total, pageNumber, pageSize };
};

export const NotificationsApi = {
  // ===== Admin =====
  listAdminPaged: async (params = {}) => {
    const data = await axiosClient.get(NOTIFICATION_ENDPOINTS.ROOT, { params });
    return normalizePaged(data, params);
  },

  getDetail: (id) => axiosClient.get(`${NOTIFICATION_ENDPOINTS.ROOT}/${id}`),

  createManual: (payload) => axiosClient.post(NOTIFICATION_ENDPOINTS.ROOT, payload),

  getManualTargetOptions: () =>
    axiosClient.get(NOTIFICATION_ENDPOINTS.MANUAL_TARGET_OPTIONS),

  // ===== Current user =====
  listMyPaged: async (params = {}) => {
    const data = await axiosClient.get(NOTIFICATION_ENDPOINTS.MY, { params });
    return normalizePaged(data, params);
  },

  /**
   * Lấy unread count (nhẹ) nếu BE có endpoint /my/unread-count.
   * Nếu BE chưa có, fallback về listMyPaged(onlyUnread=true, pageSize=1) để lấy totalCount.
   */
  getMyUnreadCount: async () => {
    // Prefer lightweight endpoint if available
    if (_hasUnreadCountEndpoint !== false) {
      try {
        const data = await axiosClient.get(NOTIFICATION_ENDPOINTS.MY_UNREAD_COUNT);
        _hasUnreadCountEndpoint = true;

        // accept common shapes: { count }, { unreadCount }, { total }, number
        if (typeof data === "number") return data;
        const v =
          data?.count ??
          data?.unreadCount ??
          data?.totalCount ??
          data?.total ??
          data?.TotalCount ??
          data?.Total;
        if (typeof v === "number") return v;

        // if server returns unexpected, fallback
      } catch (err) {
        const status = err?.response?.status;
        if (status === 404) {
          _hasUnreadCountEndpoint = false;
        } else {
          // unknown error => still allow fallback
        }
      }
    }

    // Fallback: use listMyPaged to get totalCount
    const data = await axiosClient.get(NOTIFICATION_ENDPOINTS.MY, {
      params: {
        pageNumber: 1,
        pageSize: 1,
        onlyUnread: true,
        sortBy: "CreatedAtUtc",
        sortDescending: true,
      },
    });

    const total =
      typeof data?.totalCount === "number"
        ? data.totalCount
        : typeof data?.TotalCount === "number"
        ? data.TotalCount
        : Array.isArray(data?.items)
        ? data.items.length
        : 0;

    return total;
  },

  markMyNotificationRead: (notificationUserId) =>
    axiosClient.post(`${NOTIFICATION_ENDPOINTS.MY}/${notificationUserId}/read`),
};
