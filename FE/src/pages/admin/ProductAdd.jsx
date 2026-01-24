// src/pages/admin/ProductAdd.jsx
import React from "react";
import { useNavigate } from "react-router-dom";
import ProductApi from "../../services/products";
import { CategoryApi } from "../../services/categories";
import { BadgesApi } from "../../services/badges";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./admin.css";

// ====== CONST & HELPERS ======
const MAX_PRODUCT_NAME = 100;
const MAX_PRODUCT_CODE = 50;

// Chuẩn hoá mã định danh: trim + bỏ dấu + chỉ A-Z0-9_ + uppercase
const normalizeProductCode = (raw) => {
  if (typeof raw !== "string") return "";
  let s = raw.trim();
  if (!s) return "";
  try {
    // bỏ dấu tiếng Việt
    s = s.normalize("NFD").replace(/[\u0300-\u036f]/g, "");
  } catch {
    // ignore nếu không hỗ trợ
  }
  // ký tự không phải chữ/số => _
  s = s.replace(/[^A-Za-z0-9]+/g, "_");
  // gộp nhiều _ và bỏ _ đầu/cuối
  s = s.replace(/_+/g, "_").replace(/^_+|_+$/g, "");
  return s.toUpperCase();
};

// Helpers dùng chung
const RequiredMark = () => (
  <span style={{ color: "#dc2626", marginLeft: 4 }}>*</span>
);

