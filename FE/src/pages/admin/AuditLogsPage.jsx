// File: src/pages/admin/AuditLogsPage.jsx
import React from "react";
import { AuditLogsApi } from "../../services/auditLogs";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./AuditLogsPage.css";
import formatDatetime from "../../utils/formatDatetime";

/* =========================
   VN MAPPING (FE-only)
   - Tooltip chỉ tiếng Việt
   ========================= */

const ACTION_VI = {
  // ===== AUTH / ACCOUNT (GIỮ NGUYÊN cụm này theo yêu cầu) =====
  Login: "Đăng nhập",
  Register: "Đăng ký",
  ChangePassword: "Đổi mật khẩu",
  ResetPassword: "Đặt lại mật khẩu",
  RevokeToken: "Đăng xuất thiết bị",

  UpdateProfile: "Cập nhật hồ sơ",
  UpdatePermissions: "Cập nhật phân quyền",

  // ===== PRODUCT ACCOUNT (Account share) =====
  AddCustomerToProductAccount: "Thêm khách vào tài khoản sản phẩm",
  RemoveCustomerFromProductAccount: "Gỡ khách khỏi tài khoản sản phẩm",
  CreateProductAccount: "Tạo tài khoản sản phẩm",
  UpdateProductAccount: "Cập nhật tài khoản sản phẩm",
  DeleteProductAccount: "Xóa tài khoản sản phẩm",
  ExtendProductAccountExpiry: "Gia hạn thời hạn tài khoản sản phẩm",
  GetProductAccountPassword: "Xem mật khẩu tài khoản sản phẩm",

  // ===== SUPPORT CHAT =====
  ClaimSupportChatSession: "Nhận xử lý phiên chat hỗ trợ",
  UnassignSupportChatSession: "Bỏ nhận phiên chat hỗ trợ",
  CloseSupportChatSession: "Đóng phiên chat hỗ trợ",
  AdminAssignStaff: "Quản trị viên gán nhân viên",
  AdminTransferStaff: "Quản trị viên chuyển nhân viên",

  // ===== TICKET / SUPPORT =====
  AssignStaffToTicket: "Gán nhân viên xử lý ticket",
  AssignToMe: "Tự nhận xử lý ticket",
  TransferToTech: "Chuyển ticket sang kỹ thuật",
  CloseTicket: "Đóng ticket",
  CompleteTicket: "Hoàn tất ticket",
  StaffReply: "Nhân viên phản hồi ticket",

  // ===== ORDER / PAYMENT =====
  CheckoutFromCart: "Thanh toán từ giỏ hàng",
  ViewOrderDetail: "Xem chi tiết đơn hàng",
  ViewOrderDetailWithCredentials: "Xem chi tiết đơn hàng",
  PaymentStatusChanged: "Cập nhật trạng thái thanh toán",
  CreateSupportPlanPayOSPayment: "Tạo thanh toán PayOS cho gói hỗ trợ",
  ConfirmSupportPlanPayment: "Xác nhận thanh toán gói hỗ trợ",

  // ===== PRODUCT KEY =====
  CreateProductKey: "Tạo key sản phẩm",
  UpdateProductKey: "Cập nhật key sản phẩm",
  DeleteProductKey: "Xóa key sản phẩm",
  BulkUpdateStatus: "Cập nhật trạng thái key hàng loạt",
  AssignToOrder: "Gán key vào đơn hàng",
  UnassignFromOrder: "Hủy gán key khỏi đơn hàng",
  ImportCsv: "Nhập key từ CSV",

  // ===== PRODUCT REPORT =====
  CreateProductReport: "Tạo báo cáo lỗi sản phẩm",
  UpdateStatus: "Cập nhật trạng thái báo cáo lỗi sản phẩm",

  // ===== PRODUCT / VARIANT / IMAGES / SECTIONS =====
  CreateProduct: "Tạo sản phẩm",
  UpdateProduct: "Cập nhật sản phẩm",
  DeleteProduct: "Xóa sản phẩm",
  ToggleVisibility: "Ẩn/hiện sản phẩm",

  CreateProductVariant: "Tạo biến thể sản phẩm",
  UpdateProductVariant: "Cập nhật biến thể sản phẩm",
  DeleteProductVariant: "Xóa biến thể sản phẩm",
  ToggleProductVariantStatus: "Bật/tắt trạng thái biến thể sản phẩm",

  UploadImage: "Tải lên hình ảnh",
  DeleteImage: "Xóa hình ảnh",

  CreateProductSection: "Tạo mục nội dung sản phẩm",
  UpdateProductSection: "Cập nhật mục nội dung sản phẩm",
  DeleteProductSection: "Xóa mục nội dung sản phẩm",
  ToggleProductSectionActive: "Bật/tắt mục nội dung sản phẩm",
  ReorderProductSections: "Sắp xếp lại mục nội dung sản phẩm",

  // ===== CATEGORY =====
  CreateCategory: "Tạo danh mục",
  UpdateCategory: "Cập nhật danh mục",
  DeleteCategory: "Xóa danh mục",
  ToggleCategory: "Bật/tắt danh mục",

  // ===== BADGE =====
  CreateBadge: "Tạo nhãn sản phẩm",
  UpdateBadge: "Cập nhật nhãn sản phẩm",
  DeleteBadge: "Xóa nhãn sản phẩm",
  ToggleBadge: "Bật/tắt nhãn sản phẩm",
  SetBadgeStatus: "Cập nhật trạng thái nhãn sản phẩm",
  SetBadgesForProduct: "Gán nhãn cho sản phẩm",

  // ===== FAQ =====
  CreateFaq: "Tạo câu hỏi thường gặp",
  UpdateFaq: "Cập nhật câu hỏi thường gặp",
  DeleteFaq: "Xóa câu hỏi thường gặp",
  ToggleFaq: "Bật/tắt câu hỏi thường gặp",

  // ===== LICENSE PACKAGE / STOCK =====
  CreateLicensePackage: "Tạo gói bản quyền",
  UpdateLicensePackage: "Cập nhật gói bản quyền",
  DeleteLicensePackage: "Xóa gói bản quyền",
  UploadLicenseCsv: "Tải lên CSV mã license",
  ImportLicenseToStock: "Nhập mã license vào kho",

  // ===== SUPPLIER =====
  CreateSupplier: "Tạo nhà cung cấp",
  UpdateSupplier: "Cập nhật nhà cung cấp",
  DeactivateSupplier: "Vô hiệu hóa nhà cung cấp",
  ToggleStatus: "Bật/tắt trạng thái",

  // ===== ROLES =====
  CreateRole: "Tạo vai trò",
  UpdateRole: "Cập nhật vai trò",
  DeleteRole: "Xóa vai trò",

  // ===== SLA RULES =====
  CreateSlaRule: "Tạo quy tắc SLA",
  UpdateSlaRule: "Cập nhật quy tắc SLA",
  DeleteSlaRule: "Xóa quy tắc SLA",
  ToggleActive: "Bật/tắt kích hoạt",

  // ===== SUPPORT PLANS =====
  CreateSupportPlan: "Tạo gói hỗ trợ",
  UpdateSupportPlan: "Cập nhật gói hỗ trợ",
  DeleteSupportPlan: "Xóa gói hỗ trợ",
  ToggleSupportPlanActive: "Bật/tắt gói hỗ trợ",

  // ===== SUPPORT PRIORITY LOYALTY =====
  CreateSupportPriorityLoyaltyRule: "Tạo quy tắc nâng hạng ưu tiên theo chi tiêu",
  UpdateSupportPriorityLoyaltyRule: "Cập nhật quy tắc nâng hạng ưu tiên theo chi tiêu",
  DeleteSupportPriorityLoyaltyRule: "Xóa quy tắc nâng hạng ưu tiên theo chi tiêu",
  ToggleSupportPriorityLoyaltyRuleActive: "Bật/tắt quy tắc nâng hạng ưu tiên theo chi tiêu",

  // ===== TICKET SUBJECT TEMPLATE =====
  CreateTicketSubjectTemplate: "Tạo mẫu chủ đề ticket",
  UpdateTicketSubjectTemplate: "Cập nhật mẫu chủ đề ticket",
  DeleteTicketSubjectTemplate: "Xóa mẫu chủ đề ticket",
  ToggleTicketSubjectTemplateActive: "Bật/tắt mẫu chủ đề ticket",

  // ===== USERS =====
  CreateUser: "Tạo người dùng",
  UpdateUser: "Cập nhật người dùng",

  // ===== WEBSITE SETTINGS =====
  SaveWebsiteSettings: "Lưu cấu hình website",

  // ===== GENERIC FALLBACK (giữ để tương thích nếu BE có log action dạng generic) =====
  Assign: "Gán xử lý",
  Unassign: "Hủy gán xử lý",
  Claim: "Nhận xử lý",
  Close: "Đóng",
  Complete: "Hoàn tất",
  Reorder: "Sắp xếp lại",
  Deactivate: "Vô hiệu hóa",

  // ✅ quan trọng: WebConfig từng log action: "Save"
  Save: "Lưu thay đổi",
  Create: "Tạo mới",
  Update: "Cập nhật",
  Delete: "Xóa",
  Toggle: "Bật/Tắt",
};

