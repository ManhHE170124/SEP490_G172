// src/pages/admin/CategoryPage.jsx
import React from "react";
import { CategoryApi, CategoryCsv } from "../../services/categories";
import { BadgesApi } from "../../services/badges";
import ToastContainer from "../../components/Toast/ToastContainer";
import ColorPickerTabs, {
  bestTextColor,
} from "../../components/color/ColorPickerTabs";
import "./CategoryPage.css";

/* ============ Helpers: Label + Error ============ */
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

const CATEGORY_NAME_MAX = 100;
const CATEGORY_CODE_MAX = 50;
const CATEGORY_DESC_MAX = 200;

// Giới hạn badge code + validate màu hex
const BADGE_CODE_MAX = 32;
const BADGE_NAME_MAX = 64;
const isValidHexColor = (value) => {
  if (!value) return false;
  const v = value.trim();
  // #RGB hoặc #RRGGBB
  return /^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$/.test(v);
};

/* ============ Modal: Category (Add / Edit) ============ */
function CategoryModal({
  open,
  mode, // "add" | "edit"
  initial,
  onClose,
  onSubmit,
  submitting,
  addToast,
  openConfirm,
}) {
  const isEdit = mode === "edit";

  const [form, setForm] = React.useState({
    categoryName: "",
    categoryCode: "",
    description: "",
    isActive: true,
  });
  const [errors, setErrors] = React.useState({});
  const initialRef = React.useRef(null);

  React.useEffect(() => {
    if (open) {
      const next = {
        categoryName: initial?.categoryName || "",
        categoryCode: initial?.categoryCode || "",
        description: initial?.description || "",
        isActive:
          typeof initial?.isActive === "boolean" ? initial.isActive : true,
      };
      setForm(next);
      setErrors({});
      initialRef.current = next;
    }
  }, [open, initial]);

  const isDirty = React.useMemo(() => {
    if (!open || !initialRef.current) return false;
    return JSON.stringify(form) !== JSON.stringify(initialRef.current);
  }, [open, form]);

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }));

  const validate = () => {
    const e = {};
    const name = form.categoryName.trim();
    const code = form.categoryCode.trim();
    const desc = form.description || "";

    if (!name) {
      e.categoryName = "Tên danh mục là bắt buộc.";
    } else if (name.length > CATEGORY_NAME_MAX) {
      e.categoryName = `Tên danh mục không được vượt quá ${CATEGORY_NAME_MAX} ký tự.`;
    }

    // Validate mã danh mục cho cả add & edit
    if (!code) {
      e.categoryCode = "Mã danh mục là bắt buộc.";
    } else if (/\s/.test(code)) {
      e.categoryCode = "Mã danh mục không được chứa dấu cách.";
    } else if (code.length > CATEGORY_CODE_MAX) {
      e.categoryCode = `Mã danh mục không được vượt quá ${CATEGORY_CODE_MAX} ký tự.`;
    }

    if (desc && desc.length > CATEGORY_DESC_MAX) {
      e.description = `Mô tả không được vượt quá ${CATEGORY_DESC_MAX} ký tự.`;
    }

    setErrors(e);

    if (Object.keys(e).length > 0 && typeof addToast === "function") {
      addToast(
        "warning",
        "Vui lòng kiểm tra các trường được đánh dấu.",
        "Dữ liệu chưa hợp lệ"
      );
    }

    return Object.keys(e).length === 0;
  };

  const handleSubmit = async (evt) => {
    evt.preventDefault();
    if (!validate()) return;

    try {
      await onSubmit?.({
        categoryName: form.categoryName.trim(),
        categoryCode: form.categoryCode.trim(),
        description: form.description,
        isActive: !!form.isActive,
      });
    } catch (err) {
      // Map lỗi BE -> field + toast (theo style VariantDetail)
      const resp = err?.response || err;
      const status = resp?.status;
      const data = resp?.data || {};
      const msg = data.message || resp.message || "";
      const fieldErrors = {};

      if (status === 409) {
        // Lỗi mã trùng
        fieldErrors.categoryCode =
          "Mã danh mục đã tồn tại, vui lòng chọn mã khác.";
        if (typeof addToast === "function") {
          addToast(
            "warning",
            "Mã danh mục đã tồn tại, vui lòng chọn mã khác.",
            "Mã danh mục trùng"
          );
        }
      }

      // Trường hợp BE trả lỗi tên (phòng hờ)
      if (status === 400 && /CategoryName/i.test(msg)) {
        fieldErrors.categoryName = "Tên danh mục không hợp lệ.";
        if (typeof addToast === "function") {
          addToast(
            "warning",
            "Tên danh mục không hợp lệ.",
            "Dữ liệu chưa hợp lệ"
          );
        }
      }

      if (Object.keys(fieldErrors).length === 0) {
        // Lỗi khác: bắn toast error chung
        if (typeof addToast === "function") {
          addToast("error", msg || "Lưu danh mục thất bại.", "Lỗi");
        }
      }

      if (Object.keys(fieldErrors).length > 0) {
        setErrors((prev) => ({ ...prev, ...fieldErrors }));
      }
    }
  };

  const handleClose = () => {
    if (isDirty) {
      // Dùng ConfirmDialog
      if (typeof openConfirm === "function") {
        openConfirm({
          title: "Đóng cửa sổ?",
          message:
            "Bạn có các thay đổi chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này.",
          onConfirm: () => {
            onClose?.();
          },
        });
      } else {
        const ok = window.confirm(
          "Bạn có các thay đổi chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này. Bạn có chắc muốn thoát?"
        );
        if (!ok) return;
        onClose?.();
      }
    } else {
      onClose?.();
    }
  };

  if (!open) return null;

  return (
    <div className="cat-modal-backdrop">
      <div className="cat-modal-card">
        <div className="cat-modal-header">
          <h3>{isEdit ? "Chỉnh sửa danh mục" : "Thêm danh mục"}</h3>
          {/* Trạng thái */}
          <div className="group" style={{ gridColumn: "1 / 3" }}>
            <div className="row" style={{ gap: 8, alignItems: "center" }}>
              <label className="switch" title="Bật/Tắt hiển thị">
                <input
                  type="checkbox"
                  checked={!!form.isActive}
                  onChange={() => set("isActive", !form.isActive)}
                />
                <span className="slider" />
              </label>
              <span
                className={form.isActive ? "badge green" : "badge gray"}
                style={{ textTransform: "none" }}
              >
                {form.isActive ? "Đang hiển thị" : "Đang ẩn"}
              </span>
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="cat-modal-body grid cols-2 input-group">
            {/* Tên danh mục */}
            <div className="group" style={{ gridColumn: "1 / 2" }}>
              <span>
                Tên danh mục <RequiredMark />
              </span>
              <input
                value={form.categoryName}
                onChange={(e) => set("categoryName", e.target.value)}
                placeholder="VD: Office"
                maxLength={CATEGORY_NAME_MAX}
              />
              <FieldError message={errors.categoryName} />
            </div>

            {/* Mã danh mục */}
            <div className="group" style={{ gridColumn: "2 / 3" }}>
              <span>
                Mã danh mục <RequiredMark />
              </span>
              <input
                value={form.categoryCode}
                onChange={(e) => set("categoryCode", e.target.value)}
                placeholder="VD: office"
                maxLength={CATEGORY_CODE_MAX}
                className="mono"
                title="Không chứa dấu cách"
              />
              <FieldError message={errors.categoryCode} />
            </div>

            {/* Mô tả */}
            <div className="group" style={{ gridColumn: "1 / 3" }}>
              <span>Mô tả</span>
              <textarea
                rows={3}
                value={form.description}
                onChange={(e) => set("description", e.target.value)}
                placeholder="Mô tả ngắn sẽ hiển thị trên website…"
                maxLength={CATEGORY_DESC_MAX}
              />
              <FieldError message={errors.description} />
            </div>
          </div>

          <div className="cat-modal-footer">
            <button
              type="button"
              className="btn ghost"
              onClick={handleClose}
              disabled={submitting}
            >
              Hủy
            </button>
            <button
              type="submit"
              className="btn primary"
              disabled={submitting}
            >
              {submitting
                ? isEdit
                  ? "Đang lưu…"
                  : "Đang tạo…"
                : isEdit
                ? "Lưu thay đổi"
                : "Tạo danh mục"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ============ Modal: Badge (Add / Edit) ============ */
function BadgeModal({
  open,
  mode, // "add" | "edit"
  initial,
  onClose,
  onSubmit,
  submitting,
  addToast,
  openConfirm,
}) {
  const isEdit = mode === "edit";

  const [form, setForm] = React.useState({
    badgeCode: "",
    displayName: "",
    colorHex: "#1e40af",
    icon: "",
    isActive: true,
  });
  const [errors, setErrors] = React.useState({});
  const [showPreview, setShowPreview] = React.useState(false);
  const initialRef = React.useRef(null);

  React.useEffect(() => {
    if (open) {
      const next = {
        badgeCode: initial?.badgeCode || "",
        displayName: initial?.displayName || "",
        colorHex: initial?.colorHex || initial?.color || "#1e40af",
        icon: initial?.icon || "",
        isActive:
          typeof initial?.isActive === "boolean" ? initial.isActive : true,
      };
      setForm(next);
      setErrors({});
      setShowPreview(false);
      initialRef.current = next;
    }
  }, [open, initial]);

  const isDirty = React.useMemo(() => {
    if (!open || !initialRef.current) return false;
    return JSON.stringify(form) !== JSON.stringify(initialRef.current);
  }, [open, form]);

  const set = (k, v) => setForm((s) => ({ ...s, [k]: v }));

  const validate = () => {
    const e = {};
    const code = form.badgeCode.trim();
    const name = form.displayName.trim();
    const color = (form.colorHex || "").trim();

    // BadgeCode: bắt buộc, không space, giới hạn độ dài
    if (!code) {
      e.badgeCode = "Mã nhãn là bắt buộc.";
    } else if (/\s/.test(code)) {
      e.badgeCode = "Mã nhãn không được chứa dấu cách.";
    } else if (code.length > BADGE_CODE_MAX) {
      e.badgeCode = `Mã nhãn không được vượt quá ${BADGE_CODE_MAX} ký tự.`;
    }

    // DisplayName: bắt buộc
  if (!name) {
    e.displayName = "Tên hiển thị là bắt buộc.";
  } else if (name.length > BADGE_NAME_MAX) {
    e.displayName = `Tên hiển thị không được vượt quá ${BADGE_NAME_MAX} ký tự.`;
  }

    // ColorHex: nếu có thì phải là mã hex hợp lệ
    if (color && !isValidHexColor(color)) {
      e.colorHex = "Màu phải là mã hex hợp lệ, ví dụ: #1e40af.";
    }

    setErrors(e);

    if (Object.keys(e).length > 0 && typeof addToast === "function") {
      addToast(
        "warning",
        "Vui lòng kiểm tra các trường được đánh dấu.",
        "Dữ liệu chưa hợp lệ"
      );
    }

    return Object.keys(e).length === 0;
  };

  const handleSubmit = async (evt) => {
    evt.preventDefault();
    if (!validate()) return;

    try {
      await onSubmit?.({
        badgeCode: form.badgeCode.trim(),
        displayName: form.displayName.trim(),
        colorHex: (form.colorHex || "").trim(),
        icon: form.icon?.trim(),
        isActive: !!form.isActive,
      });
    } catch (err) {
      const resp = err?.response || err;
      const status = resp?.status;
      const data = resp?.data || {};
      const msg = data.message || resp.message || "";
      const fieldErrors = {};

      if (status === 409) {
        // BadgeCode trùng
        fieldErrors.badgeCode =
          "Mã nhãn đã tồn tại, vui lòng chọn mã khác.";
        addToast?.(
          "warning",
          "Mã nhãn đã tồn tại, vui lòng chọn mã khác.",
          "Mã nhãn trùng"
        );
      }

      // Trường hợp BE trả lỗi chỉ rõ BadgeCode/DisplayName/ColorHex
      if (status === 400) {
        if (/BadgeCode/i.test(msg) && !fieldErrors.badgeCode) {
          fieldErrors.badgeCode = "Mã nhãn không hợp lệ.";
        }
        if (/DisplayName/i.test(msg) && !fieldErrors.displayName) {
          fieldErrors.displayName = "Tên hiển thị không hợp lệ.";
        }
        if (/ColorHex/i.test(msg) && !fieldErrors.colorHex) {
          fieldErrors.colorHex = "Màu không hợp lệ.";
        }
      }

      if (Object.keys(fieldErrors).length === 0) {
        // Lỗi chung
        addToast?.("error", msg || "Lưu nhãn thất bại.", "Lỗi");
      } else {
        setErrors((prev) => ({ ...prev, ...fieldErrors }));
      }
    }
  };

  const handleClose = () => {
    if (isDirty) {
      if (typeof openConfirm === "function") {
        openConfirm({
          title: "Đóng cửa sổ?",
          message:
            "Bạn có các thay đổi chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này.",
          onConfirm: () => {
            onClose?.();
          },
        });
      } else {
        const ok = window.confirm(
          "Bạn có các thay đổi chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này. Bạn có chắc muốn thoát?"
        );
        if (!ok) return;
        onClose?.();
      }
    } else {
      onClose?.();
    }
  };

  if (!open) return null;

  return (
    <div className="cat-modal-backdrop">
      <div className="cat-modal-card">
        <div className="cat-modal-header">
          <h3>{isEdit ? "Chỉnh sửa nhãn" : "Thêm nhãn"}</h3>
          {/* Trạng thái */}
          <div className="group" style={{ marginTop: 8 }}>
            <div className="row" style={{ gap: 8, alignItems: "center" }}>
              <label className="switch" title="Bật/Tắt hiển thị">
                <input
                  type="checkbox"
                  checked={!!form.isActive}
                  onChange={() => set("isActive", !form.isActive)}
                />
                <span className="slider" />
              </label>
              <span
                className={form.isActive ? "badge green" : "badge gray"}
                style={{ textTransform: "none" }}
              >
                {form.isActive ? "Đang hiển thị" : "Đang ẩn"}
              </span>
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="cat-modal-body input-group">
            <div className="grid cols-2">
              {/* Mã nhãn */}
              <div className="group">
                <span>
                  Mã nhãn <RequiredMark />
                </span>
                <input
                  value={form.badgeCode}
                  onChange={(e) => set("badgeCode", e.target.value)}
                  placeholder="VD: HOT"
                  className="mono"
                  maxLength={BADGE_CODE_MAX}
                />
                <FieldError message={errors.badgeCode} />
              </div>

              {/* Tên hiển thị */}
              <div className="group">
                <span>
                  Tên hiển thị <RequiredMark />
                </span>
                <input
                  value={form.displayName}
                  onChange={(e) => set("displayName", e.target.value)}
                  placeholder="VD: Nổi bật"
                 maxLength={BADGE_NAME_MAX}
                />
                <FieldError message={errors.displayName} />
              </div>

              {/* Icon */}
              <div className="group">
                <span>Biểu tượng (tùy chọn)</span>
                <input
                  value={form.icon}
                  onChange={(e) => set("icon", e.target.value)}
                  placeholder="VD: fire, star, sale…"
                />
              </div>

              {/* Màu nhãn */}
              <div className="group">
                <span>Màu nhãn</span>
                <ColorPickerTabs
                  value={form.colorHex || "#1e40af"}
                  onChange={(hex) => set("colorHex", hex)}
                />
                <FieldError message={errors.colorHex} />
              </div>
            </div>

            {/* Preview */}
            <div
              className="row"
              style={{ marginTop: 8, gap: 8, alignItems: "center" }}
            >
              {showPreview && (
                <span
                  className="label-chip"
                  style={{
                    backgroundColor: form.colorHex,
                    color: bestTextColor(form.colorHex),
                  }}
                  title={form.displayName || form.badgeCode}
                >
                  {form.displayName || form.badgeCode || "Nhãn"}
                </span>
              )}
              <button
                type="button"
                className="btn"
                onClick={() => setShowPreview((v) => !v)}
              >
                {showPreview ? "Ẩn xem trước" : "Xem trước nhãn"}
              </button>
            </div>
          </div>

          <div className="cat-modal-footer">
            <button
              type="button"
              className="btn ghost"
              onClick={handleClose}
              disabled={submitting}
            >
              Hủy
            </button>
            <button
              type="submit"
              className="btn primary"
              disabled={submitting}
            >
              {submitting
                ? isEdit
                  ? "Đang lưu…"
                  : "Đang tạo…"
                : isEdit
                ? "Lưu thay đổi"
                : "Tạo nhãn"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ============ MAIN PAGE ============ */
export default function CategoryPage() {
  // ====== Toast & ConfirmDialog ======
  const [toasts, setToasts] = React.useState([]);
  const [confirmDialog, setConfirmDialog] = React.useState(null);
  const toastIdRef = React.useRef(1);

  const removeToast = (id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  // signature: (type, message, title)
  const addToast = (type, message, title) => {
    const id = toastIdRef.current++;
    setToasts((prev) => [
      ...prev,
      { id, type, message, title: title || undefined },
    ]);

    setTimeout(() => {
      removeToast(id);
    }, 5000);

    return id;
  };

  const openConfirm = ({ title, message, onConfirm }) => {
    setConfirmDialog({
      title,
      message,
      onConfirm: async () => {
        setConfirmDialog(null);
        await onConfirm?.();
      },
      onCancel: () => setConfirmDialog(null),
    });
  };

  // ====== Danh mục ======
  const [catQuery, setCatQuery] = React.useState({
    keyword: "",
    active: "",
    sort: "name",
    direction: "asc",
  });
  const [categories, setCategories] = React.useState([]);
  const [catLoading, setCatLoading] = React.useState(false);
  const [catPage, setCatPage] = React.useState(1);
  const [catPageSize, setCatPageSize] = React.useState(10);
  const [catTotal, setCatTotal] = React.useState(0);

  const [catModal, setCatModal] = React.useState({
    open: false,
    mode: "add", // "add" | "edit"
    data: null,
  });
  const [catSubmitting, setCatSubmitting] = React.useState(false);

  const loadCategories = React.useCallback(() => {
    setCatLoading(true);
    const params = { ...catQuery, page: catPage, pageSize: catPageSize };
    if (params.active === "") delete params.active;
    if (!params.sort) params.sort = "name";
    if (!params.direction) params.direction = "asc";

    CategoryApi.listPaged(params)
      .then((res) => {
        const items = res?.items ?? res ?? [];
        setCategories(items);
        setCatTotal(typeof res?.total === "number" ? res.total : items.length);
      })
      .catch((err) => {
        console.error(err);
        addToast("error", "Không tải được danh mục.", "Lỗi");
      })
      .finally(() => setCatLoading(false));
  }, [catQuery, catPage, catPageSize]);

  React.useEffect(() => {
    const t = setTimeout(loadCategories, 300);
    return () => clearTimeout(t);
  }, [loadCategories]);

  React.useEffect(() => {
    setCatPage(1);
  }, [catQuery.keyword, catQuery.active, catQuery.sort, catQuery.direction]);

  const catToggle = async (id) => {
    try {
      const resp = await CategoryApi.toggle(id);
      const isActive =
        resp?.isActive ??
        resp?.IsActive ??
        resp?.data?.isActive ??
        resp?.data?.IsActive;

      let message;
      if (isActive === true) {
        message = "Danh mục đang được hiển thị.";
      } else if (isActive === false) {
        message = "Danh mục đã được ẩn.";
      } else {
        message = "Đã cập nhật trạng thái danh mục.";
      }

      addToast("success", message, "Thành công");
      loadCategories();
    } catch (err) {
      console.error(err);
      addToast(
        "error",
        err?.response?.data?.message ||
          "Không thể cập nhật trạng thái danh mục.",
        "Lỗi"
      );
    }
  };

  const deleteCategory = (c) => {
    openConfirm({
      title: "Xoá danh mục?",
      message: `Xoá danh mục "${c.categoryName}"? Hành động này không thể hoàn tác!`,
      onConfirm: async () => {
        try {
          await CategoryApi.remove(c.categoryId);
          addToast("success", "Đã xoá danh mục.", "Thành công");
          loadCategories();
        } catch (e) {
          console.error(e);
          addToast(
            "error",
            e?.response?.data?.message || "Xoá danh mục thất bại.",
            "Lỗi"
          );
        }
      },
    });
  };

  const openAddCategory = () =>
    setCatModal({ open: true, mode: "add", data: null });
  const openEditCategory = (c) =>
    setCatModal({ open: true, mode: "edit", data: c });

  const handleCategorySubmit = async (form) => {
    setCatSubmitting(true);
    try {
      if (catModal.mode === "add") {
        await CategoryApi.create({
          categoryName: form.categoryName,
          categoryCode: form.categoryCode,
          description: form.description,
          isActive: form.isActive,
        });
        addToast("success", "Đã tạo danh mục.", "Thành công");
      } else if (catModal.mode === "edit" && catModal.data) {
        await CategoryApi.update(catModal.data.categoryId, {
          categoryName: form.categoryName,
          categoryCode: form.categoryCode,
          description: form.description,
          isActive: form.isActive,
        });
        addToast("success", "Đã lưu thay đổi danh mục.", "Thành công");
      }
      setCatModal((m) => ({ ...m, open: false }));
      loadCategories();
    } catch (err) {
      console.error(err);
      // Lỗi validate/trùng đã xử lý ở CategoryModal (toast + field)
      throw err;
    } finally {
      setCatSubmitting(false);
    }
  };

  // ====== CSV danh mục ======
  const catExportCsv = async () => {
    try {
      const blob = await CategoryCsv.exportCsv();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = "categories.csv";
      a.click();
      URL.revokeObjectURL(url);
      addToast("success", "Đã xuất CSV danh mục.", "Thành công");
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message || "Xuất CSV thất bại.",
        "Lỗi"
      );
    }
  };

  const catImportCsv = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.name.toLowerCase().endsWith(".csv")) {
      addToast("error", "Vui lòng chọn file .csv hợp lệ.", "Sai định dạng");
      e.target.value = "";
      return;
    }

    try {
      const res = await CategoryCsv.importCsv(file);
      addToast(
        "success",
        `Import xong: total=${res.total}, created=${res.created}, updated=${res.updated}`,
        "Import CSV"
      );
      loadCategories();
    } catch (err) {
      console.error(err);
      addToast(
        "error",
        err?.response?.data?.message || "Import CSV thất bại.",
        "Lỗi"
      );
    } finally {
      e.target.value = "";
    }
  };

  // ====== Badges ======
  const [badges, setBadges] = React.useState([]);
  const [badgesLoading, setBadgesLoading] = React.useState(false);
  const [badgeQuery, setBadgeQuery] = React.useState({
    keyword: "",
    active: "",
  });
  const [badgeSort, setBadgeSort] = React.useState("name");
  const [badgeDirection, setBadgeDirection] = React.useState("asc");
  const [badgePage, setBadgePage] = React.useState(1);
  const [badgePageSize, setBadgePageSize] = React.useState(10);
  const [badgeTotal, setBadgeTotal] = React.useState(0);

  const [badgeModal, setBadgeModal] = React.useState({
    open: false,
    mode: "add",
    data: null,
  });
  const [badgeSubmitting, setBadgeSubmitting] = React.useState(false);

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

  const loadBadges = React.useCallback(() => {
    setBadgesLoading(true);
    const params = {
      keyword: badgeQuery.keyword || undefined,
      active: badgeQuery.active || undefined,
      sort: badgeSort,
      direction: badgeDirection,
      page: badgePage,
      pageSize: badgePageSize,
    };
    BadgesApi.listPaged(params)
      .then((res) => {
        const items = res?.items ?? res ?? [];
        setBadges(items);
        setBadgeTotal(
          typeof res?.total === "number" ? res.total : items.length
        );
      })
      .catch((err) => {
        console.error(err);
        addToast("error", "Không tải được nhãn sản phẩm.", "Lỗi");
      })
      .finally(() => setBadgesLoading(false));
  }, [badgeQuery, badgeSort, badgeDirection, badgePage, badgePageSize]);

  React.useEffect(() => {
    loadBadges();
  }, [loadBadges]);

  React.useEffect(() => {
    setBadgePage(1);
  }, [badgeQuery.keyword, badgeQuery.active, badgeSort, badgeDirection]);

  const toggleBadge = async (code) => {
    try {
      const resp = await BadgesApi.toggle(code);
      const isActive =
        resp?.isActive ??
        resp?.IsActive ??
        resp?.data?.isActive ??
        resp?.data?.IsActive;

      let message;
      if (isActive === true) {
        message = "Nhãn đang được hiển thị.";
      } else if (isActive === false) {
        message = "Nhãn đã được ẩn.";
      } else {
        message = "Đã cập nhật trạng thái nhãn.";
      }

      addToast("success", message, "Thành công");
      loadBadges();
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message || "Không thể cập nhật trạng thái nhãn.",
        "Lỗi"
      );
    }
  };

  const deleteBadge = (b) => {
    const name = getBadgeName(b);
    openConfirm({
      title: "Xoá nhãn?",
      message: `Xoá nhãn "${name}" (${b.badgeCode})? Hành động này không thể hoàn tác!`,
      onConfirm: async () => {
        try {
          await BadgesApi.remove(b.badgeCode);
          addToast("success", "Đã xoá nhãn.", "Thành công");
          loadBadges();
        } catch (e) {
          console.error(e);
          addToast(
            "error",
            e?.response?.data?.message || "Xoá nhãn thất bại.",
            "Lỗi"
          );
        }
      },
    });
  };

  const openAddBadge = () =>
    setBadgeModal({ open: true, mode: "add", data: null });
  const openEditBadge = (b) =>
    setBadgeModal({ open: true, mode: "edit", data: b });

  const handleBadgeSubmit = async (form) => {
    setBadgeSubmitting(true);
    try {
      if (badgeModal.mode === "add") {
        await BadgesApi.create(form);
        addToast("success", "Đã tạo nhãn.", "Thành công");
      } else if (badgeModal.mode === "edit" && badgeModal.data) {
        await BadgesApi.update(badgeModal.data.badgeCode, form);
        addToast("success", "Đã lưu thay đổi nhãn.", "Thành công");
      }
      setBadgeModal((m) => ({ ...m, open: false }));
      loadBadges();
    } finally {
      setBadgeSubmitting(false);
    }
  };

  return (
    <>
      <div className="page">
        {/* ===== Khối: Danh mục ===== */}
        <div className="card">
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
            }}
          >
            <h2>Danh mục sản phẩm</h2>
            <div className="row" style={{ gap: 8 }}>
              <label className="btn">
                ⬆ Nhập CSV
                <input
                  type="file"
                  accept=".csv,text/csv"
                  style={{ display: "none" }}
                  onChange={catImportCsv}
                />
              </label>
              <button className="btn" onClick={catExportCsv}>
                ⬇ Xuất CSV
              </button>
              <button className="btn primary" onClick={openAddCategory}>
                + Thêm danh mục
              </button>
            </div>
          </div>

          {/* Bộ lọc danh mục */}
          <div
            className="row input-group"
            style={{
              gap: 10,
              marginTop: 12,
              flexWrap: "nowrap",
              alignItems: "end",
              overflowX: "auto",
            }}
          >
            <div className="group" style={{ minWidth: 320, maxWidth: 520 }}>
              <span>Tìm kiếm</span>
              <input
                value={catQuery.keyword}
                onChange={(e) =>
                  setCatQuery((s) => ({ ...s, keyword: e.target.value }))
                }
                placeholder="Tìm theo mã, tên hoặc mô tả…"
              />
            </div>
            <div className="group" style={{ minWidth: 160 }}>
              <span>Trạng thái</span>
              <select
                value={catQuery.active}
                onChange={(e) =>
                  setCatQuery((s) => ({ ...s, active: e.target.value }))
                }
              >
                <option value="">Tất cả</option>
                <option value="true">Hiển thị</option>
                <option value="false">Ẩn</option>
              </select>
            </div>

            {catLoading && <span className="badge gray">Đang tải…</span>}

            <button
              className="btn"
              onClick={() =>
                setCatQuery((s) => ({ ...s, keyword: "", active: "" }))
              }
              title="Xoá bộ lọc"
            >
              Đặt lại
            </button>
          </div>

          <table className="table" style={{ marginTop: 10 }}>
            <thead>
              <tr>
                <th
                  onClick={() =>
                    setCatQuery((s) => ({
                      ...s,
                      sort: "name",
                      direction:
                        s.sort === "name" && s.direction === "asc"
                          ? "desc"
                          : "asc",
                    }))
                  }
                  style={{ cursor: "pointer" }}
                >
                  Tên{" "}
                  {catQuery.sort === "name"
                    ? catQuery.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th
                  onClick={() =>
                    setCatQuery((s) => ({
                      ...s,
                      sort: "code",
                      direction:
                        s.sort === "code" && s.direction === "asc"
                          ? "desc"
                          : "asc",
                    }))
                  }
                  style={{ cursor: "pointer" }}
                >
                  Mã danh mục{" "}
                  {catQuery.sort === "code"
                    ? catQuery.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th>Số sản phẩm</th>
                <th
                  onClick={() =>
                    setCatQuery((s) => ({
                      ...s,
                      sort: "active",
                      direction:
                        s.sort === "active" && s.direction === "asc"
                          ? "desc"
                          : "asc",
                    }))
                  }
                  style={{ cursor: "pointer" }}
                >
                  Trạng thái{" "}
                  {catQuery.sort === "active"
                    ? catQuery.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {categories.map((c) => (
                <tr key={c.categoryId}>
                  <td>{c.categoryName}</td>
                  <td className="mono">{c.categoryCode}</td>
                  <td>{c.productsCount ?? c.productCount ?? c.products ?? 0}</td>
                  <td>
                    <span className={c.isActive ? "badge green" : "badge gray"}>
                      {c.isActive ? "Hiển thị" : "Ẩn"}
                    </span>
                  </td>
                  <td
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 8,
                    }}
                  >
                    <div className="action-buttons">
                      <button
                        className="action-btn edit-btn"
                        type="button"
                        title="Xem chi tiết / chỉnh sửa"
                        onClick={() => openEditCategory(c)}
                      >
                        <svg
                          viewBox="0 0 24 24"
                          width="16"
                          height="16"
                          fill="currentColor"
                          aria-hidden="true"
                        >
                          <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                          <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                        </svg>
                      </button>
                      <button
                        className="action-btn delete-btn"
                        title="Xoá danh mục"
                        type="button"
                        onClick={() => deleteCategory(c)}
                      >
                        <svg
                          viewBox="0 0 24 24"
                          width="16"
                          height="16"
                          fill="currentColor"
                          aria-hidden="true"
                        >
                          <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                        </svg>
                      </button>
                    </div>
                    <label className="switch" title="Bật/Tắt hiển thị">
                      <input
                        type="checkbox"
                        checked={!!c.isActive}
                        onChange={() => catToggle(c.categoryId)}
                      />
                      <span className="slider" />
                    </label>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Pagination: Categories */}
          <div className="pager">
            <button
              disabled={catPage <= 1}
              onClick={() => setCatPage((p) => Math.max(1, p - 1))}
            >
              Trước
            </button>
            <span style={{ padding: "0 8px" }}>Trang {catPage}</span>
            <button
              disabled={catPage * catPageSize >= catTotal}
              onClick={() => setCatPage((p) => p + 1)}
            >
              Tiếp
            </button>
          </div>
        </div>

        {/* ===== Khối: Nhãn sản phẩm ===== */}
        <div className="card" style={{ marginTop: 14 }}>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
            }}
          >
            <h2>Nhãn sản phẩm</h2>
            <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <button className="btn primary" onClick={openAddBadge}>
                + Thêm nhãn
              </button>
            </div>
          </div>

          {/* Filter badges */}
          <div
            className="row input-group"
            style={{
              gap: 10,
              marginTop: 12,
              flexWrap: "nowrap",
              alignItems: "end",
              overflowX: "auto",
            }}
          >
            <div className="group" style={{ minWidth: 320 }}>
              <span>Tìm kiếm</span>
              <input
                value={badgeQuery.keyword}
                onChange={(e) =>
                  setBadgeQuery((s) => ({ ...s, keyword: e.target.value }))
                }
                placeholder="Tìm theo mã, tên, màu…"
              />
            </div>
            <div className="group" style={{ minWidth: 160 }}>
              <span>Trạng thái</span>
              <select
                value={badgeQuery.active}
                onChange={(e) =>
                  setBadgeQuery((s) => ({ ...s, active: e.target.value }))
                }
              >
                <option value="">Tất cả</option>
                <option value="true">Hiển thị</option>
                <option value="false">Ẩn</option>
              </select>
            </div>

            {badgesLoading && <span className="badge gray">Đang tải…</span>}

            <button
              className="btn"
              onClick={() => setBadgeQuery({ keyword: "", active: "" })}
            >
              Đặt lại
            </button>
          </div>

          <table className="table" style={{ marginTop: 10 }}>
            <thead>
              <tr>
                <th
                  onClick={() => {
                    const key = "code";
                    setBadgeSort((prev) => {
                      setBadgeDirection((d) =>
                        prev === key && d === "asc" ? "desc" : "asc"
                      );
                      return key;
                    });
                  }}
                  style={{ cursor: "pointer" }}
                >
                  Mã{" "}
                  {badgeSort === "code"
                    ? badgeDirection === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th
                  onClick={() => {
                    const key = "name";
                    setBadgeSort((prev) => {
                      setBadgeDirection((d) =>
                        prev === key && d === "asc" ? "desc" : "asc"
                      );
                      return key;
                    });
                  }}
                  style={{ cursor: "pointer" }}
                >
                  Tên{" "}
                  {badgeSort === "name"
                    ? badgeDirection === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th>Nhãn hiển thị</th>
                <th title="Số sản phẩm đang gắn nhãn này">Số SP</th>
                <th
                  onClick={() => {
                    const key = "color";
                    setBadgeSort((prev) => {
                      setBadgeDirection((d) =>
                        prev === key && d === "asc" ? "desc" : "asc"
                      );
                      return key;
                    });
                  }}
                  style={{ cursor: "pointer" }}
                >
                  Màu{" "}
                  {badgeSort === "color"
                    ? badgeDirection === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th
                  onClick={() => {
                    const key = "active";
                    setBadgeSort((prev) => {
                      setBadgeDirection((d) =>
                        prev === key && d === "asc" ? "desc" : "asc"
                      );
                      return key;
                    });
                  }}
                  style={{ cursor: "pointer" }}
                >
                  Trạng thái{" "}
                  {badgeSort === "active"
                    ? badgeDirection === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {badges.map((b) => {
                const name = getBadgeName(b);
                const color = getBadgeColor(b);
                const count =
                  b.productsCount ?? b.productCount ?? b.ProductsCount ?? 0;

                return (
                  <tr key={b.badgeCode}>
                    <td className="mono">{b.badgeCode}</td>
                    <td>{name}</td>
                    <td>
                      <span
                        className="label-chip"
                        style={{
                          backgroundColor: color,
                          color: "#fff",
                          padding: "4px 10px",
                          borderRadius: 8,
                          fontSize: 12,
                          display: "inline-block",
                        }}
                        title={name}
                      >
                        {name}
                      </span>
                    </td>
                    <td className="mono">{count}</td>
                    <td className="mono">{color}</td>
                    <td>
                      <span
                        className={b.isActive ? "badge green" : "badge gray"}
                      >
                        {b.isActive ? "Hiển thị" : "Ẩn"}
                      </span>
                    </td>
                    <td
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 8,
                      }}
                    >
                      <div className="action-buttons">
                        <button
                          className="action-btn edit-btn"
                          type="button"
                          title="Xem chi tiết / chỉnh sửa"
                          onClick={() => openEditBadge(b)}
                        >
                          <svg
                            viewBox="0 0 24 24"
                            width="16"
                            height="16"
                            fill="currentColor"
                            aria-hidden="true"
                          >
                            <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                            <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                          </svg>
                        </button>
                        <button
                          className="action-btn delete-btn"
                          title="Xoá nhãn"
                          type="button"
                          onClick={() => deleteBadge(b)}
                        >
                          <svg
                            viewBox="0 0 24 24"
                            width="16"
                            height="16"
                            fill="currentColor"
                            aria-hidden="true"
                          >
                            <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                          </svg>
                        </button>
                      </div>

                      <label className="switch" title="Bật/Tắt nhãn">
                        <input
                          type="checkbox"
                          checked={!!b.isActive}
                          onChange={() => toggleBadge(b.badgeCode)}
                        />
                        <span className="slider" />
                      </label>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          {/* Pagination: Badges */}
          <div className="pager">
            <button
              disabled={badgePage <= 1}
              onClick={() => setBadgePage((p) => Math.max(1, p - 1))}
            >
              Trước
            </button>
            <span style={{ padding: "0 8px" }}>Trang {badgePage}</span>
            <button
              disabled={badgePage * badgePageSize >= badgeTotal}
              onClick={() => setBadgePage((p) => p + 1)}
            >
              Tiếp
            </button>
          </div>
        </div>
      </div>

      {/* Modals */}
      <CategoryModal
        open={catModal.open}
        mode={catModal.mode}
        initial={catModal.data}
        onClose={() => setCatModal((m) => ({ ...m, open: false }))}
        onSubmit={handleCategorySubmit}
        submitting={catSubmitting}
        addToast={addToast}
        openConfirm={openConfirm}
      />

      <BadgeModal
        open={badgeModal.open}
        mode={badgeModal.mode}
        initial={badgeModal.data}
        onClose={() => setBadgeModal((m) => ({ ...m, open: false }))}
        onSubmit={handleBadgeSubmit}
        submitting={badgeSubmitting}
        addToast={addToast}
        openConfirm={openConfirm}
      />

      {/* Toast + Confirm Dialog */}
      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </>
  );
}
