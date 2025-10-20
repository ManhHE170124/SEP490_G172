import axiosClient from "../api/axiosClient";

const RBAC_ENDPOINTS = {
  ROLES: "roles",
  MODULES: "modules",
  PERMISSIONS: "permissions",
};

export const rbacApi = {
  async getRoles() {
    return axiosClient.get(`${RBAC_ENDPOINTS.ROLES}`);
  },

  async getRoleById(roleId) {
    return axiosClient.get(`${RBAC_ENDPOINTS.ROLES}/${roleId}`);
  },

  async createRole(payload) {
    return axiosClient.post(`${RBAC_ENDPOINTS.ROLES}`, payload);
  },
  async updateRole(roleId, payload) {
    return axiosClient.put(`${RBAC_ENDPOINTS.ROLES}/${roleId}`, payload);
  },
  async deleteRole(roleId) {
    return axiosClient.delete(`${RBAC_ENDPOINTS.ROLES}/${roleId}`);
  },
  
  async getModules() {
    return axiosClient.get(`${RBAC_ENDPOINTS.MODULES}`);
  },

  async createModule(payload) {
    return axiosClient.post(`${RBAC_ENDPOINTS.MODULES}`, payload);
  },
  async updateModule(moduleId, payload) {
    return axiosClient.put(`${RBAC_ENDPOINTS.MODULES}/${moduleId}`, payload);
  },
  async deleteModule(moduleId) {
    return axiosClient.delete(`${RBAC_ENDPOINTS.MODULES}/${moduleId}`);
  },

  async getPermissions() {
    return axiosClient.get(`${RBAC_ENDPOINTS.PERMISSIONS}`);
  },

  async createPermission(payload) {
    return axiosClient.post(`${RBAC_ENDPOINTS.PERMISSIONS}`, payload);
  },
  async updatePermission(permissionId, payload) {
    return axiosClient.put(`${RBAC_ENDPOINTS.PERMISSIONS}/${permissionId}`, payload);
  },
  async deletePermission(permissionId) {
    return axiosClient.delete(`${RBAC_ENDPOINTS.PERMISSIONS}/${permissionId}`);
  },
};

export default rbacApi;

