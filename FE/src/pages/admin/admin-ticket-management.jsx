import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import "../../styles/admin-ticket-management.css";
import { ticketsApi } from "../../api/ticketsApi";

// Filters default
const initialFilters = {
  q: "",
  status: "",
  severity: "",
  sla: "",               // ✅ thêm SLA
  assignmentState: "",
  page: 1,
  pageSize: 10,
};

// Select options
const STATUS_OPTIONS = [
  { value: "", label: "Tất cả trạng thái" },
  { value: "New", label: "Mới" },
  { value: "InProgress", label: "Đang xử lý" },
  { value: "Completed", label: "Hoàn thành" },
  { value: "Closed", label: "Đã đóng" },
];

const SEVERITY_OPTIONS = [
  { value: "", label: "Tất cả mức độ" },
  { value: "Low", label: "Thấp" },
  { value: "Medium", label: "Trung bình" },
  { value: "High", label: "Cao" },
  { value: "Critical", label: "Nghiêm trọng" },
];

const SLA_OPTIONS = [
  { value: "", label: "Tất cả SLA" },
  { value: "OK", label: "Đúng SLA" },
  { value: "Warning", label: "Cảnh báo SLA" },
  { value: "Overdue", label: "Quá hạn SLA" },
];

const ASSIGNMENT_OPTIONS = [
  { value: "", label: "Tất cả phân công" },
  { value: "Unassigned", label: "Chưa gán" },
  { value: "Assigned", label: "Đã gán" },
  { value: "Technical", label: "Đã chuyển" },
];

function fmtVNDate(dt) {
  try {
    const d = typeof dt === "string" || typeof dt === "number" ? new Date(dt) : dt;
    return new Intl.DateTimeFormat("vi-VN", {
      day: "2-digit", month: "2-digit", year: "numeric",
      hour: "2-digit", minute: "2-digit"
    }).format(d);
  } catch { return ""; }
}

function StatusBadge({ value }) {
  const v = normalizeStatus(value);
  const map = {
    New: { cls: "st st-new", text: "Mới" },
    InProgress: { cls: "st st-processing", text: "Đang xử lý" },
    Completed: { cls: "st st-completed", text: "Hoàn thành" },
    Closed: { cls: "st st-closed", text: "Đã đóng" },
  };
  const d = map[v] || map.New;
  return <span className={d.cls}>{d.text}</span>;
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
  if (v === "Overdue") return <span className="sla sla-breached">Quá hạn SLA</span>;
  if (v === "Warning") return <span className="sla sla-warning">Cảnh báo SLA</span>;
  return <span className="sla sla-ok">Đúng SLA</span>;
}
function AssignPill({ value }) {
  const v = (value || "").toString();
  if (v === "Assigned") return <span className="as as-assigned">Đã gán</span>;
  if (v === "Technical") return <span className="as as-technical">Đã chuyển</span>;
  return <span className="as as-unassigned">Chưa gán</span>;
}

function normalizeStatus(s) {
  const v = String(s || "").toLowerCase();
  if (v === "open" || v === "new") return "New";
  if (v === "processing" || v === "inprogress" || v === "in_process") return "InProgress";
  if (v === "done" || v === "resolved" || v === "completed") return "Completed";
  if (v === "closed" || v === "close") return "Closed";
  return "New";
}

