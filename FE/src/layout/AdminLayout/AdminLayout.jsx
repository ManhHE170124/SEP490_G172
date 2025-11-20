/**
 * File: AdminLayout.jsx
 * Author: HieuNDHE173169
 * Created: 18/10/2025
 * Last Updated: 29/10/2025
 * Version: 1.0.0
 * Purpose: Admin layout wrapper combining Sidebar and Header components.
 *          Provides consistent layout structure for all admin pages.
 */
import React, { useCallback, useEffect, useState } from "react";
import Sidebar from "../AdminLayout/Sidebar.jsx";
import Header from "../AdminLayout/Header.jsx";
import profileService from "../../services/profile";
import "./AdminLayout.css";

const unwrap = (payload) =>
  payload?.data !== undefined ? payload.data : payload;

/**
 * @summary: Admin layout component that wraps pages with Sidebar and Header.
 * @param {Object} props - Component props
 * @param {React.ReactNode} props.children - Child components to render inside the layout
 * @returns {JSX.Element} - Admin layout with Sidebar and Header
 */
const AdminLayout = ({ children }) => {
  const [profile, setProfile] = useState(null);

  const loadProfile = useCallback(async () => {
    try {
      const response = await profileService.getAdminProfile();
      const data = unwrap(response);
      setProfile(data);
    } catch (error) {
      console.error("Unable to load admin profile:", error);
    }
  }, []);

  useEffect(() => {
    loadProfile();
  }, [loadProfile]);

  return (
    <div className="al-admin-layout">
      <Sidebar />
      <div className="al-admin-content">
        <Header profile={profile} />
        <main className="al-admin-main">{children}</main>
      </div>
    </div>
  );
};

export default AdminLayout;
