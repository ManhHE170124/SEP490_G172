/**
 * File: AdminHomePage.jsx
 * Author: Keytietkiem Team
 * Created: 2025
 * Version: 1.0.0
 * Purpose: Temporary admin dashboard page showing overview information
 */
import React from "react";
import "./AdminHomePage.css";

const AdminHomePage = () => {
  return (
    <div className="admin-home-page">
      <div className="admin-home-header">
        <h1>Admin Dashboard</h1>
        <p className="admin-home-subtitle">Tổng quan hệ thống</p>
      </div>

      <div className="admin-home-content">
        <div className="admin-home-grid">
          {/* Placeholder cards */}
          <div className="admin-home-card">
            <div className="admin-home-card-header">
              <h3>Tổng quan</h3>
            </div>
            <div className="admin-home-card-body">
              <p>Dashboard này đang được phát triển. Nội dung sẽ được cập nhật sau.</p>
            </div>
          </div>

          <div className="admin-home-card">
            <div className="admin-home-card-header">
              <h3>Thống kê</h3>
            </div>
            <div className="admin-home-card-body">
              <p>Thông tin thống kê sẽ được hiển thị tại đây.</p>
            </div>
          </div>

          <div className="admin-home-card">
            <div className="admin-home-card-header">
              <h3>Hoạt động gần đây</h3>
            </div>
            <div className="admin-home-card-body">
              <p>Danh sách hoạt động gần đây sẽ được hiển thị tại đây.</p>
            </div>
          </div>

          <div className="admin-home-card">
            <div className="admin-home-card-header">
              <h3>Thông báo</h3>
            </div>
            <div className="admin-home-card-body">
              <p>Các thông báo quan trọng sẽ được hiển thị tại đây.</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default AdminHomePage;