export default function AdminTicketManagement() {
  const nav = useNavigate();

  const [ui, setUi] = useState(initialFilters);
  const [applied, setApplied] = useState(initialFilters);

  const [data, setData] = useState({ items: [], totalItems: 0, page: 1, pageSize: 10 });
  const [loading, setLoading] = useState(false);

  const totalPages = useMemo(
    () => Math.max(1, Math.ceil((data.totalItems || 0) / (applied.pageSize || 10))),
    [data.totalItems, applied.pageSize]
  );

  // chuẩn hoá dữ liệu trả về (camelCase/PascalCase đều ok)
  const normalizePaged = (res, fallbacks) => ({
    items: res?.items ?? res?.Items ?? fallbacks.items,
    totalItems: res?.totalItems ?? res?.TotalItems ?? fallbacks.totalItems,
    page: res?.page ?? res?.Page ?? fallbacks.page,
    pageSize: res?.pageSize ?? res?.PageSize ?? fallbacks.pageSize,
  });

  const fetchList = useCallback(async (take = applied) => {
    setLoading(true);
    try {
      const res = await ticketsApi.list(take);
      setData(normalizePaged(res, { items: [], totalItems: 0, page: take.page, pageSize: take.pageSize }));
    } catch (e) {
      alert(e?.response?.data?.message || e.message || "Không tải được danh sách ticket.");
      setData(prev => ({ ...prev, items: [] }));
    } finally { setLoading(false); }
  }, [applied]);

  useEffect(() => {
    fetchList(applied);
  }, [
    applied.page, applied.pageSize,
    applied.q, applied.status, applied.severity, applied.sla, applied.assignmentState,
    fetchList
  ]);

  const onApply = (e) => {
    e.preventDefault();
    setApplied(prev => ({ ...prev, ...ui, page: 1 }));
  };
  const onReset = () => { setUi({ ...initialFilters }); setApplied({ ...initialFilters }); };
  const gotoPage = (p) =>
    setApplied(prev => ({ ...prev, page: Math.max(1, Math.min(totalPages, p)) }));

  // ----- actions -----
  const [modal, setModal] = useState({ open: false, mode: "", id: null });

  const doAssign = async (id) => {
    try { await ticketsApi.assign(id); await fetchList(); }
    catch (e) { alert(e?.response?.data?.message || e.message || "Gán ticket thất bại."); }
  };
  const doTransfer = async (id) => {
    try { await ticketsApi.transferTech(id); await fetchList(); }
    catch (e) { alert(e?.response?.data?.message || e.message || "Chuyển hỗ trợ thất bại."); }
  };
  const doComplete = async (id) => {
    if (!window.confirm("Xác nhận đánh dấu Hoàn thành?")) return;
    try { await ticketsApi.complete(id); await fetchList(); }
    catch (e) { alert(e?.response?.data?.message || e.message || "Hoàn thành ticket thất bại."); }
  };
  const doClose = async (id) => {
    if (!window.confirm("Xác nhận Đóng ticket?")) return;
    try { await ticketsApi.close(id); await fetchList(); }
    catch (e) { alert(e?.response?.data?.message || e.message || "Đóng ticket thất bại."); }
  };

  const actionsFor = (row) => {
    const st = normalizeStatus(row.status);
    const list = { canAssign: false, canTransfer: false, canComplete: false, canClose: false };
    if (st === "New") { list.canAssign = true; list.canClose = true; }
    else if (st === "InProgress") {
      list.canComplete = true;
      list.canTransfer = row.assignmentState === "Assigned" || row.assignmentState === "Technical";
    } // Completed/Closed -> chỉ Chi tiết
    return list;
  };

  const startIndex = (applied.page - 1) * applied.pageSize; // ✅ STT

  return (
    <div className="tk-page">
      <div className="tk-header">
        <h1 className="tk-title">Quản lý Ticket</h1>
      </div>

      {/* Filters: 1 hàng, có SLA */}
      <form className="tk-filters" onSubmit={onApply}>
        <input className="ip" placeholder="Tìm theo mã, tiêu đề, khách hàng, email..."
          value={ui.q} onChange={(e) => setUi(s => ({ ...s, q: e.target.value }))} />
        <select className="ip" value={ui.status} onChange={(e) => setUi(s => ({ ...s, status: e.target.value }))}>
          {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
        <select className="ip" value={ui.severity} onChange={(e) => setUi(s => ({ ...s, severity: e.target.value }))}>
          {SEVERITY_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
        <select className="ip" value={ui.sla} onChange={(e) => setUi(s => ({ ...s, sla: e.target.value }))}>
          {SLA_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
        <select className="ip" value={ui.assignmentState} onChange={(e) => setUi(s => ({ ...s, assignmentState: e.target.value }))}>
          {ASSIGNMENT_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
        <button type="submit" className="btn primary">Áp dụng</button>
        <button type="button" className="btn ghost" onClick={onReset}>Reset</button>
      </form>

      {/* Table */}
      <div className="tk-table-wrap">
        <table className="tk-table">
          <thead>
            <tr>
              <th>STT</th>
              <th>Mã</th>
              <th>Tiêu đề</th>
              <th>Khách hàng</th>
              <th>Trạng thái</th>
              <th>Mức độ</th>
              <th>SLA</th>
              <th>Phân công</th>
              <th>Ngày tạo</th>
              <th style={{ width: 280 }}>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {loading && (
              <tr><td colSpan={10} style={{ textAlign: "center", padding: 16 }}>Đang tải...</td></tr>
            )}
            {!loading && (data.items || []).map((r, i) => {
              const a = actionsFor(r);
              return (
                <tr key={r.ticketId}>
                  <td className="mono">{startIndex + i + 1}</td>
                  <td className="mono">{r.ticketCode}</td>
                  <td className="ellipsis">{r.subject}</td>
                  <td>
                    <div className="cell">
                      <div className="bold">{r.customerName}</div>
                      <div className="muted">{r.customerEmail}</div>
                    </div>
                  </td>
                  <td><StatusBadge value={r.status} /></td>
                  <td><SeverityTag value={r.severity} /></td>
                  <td><SlaPill value={r.slaStatus} /></td>
                  <td><AssignPill value={r.assignmentState} /></td>
                  <td className="muted">{fmtVNDate(r.createdAt)}</td>
                  <td className="tk-row-actions">
                    {a.canAssign && (
                      <button className="btn xs" onClick={() => setModal({ open: true, mode: "assign", id: r.ticketId })}>
                        Gán
                      </button>
                    )}
                    {a.canTransfer && (
                      <button className="btn xs" onClick={() => setModal({ open: true, mode: "transfer", id: r.ticketId })}>
                        Chuyển hỗ trợ
                      </button>
                    )}
                    {a.canComplete && (
                      <button className="btn xs" onClick={() => doComplete(r.ticketId)}>
                        Hoàn thành
                      </button>
                    )}
                    {normalizeStatus(r.status) === "New" && (
                      <button className="btn xs danger" onClick={() => doClose(r.ticketId)}>
                        Đóng
                      </button>
                    )}
                    <button className="btn xs ghost" onClick={() => nav(`/admin/tickets/${r.ticketId}`)}>
                      Chi tiết
                    </button>
                  </td>
                </tr>
              );
            })}
            {!loading && !(data.items || []).length && (
              <tr><td colSpan={10} style={{ textAlign: "center", padding: 16 }}>Không có dữ liệu.</td></tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      <div className="tk-pager">
        <button className="btn xs ghost" onClick={() => gotoPage(applied.page - 1)} disabled={applied.page <= 1}>« Trước</button>
        <span>Trang {applied.page}/{totalPages}</span>
        <button className="btn xs ghost" onClick={() => gotoPage(applied.page + 1)} disabled={applied.page >= totalPages}>Sau »</button>
      </div>

      {/* Assign / Transfer modal (giữ popup) */}
      <AssignModal
        open={modal.open}
        title={modal.mode === "transfer" ? "Chuyển hỗ trợ" : "Gán nhân viên phụ trách"}
        onClose={() => setModal({ open: false, mode: "", id: null })}
        onConfirm={async () => {
          try {
            if (modal.mode === "transfer") await doTransfer(modal.id);
            else await doAssign(modal.id);
          } finally {
            setModal({ open: false, mode: "", id: null });
          }
        }}
      />
    </div>
  );
}

function AssignModal({ open, title, onClose, onConfirm }) {
  if (!open) return null;
  return (
    <div className="tk-modal" role="dialog" aria-modal="true">
      <div className="tk-modal-card">
        <div className="tk-modal-head">
          <h3 className="tk-modal-title">{title}</h3>
          <button className="btn icon ghost" onClick={onClose} aria-label="Đóng">×</button>
        </div>
        <div className="tk-modal-body">
          {/* giữ popup, lược nội dung KB */}
          <div className="form-group">
            <label>Chọn nhân viên hỗ trợ</label>
            <select className="ip">
              <option>Nguyễn Văn A</option>
              <option>Trần Thị B</option>
              <option>Phạm Văn C</option>
            </select>
          </div>
        </div>
        <div className="tk-modal-foot">
          <button className="btn ghost" onClick={onClose}>Huỷ</button>
          <button className="btn primary" onClick={onConfirm}>Xác nhận</button>
        </div>
      </div>
    </div>
  );
}
