import React from "react";

export default function UserDetailModal({ user, onClose }) {
  if (!user) return null;

  const rows = [
    ["UserId", user.userId],
    ["AccountId", user.accountId],
    ["FullName", user.fullName],
    ["PhoneNumber", user.phoneNumber],
    ["AvatarUrl", user.avatarUrl],
    ["Notes", user.notes],
    ["CreatedAt", user.createdAt],
    ["UpdatedAt", user.updatedAt],
    ["Username", user.username],
    ["Email", user.email],
    ["Status", user.status],
    ["EmailVerified", String(user.emailVerified)],
    ["TwoFaEnabled", String(user.twoFaEnabled)],
    ["LastLoginAt", user.lastLoginAt]
  ];

  return (
    <div style={{
      position: "fixed", inset: 0, background: "rgba(0,0,0,0.5)",
      display: "flex", alignItems: "center", justifyContent: "center", zIndex: 9999
    }}>
      <div style={{ width: 760, maxHeight: "85vh", overflowY: "auto", background: "#fff", padding: 20, borderRadius: 6 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
          <h2>Chi tiết user</h2>
          <button onClick={onClose}>Đóng</button>
        </div>

        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <tbody>
            {rows.map(([k, v]) => (
              <tr key={k}>
                <td style={{ border: "1px solid #ddd", padding: 8, width: "30%", fontWeight: 600 }}>{k}</td>
                <td style={{ border: "1px solid #ddd", padding: 8 }}>{v ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
