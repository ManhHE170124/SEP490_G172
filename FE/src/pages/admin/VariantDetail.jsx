// src/pages/admin/VariantDetail.jsx
import React from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { ProductVariantsApi } from "../../services/productVariants";
import ProductSectionsPanel from "../admin/ProductSectionsPanel";
import "../admin/admin.css";

// Helper: chuẩn hoá URL thumbnail (bỏ query, cắt max length)
const sanitizeThumbnail = (url, max = 255) => {
  if (!url) return null;
  const noQuery = url.split("?")[0];
  return noQuery.length > max ? noQuery.slice(0, max) : noQuery;
};

export default function VariantDetail() {
  const { id: productId, variantId } = useParams();
  const nav = useNavigate();

  const [loading, setLoading] = React.useState(true);
  const [notFound, setNotFound] = React.useState(false);
  const [saving, setSaving] = React.useState(false);

  const [variant, setVariant] = React.useState(null);

  // Upload preview/state giống VariantsPanel
  const [thumbPreview, setThumbPreview] = React.useState(null); // DataURL/URL để xem trước
  const [thumbUrl, setThumbUrl] = React.useState(null);         // URL sau khi upload (gửi lên BE)
  const fileInputRef = React.useRef(null);

  const statusClass = (s) =>
    String(s).toUpperCase() === "ACTIVE"
      ? "badge green"
      : String(s).toUpperCase() === "OUT_OF_STOCK"
      ? "badge warning"
      : "badge gray";

  const statusText = (s) =>
    (
      {
        ACTIVE: "Đang hiển thị",
        INACTIVE: "Đang ẩn",
        OUT_OF_STOCK: "Hết hàng",
      }[String(s || "").toUpperCase()] || s || "-"
    );

  const load = React.useCallback(async () => {
    try {
      setLoading(true);
      setNotFound(false);

      const v = await ProductVariantsApi.get(productId, variantId);
      if (!v) {
        setNotFound(true);
        return;
      }

      const serverThumb = v.thumbnail ?? v.Thumbnail ?? null;

      setVariant({
        variantId: v.variantId ?? v.VariantId ?? variantId,
        productId: v.productId ?? v.ProductId ?? productId,
        variantCode: v.variantCode ?? v.VariantCode ?? "",
        title: v.title ?? v.Title ?? "",
        durationDays: v.durationDays ?? v.DurationDays ?? 0,
        warrantyDays: v.warrantyDays ?? v.WarrantyDays ?? 0,
        stockQty: v.stockQty ?? v.StockQty ?? 0,
        thumbnail: serverThumb,
        status: (v.status ?? v.Status ?? "INACTIVE").toString().toUpperCase(),
      });

      // set preview & URL để màn detail có thể giữ / đổi / xoá ảnh
      setThumbPreview(serverThumb);
      setThumbUrl(serverThumb);
    } catch (e) {
      setNotFound(true);
    } finally {
      setLoading(false);
    }
  }, [productId, variantId]);

  React.useEffect(() => {
    load();
  }, [load]);

  const setVar = (k, val) =>
    setVariant((s) => (s ? { ...s, [k]: val } : s));

  const toggleActive = async () => {
    try {
      if (!variant) return;
      // gọi API toggle và dùng Status trả về để sync đúng logic BE
      const payload = await ProductVariantsApi.toggle(
        productId,
        variant.variantId
      );
      const next = (payload?.Status || payload?.status || "").toUpperCase();
      if (next) {
        setVar("status", next);
      }
    } catch (e) {
      alert(e?.response?.data?.message || e.message);
    }
  };

  // ===== Helpers upload ảnh (giống VariantsPanel) =====
  const urlToFile = async (url) => {
    const res = await fetch(url);
    const blob = await res.blob();
    return new File([blob], "image." + (blob.type.split("/")[1] || "png"), {
      type: blob.type,
    });
  };

  const handleLocalPreview = (file) => {
    const reader = new FileReader();
    reader.onload = (ev) => setThumbPreview(ev.target.result);
    reader.readAsDataURL(file);
  };

  const uploadThumbnailFile = async (file) => {
    // xem trước ngay (local)
    handleLocalPreview(file);
    // upload lên server (Cloudinary qua controller)
    const up = await ProductVariantsApi.uploadImage(file);
    const imageUrl =
      up?.path || up?.Path || up?.url || up?.Url || (typeof up === "string" ? up : null);
    if (!imageUrl) throw new Error("Không lấy được URL ảnh sau khi upload.");
    setThumbUrl(imageUrl);
    setVar("thumbnail", imageUrl);
  };

  const onPickThumb = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      await uploadThumbnailFile(file);
    } catch (err) {
      alert(err?.message || "Upload ảnh thất bại.");
      setThumbPreview(null);
      setThumbUrl(null);
      setVar("thumbnail", null);
    } finally {
      e.target.value = "";
    }
  };

  const onDrop = async (e) => {
    e.preventDefault();
    e.stopPropagation();
    const dt = e.dataTransfer;
    if (!dt) return;
    if (dt.files && dt.files[0]) {
      try {
        await uploadThumbnailFile(dt.files[0]);
      } catch (err) {
        alert(err?.message || "Upload ảnh thất bại.");
      }
      return;
    }
    // Support paste URL vào drop
    const text = dt.getData("text/uri-list") || dt.getData("text/plain");
    if (text && /^https?:\/\//i.test(text)) {
      try {
        const f = await urlToFile(text);
        await uploadThumbnailFile(f);
      } catch {
        alert("Không thể tải ảnh từ URL này.");
      }
    }
  };

  const onPaste = async (e) => {
    const items = Array.from(e.clipboardData?.items || []);
    for (const it of items) {
      if (it.kind === "file" && it.type.startsWith("image/")) {
        const f = it.getAsFile();
        if (f) {
          try {
            await uploadThumbnailFile(f);
          } catch {
            alert("Upload ảnh thất bại.");
          }
          break;
        }
      } else if (it.kind === "string" && it.type === "text/plain") {
        it.getAsString(async (text) => {
          if (/^https?:\/\/.+\.(jpg|jpeg|png|gif|webp)$/i.test(text)) {
            try {
              const f = await urlToFile(text);
              await uploadThumbnailFile(f);
            } catch {
              alert("Không thể tải ảnh từ URL này.");
            }
          }
        });
      }
    }
  };

  const clearThumb = () => {
    setThumbPreview(null);
    setThumbUrl(null);
    setVar("thumbnail", null);
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  const save = async () => {
    if (!variant) return;
    try {
      setSaving(true);

      // stockQty:
      //   - Hiện tại được coi là giá trị tính từ tài khoản/key của biến thể (ở BE / service khác).
      //   - Màn hình này chỉ HIỂN THỊ, không cho chỉnh.
      //   - Khi save, vẫn gửi lại giá trị hiện có để BE không bị reset về 0.
      const dto = {
        title: variant.title?.trim(),
        durationDays:
          variant.durationDays === "" || variant.durationDays == null
            ? null
            : Number(variant.durationDays),
        stockQty: Number(variant.stockQty ?? 0),
        warrantyDays:
          variant.warrantyDays === "" || variant.warrantyDays == null
            ? null
            : Number(variant.warrantyDays),
        thumbnail: sanitizeThumbnail(thumbUrl) || null,
        status: variant.status,
      };

      await ProductVariantsApi.update(productId, variant.variantId, dto);
      alert("Đã lưu biến thể.");
      await load();
    } catch (e) {
      alert(e?.response?.data?.message || e.message);
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="page">
        <div className="card">Đang tải chi tiết biến thể…</div>
      </div>
    );
  }
  if (notFound || !variant) {
    return (
      <div className="page">
        <div className="card">
          <h2>Không tìm thấy biến thể</h2>
          <div className="row" style={{ marginTop: 10 }}>
            <Link className="btn" to={`/admin/products/${productId}`}>
              ← Quay lại sản phẩm
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="page">
      <div className="card">
        {/* Header (đồng bộ style với ProductDetail) */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 10,
          }}
        >
          <h2>Chi tiết biến thể</h2>

          <div className="row" style={{ gap: 10, alignItems: "center" }}>
            {/* Tồn kho: hiển thị, không cho chỉnh trên màn hình này */}
            <span className="badge gray">
              Tồn kho: {variant.stockQty ?? 0}
            </span>

            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <label className="switch" title="Bật/Tắt hiển thị biến thể">
                <input
                  type="checkbox"
                  checked={variant.status === "ACTIVE"}
                  onChange={toggleActive}
                  aria-label="Bật/Tắt hiển thị biến thể"
                />
                <span className="slider" />
              </label>

              <span
                className={statusClass(variant.status)}
                style={{ textTransform: "none" }}
              >
                {statusText(variant.status)}
              </span>
            </div>

            <button
              className="btn ghost"
              onClick={() => nav(`/admin/products/${productId}`)}
            >
              ⬅ Quay lại SP
            </button>
          </div>
        </div>

        {/* Form cơ bản – format giống ProductDetail */}
        <div className="grid cols-3 input-group" style={{ gridColumn: "1 / 3" }}>
          <div className="group">
            <span>Tên biến thể</span>
            <input
              value={variant.title || ""}
              onChange={(e) => setVar("title", e.target.value)}
              placeholder="VD: Gói 12 tháng"
            />
          </div>
          <div className="group">
            <span>Mã biến thể</span>
            <input
              value={variant.variantCode || ""}
              onChange={(e) => setVar("variantCode", e.target.value)}
              placeholder="VD: VAR_12M"
              // Lưu ý: hiện tại BE Update DTO không có VariantCode,
              // nên nếu muốn update mã từ đây thì phải sửa DTO + controller.
            />
          </div>
          <div className="group">
            <span>Thời lượng (ngày)</span>
            <input
              type="number"
              min={0}
              step={1}
              value={variant.durationDays ?? 0}
              onChange={(e) => setVar("durationDays", e.target.value)}
            />
          </div>
        </div>

        <div className="grid cols-3 input-group" style={{ gridColumn: "1 / 3" }}>
          <div className="group">
            <span>Bảo hành (ngày)</span>
            <input
              type="number"
              min={0}
              step={1}
              value={variant.warrantyDays ?? 0}
              onChange={(e) => setVar("warrantyDays", e.target.value)}
            />
          </div>
        </div>

        {/* Upload thumbnail giống modal Thêm biến thể */}
        <div className="input-group" style={{ gridColumn: "1 / 3" }}>
          <div className="group">
            <span>Ảnh biến thể (thumbnail)</span>

            <input
              ref={fileInputRef}
              type="file"
              accept="image/*"
              style={{ display: "none" }}
              onChange={onPickThumb}
            />

            <div
              className={`cep-featured-image-upload ${
                thumbPreview ? "has-image" : ""
              }`}
              onClick={() => fileInputRef.current?.click()}
              onDrop={onDrop}
              onDragOver={(e) => {
                e.preventDefault();
                e.stopPropagation();
              }}
              onPaste={onPaste}
              tabIndex={0}
              role="button"
              style={{
                outline: "none",
                border: "1px dashed var(--line)",
                borderRadius: 10,
                padding: 12,
                textAlign: "center",
                background: "#fafafa",
              }}
            >
              {thumbPreview ? (
                <img
                  src={thumbPreview}
                  alt="thumbnail"
                  style={{
                    width: "100%",
                    maxHeight: 220,
                    objectFit: "contain",
                    borderRadius: 8,
                  }}
                />
              ) : (
                <div>
                  <div>Kéo thả ảnh vào đây</div>
                  <div>hoặc</div>
                  <div>Click để chọn ảnh</div>
                  <div>hoặc</div>
                  <div>Dán URL ảnh (Ctrl+V)</div>
                </div>
              )}
            </div>

            {thumbPreview && (
              <button
                type="button"
                className="btn"
                style={{ marginTop: 8 }}
                onClick={clearThumb}
              >
                Xoá ảnh
              </button>
            )}
          </div>
        </div>

        {/* Sections của biến thể */}
        <div
          className="group"
          style={{ gridColumn: "1 / 3", marginTop: 12 }}
        >
          <ProductSectionsPanel
            productId={productId}
            variantId={variant.variantId}
          />
        </div>

        {/* Actions */}
        <div className="row" style={{ marginTop: 12 }}>
          <button className="btn primary" disabled={saving} onClick={save}>
            {saving ? "Đang lưu…" : "Lưu thay đổi"}
          </button>
        </div>
      </div>
    </div>
  );
}