const ENTITY_VI = {
  // ===== CORE / AUTH =====
  Account: "Tài khoản",
  User: "Người dùng",
  Role: "Vai trò",

  // ===== CATALOG =====
  Product: "Sản phẩm",
  ProductVariant: "Biến thể sản phẩm",
  ProductVariantImage: "Ảnh biến thể sản phẩm",
  ProductSection: "Mục nội dung sản phẩm",
  Category: "Danh mục",
  Badge: "Nhãn sản phẩm",

  // ===== INVENTORY / DELIVERABLES =====
  ProductKey: "Key sản phẩm",
  ProductKeyAssignment: "Gán key cho đơn hàng",
  ProductAccount: "Tài khoản sản phẩm",
  ProductAccountCustomer: "Khách - tài khoản sản phẩm",

  LicensePackage: "Gói bản quyền",
  Supplier: "Nhà cung cấp",

  // ===== COMMERCE =====
  Order: "Đơn hàng",
  Payment: "Thanh toán",

  // ===== SUPPORT =====
  Ticket: "Ticket hỗ trợ",
  TicketReply: "Phản hồi ticket",
  TicketSubjectTemplate: "Mẫu chủ đề ticket",
  SupportPlan: "Gói hỗ trợ",
  UserSupportPlanSubscription: "Đăng ký gói hỗ trợ",
  SupportChatSession: "Phiên chat hỗ trợ",
  SupportPriorityLoyaltyRule: "Quy tắc nâng hạng ưu tiên theo chi tiêu",
  SlaRule: "Quy tắc SLA",

  // ===== CONTENT / SETTINGS =====
  Faq: "Câu hỏi thường gặp",
  ProductReport: "Báo cáo lỗi sản phẩm",
  WebsiteSettings: "Thiết lập website",
};

