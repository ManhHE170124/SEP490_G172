// src/services/notifications.js
// API client cho Notifications (thông báo hệ thống + lịch sử user)

import axiosClient from "../api/axiosClient";

const NOTIFICATION_ENDPOINTS = {
  ROOT: "/notifications", // /api/notifications
  MY: "/notifications/my", // /api/notifications/my
  MANUAL_TARGET_OPTIONS: "/notifications/manual-target-options", // /api/notifications/manual-target-options
};

export const NotificationsApi = {
  /**
   * Lấy danh sách thông báo (Admin) có phân trang + filter.
   *
   * Backend (NotificationsController.GetNotifications) trả:
   *   NotificationListResponseDto:
   *     {
   *       totalCount: number,
   *       items: NotificationListItemDto[]
   *     }
   *
   * Hàm này normalize về:
   *   { items, total, pageNumber, pageSize }
   * - pageNumber/pageSize suy ra từ params hoặc mặc định.
   */
  listAdminPaged: async (params = {}) => {
    const data = await axiosClient.get(NOTIFICATION_ENDPOINTS.ROOT, {
      params,
    });
    // data ~= { totalCount, items }

    const items = Array.isArray(data.items) ? data.items : [];

    const total =
      typeof data.totalCount === "number"
        ? data.totalCount
        : typeof data.totalItems === "number"
        ? data.totalItems
        : items.length;

    const pageNumber =
      typeof data.page === "number"
        ? data.page
        : typeof data.pageNumber === "number"
        ? data.pageNumber
        : params.page ?? params.pageNumber ?? 1;

    const pageSize =
      typeof data.pageSize === "number"
        ? data.pageSize
        : params.pageSize ?? items.length ?? 20;

    return {
      items,
      total,
      pageNumber,
      pageSize,
    };
  },

  /**
   * Lấy chi tiết 1 thông báo (Admin).
   * GET /api/notifications/{id}
   * Backend trả: NotificationDetailDto.
   */
  getDetail: (id) =>
    axiosClient.get(`${NOTIFICATION_ENDPOINTS.ROOT}/${id}`),

  /**
   * Lấy lịch sử thông báo của user hiện tại (NotificationUser).
   *
   * Backend (NotificationsController.GetMyNotifications) trả:
   *   NotificationUserListResponseDto:
   *     {
   *       totalCount: number,
   *       items: NotificationUserListItemDto[]
   *     }
   *
   * Hàm này normalize về:
   *   { items, total, pageNumber, pageSize }
   */
  listMyPaged: async (params = {}) => {
    const data = await axiosClient.get(NOTIFICATION_ENDPOINTS.MY, {
      params,
    });
    // data ~= { totalCount, items }

    const items = Array.isArray(data.items) ? data.items : [];

    const total =
      typeof data.totalCount === "number"
        ? data.totalCount
        : typeof data.totalItems === "number"
        ? data.totalItems
        : items.length;

    const pageNumber =
      typeof data.page === "number"
        ? data.page
        : typeof data.pageNumber === "number"
        ? data.pageNumber
        : params.page ?? params.pageNumber ?? 1;

    const pageSize =
      typeof data.pageSize === "number"
        ? data.pageSize
        : params.pageSize ?? items.length ?? 20;

    return {
      items,
      total,
      pageNumber,
      pageSize,
    };
  },
  /**
   * Tạo thông báo thủ công (Admin) và gán cho danh sách user.
   * POST /api/notifications
   * Payload: CreateNotificationDto
   */
  createManual: (payload) =>
    axiosClient.post(NOTIFICATION_ENDPOINTS.ROOT, payload),

  /**
   * Lấy options cho dropdown tạo thông báo thủ công:
   *  - Roles (RoleId + RoleName)
   *  - Users (UserId + FullName + Email)
   *
   * GET /api/notifications/manual-target-options
   * Backend trả: NotificationManualTargetOptionsDto
   */
  getManualTargetOptions: () =>
    axiosClient.get(NOTIFICATION_ENDPOINTS.MANUAL_TARGET_OPTIONS),

  /**
   * Đánh dấu 1 thông báo của user hiện tại là ĐÃ ĐỌC.
   * POST /api/notifications/my/{notificationUserId}/read
   */
  markMyNotificationRead: (notificationUserId) =>
    axiosClient.post(
      `${NOTIFICATION_ENDPOINTS.MY}/${notificationUserId}/read`
    ),
};
