// File: src/pages/admin/staff-ticket-management.jsx
import React, {
  useCallback,
  useEffect,
  useMemo,
  useState,
} from "react";
import { createPortal } from "react-dom";
import { useNavigate } from "react-router-dom";
import "../../styles/staff-ticket-management.css";
import { ticketsApi } from "../../api/ticketsApi";
import axiosClient from "../../api/axiosClient";
import PermissionGuard from "../../components/PermissionGuard";
import { usePermission } from "../../hooks/usePermission";
import useToast from "../../hooks/useToast";

// ---- Options & constants ----
const SLA_OPTIONS = [
  { value: "", label: "Tất cả SLA" },
  { value: "OK", label: "Đúng SLA" },
  { value: "Warning", label: "Cảnh báo SLA" },
  { value: "Overdue", label: "Quá hạn SLA" },
];

const SEVERITY_OPTIONS = [
  { value: "", label: "Tất cả mức độ" },
  { value: "Low", label: "Thấp" },
  { value: "Medium", label: "Trung bình" },
  { value: "High", label: "Cao" },
  { value: "Critical", label: "Nghiêm trọng" },
];

const PRIORITY_OPTIONS = [
  { value: "", label: "Tất cả cấp ưu tiên" },
  { value: "1", label: "Tiêu chuẩn" },
  { value: "2", label: "Ưu tiên" },
  { value: "3", label: "VIP" },
];

const PAGE_SIZE = 10;

// ---- helpers hiển thị ----
function fmtVNDate(dt) {
  try {
    if (!dt) return "";
    const d =
      typeof dt === "string" || typeof dt === "number" ? new Date(dt) : dt;
    return new Intl.DateTimeFormat("vi-VN", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    }).format(d);
  } catch {
    return "";
  }
}

function fmtVNDateOrDash(dt) {
  const v = fmtVNDate(dt);
  return v || "-";
}

function normalizeStatus(s) {
  const v = String(s || "").toLowerCase();
  if (v === "open" || v === "new") return "New";
  if (v === "processing" || v === "inprogress" || v === "in_process")
    return "InProgress";
  if (v === "done" || v === "resolved" || v === "completed")
    return "Completed";
  if (v === "closed" || v === "close") return "Closed";
  return "New";
}

function SeverityTag({ value }) {
  const v = (value || "").toString();
  const map = {
    Low: { cls: "tag tag-low", text: "Thấp" },
    Medium: { cls: "tag tag-medium", text: "Trung bình" },
    High: { cls: "tag tag-high", text: "Cao" },
    Critical: { cls: "tag tag-critical", text: "Nghiêm trọng" },
  };
  const d = map[v] || map.Medium;
  return <span className={d.cls}>{d.text}</span>;
}

function SlaPill({ value }) {
  const v = (value || "").toString();
  if (v === "Overdue")
    return <span className="sla sla-breached">Quá hạn SLA</span>;
  if (v === "Warning")
    return <span className="sla sla-warning">Cảnh báo SLA</span>;
  return <span className="sla sla-ok">Đúng SLA</span>;
}

function PriorityPill({ value }) {
  const v = Number(value ?? 0);
  if (!v) return <span className="prio prio-normal">Tiêu chuẩn</span>;

  if (v === 1) return <span className="prio prio-1">Ưu tiên</span>;
  if (v === 2) return <span className="prio prio-2">VIP</span>;

  return <span className="prio prio-normal">Thường</span>;
}

// ---- sort cho 2 cột queue ----
const SLA_WEIGHT = {
  Overdue: 3,
  Warning: 2,
  OK: 1,
};
const SEVERITY_WEIGHT = {
  Critical: 4,
  High: 3,
  Medium: 2,
  Low: 1,
};
const STATUS_WEIGHT_MY = {
  InProgress: 2,
  New: 1,
  Completed: 0,
  Closed: 0,
};

