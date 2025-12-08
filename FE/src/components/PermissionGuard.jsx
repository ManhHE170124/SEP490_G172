/**
 * File: PermissionGuard.jsx
 * Author: Keytietkiem Team
 * Created: 29/10/2025
 * Version: 1.0.0
 * Purpose: Component wrapper to conditionally render children based on permission.
 *          Hides or shows content based on user's permissions.
 * Usage:
 *   <PermissionGuard moduleCode="POST_MANAGER" permissionCode="CREATE">
 *     <button>Create Post</button>
 *   </PermissionGuard>
 */

import React from "react";
import PropTypes from "prop-types";
import { usePermission } from "../hooks/usePermission";

/**
 * Component that conditionally renders children based on permission
 * @param {string} moduleCode - The module code (e.g., "POST_MANAGER")
 * @param {string} permissionCode - The permission code (e.g., "CREATE", "EDIT", "DELETE", "VIEW_DETAIL", "ACCESS")
 * @param {ReactNode} children - The content to render if user has permission
 * @param {ReactNode} fallback - Optional content to render if user doesn't have permission (default: null)
 * @returns {ReactNode} - Children if has permission, fallback otherwise, or null
 */
const PermissionGuard = ({ moduleCode, permissionCode, children, fallback = null }) => {
  const { hasPermission, loading } = usePermission(moduleCode, permissionCode);

  // Don't render anything while loading to prevent flash of content
  if (loading) {
    return null;
  }

  if (hasPermission) {
    return <>{children}</>;
  }

  return <>{fallback}</>;
};

PermissionGuard.propTypes = {
  moduleCode: PropTypes.string.isRequired,
  permissionCode: PropTypes.string.isRequired,
  children: PropTypes.node.isRequired,
  fallback: PropTypes.node,
};

export default PermissionGuard;

