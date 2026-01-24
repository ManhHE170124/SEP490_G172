// File: src/pages/admin/SystemInsightsDashboardPage.jsx
import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";

import {
  ResponsiveContainer,
  ComposedChart,
  CartesianGrid,
  XAxis,
  YAxis,
  Tooltip,
  Legend,
  Line,
  Bar,
  Brush,
  BarChart,
} from "recharts";

import axiosClient from "../../api/axiosClient";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./SystemInsightsDashboardPage.css";

/* =========================
   VN MAPPING (tái sử dụng)
   ========================= */

const ACTION_VI = {
  // ===== AUTH / ACCOUNT =====
  Login: "Đăng nhập",
  Register: "Đăng ký",
  ChangePassword: "Đổi mật khẩu",
  ResetPassword: "Đặt lại mật khẩu",
  RevokeToken: "Đăng xuất thiết bị",

  UpdateProfile: "Cập nhật hồ sơ",
  UpdatePermissions: "Cập nhật phân quyền",

  // ===== PRODUCT ACCOUNT =====
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

  // ===== TICKET =====
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
  ChangeOrderStatus: "Nhân viên đổi trạng thái đơn hàng",
  PaymentStatusChanged: "Cập nhật trạng thái thanh toán",
  OrderStatusChanged: "Đồng bộ trạng thái đơn hàng theo thanh toán",
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

  // ===== PRODUCT / VARIANT =====
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

  // ===== LICENSE PACKAGE =====
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

  // ===== GENERIC =====
  Assign: "Gán xử lý",
  Unassign: "Hủy gán xử lý",
  Claim: "Nhận xử lý",
  Close: "Đóng",
  Complete: "Hoàn tất",
  Reorder: "Sắp xếp lại",
  Deactivate: "Vô hiệu hóa",

  Save: "Lưu thay đổi",
  Create: "Tạo mới",
  Update: "Cập nhật",
  Delete: "Xóa",
  Toggle: "Bật/Tắt",
};

const ENTITY_VI = {
  Account: "Tài khoản",
  User: "Người dùng",
  Role: "Vai trò",

  Product: "Sản phẩm",
  ProductVariant: "Biến thể sản phẩm",
  ProductVariantImage: "Ảnh biến thể sản phẩm",
  ProductSection: "Mục nội dung sản phẩm",
  Category: "Danh mục",
  Badge: "Nhãn sản phẩm",

  ProductKey: "Key sản phẩm",
  ProductKeyAssignment: "Gán key cho đơn hàng",
  ProductAccount: "Tài khoản sản phẩm",
  ProductAccountCustomer: "Khách - tài khoản sản phẩm",

  LicensePackage: "Gói bản quyền",
  Supplier: "Nhà cung cấp",

  Order: "Đơn hàng",
  Payment: "Thanh toán",

  Ticket: "Ticket hỗ trợ",
  TicketReply: "Phản hồi ticket",
  TicketSubjectTemplate: "Mẫu chủ đề ticket",
  SupportPlan: "Gói hỗ trợ",
  UserSupportPlanSubscription: "Đăng ký gói hỗ trợ",
  SupportChatSession: "Phiên chat hỗ trợ",
  SupportPriorityLoyaltyRule: "Quy tắc nâng hạng ưu tiên theo chi tiêu",
  SlaRule: "Quy tắc SLA",

  Faq: "Câu hỏi thường gặp",
  ProductReport: "Báo cáo lỗi sản phẩm",
  WebsiteSettings: "Thiết lập website",
};

const normalizeRoleKey = (value) =>
  String(value || "")
    .trim()
    .toUpperCase()
    .replace(/\s+/g, "_")
    .replace(/-+/g, "_");

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

const viAction = (v) => ACTION_VI[String(v || "").trim()] || (v || "-");
const viEntity = (v) => ENTITY_VI[String(v || "").trim()] || (v || "-");
const viRole = (v) => {
  const raw = String(v || "").trim();
  if (!raw) return ROLE_VI.SYSTEM;
  const k = normalizeRoleKey(raw);
  return ROLE_VI[k] || raw || "-";
};

