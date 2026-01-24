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
  const [isCollapsed, setIsCollapsed] = useState(() => {
    try {
      const saved = localStorage.getItem("admin-sidebar-collapsed");
      return saved === "true"; // Convert string to boolean
    } catch {
      return false; // Default to open if localStorage fails
    }
  });

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

  useEffect(() => {
    try {
      localStorage.setItem("admin-sidebar-collapsed", isCollapsed.toString());
    } catch (error) {
      // Silently fail if localStorage is unavailable (e.g., private mode)
      console.warn("Failed to save sidebar state:", error);
    }
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
        return roles
          .map((r) => {
            if (typeof r === "string") return r.toUpperCase();
            if (typeof r === "object")
              return (r.code || r.roleCode || r.name || "").toUpperCase();
            return "";
          })
          .filter(Boolean);
      }
    } catch (error) {
      console.warn("Không thể đọc roles từ localStorage", error);
    }
    return [];
  }, []);

  // Check if user has any of the allowed roles
  const hasRole = (allowedRoles) => {
    if (!allowedRoles || allowedRoles.length === 0) return true;
    return allowedRoles.some((role) => userRoles.includes(role.toUpperCase()));
  };

  const defaultSections = [
    {
      id: "overview",
      title: "Tổng quan",
      items: [
        // {
        //   id: "home",
        //   label: "Dashboard",
        //   allowedRoles: [ROLE_ADMIN],
        //   to: "/admin/home",
        //   isActive: currentPage === "admin/home",
        //   icon: (
        //     <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 20 20"><path fill="#000000" d="M18.178 11.373a.7.7 0 0 1 .7.7v5.874c.027.812-.071 1.345-.434 1.68c-.338.311-.828.4-1.463.366H3.144C2.5 19.961 2 19.7 1.768 19.173c-.154-.347-.226-.757-.226-1.228v-5.873a.7.7 0 0 1 1.4 0v5.873c0 .232.026.42.07.562l.036.098l-.003-.01c.001-.013.03-.008.132-.002h13.84c.245.014.401 0 .456-.001l.004-.001c-.013-.053.012-.27 0-.622v-5.897a.7.7 0 0 1 .701-.7ZM10.434 0c.264 0 .5.104.722.297l8.625 8.139a.7.7 0 1 1-.962 1.017l-8.417-7.944l-9.244 7.965a.7.7 0 0 1-.915-1.06L9.689.277l.086-.064c.214-.134.428-.212.66-.212Z" /></svg>
        //   ),
        // },
        {
          id: "order-dashboard-admin",
          label: "Dashboard đơn hàng",
          to: "/admin/orders/dashboard",
          allowedRoles: [ROLE_ADMIN],
          isActive:
            currentPage === "admin/orders/dashboard",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 32 32"><path fill="#000000" d="M24 21h2v5h-2zm-4-5h2v10h-2zm-9 10a5.006 5.006 0 0 1-5-5h2a3 3 0 1 0 3-3v-2a5 5 0 0 1 0 10z" /><path fill="#000000" d="M28 2H4a2.002 2.002 0 0 0-2 2v24a2.002 2.002 0 0 0 2 2h24a2.003 2.003 0 0 0 2-2V4a2.002 2.002 0 0 0-2-2Zm0 9H14V4h14ZM12 4v7H4V4ZM4 28V13h24l.002 15Z" /></svg>),
        },
        {
          id: "payments-dashboard-admin",
          label: "Dashboard thanh toán",
          to: "/admin/payments/dashboard",
          allowedRoles: [ROLE_ADMIN],
          isActive:
            currentPage === "admin/payments/dashboard",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 512 512" fill="#000000"><g fill="#000000" fill-rule="evenodd" clip-rule="evenodd"><path d="M426.667 125.489H85.333v20.078h341.334zM85.333 386.508V185.724h341.334v200.784zM42.667 85.332v341.333h426.666V85.332zm320 149.333v128h42.666v-128zm-64 128v-85.333h42.666v85.333zm-64-21.333v21.333h42.666v-21.333z" /><path d="M170.667 362.665c41.237 0 74.666-33.429 74.666-74.666c0-41.238-33.429-74.667-74.666-74.667c-41.238 0-74.667 33.429-74.667 74.667s33.429 74.666 74.667 74.666m35.476-50.962a42.67 42.67 0 0 0 7.19-23.704h-42.666v-42.667a42.66 42.66 0 0 0-39.419 26.339a42.664 42.664 0 0 0 31.095 58.175a42.67 42.67 0 0 0 43.8-18.143" /></g></svg>),
        },
        {
          id: "system-insights-dashboard-admin",
          label: "Giám sát hệ thống",
          to: "/admin/system-insights",
          allowedRoles: [ROLE_ADMIN],
          isActive:
            currentPage === "admin/system-insights",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 24 24"><path fill="#000000" fill-rule="evenodd" d="M15 8h4v12H5V10h4V4h6v4Zm-2-2h-2v12h2V6Zm2 4v8h2v-8h-2Zm-6 2v6H7v-6h2Z" clip-rule="evenodd" /></svg>
          ),
        },
        {
          id: "user-dashboard",
          label: "Dashboard người dùng",
          to: "/admin/user-dashboard",
          allowedRoles: [ROLE_ADMIN],
          isActive:
            currentPage === "admin/user-dashboard" ||
            currentPage.startsWith("admin/user-dashboard/"),
          icon: (
            <svg width="200" height="200" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="none" stroke="#000000" stroke-width="1.5" d="M20 16.5a2.5 2.5 0 1 1-5 0a2.5 2.5 0 0 1 5 0ZM13.5 23v-1.5s1.5-1 4-1s4 1 4 1V23m-11-3H5v-5.5m14-5V4h-5.5M9 4.5a2.5 2.5 0 1 1-5 0a2.5 2.5 0 0 1 5 0ZM2.5 11V9.5s1.5-1 4-1s4 1 4 1V11" /></svg>
          ),
        },
        {
          id: "dashboard",
          label: "Dashboard Hỗ trợ",
          to: "/admin/support-dashboard",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/support-dashboard",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 48 48" fill="#000000"><g fill="none" stroke="#000000" stroke-linecap="round" stroke-linejoin="round" stroke-width="3"><path d="M30.24 6.212q.387.213.76.444c4.937 3.058 8.238 8.46 8.313 14.63l3.747 5.223c.563.785.465 1.846-.363 2.344c-.83.5-2.063 1.084-3.734 1.488l-.566 6.718a3 3 0 0 1-3.358 2.725l-2.8-.346V43a2 2 0 0 1-1.998 2H13.014a2 2 0 0 1-1.999-2v-7.5c-4.295-3.192-7.075-8.275-7.075-14c0-6.26 3.321-11.75 8.315-14.844" /><path d="M17.023 14.057a8 8 0 1 0 7.954 0Q24.998 12.738 25 11c0-2.551-.044-4.405-.09-5.645c-.043-1.2-.854-2.187-2.05-2.285A24 24 0 0 0 21 3c-.732 0-1.35.029-1.86.07c-1.196.098-2.007 1.085-2.05 2.285C17.043 6.595 17 8.449 17 11q.001 1.739.023 3.057" /><path d="M21 29c3 3 7.6 5 12 5" /></g></svg>
          ),
        },
        {
          id: "post-dashboard",
          label: "Dashboard Bài viết",
          to: "/post-dashboard",
          allowedRoles: [ROLE_ADMIN, ROLE_CONTENT_CREATOR],
          isActive: currentPage === "post-dashboard",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 24 24"><path fill="#000000" d="M21 13.1c-.1 0-.3.1-.4.2l-1 1l2.1 2.1l1-1c.2-.2.2-.6 0-.8l-1.3-1.3c-.1-.1-.2-.2-.4-.2m-1.9 1.8l-6.1 6V23h2.1l6.1-6.1l-2.1-2M21 3h-8v6h8V3m-2 4h-4V5h4v2m-6 11.06V11h8v.1c-.76 0-1.43.4-1.81.79L18.07 13H15v3.07l-2 1.99M11 3H3v10h8V3m-2 8H5V5h4v6m2 9.06V15H3v6h8v-.94M9 19H5v-2h4v2Z" /></svg>),
        },
        {
          id: "key-monitor-main",
          label: "Quản lý tình trạng kho",
          to: "/key-monitor",
          allowedRoles: [ROLE_ADMIN, ROLE_STORAGE_STAFF],
          isActive: currentPage === "key-monitor",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 24 24"><path fill="none" stroke="#000000" stroke-linecap="round" stroke-width="2" d="M5 19h4m6 0h4m-6.963-4.384V8.634L17 5.94m-4.93 2.662L7.042 5.94M12 2.997l5.033 2.906v5.812L12 14.62l-5.033-2.906V5.903zM14 19a2 2 0 1 1-4 0a2 2 0 0 1 4 0Z" /></svg>
          ),
        },
      ],
    },
    {
      id: "notifications",
      title: "Quản lý thông báo",
      items: [
        {
          id: "notifications-admin",
          label: "Thông báo hệ thống",
          to: "/admin/notifications",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/notifications",
          icon: (
            <svg viewBox="0 0 24 24" fill="none" style={{ width: 20, height: 20 }}>
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
        },
        {
          id: "audit-logs",
          label: "Lịch sử thao tác hệ thống",
          to: "/admin/audit-logs",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/audit-logs",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 512 512"><path fill="#000000" fill-rule="evenodd" d="M490.667 448v42.667H384V448zm-128 0v42.667H320V448zm128-64v42.667H384V384zm-128 0v42.667H320V384zM165.49 303.843c23.164 23.164 55.164 37.49 90.51 37.49c14.96 0 29.322-2.566 42.668-7.283v44.573C285.031 382.133 270.733 384 256 384c-47.129 0-89.796-19.103-120.68-49.988zM490.667 320v42.667H384V320zm-128 0v42.667H320V320zM256 42.667c94.256 0 170.667 76.41 170.667 170.667c0 31.086-8.312 60.23-22.833 85.334l-52.43.002C371.675 276.024 384 246.118 384 213.334c0-70.693-57.308-128-128-128s-128 57.307-128 128c0 4.025.186 8.008.55 11.938l20.783-20.775l30.17 30.17l-72.836 72.837l-72.837-72.837L64 204.497l21.82 21.812q-.486-6.426-.487-12.975c0-94.257 76.41-170.667 170.667-170.667m21.333 64v95.147l54.4 36.48l-23.466 35.413l-73.6-48.853V106.667z" /></svg>
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
            currentPage === "admin/users" || currentPage.startsWith("admin/users/"),
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 24 24"><path fill="#000000" d="M10 12q-1.65 0-2.825-1.175T6 8q0-1.65 1.175-2.825T10 4q1.65 0 2.825 1.175T14 8q0 1.65-1.175 2.825T10 12Zm-8 8v-2.8q0-.825.425-1.55t1.175-1.1q1.275-.65 2.875-1.1T10 13h.35q.15 0 .3.05q-.2.45-.338.938T10.1 15H10q-1.775 0-3.187.45t-2.313.9q-.225.125-.363.35T4 17.2v.8h6.3q.15.525.4 1.038t.55.962H2Zm14 1l-.3-1.5q-.3-.125-.563-.263T14.6 18.9l-1.45.45l-1-1.7l1.15-1q-.05-.35-.05-.65t.05-.65l-1.15-1l1-1.7l1.45.45q.275-.2.538-.337t.562-.263L16 11h2l.3 1.5q.3.125.563.275t.537.375l1.45-.5l1 1.75l-1.15 1q.05.3.05.625t-.05.625l1.15 1l-1 1.7l-1.45-.45q-.275.2-.537.338t-.563.262L18 21h-2Zm1-3q.825 0 1.413-.588T19 16q0-.825-.588-1.413T17 14q-.825 0-1.413.588T15 16q0 .825.588 1.413T17 18Zm-7-8q.825 0 1.413-.588T12 8q0-.825-.588-1.413T10 6q-.825 0-1.413.588T8 8q0 .825.588 1.413T10 10Zm0-2Zm.3 10Z" /></svg>
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
          allowedRoles: [ROLE_ADMIN],
          isActive:
            currentPage === "admin/products" ||
            currentPage.startsWith("admin/products/"),
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 512 512"><path fill="#000000" fill-rule="evenodd" d="M468.915 401.333q.345 1.631.406 3.295l.013.706v21.333c0 23.564-42.981 42.667-96 42.667c-52.49 0-95.14-18.723-95.987-41.961l-.013-.706v-21.333l.013-.706q.06-1.66.402-3.288c5.88 4.419 13.037 8.494 21.331 11.983c19.36 8.144 45.463 13.344 74.254 13.344c29.932 0 56.956-5.629 76.546-14.335c7.323-3.255 13.697-6.982 19.035-10.999M234.667 34.347l192 106.667l.001 78.722c-15.727-4.038-33.92-6.402-53.334-6.402c-29.239 0-55.704 5.375-75.228 14.052c-26.733 11.882-40.343 30.052-42.063 48.441l-.049.557l.122 172.713l-21.449 11.917l-192-106.667V141.014zm234.248 302.986q.345 1.631.406 3.295l.013.706v21.333c0 23.564-42.981 42.667-96 42.667c-52.49 0-95.14-18.723-95.987-41.961l-.013-.706v-21.333l.013-.706q.06-1.66.402-3.288c5.88 4.419 13.037 8.494 21.331 11.983c19.36 8.144 45.463 13.344 74.254 13.344c29.932 0 56.956-5.629 76.546-14.335c7.323-3.255 13.697-6.982 19.035-10.999M170.666 233.455l.001 144.598l42.667 23.704V257.158zm-85.332-47.406v144.594L128 354.348V209.752zm288 48.618c52.489 0 95.14 18.722 95.987 41.961l.013.706v21.333c0 23.564-42.981 42.667-96 42.667c-52.49 0-95.14-18.723-95.987-41.961l-.013-.706v-21.333l.142-2.341c2.734-22.476 44.606-40.326 95.858-40.326m-54.676-106.251l-125.579 70.086l41.588 23.104l125.867-69.926zm-83.991-46.662L108.8 151.68l41.662 23.146L276.04 104.74z" /></svg>
          ),
        },
        {
          id: "categories",
          label: "Quản lý danh mục",
          to: "/admin/categories",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/categories",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 48 48"><g fill="none"><rect width="36" height="14" x="6" y="28" stroke="#000000" stroke-width="4" rx="4" /><path stroke="#000000" stroke-linecap="round" stroke-width="4" d="M20 7H10a4 4 0 0 0-4 4v6a4 4 0 0 0 4 4h10" /><circle cx="34" cy="14" r="8" stroke="#000000" stroke-width="4" /><circle cx="34" cy="14" r="3" fill="#000000" /></g></svg>
          ),
        },
        {
          id: "faqs",
          label: "Câu hỏi thường gặp",
          to: "/admin/faqs",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/faqs",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 24 24" fill="#000000"><g fill="none" stroke="#000000" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"><path d="M4.842 13.657V7.691A1.49 1.49 0 0 1 6.334 6.2h1.491m-2.983 4.474h2.237m3.13 2.983V7.691a1.492 1.492 0 1 1 2.983 0v5.966m-2.983-2.983h2.983m5.966 1.491a1.492 1.492 0 1 1-2.983 0V7.691a1.492 1.492 0 1 1 2.983 0zm-1.492 1.492l1.492 1.491" /><path d="M22.2 2.571H1.8A1.054 1.054 0 0 0 .75 3.625v13.588a1.054 1.054 0 0 0 1.05 1.054h2.443a7.8 7.8 0 0 1-1.386 3.16c3.05.044 4.98-1.136 6.138-3.16H22.2a1.054 1.054 0 0 0 1.054-1.054V3.625A1.054 1.054 0 0 0 22.2 2.571" /></g></svg>
          ),
        },
      ],
    },
    {
      id: "orders-transactions",
      title: "Quản lý đơn và giao dịch",
      items: [
        {
          id: "orders-admin-list",
          label: "Danh sách đơn hàng",
          to: "/admin/orders",
          allowedRoles: [ROLE_ADMIN, ROLE_CUSTOMER_CARE],
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
        {
          id: "payments-admin",
          label: "Danh sách giao dịch",
          to: "/admin/payments",
          allowedRoles: [ROLE_ADMIN, ROLE_CUSTOMER_CARE],
          isActive:
            currentPage === "admin/payments",
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
      ],
    },


    {
      id: "warehouse",
      title: "Kho & Nhà cung cấp",
      items: [
        {
          id: "suppliers-main",
          label: "Quản lý nhà cung cấp",
          to: "/suppliers",
          allowedRoles: [ROLE_ADMIN, ROLE_STORAGE_STAFF],
          isActive:
            currentPage === "suppliers" || currentPage.startsWith("suppliers/"),
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
          id: "keys-main",
          label: "Quản lý kho Key",
          to: "/keys",
          allowedRoles: [ROLE_ADMIN, ROLE_STORAGE_STAFF],
          isActive: currentPage === "keys" || currentPage.startsWith("keys/"),
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
          label: "Quản lý kho tài khoản",
          to: "/accounts",
          allowedRoles: [ROLE_ADMIN, ROLE_STORAGE_STAFF],
          isActive:
            currentPage === "accounts" || currentPage.startsWith("accounts/"),
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
      id: "support",
      title: "Quản lý dịch vụ hỗ trợ",
      items: [
        {
          id: "tickets-admin",
          label: "Quản lý phiếu hỗ trợ",
          to: "/admin/tickets",
          allowedRoles: [ROLE_ADMIN],
          isActive:
            currentPage === "admin/tickets" ||
            currentPage.startsWith("admin/tickets/"),
          icon: (
            <svg width="200" height="200" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16"><path fill="#000000" d="M7.384 2.497a1 1 0 0 1 1.365-.366L11 3.431a1 1 0 0 1 .366 1.366L10.095 7H11a.5.5 0 0 1 0 1H5a.5.5 0 0 1 0-1h.675a1 1 0 0 1-.181-1.229zM7.621 7H8.94l1.56-2.703l-2.25-1.3l-1.89 3.274zM4.515 5h.269l-.157.271c-.137.238-.22.492-.252.749a.5.5 0 0 0-.267.19L2.114 9h11.772l-1.994-2.79a.5.5 0 0 0-.118-.118l.458-.795q.021-.036.04-.074a1.5 1.5 0 0 1 .434.405l2.015 2.82c.181.255.279.56.279.872v3.18a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 1 12.5V9.32a1.5 1.5 0 0 1 .28-.871l2.014-2.82A1.5 1.5 0 0 1 4.514 5M14 10H2v2.5a.5.5 0 0 0 .5.5h11a.5.5 0 0 0 .5-.5z" /></svg>
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
            <svg width="200" height="200" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16"><path fill="#000000" d="M7.384 2.497a1 1 0 0 1 1.365-.366L11 3.431a1 1 0 0 1 .366 1.366L10.095 7H11a.5.5 0 0 1 0 1H5a.5.5 0 0 1 0-1h.675a1 1 0 0 1-.181-1.229zM7.621 7H8.94l1.56-2.703l-2.25-1.3l-1.89 3.274zM4.515 5h.269l-.157.271c-.137.238-.22.492-.252.749a.5.5 0 0 0-.267.19L2.114 9h11.772l-1.994-2.79a.5.5 0 0 0-.118-.118l.458-.795q.021-.036.04-.074a1.5 1.5 0 0 1 .434.405l2.015 2.82c.181.255.279.56.279.872v3.18a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 1 12.5V9.32a1.5 1.5 0 0 1 .28-.871l2.014-2.82A1.5 1.5 0 0 1 4.514 5M14 10H2v2.5a.5.5 0 0 0 .5.5h11a.5.5 0 0 0 .5-.5z" /></svg>
          ),
        },
        {
          id: "support-chats-admin",
          label: "Quản lý chat hỗ trợ",
          to: "/admin/support-chats",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/support-chats",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 24 24"><path fill="#000000" d="M1.5 2h21v9h-2V4h-17v14.296L6.124 16H13v2H6.876L1.5 22.704V2ZM20 12.5v1.14a3.496 3.496 0 0 1 1.405.814l.99-.571l1 1.732l-.99.571a3.51 3.51 0 0 1 0 1.623l.99.572l-1 1.732l-.993-.573a3.496 3.496 0 0 1-1.403.81v1.145h-2V20.35a3.496 3.496 0 0 1-1.403-.81l-.992.573l-1-1.732l.99-.572a3.506 3.506 0 0 1 0-1.623l-.99-.571l1-1.732l.989.57a3.497 3.497 0 0 1 1.406-.813V12.5h2Zm-1 2.995a1.5 1.5 0 1 0 0 3a1.5 1.5 0 0 0 0-3Z" /></svg>
          ),
        },
        {
          id: "support-chats-staff",
          label: "Quản lý chat hỗ trợ",
          to: "/staff/support-chats",
          allowedRoles: [ROLE_CUSTOMER_CARE],
          isActive: currentPage === "staff/support-chats",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 24 24"><path fill="#000000" d="M1.5 2h21v9h-2V4h-17v14.296L6.124 16H13v2H6.876L1.5 22.704V2ZM20 12.5v1.14a3.496 3.496 0 0 1 1.405.814l.99-.571l1 1.732l-.99.571a3.51 3.51 0 0 1 0 1.623l.99.572l-1 1.732l-.993-.573a3.496 3.496 0 0 1-1.403.81v1.145h-2V20.35a3.496 3.496 0 0 1-1.403-.81l-.992.573l-1-1.732l.99-.572a3.506 3.506 0 0 1 0-1.623l-.99-.571l1-1.732l.989.57a3.497 3.497 0 0 1 1.406-.813V12.5h2Zm-1 2.995a1.5 1.5 0 1 0 0 3a1.5 1.5 0 0 0 0-3Z" /></svg>
          ),
        },
        {
          id: "product-reports",
          label: "Báo cáo sản phẩm",
          to: "/reports",
          allowedRoles: [ROLE_ADMIN, ROLE_CUSTOMER_CARE],
          isActive: currentPage === "reports" || currentPage.startsWith("reports/"),
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 2048 2048"><path fill="#000000" d="m910 1664l-64 128H256V0h1536v1179l-128-256V128H384v1536h526zM640 896H512V768h128v128zm654 0H768V768h590l-64 128zm-782 256h128v128H512v-128zm256 0h398l-64 128H768v-128zM640 512H512V384h128v128zm896 0H768V384h768v128zm0 896v320h-128v-320h128zm-128 384h128v128h-128v-128zm640 256H896l576-1152l576 1152zm-971-112h790l-395-790l-395 790z" /></svg>),
        },
      ],
    },

    {
      id: "posts",
      title: "Quản lý bài viết",
      items: [
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
          isActive: currentPage === "post-create-edit" ||
            currentPage.startsWith("post-create-edit/"),
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 24 24"><path fill="#000000" d="M5.115 20q-.69 0-1.152-.462q-.463-.463-.463-1.153V5.615q0-.69.463-1.152Q4.425 4 5.115 4h9.308v1H5.115q-.23 0-.423.192q-.192.193-.192.423v12.77q0 .23.192.423q.193.192.423.192h12.77q.23 0 .423-.192q.192-.193.192-.423V9.077h1v9.308q0 .69-.462 1.152q-.463.463-1.153.463H5.115ZM8 16.5v-1h7v1H8Zm0-3v-1h7v1H8Zm0-3v-1h7v1H8ZM17.5 8V6h-2V5h2V3h1v2h2v1h-2v2h-1Z" /></svg>
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
        {
          id: "specific-documentation",
          label: "Quản lý tài liệu",
          to: "/admin/specific-documentation",
          allowedRoles: [ROLE_ADMIN, ROLE_CONTENT_CREATOR],
          isActive: currentPage === "admin/specific-documentation",
          icon: (
            <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path
                d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path
                d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path
                d="M8 7h8M8 11h8M8 15h4"
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
      id: "settings",
      title: "Cài đặt và cấu hình",
      items: [
        {
          id: "website-config",
          label: "Cấu hình trang web",
          to: "/admin/website-config",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/website-config",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 48 48"><g fill="none"><path stroke="#000000" stroke-linecap="round" stroke-linejoin="round" stroke-width="4" d="M24 40H7a3 3 0 0 1-3-3V11a3 3 0 0 1 3-3h34a3 3 0 0 1 3 3v12.059" /><path stroke="#000000" stroke-width="4" d="M4 11a3 3 0 0 1 3-3h34a3 3 0 0 1 3 3v9H4v-9Z" /><circle r="2" fill="#000000" transform="matrix(0 -1 -1 0 10 14)" /><circle r="2" fill="#000000" transform="matrix(0 -1 -1 0 16 14)" /><circle cx="37" cy="34" r="3" stroke="#000000" stroke-width="4" /><path stroke="#000000" stroke-linecap="round" stroke-linejoin="round" stroke-width="4" d="M37 41v-4m0-6v-4m-6.062 10.5l3.464-2m5.196-3l3.464-2m-12.124 0l3.464 2m5.196 3l3.464 2" /></g></svg>
          ),
        },
        {
          id: "support-priority-loyalty-rules",
          label: "Cấu hình quy tắc hỗ trợ",
          to: "/admin/support-priority-loyalty-rules",
          allowedRoles: [ROLE_ADMIN],
          isActive: currentPage === "admin/support-priority-loyalty-rules",
          icon: (
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 24 24"><path fill="#000000" d="M4.115 19.346v-1h3.097l-1.054-1.042q-1.166-1.13-1.681-2.48q-.515-1.35-.515-2.736q0-2.41 1.374-4.36Q6.71 5.777 8.962 4.942v1.062q-1.82.765-2.91 2.424q-1.09 1.659-1.09 3.66q0 1.222.463 2.37q.463 1.15 1.44 2.127l1.02 1.019v-3.027h1v4.77h-4.77Zm15.79-8.673H18.9q-.183-.875-.614-1.704q-.432-.829-1.151-1.554l-1.02-1.019v3.027h-1v-4.77h4.77v1h-3.097l1.054 1.043q.871.896 1.373 1.898q.502 1.002.69 2.08ZM17 20.808l-.108-.866q-.569-.125-.937-.349q-.368-.224-.701-.558l-.796.353l-.577-.853l.707-.577q-.184-.543-.184-1.035q0-.492.184-1.035l-.707-.576l.577-.854l.796.354q.333-.335.7-.56q.37-.223.938-.348l.108-.866h1l.108.866q.569.125.937.352q.368.227.701.567l.796-.365l.577.865l-.707.577q.184.53.184 1.029q0 .498-.184 1.029l.707.577l-.577.853l-.796-.353q-.333.334-.7.558q-.37.224-.938.35l-.108.865h-1Zm.5-1.731q.883 0 1.518-.636q.636-.635.636-1.518t-.636-1.518q-.635-.636-1.518-.636t-1.518.636q-.636.635-.636 1.518t.636 1.518q.635.636 1.518.636Z" /></svg>
          ),
        },
        {
          id: "support-plans-admin",
          label: "Cấu hình gói hỗ trợ",
          to: "/admin/support-plans",
          allowedRoles: [ROLE_ADMIN],
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
            <svg viewBox="0 0 24 24" fill="none" style={{ width: 20, height: 20 }}>
              <circle cx="12" cy="12" r="8" stroke="currentColor" strokeWidth="1.5" />
              <path
                d="M12 8v4l2.5 2.5"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path d="M9 3h6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
            </svg>
          ),
        },
        {
          id: "ticket-subject-templates",
          label: "Cấu hình mẫu chủ đề ticket",
          to: "/admin/ticket-subject-templates",
          allowedRoles: [ROLE_ADMIN],
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
        },
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
      <SidebarTooltip key={item.id} label={item.label} disabled={!isCollapsed}>
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
