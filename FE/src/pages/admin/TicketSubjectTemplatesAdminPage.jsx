// src/pages/admin/TicketSubjectTemplatesAdminPage.jsx
import React from "react";
import ToastContainer from "../../components/Toast/ToastContainer";
import { TicketSubjectTemplatesAdminApi } from "../../services/ticketSubjectTemplatesAdmin";
import "../../styles/TicketSubjectTemplatesAdminPage.css";

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

/* ============ Constants (Severity / Category) ============ */
// Phải khớp list fix-cứng trong TicketSubjectTemplatesAdminController
const SEVERITY_OPTIONS = [
  { value: "Low", label: "Low" },
  { value: "Medium", label: "Medium" },
  { value: "High", label: "High" },
  { value: "Critical", label: "Critical" },
];

// Map Category -> tiếng Việt (giống customer-ticket-create)
const CATEGORY_LABELS_VI = {
  Payment: "Thanh toán",
  Key: "Key / License",
  Account: "Tài khoản dịch vụ",
  Refund: "Hoàn tiền / đổi sản phẩm",
  Support: "Hỗ trợ kỹ thuật / cài đặt",
  Security: "Bảo mật / rủi ro",
  General: "Tư vấn / khác",
};

// Dropdown Category: value = code BE, label = tiếng Việt
const CATEGORY_OPTIONS = [
  { value: "General", label: CATEGORY_LABELS_VI.General },
  { value: "Payment", label: CATEGORY_LABELS_VI.Payment },
  { value: "Key", label: CATEGORY_LABELS_VI.Key },
  { value: "Account", label: CATEGORY_LABELS_VI.Account },
  { value: "Refund", label: CATEGORY_LABELS_VI.Refund },
  { value: "Support", label: CATEGORY_LABELS_VI.Support },
  { value: "Security", label: CATEGORY_LABELS_VI.Security },
];

const severityBadgeClass = (severity) => {
  const s = (severity || "").toLowerCase();
  if (s === "low") return "badge sev-low";
  if (s === "medium") return "badge sev-medium";
  if (s === "high") return "badge sev-high";
  if (s === "critical") return "badge sev-critical";
  return "badge sev-low";
};

