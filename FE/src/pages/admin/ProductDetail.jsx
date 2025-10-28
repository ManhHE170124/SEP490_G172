import React from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import AdminLayout from "../../components/admin/Layout";
import { ProductApi } from "../../services/products";
import { CategoryApi } from "../../services/categories";
import { BadgesApi } from "../../services/badges";
import { useConfirm } from "../../components/common/ConfirmProvider.jsx";

export default function ProductDetail() {
  const { id } = useParams();
  const productId = id; // id is GUID string
  const nav = useNavigate();
  const confirm = useConfirm();

  const [loading, setLoading] = React.useState(true);
  const [saving, setSaving] = React.useState(false);
  const [notFound, setNotFound] = React.useState(false);

  const [cats, setCats] = React.useState([]);
  const [badges, setBadges] = React.useState([]);
  const [images, setImages] = React.useState([]);
  const [newFiles, setNewFiles] = React.useState([]);
  const [newPreviews, setNewPreviews] = React.useState([]);
  const [deleteImageIds, setDeleteImageIds] = React.useState([]);
  const [primaryIndex, setPrimaryIndex] = React.useState(null);
  const [showCats, setShowCats] = React.useState(true);
  const [showBadgesPanel, setShowBadgesPanel] = React.useState(true);

  const [form, setForm] = React.useState({
    productCode: "",
    productName: "",
    supplierId: 1,
    productType: "SOFTWARE",
    costPrice: 0,
    salePrice: 0,
    stockQty: 0,
    warrantyDays: 0,
    expiryDate: "",
    autoDelivery: false,
    status: "ACTIVE",
    description: "",
    shortDesc: "",
    badgeCodes: [],
    thumbnailUrl: "",
    categoryIds: [],
    categoryId: null, // đề phòng BE trả dạng đơn
  });

  const set = (k, v) => setForm((s) => ({ ...s, [k]: v }));

  // badge + lớp màu trạng thái
  const statusClass = (s) =>
    s === "ACTIVE"
      ? "badge green"
      : s === "OUT_OF_STOCK"
      ? "badge warning"
      : "badge gray";

  // nạp danh mục & chi tiết sản phẩm
  const load = React.useCallback(async () => {
    try {
      setLoading(true);
      setNotFound(false);

      const [dto, catList, badgeList] = await Promise.all([
        ProductApi.get(productId),
        CategoryApi.list({ active: true }),
        BadgesApi.list({ active: true }),
      ]);

      setCats(catList || []);
      setBadges(badgeList || []);

      if (!dto) {
        setNotFound(true);
        return;
      }

      // chuẩn hóa categoryIds
      const ids =
        dto.categoryIds && dto.categoryIds.length
          ? dto.categoryIds
          : dto.categoryId
          ? [dto.categoryId]
          : [];

      setForm((s) => ({
        ...s,
        productCode: dto.productCode || "",
        productName: dto.productName || "",
        supplierId: dto.supplierId ?? 1,
        productType: dto.productType || "SOFTWARE",
        costPrice: dto.costPrice ?? 0,
        salePrice: dto.salePrice ?? 0,
        stockQty: dto.stockQty ?? 0,
        warrantyDays: dto.warrantyDays ?? 0,
        expiryDate: dto.expiryDate || "",
        autoDelivery: !!dto.autoDelivery,
        status: dto.status || "INACTIVE",
        description: dto.description || "",
  shortDesc: dto.shortDesc || "",
  badgeCodes: dto.badgeCodes ?? [],
        categoryIds: ids,
        categoryId: dto.categoryId ?? null,
      }));
      setImages((dto.images || []).map(i => ({
        imageId: i.imageId ?? i.ImageId,
        url: i.url ?? i.Url,
        sortOrder: i.sortOrder ?? i.SortOrder,
        isPrimary: i.isPrimary ?? i.IsPrimary,
      })));
      // set default primary index based on server isPrimary flag
      const imgs = (dto.images || []).map(i => ({
        imageId: i.imageId ?? i.ImageId,
        url: i.url ?? i.Url,
        sortOrder: i.sortOrder ?? i.SortOrder,
        isPrimary: i.isPrimary ?? i.IsPrimary,
      }));
      const prim = imgs.findIndex(x => x.isPrimary);
      setPrimaryIndex(prim >= 0 ? prim : (imgs.length > 0 ? 0 : null));
      // thumbnail
      setForm(f => ({ ...f, thumbnailUrl: dto.thumbnailUrl ?? f.thumbnailUrl }));
    } catch (e) {
      setNotFound(true);
    } finally {
      setLoading(false);
    }
  }, [productId]);

  React.useEffect(() => {
    load();
  }, [load]);

  // revoke object URLs when previews change/unmount
  React.useEffect(() => {
    return () => {
      newPreviews.forEach(p => URL.revokeObjectURL(p.url));
    };
  }, [newPreviews]);

  const save = async () => {
    try {
      setSaving(true);
      const payload = { ...form };

      // đồng bộ categoryIds gửi lên BE
      if (!payload.categoryIds || payload.categoryIds.length === 0) {
        payload.categoryIds = payload.categoryId ? [payload.categoryId] : [];
      }

      // ensure badgeCodes is array
      payload.badgeCodes = payload.badgeCodes ?? [];

      // normalize expiryDate for backend DateOnly? (send null when empty)
      if (!payload.expiryDate) payload.expiryDate = null;

      // compute primary index for API: combined = existing images (after deletions) + newFiles
      const existingCount = images.length; // images state already pruned for deletions
      const apiPrimary = primaryIndex !== null && primaryIndex !== undefined ? primaryIndex : null;

      if ((newFiles && newFiles.length > 0) || (deleteImageIds && deleteImageIds.length > 0) || apiPrimary !== null) {
        // call multipart update to atomically apply image changes
        await ProductApi.updateWithImages(productId, payload, newFiles, apiPrimary, deleteImageIds);
      } else {
        await ProductApi.update(productId, payload);
      }

      alert("Đã lưu thay đổi sản phẩm.");
      // clear temporary states
      setNewFiles([]);
      newPreviews.forEach(p => URL.revokeObjectURL(p.url));
      setNewPreviews([]);
      setDeleteImageIds([]);
      await load();
    } catch (e) {
      alert(e?.response?.data?.message || e.message);
    } finally {
      setSaving(false);
    }
  };

  const toggle = async () => {
    const next =
      form.status === "ACTIVE" ? "INACTIVE" : form.status === "INACTIVE" ? "ACTIVE" : "ACTIVE";
    // quick toggle without confirmation
    try {
      await ProductApi.toggle(productId);
    } catch (e) {
      await ProductApi.changeStatus(productId, next);
    }
    await load();
  };

  if (loading) {
    return (
      <AdminLayout>
        <div className="card">
          <div>Đang tải chi tiết sản phẩm…</div>
        </div>
      </AdminLayout>
    );
  }

  if (notFound) {
    return (
      <AdminLayout>
        <div className="card">
          <h2>Không tìm thấy sản phẩm</h2>
          <div className="row" style={{ marginTop: 10 }}>
            <Link className="btn" to="/admin/products">
              ← Quay lại danh sách
            </Link>
          </div>
        </div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      {/* Header gọn, không có nút bên phải để đồng bộ rule trước đó */}
      <div className="card">
        <h2>Chi tiết sản phẩm</h2>

        {/* Nhóm trạng thái + hành động giống khối danh mục */}
        <div className="row" style={{ gap: 10, alignItems: "center", marginTop: 6 }}>
                <span className={statusClass(form.status)}>{form.status}</span>
                <label className="switch" title="Bật/Tắt hiển thị">
                  <input type="checkbox" checked={form.status === 'ACTIVE'} onChange={toggle} />
                  <span className="slider" />
                </label>
              </div>

        {/* Form chính */}
        <div className="grid cols-2" style={{ marginTop: 12 }}>
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
              className="mono"
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
                  {cats.map((c) => (
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
            <select
              value={form.productType}
              onChange={(e) => set("productType", e.target.value)}
            >
              <option value="PERSONAL_KEY">Key cá nhân</option>
              <option value="SHARED_KEY">Key dùng chung</option>
              <option value="PERSONAL_ACCOUNT">Tài khoản cá nhân</option>
              <option value="SHARED_ACCOUNT">Tài khoản dùng chung</option>
            </select>
          </div>

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

          <div className="group">
            <label>Tồn kho</label>
            <input
              type="number"
              value={form.stockQty}
              onChange={(e) => set("stockQty", Number(e.target.value) || 0)}
              placeholder="0"
            />
          </div>
          <div className="group">
            <label>Bảo hành (ngày)</label>
            <input
              type="number"
              value={form.warrantyDays}
              onChange={(e) => set("warrantyDays", Number(e.target.value) || 0)}
              placeholder="0"
            />
          </div>

          <div className="group" style={{ gridColumn: "1/3" }}>
            <label>Mô tả ngắn</label>
            <textarea
              value={form.shortDesc || ""}
              onChange={(e) => set("shortDesc", e.target.value)}
              placeholder="Hiển thị trong danh sách…"
            />
          </div>
          <div className="group" style={{ gridColumn: "1/3" }}>
            <label>Mô tả chi tiết</label>
            <textarea
              value={form.description}
              onChange={(e) => set("description", e.target.value)}
              placeholder="Nội dung landing sản phẩm…"
              rows={6}
            />
          </div>
        </div>
      </div>

      {/* Cấu hình hiển thị */}
      <div className="card" style={{ marginTop: 14 }}>
        <h2>Cấu hình hiển thị</h2>
        <div className="grid cols-3">
          <div className="group">
            <label>Ảnh sản phẩm</label>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <input type="file" accept="image/*" multiple onChange={(e) => {
                const files = Array.from(e.target.files || []);
                if (!files.length) return;
                setNewFiles(files);
                // create previews
                const urls = files.map(f => ({ name: f.name, url: URL.createObjectURL(f) }));
                // revoke old previews
                newPreviews.forEach(p => URL.revokeObjectURL(p.url));
                setNewPreviews(urls);
                // if no primary selected yet, set to first existing or new
                if (primaryIndex === null) setPrimaryIndex(images.length > 0 ? 0 : 0);
              }} />
            </div>
            <div style={{ marginTop: 8, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              {/* render existing images (not deleted) then new previews */}
              {images.map((img, idx) => (
                <div key={img.imageId} style={{ border: '1px solid #eee', padding: 6, position: 'relative' }}>
                  <img src={img.url} alt="" style={{ width: 120, height: 80, objectFit: 'cover', display: 'block' }} />
                  <div style={{ position: 'absolute', left: 6, top: 6 }}>
                    {primaryIndex === idx && <div className="primary-indicator">★</div>}
                  </div>
                  <div style={{ marginTop: 6 }}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <input type="radio" name="primary" checked={primaryIndex === idx} onChange={() => setPrimaryIndex(idx)} />
                      <span style={{ fontSize: 12 }}>Chọn làm chính</span>
                    </label>
                    <div style={{ marginTop: 6 }}>
                      <button className="btn" onClick={() => {
                        // mark for deletion (defer to updateWithImages)
                        setDeleteImageIds(prev => Array.from(new Set([...prev, img.imageId])));
                        setImages(prev => prev.filter(x => x.imageId !== img.imageId));
                        // adjust primaryIndex if needed
                        setPrimaryIndex(pi => {
                          if (pi === null) return null;
                          if (pi > idx) return pi - 1; // shift left
                          if (pi === idx) return Math.max(0, Math.min(images.length - 2, 0));
                          return pi;
                        });
                      }}>Remove</button>
                    </div>
                  </div>
                </div>
              ))}

              {newPreviews.map((p, nidx) => {
                const overallIndex = images.length + nidx;
                return (
                  <div key={p.url} style={{ border: '1px solid #eee', padding: 6, position: 'relative' }}>
                    <img src={p.url} alt={p.name} style={{ width: 120, height: 80, objectFit: 'cover', display: 'block' }} />
                    <div style={{ position: 'absolute', left: 6, top: 6 }}>
                      {primaryIndex === overallIndex && <div className="primary-indicator">★</div>}
                    </div>
                    <div style={{ marginTop: 6 }}>
                      <label style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        <input type="radio" name="primary" checked={primaryIndex === overallIndex} onChange={() => setPrimaryIndex(overallIndex)} />
                        <span style={{ fontSize: 12 }}>Chọn làm chính</span>
                      </label>
                      <div style={{ marginTop: 6 }}>
                        <button className="btn" onClick={() => {
                          // remove this new file from selection
                          setNewFiles(prev => prev.filter((_, i) => i !== nidx));
                          setNewPreviews(prev => {
                            const toRevoke = prev[nidx];
                            if (toRevoke) URL.revokeObjectURL(toRevoke.url);
                            return prev.filter((_, i) => i !== nidx);
                          });
                          setPrimaryIndex(pi => {
                            if (pi === null) return null;
                            if (pi === overallIndex) return 0;
                            return pi > overallIndex ? pi - 1 : pi;
                          });
                        }}>Remove</button>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
          <div className="group">
            <label>Ảnh đại diện</label>
            <label className="btn">
              Upload & set thumbnail
              <input type="file" accept="image/*" style={{ display: 'none' }} onChange={async (e) => {
                const f = e.target.files?.[0];
                if (!f) return;
                try {
                  const res = await ProductApi.uploadImage(productId, f);
                  const url = res?.url || res?.Url || res?.url;
                  if (url) {
                    await ProductApi.setThumbnail(productId, url);
                    await load();
                  } else {
                    alert('Upload thành công nhưng không lấy được URL ảnh');
                  }
                } catch (err) {
                  alert(err?.response?.data?.message || err.message);
                }
                e.target.value = '';
              }} />
            </label>
            {form.thumbnailUrl && (
              <div style={{ marginTop: 8 }}>
                <img src={form.thumbnailUrl} alt="thumbnail" style={{ width: 160, height: 100, objectFit: 'cover', border: '1px solid #ddd' }} />
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
        <button className="btn primary" disabled={saving} onClick={save}>
          {saving ? "Đang lưu…" : "Lưu thay đổi"}
        </button>
      </div>
    </AdminLayout>
  );
}
