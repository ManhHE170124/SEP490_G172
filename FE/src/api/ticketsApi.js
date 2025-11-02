// src/api/ticketsApi.js
import axiosClient from "./axiosClient";

const END = { TICKETS: "tickets" };

const build = (p = {}) =>
  Object.entries(p)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
    .join("&");

export const ticketsApi = {
  list: (params = {}) => {
    const q = { ...params };
    // alias: FE dùng assignmentState, BE cũng chấp nhận assigned
    if (q.assignmentState && !q.assigned) q.assigned = q.assignmentState;
    delete q.assignmentState;
    return axiosClient.get(`${END.TICKETS}?${build(q)}`);
  },
  detail: (id) => axiosClient.get(`${END.TICKETS}/${id}`),
  assign: (id) => axiosClient.post(`${END.TICKETS}/${id}/assign`),
  transferTech: (id) => axiosClient.post(`${END.TICKETS}/${id}/transfer-tech`),
  complete: (id) => axiosClient.post(`${END.TICKETS}/${id}/complete`),
  close: (id) => axiosClient.post(`${END.TICKETS}/${id}/close`),
};
