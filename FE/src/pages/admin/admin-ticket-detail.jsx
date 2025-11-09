// File: src/pages/admin/admin-ticket-detail.jsx
import React, { useEffect, useState, useMemo } from "react";
import "../../styles/admin-ticket-detail.css";
import { useParams, useNavigate } from "react-router-dom";
import { ticketsApi } from "../../api/ticketsApi";
import axiosClient from "../../api/axiosClient";

const MAP_STATUS = { New: "Mới", InProgress: "Đang xử lý", Completed: "Đã hoàn thành", Closed: "Đã đóng" };
const MAP_SEV = { Low: "Thấp", Medium: "Trung bình", High: "Cao", Critical: "Nghiêm trọng" };
const MAP_SLA = { OK: "Đúng hạn", Warning: "Cảnh báo", Overdue: "Quá hạn" };
const MAP_ASN = { Unassigned: "Chưa gán", Assigned: "Đã gán", Technical: "Đã chuyển" };

function fmtDateTime(v){
  try{ const d = typeof v==="string"||typeof v==="number"?new Date(v):v;
    return new Intl.DateTimeFormat("vi-VN",{day:"2-digit",month:"2-digit",year:"numeric",hour:"2-digit",minute:"2-digit"}).format(d);
  }catch{ return ""; }
}
function normalizeStatus(s){
  const v=String(s||"").toLowerCase();
  if(v==="open"||v==="new")return"New";
  if(v==="processing"||v==="inprogress"||v==="in_process")return"InProgress";
  if(v==="done"||v==="resolved"||v==="completed")return"Completed";
  if(v==="closed"||v==="close")return"Closed";
  return"New";
}