const ROLE_VI = {
  ADMIN: "Quản trị viên",
  STORAGE_STAFF: "Nhân viên kho",
  CUSTOMER_CARE: "Nhân viên CSKH",
  CONTENT_CREATOR: "Nhân viên nội dung",
  TECH_STAFF: "Nhân viên kỹ thuật",
  STAFF: "Nhân viên",
  CUSTOMER: "Khách hàng",
  USER: "Người dùng",
  SYSTEM: "Hệ thống",
};

const SYSTEM_ROLE_VALUE = "System";
const SYSTEM_ACTOR_EMAIL = "Hệ thống tự tạo";

const normalizeRoleKey = (value) =>
  String(value || "")
    .trim()
    .toUpperCase()
    .replace(/\s+/g, "_")
    .replace(/-+/g, "_");

const viAction = (v) => ACTION_VI[String(v || "").trim()] || v || "-";
const viEntity = (v) => ENTITY_VI[String(v || "").trim()] || v || "-";
const viRole = (v) => {
  const raw = String(v || "").trim();
  if (!raw) return ROLE_VI.SYSTEM;
  const k = normalizeRoleKey(raw);
  return ROLE_VI[k] || raw || "-";
};

const displayActorEmail = (email) => {
  const raw = String(email || "").trim();
  return raw ? raw : SYSTEM_ACTOR_EMAIL;
};

/* =========================
   Time formatting: UI display in UTC+7 (using shared util)
   ========================= */
const formatDateTimeBkk = (value) => (value ? formatDatetime(value) : "");

/* =========================
   DatePicker (calendar) + display dd/MM/yyyy
   - KHÔNG button
   - Click vào ô => mở calendar (overlay input type="date")
   value: "YYYY-MM-DD" | ""
   ========================= */
