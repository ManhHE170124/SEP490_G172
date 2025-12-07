// src/services/auditLogs.js
// API client cho Audit Logs, tham khảo từ services/categories.js
import axiosClient from "../api/axiosClient";

const AUDIT_ENDPOINTS = {
  ROOT: "auditlogs",
};

export const AuditLogsApi = {
  /**
   * Lấy danh sách audit log có phân trang + filter.
   * Backend trả: { page, pageSize, totalItems, items }
   * Hàm này normalize về: { items, total, pageNumber, pageSize }
   */
  listPaged: async (params = {}) => {
    const data = await axiosClient.get(AUDIT_ENDPOINTS.ROOT, { params });

    if (!data) {
      return {
        items: [],
        total: 0,
        pageNumber: params.page || params.pageNumber || 1,
        pageSize: params.pageSize || 20,
      };
    }

    // Chuẩn theo DTO: { page, pageSize, totalItems, items }
    const items =
      data.items ??
      data.data ??
      data.result ??
      (Array.isArray(data) ? data : []);
    const total =
      typeof data.totalItems === "number"
        ? data.totalItems
        : typeof data.total === "number"
        ? data.total
        : items.length;

    const pageNumber = data.page ?? data.pageNumber ?? params.page ?? 1;
    const pageSize = data.pageSize ?? params.pageSize ?? items.length ?? 20;

    return {
      items,
      total,
      pageNumber,
      pageSize,
    };
  },

  /**
   * Lấy chi tiết 1 audit log theo id
   */
  getDetail: (id) => axiosClient.get(`${AUDIT_ENDPOINTS.ROOT}/${id}`),

  /**
   * Lấy danh sách option không trùng nhau cho dropdown:
   * - actions
   * - entityTypes
   * - actorRoles
   * GET /api/auditlogs/options
   */
  getFilterOptions: () => axiosClient.get(`${AUDIT_ENDPOINTS.ROOT}/options`),
};
