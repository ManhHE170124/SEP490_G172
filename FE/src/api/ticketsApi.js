// File: src/api/ticketsApi.js
import axiosClient from "./axiosClient";

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
  // NEW: nhận staffId
  assign(id, assigneeId) {
    return axiosClient.post(`/tickets/${id}/assign`, { assigneeId });
  },
  transferTech(id, assigneeId) {
    return axiosClient.post(`/tickets/${id}/transfer-tech`, { assigneeId });
  },
  complete(id) {
    return axiosClient.post(`/tickets/${id}/complete`, {});
  },
  close(id) {
    return axiosClient.post(`/tickets/${id}/close`, {});
  },
  reply(id, payload) {
    return axiosClient.post(`/tickets/${id}/replies`, payload);
  },
};
