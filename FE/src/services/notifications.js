// File: src/services/notifications.js
// API client cho Notifications (Admin + lịch sử user)
// Updated to match Notifications controller changes (Type/CreatedByEmail/sort aliases/search fields)

import axiosClient from "../api/axiosClient";

const NOTIFICATION_ENDPOINTS = {
  ROOT: "/notifications", // /api/notifications
  MY: "/notifications/my", // /api/notifications/my
  MY_UNREAD_COUNT: "/notifications/my/unread-count", // /api/notifications/my/unread-count
  MANUAL_TARGET_OPTIONS: "/notifications/manual-target-options", // /api/notifications/manual-target-options (Admin-only)
  ADMIN_FILTER_OPTIONS: "/notifications/admin-filter-options", // /api/notifications/admin-filter-options (Admin-only)
};

let _hasUnreadCountEndpoint = null; // null = unknown, true/false = cached

const normalizePaged = (data, params = {}) => {
  const items = Array.isArray(data?.items)
    ? data.items
    : Array.isArray(data?.Items)
    ? data.Items
    : [];

  const total =
    typeof data?.totalCount === "number"
      ? data.totalCount
      : typeof data?.TotalCount === "number"
      ? data.TotalCount
      : typeof data?.totalItems === "number"
      ? data.totalItems
      : typeof data?.TotalItems === "number"
      ? data.TotalItems
      : items.length;

  const pageNumber =
    typeof data?.pageNumber === "number"
      ? data.pageNumber
      : typeof data?.PageNumber === "number"
      ? data.PageNumber
      : typeof data?.page === "number"
      ? data.page
      : typeof data?.Page === "number"
      ? data.Page
      : params.pageNumber ?? params.page ?? 1;

  const pageSize =
    typeof data?.pageSize === "number"
      ? data.pageSize
      : typeof data?.PageSize === "number"
      ? data.PageSize
      : params.pageSize ?? items.length ?? 20;

  return { items, total, pageNumber, pageSize };
};

/**
 * Small helper: try POST first (current design), fallback to PUT if BE uses PUT.
 */
const postThenPutFallback = async (url, body) => {
  try {
    return await axiosClient.post(url, body);
  } catch (err) {
    const status = err?.response?.status;
    if (status === 404 || status === 405) {
      return await axiosClient.put(url, body);
    }
    throw err;
  }
};

export const NotificationsApi = {
  // ===== Admin =====
  /**
   * GET /api/notifications
   * Query supports:
   * - pageNumber, pageSize
   * - search (Title/Message/CreatedByEmail/Type/CorrelationId...)
   * - severity, isSystemGenerated, isGlobal
   * - createdFromUtc, createdToUtc
   * - sortBy (aliases), sortDescending
   */
  listAdminPaged: async (params = {}, options = {}) => {
    const data = await axiosClient.get(NOTIFICATION_ENDPOINTS.ROOT, {
      params,
      signal: options?.signal,
    });
    return normalizePaged(data, params);
  },

  /**
   * GET /api/notifications/{id}/recipients (Admin-only)
   * Query supports: pageNumber, pageSize, search, isRead
   * Returns: { totalCount, items }
   */
  getAdminRecipientsPaged: async (id, params = {}, options = {}) => {
    const data = await axiosClient.get(
      `${NOTIFICATION_ENDPOINTS.ROOT}/${id}/recipients`,
      {
        params,
        signal: options?.signal,
      }
    );
    return normalizePaged(data, params);
  },

  /**
   * GET /api/notifications/{id}
   */
  getDetail: (id) => axiosClient.get(`${NOTIFICATION_ENDPOINTS.ROOT}/${id}`),

  /**
   * POST /api/notifications
   * Payload (minimal): { title, message, severity, isGlobal, targetUserIds?, targetRoleIds? }
   * Option fields allowed: type, correlationId, relatedUrl, relatedEntityType, relatedEntityId...
   * BE will default Type="Manual" if not provided.
   */
  createManual: (payload) => axiosClient.post(NOTIFICATION_ENDPOINTS.ROOT, payload),

  /**
   * GET /api/notifications/manual-target-options (Admin-only)
   */
  getManualTargetOptions: () =>
    axiosClient.get(NOTIFICATION_ENDPOINTS.MANUAL_TARGET_OPTIONS),

  /**
   * GET /api/notifications/admin-filter-options (Admin-only)
   * Returns: { types: [{value,label}], creators: [{value,label}] }
   */
  getAdminFilterOptions: () =>
    axiosClient.get(NOTIFICATION_ENDPOINTS.ADMIN_FILTER_OPTIONS),

  // ===== Current user =====
  /**
   * GET /api/notifications/my
   * Query supports: pageNumber, pageSize, onlyUnread, severity, fromUtc, toUtc, search, sortBy, sortDescending
   */
  listMyPaged: async (params = {}) => {
    const data = await axiosClient.get(NOTIFICATION_ENDPOINTS.MY, { params });
    return normalizePaged(data, params);
  },

  /**
   * GET unread count (lightweight if endpoint exists).
   * Fallback: call /my?onlyUnread=true&pageSize=1 and read totalCount.
   */
  getMyUnreadCount: async () => {
    if (_hasUnreadCountEndpoint !== false) {
      try {
        const data = await axiosClient.get(NOTIFICATION_ENDPOINTS.MY_UNREAD_COUNT);
        _hasUnreadCountEndpoint = true;

        // accept common shapes: { unreadCount }, { count }, { totalCount }, number
        if (typeof data === "number") return data;

        const v =
          data?.unreadCount ??
          data?.UnreadCount ??
          data?.count ??
          data?.Count ??
          data?.totalCount ??
          data?.TotalCount ??
          data?.total ??
          data?.Total;

        if (typeof v === "number") return v;
        // unexpected shape -> fallback below
      } catch (err) {
        const status = err?.response?.status;
        if (status === 404) _hasUnreadCountEndpoint = false;
        // otherwise fallback below
      }
    }

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
        : Array.isArray(data?.Items)
        ? data.Items.length
        : 0;

    return total;
  },

  /**
   * Mark read:
   * POST (or PUT fallback) /api/notifications/my/{notificationUserId}/read
   */
  markMyNotificationRead: (notificationUserId) =>
    postThenPutFallback(`${NOTIFICATION_ENDPOINTS.MY}/${notificationUserId}/read`),

  /**
   * Dismiss (hide):
   * POST (or PUT fallback) /api/notifications/my/{notificationUserId}/dismiss
   * (Only if your BE implemented it. If not, calling will return 404.)
   */
  dismissMyNotification: (notificationUserId) =>
    postThenPutFallback(`${NOTIFICATION_ENDPOINTS.MY}/${notificationUserId}/dismiss`),
};
