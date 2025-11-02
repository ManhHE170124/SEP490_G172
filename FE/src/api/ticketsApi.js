import axiosClient from "./axiosClient";

const END = { TICKETS: "tickets" };

const build = (p = {}) =>
  Object.entries(p)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
    .join("&");
export const ticketsApi = {
  list(params) {
    const p = {
      q: params?.q ?? "",
      status: params?.status || "",
      severity: params?.severity || "",
      sla: params?.sla || "",
      assignmentState: params?.assignmentState || "",
      page: params?.page || 1,
      pageSize: params?.pageSize || 10,
    };
    return axiosClient.get("/tickets", { params: p });
  },
  detail(id) {
    return axiosClient.get(`/tickets/${id}`);
  },
  assign(id) {
    return axiosClient.post(`/tickets/${id}/assign`, {});
  },
  transferTech(id) {
    return axiosClient.post(`/tickets/${id}/transfer-tech`, {});
  },
  complete(id) {
    return axiosClient.post(`/tickets/${id}/complete`, {});
  },
  close(id) {
    return axiosClient.post(`/tickets/${id}/close`, {});
  },
  // NEW: chat
  reply(id, payload) {
    return axiosClient.post(`/tickets/${id}/replies`, payload);
  },
};
