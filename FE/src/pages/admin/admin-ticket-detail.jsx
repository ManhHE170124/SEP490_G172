import React, { useEffect, useState, useMemo } from "react";
import "../../styles/admin-ticket-detail.css";
import { useParams, useNavigate } from "react-router-dom";
import { ticketsApi } from "../../api/ticketsApi";

const MAP_STATUS = { New: "Mới", InProgress: "Đang xử lý", Completed: "Đã hoàn thành", Closed: "Đã đóng" };
const MAP_SEV = { Low: "Thấp", Medium: "Trung bình", High: "Cao", Critical: "Nghiêm trọng" };
const MAP_SLA = { OK: "Đúng hạn", Warning: "Cảnh báo", Overdue: "Quá hạn" };
const MAP_ASN = { Unassigned: "Chưa gán", Assigned: "Đã gán", Technical: "Đã chuyển" };

function fmtDateTime(v) {
  try {
    const d = typeof v === "string" || typeof v === "number" ? new Date(v) : v;
    return new Intl.DateTimeFormat("vi-VN", {
      day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit",
    }).format(d);
  } catch { return ""; }
}

function normalizeStatus(s) {
  const v = String(s || "").toLowerCase();
  if (v === "open" || v === "new") return "New";
  if (v === "processing" || v === "inprogress" || v === "in_process") return "InProgress";
  if (v === "done" || v === "resolved" || v === "completed") return "Completed";
  if (v === "closed" || v === "close") return "Closed";
  return "New";
}

export default function AdminTicketDetail() {
  const { id } = useParams();
  const nav = useNavigate();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");

  const load = async () => {
    setLoading(true); setErr("");
    try {
      const res = await ticketsApi.detail(id);
      setData(res);
    } catch (e) {
      setErr(e?.message || "Không thể tải chi tiết ticket");
    } finally { setLoading(false); }
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [id]);

  // Quy tắc hiển thị nút giống màn list
  const actions = useMemo(() => {
    const s = normalizeStatus(data?.status);
    return {
      canAssign: s === "New",
      canClose: s === "New",
      canComplete: s === "InProgress",
      canTransfer: s === "InProgress" && (data?.assignmentState === "Assigned" || data?.assignmentState === "Technical"),
    };
  }, [data]);

  const doAssign = async () => { try { await ticketsApi.assign(id); await load(); } catch (e) { alert(e.message); } };
  const doTransfer = async () => { try { await ticketsApi.transferTech(id); await load(); } catch (e) { alert(e.message); } };
  const doComplete = async () => {
    if (!window.confirm("Xác nhận đánh dấu Hoàn thành?")) return;
    try { await ticketsApi.complete(id); await load(); } catch (e) { alert(e.message); }
  };
  const doClose = async () => {
    if (!window.confirm("Xác nhận Đóng ticket?")) return;
    try { await ticketsApi.close(id); await load(); } catch (e) { alert(e.message); }
  };

  if (loading) return <div className="tkd-page"><div className="loading">Đang tải...</div></div>;
  if (err) return <div className="tkd-page"><div className="error">{err}</div></div>;
  if (!data) return <div className="tkd-page"><div className="error">Không tìm thấy dữ liệu ticket</div></div>;

  return (
    <div className="tkd-page">
      <div className="ticket-header">
        <div className="left">
          <div className="code">Mã: <strong>{data.ticketCode}</strong></div>
          <h3 className="subject">{data.subject}</h3>
          <div className="meta">
            <span className="chip">{MAP_STATUS[data.status] || data.status}</span>
            <span className="chip">{MAP_SEV[data.severity] || data.severity}</span>
            <span className="chip">{MAP_SLA[data.slaStatus] || data.slaStatus}</span>
            <span className="chip">{MAP_ASN[data.assignmentState] || data.assignmentState}</span>
            <span className="sub">Tạo lúc: {fmtDateTime(data.createdAt)}</span>
            {data.updatedAt ? <span className="sub">Cập nhật: {fmtDateTime(data.updatedAt)}</span> : null}
          </div>
        </div>
        <div className="right">
          {actions.canAssign && <button className="btn primary" onClick={doAssign}>Gán</button>}
          {actions.canTransfer && <button className="btn warning" onClick={doTransfer}>Chuyển hỗ trợ</button>}
          {actions.canComplete && <button className="btn success" onClick={doComplete}>Hoàn thành</button>}
          {actions.canClose && <button className="btn danger" onClick={doClose}>Đóng</button>}
          <button className="btn ghost" onClick={() => nav(-1)}>Quay lại</button>
        </div>
      </div>

      <div className="ticket-content">
        <div className="left-col">
          <div className="thread">
            <div className="thread-title">Lịch sử trao đổi</div>
            {(data.replies || []).length === 0 && (
              <div className="empty">Chưa có phản hồi</div>
            )}
            {(data.replies || []).map(r => (
              <div key={r.replyId} className={`msg ${r.isStaffReply ? "staff" : "customer"}`}>
                <div className="avatar">{(r.senderName || "?").substring(0,1).toUpperCase()}</div>
                <div className="bubble">
                  <div className="head">
                    <span className="name">{r.senderName}</span>
                    <span className="time">{fmtDateTime(r.sentAt)}</span>
                  </div>
                  <div className="text">{r.message}</div>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="right-col">
          <div className="card">
            <div className="card-title">Thông tin khách hàng</div>
            <div className="kv"><span className="k">Họ tên</span><span className="v">{data.customerName || "-"}</span></div>
            <div className="kv"><span className="k">Email</span><span className="v">{data.customerEmail || "-"}</span></div>
            <div className="kv"><span className="k">Điện thoại</span><span className="v">{data.customerPhone || "-"}</span></div>
            {data.assigneeName && (
              <div className="kv"><span className="k">Nhân viên</span><span className="v">{data.assigneeName}</span></div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