/* ============ Modal: Ticket Subject Template (Add / Edit) ============ */
function TicketSubjectTemplateModal({
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
    templateCode: "",
    title: "",
    severity: "",
    category: "General", // mặc định General
    isActive: true,
  });
  const [errors, setErrors] = React.useState({});
  const initialRef = React.useRef(null);

  React.useEffect(() => {
    if (open) {
      const base =
        initial || {
          templateCode: "",
          title: "",
          severity: "",
          category: "General",
          isActive: true,
        };

      const next = {
        templateCode:
          base.templateCode === null || base.templateCode === undefined
            ? ""
            : String(base.templateCode),
        title:
          base.title === null || base.title === undefined
            ? ""
            : String(base.title),
        severity:
          base.severity === null || base.severity === undefined
            ? ""
            : String(base.severity),
        category:
          base.category === null ||
          base.category === undefined ||
          base.category === ""
            ? "General"
            : String(base.category),
        isActive: typeof base.isActive === "boolean" ? base.isActive : true,
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

    const codeRaw = (form.templateCode || "").toString().trim();
    const titleRaw = (form.title || "").toString().trim();
    const severityRaw = (form.severity || "").toString().trim();
    const categoryRaw = (form.category || "").toString().trim();

    if (!isEdit) {
      if (!codeRaw) {
        e.templateCode = "Mã mẫu chủ đề không được để trống.";
      } else if (codeRaw.length > 50) {
        e.templateCode = "Mã mẫu chủ đề không được vượt quá 50 ký tự.";
      } else if (!/^[A-Za-z0-9_\-]+$/.test(codeRaw)) {
        e.templateCode =
          "Mã mẫu chủ đề chỉ được chứa chữ, số, dấu - và _, không chứa khoảng trắng.";
      }
    }

    if (!titleRaw) {
      e.title = "Tiêu đề không được để trống.";
    } else if (titleRaw.length > 200) {
      e.title = "Tiêu đề không được vượt quá 200 ký tự.";
    }

    if (!severityRaw) {
      e.severity = "Độ ưu tiên (Severity) là bắt buộc.";
    } else if (severityRaw.length > 10) {
      e.severity = "Severity không được vượt quá 10 ký tự.";
    } else if (
      !SEVERITY_OPTIONS.some(
        (opt) => opt.value.toLowerCase() === severityRaw.toLowerCase()
      )
    ) {
      e.severity = "Severity không hợp lệ.";
    }

    if (categoryRaw && categoryRaw.length > 100) {
      e.category = "Category không được vượt quá 100 ký tự.";
    } else if (
      categoryRaw &&
      !CATEGORY_OPTIONS.some(
        (opt) =>
          opt.value &&
          opt.value.toLowerCase() === categoryRaw.toLowerCase()
      )
    ) {
      e.category = "Category không hợp lệ.";
    }

    setErrors(e);
    if (Object.keys(e).length > 0 && typeof addToast === "function") {
      addToast(
        "warning",
        "Vui lòng kiểm tra các trường được đánh dấu.",
        isEdit
          ? "Dữ liệu mẫu chủ đề chưa hợp lệ"
          : "Dữ liệu mẫu chủ đề chưa hợp lệ"
      );
    }
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async (evt) => {
    evt.preventDefault();
    if (!validate()) return;

    const codeRaw = (form.templateCode || "").toString().trim();
    const titleRaw = (form.title || "").toString().trim();
    const severityRaw = (form.severity || "").toString().trim();
    const categoryRaw = (form.category || "").toString().trim();

    const payload = {
      templateCode: codeRaw || undefined,
      title: titleRaw,
      severity: severityRaw,
      category: categoryRaw || null,
      isActive: !!form.isActive,
    };

    await onSubmit?.(payload);
  };

  const handleClose = () => {
    if (isDirty) {
      if (typeof openConfirm === "function") {
        openConfirm({
          title: "Đóng cửa sổ?",
          message:
            "Bạn có các thay đổi mẫu chủ đề chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này.",
          onConfirm: () => {
            onClose?.();
          },
        });
      } else {
        const ok = window.confirm(
          "Bạn có các thay đổi mẫu chủ đề chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này. Bạn có chắc muốn thoát?"
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
          <h3>
            {isEdit
              ? "Chỉnh sửa mẫu chủ đề phiếu hỗ trợ"
              : "Thêm mẫu chủ đề phiếu hỗ trợ"}
          </h3>
          <div className="group" style={{ marginTop: 8 }}>
            <div className="row" style={{ gap: 8, alignItems: "center" }}>
              <label className="switch" title="Bật/Tắt mẫu chủ đề">
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
                {form.isActive ? "Đang bật" : "Đang tắt"}
              </span>
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="cat-modal-body input-group">
            <div className="row" style={{ gap: 16 }}>
              <div className="group" style={{ width: 260 }}>
                <span>
                  Mã mẫu chủ đề <RequiredMark />
                </span>
                <input
                  type="text"
                  value={form.templateCode}
                  onChange={(e) => set("templateCode", e.target.value)}
                  placeholder="VD: PAYMENT_REFUND, ACCOUNT_LOGIN_ISSUE..."
                  disabled={isEdit}
                />
                <FieldError message={errors.templateCode} />
                {isEdit && (
                  <span className="muted">
                    Mã mẫu chủ đề không thể thay đổi sau khi tạo.
                  </span>
                )}
              </div>

              <div className="group" style={{ flex: 1 }}>
                <span>
                  Tiêu đề <RequiredMark />
                </span>
                <input
                  type="text"
                  value={form.title}
                  onChange={(e) => set("title", e.target.value)}
                  placeholder="VD: Yêu cầu hoàn tiền, Vấn đề đăng nhập tài khoản..."
                />
                <FieldError message={errors.title} />
              </div>
            </div>

            <div className="row" style={{ gap: 16, marginTop: 12 }}>
              <div className="group" style={{ width: 220 }}>
                <span>
                  Độ ưu tiên (Severity) <RequiredMark />
                </span>
                <select
                  value={form.severity}
                  onChange={(e) => set("severity", e.target.value)}
                >
                  <option value="">-- Chọn Severity --</option>
                  {SEVERITY_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
                <FieldError message={errors.severity} />
              </div>

              <div className="group" style={{ width: 220 }}>
                <span>Nhóm vấn đề (Category)</span>
                <select
                  value={form.category}
                  onChange={(e) => set("category", e.target.value)}
                >
                  {CATEGORY_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
                <FieldError message={errors.category} />
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
                : "Tạo mẫu chủ đề mới"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ============ Page: TicketSubjectTemplatesAdminPage ============ */
export default function TicketSubjectTemplatesAdminPage() {
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
    keyword: "",
    severity: "",
    category: "",
    active: "",
    sort: "templateCode",
    direction: "asc",
  });
  const [templates, setTemplates] = React.useState([]);
  const [loading, setLoading] = React.useState(false);
  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(10);
  const [total, setTotal] = React.useState(0);

  const [modalState, setModalState] = React.useState({
    open: false,
    mode: "add",
    data: null,
  });
  const [submitting, setSubmitting] = React.useState(false);

  const loadTemplates = React.useCallback(() => {
    setLoading(true);

    const params = {
      keyword: query.keyword || undefined,
      severity: query.severity || undefined,
      category: query.category || undefined,
      active:
        query.active === ""
          ? undefined
          : query.active === "true"
          ? true
          : false,
      sort: query.sort || "templateCode",
      direction: query.direction || "asc",
      page,
      pageSize,
    };

    TicketSubjectTemplatesAdminApi.listPaged(params)
      .then((res) => {
        const items = Array.isArray(res?.items)
          ? res.items
          : Array.isArray(res)
          ? res
          : [];
        setTemplates(items);
        setPage(typeof res?.page === "number" ? res.page : page);
        setPageSize(
          typeof res?.pageSize === "number" ? res.pageSize : pageSize
        );
        setTotal(typeof res?.total === "number" ? res.total : items.length);
      })
      .catch((err) => {
        console.error(err);
        addToast(
          "error",
          "Không tải được danh sách mẫu chủ đề phiếu hỗ trợ.",
          "Lỗi"
        );
      })
      .finally(() => setLoading(false));
  }, [query, page, pageSize]);

  React.useEffect(() => {
    const t = setTimeout(loadTemplates, 300);
    return () => clearTimeout(t);
  }, [loadTemplates]);

  const totalPages = React.useMemo(
    () => Math.max(1, Math.ceil((total || 0) / (pageSize || 1))),
    [total, pageSize]
  );

  const openAdd = () =>
    setModalState({
      open: true,
      mode: "add",
      data: null,
    });

  const openEdit = async (tpl) => {
    try {
      const detail = await TicketSubjectTemplatesAdminApi.get(
        tpl.templateCode
      );
      setModalState({
        open: true,
        mode: "edit",
        data: detail,
      });
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message ||
          "Không tải được chi tiết mẫu chủ đề để chỉnh sửa.",
        "Lỗi"
      );
    }
  };

  const handleSubmit = async (payload) => {
    setSubmitting(true);
    try {
      if (modalState.mode === "add") {
        await TicketSubjectTemplatesAdminApi.create(payload);
        addToast(
          "success",
          "Đã tạo mẫu chủ đề phiếu hỗ trợ mới.",
          "Thành công"
        );
      } else if (modalState.mode === "edit" && modalState.data) {
        await TicketSubjectTemplatesAdminApi.update(
          modalState.data.templateCode,
          {
            title: payload.title,
            severity: payload.severity,
            category: payload.category,
            isActive: payload.isActive,
          }
        );
        addToast(
          "success",
          "Đã cập nhật mẫu chủ đề phiếu hỗ trợ.",
          "Thành công"
        );
      }

      setModalState((m) => ({ ...m, open: false }));
      loadTemplates();
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message || "Lưu mẫu chủ đề phiếu hỗ trợ thất bại.",
        "Lỗi"
      );
    } finally {
      setSubmitting(false);
    }
  };

  const toggleActive = async (tpl) => {
    try {
      await TicketSubjectTemplatesAdminApi.toggle(tpl.templateCode);
      addToast("success", "Đã cập nhật trạng thái mẫu chủ đề.", "Thành công");
      loadTemplates();
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message ||
          "Không thể cập nhật trạng thái mẫu chủ đề.",
        "Lỗi"
      );
    }
  };

  const deleteTemplate = (tpl) => {
    openConfirm({
      title: "Xoá mẫu chủ đề phiếu hỗ trợ?",
      message: `Xoá mẫu chủ đề "${tpl.title}" (mã: ${tpl.templateCode})? Hành động này không thể hoàn tác!`,
      onConfirm: async () => {
        try {
          await TicketSubjectTemplatesAdminApi.remove(tpl.templateCode);
          addToast(
            "success",
            "Đã xoá mẫu chủ đề phiếu hỗ trợ.",
            "Thành công"
          );
          loadTemplates();
        } catch (e) {
          console.error(e);
          addToast(
            "error",
            e?.response?.data?.message ||
              "Xoá mẫu chủ đề phiếu hỗ trợ thất bại.",
            "Lỗi"
          );
        }
      },
    });
  };

  const resetFilters = () => {
    setQuery({
      keyword: "",
      severity: "",
      category: "",
      active: "",
      sort: "templateCode",
      direction: "asc",
    });
    setPage(1);
  };

  return (
    <>
      <div className="page ticket-templates-page">
        <div className="card">
          <div className="card-header">
            <div className="left">
              <h2>Cấu hình mẫu chủ đề phiếu hỗ trợ</h2>
              <p className="muted">
                Quản lý danh sách chủ đề có sẵn khi khách tạo phiếu hỗ trợ
                (ticket). Mỗi mẫu chủ đề gồm mã cố định, tiêu đề hiển thị, độ ưu
                tiên (Severity) và nhóm vấn đề (Category) hiển thị bằng tiếng
                Việt giống màn tạo ticket.
              </p>
            </div>
          </div>

          {/* Hàng filter + nút (style giống SLA Rule) */}
          <div
            className="row"
            style={{
              gap: 10,
              marginTop: 12,
              alignItems: "flex-end",
              flexWrap: "nowrap",
            }}
          >
            {/* Cụm filter + Làm mới + Đặt lại (bên trái) */}
            <div
              className="row"
              style={{
                gap: 10,
                alignItems: "flex-end",
                flex: 1,
                flexWrap: "wrap",
              }}
            >
              <div className="group" style={{ width: 260 }}>
                <span>Tìm theo mã / tiêu đề / nhóm vấn đề</span>
                <input
                  type="text"
                  value={query.keyword}
                  onChange={(e) =>
                    setQuery((s) => ({ ...s, keyword: e.target.value }))
                  }
                  placeholder="Nhập keyword..."
                />
              </div>

              <div className="group" style={{ width: 180 }}>
                <span>Severity</span>
                <select
                  value={query.severity}
                  onChange={(e) =>
                    setQuery((s) => ({ ...s, severity: e.target.value }))
                  }
                >
                  <option value="">Tất cả</option>
                  {SEVERITY_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="group" style={{ width: 200 }}>
                <span>Nhóm vấn đề</span>
                <select
                  value={query.category}
                  onChange={(e) =>
                    setQuery((s) => ({ ...s, category: e.target.value }))
                  }
                >
                  <option value="">Tất cả</option>
                  {CATEGORY_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="group" style={{ width: 160 }}>
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
                  onClick={loadTemplates}
                  disabled={loading}
                  title="Làm mới dữ liệu với bộ lọc hiện tại"
                >
                  Làm mới
                </button>

                <button
                  className="btn secondary"
                  onClick={resetFilters}
                  title="Xoá bộ lọc"
                >
                  Đặt lại
                </button>
              </div>
            </div>

            {/* Nút Thêm template nằm sát phải giống Thêm SLA rule */}
            <button
              className="btn primary"
              style={{ flexShrink: 0, whiteSpace: "nowrap" }}
              onClick={openAdd}
            >
              Thêm mẫu chủ đề
            </button>
          </div>

          {/* Bảng templates – style giống SLA Rule */}
          <table className="table" style={{ marginTop: 10 }}>
            <thead>
              <tr>
                <th style={{ width: 160 }}>Mã mẫu chủ đề</th>
                <th style={{ minWidth: 260 }}>Tiêu đề</th>
                <th style={{ width: 130 }}>Severity</th>
                <th style={{ width: 200 }}>Nhóm vấn đề</th>
                <th style={{ width: 130 }}>Trạng thái</th>
                <th
                  style={{
                    width: 180,
                    textAlign: "right",
                    paddingRight: 10,
                  }}
                >
                  Thao tác
                </th>
              </tr>
            </thead>
            <tbody>
              {templates.map((t) => {
                const rawCat = t.category || "";
                const viLabel = rawCat
                  ? CATEGORY_LABELS_VI[rawCat] || rawCat
                  : "Không có";

                return (
                  <tr key={t.templateCode}>
                    <td>
                      <EllipsisCell
                        mono
                        maxWidth={160}
                        title={t.templateCode}
                      >
                        {t.templateCode}
                      </EllipsisCell>
                    </td>
                    <td>
                      <EllipsisCell maxWidth={260} title={t.title}>
                        <b>{t.title}</b>
                      </EllipsisCell>
                    </td>
                    <td>
                      <span className={severityBadgeClass(t.severity)}>
                        {t.severity}
                      </span>
                    </td>
                    <td>
                      <EllipsisCell maxWidth={200} title={viLabel}>
                        {rawCat ? (
                          <>
                            <span>{viLabel}</span>
                            <span className="muted" style={{ marginLeft: 4 }}>
                              ({rawCat})
                            </span>
                          </>
                        ) : (
                          <span className="muted">Không có</span>
                        )}
                      </EllipsisCell>
                    </td>

                    {/* Trạng thái giống SLA: badge Hiển thị / Ẩn */}
                    <td>
                      <span
                        className={t.isActive ? "badge green" : "badge gray"}
                        style={{ textTransform: "none" }}
                      >
                        {t.isActive ? "Hiển thị" : "Ẩn"}
                      </span>
                    </td>

                    {/* Thao tác: Switch + icon Sửa/Xoá giống SLA Rule */}
                    <td>
                      <div className="action-buttons">
                        <label
                          className="switch"
                          title={t.isActive ? "Đang bật" : "Đang tắt"}
                        >
                          <input
                            type="checkbox"
                            checked={!!t.isActive}
                            onChange={() => toggleActive(t)}
                          />
                          <span className="slider" />
                        </label>

                        <button
                          type="button"
                          className="action-btn edit-btn"
                          onClick={() => openEdit(t)}
                          title="Chỉnh sửa mẫu chủ đề"
                        >
                          {/* icon bút chì */}
                          <svg
                            xmlns="http://www.w3.org/2000/svg"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                          >
                            <path d="M12 20h9" />
                            <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z" />
                          </svg>
                        </button>

                        <button
                          type="button"
                          className="action-btn delete-btn"
                          onClick={() => deleteTemplate(t)}
                          title="Xoá mẫu chủ đề"
                        >
                          {/* icon thùng rác */}
                          <svg
                            xmlns="http://www.w3.org/2000/svg"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                          >
                            <polyline points="3 6 5 6 21 6" />
                            <path d="M19 6l-1 14H6L5 6" />
                            <path d="M10 11v6" />
                            <path d="M14 11v6" />
                            <path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2" />
                          </svg>
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}

              {templates.length === 0 && !loading && (
                <tr>
                  <td colSpan={6} style={{ textAlign: "center", padding: 14 }}>
                    Chưa có mẫu chủ đề phiếu hỗ trợ nào.
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

          {/* Pagination giống SLA Rule */}
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
                    (Tổng {total.toLocaleString("vi-VN")} mẫu chủ đề)
                  </span>
                )}
              </span>

              <button
                className="pager-btn"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
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

      <TicketSubjectTemplateModal
        open={modalState.open}
        mode={modalState.mode}
        initial={modalState.data}
        onClose={() =>
          setModalState((m) => ({
            ...m,
            open: false,
          }))
        }
        onSubmit={handleSubmit}
        submitting={submitting}
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
