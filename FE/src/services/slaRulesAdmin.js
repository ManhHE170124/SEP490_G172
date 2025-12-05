// src/services/slaRulesAdmin.js
/**
 * File: slaRulesAdmin.js
 * Purpose: REST client cho API cấu hình SLA Rule (SlaRules Admin).
 */
import axiosClient from "../api/axiosClient";

// BE route: [Route("api/sla-rules-admin")]
// baseURL (".../api") + "sla-rules-admin"
const ENDPOINT = "sla-rules-admin";

const buildQuery = (p = {}) =>
  Object.entries(p)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
    .join("&");

export const SlaRulesAdminApi = {
  /**
   * Lấy danh sách SLA rule có phân trang.
   * Hỗ trợ cả trường hợp BE trả về array thuần hoặc PagedResult.
   */
  listPaged: (params) =>
    axiosClient
      .get(
        `${ENDPOINT}${
          params && Object.keys(params).length > 0
            ? `?${buildQuery(params)}`
            : ""
        }`
      )
      .then((data) => {
        // Cho phép BE trả về array hoặc PagedResult
        if (Array.isArray(data)) {
          return {
            items: data,
            page: 1,
            pageSize: data.length,
            total: data.length,
          };
        }

        const items = data?.items ?? data?.Items ?? [];
        const page = data?.page ?? data?.Page ?? 1;
        const pageSize = data?.pageSize ?? data?.PageSize ?? items.length;
        const total =
          data?.totalItems ?? data?.TotalItems ?? data?.total ?? items.length;

        return {
          items,
          page,
          pageSize,
          total,
        };
      }),

  get: (id) => axiosClient.get(`${ENDPOINT}/${id}`),

  create: (payload) => axiosClient.post(ENDPOINT, payload),

  update: (id, payload) => axiosClient.put(`${ENDPOINT}/${id}`, payload),

  remove: (id) => axiosClient.delete(`${ENDPOINT}/${id}`),

  toggle: (id) => axiosClient.patch(`${ENDPOINT}/${id}/toggle`),
};

export default SlaRulesAdminApi;
