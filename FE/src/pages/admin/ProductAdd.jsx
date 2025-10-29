import React from "react";
import { useNavigate } from "react-router-dom";
import { ProductApi } from "../../services/products";
import { CategoryApi } from "../../services/categories";
import { BadgesApi } from "../../services/badges";
import "./admin.css";
export default function ProductAdd() {
  const nav = useNavigate();
  const [cats, setCats] = React.useState([]);
  const [badges, setBadges] = React.useState([]);
  const [showCats, setShowCats] = React.useState(true);
  const [showBadgesPanel, setShowBadgesPanel] = React.useState(true);
  const [saving, setSaving] = React.useState(false);
  const [selectedFiles, setSelectedFiles] = React.useState([]);
  const [previews, setPreviews] = React.useState([]);
  const [primaryIndex, setPrimaryIndex] = React.useState(0);
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
    categoryIds: [],
    badgeCodes: [],
  });

  React.useEffect(() => {
    CategoryApi.list({ active: true }).then(setCats).catch(() => {});
    BadgesApi.list({ active: true }).then(setBadges).catch(() => {});
  }, []);

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }));

  const save = async (publish = true) => {
    try {
      setSaving(true);
      const payload = { ...form, status: publish ? form.status : "INACTIVE" };
      // ensure badgeCodes array
      payload.badgeCodes = payload.badgeCodes ?? [];

      // normalize expiryDate for backend DateOnly? (send null when empty)
      if (!payload.expiryDate) payload.expiryDate = null; // backend chấp nhận null

      if (selectedFiles && selectedFiles.length > 0) {
        // use multipart createWithImages
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
        <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:10}}>
          <h2>Thêm sản phẩm</h2>
          <div className="row">
            <button className="btn ghost" onClick={() => nav("/admin/products")}>⬅ Quay lại</button>
          </div>
        </div>

        <div className="grid cols-2">
          <div className="group">
            <label>Tên sản phẩm</label>
            <input
              value={form.productName}
              onChange={(e) => set("productName", e.target.value)}
              placeholder="VD: Microsoft 365 Family"
            />
          </div>
          <div className="group">
            <label>SKU</label>
            <input
              value={form.productCode}
              onChange={(e) => set("productCode", e.target.value)}
              placeholder="OFF_365_FAM"
            />
          </div>

          <div className="group">
            <div className={`panel ${!showCats ? 'collapsed' : ''}`}>
              <div className="panel-header" onClick={() => setShowCats(s => !s)}>
                <h4>Danh mục <span style={{fontSize:12,color:'var(--muted)',marginLeft:8}}>({cats.length})</span></h4>
                <div className="caret">▾</div>
              </div>
              {showCats && (
                <div className="panel-body">
                  {cats.map(c => (
                    <div key={c.categoryId} className="list-row">
                      <div className="left">
                        {c.thumbnailUrl ? <img src={c.thumbnailUrl} alt="" /> : <div style={{width:36,height:36,background:'#f3f4f6',borderRadius:6}}/>}
                        <div>{c.categoryName}</div>
                      </div>
                      <div>
                        <label className="switch">
                          <input
                            type="checkbox"
                            checked={(form.categoryIds || []).includes(c.categoryId)}
                            onChange={(e) => {
                              const prev = form.categoryIds || [];
                              if (e.target.checked) set('categoryIds', Array.from(new Set([...prev, c.categoryId])));
                              else set('categoryIds', prev.filter(x => x !== c.categoryId));
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

          <div className="group">
            <label>Loại</label>
            <select value={form.productType} onChange={(e) => set("productType", e.target.value)}>
              <option value="PERSONAL_KEY">Key cá nhân</option>
              <option value="SHARED_KEY">Key dùng chung</option>
              <option value="PERSONAL_ACCOUNT">Tài khoản cá nhân</option>
              <option value="SHARED_ACCOUNT">Tài khoản dùng chung</option>
            </select>
          </div>

          <div className="group">
            <label>Trạng thái hiển thị</label>
            <select value={form.status} onChange={(e) => set("status", e.target.value)}>
              <option value="ACTIVE">Hiển thị</option>
              <option value="INACTIVE">Ẩn</option>
              <option value="OUT_OF_STOCK">Hết hàng</option>
            </select>
          </div>
          <div />

          <div className="group">
            <label>Giá bán (đ)</label>
            <input
              type="number"
              value={form.salePrice}
              onChange={(e) => set("salePrice", Number(e.target.value) || 0)}
              placeholder="349000"
            />
          </div>
          <div className="group">
            <label>Giá gốc/niêm yết (đ)</label>
            <input
              type="number"
              value={form.costPrice}
              onChange={(e) => set("costPrice", Number(e.target.value) || 0)}
              placeholder="399000"
            />
          </div>

          <div className="group" style={{ gridColumn: "1/2" }}>
            <label>Mô tả ngắn</label>
            <textarea
              value={form.shortDesc || ""}
              onChange={(e) => set("shortDesc", e.target.value)}
              placeholder="Hiển thị trong danh sách…"
            />
          </div>
          <div className="group" style={{ gridColumn: "2/3" }}>
            <label>Mô tả chi tiết</label>
            <textarea
              value={form.description}
              onChange={(e) => set("description", e.target.value)}
              placeholder="Nội dung landing sản phẩm…"
            />
          </div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <h2>Cấu hình hiển thị</h2>
        <div className="grid cols-3">
          <div className="group">
            <label>Ảnh đại diện</label>
            <input
              type="file"
              multiple
              onChange={(e) => {
                const files = Array.from(e.target.files || []);
                setSelectedFiles(files);
                // build previews
                const urls = files.map((f) => ({ name: f.name, url: URL.createObjectURL(f) }));
                // revoke previous
                previews.forEach(p => URL.revokeObjectURL(p.url));
                setPreviews(urls);
                setPrimaryIndex(0);
              }}
            />
            {previews.length > 0 && (
              <div style={{ display: 'flex', gap: 8, marginTop: 8, flexWrap: 'wrap' }}>
                {previews.map((p, idx) => (
                  <div key={p.url} style={{ border: primaryIndex === idx ? '2px solid var(--primary)' : '1px solid #eee', padding: 6, borderRadius: 8 }}>
                    <img src={p.url} alt={p.name} style={{ width: 120, height: 80, objectFit: 'cover', display: 'block' }} />
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 6 }}>
                      <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        <input type="radio" name="primary" checked={primaryIndex === idx} onChange={() => setPrimaryIndex(idx)} />
                        <span style={{ fontSize: 12 }}>{idx === 0 ? 'Mặc định' : `Ảnh ${idx + 1}`}</span>
                      </label>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
          <div className="group">
            <div className={`panel ${!showBadgesPanel ? 'collapsed' : ''}`}>
              <div className="panel-header" onClick={() => setShowBadgesPanel(s => !s)}>
                <h4>Badge <span style={{fontSize:12,color:'var(--muted)',marginLeft:8}}>({badges.length})</span></h4>
                <div className="caret">▾</div>
              </div>
              {showBadgesPanel && (
                <div className="panel-body">
                  {badges.map(b => (
                    <div key={b.badgeCode} className="list-row">
                      <div className="left">
                        {b.thumbUrl ? <img src={b.thumbUrl} alt="" /> : <div style={{width:36,height:36,background:'#f3f4f6',borderRadius:6}}/>}
                        <div>{b.badgeName ?? b.badgeCode}</div>
                      </div>
                      <div>
                        <label className="switch">
                          <input
                            type="checkbox"
                            checked={(form.badgeCodes || []).includes(b.badgeCode)}
                            onChange={(e) => {
                              const prev = form.badgeCodes || [];
                              if (e.target.checked) set('badgeCodes', Array.from(new Set([...prev, b.badgeCode])));
                              else set('badgeCodes', prev.filter(x => x !== b.badgeCode));
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
        </div>
      </div>

      <div className="row" style={{ marginTop: 12 }}>
        <button className="btn" disabled={saving} onClick={() => save(false)}>Lưu nháp</button>
        <button className="btn primary" disabled={saving} onClick={() => save(true)}>Lưu & Xuất bản</button>
      </div>
    </div>
  );
}
