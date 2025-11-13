// File: src/pages/admin/admin-ticket-detail.jsx
import React, { useEffect, useState, useMemo } from "react";
import { createPortal } from "react-dom";
import "../../styles/admin-ticket-detail.css";
import { useParams, useNavigate } from "react-router-dom";
import { ticketsApi } from "../../api/ticketsApi";

const MAP_STATUS = {
  New: "Mới",
  InProgress: "Đang xử lý",
  Completed: "Đã hoàn thành",
  Closed: "Đã đóng",
};
const MAP_SEV = {
  Low: "Thấp",
  Medium: "Trung bình",
  High: "Cao",
  Critical: "Nghiêm trọng",
};
const MAP_SLA = { OK: "Đúng hạn", Warning: "Cảnh báo", Overdue: "Quá hạn" };
const MAP_ASN = {
  Unassigned: "Chưa gán",
  Assigned: "Đã gán",
  Technical: "Đã chuyển",
};

function fmtDateTime(v) {
  try {
    const d =
      typeof v === "string" || typeof v === "number" ? new Date(v) : v;
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
function StatusBadge({ value }) {
  const v = normalizeStatus(value);
  const cls =
    v === "New"
      ? "st st-new"
      : v === "InProgress"
      ? "st st-processing"
      : v === "Completed"
      ? "st st-completed"
      : "st st-closed";
  return <span className={cls}>{MAP_STATUS[v] || v}</span>;
}
function SeverityTag({ value }) {
  const v = String(value);
  const cls =
    v === "Low"
      ? "tag tag-low"
      : v === "Medium"
      ? "tag tag-medium"
      : v === "High"
      ? "tag tag-high"
      : "tag tag-critical";
  return <span className={cls}>{MAP_SEV[v] || v}</span>;
}
function SlaPill({ value }) {
  const v = String(value);
  const cls =
    v === "OK"
      ? "sla sla-ok"
      : v === "Overdue"
      ? "sla sla-breached"
      : "sla sla-warning";
  return <span className={cls}>{MAP_SLA[v] || v}</span>;
}

export default function AdminTicketDetail() {
  const { id } = useParams();
  const nav = useNavigate();

  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");

  const [replyText, setReplyText] = useState("");
  const [sending, setSending] = useState(false);
  const [sendEmail, setSendEmail] = useState(false);

  const [modal, setModal] = useState({
    open: false,
    mode: "",
    excludeUserId: null,
  });

  // 👤 user đang đăng nhập (lấy từ localStorage)
  const [currentUser, setCurrentUser] = useState(null);
  const [replyError, setReplyError] = useState("");

  // true nếu người dùng hiện tại là Customer (dựa vào roles trong localStorage)
  const isCustomerView = useMemo(() => {
    if (!currentUser) return false;

    const rawRoles =
      currentUser.roles ||
      currentUser.Roles ||
      currentUser.user?.roles ||
      currentUser.user?.Roles ||
      currentUser.userInfo?.roles ||
      currentUser.userInfo?.Roles ||
      [];

    const rolesArray = Array.isArray(rawRoles) ? rawRoles : [rawRoles];

    return rolesArray.some((r) =>
      String(r || "")
        .trim()
        .toLowerCase()
        .includes("customer")
    );
  }, [currentUser]);

  const draftKey = useMemo(() => `tk_reply_draft_${id}`, [id]);

  const load = async () => {
    setLoading(true);
    setErr("");
    try {
      const res = await ticketsApi.detail(id);
      setData(res);

      const draft = localStorage.getItem(draftKey);
      setReplyText(draft || "");

      // Đọc user từ localStorage (do màn login lưu vào)
      try {
        const rawUser = localStorage.getItem("user");
        if (rawUser) {
          setCurrentUser(JSON.parse(rawUser));
        } else {
          setCurrentUser(null);
        }
      } catch {
        setCurrentUser(null);
      }
    } catch (e) {
      setErr(e?.message || "Không thể tải chi tiết ticket");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const actions = useMemo(() => {
    const s = normalizeStatus(data?.status);
    return {
      canAssign: s === "New",
      canClose: s === "New",
      canComplete: s === "InProgress",
      canTransfer:
        s === "InProgress" &&
        (data?.assignmentState === "Assigned" ||
          data?.assignmentState === "Technical"),
    };
  }, [data]);

  const doAssign = async (assigneeId) => {
    try {
      await ticketsApi.assign(id, assigneeId);
      await load();
    } catch (e) {
      alert(e?.response?.data?.message || e.message || "Gán ticket thất bại.");
    }
  };
  const doTransfer = async (assigneeId) => {
    try {
      await ticketsApi.transferTech(id, assigneeId);
      await load();
    } catch (e) {
      alert(
        e?.response?.data?.message || e.message || "Chuyển hỗ trợ thất bại."
      );
    }
  };
  const doComplete = async () => {
    if (!window.confirm("Xác nhận đánh dấu Hoàn thành?")) return;
    try {
      await ticketsApi.complete(id);
      await load();
    } catch (e) {
      alert(e?.response?.data?.message || e.message || "Hoàn thành thất bại.");
    }
  };
  const doClose = async () => {
    if (!window.confirm("Xác nhận Đóng ticket?")) return;
    try {
      await ticketsApi.close(id);
      await load();
    } catch (e) {
      alert(e?.response?.data?.message || e.message || "Đóng ticket thất bại.");
    }
  };

  const handleQuickInsert = (t) =>
    setReplyText((prev) => (prev ? `${prev}\n${t}` : t));
  const handleSaveDraft = () => {
    localStorage.setItem(draftKey, replyText || "");
    alert("Đã lưu nháp phản hồi.");
  };

  const handleSendReply = async () => {
    const msg = replyText.trim();

    // 🔐 Chưa đăng nhập -> báo lỗi trên màn hình, không gọi API
    const accessToken = localStorage.getItem("access_token");
    if (!accessToken || !currentUser) {
      setReplyError("Bạn cần đăng nhập để gửi phản hồi.");
      return;
    }

    if (!msg) {
      setReplyError("Vui lòng nhập nội dung phản hồi.");
      return;
    }

    try {
      setSending(true);
      setReplyError("");
      const res = await ticketsApi.reply(id, { message: msg, sendEmail });

      // Cập nhật lịch sử trao đổi ngay lập tức (giống realtime)
      setData((prev) =>
        prev
          ? {
              ...prev,
              replies: [...(prev.replies || []), res],
            }
          : prev
      );
      setReplyText("");
      localStorage.removeItem(draftKey);
    } catch (e) {
      setReplyError(
        e?.response?.data?.message ||
          e.message ||
          "Gửi phản hồi thất bại. Vui lòng thử lại."
      );
    } finally {
      setSending(false);
    }
  };

  if (loading)
    return (
      <div className="tkd-page">
        <div className="loading">Đang tải...</div>
      </div>
    );
  if (err)
    return (
      <div className="tkd-page">
        <div className="error">{err}</div>
      </div>
    );
  if (!data)
    return (
      <div className="tkd-page">
        <div className="error">Không tìm thấy dữ liệu ticket</div>
      </div>
    );

  const relatedTickets = data.relatedTickets || [];
  const latestOrder = data.latestOrder || null;

  return (
    <div className="tkd-page">
      <div className="ticket-header">
        <div className="left">
          <div className="code">
            Mã: <strong>{data.ticketCode}</strong>
          </div>
          <h3 className="subject">{data.subject}</h3>
          <div className="meta">
            <span className="chip">
              {MAP_STATUS[data.status] || data.status}
            </span>
            <span className="chip">
              {MAP_SEV[data.severity] || data.severity}
            </span>
            <span className="chip">
              {MAP_SLA[data.slaStatus] || data.slaStatus}
            </span>
            <span className="chip">
              {MAP_ASN[data.assignmentState] || data.assignmentState}
            </span>
            <span className="sub">Tạo lúc: {fmtDateTime(data.createdAt)}</span>
            {data.updatedAt ? (
              <span className="sub">
                Cập nhật: {fmtDateTime(data.updatedAt)}
              </span>
            ) : null}
          </div>
        </div>
        <div className="right">
          {actions.canAssign && (
            <button
              className="btn primary"
              onClick={() =>
                setModal({ open: true, mode: "assign", excludeUserId: null })
              }
            >
              Gán
            </button>
          )}
          {actions.canTransfer && (
            <button
              className="btn warning"
              onClick={() =>
                setModal({
                  open: true,
                  mode: "transfer",
                  excludeUserId: data.assigneeId,
                })
              }
            >
              Chuyển hỗ trợ
            </button>
          )}
          {actions.canComplete && (
            <button className="btn success" onClick={doComplete}>
              Hoàn thành
            </button>
          )}
          {actions.canClose && (
            <button className="btn danger" onClick={doClose}>
              Đóng
            </button>
          )}
          <button className="btn ghost" onClick={() => nav(-1)}>
            Quay lại
          </button>
        </div>
      </div>

      <div className="ticket-content">
        {/* Left column – thread + reply */}
        <div className="left-col">
          <div className="thread">
            <div className="thread-title">Lịch sử trao đổi</div>

            {/* Vùng tin nhắn có scroll riêng */}
            <div className="thread-messages">
              {(data.replies || []).length === 0 && (
                <div className="no-replies">Chưa có trao đổi nào.</div>
              )}

              {(data.replies || []).map((r) => {
                const isStaff = !!r.isStaffReply;
                const isCustomerMsg = !isStaff;

                // Nếu màn hình đang là của customer:
                //   - Tin nhắn customer (isCustomerMsg) -> bên phải
                //   - Tin nhắn staff -> bên trái
                // Nếu màn hình là của staff/admin:
                //   - Tin nhắn staff -> bên phải
                //   - Tin nhắn customer -> bên trái
                const isRightSide = isCustomerView ? isCustomerMsg : isStaff;

                const sender = r.senderName || "Không rõ";

                return (
                  <div
                    key={r.replyId || r.id}
                    className={`msg ${isRightSide ? "msg-me" : "msg-other"}`}
                  >
                    <div className="avatar">
                      {sender.substring(0, 1).toUpperCase()}
                    </div>
                    <div className="bubble">
                      <div className="head">
                        <span className="name">
                          {sender}
                          {isStaff && (
                            <span className="staff-tag">Staff</span>
                          )}
                        </span>
                        <span className="time">
                          {fmtDateTime(r.sentAt || r.createdAt)}
                        </span>
                      </div>
                      <div className="text">{r.message}</div>
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Reply box */}
            <div className="reply-box">
              <div className="reply-title">Phản hồi khách hàng</div>
              <textarea
                className="reply-textarea"
                placeholder="Nhập nội dung phản hồi cho khách hàng..."
                value={replyText}
                onChange={(e) => {
                  setReplyText(e.target.value);
                  if (replyError) setReplyError("");
                }}
              />
              <div className="reply-quick">
                <span>Mẫu phản hồi nhanh</span>
                <div className="reply-quick-buttons">
                  <button
                    type="button"
                    className="chip-btn"
                    onClick={() =>
                      handleQuickInsert(
                        "Chào anh/chị, hệ thống đã tiếp nhận yêu cầu. Em sẽ kiểm tra và phản hồi sớm nhất ạ."
                      )
                    }
                  >
                    Chào hỏi
                  </button>
                  <button
                    type="button"
                    className="chip-btn"
                    onClick={() =>
                      handleQuickInsert(
                        "Hiện tại em đang kiểm tra lại thông tin đơn hàng và key kích hoạt cho anh/chị."
                      )
                    }
                  >
                    Đang kiểm tra
                  </button>
                  <button
                    type="button"
                    className="chip-btn"
                    onClick={() =>
                      handleQuickInsert(
                        "Em đã cập nhật lại key/tài khoản cho anh/chị. Anh/chị vui lòng thử lại và phản hồi giúp em nhé."
                      )
                    }
                  >
                    Giải pháp
                  </button>
                  <button
                    type="button"
                    className="chip-btn"
                    onClick={() =>
                      handleQuickInsert(
                        "Vấn đề đã được xử lý. Nếu cần thêm hỗ trợ anh/chị có thể phản hồi lại ticket này hoặc tạo ticket mới ạ."
                      )
                    }
                  >
                    Kết thúc
                  </button>
                </div>
              </div>

              {/* Lỗi gửi phản hồi (chưa login / nội dung trống / lỗi server) */}
              {replyError && <div className="reply-error">{replyError}</div>}

              <div className="reply-footer">
                <div className="left">
                  {/* <label>
                    <input
                      type="checkbox"
                      checked={sendEmail}
                      onChange={(e) => setSendEmail(e.target.checked)}
                    />
                    Gửi email thông báo
                  </label> */}
                </div>
                <div className="right">
                  <button
                    type="button"
                    className="btn ghost"
                    onClick={handleSaveDraft}
                  >
                    Lưu nháp
                  </button>
                  <button
                    type="button"
                    className="btn primary"
                    onClick={handleSendReply}
                    disabled={sending}
                  >
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
            <div className="kv">
              <span className="k">Họ tên</span>
              <span className="v">{data.customerName || "-"}</span>
            </div>
            <div className="kv">
              <span className="k">Email</span>
              <span className="v">{data.customerEmail || "-"}</span>
            </div>
            <div className="kv">
              <span className="k">Điện thoại</span>
              <span className="v">{data.customerPhone || "-"}</span>
            </div>
          </div>

          {/* Nhân viên – tách card riêng */}
          <div className="card">
            <div className="card-title">Thông tin nhân viên</div>
            {data.assigneeName || data.assigneeEmail ? (
              <>
                <div className="kv">
                  <span className="k">Trạng thái</span>
                  <span className="v">
                    {MAP_ASN[data.assignmentState] || data.assignmentState}
                  </span>
                </div>
                <div className="kv">
                  <span className="k">Nhân viên</span>
                  <span className="v">{data.assigneeName || "-"}</span>
                </div>
                <div className="kv">
                  <span className="k">Email</span>
                  <span className="v">{data.assigneeEmail || "-"}</span>
                </div>
              </>
            ) : (
              <div className="empty small">Chưa được gán.</div>
            )}
          </div>

          {/* Đơn hàng gần nhất */}
          <div className="card">
            <div className="card-title">Đơn hàng gần nhất</div>
            {!latestOrder && (
              <div className="empty small">Khách hàng chưa có đơn hàng.</div>
            )}
            {latestOrder && (
              <>
                <div className="kv">
                  <span className="k">Mã đơn</span>
                  <span className="v mono">{latestOrder.orderId}</span>
                </div>
                <div className="kv">
                  <span className="k">Ngày tạo</span>
                  <span className="v">
                    {fmtDateTime(latestOrder.createdAt)}
                  </span>
                </div>
                <div className="kv">
                  <span className="k">Trạng thái</span>
                  <span className="v">{latestOrder.status}</span>
                </div>
                <div className="kv">
                  <span className="k">Tổng tiền</span>
                  <span className="v">
                    {latestOrder.finalAmount?.toLocaleString("vi-VN", {
                      style: "currency",
                      currency: "VND",
                    }) ||
                      latestOrder.totalAmount?.toLocaleString("vi-VN", {
                        style: "currency",
                        currency: "VND",
                      })}
                  </span>
                </div>
              </>
            )}
          </div>

          {/* Ticket liên quan */}
          <div className="panel related">
            <div className="panel-title">Ticket liên quan</div>
            {(relatedTickets || []).length === 0 && (
              <div className="empty">
                Không có ticket nào khác của khách hàng này.
              </div>
            )}
            <div className="related-list">
              {(relatedTickets || []).map((t) => (
                <div key={t.ticketId} className="related-item">
                  <div className="ri-main">
                    <div className="ri-line1">
                      <span className="ri-code">#{t.ticketCode}</span>
                      <span className="ri-dot">•</span>
                      <span className="ri-time">
                        {fmtDateTime(t.createdAt)}
                      </span>
                    </div>
                    <div className="ri-subject" title={t.subject}>
                      {t.subject}
                    </div>
                    <div className="ri-meta">
                      <StatusBadge value={t.status} />
                      <SeverityTag value={t.severity} />
                      <SlaPill value={t.slaStatus} />
                    </div>
                  </div>
                  <div className="ri-actions">
                    <button
                      className="btn xs ghost"
                      onClick={() => nav(`/admin/tickets/${t.ticketId}`)}
                    >
                      Chi tiết
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Modal gán / chuyển hỗ trợ */}
      <AssignModal
        open={modal.open}
        title={
          modal.mode === "transfer"
            ? "Chuyển hỗ trợ"
            : "Gán nhân viên phụ trách"
        }
        excludeUserId={modal.excludeUserId}
        onClose={() =>
          setModal({ open: false, mode: "", excludeUserId: null })
        }
        onConfirm={async (userId) => {
          try {
            if (modal.mode === "transfer") await doTransfer(userId);
            else await doAssign(userId);
          } finally {
            setModal({ open: false, mode: "", excludeUserId: null });
          }
        }}
      />
    </div>
  );
}

function useDebounced(value, delay = 250) {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return v;
}

function AssignModal({ open, title, onClose, onConfirm, excludeUserId }) {
  const [list, setList] = useState([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState("");
  const debounced = useDebounced(search, 250);
  const [selected, setSelected] = useState("");

  useEffect(() => {
    if (!open) {
      setSearch("");
      setSelected("");
      setList([]);
    }
  }, [open]);

  useEffect(() => {
    if (!open) return;
    let alive = true;
    (async () => {
      try {
        setLoading(true);
        let res;
        if (excludeUserId) {
          res = await ticketsApi.getTransferAssignees({
            q: debounced,
            excludeUserId,
            pageSize: 50,
            page: 1,
          });
        } else {
          res = await ticketsApi.getAssignees({
            q: debounced,
            pageSize: 50,
            page: 1,
          });
        }
        const items = Array.isArray(res) ? res : [];
        const mapped = items.map((u) => ({
          id: u.userId,
          name: u.fullName || u.email,
          email: u.email,
        }));
        if (alive) setList(mapped);
      } catch {
        if (alive) setList([]);
      } finally {
        if (alive) setLoading(false);
      }
    })();
    return () => {
      alive = false;
    };
  }, [open, debounced, excludeUserId]);

  if (!open) return null;

  return createPortal(
    <div className="tk-modal" role="dialog" aria-modal="true">
      <div className="tk-modal-card">
        <div className="tk-modal-head">
          <h3 className="tk-modal-title">{title}</h3>
          <button type="button" className="btn icon" onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="tk-modal-body">
          <div className="form-group">
            <label>Tìm theo tên hoặc email</label>
            <input
              className="ip"
              placeholder="Nhập tên hoặc email..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <div className="staff-list">
            {loading && (
              <div className="empty small">Đang tải danh sách nhân viên...</div>
            )}
            {!loading && !list.length && (
              <div className="empty small">
                Không tìm thấy nhân viên phù hợp.
              </div>
            )}
            {!loading && !!list.length && (
              <ul className="staff-ul">
                {list.map((u) => (
                  <li
                    key={u.id}
                    className={
                      selected === u.id ? "staff-item selected" : "staff-item"
                    }
                  >
                    <label>
                      <input
                        type="radio"
                        name="staff"
                        value={u.id}
                        checked={selected === u.id}
                        onChange={() => setSelected(u.id)}
                      />
                      <div className="staff-info">
                        <div className="staff-name">{u.name}</div>
                        <div className="staff-email">{u.email}</div>
                      </div>
                    </label>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
        <div className="tk-modal-foot">
          <button type="button" className="btn ghost" onClick={onClose}>
            Hủy
          </button>
          <button
            type="button"
            className="btn primary"
            disabled={!selected}
            onClick={() => onConfirm(selected)}
          >
            Xác nhận
          </button>
        </div>
      </div>
    </div>,
    document.body
  );
}
