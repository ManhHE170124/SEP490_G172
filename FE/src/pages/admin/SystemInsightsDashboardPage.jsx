// File: src/pages/admin/SystemInsightsDashboardPage.jsx
import React from "react";
import axiosClient from "../../api/axiosClient";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./SystemInsightsDashboardPage.css";

/* =========================
   VN MAPPING (tái sử dụng từ AuditLogsPage.jsx + AdminNotificationsPage.jsx)
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

const normalizeRoleKey = (value) =>
  String(value || "")
    .trim()
    .toUpperCase()
    .replace(/\s+/g, "_")
    .replace(/-+/g, "_");

const viAction = (v) => ACTION_VI[String(v || "").trim()] || (v || "-");
const viEntity = (v) => ENTITY_VI[String(v || "").trim()] || (v || "-");
const viRole = (v) => {
  const raw = String(v || "").trim();
  if (!raw) return ROLE_VI.SYSTEM;
  const k = normalizeRoleKey(raw);
  return ROLE_VI[k] || raw || "-";
};

// ===== Notifications mapping từ AdminNotificationsPage.jsx =====
function severityLabel(sev) {
  switch (sev) {
    case 0:
      return "Thông tin";
    case 1:
      return "Thành công";
    case 2:
      return "Cảnh báo";
    case 3:
      return "Lỗi";
    default:
      return String(sev ?? "");
  }
}
function notificationTypeLabel(type) {
  const v = String(type || "").trim();
  if (!v) return "-";

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
   Helpers format / date
   ========================= */
const fmtInt = (n) => {
  const x = typeof n === "number" ? n : Number(n || 0);
  return x.toLocaleString("vi-VN");
};
const fmtPercent = (v) => {
  const x = typeof v === "number" ? v : Number(v || 0);
  return `${(x * 100).toFixed(1).replace(".", ",")}%`;
};
const clamp01 = (x) => Math.max(0, Math.min(1, x));

const pad2 = (s) => String(s).padStart(2, "0");
const ymdToDmy = (ymd) => {
  const t = String(ymd || "").trim();
  if (!t) return "";
  // accept "YYYY-MM-DD" or "YYYY-MM-DDTHH..."
  const datePart = t.slice(0, 10);
  const [y, m, d] = datePart.split("-");
  if (!y || !m || !d) return t;
  return `${pad2(d)}/${pad2(m)}/${y}`;
};
const bucketLabelVi = (bucketStartLocal) => {
  const s = String(bucketStartLocal || "").trim();
  if (!s) return "";
  if (s.length >= 13 && s.includes("T")) {
    // "YYYY-MM-DDTHH:00:00"
    const datePart = s.slice(0, 10);
    const hh = s.slice(11, 13);
    return `${ymdToDmy(datePart)} ${hh}h`;
  }
  return ymdToDmy(s);
};

const weekdayViByMon0 = (d) => {
  // Mon=0..Sun=6
  const map = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];
  return map[d] || "";
};

const toLocalUnspecifiedIso = (dateYmd, hour = 0, minute = 0, second = 0) => {
  // returns "YYYY-MM-DDTHH:mm:ss" (không timezone) để BE treat as local unspecified
  const d = String(dateYmd || "").trim();
  if (!d) return "";
  return `${d}T${pad2(hour)}:${pad2(minute)}:${pad2(second)}`;
};

const addDaysYmd = (ymd, days) => {
  // ymd: "YYYY-MM-DD" -> add days (local)
  const [y, m, d] = String(ymd || "").split("-").map((x) => Number(x));
  if (!y || !m || !d) return ymd;
  const dt = new Date(y, m - 1, d);
  dt.setDate(dt.getDate() + Number(days || 0));
  const yy = dt.getFullYear();
  const mm = pad2(dt.getMonth() + 1);
  const dd = pad2(dt.getDate());
  return `${yy}-${mm}-${dd}`;
};

/* =========================
   Simple SVG charts (no libs)
   ========================= */

