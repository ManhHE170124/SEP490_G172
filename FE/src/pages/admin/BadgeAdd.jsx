import React from "react";
import { useNavigate } from "react-router-dom";
import { BadgesApi } from "../../services/badges";
import ColorPickerTabs, { bestTextColor } from "../../components/color/ColorPickerTabs";
import "./admin.css";

/* ===================== BadgeAdd ===================== */
export default function BadgeAdd() {
  const nav = useNavigate();

  const [form, setForm] = React.useState({
    badgeCode: "",
    displayName: "",
    colorHex: "#1e40af",   // lưu trong state để submit
    icon: "",
    isActive: true,
  });
  const [saving, setSaving] = React.useState(false);
  const [showPreview, setShowPreview] = React.useState(false);

  const set = (k, v) => setForm((s) => ({ ...s, [k]: v }));

  const statusClass = form.isActive ? "badge green" : "badge gray";
  const statusText  = form.isActive ? "Đang hiển thị" : "Đang ẩn";
  const toggleActive = () => set("isActive", !form.isActive);

  const submit = async (e) => {
    e.preventDefault();
    try {
      setSaving(true);
      await BadgesApi.create({
        badgeCode: form.badgeCode.trim(),
        displayName: form.displayName.trim(),
        colorHex: form.colorHex,
        icon: form.icon?.trim(),
        isActive: !!form.isActive,
      });
      alert("Đã tạo nhãn thành công");
      nav("/admin/categories");
    } catch (err) {
      alert(err?.response?.data?.message || err.message || "Lỗi tạo nhãn");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="page">
      <div className="card">
        {/* Header: tiêu đề + công tắc trạng thái + quay lại */}
        <div style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: 10
        }}>
          <h2>Thêm nhãn</h2>

          <div className="row" style={{ gap: 10, alignItems: "center" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <label className="switch" title="Bật/Tắt hiển thị">
                <input
                  type="checkbox"
                  checked={form.isActive}
                  onChange={toggleActive}
                  aria-label="Bật/Tắt hiển thị nhãn"
                />
                <span className="slider" />
              </label>
              <span className={statusClass} style={{ textTransform: "none" }}>
                {statusText}
              </span>
            </div>
            <button className="btn ghost" onClick={() => nav("/admin/categories")}>
              ⬅ Quay lại
            </button>
          </div>
        </div>

        <form onSubmit={submit} className="input-group">
          <div className="grid cols-2">
            <div className="group">
              <span>Mã sản phẩm</span>
              <input
                value={form.badgeCode}
                onChange={(e) => set("badgeCode", e.target.value)}
                placeholder="VD: HOT"
                required
              />
            </div>

            <div className="group">
              <span>Tên hiển thị</span>
              <input
                value={form.displayName}
                onChange={(e) => set("displayName", e.target.value)}
                placeholder="VD: Nổi bật"
                required
              />
            </div>

            <div className="group">
              <span>Biểu tượng (tùy chọn)</span>
              <input
                value={form.icon}
                onChange={(e) => set("icon", e.target.value)}
                placeholder="VD: fire, star, sale…"
              />
            </div>

            {/* Bộ chọn màu kiểu Sampler | Spectrum | Image */}
            <div className="group">
              <span>Màu nhãn</span>
              <ColorPickerTabs value={form.colorHex} onChange={(hex) => set("colorHex", hex)} />
            </div>
          </div>

          {/* Xem trước nhãn */}
          <div className="row" style={{ marginTop: 8, gap: 8, alignItems: "center" }}>
            {showPreview && (
              <span
                className="label-chip"
                style={{
                  backgroundColor: form.colorHex,
                  color: bestTextColor(form.colorHex),
                  padding: "4px 10px",
                  borderRadius: 8,
                  fontSize: 12,
                  display: "inline-block"
                }}
                title={form.displayName || form.badgeCode}
              >
                {form.displayName || form.badgeCode || "Nhãn"}
              </span>
            )}
          </div>

          {/* ACTIONS */}
          <div className="row" style={{ marginTop: 12 }}>
            <button
              type="button"
              className="btn"
              onClick={() => setShowPreview((v) => !v)}
            >
              {showPreview ? "Ẩn xem trước" : "Xem trước nhãn"}
            </button>
            <button type="submit" className="btn primary" disabled={saving}>
              {saving ? "Đang lưu…" : "Lưu"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
