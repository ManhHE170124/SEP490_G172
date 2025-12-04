import React from "react";
import ToastContainer from "../../components/Toast/ToastContainer";
import { SupportPriorityLoyaltyRulesApi } from "../../services/supportPriorityLoyaltyRules";
import "../../styles/SupportPriorityLoyaltyRulesPage.css";

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

/* ============ Modal: Rule (Add / Edit) ============ */
function RuleModal({
  open,
  mode,
  initial,
  onClose,
  onSubmit,
  submitting,
  addToast,
  openConfirm,
}) {
  const isEdit = mode === "edit";

  const [form, setForm] = React.useState({
    minTotalSpend: "",
    priorityLevel: "",
    isActive: false,
  });
  const [errors, setErrors] = React.useState({});
  const initialRef = React.useRef(null);

  React.useEffect(() => {
    if (open) {
      const base =
        initial || {
          minTotalSpend: "",
          priorityLevel: "",
          isActive: false,
        };

      const next = {
        minTotalSpend:
          base.minTotalSpend === null || base.minTotalSpend === undefined
            ? ""
            : String(base.minTotalSpend),
        priorityLevel:
          base.priorityLevel === null || base.priorityLevel === undefined
            ? ""
            : String(base.priorityLevel),
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

    const spendRaw = (form.minTotalSpend || "").toString().trim();
    const levelRaw = (form.priorityLevel || "").toString().trim();

    if (!spendRaw) {
      e.minTotalSpend = "Tổng chi tiêu tối thiểu là bắt buộc.";
    } else if (Number.isNaN(Number(spendRaw))) {
      e.minTotalSpend = "Tổng chi tiêu tối thiểu phải là số.";
    } else if (Number(spendRaw) < 0) {
      e.minTotalSpend =
        "Tổng chi tiêu tối thiểu phải lớn hơn hoặc bằng 0.";
    }

    if (!levelRaw) {
      e.priorityLevel = "Mức ưu tiên là bắt buộc.";
    } else if (!Number.isInteger(Number(levelRaw))) {
      e.priorityLevel = "Mức ưu tiên phải là số nguyên.";
    } else if (Number(levelRaw) <= 0) {
      e.priorityLevel =
        "Mức ưu tiên phải lớn hơn 0 (Standard = 0 là mặc định, không cấu hình).";
    }

    setErrors(e);
    if (Object.keys(e).length > 0 && typeof addToast === "function") {
      addToast(
        "warning",
        "Vui lòng kiểm tra các trường được đánh dấu.",
        isEdit ? "Dữ liệu rule chưa hợp lệ" : "Dữ liệu rule chưa hợp lệ"
      );
    }
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async (evt) => {
    evt.preventDefault();
    if (!validate()) return;

    await onSubmit?.({
      minTotalSpend: Number(form.minTotalSpend || 0) || 0,
      priorityLevel: Number(form.priorityLevel || 0) || 0,
      isActive: !!form.isActive,
    });
  };

  const handleClose = () => {
    if (isDirty) {
      if (typeof openConfirm === "function") {
        openConfirm({
          title: "Đóng cửa sổ?",
          message:
            "Bạn có các thay đổi rule chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này.",
          onConfirm: () => {
            onClose?.();
          },
        });
      } else {
        const ok = window.confirm(
          "Bạn có các thay đổi rule chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này. Bạn có chắc muốn thoát?"
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
          <h3>{isEdit ? "Chỉnh sửa rule ưu tiên" : "Thêm rule ưu tiên"}</h3>
          {/* Trạng thái + luật ngay cạnh */}
          <div className="group" style={{ marginTop: 8 }}>
            <div
              className="row"
              style={{ gap: 8, alignItems: "center", flexWrap: "wrap" }}
            >
              <label className="switch" title="Bật/Tắt rule">
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

              <div className="muted support-priority-modal-note">
                <strong>Quy tắc khi bật rule:</strong>
                <div>
                  - Mỗi <b>PriorityLevel</b> chỉ có tối đa <b>1 rule đang bật</b>.
                </div>
                <div>
                  - Rule ở PriorityLevel <b>cao hơn</b> phải có{" "}
                  <b>ngưỡng chi tiêu</b> (Tổng chi tiêu tối thiểu){" "}
                  <b>cao hơn</b> các rule đang bật ở level thấp hơn.
                </div>
                <div>
                  - Rule ở PriorityLevel <b>thấp hơn</b> phải có ngưỡng chi tiêu{" "}
                  <b>thấp hơn</b> các rule đang bật ở level cao hơn.
                </div>
                <div>
                  - Các rule đang tắt không bị ràng buộc; hệ thống chỉ kiểm tra
                  khi <b>lưu / bật</b> rule.
                </div>
              </div>
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="cat-modal-body input-group">
            {/* Hàng 1: Tổng chi tiêu tối thiểu + Mức ưu tiên */}
            <div className="row" style={{ gap: 16 }}>
              <div className="group" style={{ flex: 1 }}>
                <span>
                  Tổng chi tiêu tối thiểu (TotalProductSpend) <RequiredMark />
                </span>
                <input
                  type="number"
                  min={0}
                  step={1000}
                  value={form.minTotalSpend}
                  onChange={(e) => set("minTotalSpend", e.target.value)}
                  placeholder="VD: 500000"
                />
                <FieldError message={errors.minTotalSpend} />
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
                  <option value="1">Priority (1)</option>
                  <option value="2">VIP (2)</option>
                </select>
                <FieldError message={errors.priorityLevel} />
              </div>
            </div>
          </div>

          <div className="cat-modal-footer">
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
                  ? "Đang lưu…"
                  : "Đang tạo…"
                : isEdit
                ? "Lưu thay đổi"
                : "Tạo rule"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ============ Page: SupportPriorityLoyaltyRules ============ */
export default function SupportPriorityLoyaltyRulesPage() {
  // ===== Toast & Confirm =====
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

  // ===== State cho list rules =====
  const [query, setQuery] = React.useState({
    priorityLevel: "",
    active: "",
    sort: "minTotalSpend",
    direction: "asc",
  });
  const [rules, setRules] = React.useState([]);
  const [loading, setLoading] = React.useState(false);
  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(10);
  const [total, setTotal] = React.useState(0);

  const [ruleModal, setRuleModal] = React.useState({
    open: false,
    mode: "add",
    data: null,
  });
  const [ruleSubmitting, setRuleSubmitting] = React.useState(false);

  const loadRules = React.useCallback(() => {
    setLoading(true);

    const params = {
      priorityLevel:
        query.priorityLevel === "" ? undefined : Number(query.priorityLevel),
      active: query.active === "" ? undefined : query.active === "true",
      sort: query.sort || "minTotalSpend",
      direction: query.direction || "asc",
      page,
      pageSize,
    };

    SupportPriorityLoyaltyRulesApi.listPaged(params)
      .then((res) => {
        const items = Array.isArray(res?.items)
          ? res.items
          : Array.isArray(res)
          ? res
          : [];
        setRules(items);
        setTotal(typeof res?.total === "number" ? res.total : items.length);
      })
      .catch((err) => {
        console.error(err);
        addToast(
          "error",
          "Không tải được danh sách rules ưu tiên.",
          "Lỗi"
        );
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

  // ===== CRUD Handlers =====
  const openAddRule = () =>
    setRuleModal({ open: true, mode: "add", data: null });

  const openEditRule = async (r) => {
    try {
      const detail = await SupportPriorityLoyaltyRulesApi.get(r.ruleId);
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
          "Không tải được chi tiết rule để chỉnh sửa.",
        "Lỗi"
      );
    }
  };

  const handleRuleSubmit = async (payload) => {
    setRuleSubmitting(true);
    try {
      if (ruleModal.mode === "add") {
        await SupportPriorityLoyaltyRulesApi.create(payload);
        addToast("success", "Đã tạo rule ưu tiên mới.", "Thành công");
      } else if (ruleModal.mode === "edit" && ruleModal.data) {
        await SupportPriorityLoyaltyRulesApi.update(
          ruleModal.data.ruleId,
          payload
        );
        addToast("success", "Đã cập nhật rule ưu tiên.", "Thành công");
      }

      setRuleModal((m) => ({ ...m, open: false }));
      loadRules();
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message || "Lưu rule ưu tiên thất bại.",
        "Lỗi"
      );
    } finally {
      setRuleSubmitting(false);
    }
  };

  const toggleRuleActive = async (r) => {
    try {
      await SupportPriorityLoyaltyRulesApi.toggle(r.ruleId);
      addToast("success", "Đã cập nhật trạng thái rule.", "Thành công");
      loadRules();
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message || "Không thể cập nhật trạng thái rule.",
        "Lỗi"
      );
    }
  };

  const deleteRule = (r) => {
    openConfirm({
      title: "Xoá rule?",
      message: `Xoá rule ưu tiên với ngưỡng chi tiêu ${formatCurrency(
        r.minTotalSpend
      )} và mức ưu tiên ${priorityLabel(
        r.priorityLevel
      )}? Hành động này không thể hoàn tác!`,
      onConfirm: async () => {
        try {
          await SupportPriorityLoyaltyRulesApi.remove(r.ruleId);
          addToast("success", "Đã xoá rule.", "Thành công");
          loadRules();
        } catch (e) {
          console.error(e);
          addToast(
            "error",
            e?.response?.data?.message || "Xoá rule thất bại.",
            "Lỗi"
          );
        }
      },
    });
  };

  // ===== Render =====
  return (
    <>
      <div className="page">
        <div className="card">
          {/* Header */}
          <div
            style={{
              display: "flex",
              justifyContent: "flex-start",
              alignItems: "center",
            }}
          >
            <h2>Cấu hình ưu tiên hỗ trợ theo tổng chi tiêu</h2>
          </div>

          <p className="muted" style={{ marginTop: 4 }}>
            Định nghĩa các mốc <b>TotalProductSpend</b> tương ứng với
            <b> PriorityLevel</b> (1 = Priority, 2 = VIP, …). Level 0 (Standard)
            là mức mặc định, không cấu hình tại màn hình này. Hệ thống sẽ dựa
            vào các rule đang bật để tự động xác định mức ưu tiên hỗ trợ cho
            khách hàng.
          </p>

          {/* Ghi chú luật rule ưu tiên */}
          <div className="support-priority-rules-note">
            <div className="support-priority-rules-note-title">
              Luật khi tạo & bật rule ưu tiên:
            </div>
            <ul>
              <li>
                Mỗi <b>PriorityLevel</b> chỉ có tối đa <b>01 rule đang bật</b>.
              </li>
              <li>
                Rule ở PriorityLevel <b>cao hơn</b> phải có{" "}
                <b>ngưỡng chi tiêu tối thiểu</b> (Tổng chi tiêu){" "}
                <b>cao hơn</b> tất cả các rule đang bật ở level thấp hơn.
              </li>
              <li>
                Rule ở PriorityLevel <b>thấp hơn</b> phải có ngưỡng chi tiêu{" "}
                <b>thấp hơn</b> các rule đang bật ở level cao hơn.
              </li>
              <li>
                Các rule ở trạng thái <b>tắt</b> không bị ràng buộc; hệ thống
                chỉ kiểm tra khi <b>lưu / bật</b> rule.
              </li>
            </ul>
          </div>

          {/* Bộ lọc + nút trên cùng một hàng */}
          <div className="input-group filter-row">
            {/* Filter: Priority */}
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
                <option value="1">Priority (1)</option>
                <option value="2">VIP (2)</option>
              </select>
            </div>

            {/* Filter: Status */}
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

            {/* Cụm Làm mới + Đặt lại + (loading) ngay cạnh filter */}
            <div className="group filter-actions">
              <span>&nbsp;</span>
              <div className="filter-actions-inner">
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
                  onClick={() =>
                    setQuery({
                      priorityLevel: "",
                      active: "",
                      sort: "minTotalSpend",
                      direction: "asc",
                    })
                  }
                  title="Xoá bộ lọc"
                >
                  Đặt lại
                </button>
              </div>
            </div>

            {/* Nút Thêm rule: sát phải hàng */}
            <div className="group filter-add">
              <span>&nbsp;</span>
              <button className="btn primary" onClick={openAddRule}>
                Thêm rule ưu tiên
              </button>
            </div>
          </div>

          {/* Bảng rules */}
          <table className="table" style={{ marginTop: 10 }}>
            <thead>
              <tr>
                <th
                  onClick={() =>
                    setQuery((s) => ({
                      ...s,
                      sort: "ruleId",
                      direction:
                        s.sort === "ruleId" && s.direction === "asc"
                          ? "desc"
                          : "asc",
                    }))
                  }
                  style={{ cursor: "pointer", width: 80 }}
                >
                  ID{" "}
                  {query.sort === "ruleId"
                    ? query.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>

                <th
                  onClick={() =>
                    setQuery((s) => ({
                      ...s,
                      sort: "minTotalSpend",
                      direction:
                        s.sort === "minTotalSpend" && s.direction === "asc"
                          ? "desc"
                          : "asc",
                    }))
                  }
                  style={{ cursor: "pointer", minWidth: 200 }}
                >
                  Tổng chi tiêu tối thiểu{" "}
                  {query.sort === "minTotalSpend"
                    ? query.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>

                <th
                  onClick={() =>
                    setQuery((s) => ({
                      ...s,
                      sort: "priorityLevel",
                      direction:
                        s.sort === "priorityLevel" && s.direction === "asc"
                          ? "desc"
                          : "asc",
                    }))
                  }
                  style={{ cursor: "pointer", width: 180 }}
                >
                  Mức ưu tiên{" "}
                  {query.sort === "priorityLevel"
                    ? query.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>

                <th
                  onClick={() =>
                    setQuery((s) => ({
                      ...s,
                      sort: "active",
                      direction:
                        s.sort === "active" && s.direction === "asc"
                          ? "desc"
                          : "asc",
                    }))
                  }
                  style={{ cursor: "pointer", width: 140 }}
                >
                  Trạng thái{" "}
                  {query.sort === "active"
                    ? query.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>

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
              {rules.map((r) => (
                <tr key={r.ruleId}>
                  <td>
                    <EllipsisCell mono maxWidth={80} title={r.ruleId}>
                      {r.ruleId}
                    </EllipsisCell>
                  </td>
                  <td>
                    <EllipsisCell
                      mono
                      maxWidth={220}
                      title={formatCurrency(r.minTotalSpend)}
                    >
                      {formatCurrency(r.minTotalSpend)}
                    </EllipsisCell>
                  </td>
                  <td>
                    <span className={priorityBadgeClass(r.priorityLevel)}>
                      {priorityLabel(r.priorityLevel)}
                    </span>
                  </td>
                  <td>
                    <button
                      type="button"
                      className="btn ghost status-btn"
                      onClick={() => toggleRuleActive(r)}
                    >
                      <span
                        className={
                          r.isActive ? "badge green" : "badge gray"
                        }
                        style={{ textTransform: "none" }}
                      >
                        {r.isActive ? "Đang bật" : "Đang tắt"}
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
                        onClick={() => openEditRule(r)}
                      >
                        Sửa
                      </button>
                      <button
                        className="btn danger"
                        onClick={() => deleteRule(r)}
                      >
                        Xoá
                      </button>
                    </div>
                  </td>
                </tr>
              ))}

              {rules.length === 0 && !loading && (
                <tr>
                  <td colSpan={5} style={{ textAlign: "center", padding: 14 }}>
                    Chưa có rule ưu tiên nào.
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

              <span className="pager-current">
                Trang <b>{page}</b> / {totalPages}
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
              <span className="pager-page-size-label">Số dòng / trang:</span>
              <select
                className="pager-page-size"
                value={pageSize}
                onChange={(e) => {
                  const v = Number(e.target.value || 10);
                  setPageSize(v);
                  setPage(1);
                }}
              >
                <option value={5}>5</option>
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      {/* Modal Add/Edit Rule */}
      <RuleModal
        open={ruleModal.open}
        mode={ruleModal.mode}
        initial={ruleModal.data}
        onClose={() => setRuleModal((m) => ({ ...m, open: false }))}
        onSubmit={handleRuleSubmit}
        submitting={ruleSubmitting}
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
