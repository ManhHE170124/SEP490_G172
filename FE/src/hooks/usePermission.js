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
 */
export const usePermission = () => {
  return { hasPermission: true, loading: false };
};

