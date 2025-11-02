import React from "react";
import { Link, useLocation } from "react-router-dom";
import "./Sidebar.css";

const Sidebar = () => {
  const location = useLocation();
  const currentPage = location.pathname.substring(1) || "home";

  return (
    <aside className="sb-sidebar" aria-label="Điều hướng">
      <div className="sb-logo" aria-label="Keytietkiem">
        <i>@</i>
        <span>Keytietkiem</span>
      </div>

      <nav className="sb-nav">
        <div className="sb-section-title">Tổng quan</div>
        <Link
          className={`sb-item ${currentPage === "home" ? "active" : ""}`}
          to="/home"
        >
          <svg viewBox="0 0 24 24">
            <path
              d="M3 11L12 3l9 8v9a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1v-9Z"
              fill="#111827"
            />
          </svg>
          <span className="sb-label">Dashboard</span>
        </Link>

        <Link
          className={`sb-item ${currentPage === "admin/products" ? "active" : ""}`}
          to="/admin/products"
        >
          <svg viewBox="0 0 24 24" fill="none">
            <path
              d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <circle cx="9" cy="7" r="4" stroke="currentColor" strokeWidth="2" />
            <path
              d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
          <span className="sb-label">Quản lý sản phẩm</span>
        </Link>
        <Link
          className={`sb-item ${currentPage === "admin/categories" ? "active" : ""}`}
          to="/admin/categories"
        >
          <svg viewBox="0 0 24 24" fill="none">
            <path
              d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <circle cx="9" cy="7" r="4" stroke="currentColor" strokeWidth="2" />
            <path
              d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
          <span className="sb-label">Quản lý danh mục</span>
        </Link>
        <div className="sb-section-title">RBAC</div>
        <Link
          className={`sb-item ${currentPage === "rbac" ? "active" : ""}`}
          to="/rbac"
        >
          <svg viewBox="0 0 24 24" fill="none">
            <path
              d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <circle cx="9" cy="7" r="4" stroke="currentColor" strokeWidth="2" />
            <path
              d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
          <span className="sb-label">Quản lý RBAC</span>
        </Link>

        <Link
          className={`sb-item ${currentPage === "roleassign" ? "active" : ""}`}
          to="/roleassign"
        >
          <svg viewBox="0 0 24 24" fill="none">
            <path
              d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <rect x="8" y="2" width="8" height="4" rx="1" ry="1" stroke="currentColor" strokeWidth="2" />
            <path
              d="M12 11h4M12 16h4M8 11h.01M8 16h.01"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
          <span className="sb-label">Phân công vai trò</span>
        </Link>

        <div className="sb-section-title">Quản lý người dùng</div>

        <Link
          className={`sb-item ${currentPage === "admin-user-management" ? "active" : ""}`}
          to="/admin-user-management"
        >
          <svg viewBox="0 0 24 24" fill="none">
            <path
              d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <rect x="8" y="2" width="8" height="4" rx="1" ry="1" stroke="currentColor" strokeWidth="2" />
            <path
              d="M12 11h4M12 16h4M8 11h.01M8 16h.01"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
          <span className="sb-label">Quản lý người dùng</span>
        </Link>
        <Link
          className={`sb-item ${currentPage === "admin/tickets" ? "active" : ""}`}
          to="/admin/tickets"
        >
          <svg viewBox="0 0 24 24" fill="none">
            <path
              d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <rect x="8" y="2" width="8" height="4" rx="1" ry="1" stroke="currentColor" strokeWidth="2" />
            <path
              d="M12 11h4M12 16h4M8 11h.01M8 16h.01"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
          <span className="sb-label">Quản lý ticket</span>
        </Link>
      </nav>
    </aside>
  );
};

// Sidebar không cần props nữa vì sử dụng useLocation

export default Sidebar;