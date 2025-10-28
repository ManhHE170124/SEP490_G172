import React from "react";
import { useNavigate, useParams } from "react-router-dom";
import AdminLayout from "../../components/admin/Layout";
import { BadgesApi } from "../../services/badges";

export default function BadgeDetail() {
  const { code } = useParams();
  const navigate = useNavigate();
  const [loading, setLoading] = React.useState(false);
  const [form, setForm] = React.useState(null);

  React.useEffect(() => {
    if (!code) return;
    setLoading(true);
    BadgesApi.get(code).then(d => setForm(d)).catch(() => alert('Không tìm thấy badge')).finally(() => setLoading(false));
  }, [code]);

  if (loading || !form) return (
    <AdminLayout>
      <div className="card">Đang tải…</div>
    </AdminLayout>
  );

  const save = async (e) => {
    e.preventDefault();
    setLoading(true);
    try {
      await BadgesApi.update(code, {
        displayName: form.displayName,
        colorHex: form.colorHex,
        icon: form.icon,
        isActive: form.isActive
      });
      alert('Đã lưu');
      navigate('/admin/categories');
    } catch (err) {
      alert(err?.response?.data?.message || err.message || 'Error');
    } finally { setLoading(false); }
  };

  const remove = async () => {
    if (!confirm('Xoá badge?')) return;
    setLoading(true);
    try {
      await BadgesApi.remove(code);
      navigate('/admin/categories');
    } catch (err) {
      alert(err?.response?.data?.message || err.message || 'Error');
    } finally { setLoading(false); }
  };

  return (
    <AdminLayout>
      <div className="card">
        <h2>Chi tiết badge: {form.badgeCode}</h2>
        <form onSubmit={save} style={{ marginTop: 12 }}>
          <div className="grid cols-2">
            <div className="group">
              <label>Mã badge</label>
              <input value={form.badgeCode} disabled />
            </div>
            <div className="group">
              <label>Tên hiển thị</label>
              <input value={form.displayName} onChange={e => setForm(s => ({ ...s, displayName: e.target.value }))} />
            </div>
            <div className="group">
              <label>Màu (hex)</label>
              <input value={form.colorHex} onChange={e => setForm(s => ({ ...s, colorHex: e.target.value }))} />
            </div>
            <div className="group">
              <label>Icon</label>
              <input value={form.icon} onChange={e => setForm(s => ({ ...s, icon: e.target.value }))} />
            </div>
          </div>

          <div style={{ marginTop: 8 }}>
            <label className="badge">
              <input type="checkbox" checked={form.isActive} onChange={e => setForm(s => ({ ...s, isActive: e.target.checked }))} /> Kích hoạt
            </label>
          </div>

          <div className="row" style={{ marginTop: 12 }}>
            <button className="btn" type="button" onClick={() => navigate(-1)}>Quay lại</button>
            <button className="btn primary" type="submit" disabled={loading}>Lưu</button>
          </div>
        </form>
      </div>
    </AdminLayout>
  );
}
