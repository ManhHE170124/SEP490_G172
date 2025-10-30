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

  const statusClass = (active) => (active ? "badge green" : "badge gray");
  const statusText = form.isActive ? "Đang hiển thị" : "Đang ẩn";
  const toggleStatus = () => set("isActive", !form.isActive);

  const save = async (publish = true) => {
    try {
      setSaving(true);
      const payload = { ...form, isActive: publish ? true : false };
      await CategoryApi.create(payload);
      alert(publish ? "Đã lưu & kích hoạt danh mục" : "Đã lưu nháp danh mục");
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
        {/* Header: tiêu đề + công tắc trạng thái + quay lại (giống ProductAdd) */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 10,
          }}
        >
          <h2>Thêm danh mục</h2>

          <div className="row" style={{ gap: 10, alignItems: "center" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <label className="switch" title="Bật/Tắt hiển thị">
                <input
                  type="checkbox"
                  checked={form.isActive}
                  onChange={toggleStatus}
                  aria-label="Bật/Tắt hiển thị danh mục"
                />
                <span className="slider" />
              </label>
              <span className={statusClass(form.isActive)} style={{ textTransform: "none" }}>
                {statusText}
              </span>
            </div>

            <button className="btn ghost" onClick={() => nav("/admin/categories")}>
              ⬅ Quay lại
            </button>
          </div>
        </div>

        {/* GRID 2 cột (đồng bộ format với ProductAdd) */}
        <div className="grid cols-2 input-group">
          {/* HÀNG 1: Tên + Mã danh mục */}
          <div className="group" style={{ gridColumn: "1 / 2" }}>
            <span>Tên danh mục</span>
            <input
              value={form.categoryName}
              onChange={(e) => set("categoryName", e.target.value)}
              placeholder="VD: Office"
            />
          </div>

          <div className="group" style={{ gridColumn: "2 / 3" }}>
            <span>Mã danh mục</span>
            <input
              value={form.categoryCode}
              onChange={(e) => set("categoryCode", e.target.value)}
              placeholder="VD: office"
            />
          </div>

          {/* HÀNG 2: Thứ tự hiển thị (số) */}
          <div className="group" style={{ gridColumn: "1 / 2" }}>
            <span>Thứ tự hiển thị</span>
            <input
              type="number"
              min={0}
              step={1}
              value={form.displayOrder}
              onChange={(e) => set("displayOrder", Number(e.target.value) || 0)}
              placeholder="VD: 0"
            />
          </div>

          {/* chừa trống cột phải cho cân đối */}
          <div className="group" style={{ gridColumn: "2 / 3" }} />

          {/* HÀNG 3: Mô tả (full width) */}
          <div className="group" style={{ gridColumn: "1 / 3" }}>
            <span>Mô tả</span>
            <textarea
              value={form.description}
              onChange={(e) => set("description", e.target.value)}
              placeholder="Mô tả ngắn sẽ hiển thị trên website…"
            />
          </div>
        </div>

        {/* ACTIONS (giống ProductAdd) */}
        <div className="row" style={{ marginTop: 12 }}>
          <button className="btn" disabled={saving} onClick={() => save(false)}>
            Lưu nháp
          </button>
          <button className="btn primary" disabled={saving} onClick={() => save(true)}>
            Lưu &amp; Kích hoạt
          </button>
        </div>
      </div>
    </div>
  );
}
