// src/pages/admin/SlaRulesAdminPage.jsx
import React from "react";
import ToastContainer from "../../components/Toast/ToastContainer";
import { SlaRulesAdminApi } from "../../services/slaRulesAdmin";
import PermissionGuard from "../../components/PermissionGuard";
import { usePermission } from "../../hooks/usePermission";
import useToast from "../../hooks/useToast";
import "../../styles/SlaRulesAdminPage.css";

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
    style={{
      maxWidth,
      whiteSpace: "nowrap",
      overflow: "hidden",
      textOverflow: "ellipsis",
    }}
    title={title}
  >
    {children}
  </div>
);

/* ============ Constant options ============ */

const SEVERITY_OPTIONS = [
  { value: "Low", label: "Low" },
  { value: "Medium", label: "Medium" },
  { value: "High", label: "High" },
  { value: "Critical", label: "Critical" },
];

const PRIORITY_OPTIONS = [
  { value: 0, label: "Standard (0)" },
  { value: 1, label: "Priority (1)" },
  { value: 2, label: "VIP (2)" },
];

const severityLabel = (value) => {
  const v = String(value || "").trim();
  if (!v) return "";
  const opt = SEVERITY_OPTIONS.find((o) => o.value === v);
  return opt ? opt.label : v;
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
    case 0:
    default:
      return "badge level-other";
  }
};

const severityBadgeClass = (severity) => {
  const v = String(severity || "").toLowerCase();
  switch (v) {
    case "low":
      return "badge sev-low";
    case "medium":
      return "badge sev-medium";
    case "high":
      return "badge sev-high";
    case "critical":
      return "badge sev-critical";
    default:
      return "badge";
  }
};

// Hiển thị phút + nếu >= 60 thì hiển thị thêm giờ, làm tròn 1 chữ số sau thập phân khi lẻ
const formatMinutes = (value) => {
  if (value === null || value === undefined) return "";
  const n = Number(value);
  if (!Number.isFinite(n)) return String(value);

  if (n < 60) {
    return `${n.toLocaleString("vi-VN")} phút`;
  }

  const hours = n / 60;
  const rounded = Math.round(hours * 10) / 10;
  const hoursStr = Number.isInteger(rounded)
    ? rounded.toLocaleString("vi-VN")
    : rounded.toLocaleString("vi-VN", {
        minimumFractionDigits: 1,
        maximumFractionDigits: 1,
      });

  return `${n.toLocaleString("vi-VN")} phút (${hoursStr} giờ)`;
};

