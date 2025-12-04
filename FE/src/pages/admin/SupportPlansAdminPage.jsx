// src/pages/admin/SupportPlansAdminPage.jsx
import React from "react";
import ToastContainer from "../../components/Toast/ToastContainer";
import { SupportPlansAdminApi } from "../../services/supportPlansAdmin";
import "../../styles/SupportPlansAdminPage.css"; // dùng CSS riêng cho màn này

/* ============ Helpers: Label + Error + Ellipsis ============ */
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

const EllipsisCell = ({ children, title, maxWidth = 260, mono = false }) => (
  <div
    className={mono ? "mono" : undefined}
    title={title ?? (typeof children === "string" ? children : "")}
    style={{
      maxWidth,
      whiteSpace: "nowrap",
      overflow: "hidden",
      textOverflow: "ellipsis",
    }}
  >
    {children}
  </div>
);

/* ============ Utils ============ */
const formatCurrency = (value) => {
  const num = Number(value || 0);
  try {
    return new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: "VND",
      maximumFractionDigits: 0,
    }).format(num);
  } catch {
    return `${num.toLocaleString("vi-VN")} đ`;
  }
};

const priorityLabel = (level) => {
  const n = Number(level);
  if (Number.isNaN(n)) return `Level ${level}`;
  switch (n) {
    case 0:
      return "Standard (0)";
    case 1:
      return "Priority (1)";
    case 2:
      return "VIP (2)";
    default:
      return `Level ${n}`;
  }
};

const priorityBadgeClass = (level) => {
  const n = Number(level);
  switch (n) {
    case 1:
      return "badge level-priority";
    case 2:
      return "badge level-vip";
    default:
      return "badge level-other";
  }
};

const PRIORITY_OPTIONS = [
  { value: 0, label: "Standard (0)" },
  { value: 1, label: "Priority (1)" },
  { value: 2, label: "VIP (2)" },
];

