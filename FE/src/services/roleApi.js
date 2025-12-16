/**
 * File: roleApi.js
 * Author: HieuNDHE173169
 * Created: 18/10/2025
 * Last Updated: 29/10/2025
 * Version: 1.0.0
 * Purpose: REST client for Role management endpoints.
 *          Provides API methods for managing roles, modules, permissions, and role-permissions.
 * Endpoints:
 *   Roles:
 *     - GET    /api/roles/list           : List all roles
 *     - GET    /api/roles                : Get all roles
 *     - GET    /api/roles/{id}           : Get role by id
 *     - POST   /api/roles                : Create a role
 *     - PUT    /api/roles/{id}           : Update a role
 *     - DELETE /api/roles/{id}           : Delete a role
 *     - GET    /api/roles/active         : Get active roles
 *   Role Permissions:
 *     - GET    /api/roles/{id}/permissions : Get role permissions
 *     - PUT    /api/roles/{id}/permissions : Update role permissions
 *   Modules:
 *     - GET    /api/modules              : List all modules
 *     - POST   /api/modules               : Create a module
 *     - PUT    /api/modules/{id}          : Update a module
 *     - DELETE /api/modules/{id}          : Delete a module
 *   Permissions:
 *     - GET    /api/permissions          : List all permissions
 *     - POST   /api/permissions           : Create a permission
 *     - PUT    /api/permissions/{id}      : Update a permission
 *     - DELETE /api/permissions/{id}       : Delete a permission
 */
import axiosClient from "../api/axiosClient";

const END = { 
  ROLES: "roles", 
  MODULES: "modules", 
  PERMISSIONS: "permissions" 
};

export const roleApi = {
  /// Roles
  getAllRoles: () => axiosClient.get(`${END.ROLES}/list`),
  getRoles: () => axiosClient.get(END.ROLES),
  getRoleById: (id) => axiosClient.get(`${END.ROLES}/${id}`),
  createRole: (data) => axiosClient.post(END.ROLES, data),
  updateRole: (id, data) => axiosClient.put(`${END.ROLES}/${id}`, data),
  deleteRole: (id) => axiosClient.delete(`${END.ROLES}/${id}`),
  getActiveRoles: () => axiosClient.get(`${END.ROLES}/active`),
  
  /// Role Permissions
  getRolePermissions: (id) => axiosClient.get(`${END.ROLES}/${id}/permissions`),
  updateRolePermissions: (id, data) => axiosClient.put(`${END.ROLES}/${id}/permissions`, data),
  
  /// Modules
  getModules: () => axiosClient.get(END.MODULES),
  createModule: (data) => axiosClient.post(END.MODULES, data),
  updateModule: (id, data) => axiosClient.put(`${END.MODULES}/${id}`, data),
  deleteModule: (id) => axiosClient.delete(`${END.MODULES}/${id}`),
  
  /// Permissions
  getPermissions: () => axiosClient.get(END.PERMISSIONS),
  createPermission: (data) => axiosClient.post(END.PERMISSIONS, data),
  updatePermission: (id, data) => axiosClient.put(`${END.PERMISSIONS}/${id}`, data),
  deletePermission: (id) => axiosClient.delete(`${END.PERMISSIONS}/${id}`),
  
  /// Module Access
  getModuleAccess: (roleCodes = [], permissionCode) =>
    axiosClient.post(`${END.ROLES}/module-access`, {
      roleCodes,
      permissionCode,
    }),

  /// Permission Check
  checkPermission: (roleCode, moduleCode, permissionCode) => 
    axiosClient.post(`${END.ROLES}/check-permission`, {
      roleCode,
      moduleCode,
      permissionCode
    }),
  checkPermissions: (requests) => 
    axiosClient.post(`${END.ROLES}/check-permissions`, requests),
  
  /// User Permissions
  getUserPermissions: (roleCodes = []) =>
    axiosClient.post(`${END.ROLES}/user-permissions`, {
      roleCodes,
    }),
};



