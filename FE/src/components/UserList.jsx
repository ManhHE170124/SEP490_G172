import React, { useEffect, useState } from "react";
import UserDetailModal from "./UserDetailModal";

export default function UserList() {
  const [users, setUsers] = useState([]);
  const [selected, setSelected] = useState(null);
  const [showModal, setShowModal] = useState(false);

  // ⚠️ Backend URL phải dùng HTTP (cùng giao thức với React)
  const apiBase = "http://localhost:5042/api";

  useEffect(() => {
    console.log("👉 Gọi API:", `${apiBase}/users`);
    fetch(`${apiBase}/users`)
      .then((res) => {
        if (!res.ok) throw new Error("Lỗi khi lấy danh sách");
        return res.json();
      })
      .then((data) => {
        console.log("✅ Nhận data:", data);
        setUsers(data);
      })
      .catch((err) => {
        console.error("❌ Lỗi fetch:", err);
        alert("Không thể lấy dữ liệu từ API. Kiểm tra backend/CORS/URL.");
      });
  }, []);

  const openDetail = (userId) => {
    fetch(`${apiBase}/users/${userId}`)
      .then((r) => {
        if (!r.ok) throw new Error("Fail");
        return r.json();
      })
      .then((data) => {
        setSelected(data);
        setShowModal(true);
      })
      .catch((err) => {
        console.error("❌ Lỗi load chi tiết:", err);
        alert("Không thể tải chi tiết");
      });
  };

  return (
    <div style={{ padding: 20 }}>
      <h1>Danh sách người dùng</h1>
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead>
          <tr>
            <th>Avatar</th>
            <th>Họ và tên</th>
            <th>Username</th>
            <th>Email</th>
            <th>Trạng thái</th>
            <th>Hành động</th>
          </tr>
        </thead>
        <tbody>
          {users.length === 0 && (
            <tr>
              <td colSpan={6}>Không có user</td>
            </tr>
          )}
          {users.map((u) => (
            <tr key={u.userId}>
              <td>
                {u.avatarUrl ? (
                  <img
                    src={u.avatarUrl}
                    alt={u.fullName}
                    style={{ width: 48, height: 48, borderRadius: 24 }}
                  />
                ) : (
                  "—"
                )}
              </td>
              <td>{u.fullName}</td>
              <td>{u.username}</td>
              <td>{u.email}</td>
              <td>{u.status}</td>
              <td>
                <button onClick={() => openDetail(u.userId)}>Xem chi tiết</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {showModal && selected && (
        <UserDetailModal user={selected} onClose={() => setShowModal(false)} />
      )}
    </div>
  );
}