function SvgLineChart({ points, height = 180, valueTitle = "Số lượng", xTitle = "Thời gian" }) {
  const w = 1000; // internal viewBox width
  const h = 300;  // internal viewBox height

  const data = Array.isArray(points) ? points : [];
  if (data.length === 0) {
    return <div className="sid-chart-empty">Không có dữ liệu.</div>;
  }

  const ys = data.map((p) => Number(p.y || 0));
  const yMax = Math.max(1, ...ys);
  const yMin = 0;

  const padL = 55;
  const padR = 20;
  const padT = 18;
  const padB = 40;

  const innerW = w - padL - padR;
  const innerH = h - padT - padB;

  const xOf = (i) => padL + (data.length === 1 ? innerW / 2 : (i * innerW) / (data.length - 1));
  const yOf = (v) => padT + innerH - ((v - yMin) * innerH) / (yMax - yMin);

  const path = data
    .map((p, i) => `${i === 0 ? "M" : "L"} ${xOf(i)} ${yOf(Number(p.y || 0))}`)
    .join(" ");

  // ticks: show at most 6 labels
  const tickCount = Math.min(6, data.length);
  const tickIdx = Array.from({ length: tickCount }, (_, k) =>
    Math.round((k * (data.length - 1)) / Math.max(1, tickCount - 1))
  );

  return (
    <div className="sid-chart">
      <svg viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" style={{ height }}>
        {/* axes */}
        <line x1={padL} y1={padT} x2={padL} y2={padT + innerH} className="sid-axis" />
        <line x1={padL} y1={padT + innerH} x2={padL + innerW} y2={padT + innerH} className="sid-axis" />

        {/* y labels */}
        <text x={10} y={padT + 12} className="sid-axis-label">{valueTitle}</text>
        <text x={padL - 8} y={padT + innerH + 5} textAnchor="end" className="sid-tick-text">0</text>
        <text x={padL - 8} y={padT + 12} textAnchor="end" className="sid-tick-text">{fmtInt(yMax)}</text>

        {/* line */}
        <path d={path} className="sid-line" />

        {/* points */}
        {data.map((p, i) => (
          <circle key={i} cx={xOf(i)} cy={yOf(Number(p.y || 0))} r={4} className="sid-dot">
            <title>{`${p.x}: ${fmtInt(p.y)} ${valueTitle.toLowerCase()}`}</title>
          </circle>
        ))}

        {/* x labels */}
        <text x={padL} y={h - 8} className="sid-axis-label">{xTitle}</text>
        {tickIdx.map((i) => (
          <text
            key={i}
            x={xOf(i)}
            y={padT + innerH + 20}
            textAnchor="middle"
            className="sid-tick-text"
          >
            {String(data[i]?.x || "")}
          </text>
        ))}
      </svg>
    </div>
  );
}

function SvgHorizontalBarChart({ items, height = 260, labelTitle = "Tên", valueTitle = "Số lượng" }) {
  const data = Array.isArray(items) ? items : [];
  if (data.length === 0) return <div className="sid-chart-empty">Không có dữ liệu.</div>;

  const w = 1000;
  const h = 300;
  const padL = 290;
  const padR = 25;
  const padT = 18;
  const padB = 30;

  const innerW = w - padL - padR;
  const innerH = h - padT - padB;

  const maxV = Math.max(1, ...data.map((d) => Number(d.value || 0)));
  const barH = innerH / data.length;

  return (
    <div className="sid-chart">
      <svg viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" style={{ height }}>
        <text x={10} y={padT + 12} className="sid-axis-label">{labelTitle}</text>
        <text x={padL} y={padT + 12} className="sid-axis-label">{valueTitle}</text>

        {/* axes */}
        <line x1={padL} y1={padT} x2={padL} y2={padT + innerH} className="sid-axis" />
        <line x1={padL} y1={padT + innerH} x2={padL + innerW} y2={padT + innerH} className="sid-axis" />

        {data.map((d, i) => {
          const v = Number(d.value || 0);
          const bw = (v * innerW) / maxV;
          const y = padT + i * barH + barH * 0.18;
          const bh = barH * 0.64;
          return (
            <g key={i}>
              <text
                x={padL - 10}
                y={y + bh * 0.75}
                textAnchor="end"
                className="sid-bar-label"
              >
                {String(d.label || "")}
              </text>
              <rect x={padL} y={y} width={bw} height={bh} className="sid-bar" />
              <text x={padL + bw + 8} y={y + bh * 0.75} className="sid-bar-value">
                {fmtInt(v)}
              </text>
              <title>{`${d.label}: ${fmtInt(v)}`}</title>
            </g>
          );
        })}
      </svg>
    </div>
  );
}