/* ============ Modal: Support Plan (Add / Edit) ============ */
function SupportPlanModal({
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
    name: "",
    description: "",
    priorityLevel: "",
    price: "",
    isActive: false,
  });
  const [errors, setErrors] = React.useState({});
  const initialRef = React.useRef(null);

  React.useEffect(() => {
    if (open) {
      const base =
        initial || {
          name: "",
          description: "",
          priorityLevel: "",
          price: "",
          isActive: false,
        };

      const next = {
        name:
          base.name === null || base.name === undefined
            ? ""
            : String(base.name),
        description:
          base.description === null || base.description === undefined
            ? ""
            : String(base.description),
        priorityLevel:
          base.priorityLevel === null || base.priorityLevel === undefined
            ? ""
            : String(base.priorityLevel),
        price:
          base.price === null || base.price === undefined
            ? ""
            : String(base.price),
        isActive: typeof base.isActive === "boolean" ? base.isActive : false,
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

    const nameRaw = (form.name || "").toString().trim();
    const descRaw = (form.description || "").toString();
    const levelRaw = (form.priorityLevel || "").toString().trim();
    const priceRaw = (form.price || "").toString().trim();

    if (!nameRaw) {
      e.name = "Tên gói không được để trống.";
    } else if (nameRaw.length > 120) {
      e.name = "Tên gói không được vượt quá 120 ký tự.";
    }

    if (descRaw && descRaw.length > 500) {
      e.description = "Mô tả không được vượt quá 500 ký tự.";
    }

    if (!levelRaw && levelRaw !== "0") {
      e.priorityLevel = "Mức ưu tiên là bắt buộc.";
    } else if (Number.isNaN(Number(levelRaw))) {
      e.priorityLevel = "Mức ưu tiên phải là số.";
    } else if (!Number.isInteger(Number(levelRaw))) {
      e.priorityLevel = "Mức ưu tiên phải là số nguyên.";
    } else if (Number(levelRaw) < 0) {
      e.priorityLevel = "Mức ưu tiên phải lớn hơn hoặc bằng 0.";
    }

    if (!priceRaw) {
      e.price = "Giá gói là bắt buộc.";
    } else if (Number.isNaN(Number(priceRaw))) {
      e.price = "Giá gói phải là số.";
    } else if (Number(priceRaw) < 0) {
      e.price = "Giá gói phải lớn hơn hoặc bằng 0.";
    }

    setErrors(e);
    if (Object.keys(e).length > 0 && typeof addToast === "function") {
      addToast(
        "warning",
        "Vui lòng kiểm tra các trường được đánh dấu.",
        isEdit
          ? "Dữ liệu gói hỗ trợ chưa hợp lệ"
          : "Dữ liệu gói hỗ trợ chưa hợp lệ"
      );
    }
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async (evt) => {
    evt.preventDefault();
    if (!validate()) return;

    const nameRaw = (form.name || "").toString().trim();
    const descRaw = (form.description || "").toString().trim();
    const levelRaw = (form.priorityLevel || "").toString().trim();
    const priceRaw = (form.price || "").toString().trim();

    await onSubmit?.({
      name: nameRaw,
      description: descRaw || null,
      priorityLevel: Number(levelRaw || 0) || 0,
      price: Number(priceRaw || 0) || 0,
      isActive: !!form.isActive,
    });
  };

  const handleClose = () => {
    if (isDirty) {
      if (typeof openConfirm === "function") {
        openConfirm({
          title: "Đóng cửa sổ?",
          message:
            "Bạn có các thay đổi gói hỗ trợ chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này.",
          onConfirm: () => {
            onClose?.();
          },
        });
      } else {
        const ok = window.confirm(
          "Bạn có các thay đổi gói hỗ trợ chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này. Bạn có chắc muốn thoát?"
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
          <h3>{isEdit ? "Chỉnh sửa gói hỗ trợ" : "Thêm gói hỗ trợ"}</h3>
          <div className="group" style={{ marginTop: 8 }}>
            <div className="row" style={{ gap: 8, alignItems: "center" }}>
              <label className="switch" title="Bật/Tắt gói hỗ trợ">
                <input
                  type="checkbox"
                  checked={!!form.isActive}
                  onChange={() => set("isActive", !form.isActive)}
                />
                <span className="slider" />
                <span className="switch-label">
                  {form.isActive ? "Đang bật" : "Đang tắt"}
                </span>
              </label>
              <span className="muted">
                Trạng thái gói (chỉ 1 gói hoạt động cho mỗi mức ưu tiên)
              </span>
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="cat-modal-body input-group">
            <div className="row" style={{ gap: 16 }}>
              <div className="group" style={{ flex: 1 }}>
                <span>
                  Tên gói hỗ trợ <RequiredMark />
                </span>
                <input
                  type="text"
                  value={form.name}
                  onChange={(e) => set("name", e.target.value)}
                  placeholder="VD: Standard Support, Priority Support..."
                />
                <FieldError message={errors.name} />
              </div>

              <div className="group" style={{ width: 240 }}>
                <span>
                  Mức ưu tiên (PriorityLevel) <RequiredMark />
                </span>
                <select
                  value={form.priorityLevel}
                  onChange={(e) => set("priorityLevel", e.target.value)}
                >
                  <option value="">-- Chọn mức ưu tiên --</option>
                  {PRIORITY_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
                <FieldError message={errors.priorityLevel} />
              </div>
            </div>

            <div className="row" style={{ gap: 16, marginTop: 12 }}>
              <div className="group" style={{ width: 280 }}>
                <span>
                  Giá gói (VNĐ) <RequiredMark />
                </span>
                <input
                  type="number"
                  min={0}
                  step={1000}
                  value={form.price}
                  onChange={(e) => set("price", e.target.value)}
                  placeholder="VD: 100000"
                />
                <FieldError message={errors.price} />
              </div>
            </div>

            <div className="row" style={{ marginTop: 12 }}>
              <div className="group" style={{ flex: 1 }}>
                <span>Mô tả gói hỗ trợ</span>
                <textarea
                  rows={3}
                  value={form.description}
                  onChange={(e) => set("description", e.target.value)}
                  placeholder="Mô tả quyền lợi của gói: ưu tiên xử lý ticket, thời gian phản hồi, kênh hỗ trợ..."
                />
                <FieldError message={errors.description} />
              </div>
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
                  ? "Đang lưu..."
                  : "Đang tạo..."
                : isEdit
                ? "Lưu thay đổi"
                : "Tạo gói mới"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ============ Page: SupportPlansAdminPage ============ */
export default function SupportPlansAdminPage() {
  const [toasts, setToasts] = React.useState([]);
  const [confirmDialog, setConfirmDialog] = React.useState(null);
  const toastIdRef = React.useRef(1);

  const removeToast = (id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  const addToast = (type, message, title) => {
    const id = toastIdRef.current++;
    setToasts((prev) => [
      ...prev,
      { id, type, message, title: title || undefined },
    ]);
    setTimeout(() => removeToast(id), 5000);
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

  const [query, setQuery] = React.useState({
    priorityLevel: "",
    active: "",
    sort: "priorityLevel",
    direction: "asc",
  });
  const [plans, setPlans] = React.useState([]);
  const [loading, setLoading] = React.useState(false);
  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(10);
  const [total, setTotal] = React.useState(0);

  const [planModal, setPlanModal] = React.useState({
    open: false,
    mode: "add",
    data: null,
  });
  const [planSubmitting, setPlanSubmitting] = React.useState(false);

  const loadPlans = React.useCallback(() => {
    setLoading(true);

    const params = {
      priorityLevel:
        query.priorityLevel === "" ? undefined : Number(query.priorityLevel),
      active: query.active === "" ? undefined : query.active === "true",
      sort: query.sort || "priorityLevel",
      direction: query.direction || "asc",
      page,
      pageSize,
    };

    SupportPlansAdminApi.listPaged(params)
      .then((res) => {
        const items = Array.isArray(res?.items)
          ? res.items
          : Array.isArray(res)
          ? res
          : [];
        setPlans(items);
        setPage(typeof res?.page === "number" ? res.page : page);
        setPageSize(
          typeof res?.pageSize === "number" ? res.pageSize : pageSize
        );
        setTotal(typeof res?.total === "number" ? res.total : items.length);
      })
      .catch((err) => {
        console.error(err);
        addToast("error", "Không tải được danh sách gói hỗ trợ.", "Lỗi");
      })
      .finally(() => setLoading(false));
  }, [query, page, pageSize]);

  React.useEffect(() => {
    const t = setTimeout(loadPlans, 300);
    return () => clearTimeout(t);
  }, [loadPlans]);

  const totalPages = React.useMemo(
    () => Math.max(1, Math.ceil((total || 0) / (pageSize || 1))),
    [total, pageSize]
  );

  const openAddPlan = () =>
    setPlanModal({ open: true, mode: "add", data: null });

  const openEditPlan = async (p) => {
    try {
      const detail = await SupportPlansAdminApi.get(p.supportPlanId);
      setPlanModal({
        open: true,
        mode: "edit",
        data: detail,
      });
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message ||
          "Không tải được chi tiết gói hỗ trợ để chỉnh sửa.",
        "Lỗi"
      );
    }
  };

  const handlePlanSubmit = async (payload) => {
    setPlanSubmitting(true);
    try {
      if (planModal.mode === "add") {
        await SupportPlansAdminApi.create(payload);
        addToast("success", "Đã tạo gói hỗ trợ mới.", "Thành công");
      } else if (planModal.mode === "edit" && planModal.data) {
        await SupportPlansAdminApi.update(
          planModal.data.supportPlanId,
          payload
        );
        addToast("success", "Đã cập nhật gói hỗ trợ.", "Thành công");
      }

      setPlanModal((m) => ({ ...m, open: false }));
      loadPlans();
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message || "Lưu gói hỗ trợ thất bại.",
        "Lỗi"
      );
    } finally {
      setPlanSubmitting(false);
    }
  };

  const togglePlanActive = async (p) => {
    try {
      await SupportPlansAdminApi.toggle(p.supportPlanId);
      addToast("success", "Đã cập nhật trạng thái gói hỗ trợ.", "Thành công");
      loadPlans();
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message ||
          "Không thể cập nhật trạng thái gói hỗ trợ.",
        "Lỗi"
      );
    }
  };

  const deletePlan = (p) => {
    openConfirm({
      title: "Xoá gói hỗ trợ?",
      message: `Xoá gói "${p.name}" với mức ưu tiên ${priorityLabel(
        p.priorityLevel
      )} và giá ${formatCurrency(
        p.price
      )}? Hành động này không thể hoàn tác!`,
      onConfirm: async () => {
        try {
          await SupportPlansAdminApi.remove(p.supportPlanId);
          addToast("success", "Đã xoá gói hỗ trợ.", "Thành công");
          loadPlans();
        } catch (e) {
          console.error(e);
          addToast(
            "error",
            e?.response?.data?.message || "Xoá gói hỗ trợ thất bại.",
            "Lỗi"
          );
        }
      },
    });
  };

  const resetFilters = () => {
    setQuery({
      priorityLevel: "",
      active: "",
      sort: "priorityLevel",
      direction: "asc",
    });
    setPage(1);
  };

  return (
    <>
      <div className="page">
        <div
          className="card"
          style={{
            margin: "0 auto",
            maxWidth: 1120,
          }}
        >
          <div className="card-header">
            <div className="left">
              <h2>Cấu hình gói hỗ trợ (Support Plans)</h2>
              <p className="muted">
                Quản lý các gói hỗ trợ (Standard, Priority, VIP...) mà khách
                hàng có thể đăng ký. Mỗi <b>mức ưu tiên</b> (PriorityLevel) chỉ
                có tối đa <b>1 gói đang hoạt động</b> tại một thời điểm. Hệ
                thống cũng kiểm tra thêm quy tắc về <b>thứ tự giá</b> giữa các
                mức ưu tiên (gói ở level cao hơn phải có giá cao hơn level thấp
                hơn).
              </p>
            </div>
          </div>

          {/* Hàng filter + nút */}
          <div
            className="row"
            style={{
              gap: 10,
              marginTop: 12,
              alignItems: "flex-end",
              flexWrap: "nowrap",
            }}
          >
            {/* Cụm filter + Làm mới + Đặt lại (bên trái, chiếm rộng) */}
            <div
              className="row"
              style={{
                gap: 10,
                alignItems: "flex-end",
                flex: 1,
                flexWrap: "wrap",
              }}
            >
              <div className="group" style={{ width: 220 }}>
                <span>Mức ưu tiên</span>
                <select
                  value={query.priorityLevel}
                  onChange={(e) =>
                    setQuery((s) => ({
                      ...s,
                      priorityLevel: e.target.value,
                    }))
                  }
                >
                  <option value="">Tất cả</option>
                  {PRIORITY_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="group" style={{ width: 220 }}>
                <span>Trạng thái</span>
                <select
                  value={query.active}
                  onChange={(e) =>
                    setQuery((s) => ({ ...s, active: e.target.value }))
                  }
                >
                  <option value="">Tất cả</option>
                  <option value="true">Đang bật</option>
                  <option value="false">Đang tắt</option>
                </select>
              </div>

              {/* Nhóm Làm mới + Đặt lại nằm sát filter */}
              <div
                className="row"
                style={{
                  gap: 8,
                  alignItems: "flex-end",
                  flexShrink: 0,
                }}
              >
                {loading && <span className="badge gray">Đang tải…</span>}

                <button
                  className="btn ghost"
                  onClick={loadPlans}
                  disabled={loading}
                  title="Làm mới dữ liệu với bộ lọc hiện tại"
                >
                  Làm mới
                </button>

                <button
                  className="btn"
                  onClick={resetFilters}
                  title="Xoá bộ lọc"
                >
                  Đặt lại
                </button>
              </div>
            </div>

            {/* Nút Thêm gói nằm sát phải cùng hàng */}
            <button
              className="btn primary"
              style={{ flexShrink: 0, whiteSpace: "nowrap" }}
              onClick={openAddPlan}
            >
              Thêm gói hỗ trợ
            </button>
          </div>

          {/* Bảng plans */}
          <table className="table" style={{ marginTop: 10 }}>
            <thead>
              <tr>
                <th style={{ width: 80 }}>ID</th>
                <th style={{ minWidth: 260 }}>Gói hỗ trợ</th>
                <th style={{ width: 160 }}>Mức ưu tiên</th>
                <th style={{ width: 160 }}>Giá gói</th>
                <th style={{ width: 140 }}>Trạng thái</th>
                <th
                  style={{
                    width: 180,
                    textAlign: "right",      // căn phải cho khớp nút
                    paddingRight: 10,        // đẩy chữ sát về phía nút
                  }}
                >
                  Thao tác
                </th>
              </tr>
            </thead>
            <tbody>
              {plans.map((p) => (
                <tr key={p.supportPlanId}>
                  <td>
                    <EllipsisCell mono maxWidth={80} title={p.supportPlanId}>
                      {p.supportPlanId}
                    </EllipsisCell>
                  </td>
                  <td>
                    <div className="col">
                      <EllipsisCell maxWidth={260} title={p.name}>
                        <b>{p.name}</b>
                      </EllipsisCell>
                      {p.description && (
                        <EllipsisCell maxWidth={260} title={p.description}>
                          <span className="muted">{p.description}</span>
                        </EllipsisCell>
                      )}
                    </div>
                  </td>
                  <td>
                    <span className={priorityBadgeClass(p.priorityLevel)}>
                      {priorityLabel(p.priorityLevel)}
                    </span>
                  </td>
                  <td>
                    <EllipsisCell
                      mono
                      maxWidth={160}
                      title={formatCurrency(p.price)}
                    >
                      {formatCurrency(p.price)}
                    </EllipsisCell>
                  </td>
                  <td>
                    <button
                      type="button"
                      className="btn ghost status-btn"
                      onClick={() => togglePlanActive(p)}
                    >
                      <span
                        className={p.isActive ? "badge green" : "badge gray"}
                        style={{ textTransform: "none" }}
                      >
                        {p.isActive ? "Đang bật" : "Đang tắt"}
                      </span>
                    </button>
                  </td>
                  <td>
                    <div
                      className="row"
                      style={{ gap: 8, justifyContent: "flex-end" }}
                    >
                      <button
                        className="btn secondary"
                        onClick={() => openEditPlan(p)}
                      >
                        Sửa
                      </button>
                      <button
                        className="btn danger"
                        onClick={() => deletePlan(p)}
                      >
                        Xoá
                      </button>
                    </div>
                  </td>
                </tr>
              ))}

              {plans.length === 0 && !loading && (
                <tr>
                  <td colSpan={6} style={{ textAlign: "center", padding: 14 }}>
                    Chưa có gói hỗ trợ nào.
                  </td>
                </tr>
              )}

              {loading && (
                <tr>
                  <td colSpan={6} style={{ textAlign: "center", padding: 14 }}>
                    Đang tải dữ liệu…
                  </td>
                </tr>
              )}
            </tbody>
          </table>

          {/* Pagination */}
          <div className="pager">
            <div className="pager-left">
              <button
                className="pager-btn"
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                ‹ Trước
              </button>

              <span className="pager-info">
                Trang {page} / {totalPages}{" "}
                {total > 0 && (
                  <span className="muted">
                    (Tổng {total.toLocaleString("vi-VN")} gói)
                  </span>
                )}
              </span>

              <button
                className="pager-btn"
                disabled={page >= totalPages}
                onClick={() =>
                  setPage((p) => Math.min(totalPages, p + 1))
                }
              >
                Sau ›
              </button>
            </div>

            <div className="pager-right">
              <span className="muted">Mỗi trang:</span>
              <select
                value={pageSize}
                onChange={(e) => {
                  setPageSize(Number(e.target.value) || 10);
                  setPage(1);
                }}
              >
                {[5, 10, 20, 50].map((n) => (
                  <option key={n} value={n}>
                    {n}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>
      </div>

      <SupportPlanModal
        open={planModal.open}
        mode={planModal.mode}
        initial={planModal.data}
        onClose={() =>
          setPlanModal((m) => ({
            ...m,
            open: false,
          }))
        }
        onSubmit={handlePlanSubmit}
        submitting={planSubmitting}
        addToast={addToast}
        openConfirm={openConfirm}
      />

      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </>
  );
}
