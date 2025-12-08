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

import { useMemo } from "react";
import { usePermissions } from "../context/PermissionContext";

/**
 * Hook to check if user has a specific permission for a module
 * @param {string} moduleCode - The module code (e.g., "POST_MANAGER")
 * @param {string} permissionCode - The permission code (e.g., "CREATE", "EDIT", "DELETE", "VIEW_DETAIL", "ACCESS")
 * @returns {Object} - { hasPermission: boolean, loading: boolean }
 */
export const usePermission = (moduleCode, permissionCode) => {
  const { allPermissions, loading } = usePermissions();

  const hasPermission = useMemo(() => {
    if (!moduleCode || !permissionCode) {
      return false;
    }

    if (loading || !allPermissions) {
      return false;
    }

    const normalizedModuleCode = String(moduleCode).trim().toUpperCase();
    const normalizedPermissionCode = String(permissionCode).trim().toUpperCase();

    const modulePermissions = allPermissions.get(normalizedModuleCode);
    if (!modulePermissions) {
      return false;
    }

    return modulePermissions.has(normalizedPermissionCode);
  }, [moduleCode, permissionCode, allPermissions, loading]);

  return {
    hasPermission,
    loading,
  };
};