// ===== Notifications mapping từ AdminNotificationsPage.jsx =====
function notificationTypeLabel(type) {
  const v = String(type || "").trim();
  if (!v) return "Quản trị viên tạo";

  const raw = v.toLowerCase();
  const key = raw.replace(/[^a-z0-9]/g, "");

  if (key === "manual") return "Thủ công";
  if (key === "system") return "Hệ thống";

  const map = {
    ticketassigned: "Gán ticket",
    tickettransferred: "Chuyển ticket",
    ticketstaffreplied: "Ticket có phản hồi",
    keyimportcsv: "Nhập key hàng loạt",
    productreportcreated: "Báo lỗi sản phẩm",
    productaccountcustomerrevoked: "Thu hồi quyền truy cập tài khoản",
    ordercreated: "Đơn hàng mới",
  };

  return map[key] || v;
}

/* =========================
   Helpers: format / date
   ========================= */
const pad2 = (n) => String(n).padStart(2, "0");
const fmtInt = (n) => new Intl.NumberFormat("vi-VN").format(Number(n || 0));
const fmtPercent = (v01) => {
  const x = typeof v01 === "number" ? v01 : Number(v01 || 0);
  return `${(x * 100).toFixed(1).replace(".", ",")}%`;
};
const fmtPercent100 = (v) => {
  const x = typeof v === "number" ? v : Number(v || 0);
  return `${x.toFixed(1).replace(".", ",")}%`;
};

const dmy = (d) => `${pad2(d.getDate())}/${pad2(d.getMonth() + 1)}/${d.getFullYear()}`;

const localStartOfDay = (d) => new Date(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0, 0);
const addDaysLocal = (d, days) => {
  const x = new Date(d);
  x.setDate(x.getDate() + Number(days || 0));
  return x;
};

// "YYYY-MM-DDTHH:mm:ss" (không timezone) để BE treat as local unspecified
const toLocalUnspecifiedIsoFromDate = (d) => {
  const yyyy = d.getFullYear();
  const mm = pad2(d.getMonth() + 1);
  const dd = pad2(d.getDate());
  const hh = pad2(d.getHours());
  const mi = pad2(d.getMinutes());
  const ss = pad2(d.getSeconds());
  return `${yyyy}-${mm}-${dd}T${hh}:${mi}:${ss}`;
};

const bucketLabelVi = (bucketStartLocal) => {
  const s = String(bucketStartLocal || "").trim();
  if (!s) return "";
  // "YYYY-MM-DDTHH:00:00"
  if (s.length >= 13 && s.includes("T")) {
    const dd = s.slice(8, 10);
    const mm = s.slice(5, 7);
    const yyyy = s.slice(0, 4);
    const hh = s.slice(11, 13);
    return `${dd}/${mm}/${yyyy} ${hh}h`;
  }
  // "YYYY-MM-DD"
  if (s.length >= 10 && s.includes("-")) {
    const dd = s.slice(8, 10);
    const mm = s.slice(5, 7);
    const yyyy = s.slice(0, 4);
    return `${dd}/${mm}/${yyyy}`;
  }
  return s;
};

const weekdayViByMon0 = (d) => {
  const map = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];
  return map[d] || "";
};

/* =========================
   API
   ========================= */
async function fetchSystemInsightsOverview({ fromLocalIso, toLocalExclusiveIso, bucket }) {
  const params = {
    fromLocal: fromLocalIso || undefined,
    toLocalExclusive: toLocalExclusiveIso || undefined,
    bucket: bucket || "day",
  };

  const res = await axiosClient.get("system-insights-dashboard/overview", { params });
  return res?.data ?? res;
}

/* =========================
   Tooltips (Recharts)
   ========================= */
const AuditTrendTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null;
  const row = payload?.[0]?.payload || {};
  return (
    <div className="sid-tooltip">
      <div className="sid-tooltip-title">{label}</div>
      <div className="sid-tooltip-row">
        <span className="sid-tooltip-k">Thao tác</span>
        <span className="sid-tooltip-v">{fmtInt(row.count)}</span>
      </div>
    </div>
  );
};

const NotiTrendTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null;
  const row = payload?.[0]?.payload || {};
  return (
    <div className="sid-tooltip">
      <div className="sid-tooltip-title">{label}</div>
      <div className="sid-tooltip-body">
        <div className="sid-tooltip-row">
          <span className="sid-tooltip-k">Tổng</span>
          <span className="sid-tooltip-v">{fmtInt(row.total)}</span>
        </div>
        <div className="sid-tooltip-row">
          <span className="sid-tooltip-k">Hệ thống</span>
          <span className="sid-tooltip-v">{fmtInt(row.system)}</span>
        </div>
        <div className="sid-tooltip-row">
          <span className="sid-tooltip-k">Thủ công</span>
          <span className="sid-tooltip-v">{fmtInt(row.manual)}</span>
        </div>
      </div>
    </div>
  );
};

const ReadRateTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null;
  const row = payload?.[0]?.payload || {};
  return (
    <div className="sid-tooltip">
      <div className="sid-tooltip-title">{label}</div>
      <div className="sid-tooltip-row">
        <span className="sid-tooltip-k">Tỷ lệ đã đọc</span>
        <span className="sid-tooltip-v">{fmtPercent100(row.readRatePct)}</span>
      </div>
    </div>
  );
};

/* =========================
   Small components
   ========================= */
