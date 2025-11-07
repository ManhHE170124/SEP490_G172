/**
 * File: Sidebar.jsx
 * Author: HieuNDHE173169
 * Created: 18/10/2025
 * Last Updated: 29/10/2025
 * Version: 1.0.0
 * Purpose: Admin sidebar navigation component. Provides navigation menu for admin pages.
 */

import React from "react";
import { Link, useLocation } from "react-router-dom";
import "./Sidebar.css";

/**
 * @summary: Sidebar navigation component for admin layout.
 * @returns {JSX.Element} - Sidebar with navigation links
 */
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

        <div className="sb-section-title">Quản lý phân quyền</div>

        <Link
          className={`sb-item ${currentPage === "role-manage" ? "active" : ""}`}
          to="/role-manage"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 32 32">
            <path fill="currentColor" d="M18 23h-2v-2a3.003 3.003 0 0 0-3-3H9a3.003 3.003 0 0 0-3 3v2H4v-2a5.006 5.006 0 0 1 5-5h4a5.006 5.006 0 0 1 5 5zM11 6a3 3 0 1 1-3 3a3 3 0 0 1 3-3m0-2a5 5 0 1 0 5 5a5 5 0 0 0-5-5zM2 26h28v2H2zM22 4v2h4.586L20 12.586L21.414 14L28 7.414V12h2V4h-8z" />
          </svg>
          <span className="sb-label">Quản lý phân quyền</span>
        </Link>

        <Link
          className={`sb-item ${currentPage === "role-assign" ? "active" : ""}`}
          to="/role-assign"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 48 48">
            <g fill="none" stroke="currentColor" stroke-linecap="round" stroke-width="4">
              <path stroke-linejoin="round" d="M20 10H6a2 2 0 0 0-2 2v26a2 2 0 0 0 2 2h36a2 2 0 0 0 2-2v-2.5" />
              <path d="M10 23h8m-8 8h24" />
              <circle cx="34" cy="16" r="6" stroke-linejoin="round" />
              <path stroke-linejoin="round" d="M44 28.419C42.047 24.602 38 22 34 22s-5.993 1.133-8.05 3" />
            </g>
          </svg>
          <span className="sb-label">Phân công vai trò</span>
        </Link>

        <div className="sb-section-title">Kho & Nhà cung cấp</div>

        <Link
          className={`sb-item ${currentPage === "suppliers" || currentPage.startsWith("suppliers/") ? "active" : ""}`}
          to="/suppliers"
        >
          <svg viewBox="0 0 24 24" fill="none">
            <path
              d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <polyline points="3.27 6.96 12 12.01 20.73 6.96" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            <line x1="12" y1="22.08" x2="12" y2="12" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          <span className="sb-label">Nhà cung cấp & License</span>
        </Link>

        <Link
          className={`sb-item ${currentPage === "keys" || currentPage.startsWith("keys/") ? "active" : ""}`}
          to="/keys"
        >
          <svg viewBox="0 0 24 24" fill="none">
            <path
              d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
          <span className="sb-label">Quản lý kho Key</span>
        </Link>

        <div className="sb-section-title">Quản lý người dùng</div>

        <Link
          className={`sb-item ${currentPage === "admin-user-management" ? "active" : ""}`}
          to="/admin-user-management"
        >
          <svg viewBox="0 0 24 24" fill="none">
            <path
              d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <circle cx="12" cy="7" r="4" stroke="currentColor" strokeWidth="2" />
          </svg>
          <span className="sb-label">Quản lý người dùng</span>
        </Link>


        <div className="sb-section-title">Quản lý bài viết</div>

        <Link
          className={`sb-item ${currentPage === "admin-post-list" ? "active" : ""}`}
          to="/admin-post-list"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 32 32">
            <path fill="currentColor" d="M16 2c-1.26 0-2.15.89-2.59 2H5v25h22V4h-8.41c-.44-1.11-1.33-2-2.59-2zm0 2c.55 0 1 .45 1 1v1h3v2h-8V6h3V5c0-.55.45-1 1-1zM7 6h3v4h12V6h3v21H7V6zm2 7v2h2v-2H9zm4 0v2h10v-2H13zm-4 4v2h2v-2H9zm4 0v2h10v-2H13zm-4 4v2h2v-2H9zm4 0v2h10v-2H13z" />
          </svg>
          <span className="sb-label">Danh sách bài viết</span>
        </Link>

        <Link
          className={`sb-item ${currentPage === "post-create-edit" ? "active" : ""}`}
          to="/post-create-edit"
        >
          <svg viewBox="0 0 24 24" fill="none">
            <path
              d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <polyline points="14 2 14 8 20 8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            <line x1="9" y1="15" x2="15" y2="15" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            <line x1="12" y1="12" x2="12" y2="18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          <span className="sb-label">Tạo bài viết</span>
        </Link>

        <Link
          className={`sb-item ${currentPage === "tag-post-type-manage" ? "active" : ""}`}
          to="/tag-post-type-manage"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 32 32">
            <path fill="currentColor" d="m14.594 4l-.313.281l-11 11l-.687.719l.687.719l9 9l.719.687l.719-.687l11-11l.281-.313V4zm.844 2H23v7.563l-10 10L5.437 16zM26 7v2h1v8.156l-9.5 9.438l-1.25-1.25l-1.406 1.406l1.937 1.969l.719.687l.688-.687l10.53-10.407L29 18V7zm-6 1c-.55 0-1 .45-1 1s.45 1 1 1s1-.45 1-1s-.45-1-1-1z" />
          </svg>
          <span className="sb-label">Quản lý Thẻ và Danh mục</span>
        </Link>


        <div className="sb-section-title">Cài đặt</div>
        <Link
          className={`sb-item ${currentPage === "admin/website-config" ? "active" : ""}`}
          to="/admin/website-config"
        >
          <svg viewBox="0 0 24 24" fill="none" style={{ width: 20, height: 20 }}>
            <path d="M12 15.5A3.5 3.5 0 1 0 12 8.5a3.5 3.5 0 0 0 0 7z" stroke="currentColor" strokeWidth="1.5" />
            <path d="M19.4 15a7 7 0 0 0 0-6M4.6 15a7 7 0 0 1 0-6" stroke="currentColor" strokeWidth="1.2" />
          </svg>
          <span className="sb-label">Cấu hình trang web</span>
        </Link>
      </nav>
    </aside>
  );
};

// Sidebar không cần props nữa vì sử dụng useLocation

export default Sidebar;