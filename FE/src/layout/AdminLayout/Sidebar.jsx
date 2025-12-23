/**
 * File: Sidebar.jsx
 * Author: HieuNDHE173169
 * Purpose: Admin sidebar navigation component with collapsible menu.
 */

import React, { Fragment, useEffect, useState, useMemo } from "react";
import { Link, useLocation } from "react-router-dom";
import SidebarTooltip from "../../components/SidebarTooltip/SidebarTooltip.jsx";
import "./Sidebar.css";

// Role constants
const ROLE_ADMIN = "ADMIN";
const ROLE_STORAGE_STAFF = "STORAGE_STAFF";
const ROLE_CONTENT_CREATOR = "CONTENT_CREATOR";
const ROLE_CUSTOMER_CARE = "CUSTOMER_CARE";

const Sidebar = () => {
  const location = useLocation();
  const currentPage = location.pathname.substring(1) || "home";
  const [isCollapsed, setIsCollapsed] = useState(false);

  const toggleSidebar = () => {
    setIsCollapsed((prev) => !prev);
  };

  const handleLogoKeyDown = (event) => {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      toggleSidebar();
    }
  };

  useEffect(() => {
    document.body.classList.toggle("sb-collapsed", isCollapsed);
    return () => {
      document.body.classList.remove("sb-collapsed");
    };
  }, [isCollapsed]);

  const sidebarClassName = `sb-sidebar${isCollapsed ? " collapsed" : ""}`;
  const logoAriaLabel = isCollapsed ? "Mở rộng sidebar" : "Thu gọn sidebar";
  const logoProps = {
    className: "sb-logo",
    role: "button",
    tabIndex: 0,
    onClick: toggleSidebar,
    onKeyDown: handleLogoKeyDown,
    "aria-label": `${logoAriaLabel} - nhấn để chuyển trạng thái`,
    "aria-expanded": !isCollapsed,
  };

  // Parse user roles from localStorage
  const userRoles = useMemo(() => {
    try {
      const storedUser = localStorage.getItem("user");
      if (storedUser) {
        const parsedUser = JSON.parse(storedUser);
        const roles = Array.isArray(parsedUser?.roles) 
          ? parsedUser.roles 
          : parsedUser?.role 
            ? [parsedUser.role] 
            : [];
        return roles.map(r => {
          if (typeof r === "string") return r.toUpperCase();
          if (typeof r === "object") return (r.code || r.roleCode || r.name || "").toUpperCase();
          return "";
        }).filter(Boolean);
      }
    } catch (error) {
      console.warn("Không thể đọc roles từ localStorage", error);
    }
    return [];
  }, []);

  // Check if user has any of the allowed roles
  const hasRole = (allowedRoles) => {
    if (!allowedRoles || allowedRoles.length === 0) return true;
    return allowedRoles.some(role => userRoles.includes(role.toUpperCase()));
  };


  const defaultSections = [
    {
      id: "overview",
      title: "Tổng quan",
      items: [
        {
          id: "home",
          label: "Dashboard",
          allowedRoles: [ROLE_ADMIN],
          to: "/home",
          isActive: currentPage === "home",
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M3 11L12 3l9 8v9a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1v-9Z"
                fill="#111827"
              />
            </svg>
          ),
        },
      ],
    },
    {
      id: "product",
      title: "Quản lý sản phẩm",
      items: [
        {
          id: "products",
          label: "Quản lý sản phẩm",
          to: "/admin/products",
          allowedRoles: [ROLE_ADMIN, ROLE_STORAGE_STAFF],
          isActive:
            currentPage === "admin/products" ||
            currentPage.startsWith("admin/products/"),
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <circle
                cx="12"
                cy="7"
                r="4"
                stroke="currentColor"
                strokeWidth="2"
              />
            </svg>
          ),
        },
        {
          id: "categories",
          label: "Quản lý danh mục",
          to: "/admin/categories",
          allowedRoles: [ROLE_ADMIN, ROLE_STORAGE_STAFF],
          isActive: currentPage === "admin/categories",
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <circle
                cx="12"
                cy="7"
                r="4"
                stroke="currentColor"
                strokeWidth="2"
              />
            </svg>
          ),
        },

