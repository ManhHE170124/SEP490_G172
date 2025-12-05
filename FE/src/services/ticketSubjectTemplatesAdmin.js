// src/services/ticketSubjectTemplatesAdmin.js
/**
 * File: ticketSubjectTemplatesAdmin.js
 * Purpose: REST client cho API cấu hình Ticket Subject Templates (Admin).
 */

import axiosClient from "../api/axiosClient";

// BE route: [Route("api/ticket-subject-templates-admin")]
// baseURL (".../api") + "ticket-subject-templates-admin"
const ENDPOINT = "ticket-subject-templates-admin";

const buildQuery = (p = {}) =>
  Object.entries(p)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
    .join("&");

export const TicketSubjectTemplatesAdminApi = {
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
        // BE trả về PagedResult<T> (Page, PageSize, TotalItems, Items)
        // hoặc 1 array đơn giản
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

  get: (templateCode) =>
    axiosClient.get(
      `${ENDPOINT}/${encodeURIComponent(templateCode)}`
    ),

  create: (payload) => axiosClient.post(ENDPOINT, payload),

  update: (templateCode, payload) =>
    axiosClient.put(
      `${ENDPOINT}/${encodeURIComponent(templateCode)}`,
      payload
    ),

  remove: (templateCode) =>
    axiosClient.delete(
      `${ENDPOINT}/${encodeURIComponent(templateCode)}`
    ),

  toggle: (templateCode) =>
    axiosClient.patch(
      `${ENDPOINT}/${encodeURIComponent(templateCode)}/toggle`
    ),
};

export default TicketSubjectTemplatesAdminApi;
