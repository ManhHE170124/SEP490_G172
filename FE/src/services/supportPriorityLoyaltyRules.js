// src/services/supportPriorityLoyaltyRules.js
/**
 * File: supportPriorityLoyaltyRules.js
 * Purpose: REST client for SupportPriorityLoyaltyRule endpoints.
 */
import axiosClient from "../api/axiosClient";

// ❌ Bỏ dấu "/" ở đầu cho giống các service khác
// const ENDPOINT = "/support-priority-loyalty-rules";

// ✅ Để như này: baseURL (".../api") + "support-priority-loyalty-rules"
const ENDPOINT = "support-priority-loyalty-rules";

const buildQuery = (p = {}) =>
  Object.entries(p)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
    .join("&");

export const SupportPriorityLoyaltyRulesApi = {
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
