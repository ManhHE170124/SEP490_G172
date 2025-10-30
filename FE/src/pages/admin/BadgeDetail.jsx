import React from "react";
import { useNavigate, useParams } from "react-router-dom";
import { BadgesApi } from "../../services/badges";
import ColorPickerTabs, { bestTextColor } from "../../components/color/ColorPickerTabs";
import "./admin.css";

export default function BadgeDetail() {
  const { code } = useParams();
  const nav = useNavigate();

  const [loading, setLoading] = React.useState(true);
  const [saving, setSaving] = React.useState(false);
  const [notFound, setNotFound] = React.useState(false);

  const [form, setForm] = React.useState({
    badgeCode: "",
    displayName: "",
    colorHex: "#1e40af",
    icon: "",
    isActive: true,
  });
  const [productCount, setProductCount] = React.useState(0);
  const [showPreview, setShowPreview] = React.useState(false);

  const statusBadgeClass = form.isActive ? "badge green" : "badge gray";
  const statusText = form.isActive ? "Hiển thị" : "Ẩn";

  const load = React.useCallback(async () => {
    if (!code) return;
    try {
      setLoading(true);
      setNotFound(false);
      const d = await BadgesApi.get(code);
      if (!d) { setNotFound(true); return; }
      setForm({
        badgeCode: d.badgeCode || code,
        displayName: d.displayName || "",
        colorHex: d.colorHex || d.color || "#1e40af",
        icon: d.icon || "",
        isActive: !!d.isActive,
      });
      setProductCount(
        d.productCount ?? d.productsCount ?? d.usageCount ?? d.usedByProducts ?? 0
      );
    } catch {
      setNotFound(true);
    } finally {
      setLoading(false);
    }
  }, [code]);

  React.useEffect(() => { load(); }, [load]);

  const set = (k, v) => setForm((s) => ({ ...s, [k]: v }));

  const toggle = async () => {
    try {
      // Nếu BE có endpoint toggle, dùng luôn; nếu không thì fallback sang update.
      if (BadgesApi.toggle) {
        await BadgesApi.toggle(code);
        setForm((s) => ({ ...s, isActive: !s.isActive }));
      } else {
        const next = !form.isActive;
        await BadgesApi.update(code, { ...form, isActive: next });
        setForm((s) => ({ ...s, isActive: next }));
      }
    } catch (e) {
      console.error(e);
    } finally {
      await load();
    }
  };

  const save = async (e) => {
    e.preventDefault();
    try {
      setSaving(true);
      await BadgesApi.update(code, {
        displayName: form.displayName.trim(),
        colorHex: form.colorHex,
        icon: form.icon?.trim(),
        isActive: !!form.isActive,
      });
      alert("Đã lưu thay đổi.");
      await load();
    } catch (err) {
      alert(err?.response?.data?.message || err.message || "Error");
    } finally {
      setSaving(false);
    }
  };

  const remove = async () => {
    if (!window.confirm("Bạn có chắc muốn xoá nhãn này?")) return;
    try {
      setSaving(true);
      await BadgesApi.remove(code);
      alert("Đã xoá nhãn.");
      nav("/admin/categories");
    } catch (err) {
      alert(err?.response?.data?.message || err.message || "Error");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="page">
        <div className="card">Đang tải chi tiết nhãn…</div>
      </div>
    );
  }
  if (notFound) {
    return (
      <div className="page">
        <div className="card">
          <h2>Không tìm thấy nhãn</h2>
          <div className="row" style={{ marginTop: 12 }}>
            <button className="btn" onClick={() => nav(-1)}>⬅ Quay lại</button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="page">
      <div className="card">
        {/* HEADER: tiêu đề trái; phải: Số sản phẩm + công tắc + trạng thái + quay lại */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 10,
          }}
        >
          <h2 style={{ margin: 0 }}>Chi tiết nhãn</h2>

          <div className="row" style={{ gap: 10, alignItems: "center" }}>
            <span className="badge gray">Số sản phẩm: {productCount}</span>

            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <label className="switch" title="Bật/Tắt hiển thị">
                <input
                  type="checkbox"
                  checked={!!form.isActive}
                  onChange={toggle}
                  aria-label="Bật/Tắt hiển thị nhãn"
                />
                <span className="slider" />
              </label>
              <span className={statusBadgeClass} style={{ textTransform: "none" }}>
                {statusText}
              </span>
            </div>

            <button className="btn ghost" onClick={() => nav(-1)}>⬅ Quay lại</button>
          </div>
        </div>

        {/* FORM: layout 2 cột, đồng bộ với Add */}
        <form onSubmit={save} className="input-group">
          <div className="grid cols-2">
            <div className="group" style={{ gridColumn: "1 / 2" }}>
              <span>Tên hiển thị</span>
              <input
                value={form.displayName}
                onChange={(e) => set("displayName", e.target.value)}
                placeholder="VD: Nổi bật"
              />
            </div>

            <div className="group" style={{ gridColumn: "2 / 3" }}>
              <span>Mã sản phẩm</span>
              <input value={form.badgeCode} readOnly className="mono" />
            </div>

            <div className="group" style={{ gridColumn: "1 / 2" }}>
              <span>Biểu tượng (tùy chọn)</span>
              <input
                value={form.icon}
                onChange={(e) => set("icon", e.target.value)}
                placeholder="VD: fire, star, sale…"
              />
            </div>

            <div className="group" style={{ gridColumn: "2 / 3" }}>
              <span>Màu nhãn</span>
              <ColorPickerTabs
                value={form.colorHex || "#1e40af"}
                onChange={(hex) => set("colorHex", hex)}
              />
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
                  display: "inline-block",
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
            <button className="btn primary" type="submit" disabled={saving}>
              {saving ? "Đang lưu…" : "Lưu thay đổi"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
