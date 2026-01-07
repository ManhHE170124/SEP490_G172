import React from "react";
import PropTypes from "prop-types";
import { Navigate } from "react-router-dom";

/**
 * ProtectedRoute kiểm tra đăng nhập và role.
 * - Nếu chưa đăng nhập → redirect /login
 * - Nếu đã đăng nhập nhưng không có role phù hợp → redirect /not-found
 */
const ProtectedRoute = ({ children, allowedRoles }) => {
  const [isCheckingAuth, setIsCheckingAuth] = React.useState(true);
  const [isAuthenticated, setIsAuthenticated] = React.useState(false);
  const [hasRequiredRole, setHasRequiredRole] = React.useState(true);

  React.useEffect(() => {
    const publicPaths = [
      "/login",
      "/register",
      "/forgot-password",
      "/check-reset-email",
      "/reset-password",
    ];
    const currentPath = window.location.pathname;
    const isPublicPage = publicPaths.some((path) =>
      currentPath.startsWith(path)
    );

    if (isPublicPage) {
      setIsAuthenticated(true);
      setIsCheckingAuth(false);
      return;
    }

    const accessToken = localStorage.getItem("access_token");
    const userStr = localStorage.getItem("user");

    if (accessToken && userStr) {
      setIsAuthenticated(true);
      
      // Check role nếu allowedRoles được truyền vào
      if (allowedRoles && allowedRoles.length > 0) {
        try {
          const user = JSON.parse(userStr);
          // Parse user roles - handle both array and single role formats
          const roles = Array.isArray(user?.roles) 
            ? user.roles 
            : user?.role 
              ? [user.role] 
              : [];
          // Normalize roles to uppercase for comparison
          const userRoles = roles.map(r => {
            if (typeof r === "string") return r.toUpperCase();
            if (typeof r === "object") return (r.code || r.roleCode || r.name || "").toUpperCase();
            return "";
          }).filter(Boolean);
          
          // Normalize allowedRoles to uppercase
          const normalizedAllowedRoles = allowedRoles.map(r => r.toUpperCase());
          
          // Check if user has any of the allowed roles
          const hasRole = userRoles.some((role) => normalizedAllowedRoles.includes(role));
          setHasRequiredRole(hasRole);
        } catch {
          setHasRequiredRole(false);
        }
      }
    } else {
      setIsAuthenticated(false);
    }
    setIsCheckingAuth(false);
  }, [allowedRoles]);

  if (isCheckingAuth) {
    return null;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (!hasRequiredRole) {
    return <Navigate to="/not-found" replace />;
  }

  return children;
};

ProtectedRoute.propTypes = {
  children: PropTypes.node.isRequired,
  allowedRoles: PropTypes.arrayOf(PropTypes.string),
};

export default ProtectedRoute;