function TopBars({
  data,
  height = 280,
  valueKey = "value",
  labelKey = "label",
  barName = "Số lần",
  barColor = "#2563eb",
}) {
  const list = Array.isArray(data) ? data : [];
  if (!list.length) return <div className="sid-empty">Chưa có dữ liệu</div>;

  const sorted = list
    .slice()
    .sort((a, b) => Number(b[valueKey] || 0) - Number(a[valueKey] || 0))
    .slice(0, 10);

  return (
    <div className="sid-chart">
      <ResponsiveContainer width="100%" height={height}>
        <BarChart
          data={sorted}
          layout="vertical"
          margin={{ top: 8, right: 10, bottom: 0, left: 10 }}
        >
          <CartesianGrid stroke="#eef2f7" />
          <XAxis type="number" allowDecimals={false} />
          <YAxis type="category" dataKey={labelKey} width={170} tick={{ fontSize: 12 }} />
          <Tooltip formatter={(v) => fmtInt(v)} />
          <Legend />
          <Bar
            dataKey={valueKey}
            name={barName}
            fill={barColor}
            isAnimationActive={false}
            barSize={16}
            radius={[8, 8, 8, 8]}
          />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}

function Heatmap({ cells }) {
  const data = Array.isArray(cells) ? cells : [];
  if (!data.length) return <div className="sid-empty">Chưa có dữ liệu</div>;

  const max = Math.max(1, ...data.map((c) => Number(c.count || 0)));
  const clamp01 = (x) => Math.max(0, Math.min(1, x));

  const getCount = (d, h) => {
    const found = data.find(
      (x) => Number(x.dayIndex) === d && Number(x.hour) === h
    );
    return found ? Number(found.count || 0) : 0;
  };

  return (
    <div className="sid-heatmap-wrap">
      <div className="sid-heatmap-head">
        <div className="sid-heatmap-corner">Giờ</div>
        <div className="sid-heatmap-hours">
          {Array.from({ length: 24 }, (_, h) => (
            <div key={h} className="sid-heatmap-hour">
              {h}
            </div>
          ))}
        </div>
      </div>

      <div className="sid-heatmap-body">
        {Array.from({ length: 7 }, (_, d) => (
          <div key={d} className="sid-heatmap-row">
            <div className="sid-heatmap-day">{weekdayViByMon0(d)}</div>
            <div className="sid-heatmap-cells">
              {Array.from({ length: 24 }, (_, h) => {
                const c = getCount(d, h);
                const t = clamp01(c / max);
                return (
                  <div
                    key={`${d}-${h}`}
                    className="sid-heatmap-cell"
                    style={{ opacity: 0.15 + 0.85 * t }}
                    title={`${weekdayViByMon0(d)} • ${h}h: ${fmtInt(
                      c
                    )} thao tác`}
                  />
                );
              })}
            </div>
          </div>
        ))}
      </div>

      <div className="sid-heatmap-legend">
        <span>Ít</span>
        <div className="sid-heatmap-grad" />
        <span>Nhiều</span>
      </div>
    </div>
  );
}

/* =========================
   MAIN PAGE
   ========================= */
export default function SystemInsightsDashboardPage() {
  const [toasts, setToasts] = useState([]);
  const toastIdRef = useRef(1);
  const addToast = (type, message, title) => {
    const id = toastIdRef.current++;
    setToasts((prev) => [...prev, { id, type, message, title }]);
    setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== id)), 4500);
  };

  const [preset, setPreset] = useState("last30");
  const [bucket, setBucket] = useState("day");

  const [customDayRange, setCustomDayRange] = useState(() => {
    const today0 = localStartOfDay(new Date());
    const end = today0;
    const start = addDaysLocal(end, -6);
    return [start, end];
  });

  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState("");
  const [data, setData] = useState(null);

  const range = useMemo(() => {
    const today0 = localStartOfDay(new Date());

    if (preset === "today") {
      const from = today0;
      const toExclusive = addDaysLocal(from, 1);
      const displayTo = addDaysLocal(toExclusive, -1);
      return { from, toExclusive, displayTo };
    }

    if (preset === "last7") {
      const toExclusive = addDaysLocal(today0, 1);
      const from = addDaysLocal(toExclusive, -7);
      const displayTo = addDaysLocal(toExclusive, -1);
      return { from, toExclusive, displayTo };
    }

    if (preset === "last30") {
      const toExclusive = addDaysLocal(today0, 1);
      const from = addDaysLocal(toExclusive, -30);
      const displayTo = addDaysLocal(toExclusive, -1);
      return { from, toExclusive, displayTo };
    }

    const s = customDayRange?.[0]
      ? localStartOfDay(new Date(customDayRange[0]))
      : today0;
    const e = customDayRange?.[1]
      ? localStartOfDay(new Date(customDayRange[1]))
      : s;
    const from = s;
    const toExclusive = addDaysLocal(e, 1);
    const displayTo = e;
    if (toExclusive <= from)
      return { from, toExclusive: addDaysLocal(from, 1), displayTo: from };
    return { from, toExclusive, displayTo };
  }, [preset, customDayRange]);

  const rangeText = useMemo(
    () => `${dmy(range.from)} → ${dmy(range.displayTo)} (UTC+7)`,
    [range]
  );

  const load = useCallback(async (r, bkt) => {
    setErr("");
    setLoading(true);

    try {
      const fromLocalIso = toLocalUnspecifiedIsoFromDate(r.from);
      const toLocalExclusiveIso = toLocalUnspecifiedIsoFromDate(r.toExclusive);

      const res = await fetchSystemInsightsOverview({
        fromLocalIso,
        toLocalExclusiveIso,
        bucket: bkt,
      });

      setData(res);
    } catch (e) {
      const msg =
        e?.response?.data?.message ||
        e?.message ||
        "Không tải được dữ liệu giám sát hệ thống.";
      setErr(msg);
      setData(null);
      addToast("error", msg, "Lỗi");
    } finally {
      setLoading(false);
    }
  }, []);

  const debounceRef = useRef(null);
  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => load(range, bucket), 250);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [range, bucket, load]);

  const resetFilters = useCallback(() => {
    setPreset("last30");
    setBucket("day");
    const today0 = localStartOfDay(new Date());
    setCustomDayRange([addDaysLocal(today0, -6), today0]);
  }, []);

  /* ===== derived data ===== */
  const auditTrend = useMemo(() => {
    const raw = data?.auditActionsSeries || data?.AuditActionsSeries || [];
    return raw.map((p) => ({
      label: bucketLabelVi(p.bucketStartLocal || p.BucketStartLocal),
      count: Number(p.count ?? p.Count ?? 0),
      raw: p.bucketStartLocal || p.BucketStartLocal,
    }));
  }, [data]);

  const notiTrend = useMemo(() => {
    const raw = data?.notificationsDaily || data?.NotificationsDaily || [];
    return raw.map((d) => ({
      label: bucketLabelVi(d.dateLocal || d.DateLocal),
      total: Number(d.total ?? d.Total ?? 0),
      system: Number(d.system ?? d.System ?? 0),
      manual: Number(d.manual ?? d.Manual ?? 0),
    }));
  }, [data]);

  const readRateTrend = useMemo(() => {
    const raw =
      data?.notificationReadRateDaily || data?.NotificationReadRateDaily || [];
    return raw.map((d) => ({
      label: bucketLabelVi(d.dateLocal || d.DateLocal),
      readRatePct:
        Math.round(
          Number(d.readRate ?? d.ReadRate ?? 0) * 1000
        ) / 10,
    }));
  }, [data]);

  const topAuditActions = useMemo(() => {
    const raw = data?.topAuditActions || data?.TopAuditActions || [];
    return raw.map((x) => ({
      label: viAction(x.name ?? x.Name),
      value: Number(x.count ?? x.Count ?? 0),
    }));
  }, [data]);

  const topAuditEntities = useMemo(() => {
    const raw = data?.topAuditEntityTypes || data?.TopAuditEntityTypes || [];
    return raw.map((x) => ({
      label: viEntity(x.name ?? x.Name),
      value: Number(x.count ?? x.Count ?? 0),
    }));
  }, [data]);

  const topNotiTypes = useMemo(() => {
    const raw = data?.topNotificationTypes || data?.TopNotificationTypes || [];
    return raw.map((x) => ({
      label: notificationTypeLabel(x.name ?? x.Name),
      value: Number(x.count ?? x.Count ?? 0),
    }));
  }, [data]);

  const notiSeverityDaily = useMemo(() => {
    const raw =
      data?.notificationsSeverityDaily || data?.NotificationsSeverityDaily || [];
    return raw.map((d) => {
      const info = Number(d.info ?? d.Info ?? 0);
      const success = Number(d.success ?? d.Success ?? 0);
      const warning = Number(d.warning ?? d.Warning ?? 0);
      const error = Number(d.error ?? d.Error ?? 0);
      return {
        label: bucketLabelVi(d.dateLocal || d.DateLocal),
        info,
        success,
        warning,
        error,
        total: info + success + warning + error,
      };
    });
  }, [data]);

  const auditHeatmap = useMemo(
    () => data?.auditHeatmap || data?.AuditHeatmap || [],
    [data]
  );

  // KPIs
  const sysAct = data?.systemActivity || data?.SystemActivity || {};
  const notiHealth = data?.notificationsHealth || data?.NotificationsHealth || {};

  const totalActions = Number(sysAct.totalActions ?? sysAct.TotalActions ?? 0);
  const systemActions = Number(sysAct.systemActions ?? sysAct.SystemActions ?? 0);
  const systemActionRate =
    Number(
      sysAct.systemActionRate ??
        sysAct.SystemActionRate ??
        (totalActions > 0 ? systemActions / totalActions : 0)
    );
  const userActions = Math.max(0, totalActions - systemActions);
  const userActionRate = totalActions > 0 ? userActions / totalActions : 0;

  const overallReadRate = Number(
    notiHealth.overallReadRate ?? notiHealth.OverallReadRate ?? 0
  );

  const C_MAIN = "#2563eb";
  const C_SYS = "#16a34a";
  const C_MANUAL = "#f59e0b";

  return (
    <>
      <div className="sid-page">
        <div className="sid-head">
          <div>
            <h1 className="sid-title">Bảng điều khiển Giám sát hệ thống</h1>
            <div className="sid-sub">
              <span className="sid-subrange">{rangeText}</span>
              {loading ? <span className="sid-loadingdot"> • Đang tải…</span> : null}
            </div>
          </div>

          <div className="sid-actions">
            <button
              className="sid-btn sid-btn-ghost"
              onClick={resetFilters}
              disabled={loading}
              type="button"
            >
              Reset
            </button>
          </div>
        </div>

        {/* Filters */}
        <section className="sid-section">
          <div className="sid-section-title">Bộ lọc thời gian</div>

          <div className="sid-filters">
            <div className="sid-filterbar">
              <div className="sid-seg sid-seg-wide">
                <button
                  className={`sid-segbtn ${preset === "today" ? "is-on" : ""}`}
                  onClick={() => setPreset("today")}
                  type="button"
                >
                  Hôm nay
                </button>
                <button
                  className={`sid-segbtn ${preset === "last7" ? "is-on" : ""}`}
                  onClick={() => setPreset("last7")}
                  type="button"
                >
                  7 ngày
                </button>
                <button
                  className={`sid-segbtn ${preset === "last30" ? "is-on" : ""}`}
                  onClick={() => setPreset("last30")}
                  type="button"
                >
                  30 ngày
                </button>
                <button
                  className={`sid-segbtn ${preset === "custom" ? "is-on" : ""}`}
                  onClick={() => setPreset("custom")}
                  type="button"
                >
                  Tuỳ chọn
                </button>
              </div>

              <div className="sid-compact">
                <div className="sid-compact-row">
                  <span className="sid-compact-label">Nhóm theo</span>
                  <select
                    className="sid-input sid-mini"
                    value={bucket}
                    onChange={(e) => setBucket(e.target.value)}
                  >
                    <option value="day">Theo ngày</option>
                    <option value="hour">Theo giờ</option>
                  </select>
                </div>

                {preset === "custom" ? (
                  <div className="sid-compact-row">
                    <DatePicker
                      selectsRange
                      startDate={
                        customDayRange?.[0]
                          ? new Date(customDayRange[0])
                          : null
                      }
                      endDate={
                        customDayRange?.[1]
                          ? new Date(customDayRange[1])
                          : null
                      }
                      onChange={(update) => setCustomDayRange(update)}
                      className="sid-input sid-range-one"
                      dateFormat="dd/MM/yyyy"
                      placeholderText="Chọn khoảng ngày"
                    />
                  </div>
                ) : null}
              </div>
            </div>
          </div>

          {err ? <div className="sid-error">{err}</div> : null}
        </section>

        {/* KPI: Audit */}
        <section className="sid-section">
          <div className="sid-kpis">
            <div className="sid-card">
              <div className="sid-card-label">Tổng số thao tác</div>
              <div className="sid-card-val">{fmtInt(totalActions)}</div>
              <div className="sid-card-foot">
                Số thao tác với hệ thống trong khoảng thời gian
              </div>
            </div>

            <div className="sid-card">
              <div className="sid-card-label">Số người thao tác</div>
              <div className="sid-card-val">
                {fmtInt(sysAct.uniqueActors ?? sysAct.UniqueActors ?? 0)}
              </div>
              <div className="sid-card-foot">
                Số email người thao tác (không trùng)
              </div>
            </div>

            <div className="sid-card">
              <div className="sid-card-label">Thao tác do hệ thống</div>
              <div className="sid-card-val">{fmtInt(systemActions)}</div>
              <div className="sid-card-foot">
                Tỷ lệ: <b>{fmtPercent(systemActionRate)}</b>
              </div>
            </div>

            <div className="sid-card">
              <div className="sid-card-label">Thao tác do người dùng</div>
              <div className="sid-card-val">{fmtInt(userActions)}</div>
              <div className="sid-card-foot">
                Tỷ lệ: <b>{fmtPercent(userActionRate)}</b>
              </div>
            </div>
          </div>
        </section>

        {/* AUDIT charts */}
        <section className="sid-section">
          <div className="sid-section-title">Thao tác với hệ thống</div>

          {/* Hàng 1: Trend theo thời gian + Heatmap (giống nhau về "khi nào") */}
          <div className="sid-grid-2 sid-row">
            <div className="sid-panel">
              <div className="sid-panel-head">
                <div>
                  <div className="sid-panel-title">Số thao tác theo thời gian</div>
                  <div className="sid-panel-sub">
                    Nhóm theo: {bucket === "hour" ? "giờ" : "ngày"}
                  </div>
                </div>
                <div className="sid-unit">Thao tác</div>
              </div>

              <div className="sid-chart">
                <ResponsiveContainer width="100%" height={280}>
                  <ComposedChart
                    data={auditTrend}
                    margin={{ top: 8, right: 10, bottom: 0, left: 0 }}
                  >
                    <CartesianGrid stroke="#eef2f7" />
                    <XAxis
                      dataKey="label"
                      interval="preserveStartEnd"
                      tickMargin={8}
                    />
                    <YAxis allowDecimals={false} />
                    <Tooltip content={<AuditTrendTooltip />} />
                    <Legend />
                    <Bar
                      dataKey="count"
                      name="Thao tác"
                      fill={C_MAIN}
                      isAnimationActive={false}
                      barSize={22}
                    />
                    <Line
                      type="monotone"
                      dataKey="count"
                      name="Xu hướng"
                      stroke={C_MAIN}
                      strokeWidth={2}
                      dot={false}
                      isAnimationActive={false}
                    />
                    {auditTrend.length > 20 ? (
                      <Brush dataKey="label" height={20} travellerWidth={10} />
                    ) : null}
                  </ComposedChart>
                </ResponsiveContainer>
              </div>
            </div>

            <div className="sid-panel">
              <div className="sid-panel-head">
                <div>
                  <div className="sid-panel-title">Bản đồ nhiệt hoạt động</div>
                  <div className="sid-panel-sub">
                    Giờ trong ngày × Thứ trong tuần
                  </div>
                </div>
              </div>
              <Heatmap cells={auditHeatmap} />
            </div>
          </div>

          {/* Hàng 2: Top hành động + Top loại đối tượng (giống nhau về "cái gì") */}
          <div className="sid-grid-2 sid-row">
            <div className="sid-panel">
              <div className="sid-panel-head">
                <div>
                  <div className="sid-panel-title">Top hành động</div>
                  <div className="sid-panel-sub">
                    Hành động diễn ra nhiều nhất
                  </div>
                </div>
              </div>
              <TopBars
                data={topAuditActions.slice(0, 5)}
                height={280}
                barName="Số lần"
                barColor={C_MAIN}
              />
            </div>

            <div className="sid-panel">
              <div className="sid-panel-head">
                <div>
                  <div className="sid-panel-title">Top loại đối tượng</div>
                </div>
              </div>
              <TopBars
                data={topAuditEntities.slice(0, 5)}
                height={280}
                barName="Số lần"
                barColor="#6d28d9"
              />
            </div>
          </div>
        </section>

        {/* Notifications charts */}
        <section className="sid-section">
          <div className="sid-section-title">Thông báo</div>

          {/* Hàng 1: Trend số thông báo + Trend read rate (cùng "theo thời gian") */}
          <div className="sid-grid-2 sid-row">
            <div className="sid-panel">
              <div className="sid-panel-head">
                <div>
                  <div className="sid-panel-title">
                    Số thông báo theo thời gian
                  </div>
                  <div className="sid-panel-sub">
                  </div>
                </div>
                <div className="sid-unit">Thông báo</div>
              </div>

              <div className="sid-chart">
                <ResponsiveContainer width="100%" height={280}>
                  <ComposedChart
                    data={notiTrend}
                    margin={{ top: 8, right: 10, bottom: 0, left: 0 }}
                  >
                    <CartesianGrid stroke="#eef2f7" />
                    <XAxis
                      dataKey="label"
                      interval="preserveStartEnd"
                      tickMargin={8}
                    />
                    <YAxis allowDecimals={false} />
                    <Tooltip content={<NotiTrendTooltip />} />
                    <Legend />
                    <Bar
                      dataKey="total"
                      name="Tổng"
                      fill={C_MANUAL}
                      isAnimationActive={false}
                      barSize={18}
                    />
                    <Line
                      type="monotone"
                      dataKey="system"
                      name="Hệ thống"
                      stroke={C_SYS}
                      strokeWidth={2}
                      dot={false}
                      isAnimationActive={false}
                    />
                    <Line
                      type="monotone"
                      dataKey="manual"
                      name="Thủ công"
                      stroke={C_MAIN}
                      strokeWidth={2}
                      dot={false}
                      isAnimationActive={false}
                    />
                    {notiTrend.length > 20 ? (
                      <Brush dataKey="label" height={20} travellerWidth={10} />
                    ) : null}
                  </ComposedChart>
                </ResponsiveContainer>
              </div>
            </div>

            <div className="sid-panel">
              <div className="sid-panel-head">
                <div>
                  <div className="sid-panel-title">
                    Tỷ lệ đã đọc theo thời gian
                  </div>
                  <div className="sid-panel-sub">(theo ngày)</div>
                </div>
                <div className="sid-unit">%</div>
              </div>

              <div className="sid-chart">
                <ResponsiveContainer width="100%" height={280}>
                  <ComposedChart
                    data={readRateTrend}
                    margin={{ top: 8, right: 10, bottom: 0, left: 0 }}
                  >
                    <CartesianGrid stroke="#eef2f7" />
                    <XAxis
                      dataKey="label"
                      interval="preserveStartEnd"
                      tickMargin={8}
                    />
                    <YAxis domain={[0, 100]} tickFormatter={(v) => `${v}%`} />
                    <Tooltip content={<ReadRateTooltip />} />
                    <Legend />
                    <Line
                      type="monotone"
                      dataKey="readRatePct"
                      name="Tỷ lệ đã đọc"
                      stroke={C_SYS}
                      strokeWidth={2}
                      dot={false}
                      isAnimationActive={false}
                    />
                    {readRateTrend.length > 20 ? (
                      <Brush dataKey="label" height={20} travellerWidth={10} />
                    ) : null}
                  </ComposedChart>
                </ResponsiveContainer>
              </div>

              <div className="sid-note">
              </div>
            </div>
          </div>

          {/* Hàng 2: Top loại thông báo + Severity theo ngày (cùng về phân loại nội dung) */}
          <div className="sid-grid-2 sid-row">
            <div className="sid-panel">
              <div className="sid-panel-head">
                <div>
                  <div className="sid-panel-title">Top loại thông báo</div>
                </div>
              </div>
              <TopBars
                data={topNotiTypes.slice(0, 5)}
                height={280}
                barName="Số thông báo"
                barColor="#f59e0b"
              />
            </div>

            <div className="sid-panel">
              <div className="sid-panel-head">
                <div>
                  <div className="sid-panel-title">Mức độ thông báo theo ngày</div>
                  <div className="sid-panel-sub">
                  </div>
                </div>
              </div>

              <div className="sid-chart">
                <ResponsiveContainer width="100%" height={260}>
                  <BarChart data={notiSeverityDaily}>
                    <CartesianGrid stroke="#eef2f7" vertical={false} />
                    <XAxis dataKey="label" />
                    <YAxis allowDecimals={false} />
                    <Tooltip />
                    <Legend />
                    <Bar dataKey="info" name="Thông tin" stackId="sev" />
                    <Bar dataKey="success" name="Thành công" stackId="sev" />
                    <Bar dataKey="warning" name="Cảnh báo" stackId="sev" />
                    <Bar dataKey="error" name="Lỗi" stackId="sev" />
                  </BarChart>
                </ResponsiveContainer>
              </div>

              <div className="sid-note">
              </div>
            </div>
          </div>
        </section>
      </div>

      <ToastContainer
        toasts={toasts}
        onRemove={(id) => setToasts((p) => p.filter((t) => t.id !== id))}
      />
    </>
  );
}
