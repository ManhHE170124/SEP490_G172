import React from "react";
import PropTypes from "prop-types";
import { Navigate } from "react-router-dom";

/**
 * ProtectedRoute chỉ kiểm tra đăng nhập.
 * Phân quyền chi tiết (module/permission) được xử lý hoàn toàn ở backend.
 */
const ProtectedRoute = ({ children }) => {
  const [isCheckingAuth, setIsCheckingAuth] = React.useState(true);
  const [isAuthenticated, setIsAuthenticated] = React.useState(false);

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
    const user = localStorage.getItem("user");

    if (accessToken && user) {
      setIsAuthenticated(true);
    } else {
      setIsAuthenticated(false);
    }
    setIsCheckingAuth(false);
  }, []);

  if (isCheckingAuth) {
    return null;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return children;
};

ProtectedRoute.propTypes = {
  children: PropTypes.node.isRequired,
};

export default ProtectedRoute;

