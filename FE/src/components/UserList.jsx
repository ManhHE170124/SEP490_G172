import React, { useEffect, useState } from "react";
import UserDetailModal from "./UserDetailModal";

export default function UserList() {
  const [users, setUsers] = useState([]);
  const [selected, setSelected] = useState(null);
  const [showModal, setShowModal] = useState(false);

  const apiBase = "http://localhost:5042/api";

  useEffect(() => {
    fetch(`${apiBase}/users`)
      .then(res => {
        if (!res.ok) throw new Error("Lỗi khi lấy danh sách");
        return res.json();
      })
      .then(data => setUsers(data))
      .catch(err => {
        console.error(err);
        alert("Không thể lấy dữ liệu từ API. Kiểm tra backend/CORS/URL.");
      });
  }, []);

  const openDetail = (userId) => {
    fetch(`${apiBase}/users/${userId}`)
      .then(r => {
        if (!r.ok) throw new Error("Fail");
        return r.json();
      })
      .then(data => {
        setSelected(data);
        setShowModal(true);
      })
      .catch(err => {
        console.error(err);
        alert("Không thể tải chi tiết");
      });
  };

  return (
    <div style={{ padding: 20 }}>
      <h1>Danh sách người dùng</h1>

      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead>
          <tr>
            <th style={{ border: "1px solid #ddd", padding: 8 }}>Avatar</th>
            <th style={{ border: "1px solid #ddd", padding: 8 }}>Họ và tên</th>
            <th style={{ border: "1px solid #ddd", padding: 8 }}>Username</th>
            <th style={{ border: "1px solid #ddd", padding: 8 }}>Email</th>
            <th style={{ border: "1px solid #ddd", padding: 8 }}>Trạng thái</th>
            <th style={{ border: "1px solid #ddd", padding: 8 }}>Hành động</th>
          </tr>
        </thead>
        <tbody>
          {users.length === 0 && (
            <tr>
              <td colSpan={6} style={{ padding: 20 }}>
                Không có user
              </td>
            </tr>
          )}
          {users.map(u => (
            <tr key={u.userId}>
              <td style={{ border: "1px solid #ddd", padding: 8 }}>
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
              <td style={{ border: "1px solid #ddd", padding: 8 }}>{u.fullName}</td>
              <td style={{ border: "1px solid #ddd", padding: 8 }}>{u.username}</td>
              <td style={{ border: "1px solid #ddd", padding: 8 }}>{u.email}</td>
              <td style={{ border: "1px solid #ddd", padding: 8 }}>{u.status}</td>
              <td style={{ border: "1px solid #ddd", padding: 8 }}>
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
