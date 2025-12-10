import React from "react";
import PropTypes from "prop-types";
import { Navigate } from "react-router-dom";
import { usePermissions } from "../context/PermissionContext";
import { usePermission } from "../hooks/usePermission";

const ProtectedRoute = ({ moduleCode, permissionCode = "ACCESS", children }) => {
  const { moduleAccessPermissions, loading } = usePermissions();
  const { hasPermission, loading: permissionLoading } = usePermission(moduleCode, permissionCode);
  const [isCheckingAuth, setIsCheckingAuth] = React.useState(true);

  // Check if we're on a public page - don't block those
  React.useEffect(() => {
    const publicPaths = ['/login', '/register', '/forgot-password', '/check-reset-email', '/reset-password'];
    const currentPath = window.location.pathname;
    const isPublicPage = publicPaths.some(path => currentPath.startsWith(path));
    
    if (isPublicPage) {
      setIsCheckingAuth(false);
      return;
    }

    // Check if user is logged in
    const accessToken = localStorage.getItem("access_token");
    const user = localStorage.getItem("user");
    
    if (!accessToken || !user) {
      setIsCheckingAuth(false);
      return;
    }

    // If we have permissions loaded (even if empty), we're done checking
    if (moduleAccessPermissions !== null) {
      setIsCheckingAuth(false);
    }
  }, [moduleAccessPermissions]);

  if (!moduleCode) {
    return children;
  }

  // Don't block on public pages
  const publicPaths = ['/login', '/register', '/forgot-password', '/check-reset-email', '/reset-password'];
  const currentPath = window.location.pathname;
  const isPublicPage = publicPaths.some(path => currentPath.startsWith(path));
  
  if (isPublicPage) {
    return children;
  }

  // Show loading only during initial auth check
  if (isCheckingAuth || (loading && moduleAccessPermissions === null)) {
    return null;
  }

  // If we have permissions but they're still loading (refresh), use existing permissions
  // This prevents redirect/reload during permission refresh
  if (moduleAccessPermissions === null) {
    // If no permissions and not loading, user might not be logged in
    // Don't redirect here, let axiosClient handle it
    return null;
  }

  // If permissionCode is ACCESS, check module access
  // Otherwise, check specific permission
  let hasAccess = false;
  
  if (permissionCode === "ACCESS") {
    const moduleCodeUpper = String(moduleCode).trim().toUpperCase();
    hasAccess = moduleAccessPermissions.get(moduleCodeUpper) === true;
  } else {
    // Use usePermission hook result
    if (permissionLoading) {
      return null; // Still loading permission
    }
    hasAccess = hasPermission;
  }

  if (!hasAccess) {
    return <Navigate to="/access-denied" replace />;
  }

  return children;
};

ProtectedRoute.propTypes = {
  moduleCode: PropTypes.string.isRequired,
  permissionCode: PropTypes.string,
  children: PropTypes.node.isRequired,
};

export default ProtectedRoute;

