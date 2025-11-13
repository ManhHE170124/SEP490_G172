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

  // ===== data sources =====
  const [cats, setCats] = React.useState([]);
  const [badges, setBadges] = React.useState([]);

  // UI panels
  const [showCats, setShowCats] = React.useState(true);
  const [showBadgesPanel, setShowBadgesPanel] = React.useState(true);

  // states
  const [saving, setSaving] = React.useState(false);

  // form (đúng với DTO ProductCreateDto)
  const [form, setForm] = React.useState({
    productCode: "",
    productName: "",
    productType: "PERSONAL_KEY",
    status: "ACTIVE",
    categoryIds: [],
    // FE giữ 'badges' (mảng code) → service map sang BadgeCodes khi gọi API
    badges: [],
  });

  React.useEffect(() => {
    CategoryApi.list({ active: true })
      .then((data) => {
        const arr = Array.isArray(data)
          ? data
          : Array.isArray(data?.items)
          ? data.items
          : Array.isArray(data?.data)
          ? data.data
          : Array.isArray(data?.result)
          ? data.result
          : [];
        setCats(arr);
      })
      .catch((e) => {
        setCats([]);
        addToast("error", "Lỗi tải danh mục", e?.response?.data?.message || e.message);
      });

    BadgesApi.list({ active: true })
      .then((data) => {
        const arr = Array.isArray(data)
          ? data
          : Array.isArray(data?.items)
          ? data.items
          : Array.isArray(data?.data)
          ? data.data
          : Array.isArray(data?.result)
          ? data.result
          : [];
        setBadges(arr);
      })
      .catch((e) => {
        setBadges([]);
        addToast("error", "Lỗi tải nhãn", e?.response?.data?.message || e.message);
      });
  }, [addToast]);

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }));

  const statusClass = (s) =>
    s === "ACTIVE" ? "badge green" : s === "OUT_OF_STOCK" ? "badge warning" : "badge gray";

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
    addToast("info", "Trạng thái sản phẩm", next === "ACTIVE" ? "Bật hiển thị" : "Tắt hiển thị");
  };

  // ======= save =======
  const save = async (publish = true) => {
    if (!form.productName?.trim()) {
      addToast("warning", "Thiếu tên sản phẩm", "Vui lòng nhập Tên sản phẩm");
      return;
    }
    if (!form.productCode?.trim()) {
      addToast("warning", "Thiếu mã định danh", "Vui lòng nhập Mã định danh sản phẩm");
      return;
    }

    try {
      setSaving(true);
      const payload = {
        productCode: form.productCode.trim(),
        productName: form.productName.trim(),
        productType: form.productType,
        status: publish ? form.status : "INACTIVE",
        categoryIds: form.categoryIds ?? [],
        badges: form.badges ?? [], // FE → service map sang badgeCodes
      };

      const created = await ProductApi.create(payload);

      addToast(
        "success",
        publish ? "Đã tạo & xuất bản sản phẩm" : "Đã lưu nháp sản phẩm",
        publish ? "Sản phẩm đã hiển thị trên website" : "Bạn có thể xuất bản sau"
      );

      if (created?.productId) {
        nav(`/admin/products/${created.productId}`);
      } else {
        nav("/admin/products");
      }
    } catch (e) {
      addToast("error", "Tạo sản phẩm thất bại", e?.response?.data?.message || e.message);
    } finally {
      setSaving(false);
    }
  };

  const catsList = React.useMemo(
    () =>
      Array.isArray(cats)
        ? cats
        : Array.isArray(cats?.items)
        ? cats.items
        : Array.isArray(cats?.data)
        ? cats.data
        : Array.isArray(cats?.result)
        ? cats.result
        : [],
    [cats]
  );

  const badgesList = React.useMemo(
    () =>
      Array.isArray(badges)
        ? badges
        : Array.isArray(badges?.items)
        ? badges.items
        : Array.isArray(badges?.data)
        ? badges.data
        : Array.isArray(badges?.result)
        ? badges.result
        : [],
    [badges]
  );

  const badgeName = (b) =>
    b?.displayName ||
    b?.badgeName ||
    b?.name ||
    b?.BadgeDisplayName ||
    b?.BadgeName ||
    b?.badgeCode ||
    "";

  const badgeColor = (b) =>
    b?.colorHex || b?.color || b?.colorhex || b?.ColorHex || "#1e40af";

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
              <span className={statusClass(form.status)} style={{ textTransform: "none" }}>
                {statusText}
              </span>
            </div>

            <button className="btn ghost" onClick={() => nav("/admin/products")}>
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
                              if (e.target.checked)
                                set("categoryIds", Array.from(new Set([...prev, c.categoryId])));
                              else set("categoryIds", prev.filter((x) => x !== c.categoryId));
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
                    ({badgesList.length})
                  </span>
                </h4>
                <div className="caret">▾</div>
              </div>

              {showBadgesPanel && (
                <div className="panel-body">
                  {badgesList.map((b) => {
                    const color = badgeColor(b);
                    const name = badgeName(b);
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
                              checked={(form.badges || []).includes(code)}
                              onChange={(e) => {
                                const prev = form.badges || [];
                                if (e.target.checked)
                                  set("badges", Array.from(new Set([...prev, code])));
                                else set("badges", prev.filter((x) => x !== code));
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

      <ToastContainer toasts={toasts} onRemove={removeToast} />
    </div>
  );
}