function sortForUnassigned(items) {
  return [...items].sort((a, b) => {
    const s1 =
      (SLA_WEIGHT[String(b.slaStatus)] || 0) -
      (SLA_WEIGHT[String(a.slaStatus)] || 0);
    if (s1 !== 0) return s1;

    const s2 =
      (SEVERITY_WEIGHT[String(b.severity)] || 0) -
      (SEVERITY_WEIGHT[String(a.severity)] || 0);
    if (s2 !== 0) return s2;

    const ta = new Date(a.createdAt || 0).getTime();
    const tb = new Date(b.createdAt || 0).getTime();
    return ta - tb;
  });
}

function sortForMine(items) {
  return [...items].sort((a, b) => {
    const s1 =
      (SLA_WEIGHT[String(b.slaStatus)] || 0) -
      (SLA_WEIGHT[String(a.slaStatus)] || 0);
    if (s1 !== 0) return s1;

    const sa = STATUS_WEIGHT_MY[normalizeStatus(a.status)] || 0;
    const sb = STATUS_WEIGHT_MY[normalizeStatus(b.status)] || 0;
    const s2 = sb - sa; // InProgress trước New
    if (s2 !== 0) return s2;

    const ta = new Date(a.createdAt || 0).getTime();
    const tb = new Date(b.createdAt || 0).getTime();
    return ta - tb;
  });
}

const INITIAL_FILTER = {
  sla: "",
  severity: "",
  priority: "",
};

