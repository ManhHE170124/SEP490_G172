import React from "react";
import { useNavigate, useParams } from "react-router-dom";
import { CategoryApi } from "../../services/categories";
import "./admin.css";

export default function CategoryDetail() {
  const { id } = useParams();
  const catId = Number(id);
  const nav = useNavigate();

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
  const [productCount, setProductCount] = React.useState(0);

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }));

  const load = React.useCallback(async () => {
    try {
      setLoading(true);
      setNotFound(false);
      const dto = await CategoryApi.get(catId);
      if (!dto) {
        setNotFound(true);
        return;
      }
      setForm({
        categoryName: dto.categoryName || "",
        categoryCode: dto.categoryCode || "",
        description: dto.description || "",
        isActive: !!dto.isActive,
        displayOrder: dto.displayOrder ?? 0,
      });
      setProductCount(dto.productCount ?? dto.productsCount ?? 0);
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
    try {
      await CategoryApi.toggle(catId);
      setForm((f) => ({ ...f, isActive: !f.isActive }));
      await load();
    } catch (e) {
      console.error(e);
    }
  };

  const statusBadgeClass = form.isActive ? "badge green" : "badge gray";
  const statusText = form.isActive ? "Hiển thị" : "Ẩn";

  if (loading) {
    return (
      <div className="page">
        <div className="card"><div>Đang tải chi tiết danh mục…</div></div>
      </div>
    );
  }

  if (notFound) {
    return (
      <div className="page">
        <div className="card">
          <h2>Không tìm thấy danh mục</h2>
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
        {/* HEADER: tiêu đề bên trái; bên phải: Số sản phẩm (badge xám) + công tắc + trạng thái + Quay lại */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 10,
          }}
        >
          <h2 style={{ margin: 0 }}>Chi tiết danh mục</h2>

          <div className="row" style={{ gap: 10, alignItems: "center" }}>
            {/* Badge xám giống "Tồn kho" của ProductDetail */}
            <span className="badge gray">Số sản phẩm: {productCount}</span>

            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <label className="switch" title="Bật/Tắt hiển thị">
                <input
                  type="checkbox"
                  checked={!!form.isActive}
                  onChange={toggle}
                  aria-label="Bật/Tắt hiển thị danh mục"
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

        {/* FORM: layout 2 cột, nhãn thuần Việt */}
        <div className="grid cols-2 input-group">
          {/* Hàng 1: Tên + Mã danh mục (readonly) */}
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
              readOnly
              className="mono"
              title="Mã danh mục (slug) do BE chuẩn hoá; không chỉnh tại đây"
              placeholder="office"
            />
          </div>

          {/* Hàng 2: Thứ tự + (cột trống cân layout) */}
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
          <div className="group" style={{ gridColumn: "2 / 3" }} />

          {/* Hàng 3: Mô tả (full width) */}
          <div className="group" style={{ gridColumn: "1 / 3" }}>
            <span>Mô tả</span>
            <textarea
              rows={4}
              value={form.description}
              onChange={(e) => set("description", e.target.value)}
              placeholder="Mô tả ngắn sẽ hiển thị trên website…"
            />
          </div>
        </div>

        {/* ACTIONS */}
        <div className="row" style={{ marginTop: 12 }}>
          <button className="btn primary" disabled={saving} onClick={save}>
            {saving ? "Đang lưu…" : "Lưu thay đổi"}
          </button>
        </div>
      </div>
    </div>
  );
}
