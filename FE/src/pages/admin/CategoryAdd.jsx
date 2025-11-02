import React from "react";
import { useNavigate } from "react-router-dom";
import { CategoryApi } from "../../services/categories";
import "./admin.css";
export default function CategoryAdd() {
  const nav = useNavigate();
  const [saving, setSaving] = React.useState(false);
  const [form, setForm] = React.useState({
    categoryName: "",
    categoryCode: "",
    description: "",
    isActive: true,
    displayOrder: 0,
  });

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }));

  const save = async (activate = true) => {
    try {
      setSaving(true);
      const payload = { ...form, isActive: activate };
      await CategoryApi.create(payload);
      alert(activate ? "Đã lưu & kích hoạt danh mục" : "Đã lưu nháp danh mục");
      nav("/admin/categories");
    } catch (e) {
      alert(e?.response?.data?.message || e.message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="page">
      <div className="card">
        {/* Header chỉ còn tiêu đề, đã bỏ các nút góc phải */}
        <div style={{marginBottom:10}}>
          <h2>Thêm danh mục</h2>
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
              onChange={(e) => set("categoryCode", e.target.value)}
              placeholder="office"
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
            />
          </div>
        </div>

        {/* Nút hành động giữ ở cuối form */}
        <div className="row" style={{ marginTop: 12 }}>
          <button className="btn" disabled={saving} onClick={() => nav(-1)}>⬅ Quay lại</button>
          <button className="btn" disabled={saving} onClick={() => save(false)}>Lưu nháp</button>
          <button className="btn primary" disabled={saving} onClick={() => save(true)}>Lưu & Kích hoạt</button>
        </div>
      </div>
    </div>
  );
}
