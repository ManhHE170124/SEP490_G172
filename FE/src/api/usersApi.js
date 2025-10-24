/**
 * File: usersApi.js
 * Purpose: REST client for Users & Roles endpoints.
 */
import axiosClient from "./axiosClient";

const END = { USERS: "users", ROLES: "roles" };
const buildQuery = (p = {}) =>
  Object.entries(p)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
    .join("&");

export const usersApi = {
  list: (params) => axiosClient.get(`${END.USERS}?${buildQuery(params)}`),
  get: (id) => axiosClient.get(`${END.USERS}/${id}`),
  create: (data) => axiosClient.post(END.USERS, data),
  update: (id, data) => axiosClient.put(`${END.USERS}/${id}`, data),
  delete: (id) => axiosClient.delete(`${END.USERS}/${id}`),
  roles: () => axiosClient.get(END.ROLES),
};
