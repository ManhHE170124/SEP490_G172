import React from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { ProductApi } from "../../services/products";
import { CategoryApi } from "../../services/categories";
import { BadgesApi } from "../../services/badges";
import VariantsPanel from "../admin/VariantsPanel";
import FaqsPanel from "../admin/FaqsPanel";
import "./admin.css";

export default function ProductDetail() {
  const { id } = useParams();
  const productId = id;
  const nav = useNavigate();
const normalizeList = (data) => {
  if (Array.isArray(data)) return data;
  if (Array.isArray(data?.items)) return data.items;
  if (Array.isArray(data?.data)) return data.data;
  if (Array.isArray(data?.result)) return data.result;
  return [];
};

  // loading/ui
  const [loading, setLoading] = React.useState(true);
  const [saving, setSaving] = React.useState(false);
  const [notFound, setNotFound] = React.useState(false);

  // meta
  const [cats, setCats] = React.useState([]);
  const [badges, setBadges] = React.useState([]);
  const [showCats, setShowCats] = React.useState(true);
  const [showBadgesPanel, setShowBadgesPanel] = React.useState(true);

  const [images, setImages] = React.useState([]);
  const [newFiles, setNewFiles] = React.useState([]);
  const [newPreviews, setNewPreviews] = React.useState([]);
  const [deleteImageIds, setDeleteImageIds] = React.useState([]);
  const [primaryIndex, setPrimaryIndex] = React.useState(null);
const catsList = React.useMemo(() => normalizeList(cats), [cats]);
const badgesList = React.useMemo(() => normalizeList(badges), [badges]);



  // form
  const [form, setForm] = React.useState({
    productCode: "",
    productName: "",
    supplierId: 1,
    productType: "PERSONAL_KEY",
    costPrice: 0,
    salePrice: 0,
    stockQty: 0,                    // CHỈ HIỂN THỊ (không cho sửa)
    warrantyDays: 0,
    expiryDate: "",
    autoDelivery: false,
    status: "ACTIVE",
    description: "",
    shortDesc: "",
    badgeCodes: [],
    categoryIds: [],
    categoryId: null,
  });
  const set = (k, v) => setForm((s) => ({ ...s, [k]: v }));

  const getBadgeName = (b) =>
    b?.displayName ||
    b?.badgeName ||
    b?.name ||
    b?.BadgeDisplayName ||
    b?.BadgeName ||
    b?.badgeCode ||
    "";

  const getBadgeColor = (b) =>
    b?.colorHex || b?.color || b?.colorhex || b?.ColorHex || "#1e40af";

  const statusClass = (s) =>
    s === "ACTIVE" ? "badge green" : s === "OUT_OF_STOCK" ? "badge warning" : "badge gray";

  const load = React.useCallback(async () => {
    try {
      setLoading(true);
      setNotFound(false);

      const [dto, catList, badgeList] = await Promise.all([
        ProductApi.get(productId),
        CategoryApi.list({ active: true }),
        BadgesApi.list({ active: true }),
      ]);

    setCats(normalizeList(catList));
    setBadges(normalizeList(badgeList));

      if (!dto) {
        setNotFound(true);
        return;
      }

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
        productType: dto.productType || "PERSONAL_KEY",
        costPrice: dto.costPrice ?? 0,
        salePrice: dto.salePrice ?? 0,
        stockQty: dto.stockQty ?? 0,              // chỉ hiển thị
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


      const imgs = (dto.images || []).map((i) => ({
        imageId: i.imageId ?? i.ImageId,
        url: i.url ?? i.Url,
        sortOrder: i.sortOrder ?? i.SortOrder,
        isPrimary: i.isPrimary ?? i.IsPrimary,
      }));
      setImages(imgs);
      const prim = imgs.findIndex((x) => x.isPrimary);
      setPrimaryIndex(prim >= 0 ? prim : imgs.length > 0 ? 0 : null);
    } catch (e) {
      setNotFound(true);
    } finally {
      setLoading(false);
    }
  }, [productId]);

  React.useEffect(() => {
    load();
  }, [load]);

  React.useEffect(() => {
    return () => {
      newPreviews.forEach((p) => URL.revokeObjectURL(p.url));
    };
  }, [newPreviews]);

  // save
  const save = async () => {
    try {
      setSaving(true);
      const payload = { ...form };

      if (!payload.categoryIds || payload.categoryIds.length === 0) {
        payload.categoryIds = payload.categoryId ? [payload.categoryId] : [];
      }
      payload.badgeCodes = payload.badgeCodes ?? [];
      if (!payload.expiryDate) payload.expiryDate = null;

      // giữ cost/sale nếu BE cần, ở đây không chỉnh vì không có input
      const apiPrimary =
        primaryIndex !== null && primaryIndex !== undefined
          ? primaryIndex
          : null;

      if (
        (newFiles && newFiles.length > 0) ||
        (deleteImageIds && deleteImageIds.length > 0) ||
        apiPrimary !== null
      ) {
        await ProductApi.updateWithImages(
          productId,
          payload,
          newFiles,
          apiPrimary,
          deleteImageIds
        );
      } else {
        await ProductApi.update(productId, payload);
      }

      alert("Đã lưu thay đổi sản phẩm.");
      setNewFiles([]);
      newPreviews.forEach((p) => URL.revokeObjectURL(p.url));
      setNewPreviews([]);
      setDeleteImageIds([]);
      await load();
    } catch (e) {
      alert(e?.response?.data?.message || e.message);
    } finally {
      setSaving(false);
    }
  };

  const statusText = React.useMemo(() => {
    switch (form.status) {
      case "ACTIVE":
        return "Đang hiển thị";
      case "INACTIVE":
        return "Đang ẩn";
      case "OUT_OF_STOCK":
        return "Hết hàng";
      default:
        return form.status || "-";
    }
  }, [form.status]);

  const toggleActive = async () => {
    const next =
      form.status === "ACTIVE"
        ? "INACTIVE"
        : form.status === "INACTIVE"
        ? "ACTIVE"
        : "ACTIVE";
    try {
      await ProductApi.changeStatus(productId, next);
      set("status", next);
    } catch (e) {
      try {
        await ProductApi.toggle(productId);
        set("status", next);
      } catch (err) {
        alert(err?.response?.data?.message || err.message);
      }
    }
  };

  if (loading) {
    return (
      <div className="page">
        <div className="card">
          <div>Đang tải chi tiết sản phẩm…</div>
        </div>
      </div>
    );
  }

  if (notFound) {
    return (
      <div className="page">
        <div className="card">
          <h2>Không tìm thấy sản phẩm</h2>
          <div className="row" style={{ marginTop: 10 }}>
            <Link className="btn" to="/admin/products">
              ← Quay lại danh sách
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="page">
      <div className="card">
        {/* Header */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 10,
          }}
        >
          <h2>Chi tiết sản phẩm</h2>

          <div className="row" style={{ gap: 10, alignItems: "center" }}>
            <span className="badge gray">Tồn kho: {form.stockQty}</span>

            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <label className="switch" title="Bật/Tắt hiển thị">
                <input
                  type="checkbox"
                  checked={form.status === "ACTIVE"}
                  onChange={toggleActive}
                  aria-label="Bật/Tắt hiển thị sản phẩm"
                />
                <span className="slider" />
              </label>

              <span
                className={statusClass(form.status)}
                style={{ textTransform: "none" }}
              >
                {statusText}
              </span>
            </div>

            <button
              className="btn ghost"
              onClick={() => nav("/admin/products")}
            >
              ⬅ Quay lại
            </button>
          </div>
        </div>

<div className="grid cols-3 input-group" style={{ gridColumn: "1 / 3" }}>
  <div className="group">
    <span>Tên sản phẩm</span>
    <input
      value={form.productName}
      onChange={(e) => set("productName", e.target.value)}
      placeholder="VD: Microsoft 365 Family"
    />
  </div>

  <div className="group">
    <span>Mã định danh sản phẩm</span>
    <input
      value={form.productCode}
      onChange={(e) => set("productCode", e.target.value)}
      placeholder="VD: OFF_365_FAM"
    />
  </div>

  <div className="group">
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
</div>


         {/* HÀNG 2.5: Loại + Danh mục + Nhãn (3 cột cùng hàng) */}
<div className="grid cols-2 input-group" style={{ gridColumn: "1 / 3" }}>

  {/* Danh mục */}
  <div className="group">
    <div className={`panel ${!showCats ? "collapsed" : ""}`}>
      <div className="panel-header" onClick={() => setShowCats((s) => !s)}>
        <h4>
          Danh mục sản phẩm{" "}
          <span style={{ fontSize: 12, color: "var(--muted)", marginLeft: 8 }}>
            ({catsList.length})
          </span>
        </h4>
        <div className="caret">▾</div>
      </div>
      {showCats && (
        <div className="panel-body">
          {catsList.map((c) => (
            <div key={c.categoryId} className="list-row">
              <div className="left">
                {c.thumbnailUrl ? <img src={c.thumbnailUrl} alt="" /> : <div style={{ width: 36, height: 36, background: "#f3f4f6", borderRadius: 6 }} />}
                <div>{c.categoryName}</div>
              </div>
              <div>
                <label className="switch">
                  <input
                    type="checkbox"
                    checked={(form.categoryIds || []).includes(c.categoryId)}
                    onChange={(e) => {
                      const prev = form.categoryIds || [];
                      set(
                        "categoryIds",
                        e.target.checked
                          ? Array.from(new Set([...prev, c.categoryId]))
                          : prev.filter((x) => x !== c.categoryId)
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

  {/* Nhãn */}
  <div className="group">
    <div className={`panel ${!showBadgesPanel ? "collapsed" : ""}`}>
      <div className="panel-header" onClick={() => setShowBadgesPanel((s) => !s)}>
        <h4>
          Nhãn sản phẩm{" "}
          <span style={{ fontSize: 12, color: "var(--muted)", marginLeft: 8 }}>
            ({badgesList.length})
          </span>
        </h4>
        <div className="caret">▾</div>
      </div>
      {showBadgesPanel && (
        <div className="panel-body">
          {badgesList.map((b) => {
            const color = getBadgeColor(b);
            const name = getBadgeName(b);
            const code = b?.badgeCode ?? name;
            return (
              <div key={code} className="list-row">
                <div className="left">
                  <span className="label-chip" style={{ backgroundColor: color, color: "#fff", padding: "4px 10px", borderRadius: 8, fontSize: 12 }}>
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
                        set(
                          "badgeCodes",
                          e.target.checked
                            ? Array.from(new Set([...prev, code]))
                            : prev.filter((x) => x !== code)
                        );
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
</div>

          {/* Ảnh sản phẩm */}
          <div className="group" style={{ gridColumn: "1 / 3" }}>
            <span>Ảnh sản phẩm</span>

            {images.length > 0 && (
              <div
                style={{
                  display: "flex",
                  gap: 10,
                  flexWrap: "wrap",
                  marginBottom: 8,
                }}
              >
                {images.map((img, idx) => (
                  <div
                    key={img.imageId || img.url}
                    style={{
                      border:
                        primaryIndex === idx
                          ? "2px solid var(--primary)"
                          : "1px solid var(--line)",
                      borderRadius: 10,
                      padding: 6,
                      background: "#fff",
                      position: "relative",
                    }}
                  >
                    <img
                      src={img.url}
                      alt=""
                      style={{
                        width: 160,
                        height: 110,
                        objectFit: "cover",
                        display: "block",
                        borderRadius: 6,
                      }}
                    />
                    <div
                      style={{
                        marginTop: 6,
                        display: "flex",
                        gap: 8,
                        alignItems: "center",
                      }}
                    >
                      <label
                        style={{
                          display: "flex",
                          alignItems: "center",
                          gap: 6,
                        }}
                      >
                        <input
                          type="radio"
                          name="primary"
                          checked={primaryIndex === idx}
                          onChange={() => setPrimaryIndex(idx)}
                        />
                        <span style={{ fontSize: 12 }}>
                          Chọn làm ảnh chính
                        </span>
                      </label>
                      <button
                        className="btn"
                        onClick={() => {
                          setDeleteImageIds((prev) =>
                            Array.from(
                              new Set([...prev, img.imageId])
                            )
                          );
                          setImages((prev) =>
                            prev.filter(
                              (x) => x.imageId !== img.imageId
                            )
                          );
                          setPrimaryIndex((pi) => {
                            if (pi == null) return pi;
                            if (pi === idx) return 0;
                            return pi > idx ? pi - 1 : pi;
                          });
                        }}
                      >
                        Remove
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}

            <div className="file-upload">
              <input
                id="prodImages"
                type="file"
                accept="image/*"
                multiple
                onChange={(e) => {
                  const files = Array.from(e.target.files || []);
                  if (!files.length) return;
                  setNewFiles(files);
                  const urls = files.map((f) => ({
                    name: f.name,
                    url: URL.createObjectURL(f),
                  }));
                  newPreviews.forEach((p) =>
                    URL.revokeObjectURL(p.url)
                  );
                  setNewPreviews(urls);
                  if (primaryIndex == null)
                    setPrimaryIndex(images.length > 0 ? 0 : 0);
                }}
              />
              <label
                htmlFor="prodImages"
                className="btn btn-upload"
              >
                Chọn ảnh
              </label>
              <span className="file-name">
                {newFiles.length
                  ? `${newFiles.length} ảnh đã chọn`
                  : "Chưa chọn ảnh"}
              </span>
            </div>
          </div>

          <div className="group" style={{ gridColumn: "1 / 3" }}>
            {newPreviews.length > 0 && (
              <div
                style={{
                  display: "flex",
                  gap: 10,
                  marginTop: 4,
                  flexWrap: "wrap",
                  alignItems: "stretch",
                }}
              >
                {newPreviews.map((p, nidx) => {
                  const overallIndex = images.length + nidx;
                  return (
                    <div
                      key={p.url}
                      style={{
                        border:
                          primaryIndex === overallIndex
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
                        <label
                          style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 6,
                          }}
                        >
                          <input
                            type="radio"
                            name="primary"
                            checked={primaryIndex === overallIndex}
                            onChange={() =>
                              setPrimaryIndex(overallIndex)
                            }
                          />
                          <span style={{ fontSize: 12 }}>
                            {overallIndex === 0
                              ? "Ảnh mặc định"
                              : `Ảnh ${overallIndex + 1}`}
                          </span>
                        </label>
                        <button
                          className="btn"
                          onClick={() => {
                            setNewFiles((prev) =>
                              prev.filter((_, i) => i !== nidx)
                            );
                            setNewPreviews((prev) => {
                              const toRevoke = prev[nidx];
                              if (toRevoke)
                                URL.revokeObjectURL(toRevoke.url);
                              return prev.filter(
                                (_, i) => i !== nidx
                              );
                            });
                            setPrimaryIndex((pi) => {
                              if (pi == null) return pi;
                              if (pi === overallIndex) return 0;
                              return pi > overallIndex ? pi - 1 : pi;
                            });
                          }}
                        >
                          Remove
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
          <div className="group" style={{ gridColumn:"1 / 3" }}>
 <VariantsPanel
  productId={productId}
  productName={form.productName}
  productCode={form.productCode}
/>
 <FaqsPanel productId={productId} />
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
