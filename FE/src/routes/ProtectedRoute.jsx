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
          const userRoles = user.roles || [];
          const hasRole = userRoles.some((role) => allowedRoles.includes(role));
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