const isoToDdMmYyyy = (iso) => {
  if (!iso) return "";
  const parts = String(iso).split("-");
  if (parts.length !== 3) return "";
  const [y, m, d] = parts;
  if (!y || !m || !d) return "";
  return `${String(d).padStart(2, "0")}/${String(m).padStart(2, "0")}/${y}`;
};

function DatePickerVN({ value, onChange }) {
  const display = isoToDdMmYyyy(value);

  return (
    <div className="audit-datepick" title="Chọn ngày">
      <input
        className="audit-date-text"
        type="text"
        value={display}
        placeholder="dd/mm/yyyy"
        readOnly
      />
      <input
        className="audit-date-native"
        type="date"
        value={value || ""}
        onChange={(e) => onChange?.(e.target.value || "")}
        aria-label="Chọn ngày"
      />
    </div>
  );
}

/* ============ Helpers: Ellipsis cell ============ */
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

/* ============ Helpers: Sortable header ============ */
const SortableHeader = ({ label, field, sortBy, sortDirection, onSort }) => {
  const isActive = sortBy === field;
  const arrow = !isActive ? "↕" : sortDirection === "asc" ? "↑" : "↓";

  return (
    <button
      type="button"
      className="sortable-header"
      onClick={() => onSort(field)}
      title="Sắp xếp"
    >
      <span>{label}</span>
      <span className="sort-arrow">{arrow}</span>
    </button>
  );
};

/* ============ Helpers cho modal JSON ============ */

const tryParseJsonSafe = (json) => {
  if (!json) return null;
  try {
    return JSON.parse(json);
  } catch {
    return null;
  }
};

const tryParseLooseJsonValue = (raw) => {
  if (raw === null || raw === undefined) return raw;
  const text = String(raw).trim();
  if (!text) return text;

  if (text.startsWith("{") || text.startsWith("[")) {
    try {
      return JSON.parse(text);
    } catch {
      return text;
    }
  }

  const num = Number(text);
  if (!Number.isNaN(num) && text === String(num)) return num;
  if (text === "true") return true;
  if (text === "false") return false;

  return text;
};

const buildPartialObjectFromChanges = (changes, side) => {
  const root = {};
  if (!Array.isArray(changes)) return root;

  const assignValue = (obj, path, rawValue) => {
    if (rawValue === undefined || rawValue === null) return;

    const segments = (path || "").split(".").filter(Boolean);
    const value = tryParseLooseJsonValue(rawValue);

    if (segments.length === 0) {
      if (value && typeof value === "object" && !Array.isArray(value)) Object.assign(obj, value);
      else obj.value = value;
      return;
    }

    let current = obj;
    for (let i = 0; i < segments.length; i += 1) {
      const key = segments[i];
      if (i === segments.length - 1) current[key] = value;
      else {
        if (!current[key] || typeof current[key] !== "object" || Array.isArray(current[key])) current[key] = {};
        current = current[key];
      }
    }
  };

  changes.forEach((c) => {
    if (!c) return;
    const raw = side === "before" ? c.before : side === "after" ? c.after : undefined;
    if (raw === undefined || raw === null) return;
    assignValue(root, c.fieldPath || "", raw);
  });

  return root;
};

const getViewLabel = (mode) => {
  if (mode === "hidden") return "Hiển thị";
  if (mode === "filtered") return "Chi tiết";
  return "Ẩn";
};

const cycleViewMode = (mode, hasTwoSideChanges) => {
  if (hasTwoSideChanges) {
    if (mode === "filtered") return "full";
    if (mode === "full") return "hidden";
    return "filtered";
  }
  if (mode === "full") return "hidden";
  return "full";
};

