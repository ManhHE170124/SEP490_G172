// src/services/auditLogs.js
// API client cho Audit Logs

import axiosClient from "../api/axiosClient";

const AUDIT_ENDPOINTS = {
  ROOT: "auditlogs",
};

export const AuditLogsApi = {
  /**
   * Lấy danh sách audit log có phân trang + filter.
   * Backend trả: { page, pageSize, totalItems, items }
   * Trong đó mỗi item: AuditLogListItemDto + Changes (danh sách diff)
   *
   * Hàm này normalize về:
   *   { items, total, pageNumber, pageSize }
   * và giữ nguyên cấu trúc item (kể cả field `changes` nếu backend trả về).
   */
  listPaged: async (params = {}) => {
    const data = await axiosClient.get(AUDIT_ENDPOINTS.ROOT, { params });
    // data chính là AuditLogListResponseDto

    const items = Array.isArray(data.items) ? data.items : [];
    const total =
      typeof data.totalItems === "number" ? data.totalItems : items.length;

    const pageNumber =
      typeof data.page === "number"
        ? data.page
        : params.page ?? params.pageNumber ?? 1;

    const pageSize =
      typeof data.pageSize === "number"
        ? data.pageSize
        : params.pageSize ?? items.length ?? 20;

    return {
      items,      // mỗi item đã có sẵn field `changes` nếu backend build
      total,
      pageNumber,
      pageSize,
    };
  },

  /**
   * Lấy chi tiết 1 audit log theo id.
   * Backend trả: AuditLogDetailDto + Changes (list diff).
   *  -> FE nhận nguyên dto này để hiển thị popup/chi tiết.
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