const FieldError = ({ message }) =>
  !message ? null : (
    <div
      style={{
        color: "#dc2626",
        fontSize: 12,
        marginTop: 4,
      }}
    >
      {message}
    </div>
  );

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

  // ===== Confirm dialog giống ProductsPage =====
  const [confirmDialog, setConfirmDialog] = React.useState(null);

  const openConfirm = React.useCallback(({ title, message, onConfirm }) => {
    setConfirmDialog({
      title,
      message,
      onConfirm: async () => {
        setConfirmDialog(null);
        await onConfirm?.();
      },
      onCancel: () => setConfirmDialog(null),
    });
  }, []);

  // ===== data sources =====
  const [cats, setCats] = React.useState([]);
  const [badges, setBadges] = React.useState([]);

  // UI panels – mặc định đóng
  const [showCats, setShowCats] = React.useState(false);
  const [showBadgesPanel, setShowBadgesPanel] = React.useState(false);

  // states
  const [saving, setSaving] = React.useState(false);

  // form (đúng với DTO ProductCreateDto)
  const [form, setForm] = React.useState({
    productCode: "",
    productName: "",
    productType: "PERSONAL_KEY",
    status: "ACTIVE",
    categoryIds: [],
    badges: [],
  });

  // errors
  const [errors, setErrors] = React.useState({});

  // Snapshot ban đầu để detect thay đổi
  const initialFormRef = React.useRef(null);
  React.useEffect(() => {
    if (!initialFormRef.current) {
      initialFormRef.current = form;
    }
    // chỉ lấy snapshot ở lần render đầu
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const isDirty = React.useMemo(() => {
    if (!initialFormRef.current) return false;
    return JSON.stringify(form) !== JSON.stringify(initialFormRef.current);
  }, [form]);

  // Cảnh báo khi reload/đóng tab nếu có thay đổi chưa lưu (bắt buộc dùng native dialog)
  React.useEffect(() => {
    const handler = (e) => {
      if (!isDirty) return;
      e.preventDefault();
      e.returnValue = "";
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [isDirty]);

  const handleBack = () => {
    if (!isDirty) {
      nav("/admin/products");
      return;
    }

    openConfirm({
      title: "Rời khỏi trang?",
      message:
        "Bạn có các thay đổi chưa lưu. Rời khỏi trang sẽ làm mất các thay đổi này.",
      onConfirm: () => {
        nav("/admin/products");
      },
    });
  };

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
        addToast(
          "error",
          "Lỗi tải danh mục",
          e?.response?.data?.message || e.message
        );
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
        addToast(
          "error",
          "Lỗi tải nhãn",
          e?.response?.data?.message || e.message
        );
      });
  }, [addToast]);

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
        return "Hiển thị";
      case "INACTIVE":
        return "Ẩn";
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

  const handleCodeBlur = () => {
    setForm((prev) => ({
      ...prev,
      productCode: normalizeProductCode(prev.productCode || ""),
    }));
  };

  // ===== validate =====
  const validate = () => {
    const e = {};
    const rawName = form.productName ?? "";
    const rawCode = form.productCode ?? "";
    const name = rawName.trim();
    const code = rawCode.trim();

    if (!name) {
      e.productName = "Tên sản phẩm là bắt buộc.";
    } else if (name.length > MAX_PRODUCT_NAME) {
      e.productName = `Tên sản phẩm không được vượt quá ${MAX_PRODUCT_NAME} ký tự.`;
    }

    if (!code) {
      e.productCode = "Mã định danh sản phẩm là bắt buộc.";
    } else {
      const normalized = normalizeProductCode(code);
      if (!normalized) {
        e.productCode =
          "Mã định danh sản phẩm không hợp lệ. Vui lòng chỉ dùng chữ, số và dấu gạch dưới.";
      } else if (normalized.length > MAX_PRODUCT_CODE) {
        e.productCode = `Mã định danh sản phẩm không được vượt quá ${MAX_PRODUCT_CODE} ký tự.`;
      }
    }

    setErrors(e);

    if (Object.keys(e).length > 0) {
      const detailMessage = Object.values(e).join(" ");

      addToast(
        "error",
        "Dữ liệu không hợp lệ",
        detailMessage || "Vui lòng kiểm tra lại các trường được đánh dấu đỏ."
      );
      return false;
    }
    return true;
  };

  // ======= save =======
  const save = async (publish = true) => {
    if (!validate()) return;

    try {
      setSaving(true);

      const trimmedName = (form.productName ?? "").trim();
      const normalizedCode = normalizeProductCode(form.productCode ?? "");

      const payload = {
        productCode: normalizedCode,
        productName: trimmedName,
        productType: form.productType,
        status: publish ? form.status : "INACTIVE", // Lưu nháp => INACTIVE
        categoryIds: form.categoryIds ?? [],
        badges: form.badges ?? [],
      };

      await ProductApi.create(payload);

      // Gửi thông tin toast sang trang danh sách qua sessionStorage
      const toastPayload = {
        type: "success",
        title: publish
          ? "Đã tạo & xuất bản sản phẩm"
          : "Đã lưu nháp sản phẩm",
        message: publish
          ? "Sản phẩm đã được tạo. Bạn có thể cấu hình biến thể / FAQ và theo dõi ở trang danh sách."
          : "Bản nháp sản phẩm đã được lưu. Bạn có thể xuất bản sau tại trang chi tiết sản phẩm.",
      };
      try {
        window.sessionStorage.setItem(
          "products:toast",
          JSON.stringify(toastPayload)
        );
      } catch {
        // ignore nếu sessionStorage bị chặn
      }

      nav("/admin/products");
    } catch (e) {
      const msg = e?.response?.data?.message || e.message;

      // Nếu trùng mã từ BE
      if (
        e?.response?.status === 409 ||
        (typeof msg === "string" &&
          msg.toLowerCase().includes("productcode already exists"))
      ) {
        setErrors((prev) => ({
          ...prev,
          productCode: "Mã định danh này đã tồn tại. Vui lòng chọn mã khác.",
        }));
      }

      // Nếu trùng tên
      if (
        typeof msg === "string" &&
        msg.toLowerCase().includes("productname already exists")
      ) {
        setErrors((prev) => ({
          ...prev,
          productName: "Tên sản phẩm này đã tồn tại. Vui lòng dùng tên khác.",
        }));
      }

      addToast(
        "error",
        "Tạo sản phẩm thất bại",
        msg || "Đã xảy ra lỗi khi tạo sản phẩm."
      );
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

  const selectedCatCount = (form.categoryIds || []).length;
  const selectedBadgeCount = (form.badges || []).length;

  return (
    <div className="page">
      <div className="card">
        {/* Header */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 16,
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

            <button className="btn ghost" onClick={handleBack}>
              ⬅ Quay lại
            </button>
          </div>
        </div>

        {/* GRID 2 cột */}
        <div className="grid cols-2 input-group">
          {/* HÀNG 1: Tên + Mã */}
          <div className="group" style={{ gridColumn: "1 / 2" }}>
            <span>
              Tên sản phẩm <RequiredMark />
            </span>
            <input
              value={form.productName}
              onChange={(e) => set("productName", e.target.value)}
              placeholder="VD: Microsoft 365 Family"
              maxLength={MAX_PRODUCT_NAME}
              className={errors.productName ? "input-error" : ""}
            />
            <FieldError message={errors.productName} />
          </div>
          <div className="group" style={{ gridColumn: "2 / 3" }}>
            <span>
              Mã định danh sản phẩm <RequiredMark />
            </span>
            <input
              value={form.productCode}
              onChange={(e) => set("productCode", e.target.value)}
              onBlur={handleCodeBlur}
              placeholder="VD: OFF_365_FAM"
              maxLength={MAX_PRODUCT_CODE}
              className={`mono ${errors.productCode ? "input-error" : ""}`}
            />
            <FieldError message={errors.productCode} />
          </div>

          <div className="group" style={{ gridColumn: "1 / 2" }}>
            <span>Loại</span>
            <select
              value={form.productType}
              onChange={(e) => set("productType", e.target.value)}
            >
              <option value="PERSONAL_KEY">Mã cá nhân</option>
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
                    ({selectedCatCount}/{catsList.length} đã chọn)
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
                            checked={(form.categoryIds || []).includes(
                              c.categoryId
                            )}
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
                    ({selectedBadgeCount}/{badgesList.length} đã chọn)
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
                                  set(
                                    "badges",
                                    Array.from(new Set([...prev, code]))
                                  );
                                else
                                  set(
                                    "badges",
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
        </div>

        {/* ACTIONS */}
        <div className="row" style={{ marginTop: 16 }}>
          <button className="btn" disabled={saving} onClick={() => save(false)}>
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

      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </div>
  );
}
