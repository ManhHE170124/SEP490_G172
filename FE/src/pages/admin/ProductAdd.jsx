import React from "react";
import { useNavigate } from "react-router-dom";
import { ProductApi } from "../../services/products";
import { CategoryApi } from "../../services/categories";
import { BadgesApi } from "../../services/badges";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./admin.css";
export default function ProductAdd() {
  const nav = useNavigate();
  // ===== Toasts =====
  const [toasts, setToasts] = React.useState([]);
  const removeToast = React.useCallback(
    (id) => setToasts((ts) => ts.filter((t) => t.id !== id)),
    []
  );
  const addToast = React.useCallback(
    (type, title, message) => {
      const id = `${Date.now()}-${Math.random()}`;
      setToasts((ts) => [...ts, { id, type, title, message }]);
      setTimeout(() => removeToast(id), 3500);
    },
    [removeToast]
  );

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
  const [autoDefaultOnImport, setAutoDefaultOnImport] = React.useState(true);

  // form (vẫn giữ salePrice trong state để payload có field, nhưng không render input)
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
  CategoryApi.list({ active: true })
    .then((data) => {
      const arr = Array.isArray(data) ? data
        : Array.isArray(data?.items)  ? data.items
        : Array.isArray(data?.data)   ? data.data
        : Array.isArray(data?.result) ? data.result
        : [];
      setCats(arr);
    })
    .catch((e) => {
      setCats([]); // fallback an toàn
      addToast("error","Lỗi tải danh mục", e?.response?.data?.message || e.message);
    });

  BadgesApi.list({ active: true })
    .then((data) => {
      const arr = Array.isArray(data) ? data
        : Array.isArray(data?.items)  ? data.items
        : Array.isArray(data?.data)   ? data.data
        : Array.isArray(data?.result) ? data.result
        : [];
      setBadges(arr);
    })
    .catch((e) => {
      setBadges([]); // fallback an toàn
      addToast("error","Lỗi tải nhãn", e?.response?.data?.message || e.message);
    });
}, [addToast]);


  React.useEffect(() => {
    return () => {
      previews.forEach((p) => URL.revokeObjectURL(p.url));
    };
  }, [previews]);

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }));

  const statusClass = (s) =>
    s === "ACTIVE"
      ? "badge green"
      : s === "OUT_OF_STOCK"
      ? "badge warning"
      : "badge gray";

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

  const toggleStatus = () => {
    const next = form.status === "ACTIVE" ? "INACTIVE" : "ACTIVE";
    set("status", next);
    addToast(
      "info",
      "Trạng thái sản phẩm",
      next === "ACTIVE" ? "Bật hiển thị" : "Tắt hiển thị"
    );
  };

  // ======= save =======
  const save = async (publish = true) => {
    if (!form.productName?.trim()) {
      addToast(
        "warning",
        "Thiếu tên sản phẩm",
        "Vui lòng nhập Tên sản phẩm"
      );
      return;
    }
    if (!form.productCode?.trim()) {
      addToast(
        "warning",
        "Thiếu mã định danh",
        "Vui lòng nhập Mã định danh sản phẩm"
      );
      return;
    }

    try {
      setSaving(true);
      const payload = { ...form, status: publish ? form.status : "INACTIVE" };
      payload.badgeCodes = payload.badgeCodes ?? [];
      let created;
 if (selectedFiles && selectedFiles.length > 0) {
   created = await ProductApi.createWithImages(payload, selectedFiles, primaryIndex);
 } else {
   created = await ProductApi.create(payload);
 }

      addToast(
        "success",
        publish ? "Đã tạo & xuất bản sản phẩm" : "Đã lưu nháp sản phẩm",
        publish
          ? "Sản phẩm đã hiển thị trên website"
          : "Bạn có thể xuất bản sau"
      );
if (created?.productId) {
   nav(`/admin/products/${created.productId}`);
 } else {
   nav("/admin/products");
 }
    } catch (e) {
      addToast(
        "error",
        "Tạo sản phẩm thất bại",
        e?.response?.data?.message || e.message
      );
    } finally {
      setSaving(false);
    }
  };
  
const catsList = React.useMemo(() => (
  Array.isArray(cats) ? cats
  : Array.isArray(cats?.items)  ? cats.items
  : Array.isArray(cats?.data)   ? cats.data
  : Array.isArray(cats?.result) ? cats.result
  : []
), [cats]);

