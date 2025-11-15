// src/pages/admin/ProductDetail.jsx
import React from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { ProductApi } from "../../services/products";
import { CategoryApi } from "../../services/categories";
import { BadgesApi } from "../../services/badges";
import VariantsPanel from "../admin/VariantsPanel";
import FaqsPanel from "../admin/FaqsPanel";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./admin.css";

// ====== CONST & HELPERS ======
const MAX_PRODUCT_NAME = 200; // TODO: chỉnh lại cho khớp DB (VD: 150)
const MAX_PRODUCT_CODE = 64;

// Chuẩn hoá mã định danh: trim + bỏ dấu + chỉ A-Z0-9_ + uppercase
const normalizeProductCode = (raw) => {
  if (typeof raw !== "string") return "";
  let s = raw.trim();
  if (!s) return "";
  try {
    s = s.normalize("NFD").replace(/[\u0300-\u036f]/g, "");
  } catch {
    // ignore
  }
  s = s.replace(/[^A-Za-z0-9]+/g, "_");
  s = s.replace(/_+/g, "_").replace(/^_+|_+$/g, "");
  return s.toUpperCase();
};

// Helpers: Required + FieldError
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
  // panels mặc định đóng theo yêu cầu
  const [showCats, setShowCats] = React.useState(false);
  const [showBadgesPanel, setShowBadgesPanel] = React.useState(false);

  const catsList = React.useMemo(() => normalizeList(cats), [cats]);
  const badgesList = React.useMemo(() => normalizeList(badges), [badges]);

  // form (đúng với DTO BE: ProductDetailDto / ProductUpdateDto)
  const [form, setForm] = React.useState({
    productCode: "",
    productName: "",
    productType: "PERSONAL_KEY",
    status: "INACTIVE",
    badges: [],
    categoryIds: [],
  });

  // lỗi validation
  const [errors, setErrors] = React.useState({});

  // để render tổng tồn kho từ variants
  const [variants, setVariants] = React.useState([]);

  // trạng thái để biết có biến thể/FAQ hay chưa
  const [hasVariants, setHasVariants] = React.useState(false);
  const [hasFaqs, setHasFaqs] = React.useState(false);
  const lockIdentity = hasVariants || hasFaqs; // có 1 cái là khóa sửa tên + mã

  // Ẩn/hiện panel biến thể + FAQ khi user đang sửa form (debounce 3s)
  const [showSubPanels, setShowSubPanels] = React.useState(true);
  const editTimerRef = React.useRef(null);

  const markEditing = React.useCallback(() => {
    setShowSubPanels(false);
    if (editTimerRef.current) {
      clearTimeout(editTimerRef.current);
    }
    editTimerRef.current = setTimeout(() => {
      setShowSubPanels(true);
    }, 1500); // 3 giây sau khi ngừng sửa thì mới hiện lại sub-panel
  }, []);

  React.useEffect(() => {
    return () => {
      if (editTimerRef.current) {
        clearTimeout(editTimerRef.current);
      }
    };
  }, []);

  // Toast
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

  // Confirm dialog giống ProductsPage
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

  // snapshot form ban đầu để detect dirty
  const initialFormRef = React.useRef(null);

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
    s === "ACTIVE"
      ? "badge green"
      : s === "OUT_OF_STOCK"
      ? "badge warning"
      : "badge gray";

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

      const nextForm = {
        productCode: dto.productCode || "",
        productName: dto.productName || "",
        productType: dto.productType || "PERSONAL_KEY",
        status: dto.status || "INACTIVE",
        badges: dto.badges ?? [],
        categoryIds: dto.categoryIds ?? [],
      };

      setForm(nextForm);
      setVariants(Array.isArray(dto.variants) ? dto.variants : []);

      // kiểm tra xem đã có biến thể / FAQ chưa để khóa sửa tên + mã
      setHasVariants((dto.variants ?? []).length > 0);
      setHasFaqs((dto.faqs ?? []).length > 0);

      setErrors({});

      // snapshot lần đầu sau khi load xong
      if (!initialFormRef.current) {
        initialFormRef.current = nextForm;
      }
    } catch (e) {
      setNotFound(true);
    } finally {
      setLoading(false);
    }
  }, [productId]);

  React.useEffect(() => {
    load();
  }, [load]);

  const isDirty = React.useMemo(() => {
    if (!initialFormRef.current) return false;
    return JSON.stringify(form) !== JSON.stringify(initialFormRef.current);
  }, [form]);

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

  const handleCodeBlur = () => {
    if (lockIdentity) return;
    setForm((prev) => ({
      ...prev,
      productCode: normalizeProductCode(prev.productCode || ""),
    }));
  };

  // validate (edit: cả tên + mã đều bắt buộc + giới hạn độ dài + chuẩn hoá mã)
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

  // save
  const save = async () => {
    if (!validate()) return;

    try {
      setSaving(true);
      const trimmedName = (form.productName ?? "").trim();
      const normalizedCode = normalizeProductCode(form.productCode ?? "");

      const payload = {
        productName: trimmedName,
        productType: form.productType,
        status: form.status,
        badges: form.badges ?? [],
        categoryIds: form.categoryIds ?? [],
        productCode: normalizedCode,
      };
      await ProductApi.update(productId, payload);

      // Sau khi update xong → quay lại danh sách hiện tại (giữ bộ lọc & trang)
      // và hiển thị toast qua sessionStorage
      try {
        window.sessionStorage.setItem(
          "products:toast",
          JSON.stringify({
            type: "success",
            title: "Cập nhật sản phẩm thành công",
            message: "Thông tin sản phẩm đã được lưu.",
          })
        );
      } catch {
        // ignore
      }

      nav("/admin/products");
    } catch (e) {
      const msg = e?.response?.data?.message || e.message;

      // Trùng mã
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

      // Trùng tên
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
        "Cập nhật sản phẩm thất bại",
        msg || "Đã xảy ra lỗi khi lưu sản phẩm."
      );
    } finally {
      setSaving(false);
    }
  };

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

  const toggleActive = async () => {
    markEditing();
    try {
      const res = await ProductApi.toggle(productId);
      const next = (res?.status || res?.Status || "").toUpperCase();
      if (next) set("status", next);
    } catch (err) {
      addToast(
        "error",
        "Đổi trạng thái thất bại",
        err?.response?.data?.message || err.message
      );
    }
  };

  const totalStock = React.useMemo(
    () =>
      (variants || []).reduce(
        (sum, v) => sum + (Number(v.stockQty) || 0),
        0
      ),
    [variants]
  );

  const selectedCatCount = (form.categoryIds || []).length;
  const selectedBadgeCount = (form.badges || []).length;

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
    <>
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

          {/* Hàng thông tin cơ bản */}
          <div
            className="grid cols-3 input-group"
            style={{ gridColumn: "1 / 3", marginBottom: 18 }}
          >
            <div className="group">
              <span>
                Tên sản phẩm <RequiredMark />
              </span>
              <input
                value={form.productName}
                onChange={(e) => {
                  markEditing();
                  set("productName", e.target.value);
                }}
                placeholder="VD: Microsoft 365 Family"
                maxLength={MAX_PRODUCT_NAME}
                disabled={lockIdentity}
                className={
                  lockIdentity
                    ? "input-readonly"
                    : errors.productName
                    ? "input-error"
                    : ""
                }
              />
              <FieldError message={errors.productName} />
            </div>

            <div className="group">
              <span>
                Mã định danh sản phẩm <RequiredMark />
              </span>
              <input
                value={form.productCode}
                onChange={(e) => {
                  markEditing();
                  set("productCode", e.target.value);
                }}
                onBlur={handleCodeBlur}
                placeholder="VD: OFF_365_FAM"
                maxLength={MAX_PRODUCT_CODE}
                disabled={lockIdentity}
                title={
                  lockIdentity
                    ? "Không cho phép đổi mã khi sản phẩm đã có biến thể hoặc FAQ"
                    : "Mã duy nhất cho sản phẩm"
                }
                className={
                  lockIdentity
                    ? "mono input-readonly"
                    : `mono ${errors.productCode ? "input-error" : ""}`
                }
              />
              <FieldError message={errors.productCode} />
            </div>

            <div className="group">
              <span>Loại</span>
              <select
                value={form.productType}
                onChange={(e) => {
                  markEditing();
                  set("productType", e.target.value);
                }}
              >
                <option value="PERSONAL_KEY">Mã cá nhân</option>
                <option value="SHARED_KEY">Mã dùng chung</option>
                <option value="PERSONAL_ACCOUNT">Tài khoản cá nhân</option>
                <option value="SHARED_ACCOUNT">Tài khoản dùng chung</option>
              </select>
            </div>
          </div>

          {/* Danh mục + Nhãn */}
          <div
            className="grid cols-2 input-group"
            style={{ gridColumn: "1 / 3", marginBottom: 20 }}
          >
            {/* Danh mục */}
            <div className="group">
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
                                markEditing();
                                const prev = form.categoryIds || [];
                                set(
                                  "categoryIds",
                                  e.target.checked
                                    ? Array.from(
                                        new Set([...prev, c.categoryId])
                                      )
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
                      const color = getBadgeColor(b);
                      const name = getBadgeName(b);
                      const code = b?.badgeCode ?? name;
                      return (
                        <div key={code} className="list-row">
                          <div className="left">
                            <span
                              className="label-chip"
                              style={{
                                backgroundColor: color,
                                color: "#fff",
                                padding: "4px 10px",
                                borderRadius: 8,
                                fontSize: 12,
                              }}
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
                                  markEditing();
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
          {showSubPanels && (
            <div
              className="group"
              style={{
                gridColumn: "1 / 3",
                marginTop: 8,
                display: "flex",
                flexDirection: "column",
                gap: 24,
              }}
            >
              <VariantsPanel
                productId={productId}
                productName={form.productName}
                productCode={form.productCode}
                onTotalChange={(total) => setHasVariants(total > 0)}
              />
              <FaqsPanel
                productId={productId}
                onTotalChange={(total) => setHasFaqs(total > 0)}
              />
            </div>
          )}

          {/* ACTIONS */}
          <div className="row" style={{ marginTop: 16 }}>
            <button className="btn primary" disabled={saving} onClick={save}>
              {saving ? "Đang lưu…" : "Lưu thay đổi"}
            </button>
          </div>
        </div>
      </div>

      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </>
  );
}
