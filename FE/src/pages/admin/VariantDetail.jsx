// src/pages/admin/VariantDetail.jsx
import React from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { ProductApi } from "../../services/products";
import { ProductVariantsApi } from "../../services/productVariants";
import ProductSectionsPanel from "../admin/ProductSectionsPanel";
import "../admin/admin.css";

const fmtMoney = (n) =>
  typeof n === "number"
    ? n.toLocaleString("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 })
    : "-";

export default function VariantDetail() {
  const { id: productId, variantId } = useParams();
  const nav = useNavigate();

  const [loading, setLoading] = React.useState(true);
  const [notFound, setNotFound] = React.useState(false);
  const [saving, setSaving] = React.useState(false);

  const [product, setProduct] = React.useState(null);
  const [variant, setVariant] = React.useState(null);

  const statusClass = (s) =>
    String(s).toUpperCase() === "ACTIVE"
      ? "badge green"
      : String(s).toUpperCase() === "OUT_OF_STOCK"
      ? "badge warning"
      : "badge gray";

  const statusText = (s) =>
    ({ ACTIVE: "Đang hiển thị", INACTIVE: "Đang ẩn", OUT_OF_STOCK: "Hết hàng" }[
      String(s || "").toUpperCase()
    ] || s || "-");

  const load = React.useCallback(async () => {
    try {
      setLoading(true);
      setNotFound(false);
      const [p, v] = await Promise.all([
        ProductApi.get(productId),
        ProductVariantsApi.get(productId, variantId),
      ]);
      if (!p || !v) {
        setNotFound(true);
        return;
      }
      setProduct(p);
      setVariant({
        variantId: v.variantId ?? v.VariantId ?? Number(variantId),
        variantCode: v.variantCode ?? v.VariantCode ?? "",
        title: v.title ?? v.Title ?? "",
        durationDays: v.durationDays ?? v.DurationDays ?? 0,
        warrantyDays: v.warrantyDays ?? v.WarrantyDays ?? 0,
        price: v.price ?? v.Price ?? 0,
        originalPrice: v.originalPrice ?? v.OriginalPrice ?? null,
        stockQty: v.stockQty ?? v.StockQty ?? 0,
        status: (v.status ?? v.Status ?? "INACTIVE").toString().toUpperCase(),
      });
    } catch (e) {
      setNotFound(true);
    } finally {
      setLoading(false);
    }
  }, [productId, variantId]);

  React.useEffect(() => { load(); }, [load]);

  const setVar = (k, val) => setVariant((s) => ({ ...s, [k]: val }));

  const toggleActive = async () => {
    try {
      const next =
        variant.status === "ACTIVE" ? "INACTIVE" :
        variant.status === "INACTIVE" ? "ACTIVE" : "ACTIVE";
      await ProductVariantsApi.toggle(productId, variant.variantId);
      setVar("status", next);
    } catch (e) {
      alert(e?.response?.data?.message || e.message);
    }
  };

  const save = async () => {
    try {
      setSaving(true);
      const dto = {
        variantCode: variant.variantCode?.trim() || undefined,
        title: variant.title?.trim(),
        durationDays: Number(variant.durationDays || 0),
        warrantyDays: Number(variant.warrantyDays || 0),
        price: Number(variant.price || 0),
        originalPrice:
          variant.originalPrice === "" || variant.originalPrice == null
            ? null
            : Number(variant.originalPrice),
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
  if (notFound) {
    return (
      <div className="page">
        <div className="card">
          <h2>Không tìm thấy biến thể</h2>
          <div className="row" style={{ marginTop: 10 }}>
            <Link className="btn" to={`/admin/products/${productId}`}>← Quay lại sản phẩm</Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="page">
      <div className="card">
        {/* Header */}
        <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center", marginBottom:10 }}>
          <h2>
            Biến thể: {variant.title || "(Chưa đặt tên)"}{" "}
            <span className="muted" style={{ fontSize:14, marginLeft:8 }}>
              — {product?.productName} ({product?.productCode})
            </span>
          </h2>
          <div className="row" style={{ gap:10, alignItems:"center" }}>
            <label className="switch" title="Bật/Tắt hiển thị biến thể">
              <input
                type="checkbox"
                checked={variant.status === "ACTIVE"}
                onChange={toggleActive}
              />
              <span className="slider" />
            </label>
            <span className={statusClass(variant.status)} style={{ textTransform: "none" }}>
              {statusText(variant.status)}
            </span>
            <button className="btn ghost" onClick={() => nav(`/admin/products/${productId}`)}>⬅ Quay lại SP</button>
          </div>
        </div>

        {/* Form cơ bản */}
        <div className="grid cols-3 input-group" style={{ gridColumn:"1 / 3" }}>
          <div className="group">
            <span>Tên biến thể</span>
            <input value={variant.title} onChange={(e)=>setVar("title", e.target.value)} />
          </div>
          <div className="group">
            <span>Mã biến thể</span>
            <input value={variant.variantCode || ""} onChange={(e)=>setVar("variantCode", e.target.value)} />
          </div>
          <div className="group">
            <span>Thời lượng (ngày)</span>
            <input type="number" min={0} step={1} value={variant.durationDays}
              onChange={(e)=>setVar("durationDays", e.target.value)} />
          </div>
        </div>

        <div className="grid cols-3 input-group" style={{ gridColumn:"1 / 3" }}>
          <div className="group">
            <span>Bảo hành (ngày)</span>
            <input type="number" min={0} step={1} value={variant.warrantyDays}
              onChange={(e)=>setVar("warrantyDays", e.target.value)} />
          </div>
          <div className="group">
            <span>Giá bán</span>
            <input type="number" min={0} step={1000} value={variant.price}
              onChange={(e)=>setVar("price", e.target.value)} />
            <div className="muted" style={{ fontSize:12, marginTop:4 }}>{fmtMoney(Number(variant.price||0))}</div>
          </div>
          <div className="group">
            <span>Giá gốc</span>
            <input type="number" min={0} step={1000} value={variant.originalPrice ?? "" }
              onChange={(e)=>setVar("originalPrice", e.target.value)} />
            <div className="muted" style={{ fontSize:12, marginTop:4 }}>
              {variant.originalPrice != null && variant.originalPrice !== "" ? fmtMoney(Number(variant.originalPrice)) : "—"}
            </div>
          </div>
        </div>

        {/* Stock chỉ hiển thị */}
        <div className="group" style={{ maxWidth: 280 }}>
          <span>Tồn kho</span>
          <input value={variant.stockQty ?? 0} readOnly />
        </div>

        {/* Sections cho biến thể */}
        <div className="group" style={{ gridColumn:"1 / 3", marginTop: 12 }}>
          <ProductSectionsPanel productId={productId} variantId={variant.variantId} />
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