const badgesList = React.useMemo(() => (
  Array.isArray(badges) ? badges
  : Array.isArray(badges?.items)  ? badges.items
  : Array.isArray(badges?.data)   ? badges.data
  : Array.isArray(badges?.result) ? badges.result
  : []
), [badges]);

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

        {/* GRID 2 cột */}
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

          {/* HÀNG 2: Loại + Bảo hành */}
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

          {/* HÀNG 3: Danh mục + Nhãn */}
          <div className="group" style={{ gridColumn: "1 / 2" }}>
            <div className={`panel ${!showCats ? "collapsed" : ""}`}>
              <div
                className="panel-header"
                onClick={() => setShowCats((s) => !s)}
              >
                <h4>
                  Danh mục sản phẩm{" "}
                  <span
                    style={{
                      fontSize: 12,
                      color: "var(--muted)",
                      marginLeft: 8,
                    }}
                  >
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
                            checked={(form.categoryIds || []).includes(
                              c.categoryId
                            )}
                            onChange={(e) => {
                              const prev = form.categoryIds || [];
                              if (e.target.checked)
                                set(
                                  "categoryIds",
                                  Array.from(
                                    new Set([...prev, c.categoryId])
                                  )
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
              <div
                className="panel-header"
                onClick={() => setShowBadgesPanel((s) => !s)}
              >
                <h4>
                  Nhãn sản phẩm{" "}
                  <span
                    style={{
                      fontSize: 12,
                      color: "var(--muted)",
                      marginLeft: 8,
                    }}
                  >
({badgesList.length})
                  </span>
                </h4>
                <div className="caret">▾</div>
              </div>

              {showBadgesPanel && (
                <div className="panel-body">
                  {badges.map((b) => {
                    const color =
                      b?.colorHex ||
                      b?.color ||
                      b?.colorhex ||
                      b?.ColorHex ||
                      "#1e40af";
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
                              checked={(form.badgeCodes || []).includes(
                                code
                              )}
                              onChange={(e) => {
                                const prev = form.badgeCodes || [];
                                if (e.target.checked)
                                  set(
                                    "badgeCodes",
                                    Array.from(new Set([...prev, code]))
                                  );
                                else
                                  set(
                                    "badgeCodes",
                                    prev.filter((x) => x !== code)
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
          {/* Ảnh sản phẩm */}
          <div className="group" style={{ gridColumn: "1 / 3" }}>
            <span>Ảnh sản phẩm</span>

            <div
              style={{
                display: "flex",
                gap: 16,
                alignItems: "flex-start",
                flexWrap: "wrap",
              }}
            >
              <div style={{ minWidth: 340, maxWidth: 420 }}>
                <div className="file-upload">
                  <input
                    id="prodImages"
                    type="file"
                    accept="image/*"
                    multiple
                    onChange={(e) => {
                      const files = Array.from(e.target.files || []);
                      const urls = files.map((f) => ({
                        name: f.name,
                        url: URL.createObjectURL(f),
                      }));
                      previews.forEach((p) => URL.revokeObjectURL(p.url));
                      setSelectedFiles(files);
                      setPreviews(urls);
                      setImagesName(
                        files.length > 0
                          ? `${files.length} ảnh đã chọn`
                          : "Chưa chọn ảnh"
                      );

                      const nextPrimary =
                        files.length === 0
                          ? 0
                          : autoDefaultOnImport
                          ? 0
                          : Math.min(
                              primaryIndex,
                              Math.max(0, files.length - 1)
                            );
                      setPrimaryIndex(nextPrimary);

                      if (files.length > 0) {
                        addToast(
                          "info",
                          "Đã chọn ảnh",
                          `${files.length} ảnh • ${files[0].name}`
                        );
                      }
                    }}
                  />
                  <label
                    htmlFor="prodImages"
                    className="btn btn-upload"
                  >
                    Chọn ảnh
                  </label>
                  <span className="file-name">
                    {imagesName || "Chưa chọn ảnh"}
                  </span>
                </div>

                <div
                  style={{
                    marginTop: 10,
                    display: "flex",
                    alignItems: "center",
                    gap: 8,
                  }}
                >
                  <label
                    className="switch"
                    title="Đặt ảnh mới làm ảnh mặc định"
                  >
                    <input
                      type="checkbox"
                      checked={autoDefaultOnImport}
                      onChange={(e) =>
                        setAutoDefaultOnImport(e.target.checked)
                      }
                      aria-label="Ảnh mới làm ảnh mặc định"
                    />
                    <span className="slider" />
                  </label>
                  <span
                    className="badge gray"
                    style={{ textTransform: "none" }}
                  >
                    Ảnh mới → mặc định
                  </span>
                </div>
              </div>

              <div style={{ flex: 1, minWidth: 360 }}>
                {previews.length > 0 && (
                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns:
                        "repeat(auto-fill, minmax(200px, 1fr))",
                      gap: 12,
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
                          borderRadius: 12,
                          background: "#fff",
                          padding: 10,
                        }}
                      >
                        <div
                          style={{
                            width: "100%",
                            height: 200,
                            borderRadius: 8,
                            background: "#fff",
                            border: "1px solid var(--line)",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                            overflow: "hidden",
                          }}
                          title={p.name}
                        >
                          <img
                            src={p.url}
                            alt={p.name}
                            style={{
                              width: "100%",
                              height: "100%",
                              objectFit: "contain",
                              display: "block",
                            }}
                          />
                        </div>

                        <div
                          style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 8,
                            marginTop: 8,
                            justifyContent: "space-between",
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
                              onChange={() => {
                                setPrimaryIndex(idx);
                                addToast(
                                  "info",
                                  "Đổi ảnh mặc định",
                                  p.name
                                );
                              }}
                            />
                            <span style={{ fontSize: 12 }}>
                              {primaryIndex === idx
                                ? "Ảnh mặc định"
                                : `Ảnh ${idx + 1}`}
                            </span>
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

        {/* ACTIONS */}
        <div className="row" style={{ marginTop: 12 }}>
          <button
            className="btn"
            disabled={saving}
            onClick={() => save(false)}
          >
            Lưu nháp
          </button>
          <button
            className="btn primary"
            disabled={saving}
            onClick={() => save(true)}
          >
            Lưu &amp; Xuất bản
          </button>
        </div>
      </div>

      <ToastContainer toasts={toasts} onRemove={removeToast} />
    </div>
  );
}
