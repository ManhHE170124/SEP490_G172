// File: src/services/auditLogs.js
import axiosClient from "../api/axiosClient";

const AUDIT_ENDPOINTS = {
  ROOT: "auditlogs",
};

export const AuditLogsApi = {
  listPaged: async (params = {}) => {
    const data = await axiosClient.get(AUDIT_ENDPOINTS.ROOT, { params });

    const items = Array.isArray(data.items) ? data.items : [];
    const total = typeof data.totalItems === "number" ? data.totalItems : items.length;

    const pageNumber =
      typeof data.page === "number" ? data.page : params.page ?? params.pageNumber ?? 1;

    const pageSize =
      typeof data.pageSize === "number" ? data.pageSize : params.pageSize ?? items.length ?? 20;

    return { items, total, pageNumber, pageSize };
  },

  getDetail: (id) => axiosClient.get(`${AUDIT_ENDPOINTS.ROOT}/${id}`),

  getFilterOptions: () => axiosClient.get(`${AUDIT_ENDPOINTS.ROOT}/options`),
};
