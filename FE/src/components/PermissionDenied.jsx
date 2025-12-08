/**
 * File: PermissionDenied.jsx
 * Author: Keytietkiem Team
 * Created: 29/10/2025
 * Version: 1.0.0
 * Purpose: Component to display when user doesn't have permission to access a resource.
 *          Provides user-friendly feedback about missing permissions.
 * Usage:
 *   <PermissionDenied message="Bạn không có quyền tạo bài viết" />
 */

import React from "react";
import PropTypes from "prop-types";
import "./PermissionDenied.css";

/**
 * Component to display permission denied message
 * @param {string} message - Custom message to display (optional)
 * @returns {ReactNode} - Permission denied UI
 */
const PermissionDenied = ({ message = "Bạn không có quyền truy cập tài nguyên này." }) => {
  return (
    <div className="permission-denied">
      <div className="permission-denied-icon">🔒</div>
      <h3 className="permission-denied-title">Không có quyền truy cập</h3>
      <p className="permission-denied-message">{message}</p>
    </div>
  );
};

PermissionDenied.propTypes = {
  message: PropTypes.string,
};

export default PermissionDenied;

