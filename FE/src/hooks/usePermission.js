/**
 * File: usePermission.js
 * Author: Keytietkiem Team
 * Created: 29/10/2025
 * Version: 1.0.0
 * Purpose: Custom hook to check if user has a specific permission for a module.
 *          Uses PermissionContext to check permissions.
 * Usage:
 *   const { hasPermission, loading } = usePermission("POST_MANAGER", "CREATE");
 */

/**
 * FE no longer enforces permissions; BE is the source of truth.
 * Always allow; loading is always false.
 * @param {string} moduleCode - Module code (ignored, kept for compatibility)
 * @param {string} permissionCode - Permission code (ignored, kept for compatibility)
 * @returns {{hasPermission: boolean, loading: boolean}}
 */
export const usePermission = (moduleCode, permissionCode) => {
  return { hasPermission: true, loading: false };
};

