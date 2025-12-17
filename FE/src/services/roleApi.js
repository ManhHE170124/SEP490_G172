/**
 * File: roleApi.js
 * Purpose: REST client for Role management endpoints (simplified - role-based auth).
 * Note: Permission assignment APIs removed since system now uses role-based authorization.
 */
import axiosClient from "../api/axiosClient";

const END = { 
  ROLES: "roles", 
  MODULES: "modules", 
  PERMISSIONS: "permissions" 
};

export const roleApi = {
  // Roles (readonly for display)
  getAllRoles: () => axiosClient.get(`${END.ROLES}/list`),
  getRoles: () => axiosClient.get(END.ROLES),
  getRoleById: (id) => axiosClient.get(`${END.ROLES}/${id}`),
  getActiveRoles: () => axiosClient.get(`${END.ROLES}/active`),
  
  // Modules & Permissions (readonly for reference)
  getModules: () => axiosClient.get(END.MODULES),
  getPermissions: () => axiosClient.get(END.PERMISSIONS),
};