export default function AdminTicketDetail(){
  const { id } = useParams();
  const nav = useNavigate();

  const [data,setData]=useState(null);
  const [loading,setLoading]=useState(true);
  const [err,setErr]=useState("");

  const [replyText,setReplyText]=useState("");
  const [sending,setSending]=useState(false);
  const [sendEmail,setSendEmail]=useState(false);

  const [modal,setModal]=useState({open:false,mode:"",excludeUserId:null});

  const draftKey = useMemo(()=>`tk_reply_draft_${id}`,[id]);

  const load = async ()=>{
    setLoading(true); setErr("");
    try{
      const res = await ticketsApi.detail(id);
      setData(res);
      const draft = localStorage.getItem(draftKey);
      setReplyText(draft||"");
    }catch(e){ setErr(e?.message||"Không thể tải chi tiết ticket"); }
    finally{ setLoading(false); }
  };
  useEffect(()=>{ load(); /* eslint-disable-next-line */ },[id]);

  const actions = useMemo(()=>{
    const s = normalizeStatus(data?.status);
    return {
      canAssign: s==="New",
      canClose: s==="New",
      canComplete: s==="InProgress",
      canTransfer: s==="InProgress" && (data?.assignmentState==="Assigned" || data?.assignmentState==="Technical"),
    };
  },[data]);

  const doAssign = async (assigneeId)=>{ try{ await ticketsApi.assign(id,assigneeId); await load(); } catch(e){ alert(e.message); } };
  const doTransfer = async (assigneeId)=>{ try{ await ticketsApi.transferTech(id,assigneeId); await load(); } catch(e){ alert(e.message); } };
  const doComplete = async ()=>{ if(!window.confirm("Xác nhận đánh dấu Hoàn thành?"))return;
    try{ await ticketsApi.complete(id); await load(); } catch(e){ alert(e.message); } };
  const doClose = async ()=>{ if(!window.confirm("Xác nhận Đóng ticket?"))return;
    try{ await ticketsApi.close(id); await load(); } catch(e){ alert(e.message); } };

  const handleQuickInsert = (t)=> setReplyText(prev => (prev ? `${prev}\n${t}` : t));
  const handleSaveDraft = ()=>{ localStorage.setItem(draftKey, replyText||""); alert("Đã lưu nháp phản hồi."); };

  const handleSendReply = async ()=>{
    const msg = replyText.trim();
    if(!msg){ alert("Vui lòng nhập nội dung phản hồi."); return; }
    try{
      setSending(true);
      const res = await ticketsApi.reply(id,{ message: msg, sendEmail });
      setData(prev => prev ? { ...prev, replies:[...(prev.replies||[]), res] } : prev);
      setReplyText(""); localStorage.removeItem(draftKey);
    }catch(e){ alert(e?.response?.data?.message || e.message || "Gửi phản hồi thất bại. Vui lòng thử lại."); }
    finally{ setSending(false); }
  };

  if(loading) return <div className="tkd-page"><div className="loading">Đang tải...</div></div>;
  if(err) return <div className="tkd-page"><div className="error">{err}</div></div>;
  if(!data) return <div className="tkd-page"><div className="error">Không tìm thấy dữ liệu ticket</div></div>;

  const relatedTickets = data.relatedTickets || [];
  const latestOrder = data.latestOrder || null;

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
          {actions.canAssign && (
            <button className="btn primary" onClick={()=>setModal({open:true,mode:"assign",excludeUserId:null})}>Gán</button>
          )}
          {actions.canTransfer && (
            <button className="btn warning" onClick={()=>setModal({open:true,mode:"transfer",excludeUserId:data.assigneeId})}>Chuyển hỗ trợ</button>
          )}
          {actions.canComplete && <button className="btn success" onClick={doComplete}>Hoàn thành</button>}
          {actions.canClose && <button className="btn danger" onClick={doClose}>Đóng</button>}
          <button className="btn ghost" onClick={()=>nav(-1)}>Quay lại</button>
        </div>
      </div>

      <div className="ticket-content">
        {/* Left column – thread + reply */}
        <div className="left-col">
          <div className="thread">
            <div className="thread-title">Lịch sử trao đổi</div>
            {(data.replies || []).length === 0 && <div className="empty">Chưa có phản hồi</div>}
            {(data.replies || []).map(r => (
              <div key={r.replyId} className={`msg ${r.isStaffReply ? "staff" : "customer"}`}>
                <div className="avatar">{(r.senderName||"?").substring(0,1).toUpperCase()}</div>
                <div className="bubble">
                  <div className="head">
                    <span className="name">{r.senderName}</span>
                    <span className="time">{fmtDateTime(r.sentAt)}</span>
                  </div>
                  <div className="text">{r.message}</div>
                </div>
              </div>
            ))}

            {/* Reply box */}
            <div className="reply-box">
              <div className="reply-title">Phản hồi khách hàng</div>
              <textarea className="reply-textarea" placeholder="Nhập nội dung phản hồi cho khách hàng..." value={replyText}
                        onChange={(e)=>setReplyText(e.target.value)} />
              <div className="reply-quick">
                <span>Mẫu phản hồi nhanh</span>
                <div className="reply-quick-buttons">
                  <button type="button" className="chip-btn" onClick={()=>handleQuickInsert("Chào anh/chị, hệ thống đã tiếp nhận yêu cầu. Em sẽ kiểm tra và phản hồi sớm nhất ạ.")}>Chào hỏi</button>
                  <button type="button" className="chip-btn" onClick={()=>handleQuickInsert("Hiện tại em đang kiểm tra lại thông tin đơn hàng và key kích hoạt cho anh/chị.")}>Đang kiểm tra</button>
                  <button type="button" className="chip-btn" onClick={()=>handleQuickInsert("Em đã cập nhật lại key/tài khoản cho anh/chị. Anh/chị vui lòng thử lại và phản hồi giúp em nhé.")}>Giải pháp</button>
                  <button type="button" className="chip-btn" onClick={()=>handleQuickInsert("Vấn đề đã được xử lý. Nếu cần thêm hỗ trợ anh/chị có thể phản hồi lại ticket này hoặc tạo ticket mới ạ.")}>Kết thúc</button>
                </div>
              </div>
              <div className="reply-footer">
                <div className="left">
                  <label>
                    <input type="checkbox" checked={sendEmail} onChange={(e)=>setSendEmail(e.target.checked)} />
                    Gửi email thông báo
                  </label>
                </div>
                <div className="right">
                  <button type="button" className="btn ghost" onClick={handleSaveDraft}>Lưu nháp</button>
                  <button type="button" className="btn primary" onClick={handleSendReply} disabled={sending}>
                    {sending ? "Đang gửi..." : "Gửi phản hồi"}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Right column – info cards */}
        <div className="right-col">
          {/* Khách hàng */}
          <div className="card">
            <div className="card-title">Thông tin khách hàng</div>
            <div className="kv"><span className="k">Họ tên</span><span className="v">{data.customerName || "-"}</span></div>
            <div className="kv"><span className="k">Email</span><span className="v">{data.customerEmail || "-"}</span></div>
            <div className="kv"><span className="k">Điện thoại</span><span className="v">{data.customerPhone || "-"}</span></div>
          </div>

          {/* ✅ Nhân viên – tách card riêng */}
          <div className="card">
            <div className="card-title">Thông tin nhân viên</div>
            {data.assigneeName || data.assigneeEmail ? (
              <>
                <div className="kv"><span className="k">Trạng thái</span><span className="v">{MAP_ASN[data.assignmentState] || data.assignmentState}</span></div>
                <div className="kv"><span className="k">Nhân viên</span><span className="v">{data.assigneeName || "-"}</span></div>
                <div className="kv"><span className="k">Email</span><span className="v">{data.assigneeEmail || "-"}</span></div>
              </>
            ) : (
              <div className="empty small">Chưa được gán.</div>
            )}
          </div>

          {/* Đơn hàng gần nhất */}
          <div className="card">
            <div className="card-title">Đơn hàng gần nhất</div>
            {!latestOrder && <div className="empty small">Khách hàng chưa có đơn hàng.</div>}
            {latestOrder && (
              <>
                <div className="kv"><span className="k">Mã đơn</span><span className="v mono">{latestOrder.orderId}</span></div>
                <div className="kv"><span className="k">Ngày tạo</span><span className="v">{fmtDateTime(latestOrder.createdAt)}</span></div>
                <div className="kv"><span className="k">Trạng thái</span><span className="v">{latestOrder.status}</span></div>
                <div className="kv"><span className="k">Tổng tiền</span>
                  <span className="v">
                    {latestOrder.finalAmount?.toLocaleString("vi-VN",{style:"currency",currency:"VND"}) ||
                     latestOrder.totalAmount?.toLocaleString("vi-VN",{style:"currency",currency:"VND"})}
                  </span>
                </div>
              </>
            )}
          </div>

          {/* Ticket liên quan */}
          <div className="card">
            <div className="card-title">Ticket liên quan</div>
            {relatedTickets.length===0 && <div className="empty small">Không có ticket nào khác.</div>}
            {relatedTickets.length>0 && (
              <div className="related-list">
                {relatedTickets.map(t => (
                  <div key={t.ticketId} className="related-item" onClick={()=>nav(`/admin/tickets/${t.ticketId}`)}>
                    <div className="code">#{t.ticketCode} · {fmtDateTime(t.createdAt)}</div>
                    <div className="subject">{t.subject}</div>
                    <div className="meta">
                      <span>{MAP_STATUS[t.status] || t.status}</span><span>·</span>
                      <span>{MAP_SEV[t.severity] || t.severity}</span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      <AssignModal
        open={modal.open}
        title={modal.mode==="transfer" ? "Chuyển hỗ trợ" : "Gán nhân viên phụ trách"}
        excludeUserId={modal.excludeUserId}
        onClose={()=>setModal({open:false,mode:"",excludeUserId:null})}
        onConfirm={async (userId)=>{
          try{ if(modal.mode==="transfer") await doTransfer(userId); else await doAssign(userId); }
          finally{ setModal({open:false,mode:"",excludeUserId:null}); }
        }}
      />
    </div>
  );
}

function useDebounced(value, delay=250){
  const [v,setV]=useState(value);
  useEffect(()=>{ const t=setTimeout(()=>setV(value),delay); return()=>clearTimeout(t); },[value,delay]);
  return v;
}

function AssignModal({ open, title, onClose, onConfirm, excludeUserId }){
  const [list,setList]=useState([]); const [loading,setLoading]=useState(false);
  const [search,setSearch]=useState(""); const debounced=useDebounced(search,250);
  const [selected,setSelected]=useState("");

  useEffect(()=>{ if(!open){ setSearch(""); setSelected(""); setList([]);} },[open]);

  useEffect(()=>{
    if(!open) return; let alive=true;
    (async()=>{
      try{
        setLoading(true);
        const roles=await axiosClient.get("/roles");
        const staffRole=(roles||[]).find(r=>String(r.name).toLowerCase()==="customer care staff".toLowerCase());
        if(!staffRole){ setList([]); return; }
        const res=await axiosClient.get("/users",{ params:{ roleId:staffRole.roleId, status:"Active", q:debounced, pageSize:50, page:1 }});
        const items=res?.items ?? res?.Items ?? [];
        let mapped=items.map(u=>({ id:u.userId, name:u.fullName||u.email, email:u.email }));
        if(excludeUserId) mapped=mapped.filter(x=>String(x.id).toLowerCase()!==String(excludeUserId||"").toLowerCase());
        if(alive) setList(mapped);
      }catch{ if(alive) setList([]); }
      finally{ if(alive) setLoading(false); }
    })();
    return()=>{ alive=false; };
  },[open,debounced,excludeUserId]);

  if(!open) return null;
  return (
    <div className="tk-modal" role="dialog" aria-modal="true">
      <div className="tk-modal-card">
        <div className="tk-modal-head">
          <h3 className="tk-modal-title">{title}</h3>
          <button className="btn icon ghost" onClick={onClose} aria-label="Đóng">×</button>
        </div>
        <div className="tk-modal-body">
          <div className="form-group">
            <label>Tìm kiếm (tên hoặc email)</label>
            <input className="ip" placeholder="Nhập để lọc..." value={search} onChange={(e)=>setSearch(e.target.value)} />
          </div>
          <div className="form-group">
            <label>Chọn nhân viên hỗ trợ</label>
            {loading ? <div style={{padding:"8px 0"}}>Đang tải...</div> : (
              <select className="ip" size={Math.min(8, Math.max(3, list.length))} value={selected} onChange={(e)=>setSelected(e.target.value)}>
                {list.map(u => <option key={u.id} value={u.id}>{u.name} — {u.email}</option>)}
              </select>
            )}
            {!loading && list.length===0 && <div style={{padding:"8px 0"}}>Không có nhân viên phù hợp.</div>}
          </div>
        </div>
        <div className="tk-modal-foot">
          <button className="btn ghost" onClick={onClose}>Hủy</button>
          <button className="btn primary" disabled={!selected} onClick={()=>onConfirm(selected)}>Xác nhận</button>
        </div>
      </div>
    </div>
  );
}