/* ============ Modal: Audit Log Detail ============ */
function AuditLogDetailModal({ open, log, detail, loading, onClose }) {
  const [parsedBefore, setParsedBefore] = React.useState(null);
  const [parsedAfter, setParsedAfter] = React.useState(null);
  const [filteredBefore, setFilteredBefore] = React.useState(null);
  const [filteredAfter, setFilteredAfter] = React.useState(null);
  const [hasTwoSideChanges, setHasTwoSideChanges] = React.useState(false);

  const [beforeViewMode, setBeforeViewMode] = React.useState("filtered");
  const [afterViewMode, setAfterViewMode] = React.useState("filtered");

  const item = detail || log;
  const changes = (detail && detail.changes) || (log && log.changes) || [];

  React.useEffect(() => {
    if (!detail) {
      setParsedBefore(null);
      setParsedAfter(null);
      setFilteredBefore(null);
      setFilteredAfter(null);
      setHasTwoSideChanges(false);
      setBeforeViewMode("filtered");
      setAfterViewMode("filtered");
      return;
    }

    const pb = tryParseJsonSafe(detail.beforeDataJson);
    const pa = tryParseJsonSafe(detail.afterDataJson);
    setParsedBefore(pb);
    setParsedAfter(pa);

    const hasTwoSide =
      Array.isArray(changes) &&
      changes.some(
        (c) =>
          c &&
          c.before !== null &&
          c.before !== undefined &&
          c.after !== null &&
          c.after !== undefined
      );

    setHasTwoSideChanges(hasTwoSide);

    if (hasTwoSide) {
      setFilteredBefore(buildPartialObjectFromChanges(changes, "before"));
      setFilteredAfter(buildPartialObjectFromChanges(changes, "after"));
      setBeforeViewMode("filtered");
      setAfterViewMode("filtered");
    } else {
      setFilteredBefore(null);
      setFilteredAfter(null);
      setBeforeViewMode("full");
      setAfterViewMode("full");
    }
  }, [detail, changes]);

  if (!open || !log) return null;

  const beforeLabel = getViewLabel(beforeViewMode);
  const afterLabel = getViewLabel(afterViewMode);

  const shouldShowBeforeBlock = beforeViewMode !== "hidden" && !!detail;
  const shouldShowAfterBlock = afterViewMode !== "hidden" && !!detail;

  const beforeContent = (() => {
    if (!detail) return "Đang tải chi tiết…";
    if (hasTwoSideChanges && beforeViewMode === "filtered" && filteredBefore)
      return JSON.stringify(filteredBefore, null, 2);
    if (parsedBefore) return JSON.stringify(parsedBefore, null, 2);
    return detail.beforeDataJson || "(empty)";
  })();

  const afterContent = (() => {
    if (!detail) return "Đang tải chi tiết…";
    if (hasTwoSideChanges && afterViewMode === "filtered" && filteredAfter)
      return JSON.stringify(filteredAfter, null, 2);
    if (parsedAfter) return JSON.stringify(parsedAfter, null, 2);
    return detail.afterDataJson || "(empty)";
  })();

  const actionLabel = viAction(item.action);
  const entityLabel = viEntity(item.entityType);
  const roleLabel = viRole(item.actorRole);

  return (
    <div className="audit-modal-backdrop">
      <div className="audit-modal-card audit-detail-card">
        <div className="audit-modal-header">
          <div className="audit-detail-title">
            <h3>Chi tiết thao tác hệ thống</h3>
          </div>
          <button type="button" className="btn ghost small" onClick={onClose}>
            Đóng
          </button>
        </div>

        {loading && (
          <div style={{ padding: 8 }}>
            <span className="badge gray">Đang tải chi tiết…</span>
          </div>
        )}

        <div className="audit-modal-body audit-detail-body">
          <div className="grid cols-2 input-group audit-detail-grid">
            <div className="group">
              <span>Thời gian (UTC+7)</span>
              <div className="mono strong">{formatDateTimeBkk(item.occurredAt)}</div>
            </div>

            <div className="group">
              <span>Hành động</span>
              <div className="mono" title={actionLabel}>
                {actionLabel}
              </div>
            </div>

            <div className="group">
              <span>Đối tượng</span>
              <div className="mono" title={entityLabel}>
                {entityLabel} {item.entityId ? `(${item.entityId})` : ""}
              </div>
            </div>

            <div className="group">
              <span>Người thao tác</span>
              <div className="mono">
                {displayActorEmail(item.actorEmail)}
                {item.actorId ? ` (${item.actorId})` : ""}
              </div>
            </div>

            <div className="group">
              <span>Vai trò</span>
              <div className="mono" title={roleLabel}>
                {roleLabel}
              </div>
            </div>

            <div className="group">
              <span>Mã phiên</span>
              <div className="mono wrap">{item.sessionId || "-"}</div>
            </div>

            <div className="group">
              <span>Địa chỉ IP</span>
              <div className="mono">{item.ipAddress || "-"}</div>
            </div>
          </div>

          <div className="audit-json-columns">
            <div className="group">
              <div className="audit-json-header">
                <span>Dữ liệu trước</span>
                <div className="audit-json-header-actions">
                  <span className="muted small">{beforeLabel}</span>
                  <button
                    type="button"
                    className="audit-json-toggle"
                    onClick={() =>
                      setBeforeViewMode((mode) => cycleViewMode(mode, hasTwoSideChanges))
                    }
                    title="Chuyển chế độ hiển thị dữ liệu trước"
                  >
                    ⋯
                  </button>
                </div>
              </div>
              {shouldShowBeforeBlock && <pre className="audit-json">{beforeContent}</pre>}
            </div>

            <div className="group">
              <div className="audit-json-header">
                <span>Dữ liệu sau</span>
                <div className="audit-json-header-actions">
                  <span className="muted small">{afterLabel}</span>
                  <button
                    type="button"
                    className="audit-json-toggle"
                    onClick={() =>
                      setAfterViewMode((mode) => cycleViewMode(mode, hasTwoSideChanges))
                    }
                    title="Chuyển chế độ hiển thị dữ liệu sau"
                  >
                    ⋯
                  </button>
                </div>
              </div>
              {shouldShowAfterBlock && <pre className="audit-json">{afterContent}</pre>}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ============ MAIN PAGE ============ */
export default function AuditLogsPage() {
  const [toasts, setToasts] = React.useState([]);
  const toastIdRef = React.useRef(1);
  const [confirmDialog, setConfirmDialog] = React.useState(null);

  const removeToast = (id) => setToasts((prev) => prev.filter((t) => t.id !== id));

  const addToast = (type, message, title) => {
    const id = toastIdRef.current++;
    setToasts((prev) => [...prev, { id, type, message, title }]);
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
    actorEmail: "",
    actorRole: "",
    action: "",
    entityType: "",
    from: "",
    to: "",
  });

  const [logs, setLogs] = React.useState([]);
  const [loading, setLoading] = React.useState(false);
  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(20);
  const [total, setTotal] = React.useState(0);

  const [selectedLog, setSelectedLog] = React.useState(null);
  const [detail, setDetail] = React.useState(null);
  const [detailLoading, setDetailLoading] = React.useState(false);

  const [options, setOptions] = React.useState({
    actions: [],
    entityTypes: [],
    actorRoles: [],
  });

  const [sortBy, setSortBy] = React.useState("OccurredAt");
  const [sortDirection, setSortDirection] = React.useState("desc");

  React.useEffect(() => {
    AuditLogsApi.getFilterOptions()
      .then((res) => {
        setOptions({
          actions: res.actions || [],
          entityTypes: res.entityTypes || [],
          actorRoles: res.actorRoles || [],
        });
      })
      .catch((err) => {
        console.error(err);
        addToast("error", "Không tải được danh sách bộ lọc (hành động, đối tượng, vai trò).", "Lỗi");
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const roleOptions = React.useMemo(() => {
    const raw = Array.isArray(options.actorRoles) ? options.actorRoles : [];
    const merged = [...raw, SYSTEM_ROLE_VALUE];
    const uniq = [];
    const seen = new Set();
    merged.forEach((r) => {
      const v = String(r || "").trim();
      if (!v) return;
      const k = v.toLowerCase();
      if (seen.has(k)) return;
      seen.add(k);
      uniq.push(v);
    });
    return uniq;
  }, [options.actorRoles]);

  const loadLogs = React.useCallback(() => {
    setLoading(true);

    const params = { page, pageSize };

    if (query.actorEmail) params.actorEmail = query.actorEmail.trim();
    if (query.actorRole) params.actorRole = query.actorRole.trim();
    if (query.action) params.action = query.action.trim();
    if (query.entityType) params.entityType = query.entityType.trim();
    if (query.from) params.from = query.from; // YYYY-MM-DD
    if (query.to) params.to = query.to;       // YYYY-MM-DD

    if (sortBy) params.sortBy = sortBy;
    if (sortDirection) params.sortDirection = sortDirection;

    AuditLogsApi.listPaged(params)
      .then((res) => {
        const items = res?.items ?? [];
        setLogs(items);
        setTotal(typeof res?.total === "number" ? res.total : items.length);
        setPage(res.pageNumber ?? page);
        setPageSize(res.pageSize ?? pageSize);
      })
      .catch((err) => {
        console.error(err);
        addToast("error", "Không tải được lịch sử thao tác.", "Lỗi");
      })
      .finally(() => setLoading(false));
  }, [page, pageSize, query, sortBy, sortDirection]);

  React.useEffect(() => {
    const t = setTimeout(loadLogs, 300);
    return () => clearTimeout(t);
  }, [loadLogs]);

  React.useEffect(() => {
    setPage(1);
  }, [query.actorEmail, query.actorRole, query.action, query.entityType, query.from, query.to]);

  const openDetail = (log) => {
    setSelectedLog(log);
    setDetail(null);
    setDetailLoading(true);
    AuditLogsApi.getDetail(log.auditId)
      .then((res) => setDetail(res))
      .catch((err) => {
        console.error(err);
        addToast("error", "Không tải được chi tiết lịch sử thao tác.", "Lỗi");
      })
      .finally(() => setDetailLoading(false));
  };

  const closeDetail = () => {
    setSelectedLog(null);
    setDetail(null);
    setDetailLoading(false);
  };

  const resetFilters = () => {
    setQuery({
      actorEmail: "",
      actorRole: "",
      action: "",
      entityType: "",
      from: "",
      to: "",
    });
    setSortBy("OccurredAt");
    setSortDirection("desc");
  };

  const totalPages = pageSize > 0 ? Math.max(1, Math.ceil(total / pageSize)) : 1;

  // ✅ sort 2 chiều
  const handleSort = (field) => {
    if (sortBy === field) {
      setSortDirection((prev) => (prev === "asc" ? "desc" : "asc"));
      return;
    }
    setSortBy(field);
    setSortDirection(field === "OccurredAt" ? "desc" : "asc");
  };

  return (
    <>
      <div className="page audit-page">
        <div className="card">
          <div className="audit-header-row">
            <div className="audit-header-left">
              <h2>Lịch sử thao tác hệ thống</h2>
            </div>
            <div className="row" style={{ gap: 8, alignItems: "center" }}>
              <span className="muted">
                Tổng: <b>{total}</b> bản ghi
              </span>
            </div>
          </div>

          <div className="row input-group audit-filters">
            <div className="group filter-col filter-col-wide">
              <span>Tìm kiếm (email người thao tác)</span>
              <input
                value={query.actorEmail}
                onChange={(e) => setQuery((s) => ({ ...s, actorEmail: e.target.value }))}
              />
            </div>

            <div className="group filter-col">
              <span>Từ ngày</span>
              <DatePickerVN value={query.from} onChange={(val) => setQuery((s) => ({ ...s, from: val }))} />
            </div>

            <div className="group filter-col">
              <span>Đến ngày</span>
              <DatePickerVN value={query.to} onChange={(val) => setQuery((s) => ({ ...s, to: val }))} />
            </div>
          </div>

          <div className="row input-group audit-filters">
            <div className="group filter-col">
              <span>Hành động</span>
              <select
                value={query.action}
                onChange={(e) => setQuery((s) => ({ ...s, action: e.target.value }))}
              >
                <option value="">Tất cả</option>
                {options.actions.map((action) => (
                  <option key={action} value={action} title={viAction(action)}>
                    {viAction(action)}
                  </option>
                ))}
              </select>
            </div>

            <div className="group filter-col">
              <span>Loại đối tượng</span>
              <select
                value={query.entityType}
                onChange={(e) => setQuery((s) => ({ ...s, entityType: e.target.value }))}
              >
                <option value="">Tất cả</option>
                {options.entityTypes.map((type) => (
                  <option key={type} value={type} title={viEntity(type)}>
                    {viEntity(type)}
                  </option>
                ))}
              </select>
            </div>

            <div className="group filter-col">
              <span>Vai trò</span>
              <select
                value={query.actorRole}
                onChange={(e) => setQuery((s) => ({ ...s, actorRole: e.target.value }))}
              >
                <option value="">Tất cả</option>
                {roleOptions.map((role) => (
                  <option key={role} value={role} title={viRole(role)}>
                    {viRole(role)}
                  </option>
                ))}
              </select>
            </div>

            {loading && <span className="badge gray">Đang tải…</span>}

            <button type="button" className="btn ghost filter-reset-btn" onClick={resetFilters} title="Xóa bộ lọc">
              Đặt lại
            </button>
          </div>

          <table className="table audit-table">
            <thead>
              <tr>
                <th>
                  <SortableHeader
                    label="Thời gian (UTC+7)"
                    field="OccurredAt"
                    sortBy={sortBy}
                    sortDirection={sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th>
                  <SortableHeader
                    label="Người thao tác"
                    field="ActorEmail"
                    sortBy={sortBy}
                    sortDirection={sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th>
                  <SortableHeader
                    label="Vai trò"
                    field="ActorRole"
                    sortBy={sortBy}
                    sortDirection={sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th>
                  <SortableHeader
                    label="Hành động"
                    field="Action"
                    sortBy={sortBy}
                    sortDirection={sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th>
                  <SortableHeader
                    label="Loại đối tượng"
                    field="EntityType"
                    sortBy={sortBy}
                    sortDirection={sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th style={{ textAlign: "center" }}>Chi tiết</th>
              </tr>
            </thead>

            <tbody>
              {logs.map((log) => {
                const actionLabel = viAction(log.action);
                const entityLabel = viEntity(log.entityType);
                const roleLabel = viRole(log.actorRole);
                const actorLabel = displayActorEmail(log.actorEmail);

                return (
                  <tr key={log.auditId} className="audit-row">
                    <td className="mono">{formatDateTimeBkk(log.occurredAt)}</td>

                    <td>
                      <EllipsisCell mono maxWidth={240} title={actorLabel}>
                        {actorLabel}
                      </EllipsisCell>
                    </td>

                    <td>
                      <EllipsisCell mono maxWidth={170} title={roleLabel}>
                        {roleLabel}
                      </EllipsisCell>
                    </td>

                    <td>
                      <EllipsisCell mono maxWidth={300} title={actionLabel}>
                        {actionLabel}
                      </EllipsisCell>
                    </td>

                    <td>
                      <EllipsisCell mono maxWidth={220} title={entityLabel}>
                        {entityLabel}
                      </EllipsisCell>
                    </td>

                    <td className="audit-actions-cell">
                      <button
                        type="button"
                        className="btn icon-btn"
                        title="Xem chi tiết"
                        onClick={(e) => {
                          e.stopPropagation();
                          openDetail(log);
                        }}
                      >
                        <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
                          <path
                            d="M1.5 12S4.5 5 12 5s10.5 7 10.5 7-3 7-10.5 7S1.5 12 1.5 12Z"
                            stroke="currentColor"
                            strokeWidth="1.7"
                            fill="none"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                          />
                          <circle
                            cx="12"
                            cy="12"
                            r="3"
                            stroke="currentColor"
                            strokeWidth="1.7"
                            fill="none"
                          />
                        </svg>
                      </button>
                    </td>
                  </tr>
                );
              })}

              {logs.length === 0 && !loading && (
                <tr>
                  <td colSpan={6} style={{ textAlign: "center", padding: 16 }}>
                    Không có dữ liệu.
                  </td>
                </tr>
              )}
            </tbody>
          </table>

          <div className="pager">
            <div className="row" style={{ gap: 8, alignItems: "center" }}>
              <button type="button" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                Trước
              </button>
              <span>
                Trang {page}/{totalPages}
              </span>
              <button type="button" disabled={page >= totalPages} onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>
                Tiếp
              </button>
            </div>

            <div className="row" style={{ gap: 8, alignItems: "center" }}>
              <span>Hiển thị</span>
              <select value={pageSize} onChange={(e) => setPageSize(Number(e.target.value) || 20)}>
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
                <option value={100}>100</option>
              </select>
              <span>bản ghi mỗi trang</span>
            </div>
          </div>
        </div>
      </div>

      <AuditLogDetailModal open={!!selectedLog} log={selectedLog} detail={detail} loading={detailLoading} onClose={closeDetail} />

      <ToastContainer toasts={toasts} onRemove={removeToast} confirmDialog={confirmDialog} />
    </>
  );
}
