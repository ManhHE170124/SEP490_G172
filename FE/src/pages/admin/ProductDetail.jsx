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

  const catsList = React.useMemo(() => normalizeList(cats), [cats]);
  const badgesList = React.useMemo(() => normalizeList(badges), [badges]);

  // form (đúng với DTO BE: ProductDetailDto / ProductUpdateDto)
  const [form, setForm] = React.useState({
    productCode: "",
    productName: "",
    productType: "PERSONAL_KEY",
    status: "INACTIVE",
    // FE dùng 'badges' (mảng code) -> service sẽ map sang badgeCodes khi gọi API
    badges: [],
    categoryIds: [],
  });

  // để render tổng tồn kho từ variants
  const [variants, setVariants] = React.useState([]);

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
        ProductApi.get(productId),              // normalizeDetail → badges, variants, categoryIds, status...
        CategoryApi.list({ active: true }),
        BadgesApi.list({ active: true }),
      ]);

      setCats(normalizeList(catList));
      setBadges(normalizeList(badgeList));

      if (!dto) {
        setNotFound(true);
        return;
      }

      setForm({
        productCode: dto.productCode || "",
        productName: dto.productName || "",
        productType: dto.productType || "PERSONAL_KEY",
        status: dto.status || "INACTIVE",
        badges: dto.badges ?? [],            // từ normalizeDetail
        categoryIds: dto.categoryIds ?? [],
      });

      setVariants(Array.isArray(dto.variants) ? dto.variants : []);
    } catch (e) {
      setNotFound(true);
    } finally {
      setLoading(false);
    }
  }, [productId]);

  React.useEffect(() => {
    load();
  }, [load]);

  // save
  const save = async () => {
    try {
      setSaving(true);
      // Service ProductApi.update sẽ tự map { badges } -> { badgeCodes } cho BE
      const payload = {
        productName: form.productName,
        productType: form.productType,
        status: form.status,
        badges: form.badges ?? [],
        categoryIds: form.categoryIds ?? [],
      };
      await ProductApi.update(productId, payload);
      alert("Đã lưu thay đổi sản phẩm.");
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
    try {
      // chỉ dùng endpoint toggle theo BE
      const res = await ProductApi.toggle(productId);
      const next = (res?.status || res?.Status || "").toUpperCase();
      if (next) set("status", next);
    } catch (err) {
      alert(err?.response?.data?.message || err.message);
    }
  };

  const totalStock = React.useMemo(
    () => (variants || []).reduce((sum, v) => sum + (Number(v.stockQty) || 0), 0),
    [variants]
  );

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
            <span className="badge gray">Tồn kho: {totalStock}</span>

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

              <span className={statusClass(form.status)} style={{ textTransform: "none" }}>
                {statusText}
              </span>
            </div>

            <button className="btn ghost" onClick={() => nav("/admin/products")}>
              ⬅ Quay lại
            </button>
          </div>
        </div>

        {/* Hàng thông tin cơ bản */}
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
              disabled
              title="Không cho phép đổi mã sản phẩm"
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

        {/* Danh mục + Nhãn */}
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
                          <span
                            className="label-chip"
                            style={{ backgroundColor: color, color: "#fff", padding: "4px 10px", borderRadius: 8, fontSize: 12 }}
                          >
                            {name}
                          </span>
                        </div>
                        <div>
                          <label className="switch">
                            <input
                              type="checkbox"
                              checked={(form.badges || []).includes(code)}
                              onChange={(e) => {
                                const prev = form.badges || [];
                                set(
                                  "badges",
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

        {/* Variants & FAQs */}
        <div className="group" style={{ gridColumn: "1 / 3" }}>
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
