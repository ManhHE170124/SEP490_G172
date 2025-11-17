import React from "react";
import { Link } from "react-router-dom";
import "./AccessDenied.css";

const AccessDenied = () => {
  return (
    <div className="ad-wrapper">
      <div className="ad-card">
        <div className="ad-icon" aria-hidden="true">
          🔒
        </div>
        <h1>Không có quyền truy cập</h1>
        <p>
          Bạn không có quyền truy cập vào khu vực này. Vui lòng liên hệ quản trị
          viên nếu bạn nghĩ đây là nhầm lẫn.
        </p>
        <div className="ad-actions">
          <Link to="/" className="ad-btn ad-btn--ghost">
            Về trang chủ
          </Link>
          <button
            type="button"
            className="ad-btn ad-btn--primary"
            onClick={() => window.history.back()}
          >
            Quay lại
          </button>
        </div>
      </div>
    </div>
  );
};

export default AccessDenied;

