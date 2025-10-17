import React from "react";

export default function Navbar() {
  const email = localStorage.getItem("email");
  const role = localStorage.getItem("role");

  const handleLogout = () => {
    localStorage.clear();
    window.location.href = "/login";
  };

  return (
    <nav
      style={{
        background: "#1976d2",
        padding: 12,
        color: "white",
        display: "flex",
        justifyContent: "space-between",
      }}
    >
      <div>
        <a href="/" style={{ color: "white", marginRight: 20 }}>Trang chủ</a>
        {role === "Admin" && (
          <a href="/admin" style={{ color: "white" }}>Quản trị</a>
        )}
      </div>

      <div>
        {email ? (
          <>
            <span style={{ marginRight: 10 }}>{email}</span>
            <button
              onClick={handleLogout}
              style={{ background: "white", color: "#1976d2", border: "none", padding: "4px 8px", cursor: "pointer" }}
            >
              Đăng xuất
            </button>
          </>
        ) : (
          <a href="/login" style={{ color: "white" }}>Đăng nhập</a>
        )}
      </div>
    </nav>
  );
}