/* ============ Modal: SLA Rule (Add / Edit) ============ */
function SlaRuleModal({
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
    severity: "",
    priorityLevel: "",
    firstResponseMinutes: "",
    resolutionMinutes: "",
    isActive: true,
  });

  const [errors, setErrors] = React.useState({});
  const [isDirty, setIsDirty] = React.useState(false);

  const setField = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    setIsDirty(true);
  };

  React.useEffect(() => {
    if (!open) return;

    if (initial) {
      setForm({
        name: initial.name ?? "",
        severity: initial.severity ?? "",
        priorityLevel:
          initial.priorityLevel === 0 || initial.priorityLevel
            ? String(initial.priorityLevel)
            : "",
        firstResponseMinutes:
          initial.firstResponseMinutes === 0 || initial.firstResponseMinutes
            ? String(initial.firstResponseMinutes)
            : "",
        resolutionMinutes:
          initial.resolutionMinutes === 0 || initial.resolutionMinutes
            ? String(initial.resolutionMinutes)
            : "",
        isActive: !!initial.isActive,
      });
    } else {
      setForm({
        name: "",
        severity: "",
        priorityLevel: "",
        firstResponseMinutes: "",
        resolutionMinutes: "",
        isActive: true,
      });
    }

    setErrors({});
    setIsDirty(false);
  }, [open, initial]);

  const validate = () => {
    const nextErrors = {};
    const nameRaw = (form.name || "").trim();
    const severityRaw = (form.severity || "").trim();

    if (!nameRaw) {
      nextErrors.name = "Tên SLA rule không được để trống.";
    } else if (nameRaw.length > 120) {
      nextErrors.name = "Tên SLA rule không được vượt quá 120 ký tự.";
    }

    if (!severityRaw) {
      nextErrors.severity = "Mức độ (Severity) không được để trống.";
    } else if (
      !SEVERITY_OPTIONS.some(
        (o) => o.value.toLowerCase() === severityRaw.toLowerCase()
      )
    ) {
      nextErrors.severity =
        "Mức độ (Severity) không hợp lệ. Chỉ được chọn Low / Medium / High / Critical.";
    }

    if (form.priorityLevel === "") {
      nextErrors.priorityLevel = "Mức ưu tiên không được để trống.";
    } else {
      const n = Number(form.priorityLevel);
      if (!Number.isFinite(n) || n < 0) {
        nextErrors.priorityLevel =
          "Mức ưu tiên (PriorityLevel) phải là số không âm.";
      }
    }

    const fr = Number(form.firstResponseMinutes);
    if (!Number.isFinite(fr) || fr <= 0) {
      nextErrors.firstResponseMinutes =
        "Thời gian phản hồi đầu tiên (phút) phải lớn hơn 0.";
    }

    const rs = Number(form.resolutionMinutes);
    if (!Number.isFinite(rs) || rs <= 0) {
      nextErrors.resolutionMinutes =
        "Thời gian xử lý (phút) phải lớn hơn 0.";
    }

    if (Number.isFinite(fr) && Number.isFinite(rs) && rs < fr) {
      nextErrors.resolutionMinutes =
        "Thời gian xử lý phải lớn hơn hoặc bằng thời gian phản hồi đầu tiên.";
    }

    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    if (submitting) return;

    if (!validate()) {
      if (typeof addToast === "function") {
        addToast(
          "error",
          "Vui lòng kiểm tra lại các trường dữ liệu của SLA rule.",
          "Lỗi"
        );
      }
      return;
    }

    const nameRaw = (form.name || "").trim();
    const severityRaw = (form.severity || "").trim();
    const priorityLevel = Number(form.priorityLevel || 0) || 0;
    const firstResponseMinutes = Number(form.firstResponseMinutes || 0) || 0;
    const resolutionMinutes = Number(form.resolutionMinutes || 0) || 0;

    onSubmit?.({
      name: nameRaw,
      severity: severityRaw,
      priorityLevel,
      firstResponseMinutes,
      resolutionMinutes,
      isActive: !!form.isActive,
    });
  };

  const handleClose = () => {
    if (isDirty && typeof openConfirm === "function") {
      openConfirm({
        title: "Đóng cửa sổ?",
        message:
          "Bạn có các thay đổi SLA rule chưa lưu. Đóng cửa sổ sẽ mất các thay đổi này.",
        onConfirm: () => {
          setIsDirty(false);
          onClose?.();
        },
      });
      return;
    }
    onClose?.();
  };

  if (!open) return null;

  return (
    <div className="sla-modal-backdrop">
      <div className="sla-modal-card">
        <div className="sla-modal-header">
          <h3>{isEdit ? "Chỉnh sửa SLA rule" : "Thêm SLA rule"}</h3>
          <div className="group" style={{ marginTop: 8 }}>
            <div className="row" style={{ gap: 8, alignItems: "center" }}>
              <label className="switch" title="Bật/Tắt SLA rule">
                <input
                  type="checkbox"
                  checked={!!form.isActive}
                  onChange={() => setField("isActive", !form.isActive)}
                />
                <span className="slider" />
                <span className="switch-label">
                  {form.isActive ? "Đang bật" : "Đang tắt"}
                </span>
              </label>
              <div className="muted sla-modal-note">
                <strong>Quy tắc khi bật SLA rule:</strong>
                <div>- Mỗi cặp Severity + PriorityLevel chỉ có 1 rule đang bật.</div>
                <div>
                  - Cùng Severity: PriorityLevel cao hơn phải có thời gian phản hồi /
                  xử lý <b>ngắn hơn</b> PriorityLevel thấp hơn.
                </div>
                <div>
                  - Cùng PriorityLevel: Severity nghiêm trọng hơn (Low → Medium → High
                  → Critical) phải có thời gian phản hồi / xử lý <b>ngắn hơn</b>.
                </div>
              </div>
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
          <div className="sla-modal-body input-group">
            <div className="row" style={{ gap: 16 }}>
              <div className="group" style={{ flex: 1 }}>
                <span>
                  Tên SLA rule <RequiredMark />
                </span>
                <input
                  type="text"
                  value={form.name}
                  onChange={(e) => setField("name", e.target.value)}
                  placeholder="VD: SLA cho ticket High + VIP"
                />
                <FieldError message={errors.name} />
              </div>

              <div className="group" style={{ width: 240 }}>
                <span>
                  Mức độ (Severity) <RequiredMark />
                </span>
                <select
                  value={form.severity}
                  onChange={(e) => setField("severity", e.target.value)}
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
            </div>

            <div className="row" style={{ gap: 16, marginTop: 12 }}>
              <div className="group" style={{ width: 220 }}>
                <span>
                  Mức ưu tiên (PriorityLevel) <RequiredMark />
                </span>
                <select
                  value={form.priorityLevel}
                  onChange={(e) => setField("priorityLevel", e.target.value)}
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

              <div className="group" style={{ width: 220 }}>
                <span>
                  Thời gian phản hồi đầu tiên (phút) <RequiredMark />
                </span>
                <input
                  type="number"
                  min={5}
                  step={5}
                  value={form.firstResponseMinutes}
                  onChange={(e) =>
                    setField("firstResponseMinutes", e.target.value)
                  }
                  placeholder="VD: 30"
                />
                <FieldError message={errors.firstResponseMinutes} />
              </div>

              <div className="group" style={{ width: 220 }}>
                <span>
                  Thời gian xử lý / giải quyết (phút) <RequiredMark />
                </span>
                <input
                  type="number"
                  min={10}
                  step={5}
                  value={form.resolutionMinutes}
                  onChange={(e) =>
                    setField("resolutionMinutes", e.target.value)
                  }
                  placeholder="VD: 240"
                />
                <FieldError message={errors.resolutionMinutes} />
              </div>
            </div>
          </div>

          <div className="sla-modal-footer">
            <button
              type="button"
              className="btn"
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
                : "Tạo SLA rule"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ============ Page: SlaRulesAdminPage ============ */
export default function SlaRulesAdminPage() {
  const { showError } = useToast();
  const { hasPermission: hasCreatePermission } = usePermission("SUPPORT_MANAGER", "CREATE");
  const { hasPermission: hasEditPermission } = usePermission("SUPPORT_MANAGER", "EDIT");
  const { hasPermission: hasDeletePermission } = usePermission("SUPPORT_MANAGER", "DELETE");

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
    severity: "",
    priorityLevel: "",
    active: "",
    sort: "severity",
    direction: "asc",
  });

  const [rules, setRules] = React.useState([]);
  const [loading, setLoading] = React.useState(false);
  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(12);
  const [total, setTotal] = React.useState(0);

  const loadRules = React.useCallback(() => {
    setLoading(true);

    const params = {
      severity: query.severity || undefined,
      priorityLevel:
        query.priorityLevel === "" ? undefined : Number(query.priorityLevel),
      active: query.active === "" ? undefined : query.active === "true",
      sort: query.sort || "severity",
      direction: query.direction || "asc",
      page,
      pageSize,
    };

    SlaRulesAdminApi.listPaged(params)
      .then((res) => {
        const items = Array.isArray(res?.items)
          ? res.items
          : Array.isArray(res)
          ? res
          : [];
        setRules(items);
        setPage(typeof res?.page === "number" ? res.page : page);
        setPageSize(
          typeof res?.pageSize === "number" ? res.pageSize : pageSize
        );
        setTotal(typeof res?.total === "number" ? res.total : items.length);
      })
      .catch((err) => {
        console.error(err);
        addToast("error", "Không tải được danh sách SLA rule.", "Lỗi");
      })
      .finally(() => setLoading(false));
  }, [query, page, pageSize]);

  React.useEffect(() => {
    const t = setTimeout(loadRules, 300);
    return () => clearTimeout(t);
  }, [loadRules]);

  const totalPages = React.useMemo(
    () => Math.max(1, Math.ceil((total || 0) / (pageSize || 1))),
    [total, pageSize]
  );

  const resetFilters = () => {
    setQuery({
      severity: "",
      priorityLevel: "",
      active: "",
      sort: "severity",
      direction: "asc",
    });
    setPage(1);
  };

  const [ruleModal, setRuleModal] = React.useState({
    open: false,
    mode: "add",
    data: null,
  });
  const [ruleSubmitting, setRuleSubmitting] = React.useState(false);

  const openAddRule = () => {
    if (!hasCreatePermission) {
      showError("Không có quyền", "Bạn không có quyền tạo SLA rule");
      return;
    }
    setRuleModal({ open: true, mode: "add", data: null });
  };

  const openEditRule = async (r) => {
    if (!hasEditPermission) {
      showError("Không có quyền", "Bạn không có quyền sửa SLA rule");
      return;
    }
    try {
      const detail = await SlaRulesAdminApi.get(r.slaRuleId);
      setRuleModal({
        open: true,
        mode: "edit",
        data: detail,
      });
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message ||
          "Không tải được chi tiết SLA rule để chỉnh sửa.",
        "Lỗi"
      );
    }
  };

  const handleRuleSubmit = async (payload) => {
    if (ruleModal.mode === "add" && !hasCreatePermission) {
      showError("Không có quyền", "Bạn không có quyền tạo SLA rule");
      return;
    }
    if (ruleModal.mode === "edit" && !hasEditPermission) {
      showError("Không có quyền", "Bạn không có quyền sửa SLA rule");
      return;
    }
    setRuleSubmitting(true);
    try {
      if (ruleModal.mode === "add") {
        await SlaRulesAdminApi.create(payload);
        addToast("success", "Đã tạo SLA rule mới.", "Thành công");
      } else if (ruleModal.mode === "edit" && ruleModal.data) {
        await SlaRulesAdminApi.update(ruleModal.data.slaRuleId, payload);
        addToast("success", "Đã cập nhật SLA rule.", "Thành công");
      }

      setRuleModal((m) => ({ ...m, open: false }));
      loadRules();
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message ||
          "Không thể lưu SLA rule. Vui lòng thử lại.",
        "Lỗi"
      );
    } finally {
      setRuleSubmitting(false);
    }
  };

  const toggleRuleActive = async (r) => {
    if (!hasEditPermission) {
      showError("Không có quyền", "Bạn không có quyền thay đổi trạng thái SLA rule");
      return;
    }
    try {
      await SlaRulesAdminApi.toggle(r.slaRuleId);
      addToast("success", "Đã cập nhật trạng thái SLA rule.", "Thành công");
      loadRules();
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message ||
          "Không thể cập nhật trạng thái SLA rule.",
        "Lỗi"
      );
    }
  };

  const deleteRule = (r) => {
    if (!hasDeletePermission) {
      showError("Không có quyền", "Bạn không có quyền xóa SLA rule");
      return;
    }
    openConfirm({
      title: "Xoá SLA rule?",
      message: `Xoá SLA rule "${r.name}" (Severity: ${severityLabel(
        r.severity
      )}, Priority: ${priorityLabel(
        r.priorityLevel
      )})? Hành động này không thể hoàn tác!`,
      onConfirm: async () => {
        try {
          await SlaRulesAdminApi.remove(r.slaRuleId);
          addToast("success", "Đã xoá SLA rule.", "Thành công");
          loadRules();
        } catch (e) {
          console.error(e);
          addToast(
            "error",
            e?.response?.data?.message ||
              "Không thể xoá SLA rule. Có thể rule đang được tham chiếu bởi ticket.",
            "Lỗi"
          );
        }
      },
    });
  };

  return (
    <>
      <div className="page sla-rules-page">
        <div className="card">
          <div className="card-header">
            <div className="left">
              <h2>Cấu hình SLA Rule</h2>
              <p className="muted">
                Thiết lập thời gian phản hồi / xử lý tối đa cho từng combination:
                Severity + PriorityLevel.
              </p>
            </div>
          </div>

          {/* Ghi chú luật SLA */}
          <div className="sla-rules-note">
            <div className="sla-rules-note-title">
              Luật khi tạo & bật SLA rule:
            </div>
            <ul>
              <li>
                Cho phép lưu nhiều rule trùng <b>Severity + PriorityLevel</b>,
                nhưng mỗi cặp chỉ có <b>1 rule đang bật</b>. Khi bật rule mới,
                các rule khác cùng cặp sẽ tự tắt.
              </li>
              <li>
                Cùng <b>Severity</b>: PriorityLevel cao hơn phải có thời gian phản
                hồi / giải quyết <b>ngắn hơn</b> PriorityLevel thấp hơn.
              </li>
              <li>
                Cùng <b>PriorityLevel</b>: Severity nghiêm trọng hơn (Low → Medium
                → High → Critical) phải có thời gian phản hồi / giải quyết{" "}
                <b>ngắn hơn</b> mức ít nghiêm trọng hơn.
              </li>
              <li>
                Các rule đang <b>tắt</b> không bị ràng buộc bởi luật thời gian; hệ
                thống chỉ kiểm tra khi lưu / bật rule ở trạng thái hoạt động.
              </li>
            </ul>
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
                gap: 12,
                flex: 1,
                minWidth: 0,
                alignItems: "flex-end",
              }}
            >
              <div className="group" style={{ width: 220 }}>
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
                  onClick={loadRules}
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

            {/* Nút Thêm SLA nằm sát phải cùng hàng */}
            <PermissionGuard moduleCode="SUPPORT_MANAGER" permissionCode="CREATE" fallback={
              <button
                className="btn primary disabled"
                style={{ flexShrink: 0, whiteSpace: "nowrap" }}
                disabled
                title="Bạn không có quyền tạo SLA rule"
              >
                Thêm SLA rule
              </button>
            }>
              <button
                className="btn primary"
                style={{ flexShrink: 0, whiteSpace: "nowrap" }}
                onClick={openAddRule}
              >
                Thêm SLA rule
              </button>
            </PermissionGuard>
          </div>

          {/* Bảng SLA rules */}
          <table className="table" style={{ marginTop: 10 }}>
            <thead>
              <tr>
                <th style={{ width: 80 }}>ID</th>
                <th style={{ minWidth: 220 }}>Tên SLA rule</th>
                <th style={{ width: 120 }}>Severity</th>
                <th style={{ width: 150 }}>Mức ưu tiên</th>
                <th style={{ width: 180 }}>Phản hồi đầu tiên</th>
                <th style={{ width: 180 }}>Xử lý / giải quyết</th>
                <th style={{ width: 120 }}>Trạng thái</th>
                <th
                  style={{
                    width: 160,
                    paddingRight: 10,
                  }}
                >
                  Thao tác
                </th>
              </tr>
            </thead>
            <tbody>
              {rules.map((r) => (
                <tr key={r.slaRuleId}>
                  <td>
                    <EllipsisCell mono maxWidth={80} title={r.slaRuleId}>
                      {r.slaRuleId}
                    </EllipsisCell>
                  </td>
                  <td>
                    <div className="col">
                      <EllipsisCell maxWidth={260} title={r.name}>
                        <b>{r.name}</b>
                      </EllipsisCell>
                    </div>
                  </td>
                  <td>
                    <span
                      className={severityBadgeClass(r.severity)}
                      title={severityLabel(r.severity)}
                    >
                      {severityLabel(r.severity)}
                    </span>
                  </td>
                  <td>
                    <span className={priorityBadgeClass(r.priorityLevel)}>
                      {priorityLabel(r.priorityLevel)}
                    </span>
                  </td>
                  <td>
                    <EllipsisCell
                      mono
                      maxWidth={180}
                      title={formatMinutes(r.firstResponseMinutes)}
                    >
                      {formatMinutes(r.firstResponseMinutes)}
                    </EllipsisCell>
                  </td>
                  <td>
                    <EllipsisCell
                      mono
                      maxWidth={180}
                      title={formatMinutes(r.resolutionMinutes)}
                    >
                      {formatMinutes(r.resolutionMinutes)}
                    </EllipsisCell>
                  </td>
                  {/* Cột Trạng thái: chỉ hiển thị badge Hiển thị / Ẩn */}
                  <td>
                    <span
                      className={r.isActive ? "badge green" : "badge gray"}
                      style={{ textTransform: "none" }}
                    >
                      {r.isActive ? "Hiển thị" : "Ẩn"}
                    </span>
                  </td>
                  {/* Cột Thao tác: Switch + Edit + Delete */}
                  <td>
                    <div className="action-buttons">
                      {/* Switch đổi trạng thái */}
                      <label
                        className="switch"
                        title={r.isActive ? "Đang bật" : "Đang tắt"}
                      >
                        <input
                          type="checkbox"
                          checked={!!r.isActive}
                          onChange={() => toggleRuleActive(r)}
                        />
                        <span className="slider" />
                      </label>

                      {/* Nút Sửa */}
                      <button
                        type="button"
                        className="action-btn edit-btn"
                        onClick={() => openEditRule(r)}
                        title="Chỉnh sửa SLA rule"
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

                      {/* Nút Xoá */}
                      <button
                        type="button"
                        className="action-btn delete-btn"
                        onClick={() => deleteRule(r)}
                        title="Xoá SLA rule"
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
              ))}

              {rules.length === 0 && !loading && (
                <tr>
                  <td colSpan={8} style={{ textAlign: "center", padding: 14 }}>
                    Chưa có SLA rule nào.
                  </td>
                </tr>
              )}

              {loading && (
                <tr>
                  <td colSpan={8} style={{ textAlign: "center", padding: 14 }}>
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
                  <>
                    • Tổng{" "}
                    <b>
                      {total.toLocaleString("vi-VN")} rule
                      {total > 1 ? "s" : ""}
                    </b>
                  </>
                )}
              </span>

              <button
                className="pager-btn"
                disabled={page >= totalPages}
                onClick={() =>
                  setPage((p) => (p >= totalPages ? totalPages : p + 1))
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
                  setPageSize(Number(e.target.value) || 12);
                  setPage(1);
                }}
              >
                {[12, 20, 50, 100].map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>
      </div>

      <SlaRuleModal
        open={ruleModal.open}
        mode={ruleModal.mode}
        initial={ruleModal.data}
        onClose={() =>
          setRuleModal((m) => ({
            ...m,
            open: false,
          }))
        }
        onSubmit={handleRuleSubmit}
        submitting={ruleSubmitting}
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
