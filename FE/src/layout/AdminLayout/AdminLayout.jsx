/**
 * File: AdminLayout.jsx
 * Author: HieuNDHE173169
 * Created: 18/10/2025
 * Last Updated: 29/10/2025
 * Version: 1.0.0
 * Purpose: Admin layout wrapper combining Sidebar and Header components.
 *          Provides consistent layout structure for all admin pages.
 */
import React from "react";
import Sidebar from "../AdminLayout/Sidebar.jsx";
import Header from "../AdminLayout/Header.jsx";
import "./AdminLayout.css";

/**
 * @summary: Admin layout component that wraps pages with Sidebar and Header.
 * @param {Object} props - Component props
 * @param {React.ReactNode} props.children - Child components to render inside the layout
 * @returns {JSX.Element} - Admin layout with Sidebar and Header
 */
const AdminLayout = ({ children }) => {
  return (
    <div className="admin-layout">
      <Sidebar />
      <div className="admin-content">
        <Header />
        <main className="admin-main">
          {children}
        </main>
      </div>
    </div>
  );
};

export default AdminLayout;

