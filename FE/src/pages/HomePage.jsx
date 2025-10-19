import React from "react";

export default function HomePage() {
  const email = localStorage.getItem("email");
  const role = localStorage.getItem("role");

  return (
    <div style={{ padding: 30 }}>
      <h1>Trang chủ Keytietkiem</h1>
      <p>Xin chào, {email || "người dùng"}!</p>
      <p>Vai trò hiện tại: {role || "Chưa xác định"}</p>
      <a href="/admin">👉 Đi đến trang Admin</a>
    </div>
  );
}
