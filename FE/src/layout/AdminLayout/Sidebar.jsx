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
          <span className="sb-label">Quản lý phân quyền</span>
        </Link>
        
        <Link 
          className={`sb-item ${currentPage === "role-assign" ? "active" : ""}`}
          to="/role-assign"
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
            <polyline points="3.27 6.96 12 12.01 20.73 6.96" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            <line x1="12" y1="22.08" x2="12" y2="12" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
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
            <circle cx="12" cy="7" r="4" stroke="currentColor" strokeWidth="2"/>
          </svg>
          <span className="sb-label">Quản lý người dùng</span>
        </Link>


        <div className="sb-section-title">Quản lý bài viết</div>

        <Link 
          className={`sb-item ${currentPage === "admin-post-list" ? "active" : ""}`}
          to="/admin-post-list"
        >
          <svg viewBox="0 0 24 24" fill="none">
            <path
              d="M4 7h16M4 12h16M4 17h16"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
            />
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
            <polyline points="14 2 14 8 20 8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            <line x1="9" y1="15" x2="15" y2="15" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            <line x1="12" y1="12" x2="12" y2="18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
          <span className="sb-label">Tạo bài viết</span>
        </Link>

<Link 
          className={`sb-item ${currentPage === "post-type-manage" ? "active" : ""}`}
          to="/post-type-manage"
        >
          <svg viewBox="0 0 24 24" fill="none">
            <path
              d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <polyline points="14 2 14 8 20 8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            <line x1="9" y1="15" x2="15" y2="15" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
            <line x1="12" y1="12" x2="12" y2="18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
          <span className="sb-label">Quản lý danh mục bài viết</span>
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