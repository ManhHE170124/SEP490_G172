import React from "react";

export default function AdminPage() {
  const role = localStorage.getItem("role");

  if (role !== "Admin") {
    return (
      <div style={{ padding: 30, color: "red" }}>
        <h2>Bạn không có quyền truy cập trang này ❌</h2>
        <a href="/">Quay lại trang chủ</a>
      </div>
    );
  }

  return (
    <div style={{ padding: 30 }}>
      <h1>Trang quản trị (Admin)</h1>
      <p>Chào mừng bạn đến khu vực quản lý!</p>
    </div>
  );
}
