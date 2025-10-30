import React from "react";
import { useNavigate } from "react-router-dom";
import { ProductApi } from "../../services/products";
import { CategoryApi } from "../../services/categories";
import { BadgesApi } from "../../services/badges";
import "./admin.css";

export default function ProductAdd() {
  const nav = useNavigate();

  // ======= data sources =======
  const [cats, setCats] = React.useState([]);
  const [badges, setBadges] = React.useState([]);

  // UI panels
  const [showCats, setShowCats] = React.useState(true);
  const [showBadgesPanel, setShowBadgesPanel] = React.useState(true);

  // states
  const [saving, setSaving] = React.useState(false);

  // images
  const [selectedFiles, setSelectedFiles] = React.useState([]);
  const [previews, setPreviews] = React.useState([]);
  const [primaryIndex, setPrimaryIndex] = React.useState(0);
  const [imagesName, setImagesName] = React.useState("");

  // form
  const [form, setForm] = React.useState({
    productCode: "",
    productName: "",
    supplierId: 1,
    productType: "PERSONAL_KEY",
    costPrice: 0,
    salePrice: 0,
    stockQty: 0,
    warrantyDays: 0,
    expiryDate: "",
    autoDelivery: false,
    status: "ACTIVE",
    description: "",
    shortDesc: "",
    categoryIds: [],
    badgeCodes: [],
  });

  React.useEffect(() => {
    CategoryApi.list({ active: true }).then(setCats).catch(() => {});
    BadgesApi.list({ active: true }).then(setBadges).catch(() => {});
  }, []);

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }));

  const statusClass = (s) =>
    s === "ACTIVE" ? "badge green" : s === "OUT_OF_STOCK" ? "badge warning" : "badge gray";

  const statusText = React.useMemo(() => {
    switch (form.status) {
      case "ACTIVE": return "Đang hiển thị";
      case "INACTIVE": return "Đang ẩn";
      case "OUT_OF_STOCK": return "Hết hàng";
      default: return form.status || "-";
    }
  }, [form.status]);

  const toggleStatus = () => {
    set("status", form.status === "ACTIVE" ? "INACTIVE" : "ACTIVE");
  };

  // ======= save =======
  const save = async (publish = true) => {
    try {
      setSaving(true);
      const payload = { ...form, status: publish ? form.status : "INACTIVE" };
      payload.badgeCodes = payload.badgeCodes ?? [];
      if (!payload.expiryDate) payload.expiryDate = null;

      if (selectedFiles && selectedFiles.length > 0) {
        await ProductApi.createWithImages(payload, selectedFiles, primaryIndex);
      } else {
        await ProductApi.create(payload);
      }
      alert(publish ? "Đã tạo & xuất bản sản phẩm" : "Đã lưu nháp sản phẩm");
      nav("/admin/products");
    } catch (e) {
      alert(e?.response?.data?.message || e.message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="page">
      <div className="card">
        {/* Header: tiêu đề + công tắc trạng thái + nhãn + quay lại */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 10,
          }}
        >
          <h2>Thêm sản phẩm</h2>

          <div className="row" style={{ gap: 10, alignItems: "center" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <label className="switch" title="Bật/Tắt hiển thị">
                <input
                  type="checkbox"
                  checked={form.status === "ACTIVE"}
                  onChange={toggleStatus}
                  aria-label="Bật/Tắt hiển thị sản phẩm"
                />
                <span className="slider" />
              </label>
              <span className={statusClass(form.status)} style={{ textTransform: "none" }}>
                {statusText}
              </span>
            </div>

            <button className="btn ghost" onClick={() => nav("/admin/products")}>
              ⬅ Quay lại
            </button>
          </div>
        </div>

        {/* GRID 2 cột + input-group để đồng bộ format */}
        <div className="grid cols-2 input-group">
          {/* HÀNG 1: Tên + Mã */}
          <div className="group" style={{ gridColumn: "1 / 2" }}>
            <span>Tên sản phẩm</span>
            <input
              value={form.productName}
              onChange={(e) => set("productName", e.target.value)}
              placeholder="VD: Microsoft 365 Family"
            />
          </div>
          <div className="group" style={{ gridColumn: "2 / 3" }}>
            <span>Mã định danh sản phẩm</span>
            <input
              value={form.productCode}
              onChange={(e) => set("productCode", e.target.value)}
              placeholder="VD: OFF_365_FAM"
            />
          </div>

          {/* HÀNG 2: Loại + Bảo hành (ngày) */}
          <div className="group" style={{ gridColumn: "1 / 2" }}>
            <span>Loại</span>
            <select
              value={form.productType}
              onChange={(e) => set("productType", e.target.value)}
            >
              <option value="PERSONAL_KEY">Mã cá nhân</option>
              <option value="SHARED_KEY">Mã dùng chung</option>
              <option value="PERSONAL_ACCOUNT">Tài khoản cá nhân</option>
              <option value="SHARED_ACCOUNT">Tài khoản dùng chung</option>
            </select>
          </div>

          <div className="group" style={{ gridColumn: "2 / 3" }}>
            <span>Bảo hành (ngày)</span>
            <input
              type="number"
              min={0}
              step={1}
              value={form.warrantyDays}
              onChange={(e) => set("warrantyDays", Number(e.target.value) || 0)}
              placeholder="VD: 365"
            />
          </div>

          {/* HÀNG 3: Danh mục (c1) + Nhãn (c2) */}
          <div className="group" style={{ gridColumn: "1 / 2" }}>
            <div className={`panel ${!showCats ? "collapsed" : ""}`}>
              <div className="panel-header" onClick={() => setShowCats((s) => !s)}>
                <h4>
                  Danh mục sản phẩm{" "}
                  <span style={{ fontSize: 12, color: "var(--muted)", marginLeft: 8 }}>
                    ({cats.length})
                  </span>
                </h4>
                <div className="caret">▾</div>
              </div>
              {showCats && (
                <div className="panel-body">
                  {cats.map((c) => (
                    <div key={c.categoryId} className="list-row">
                      <div className="left">
                        {c.thumbnailUrl ? (
                          <img src={c.thumbnailUrl} alt="" />
                        ) : (
                          <div
                            style={{
                              width: 36,
                              height: 36,
                              background: "#f3f4f6",
                              borderRadius: 6,
                            }}
                          />
                        )}
                        <div>{c.categoryName}</div>
                      </div>
                      <div>
                        <label className="switch">
                          <input
                            type="checkbox"
                            checked={(form.categoryIds || []).includes(c.categoryId)}
                            onChange={(e) => {
                              const prev = form.categoryIds || [];
                              if (e.target.checked)
                                set(
                                  "categoryIds",
                                  Array.from(new Set([...prev, c.categoryId]))
                                );
                              else
                                set(
                                  "categoryIds",
                                  prev.filter((x) => x !== c.categoryId)
                                );
                            }}
                          />
                          <span className="slider" />
                        </label>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          <div className="group" style={{ gridColumn: "2 / 3" }}>
            <div className={`panel ${!showBadgesPanel ? "collapsed" : ""}`}>
              <div className="panel-header" onClick={() => setShowBadgesPanel((s) => !s)}>
                <h4>
                  Nhãn sản phẩm{" "}
                  <span style={{ fontSize: 12, color: "var(--muted)", marginLeft: 8 }}>
                    ({badges.length})
                  </span>
                </h4>
                <div className="caret">▾</div>
              </div>

              {showBadgesPanel && (
                <div className="panel-body">
                  {badges.map((b) => {
                    const color =
                      b?.colorHex || b?.color || b?.colorhex || b?.ColorHex || "#1e40af";
                    const name =
                      b?.displayName ||
                      b?.badgeName ||
                      b?.name ||
                      b?.BadgeDisplayName ||
                      b?.BadgeName ||
                      b?.badgeCode ||
                      "";
                    const code = b?.badgeCode ?? name;
                    return (
                      <div key={code} className="list-row">
                        <div className="left">
                          {/* Nền = màu, chữ trắng */}
                          <span
                            className="label-chip"
                            style={{ backgroundColor: color, color: "#fff" }}
                            title={name}
                          >
                            {name}
                          </span>
                        </div>
                        <div>
                          <label className="switch">
                            <input
                              type="checkbox"
                              checked={(form.badgeCodes || []).includes(code)}
                              onChange={(e) => {
                                const prev = form.badgeCodes || [];
                                if (e.target.checked)
                                  set("badgeCodes", Array.from(new Set([...prev, code])));
                                else set("badgeCodes", prev.filter((x) => x !== code));
                              }}
                            />
                            <span className="slider" />
                          </label>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>

          {/* HÀNG 4: Giá bán + Giá niêm yết */}
          <div className="group" style={{ gridColumn: "1 / 2" }}>
            <span>Giá bán (đ)</span>
            <input
              type="number"
              value={form.salePrice}
              onChange={(e) => set("salePrice", Number(e.target.value) || 0)}
              placeholder="349000"
            />
          </div>
          <div className="group" style={{ gridColumn: "2 / 3" }}>
            <span>Giá gốc/niêm yết (đ)</span>
            <input
              type="number"
              value={form.costPrice}
              onChange={(e) => set("costPrice", Number(e.target.value) || 0)}
              placeholder="399000"
            />
          </div>

          {/* HÀNG 5: Mô tả ngắn + Mô tả chi tiết */}
          <div className="group" style={{ gridColumn: "1 / 2" }}>
            <span>Mô tả ngắn</span>
            <textarea
              value={form.shortDesc || ""}
              onChange={(e) => set("shortDesc", e.target.value)}
              placeholder="Hiển thị trong danh sách…"
            />
          </div>
          <div className="group" style={{ gridColumn: "2 / 3" }}>
            <span>Mô tả chi tiết</span>
            <textarea
              value={form.description}
              onChange={(e) => set("description", e.target.value)}
              placeholder="Nội dung landing sản phẩm…"
            />
          </div>

          {/* HÀNG 6: Ảnh (full width) */}
          <div className="group" style={{ gridColumn: "1 / 3" }}>
            <span>Ảnh sản phẩm</span>
            <div className="file-upload">
              <input
                id="prodImages"
                type="file"
                accept="image/*"
                multiple
                onChange={(e) => {
                  const files = Array.from(e.target.files || []);
                  setSelectedFiles(files);
                  setImagesName(
                    files.length > 0 ? `${files.length} ảnh đã chọn` : "Chưa chọn ảnh"
                  );
                  const urls = files.map((f) => ({
                    name: f.name,
                    url: URL.createObjectURL(f),
                  }));
                  previews.forEach((p) => URL.revokeObjectURL(p.url));
                  setPreviews(urls);
                  setPrimaryIndex(0);
                }}
              />
              <label htmlFor="prodImages" className="btn btn-upload">
                Chọn ảnh
              </label>
              <span className="file-name">{imagesName || "Chưa chọn ảnh"}</span>
            </div>
          </div>

          {/* HÀNG 7: PREVIEW ẢNH (full width) */}
          <div className="group" style={{ gridColumn: "1 / 3" }}>
            {previews.length > 0 && (
              <div
                style={{
                  display: "flex",
                  gap: 10,
                  marginTop: 4,
                  flexWrap: "wrap",
                  alignItems: "stretch",
                }}
              >
                {previews.map((p, idx) => (
                  <div
                    key={p.url}
                    style={{
                      border:
                        primaryIndex === idx
                          ? "2px solid var(--primary)"
                          : "1px solid var(--line)",
                      padding: 8,
                      borderRadius: 10,
                      background: "#fff",
                    }}
                  >
                    <img
                      src={p.url}
                      alt={p.name}
                      style={{
                        width: 160,
                        height: 110,
                        objectFit: "cover",
                        display: "block",
                      }}
                    />
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 8,
                        marginTop: 6,
                      }}
                    >
                      <label style={{ display: "flex", alignItems: "center", gap: 6 }}>
                        <input
                          type="radio"
                          name="primary"
                          checked={primaryIndex === idx}
                          onChange={() => setPrimaryIndex(idx)}
                        />
                        <span style={{ fontSize: 12 }}>
                          {idx === 0 ? "Ảnh mặc định" : `Ảnh ${idx + 1}`}
                        </span>
                      </label>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* ACTIONS */}
        <div className="row" style={{ marginTop: 12 }}>
          <button className="btn" disabled={saving} onClick={() => save(false)}>
            Lưu nháp
          </button>
          <button className="btn primary" disabled={saving} onClick={() => save(true)}>
            Lưu &amp; Xuất bản
          </button>
        </div>
      </div>
    </div>
  );
}
