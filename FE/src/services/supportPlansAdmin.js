// src/services/supportPlansAdmin.js
/**
 * File: supportPlansAdmin.js
 * Purpose: REST client cho API cấu hình gói hỗ trợ (Support Plans Admin).
 */
import axiosClient from "../api/axiosClient";

// BE route: [Route("api/support-plans-admin")]
// baseURL (".../api") + "support-plans-admin"
const ENDPOINT = "support-plans-admin";

const buildQuery = (p = {}) =>
  Object.entries(p)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
    .join("&");

export const SupportPlansAdminApi = {
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
          data?.total ??
          data?.Total ??
          data?.totalItems ??
          data?.TotalItems ??
          items.length;

        return { items, page, pageSize, total };
      }),

  get: (id) => axiosClient.get(`${ENDPOINT}/${id}`),

  create: (payload) => axiosClient.post(ENDPOINT, payload),

  update: (id, payload) => axiosClient.put(`${ENDPOINT}/${id}`, payload),

  remove: (id) => axiosClient.delete(`${ENDPOINT}/${id}`),

  toggle: (id) => axiosClient.patch(`${ENDPOINT}/${id}/toggle`),
};

export default SupportPlansAdminApi;
