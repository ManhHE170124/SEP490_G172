import React from "react";
import PropTypes from "prop-types";
import { Navigate } from "react-router-dom";
import { usePermissions } from "../context/PermissionContext";

const ProtectedRoute = ({ moduleCode, children }) => {
  
  const { allowedModuleCodes, loading } = usePermissions();

  if (!moduleCode) {
    return children;
  }

  if (loading || allowedModuleCodes === null) {
    return null;
  }

  if (!allowedModuleCodes.has(moduleCode)) {
    return <Navigate to="/access-denied" replace />;
  }

  return children;
};

ProtectedRoute.propTypes = {
  moduleCode: PropTypes.string.isRequired,
  children: PropTypes.node.isRequired,
};

export default ProtectedRoute;

