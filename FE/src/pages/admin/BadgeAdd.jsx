import React from "react";
import { useNavigate } from "react-router-dom";
import { BadgesApi } from "../../services/badges";
import "./admin.css";
export default function BadgeAdd() {
  const navigate = useNavigate();
  const [form, setForm] = React.useState({ badgeCode: "", displayName: "", colorHex: "#2196f3", icon: "", isActive: true });
  const [loading, setLoading] = React.useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setLoading(true);
    try {
      await BadgesApi.create(form);
      navigate("/admin/categories");
    } catch (err) {
      alert(err?.response?.data?.message || err.message || "Error");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page">
      <div className="card">
  <h2>Thêm nhãn</h2>
        <form onSubmit={submit} style={{ marginTop: 12 }}>
          <div className="grid cols-2">
            <div className="group">
              <label>Mã badge</label>
              <input value={form.badgeCode} onChange={(e) => setForm(s => ({ ...s, badgeCode: e.target.value }))} placeholder="VD: HOT" required />
            </div>
            <div className="group">
              <label>Tên hiển thị</label>
              <input value={form.displayName} onChange={(e) => setForm(s => ({ ...s, displayName: e.target.value }))} required />
            </div>
            <div className="group">
              <label>Màu (hex)</label>
              <input value={form.colorHex} onChange={(e) => setForm(s => ({ ...s, colorHex: e.target.value }))} />
            </div>
            <div className="group">
              <label>Icon (tùy chọn)</label>
              <input value={form.icon} onChange={(e) => setForm(s => ({ ...s, icon: e.target.value }))} />
            </div>
          </div>
          <div style={{ marginTop: 8 }}>
            <label className="badge">
              <input type="checkbox" checked={form.isActive} onChange={(e) => setForm(s => ({ ...s, isActive: e.target.checked }))} /> Kích hoạt
            </label>
          </div>

          <div className="row" style={{ marginTop: 12 }}>
            <button className="btn" type="button" onClick={() => navigate(-1)}>⬅ Quay lại</button>
            <button className="btn primary" type="submit" disabled={loading}>Lưu</button>
          </div>
        </form>
      </div>
    </div>
  );
}
