import React from "react";
import { useNavigate, useParams } from "react-router-dom";
import AdminLayout from "../../components/admin/Layout";
import { CategoryApi } from "../../services/categories";
import { useConfirm } from "../../components/common/ConfirmProvider.jsx";

export default function CategoryDetail() {
  const { id } = useParams();
  const catId = Number(id);
  const nav = useNavigate();
  const confirm = useConfirm();

  const [loading, setLoading] = React.useState(true);
  const [saving, setSaving] = React.useState(false);
  const [notFound, setNotFound] = React.useState(false);

  const [form, setForm] = React.useState({
    categoryName: "",
    categoryCode: "",
    description: "",
    isActive: true,
    displayOrder: 0,
  });

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }));

  const load = React.useCallback(async () => {
    try {
      setLoading(true);
      setNotFound(false);
      const dto = await CategoryApi.get(catId);
      if (!dto) { setNotFound(true); return; }
      setForm({
        categoryName: dto.categoryName || "",
        categoryCode: dto.categoryCode || "",
        description: dto.description || "",
        isActive: !!dto.isActive,
        displayOrder: dto.displayOrder ?? 0,
      });
    } catch {
      setNotFound(true);
    } finally {
      setLoading(false);
    }
  }, [catId]);

  React.useEffect(() => { load(); }, [load]);

  const save = async () => {
    try {
      setSaving(true);
      await CategoryApi.update(catId, {
        categoryName: form.categoryName.trim(),
        description: form.description,
        isActive: form.isActive,
        displayOrder: Number(form.displayOrder) || 0,
      });
      alert("Đã lưu thay đổi.");
      await load();
    } catch (e) {
      alert(e?.response?.data?.message || e.message);
    } finally {
      setSaving(false);
    }
  };

  const toggle = async () => {
    // immediate toggle without confirmation (match product behavior)
    try {
      await CategoryApi.toggle(catId);
    } catch (e) {
      console.error(e);
    }
    await load();
  };

  if (loading) {
    return (
      <AdminLayout>
        <div className="card"><div>Đang tải chi tiết danh mục…</div></div>
      </AdminLayout>
    );
  }

  if (notFound) {
    return (
      <AdminLayout>
        <div className="card">
          <h2>Không tìm thấy danh mục</h2>
        </div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="card">
        {/* Header chỉ còn tiêu đề, đã bỏ các nút góc phải */}
        <div style={{marginBottom:10}}>
          <h2>Chi tiết danh mục</h2>
        </div>

        <div className="grid cols-3">
          <div className="group">
            <label>Tên danh mục</label>
            <input
              value={form.categoryName}
              onChange={(e) => set("categoryName", e.target.value)}
              placeholder="VD: Office"
            />
          </div>
          <div className="group">
            <label>Slug</label>
            <input
              value={form.categoryCode}
              readOnly
              className="mono"
              title="Slug do BE chuẩn hoá; không chỉnh tại đây"
            />
          </div>
          <div className="group">
            <label>Hiển thị</label>
            <select
              value={form.isActive ? "true" : "false"}
              onChange={(e) => set("isActive", e.target.value === "true")}
            >
              <option value="true">Hiện</option>
              <option value="false">Ẩn</option>
            </select>
          </div>

          <div className="group" style={{ gridColumn: "1/4" }}>
            <label>Mô tả</label>
            <textarea
              value={form.description}
              onChange={(e) => set("description", e.target.value)}
              placeholder="Mô tả ngắn sẽ hiển thị trên website…"
              rows={4}
            />
          </div>

          <div className="group">
            <label>Thứ tự hiển thị</label>
            <input
              type="number"
              value={form.displayOrder}
              onChange={(e) => set("displayOrder", e.target.value)}
              min={0}
            />
          </div>
        </div>

        {/* Nút hành động giữ ở cuối form */}
        <div className="row" style={{ marginTop: 12 }}>
          <button className="btn" onClick={() => nav(-1)}>⬅ Quay lại</button>
          <label className="switch">
            <input type="checkbox" checked={form.isActive} onChange={toggle} />
            <span className="slider" />
          </label>
          <button className="btn primary" disabled={saving} onClick={save}>
            {saving ? "Đang lưu…" : "Lưu thay đổi"}
          </button>
        </div>
      </div>
    </AdminLayout>
  );
}