// === Danh sách đơn hàng ===
{
  id: "orders-admin-list",
  label: "Danh sách đơn hàng",
  to: "/admin/orders",
  allowedRoles: [ROLE_ADMIN],
  isActive:
    currentPage === "admin/orders" ||
    currentPage.startsWith("admin/orders/"),
  icon: (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width="200"
      height="200"
      viewBox="0 0 32 32"
    >
      <rect
        x="3"
        y="4"
        width="18"
        height="16"
        rx="2"
        ry="2"
        stroke="currentColor"
        strokeWidth="2"
        fill="none"
      />
      <path
        d="M3 9h18"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
      <path
        d="M8 13h4M8 17h3"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
    </svg>
  ),
},

// === Danh sách giao dịch ===
{
  id: "payments-admin",
  label: "Danh sách giao dịch",
  to: "/admin/payments",
  allowedRoles: [ROLE_ADMIN],
  isActive: currentPage === "admin/payments",
  icon: (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width="200"
      height="200"
      viewBox="0 0 32 32"
    >
      <rect
        x="2"
        y="5"
        width="20"
        height="14"
        rx="2"
        stroke="currentColor"
        strokeWidth="2"
        fill="none"
      />
      <path
        d="M2 10h20"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
      <path
        d="M6 15h4"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
      />
    </svg>
  ),
},

        {
          id: "faqs",
          label: "Câu hỏi thường gặp",
          to: "/admin/faqs",
          allowedRoles: [ROLE_ADMIN, ROLE_STORAGE_STAFF],
          isActive: currentPage === "admin/faqs",
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <circle
                cx="12"
                cy="7"
                r="4"
                stroke="currentColor"
                strokeWidth="2"
              />
            </svg>
          ),
        },
      ],
    },

    {
      id: "role",
      title: "Quản lý phân quyền",
      items: [
        {
          id: "role-manage",
          label: "Quản lý phân quyền",
          to: "/role-manage",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "role-manage",
          icon: (
            <svg
              xmlns="http://www.w3.org/2000/svg"
              width="200"
              height="200"
              viewBox="0 0 32 32"
            >
              <path
                fill="currentColor"
                d="M18 23h-2v-2a3.003 3.003 0 0 0-3-3H9a3.003 3.003 0 0 0-3 3v2H4v-2a5.006 5.006 0 0 1 5-5h4a5.006 5.006 0 0 1 5 5zM11 6a3 3 0 1 1-3 3a3 3 0 0 1 3-3m0-2a5 5 0 1 0 5 5a5 5 0 0 0-5-5zM2 26h28v2H2zM22 4v2h4.586L20 12.586L21.414 14L28 7.414V12h2V4h-8z"
              />
            </svg>
          ),
        },
        // {
        //   id: "role-assign",
        //   label: "Phân công vai trò",
        //   to: "/role-assign",
        //   allowedRoles: [ROLE_ADMIN],
        //   isActive: currentPage === "role-assign",
        //   icon: (
        //     <svg
        //       xmlns="http://www.w3.org/2000/svg"
        //       width="200"
        //       height="200"
        //       viewBox="0 0 48 48"
        //     >
        //       <g
        //         fill="none"
        //         stroke="currentColor"
        //         strokeLinecap="round"
        //         strokeWidth="4"
        //       >
        //         <path
        //           strokeLinejoin="round"
        //           d="M20 10H6a2 2 0 0 0-2 2v26a2 2 0 0 0 2 2h36a2 2 0 0 0 2-2v-2.5"
        //         />
        //         <path d="M10 23h8m-8 8h24" />
        //         <circle
        //           cx="34"
        //           cy="16"
        //           r="6"
        //           strokeLinejoin="round"
        //         />
        //         <path
        //           strokeLinejoin="round"
        //           d="M44 28.419C42.047 24.602 38 22 34 22s-5.993 1.133-8.05 3"
        //         />
        //       </g>
        //     </svg>
        //   ),
        // },
      ],
    },
    {
      id: "warehouse",
      title: "Kho & Nhà cung cấp",
      items: [
        {
          id: "suppliers-main",
          label: "Nhà cung cấp & License",
          to: "/suppliers",
          allowedRoles: [ROLE_ADMIN, ROLE_STORAGE_STAFF],
          isActive:
            currentPage === "suppliers" ||
            currentPage.startsWith("suppliers/"),
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <polyline
                points="3.27 6.96 12 12.01 20.73 6.96"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <line
                x1="12"
                y1="22.08"
                x2="12"
                y2="12"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          ),
        },
        {
          id: "key-monitor-main",
          label: "Theo dõi tình trạng",
          to: "/key-monitor",
          allowedRoles: [ROLE_ADMIN, ROLE_STORAGE_STAFF],
          isActive: currentPage === "key-monitor",
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M3 12h18M12 3v18"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
              />
            </svg>
          ),
        },
        {
          id: "keys-main",
          label: "Quản lý kho Key",
          to: "/keys",
          allowedRoles: [ROLE_ADMIN, ROLE_STORAGE_STAFF],
          isActive:
            currentPage === "keys" || currentPage.startsWith("keys/"),
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          ),
        },
        {
          id: "accounts-main",
          label: "Tài khoản chia sẻ",
          to: "/accounts",
          allowedRoles: [ROLE_ADMIN, ROLE_STORAGE_STAFF],
          isActive:
            currentPage === "accounts" ||
            currentPage.startsWith("accounts/"),
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <circle
                cx="9"
                cy="7"
                r="4"
                stroke="currentColor"
                strokeWidth="2"
              />
              <path
                d="M23 21v-2a4 4 0 0 0-3-3.87m-4-12a4 4 0 0 1 0 7.75"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          ),
        },
      ],
    },
    {
      id: "user",
      title: "Quản lý người dùng",
      items: [
        {
          id: "users",
          label: "Quản lý người dùng",
          to: "/admin/users",
          allowedRoles: [ROLE_ADMIN],
          isActive:
            currentPage === "admin/users" ||
            currentPage.startsWith("admin/users/"),
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <rect
                x="8"
                y="2"
                width="8"
                height="4"
                rx="1"
                ry="1"
                stroke="currentColor"
                strokeWidth="2"
              />
              <path
                d="M12 11h4M12 16h4M8 11h.01M8 16h.01"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          ),
        },
      ],
    },
    {
      id: "support",
      title: "Quản lý hỗ trợ",
      items: [
        {
          id: "dashboard",
          label: "Dashboard Hỗ trợ",
          to: "/admin/support-dashboard",
          allowedRoles: [ROLE_ADMIN, ROLE_CUSTOMER_CARE],
          isActive: currentPage === "admin/support-dashboard",
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M3 11L12 3l9 8v9a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1v-9Z"
                fill="#111827"
              />
            </svg>
          ),
        },
        {
          id: "tickets-admin",
          label: "Quản lý phiếu hỗ trợ",
          to: "/admin/tickets",
          allowedRoles: [ROLE_ADMIN],
          isActive:
            currentPage === "admin/tickets" ||
            currentPage.startsWith("admin/tickets/"),
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <circle
                cx="12"
                cy="7"
                r="4"
                stroke="currentColor"
                strokeWidth="2"
              />
            </svg>
          ),
        },
        {
          id: "tickets-staff",
          label: "Quản lý phiếu hỗ trợ",
          to: "/staff/tickets",
          allowedRoles: [ROLE_CUSTOMER_CARE],
          isActive:
            currentPage === "staff/tickets" ||
            currentPage.startsWith("staff/tickets/"),
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <circle
                cx="12"
                cy="7"
                r="4"
                stroke="currentColor"
                strokeWidth="2"
              />
            </svg>
          ),
        },
        {
          id: "support-chats-admin",
          label: "Quản lý chat hỗ trợ",
          to: "/admin/support-chats",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/support-chats",
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <circle
                cx="12"
                cy="7"
                r="4"
                stroke="currentColor"
                strokeWidth="2"
              />
            </svg>
          ),
        },
        {
          id: "support-chats-staff",
          label: "Quản lý chat hỗ trợ",
          to: "/staff/support-chats",
          allowedRoles: [ROLE_CUSTOMER_CARE],
          isActive: currentPage === "staff/support-chats",
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <circle
                cx="12"
                cy="7"
                r="4"
                stroke="currentColor"
                strokeWidth="2"
              />
            </svg>
          ),
        },
        {
          id: "product-reports",
          label: "Báo cáo sản phẩm",
          to: "/reports",
          allowedRoles: [ROLE_ADMIN, ROLE_CUSTOMER_CARE],
          isActive:
            currentPage === "reports" ||
            currentPage.startsWith("reports/"),
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path
                d="M14 2v6h6"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path
                d="M16 13H8"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path
                d="M16 17H8"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path
                d="M10 9H8"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          ),
        },
      ],
    },
    {
      id: "posts",
      title: "Quản lý bài viết",
      items: [
        {
          id: "post-dashboard",
          label: "Dashboard Bài viết",
          to: "/post-dashboard",
          allowedRoles: [ROLE_ADMIN, ROLE_CONTENT_CREATOR],
          isActive: currentPage === "post-dashboard",
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          ),
        },
        {
          id: "admin-post-list",
          label: "Danh sách bài viết",
          to: "/admin-post-list",
          allowedRoles: [ROLE_ADMIN, ROLE_CONTENT_CREATOR],
          isActive: currentPage === "admin-post-list",
          icon: (
            <svg
              xmlns="http://www.w3.org/2000/svg"
              width="200"
              height="200"
              viewBox="0 0 32 32"
            >
              <path
                fill="currentColor"
                d="M16 2c-1.26 0-2.15.89-2.59 2H5v25h22V4h-8.41c-.44-1.11-1.33-2-2.59-2zm0 2c.55 0 1 .45 1 1v1h3v2h-8V6h3V5c0-.55.45-1 1-1zM7 6h3v4h12V6h3v21H7V6zm2 7v2h2v-2H9zm4 0v2h10v-2H13zm-4 4v2h2v-2H9zm4 0v2h10v-2H13zm-4 4v2h2v-2H9zm4 0v2h10v-2H13z"
              />
            </svg>
          ),
        },
        {
          id: "post-create-edit",
          label: "Tạo bài viết",
          to: "/post-create-edit",
          allowedRoles: [ROLE_ADMIN, ROLE_CONTENT_CREATOR],
          isActive: currentPage === "post-create-edit",
          icon: (
            <svg viewBox="0 0 24 24" fill="none">
              <path
                d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <polyline
                points="14 2 14 8 20 8"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <line
                x1="9"
                y1="15"
                x2="15"
                y2="15"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <line
                x1="12"
                y1="12"
                x2="12"
                y2="18"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          ),
        },
        {
          id: "tag-post-type",
          label: "Quản lý Thẻ và Danh mục",
          to: "/tag-post-type-manage",
          allowedRoles: [ROLE_ADMIN, ROLE_CONTENT_CREATOR],
          isActive: currentPage === "tag-post-type-manage",
          icon: (
            <svg
              xmlns="http://www.w3.org/2000/svg"
              width="200"
              height="200"
              viewBox="0 0 32 32"
            >
              <path
                fill="currentColor"
                d="m14.594 4l-.313.281l-11 11l-.687.719l.687.719l9 9l.719.687l.719-.687l11-11l.281-.313V4zm.844 2H23v7.563l-10 10L5.437 16zM26 7v2h1v8.156l-9.5 9.438l-1.25-1.25l-1.406 1.406l1.937 1.969l.719.687l.688-.687l10.53-10.407L29 18V7zm-6 1c-.55 0-1 .45-1 1s.45 1 1 1s1-.45 1-1s-.45-1-1-1z"
              />
            </svg>
          ),
        },
      ],
    },
    {
      id: "settings",
      title: "Cài đặt",
      items: [
        {
          id: "website-config",
          label: "Cấu hình trang web",
          to: "/admin/website-config",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/website-config",
          icon: (
            <svg viewBox="0 0 24 24" fill="none" style={{ width: 20, height: 20 }}>
              <path
                d="M12 15.5A3.5 3.5 0 1 0 12 8.5a3.5 3.5 0 0 0 0 7z"
                stroke="currentColor"
                strokeWidth="1.5"
              />
              <path
                d="M19.4 15a7 7 0 0 0 0-6M4.6 15a7 7 0 0 1 0-6"
                stroke="currentColor"
                strokeWidth="1.2"
              />
            </svg>
          ),
        },
        {
          id: "support-priority-loyalty-rules",
          label: "Cấu hình quy tắc hỗ trợ",
          to: "/admin/support-priority-loyalty-rules",
          allowedRoles: [ROLE_ADMIN, ROLE_CUSTOMER_CARE],
          isActive: currentPage === "admin/support-priority-loyalty-rules",
          icon: (
            <svg viewBox="0 0 24 24" fill="none" style={{ width: 20, height: 20 }}>
              <path
                d="M12 15.5A3.5 3.5 0 1 0 12 8.5a3.5 3.5 0 0 0 0 7z"
                stroke="currentColor"
                strokeWidth="1.5"
              />
              <path
                d="M19.4 15a7 7 0 0 0 0-6M4.6 15a7 7 0 0 1 0-6"
                stroke="currentColor"
                strokeWidth="1.2"
              />
            </svg>
          ),
        },
        {
          id: "support-plans-admin",
          label: "Cấu hình gói hỗ trợ",
          to: "/admin/support-plans",
          allowedRoles: [ROLE_ADMIN, ROLE_CUSTOMER_CARE],
          isActive: currentPage === "admin/support-plans",
          icon: (
            <svg viewBox="0 0 24 24" fill="none" style={{ width: 20, height: 20 }}>
              <path
                d="M5 7h14M5 12h9M5 17h6"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
              />
              <circle
                cx="17.5"
                cy="17.5"
                r="2"
                stroke="currentColor"
                strokeWidth="1.5"
              />
            </svg>
          ),
        },
        {
          id: "sla-rules-admin",
          label: "Cấu hình SLA",
          to: "/admin/sla-rules",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/sla-rules",
          icon: (
            <svg
              viewBox="0 0 24 24"
              fill="none"
              style={{ width: 20, height: 20 }}
            >
              {/* icon đồng hồ SLA */}
              <circle
                cx="12"
                cy="12"
                r="8"
                stroke="currentColor"
                strokeWidth="1.5"
              />
              <path
                d="M12 8v4l2.5 2.5"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path
                d="M9 3h6"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
              />
            </svg>
          ),
        },
        {
          id: "ticket-subject-templates",
          label: "Cấu hình mẫu chủ đề ticket",
          to: "/admin/ticket-subject-templates",
          allowedRoles: [ROLE_ADMIN, ROLE_CUSTOMER_CARE],
          isActive: currentPage === "admin/ticket-subject-templates",
          icon: (
            <svg viewBox="0 0 24 24" fill="none" style={{ width: 20, height: 20 }}>
              <path
                d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path
                d="M14 2v6h6"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path
                d="M9 13h6M9 17h4"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          ),
        }
        , {
          id: "notifications-admin",
          label: "Thông báo hệ thống",
          to: "/admin/notifications",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/notifications",
          icon: (
            <svg
              viewBox="0 0 24 24"
              fill="none"
              style={{ width: 20, height: 20 }}
            >
              <path
                d="M12 3a6 6 0 0 0-6 6v3.5L4 15v1h16v-1l-2-2.5V9a6 6 0 0 0-6-6Z"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path
                d="M10 19a2 2 0 0 0 4 0"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          ),
        }, {
          id: "audit-logs",
          label: "Lịch sử thao tác hệ thống",
          to: "/admin/audit-logs",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/audit-logs",
          icon: (
            <svg
              viewBox="0 0 24 24"
              fill="none"
              style={{ width: 20, height: 20 }}
            >
              <rect
                x="3"
                y="4"
                width="18"
                height="16"
                rx="2"
                ry="2"
                stroke="currentColor"
                strokeWidth="1.5"
              />
              <path
                d="M7 9h10M7 13h6M7 17h4"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
              />
            </svg>
          ),
        }


      ],
    },
  ];

  const hasItemAccess = (item) => {
    if (!item.allowedRoles || item.allowedRoles.length === 0) return true;
    return hasRole(item.allowedRoles);
  };

  const sectionsToRender = defaultSections
    .map((section) => ({
      ...section,
      items: section.items.filter(hasItemAccess),
    }))
    .filter((section) => section.items.length > 0);

  const renderNavItem = (item) => {
    const linkLabel = item.title || item.label;
    const linkProps = {
      "aria-label": item.ariaLabel || item.label,
    };

    if (!isCollapsed) {
      linkProps.title = linkLabel;
    }

    if (item.dataLabel) {
      linkProps["data-label"] = item.dataLabel;
    }

    return (
      <SidebarTooltip
        key={item.id}
        label={item.label}
        disabled={!isCollapsed}
      >
        <Link
          className={`sb-item ${item.isActive ? "active" : ""}`}
          to={item.to}
          {...linkProps}
        >
          {item.icon}
          <span className="sb-label">{item.label}</span>
        </Link>
      </SidebarTooltip>
    );
  };

  const renderNavSection = (section) => (
    <Fragment key={section.id}>
      {section.title && <div className="sb-section-title">{section.title}</div>}
      {section.items.map((item) => renderNavItem(item))}
    </Fragment>
  );

  return (
    <aside className={sidebarClassName} aria-label="Điều hướng">
      <div {...logoProps}>
        <i>@</i>
        <span>Keytietkiem</span>
      </div>
      <nav className="sb-nav">
        {sectionsToRender.length === 0 ? (
          <div className="sb-empty">Bạn chưa được cấp quyền hiển thị menu.</div>
        ) : (
          sectionsToRender.map((section) => renderNavSection(section))
        )}
      </nav>
    </aside>
  );
};

export default Sidebar;