export default function StaffTicketManagement() {
  const nav = useNavigate();
  const { showError } = useToast();
  const { hasPermission: hasEditPermission } = usePermission("SUPPORT_MANAGER", "EDIT");
  const { hasPermission: hasViewDetailPermission } = usePermission("SUPPORT_MANAGER", "VIEW_DETAIL");

  // ---- Filter + paging riêng cho từng list ----
  const [unassignedUi, setUnassignedUi] = useState(INITIAL_FILTER);
  const [unassignedApplied, setUnassignedApplied] =
    useState(INITIAL_FILTER);

  const [myUi, setMyUi] = useState(INITIAL_FILTER);
  const [myApplied, setMyApplied] = useState(INITIAL_FILTER);

  const [unassignedPage, setUnassignedPage] = useState(1);
  const [myPage, setMyPage] = useState(1);

  const [unassignedData, setUnassignedData] = useState({
    items: [],
    totalItems: 0,
    page: 1,
    pageSize: PAGE_SIZE,
  });
  const [myData, setMyData] = useState({
    items: [],
    totalItems: 0,
    page: 1,
    pageSize: PAGE_SIZE,
  });

  const [loadingUnassigned, setLoadingUnassigned] = useState(false);
  const [loadingMine, setLoadingMine] = useState(false);

  // modal gán / chuyển (hiện tại chỉ dùng nội bộ, Ticket của tôi đã bỏ các nút)
  const [modal, setModal] = useState({
    open: false,
    mode: "",
    id: null,
    currentAssigneeId: null,
  });

  // counters header (dựa trên list default, không theo filter)
  const [headerStats, setHeaderStats] = useState({
    unassignedTotal: 0,
    myTotal: 0,
    overdueTotal: 0,
  });

  // chuẩn hóa paging response
  const normalizePaged = useCallback((res, fallbacks) => {
    return {
      items: res?.items ?? res?.Items ?? fallbacks.items,
      totalItems:
        res?.totalItems ?? res?.TotalItems ?? fallbacks.totalItems,
      page: res?.page ?? res?.Page ?? fallbacks.page,
      pageSize: res?.pageSize ?? res?.PageSize ?? fallbacks.pageSize,
    };
  }, []);

  // ---- load counters header (list default, không filter UI) ----
  const refreshHeaderStats = useCallback(async () => {
    try {
      const [
        resUnassigned,
        resMine,
        resOverdueUnassigned,
        resOverdueMine,
      ] = await Promise.all([
        ticketsApi.list({
          status: "New",
          severity: "",
          sla: "",
          assignmentState: "Unassigned",
          page: 1,
          pageSize: 1,
        }),
        // Tổng "Ticket của tôi" đang xử lý
        ticketsApi.list({
          status: "InProgress",
          severity: "",
          sla: "",
          assignmentState: "Mine",
          page: 1,
          pageSize: 1,
        }),
        ticketsApi.list({
          status: "New",
          severity: "",
          sla: "Overdue",
          assignmentState: "Unassigned",
          page: 1,
          pageSize: 1,
        }),
        // "Ticket của tôi" bị quá hạn (chỉ tính InProgress)
        ticketsApi.list({
          status: "InProgress",
          severity: "",
          sla: "Overdue",
          assignmentState: "Mine",
          page: 1,
          pageSize: 1,
        }),
      ]);

      const unassignedPaged = normalizePaged(resUnassigned, {
        items: [],
        totalItems: 0,
        page: 1,
        pageSize: 1,
      });
      const minePaged = normalizePaged(resMine, {
        items: [],
        totalItems: 0,
        page: 1,
        pageSize: 1,
      });
      const overdueUnassignedPaged = normalizePaged(resOverdueUnassigned, {
        items: [],
        totalItems: 0,
        page: 1,
        pageSize: 1,
      });
      const overdueMinePaged = normalizePaged(resOverdueMine, {
        items: [],
        totalItems: 0,
        page: 1,
        pageSize: 1,
      });

      setHeaderStats({
        unassignedTotal: unassignedPaged.totalItems || 0,
        myTotal: minePaged.totalItems || 0,
        overdueTotal:
          (overdueUnassignedPaged.totalItems || 0) +
          (overdueMinePaged.totalItems || 0),
      });
    } catch (e) {
      console.error("Không tải được thống kê header", e);
    }
  }, [normalizePaged]);

  // ---- load list "Chưa gán" ----
  const loadUnassigned = useCallback(async () => {
    setLoadingUnassigned(true);
    try {
      const res = await ticketsApi.list({
        status: "New", // hàng đợi chưa gán ưu tiên ticket mới
        severity: unassignedApplied.severity || "",
        sla: unassignedApplied.sla || "",
        assignmentState: "Unassigned",
        page: unassignedPage,
        pageSize: PAGE_SIZE,
        // nếu BE hỗ trợ có thể thêm priorityLevel: unassignedApplied.priority
      });
      setUnassignedData(
        normalizePaged(res, {
          items: [],
          totalItems: 0,
          page: unassignedPage,
          pageSize: PAGE_SIZE,
        })
      );
    } catch (e) {
      alert(
        e?.response?.data?.message ||
          e.message ||
          "Không tải được danh sách ticket chưa gán."
      );
      setUnassignedData((prev) => ({ ...prev, items: [] }));
    } finally {
      setLoadingUnassigned(false);
    }
  }, [unassignedPage, unassignedApplied, normalizePaged]);

  // ---- load list "Ticket của tôi" ----
  const loadMine = useCallback(async () => {
    setLoadingMine(true);
    try {
      const res = await ticketsApi.list({
        status: "InProgress", // chỉ lấy ticket đang xử lý của tôi
        severity: myApplied.severity || "",
        sla: myApplied.sla || "",
        assignmentState: "Mine",
        page: myPage,
        pageSize: PAGE_SIZE,
        // nếu BE hỗ trợ có thể thêm priorityLevel: myApplied.priority
      });
      setMyData(
        normalizePaged(res, {
          items: [],
          totalItems: 0,
          page: myPage,
          pageSize: PAGE_SIZE,
        })
      );
    } catch (e) {
      alert(
        e?.response?.data?.message ||
          e.message ||
          "Không tải được danh sách ticket của tôi."
      );
      setMyData((prev) => ({ ...prev, items: [] }));
    } finally {
      setLoadingMine(false);
    }
  }, [myPage, myApplied, normalizePaged]);

  // auto load
  useEffect(() => {
    loadUnassigned();
  }, [loadUnassigned]);

  useEffect(() => {
    loadMine();
  }, [loadMine]);

  useEffect(() => {
    refreshHeaderStats();
  }, [refreshHeaderStats]);

  // ---- pagination ----
  const unassignedTotalPages = useMemo(
    () =>
      Math.max(
        1,
        Math.ceil(
          (unassignedData.totalItems || 0) /
            (unassignedData.pageSize || PAGE_SIZE)
        )
      ),
    [unassignedData.totalItems, unassignedData.pageSize]
  );

  const myTotalPages = useMemo(
    () =>
      Math.max(
        1,
        Math.ceil((myData.totalItems || 0) / (myData.pageSize || PAGE_SIZE))
      ),
    [myData.totalItems, myData.pageSize]
  );

  const gotoUnassignedPage = (p) =>
    setUnassignedPage((prev) =>
      Math.max(
        1,
        Math.min(unassignedTotalPages, typeof p === "number" ? p : prev)
      )
    );

  const gotoMyPage = (p) =>
    setMyPage((prev) =>
      Math.max(1, Math.min(myTotalPages, typeof p === "number" ? p : prev))
    );

  // ---- apply/reset filter cho từng panel ----
  const applyUnassignedFilters = () => {
    setUnassignedApplied({ ...unassignedUi });
    setUnassignedPage(1);
  };

  const resetUnassignedFilters = () => {
    setUnassignedUi({ ...INITIAL_FILTER });
    setUnassignedApplied({ ...INITIAL_FILTER });
    setUnassignedPage(1);
  };

  const applyMyFilters = () => {
    setMyApplied({ ...myUi });
    setMyPage(1);
  };

  const resetMyFilters = () => {
    setMyUi({ ...INITIAL_FILTER });
    setMyApplied({ ...INITIAL_FILTER });
    setMyPage(1);
  };

  // ---- actions ----
  const refresh = () => {
    loadUnassigned();
    loadMine();
    refreshHeaderStats();
  };

  const doAssign = async (id, assigneeId) => {
    try {
      await ticketsApi.assign(id, assigneeId);
      await refresh();
    } catch (e) {
      alert(
        e?.response?.data?.message || e.message || "Gán ticket thất bại."
      );
    }
  };

  // NEW: staff tự nhận ticket (assign cho chính mình)
  const doAssignMe = async (id) => {
    if (!hasEditPermission) {
      showError("Không có quyền", "Bạn không có quyền nhận ticket");
      return;
    }
    try {
      await ticketsApi.assignToMe(id);
      await refresh();
    } catch (e) {
      alert(
        e?.response?.data?.message ||
          e.message ||
          "Nhận ticket thất bại."
      );
    }
  };

  const doTransfer = async (id, assigneeId) => {
    try {
      await ticketsApi.transferTech(id, assigneeId);
      await refresh();
    } catch (e) {
      alert(
        e?.response?.data?.message ||
          e.message ||
          "Chuyển hỗ trợ thất bại."
      );
    }
  };

  const doComplete = async (id) => {
    if (!window.confirm("Xác nhận đánh dấu Hoàn thành?")) return;
    try {
      await ticketsApi.complete(id);
      await refresh();
    } catch (e) {
      alert(
        e?.response?.data?.message ||
          e.message ||
          "Hoàn thành ticket thất bại."
      );
    }
  };

  const doClose = async (id) => {
    if (!window.confirm("Xác nhận Đóng ticket?")) return;
    try {
      await ticketsApi.close(id);
      await refresh();
    } catch (e) {
      alert(
        e?.response?.data?.message || e.message || "Đóng ticket thất bại."
      );
    }
  };

  // ---- counters header (luôn theo list default) ----
  const unassignedCount = headerStats.unassignedTotal || 0;
  const myCount = headerStats.myTotal || 0;
  const overdueCount = headerStats.overdueTotal || 0;

  // ---- filter by priority client-side ----
  const filterByPriority = (items, priorityValue) => {
    if (!priorityValue) return items;
    const p = parseInt(priorityValue, 10);
    if (!p) return items;
    return (items || []).filter(
      (x) => Number(x.priorityLevel ?? 0) === p
    );
  };

  // ---- dữ liệu sort + filter cho 2 cột ----
  const unassignedItemsSorted = useMemo(
    () => sortForUnassigned(unassignedData.items || []),
    [unassignedData.items]
  );
  const myItemsSorted = useMemo(
    () => sortForMine(myData.items || []),
    [myData.items]
  );

  const unassignedItemsView = useMemo(
    () =>
      filterByPriority(unassignedItemsSorted, unassignedApplied.priority),
    [unassignedItemsSorted, unassignedApplied.priority]
  );
  const myItemsView = useMemo(
    () => filterByPriority(myItemsSorted, myApplied.priority),
    [myItemsSorted, myApplied.priority]
  );

  // ---- render ----
  return (
    <div className="tk-page">
      {/* Header + counters */}
      <div className="tk-header">
        <h1 className="tk-title">Ticket hỗ trợ (Staff)</h1>
        <div className="tk-header-actions">
          <div className="tk-counters">
            <div className="tk-counter">
              <span className="tk-counter-label">Chưa gán</span>
              <span className="tk-counter-value">{unassignedCount}</span>
            </div>
            <div className="tk-counter">
              <span className="tk-counter-label">Ticket của tôi</span>
              <span className="tk-counter-value">{myCount}</span>
            </div>
            <div className="tk-counter">
              <span className="tk-counter-label">Quá hạn SLA</span>
              <span className="tk-counter-value">{overdueCount}</span>
            </div>
          </div>
        </div>
      </div>

      {/* SPLIT VIEW – 2 cột: Chưa gán / Ticket của tôi */}
      <div className="tk-layout-split">
        {/* Cột trái: Chưa gán */}
        <div className="tk-panel">
          <div className="tk-panel-head">
            <div>
              <h2 className="tk-panel-title">Chưa gán (Unassigned)</h2>
              <p className="tk-panel-sub">
                Ticket mới chưa có nhân viên phụ trách.
              </p>
            </div>
          </div>

          {/* Filter riêng: SLA + Mức độ + Cấp ưu tiên + Áp dụng/Reset */}
          <div className="tk-panel-filters">
            <select
              className="ip ip-sm"
              value={unassignedUi.sla}
              onChange={(e) =>
                setUnassignedUi((prev) => ({
                  ...prev,
                  sla: e.target.value,
                }))
              }
            >
              {SLA_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>

            <select
              className="ip ip-sm"
              value={unassignedUi.severity}
              onChange={(e) =>
                setUnassignedUi((prev) => ({
                  ...prev,
                  severity: e.target.value,
                }))
              }
            >
              {SEVERITY_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>

            <select
              className="ip ip-sm"
              value={unassignedUi.priority}
              onChange={(e) =>
                setUnassignedUi((prev) => ({
                  ...prev,
                  priority: e.target.value,
                }))
              }
            >
              {PRIORITY_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>

            <button
              type="button"
              className="btn primary xs"
              onClick={applyUnassignedFilters}
            >
              Áp dụng
            </button>
            <button
              type="button"
              className="btn ghost xs"
              onClick={resetUnassignedFilters}
            >
              Reset
            </button>
          </div>

          <div className="tk-table-wrap">
            <table className="tk-table tk-table-mini">
              <colgroup>
                <col /> {/* Tiêu đề */}
                <col style={{ width: 130 }} /> {/* SLA */}
                <col style={{ width: 100 }} /> {/* Mức độ */}
                <col style={{ width: 100 }} /> {/* Cấp ưu tiên */}
                <col style={{ width: 120 }} /> {/* Hạn phản hồi */}
                <col style={{ width: 90 }} /> {/* Thao tác */}
              </colgroup>
              <thead>
                <tr>
                  <th>Tiêu đề</th>
                  <th>SLA</th>
                  <th>Mức độ</th>
                  <th>Cấp ưu tiên</th>
                  <th>Hạn phản hồi</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {loadingUnassigned && (
                  <tr>
                    <td
                      colSpan={6}
                      style={{ textAlign: "center", padding: 16 }}
                    >
                      Đang tải.
                    </td>
                  </tr>
                )}
                {!loadingUnassigned &&
                  unassignedItemsView.map((r) => (
                    <tr key={r.ticketId}>
                      <td className="ellipsis" title={r.subject}>
                        {r.subject}
                      </td>
                      <td>
                        <SlaPill value={r.slaStatus} />
                      </td>
                      <td>
                        <SeverityTag value={r.severity} />
                      </td>
                      <td>
                        <PriorityPill value={r.priorityLevel} />
                      </td>
                      <td className="muted">
                        {fmtVNDateOrDash(r.firstResponseDueAt)}
                      </td>
                      <td className="tk-row-actions">
                        {/* Queue này chủ yếu là NHẬN TICKET */}
                        <button
                          className={`btn icon-btn primary ${!hasEditPermission ? 'disabled' : ''}`}
                          title={!hasEditPermission ? "Bạn không có quyền nhận ticket" : "Nhận ticket"}
                          disabled={!hasEditPermission}
                          onClick={() => doAssignMe(r.ticketId)}
                        >
                          <span aria-hidden="true">👤</span>
                        </button>
                      </td>
                    </tr>
                  ))}
                {!loadingUnassigned && !unassignedItemsView.length && (
                  <tr>
                    <td
                      colSpan={6}
                      style={{ textAlign: "center", padding: 16 }}
                    >
                      Không có ticket chưa gán.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {/* Paging riêng cho cột trái */}
          <div className="tk-pager tk-pager-inline">
            <button
              className="btn xs ghost"
              onClick={() => gotoUnassignedPage(unassignedPage - 1)}
              disabled={unassignedPage <= 1}
            >
              « Trước
            </button>
            <span>
              Trang {unassignedPage}/{unassignedTotalPages}
            </span>
            <button
              className="btn xs ghost"
              onClick={() => gotoUnassignedPage(unassignedPage + 1)}
              disabled={unassignedPage >= unassignedTotalPages}
            >
              Sau »
            </button>
          </div>
        </div>

        {/* Cột phải: Ticket của tôi */}
        <div className="tk-panel">
          <div className="tk-panel-head">
            <div>
              <h2 className="tk-panel-title">Ticket của tôi</h2>
              <p className="tk-panel-sub">
                Ticket đang được gán cho tài khoản của bạn.
              </p>
            </div>
          </div>

          {/* Filter riêng: SLA + Mức độ + Cấp ưu tiên + Áp dụng/Reset */}
          <div className="tk-panel-filters">
            <select
              className="ip ip-sm"
              value={myUi.sla}
              onChange={(e) =>
                setMyUi((prev) => ({
                  ...prev,
                  sla: e.target.value,
                }))
              }
            >
              {SLA_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>

            <select
              className="ip ip-sm"
              value={myUi.severity}
              onChange={(e) =>
                setMyUi((prev) => ({
                  ...prev,
                  severity: e.target.value,
                }))
              }
            >
              {SEVERITY_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>

            <select
              className="ip ip-sm"
              value={myUi.priority}
              onChange={(e) =>
                setMyUi((prev) => ({
                  ...prev,
                  priority: e.target.value,
                }))
              }
            >
              {PRIORITY_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>

            <button
              type="button"
              className="btn primary xs"
              onClick={applyMyFilters}
            >
              Áp dụng
            </button>
            <button
              type="button"
              className="btn ghost xs"
              onClick={resetMyFilters}
            >
              Reset
            </button>
          </div>

          <div className="tk-table-wrap">
            <table className="tk-table tk-table-mini">
              <colgroup>
                <col /> {/* Tiêu đề */}
                <col style={{ width: 130 }} /> {/* SLA */}
                <col style={{ width: 100 }} /> {/* Mức độ */}
                <col style={{ width: 100 }} /> {/* Cấp ưu tiên */}
                <col style={{ width: 120 }} /> {/* Hạn giải quyết */}
                <col style={{ width: 90 }} /> {/* Thao tác */}
              </colgroup>
              <thead>
                <tr>
                  <th>Tiêu đề</th>
                  <th>SLA</th>
                  <th>Mức độ</th>
                  <th>Cấp ưu tiên</th>
                  <th>Hạn giải quyết</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {loadingMine && (
                  <tr>
                    <td
                      colSpan={6}
                      style={{ textAlign: "center", padding: 16 }}
                    >
                      Đang tải.
                    </td>
                  </tr>
                )}
                {!loadingMine &&
                  myItemsView.map((r) => (
                    <tr key={r.ticketId}>
                      <td className="ellipsis" title={r.subject}>
                        {r.subject}
                      </td>
                      <td>
                        <SlaPill value={r.slaStatus} />
                      </td>
                      <td>
                        <SeverityTag value={r.severity} />
                      </td>
                      <td>
                        <PriorityPill value={r.priorityLevel} />
                      </td>
                      <td className="muted">
                        {fmtVNDateOrDash(r.resolutionDueAt)}
                      </td>
                      <td className="tk-row-actions">
                        {/* YÊU CẦU MỚI: chỉ còn nút Chi tiết */}
                        <PermissionGuard moduleCode="SUPPORT_MANAGER" permissionCode="VIEW_DETAIL" fallback={
                          <button
                            className="btn icon-btn ghost disabled"
                            title="Bạn không có quyền xem chi tiết ticket"
                            disabled
                          >
                            <span aria-hidden="true">🔍</span>
                          </button>
                        }>
                          <button
                            className="btn icon-btn ghost"
                            title="Chi tiết"
                            onClick={() =>
                              nav(`/staff/tickets/${r.ticketId}`)
                            }
                          >
                            <span aria-hidden="true">🔍</span>
                          </button>
                        </PermissionGuard>
                      </td>
                    </tr>
                  ))}
                {!loadingMine && !myItemsView.length && (
                  <tr>
                    <td
                      colSpan={6}
                      style={{ textAlign: "center", padding: 16 }}
                    >
                      Không có ticket nào.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {/* Paging riêng cho cột phải */}
          <div className="tk-pager tk-pager-inline">
            <button
              className="btn xs ghost"
              onClick={() => gotoMyPage(myPage - 1)}
              disabled={myPage <= 1}
            >
              « Trước
            </button>
            <span>
              Trang {myPage}/{myTotalPages}
            </span>
            <button
              className="btn xs ghost"
              onClick={() => gotoMyPage(myPage + 1)}
              disabled={myPage >= myTotalPages}
            >
              Sau »
            </button>
          </div>
        </div>
      </div>

      {/* Assign / Transfer modal – hiện chưa gọi từ "Ticket của tôi" nữa nhưng giữ nguyên để tái sử dụng */}
      <AssignModal
        open={modal.open}
        mode={modal.mode}
        title={
          modal.mode === "transfer"
            ? "Chuyển hỗ trợ"
            : "Gán nhân viên phụ trách"
        }
        excludeUserId={
          modal.mode === "transfer" ? modal.currentAssigneeId : null
        }
        onClose={() =>
          setModal({
            open: false,
            mode: "",
            id: null,
            currentAssigneeId: null,
          })
        }
        onConfirm={async (assigneeId) => {
          if (!modal.id) return;
          try {
            if (modal.mode === "transfer") {
              await doTransfer(modal.id, assigneeId);
            } else {
              await doAssign(modal.id, assigneeId);
            }
          } finally {
            setModal({
              open: false,
              mode: "",
              id: null,
              currentAssigneeId: null,
            });
          }
        }}
      />
    </div>
  );
}

// ===== Hook debounce nhỏ cho modal =====
function useDebounced(value, delay) {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return v;
}

function AssignModal({
  open,
  mode,
  title,
  excludeUserId,
  onClose,
  onConfirm,
}) {
  const [q, setQ] = useState("");
  const qDebounced = useDebounced(q, 300);
  const [loading, setLoading] = useState(false);
  const [staffs, setStaffs] = useState([]);
  const [selectedId, setSelectedId] = useState(null);

  useEffect(() => {
    if (!open) return;
    setSelectedId(null);
  }, [open, mode]);

  useEffect(() => {
    if (!open) return;

    const fetchStaff = async () => {
      setLoading(true);
      try {
        let res;
        if (mode === "transfer" && excludeUserId) {
          res = await ticketsApi.getTransferAssignees({
            q: qDebounced,
            excludeUserId,
          });
        } else {
          res = await ticketsApi.getAssignees({ q: qDebounced });
        }
        const items = res?.items ?? res?.Items ?? [];
        setStaffs(items);
      } catch (e) {
        alert(
          e?.response?.data?.message ||
            e.message ||
            "Không tải được danh sách nhân viên."
        );
        setStaffs([]);
      } finally {
        setLoading(false);
      }
    };

    fetchStaff();
  }, [open, mode, excludeUserId, qDebounced]);

  const handleConfirm = () => {
    if (!selectedId) {
      alert("Vui lòng chọn nhân viên.");
      return;
    }
    onConfirm(selectedId);
  };

  if (!open) return null;

  return createPortal(
    <div className="tk-modal">
      <div className="tk-modal-card">
        <div className="tk-modal-head">
          <h2 className="tk-modal-title">
            {title || "Chọn nhân viên phụ trách"}
          </h2>
          <button
            type="button"
            className="btn ghost xs"
            onClick={onClose}
          >
            ✖
          </button>
        </div>
        <div className="tk-modal-body">
          <div className="form-group">
            <label>Tìm nhân viên</label>
            <input
              className="ip"
              placeholder="Nhập tên hoặc email."
              value={q}
              onChange={(e) => setQ(e.target.value)}
            />
          </div>
          <div className="staff-list">
            {loading && <div className="muted">Đang tải.</div>}
            {!loading && !staffs.length && (
              <div className="muted">Không có nhân viên phù hợp.</div>
            )}
            {!loading && staffs.length > 0 && (
              <ul className="staff-ul">
                {staffs.map((s) => (
                  <li
                    key={s.userId}
                    className={
                      "staff-item" +
                      (selectedId === s.userId ? " selected" : "")
                    }
                    onClick={() => setSelectedId(s.userId)}
                  >
                    <div className="staff-avatar">
                      {(s.fullName || s.email || "?")
                        .trim()
                        .charAt(0)
                        .toUpperCase()}
                    </div>
                    <div className="staff-info">
                      <div className="bold">
                        {s.fullName || "Nhân viên"}
                      </div>
                      <div className="muted">
                        {s.email || "Không có email"}
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
        <div className="tk-modal-foot">
          <button
            type="button"
            className="btn ghost"
            onClick={onClose}
          >
            Hủy
          </button>
          <button
            type="button"
            className="btn primary"
            onClick={handleConfirm}
          >
            Xác nhận
          </button>
        </div>
      </div>
    </div>,
    document.body
  );
}