function Heatmap({ cells }) {
  const data = Array.isArray(cells) ? cells : [];
  if (data.length === 0) return <div className="sid-chart-empty">Không có dữ liệu.</div>;

  const max = Math.max(1, ...data.map((c) => Number(c.count || 0)));

  const getCount = (d, h) => {
    const found = data.find((x) => Number(x.dayIndex) === d && Number(x.hour) === h);
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
                    title={`${weekdayViByMon0(d)} • ${h}h: ${fmtInt(c)} thao tác`}
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

function Donut({ items, centerLabel }) {
  const data = Array.isArray(items) ? items : [];
  const total = data.reduce((s, x) => s + Number(x.value || 0), 0) || 1;

  const size = 160;
  const r = 54;
  const c = size / 2;
  const circ = 2 * Math.PI * r;

  let offset = 0;

  return (
    <div className="sid-donut">
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        <circle cx={c} cy={c} r={r} className="sid-donut-track" />
        {data.map((it, idx) => {
          const v = Number(it.value || 0);
          const frac = v / total;
          const dash = frac * circ;
          const dashArr = `${dash} ${circ - dash}`;
          const strokeDashoffset = -offset;
          offset += dash;
          return (
            <circle
              key={idx}
              cx={c}
              cy={c}
              r={r}
              className={`sid-donut-slice slice-${idx % 6}`}
              strokeDasharray={dashArr}
              strokeDashoffset={strokeDashoffset}
            >
              <title>{`${it.label}: ${fmtInt(v)} (${fmtPercent(frac)})`}</title>
            </circle>
          );
        })}

        <text x={c} y={c - 6} textAnchor="middle" className="sid-donut-center">
          {centerLabel || "Cơ cấu"}
        </text>
        <text x={c} y={c + 16} textAnchor="middle" className="sid-donut-center-sub">
          {fmtInt(total)}
        </text>
      </svg>

      <div className="sid-donut-legend">
        {data.map((it, idx) => (
          <div key={idx} className="sid-donut-legend-item">
            <span className={`dot dot-${idx % 6}`} />
            <span className="lab">{it.label}</span>
            <span className="val">{fmtInt(it.value)}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function MiniKpi({ label, value, sub, tone = "neutral" }) {
  return (
    <div className={`sid-kpi-card tone-${tone}`}>
      <div className="sid-kpi-label">{label}</div>
      <div className="sid-kpi-value">{value}</div>
      {sub ? <div className="sid-kpi-sub">{sub}</div> : null}
    </div>
  );
}

function ProgressBar({ value01 }) {
  const v = clamp01(value01);
  return (
    <div className="sid-progress">
      <div className="sid-progress-fill" style={{ width: `${v * 100}%` }} />
    </div>
  );
}

function ChartCard({ title, subtitle, right, children }) {
  return (
    <div className="sid-card">
      <div className="sid-card-head">
        <div>
          <div className="sid-card-title">{title}</div>
          {subtitle ? <div className="sid-card-sub">{subtitle}</div> : null}
        </div>
        {right ? <div className="sid-card-right">{right}</div> : null}
      </div>
      <div className="sid-card-body">{children}</div>
    </div>
  );
}

/* =========================
   API
   ========================= */
async function fetchSystemInsightsOverview({ fromYmd, toYmd, bucket }) {
  // BE expects: fromLocal, toLocalExclusive, bucket
  // FE dùng date inclusive -> toLocalExclusive = to+1day 00:00:00
  const fromLocal = fromYmd ? toLocalUnspecifiedIso(fromYmd, 0, 0, 0) : undefined;
  const toExclusiveYmd = toYmd ? addDaysYmd(toYmd, 1) : undefined;
  const toLocalExclusive = toExclusiveYmd ? toLocalUnspecifiedIso(toExclusiveYmd, 0, 0, 0) : undefined;

  const params = {
    fromLocal: fromLocal || undefined,
    toLocalExclusive: toLocalExclusive || undefined,
    bucket: bucket || "day",
  };

  // Endpoint gợi ý: /api/system-insights-dashboard/overview
  // axiosClient thường đã gắn prefix /api
  const res = await axiosClient.get("/system-insights-dashboard/overview", { params });
  return res?.data ?? res;
}

/* =========================
   MAIN PAGE
   ========================= */
export default function SystemInsightsDashboardPage() {
  const [toasts, setToasts] = React.useState([]);
  const toastIdRef = React.useRef(1);
  const addToast = (type, message, title) => {
    const id = toastIdRef.current++;
    setToasts((prev) => [...prev, { id, type, message, title }]);
    setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== id)), 4500);
  };

  const [loading, setLoading] = React.useState(false);
  const [data, setData] = React.useState(null);

  const today = React.useMemo(() => {
    const now = new Date();
    const y = now.getFullYear();
    const m = pad2(now.getMonth() + 1);
    const d = pad2(now.getDate());
    return `${y}-${m}-${d}`;
  }, []);

  const [filters, setFilters] = React.useState(() => {
    const to = today;
    const from = addDaysYmd(today, -30);
    return { from, to, bucket: "day" };
  });

  const refresh = React.useCallback(async () => {
    setLoading(true);
    try {
      const res = await fetchSystemInsightsOverview({
        fromYmd: filters.from,
        toYmd: filters.to,
        bucket: filters.bucket,
      });
      setData(res);
    } catch (err) {
      console.error(err);
      addToast("error", "Không tải được dữ liệu giám sát hệ thống.", "Lỗi");
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [filters.from, filters.to, filters.bucket]);

  React.useEffect(() => {
    refresh();
  }, [refresh]);

  // ----- derived for charts -----
  const auditSeries = React.useMemo(() => {
    const raw = data?.auditActionsSeries || data?.AuditActionsSeries || [];
    return raw.map((p) => ({
      x: bucketLabelVi(p.bucketStartLocal || p.BucketStartLocal),
      y: Number(p.count ?? p.Count ?? 0),
    }));
  }, [data]);

  const notiDailySeries = React.useMemo(() => {
    const raw = data?.notificationsDaily || data?.NotificationsDaily || [];
    return raw.map((d) => ({
      x: bucketLabelVi(d.dateLocal || d.DateLocal),
      y: Number(d.total ?? d.Total ?? 0),
      sys: Number(d.system ?? d.System ?? 0),
      manual: Number(d.manual ?? d.Manual ?? 0),
    }));
  }, [data]);

  const notiReadRateSeries = React.useMemo(() => {
    const raw = data?.notificationReadRateDaily || data?.NotificationReadRateDaily || [];
    return raw.map((d) => ({
      x: bucketLabelVi(d.dateLocal || d.DateLocal),
      y: Math.round((Number(d.readRate ?? d.ReadRate ?? 0) * 1000)) / 10, // percent with 1 decimal
    }));
  }, [data]);

  const topAuditActions = React.useMemo(() => {
    const raw = data?.topAuditActions || data?.TopAuditActions || [];
    return raw.map((x) => ({
      label: viAction(x.name ?? x.Name),
      value: Number(x.count ?? x.Count ?? 0),
    }));
  }, [data]);

  const topAuditEntities = React.useMemo(() => {
    const raw = data?.topAuditEntityTypes || data?.TopAuditEntityTypes || [];
    return raw.map((x) => ({
      label: viEntity(x.name ?? x.Name),
      value: Number(x.count ?? x.Count ?? 0),
    }));
  }, [data]);

  const topAuditIps = React.useMemo(() => {
    const raw = data?.topAuditIpAddresses || data?.TopAuditIpAddresses || [];
    return raw.map((x) => ({
      label: String(x.name ?? x.Name ?? "-"),
      value: Number(x.count ?? x.Count ?? 0),
    }));
  }, [data]);

  const notiSeverityDaily = React.useMemo(() => {
    const raw = data?.notificationsSeverityDaily || data?.NotificationsSeverityDaily || [];
    // show as list summary (chart simplified)
    return raw.map((d) => ({
      label: bucketLabelVi(d.dateLocal || d.DateLocal),
      info: Number(d.info ?? d.Info ?? 0),
      success: Number(d.success ?? d.Success ?? 0),
      warning: Number(d.warning ?? d.Warning ?? 0),
      error: Number(d.error ?? d.Error ?? 0),
      total:
        Number(d.info ?? d.Info ?? 0) +
        Number(d.success ?? d.Success ?? 0) +
        Number(d.warning ?? d.Warning ?? 0) +
        Number(d.error ?? d.Error ?? 0),
    }));
  }, [data]);

  const topNotiTypes = React.useMemo(() => {
    const raw = data?.topNotificationTypes || data?.TopNotificationTypes || [];
    return raw.map((x) => ({
      label: notificationTypeLabel(x.name ?? x.Name),
      value: Number(x.count ?? x.Count ?? 0),
    }));
  }, [data]);

  const notiScope = React.useMemo(() => {
    const s = data?.notificationScope || data?.NotificationScope;
    if (!s) return null;
    return [
      { label: "Toàn hệ thống", value: Number(s.global ?? s.Global ?? 0) },
      { label: "Theo nhóm quyền", value: Number(s.roleTargeted ?? s.RoleTargeted ?? 0) },
      { label: "Người dùng cụ thể", value: Number(s.userTargeted ?? s.UserTargeted ?? 0) },
    ];
  }, [data]);

  const notiRecipientsHistogram = React.useMemo(() => {
    const raw = data?.notificationRecipientsHistogram || data?.NotificationRecipientsHistogram || [];
    // render as bars (horizontal)
    return raw.map((x) => ({
      label: String(x.label ?? x.Label ?? ""),
      value: Number(x.count ?? x.Count ?? 0),
    }));
  }, [data]);

  const auditHeatmap = React.useMemo(() => {
    return data?.auditHeatmap || data?.AuditHeatmap || [];
  }, [data]);

  // KPI values
  const sysAct = data?.systemActivity || data?.SystemActivity || {};
  const notiHealth = data?.notificationsHealth || data?.NotificationsHealth || {};

  const systemActionRate = Number(sysAct.systemActionRate ?? sysAct.SystemActionRate ?? 0);
  const overallReadRate = Number(notiHealth.overallReadRate ?? notiHealth.OverallReadRate ?? 0);

  const dateRangeLabel = React.useMemo(() => {
    const f = filters.from ? ymdToDmy(filters.from) : "—";
    const t = filters.to ? ymdToDmy(filters.to) : "—";
    return `${f} → ${t} (UTC+7)`;
  }, [filters.from, filters.to]);

  return (
    <>
      <div className="sid-page">
        <div className="sid-topbar">
          <div>
            <h2 className="sid-title">Bảng điều khiển Giám sát hệ thống</h2>
            <div className="sid-subtitle">
              Tổng quan hoạt động hệ thống (AuditLogs) & sức khoẻ thông báo (Notifications)
            </div>
          </div>

          <div className="sid-actions">
            <button
              type="button"
              className="btn ghost"
              onClick={() => setFilters((s) => ({ ...s, from: addDaysYmd(today, -7), to: today }))}
              title="7 ngày gần nhất"
            >
              7 ngày
            </button>
            <button
              type="button"
              className="btn ghost"
              onClick={() => setFilters((s) => ({ ...s, from: addDaysYmd(today, -30), to: today }))}
              title="30 ngày gần nhất"
            >
              30 ngày
            </button>
            <button type="button" className="btn" onClick={refresh} disabled={loading}>
              {loading ? "Đang tải..." : "Làm mới"}
            </button>
          </div>
        </div>

        {/* Filters */}
        <div className="sid-filters">
          <div className="sid-filter">
            <label>Từ ngày</label>
            <input
              type="date"
              value={filters.from || ""}
              onChange={(e) => setFilters((s) => ({ ...s, from: e.target.value }))}
            />
          </div>
          <div className="sid-filter">
            <label>Đến ngày</label>
            <input
              type="date"
              value={filters.to || ""}
              onChange={(e) => setFilters((s) => ({ ...s, to: e.target.value }))}
            />
          </div>
          <div className="sid-filter">
            <label>Nhóm theo</label>
            <select
              value={filters.bucket}
              onChange={(e) => setFilters((s) => ({ ...s, bucket: e.target.value }))}
            >
              <option value="day">Theo ngày</option>
              <option value="hour">Theo giờ</option>
            </select>
          </div>

          <div className="sid-filter sid-filter-grow">
            <label>Khoảng thời gian</label>
            <div className="sid-range-pill">{dateRangeLabel}</div>
          </div>
        </div>

        {/* KPI cards */}
        <div className="sid-kpis">
          <div className="sid-kpi-group">
            <div className="sid-kpi-group-title">Hoạt động hệ thống (AuditLogs)</div>

            <div className="sid-kpi-grid">
              <MiniKpi
                label="Tổng số thao tác"
                value={fmtInt(sysAct.totalActions ?? sysAct.TotalActions ?? 0)}
                sub="Số bản ghi AuditLogs trong khoảng thời gian"
                tone="neutral"
              />
              <MiniKpi
                label="Số người thao tác"
                value={fmtInt(sysAct.uniqueActors ?? sysAct.UniqueActors ?? 0)}
                sub="Số email người thao tác (không trùng)"
                tone="neutral"
              />
              <MiniKpi
                label="Thao tác do hệ thống"
                value={fmtInt(sysAct.systemActions ?? sysAct.SystemActions ?? 0)}
                sub={
                  <span>
                    Tỷ lệ: <b>{fmtPercent(systemActionRate)}</b>
                  </span>
                }
                tone={systemActionRate >= 0.5 ? "warning" : "neutral"}
              />

              <div className="sid-kpi-card tone-neutral">
                <div className="sid-kpi-label">Tỷ lệ thao tác do hệ thống</div>
                <div className="sid-kpi-value">{fmtPercent(systemActionRate)}</div>
                <ProgressBar value01={systemActionRate} />
                <div className="sid-kpi-sub">
                  Dùng để phát hiện các job/bot chạy quá nhiều so với thao tác người dùng.
                </div>
              </div>
            </div>
          </div>

          <div className="sid-kpi-group">
            <div className="sid-kpi-group-title">Sức khoẻ thông báo (Notifications)</div>

            <div className="sid-kpi-grid">
              <MiniKpi
                label="Tổng thông báo"
                value={fmtInt(notiHealth.totalNotifications ?? notiHealth.TotalNotifications ?? 0)}
                sub="Số thông báo tạo trong khoảng thời gian"
              />
              <MiniKpi
                label="Hệ thống vs Thủ công"
                value={`${fmtInt(notiHealth.systemGeneratedCount ?? notiHealth.SystemGeneratedCount ?? 0)} / ${fmtInt(
                  notiHealth.manualCount ?? notiHealth.ManualCount ?? 0
                )}`}
                sub={
                  <span>
                    Tỷ lệ hệ thống:{" "}
                    <b>{fmtPercent(notiHealth.systemGeneratedRate ?? notiHealth.SystemGeneratedRate ?? 0)}</b>
                  </span>
                }
              />
              <MiniKpi
                label="Toàn hệ thống vs Có mục tiêu"
                value={`${fmtInt(notiHealth.globalCount ?? notiHealth.GlobalCount ?? 0)} / ${fmtInt(
                  notiHealth.targetedCount ?? notiHealth.TargetedCount ?? 0
                )}`}
                sub={
                  <span>
                    Tỷ lệ toàn hệ thống: <b>{fmtPercent(notiHealth.globalRate ?? notiHealth.GlobalRate ?? 0)}</b>
                  </span>
                }
              />
              <div className="sid-kpi-card tone-success">
                <div className="sid-kpi-label">Tỷ lệ đã đọc tổng</div>
                <div className="sid-kpi-value">{fmtPercent(overallReadRate)}</div>
                <ProgressBar value01={overallReadRate} />
                <div className="sid-kpi-sub">
                  Đã đọc: <b>{fmtInt(notiHealth.readCountSum ?? notiHealth.ReadCountSum ?? 0)}</b> / Tổng người nhận:{" "}
                  <b>{fmtInt(notiHealth.totalTargetUsersSum ?? notiHealth.TotalTargetUsersSum ?? 0)}</b>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* AUDIT charts */}
        <div className="sid-section">
          <div className="sid-section-title">Biểu đồ hành vi hệ thống (AuditLogs)</div>
          <div className="sid-grid">
            <ChartCard
              title="Số thao tác theo thời gian"
              subtitle={`Nhóm theo: ${filters.bucket === "hour" ? "giờ" : "ngày"}`}
              right={<span className="sid-badge">UTC+7</span>}
            >
              <SvgLineChart
                points={auditSeries}
                valueTitle="Thao tác"
                xTitle={filters.bucket === "hour" ? "Thời gian (giờ)" : "Thời gian (ngày)"}
              />
            </ChartCard>

            <ChartCard title="Top 10 hành động" subtitle="Hành động nào đang diễn ra nhiều nhất">
              <SvgHorizontalBarChart
                items={topAuditActions.slice(0, 10)}
                labelTitle="Hành động"
                valueTitle="Số lần"
                height={290}
              />
            </ChartCard>

            <ChartCard title="Top 10 loại đối tượng" subtitle="Nhóm theo EntityType">
              <SvgHorizontalBarChart
                items={topAuditEntities.slice(0, 10)}
                labelTitle="Đối tượng"
                valueTitle="Số lần"
                height={290}
              />
            </ChartCard>

            <ChartCard
              title="Bản đồ nhiệt hoạt động"
              subtitle="Giờ trong ngày × Thứ trong tuần"
              right={<span className="sid-badge">Mon→Sun</span>}
            >
              <Heatmap cells={auditHeatmap} />
            </ChartCard>

            <ChartCard title="Top IP Address" subtitle="Gợi ý phát hiện bất thường (spam/đăng nhập lạ)">
              <SvgHorizontalBarChart
                items={topAuditIps.slice(0, 10)}
                labelTitle="IP"
                valueTitle="Số thao tác"
                height={280}
              />
            </ChartCard>

            <ChartCard
              title="Gợi ý đọc nhanh"
              subtitle="Mẹo vận hành"
              right={<span className="sid-badge subtle">Admin</span>}
            >
              <div className="sid-tip">
                <div className="sid-tip-item">
                  <b>Top hành động</b> giúp phát hiện “đang có hoạt động nào bất thường” (VD: nhập CSV quá nhiều, sửa tồn kho dồn dập…).
                </div>
                <div className="sid-tip-item">
                  <b>Heatmap</b> cho bạn biết giờ cao điểm thao tác (để bố trí nhân sự/giám sát).
                </div>
                <div className="sid-tip-item">
                  <b>Top IP</b> là lớp “security-lite” để soi IP spam thao tác.
                </div>
              </div>
            </ChartCard>
          </div>
        </div>

        {/* Notifications charts */}
        <div className="sid-section">
          <div className="sid-section-title">Biểu đồ sức khoẻ thông báo (Notifications)</div>

          <div className="sid-grid">
            <ChartCard title="Số thông báo theo thời gian" subtitle="Tổng số thông báo mỗi ngày">
              <SvgLineChart
                points={notiDailySeries.map((d) => ({ x: d.x, y: d.y }))}
                valueTitle="Thông báo"
                xTitle="Ngày"
              />
            </ChartCard>

            <ChartCard title="Top loại thông báo" subtitle="Type phổ biến nhất">
              <SvgHorizontalBarChart items={topNotiTypes.slice(0, 10)} labelTitle="Loại" valueTitle="Số thông báo" height={290} />
            </ChartCard>

            <ChartCard title="Cơ cấu phạm vi" subtitle="Toàn hệ thống / Theo nhóm quyền / Người dùng cụ thể">
              <Donut items={notiScope || []} centerLabel="Phạm vi" />
            </ChartCard>

            <ChartCard title="Tỷ lệ đã đọc theo thời gian" subtitle="ReadCount / TotalTargetUsers (theo ngày)">
              <SvgLineChart
                points={notiReadRateSeries.map((d) => ({ x: d.x, y: d.y }))}
                valueTitle="Tỷ lệ đã đọc (%)"
                xTitle="Ngày"
              />
              <div className="sid-note">
                Ghi chú: biểu đồ hiển thị <b>phần trăm</b> (0–100%).
              </div>
            </ChartCard>

            <ChartCard title="Phân bố số người nhận" subtitle="Histogram theo TotalTargetUsers">
              <SvgHorizontalBarChart
                items={notiRecipientsHistogram}
                labelTitle="Nhóm người nhận"
                valueTitle="Số thông báo"
                height={280}
              />
            </ChartCard>

            <ChartCard title="Mức độ theo ngày" subtitle="Info/Success/Warning/Error (tóm tắt)">
              <div className="sid-sev-table">
                <div className="sid-sev-head">
                  <div>Ngày</div>
                  <div>Thông tin</div>
                  <div>Thành công</div>
                  <div>Cảnh báo</div>
                  <div>Lỗi</div>
                  <div>Tổng</div>
                </div>

                {(notiSeverityDaily || []).slice(-14).map((r, idx) => (
                  <div key={idx} className="sid-sev-row" title={`Ngày ${r.label}`}>
                    <div className="mono">{r.label}</div>
                    <div>{fmtInt(r.info)}</div>
                    <div>{fmtInt(r.success)}</div>
                    <div className={r.warning > 0 ? "warn" : ""}>{fmtInt(r.warning)}</div>
                    <div className={r.error > 0 ? "err" : ""}>{fmtInt(r.error)}</div>
                    <div className="strong">{fmtInt(r.total)}</div>
                  </div>
                ))}

                <div className="sid-note">
                  Hiển thị 14 ngày gần nhất để nhìn nhanh xu hướng <b>Cảnh báo/Lỗi</b>.
                </div>
              </div>
            </ChartCard>
          </div>
        </div>

        {/* Small mapping hint */}
        <div className="sid-foot">
          <span className="sid-foot-badge">Thuần tiếng Việt</span>
          <span className="sid-foot-text">
            Top hành động/đối tượng đã được map qua cấu hình VN của AuditLogs; Notification Type/Severity map theo trang Thông báo hệ thống.
          </span>
        </div>
      </div>

      <ToastContainer toasts={toasts} onRemove={(id) => setToasts((p) => p.filter((t) => t.id !== id))} />
    </>
  );
}
