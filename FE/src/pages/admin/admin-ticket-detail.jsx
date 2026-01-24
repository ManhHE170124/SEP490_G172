// File: src/pages/admin/admin-ticket-detail.jsx
import React, { useEffect, useState, useMemo, useRef } from "react";
import { createPortal } from "react-dom";
import "../../styles/admin-ticket-detail.css";
import { useParams, useNavigate } from "react-router-dom";
import { ticketsApi } from "../../api/ticketsApi";
import axiosClient from "../../api/axiosClient";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

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

// ⭐ Mapping PriorityLevel → tiếng Việt
const MAP_PRIORITY = {
  0: "Tiêu chuẩn",
  1: "Ưu tiên",
  2: "VIP",
};

// ✅ Order status → tiếng Việt (Admin Ticket Detail: Đơn hàng gần nhất)
const MAP_ORDER_STATUS = {
  PendingPayment: "Chờ thanh toán",
  Paid: "Đã thanh toán",
  Refunded: "Đã hoàn tiền",
  NeedsManualAction: "Cần xử lý thủ công",
  CancelledByTimeout: "Đã hủy do quá hạn",
  Cancelled: "Đã hủy",
};

// ✅ FE-only timezone display: luôn hiển thị theo UTC+7 (Asia/Bangkok)
const DISPLAY_TZ = "Asia/Bangkok";

function hasTimeZoneDesignator(s) {
  return (
    /[zZ]$/.test(s) ||
    /[+\-]\d{2}:\d{2}$/.test(s) ||
    /[+\-]\d{2}\d{2}$/.test(s)
  );
}

function parseApiDateAssumeUtcIfNoTz(v) {
  if (!v) return null;
  if (v instanceof Date) return v;

  if (typeof v === "number") {
    const dNum = new Date(v);
    return Number.isNaN(dNum.getTime()) ? null : dNum;
  }

  const s = String(v).trim();
  if (!s) return null;

  const iso = hasTimeZoneDesignator(s) ? s : `${s}Z`;
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? null : d;
}

function fmtDateTime(v) {
  try {
    const d = parseApiDateAssumeUtcIfNoTz(v);
    if (!d) return "";

    return new Intl.DateTimeFormat("vi-VN", {
      timeZone: DISPLAY_TZ,
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
  if (["processing", "inprogress", "in_process"].includes(v)) return "InProgress";
  if (["done", "resolved", "completed"].includes(v)) return "Completed";
  if (v === "closed" || v === "close") return "Closed";
  return "New";
}

/** ✅ normalize assignment state về 3 trạng thái chuẩn để luôn hiển thị tiếng Việt */
function normalizeAssignmentState(s) {
  const v = String(s || "").trim().toLowerCase();
  if (!v) return "Unassigned";

  // Unassigned variants
  if (
    v === "unassigned" ||
    v === "none" ||
    v === "null" ||
    v === "notassigned" ||
    v.includes("unassigned")
  ) {
    return "Unassigned";
  }

  // Technical / transferred variants
  if (
    v === "technical" ||
    v.includes("tech") ||
    v.includes("technical") ||
    v.includes("transfer") ||
    v.includes("forward")
  ) {
    return "Technical";
  }

  // Assigned variants
  if (v === "assigned" || v.includes("assign")) {
    return "Assigned";
  }

  // fallback: nếu BE trả lạ thì coi như đã gán (thường là một dạng assigned)
  return "Assigned";
}

/** ✅ lấy assignment state từ nhiều field có thể có (BE có thể trả khác casing/name) */
function getAssignmentStateFromTicket(t) {
  const raw =
    t?.assignmentState ??
    t?.AssignmentState ??
    t?.assignmentStatus ??
    t?.AssignmentStatus ??
    t?.assignState ??
    t?.AssignState ??
    "";
  return normalizeAssignmentState(raw);
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

// ✅ Order status pill (colors in CSS)
function OrderStatusPill({ value }) {
  const raw = String(value || "").trim();
  const label = MAP_ORDER_STATUS[raw] || (raw ? raw : "-");

  const cls =
    raw === "PendingPayment"
      ? "ost ost-pending"
      : raw === "Paid"
        ? "ost ost-paid"
        : raw === "Refunded"
          ? "ost ost-refunded"
          : raw === "NeedsManualAction"
            ? "ost ost-manual"
            : raw === "CancelledByTimeout"
              ? "ost ost-timeout"
              : raw === "Cancelled"
                ? "ost ost-cancelled"
                : "ost ost-unknown";

  return <span className={cls}>{label}</span>;
}

// ===== Avatar helpers (Ticket thread) =====
function getApiRoot() {
  let apiBase = axiosClient?.defaults?.baseURL || "";
  if (!apiBase) {
    apiBase =
      process.env.REACT_APP_API_URL ||
      (typeof import.meta !== "undefined" &&
        import.meta.env &&
        import.meta.env.VITE_API_BASE_URL) ||
      "https://localhost:7292/api";
  }
  return apiBase.replace(/\/api\/?$/i, "");
}

function resolveAvatarUrl(rawUrl) {
  if (!rawUrl) return "";
  const u = String(rawUrl).trim();
  if (!u) return "";

  if (/^(https?:)?\/\//i.test(u) || /^data:/i.test(u) || /^blob:/i.test(u)) {
    return u;
  }

  const root = getApiRoot();
  if (!root) return u;

  if (u.startsWith("/")) return `${root}${u}`;
  return `${root}/${u}`;
}

function ChatAvatar({ name, avatarUrl }) {
  const letter = (String(name || "?").trim().substring(0, 1) || "?").toUpperCase();
  const src = resolveAvatarUrl(avatarUrl);

  return (
    <div className="avatar" style={{ position: "relative", overflow: "hidden" }}>
      <span
        style={{
          position: "absolute",
          inset: 0,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        {letter}
      </span>

      {src && (
        <img
          src={src}
          alt={String(name || "")}
          style={{
            position: "absolute",
            inset: 0,
            width: "100%",
            height: "100%",
            objectFit: "cover",
          }}
          onError={(e) => {
            e.currentTarget.style.display = "none";
          }}
        />
      )}
    </div>
  );
}

function fmtPriority(level) {
  if (level === null || level === undefined) return "-";
  let num =
    typeof level === "number"
      ? level
      : typeof level === "string" && level.trim() !== ""
        ? Number(level)
        : NaN;
  if (!Number.isFinite(num)) return "-";
  return MAP_PRIORITY[num] || "-";
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

  const [currentUser, setCurrentUser] = useState(null);
  const [replyError, setReplyError] = useState("");

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
      String(r || "").trim().toLowerCase().includes("customer")
    );
  }, [currentUser]);

  const draftKey = useMemo(() => `tk_reply_draft_${id}`, [id]);

  const messagesRef = useRef(null);
  const isAtBottomRef = useRef(true);
  const initialScrollDoneRef = useRef(false);

  const load = async () => {
    setLoading(true);
    setErr("");
    try {
      const res = await ticketsApi.detail(id);
      setData(res);

      const draft = localStorage.getItem(draftKey);
      setReplyText(draft || "");

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

  useEffect(() => {
    if (!id) return;

    let apiBase = axiosClient?.defaults?.baseURL || "";
    if (!apiBase) {
      apiBase =
        process.env.REACT_APP_API_URL ||
        (typeof import.meta !== "undefined" &&
          import.meta.env &&
          import.meta.env.VITE_API_BASE_URL) ||
        "https://localhost:7292/api";
    }
    const hubRoot = apiBase.replace(/\/api\/?$/i, "");
    const hubUrl = `${hubRoot}/hubs/tickets`;

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => localStorage.getItem("access_token") || "",
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();

    const handleReceiveReply = (reply) => {
      setData((prev) => {
        if (!prev) return prev;

        const list = prev.replies || [];
        if (list.some((x) => x.replyId === reply.replyId)) return prev;

        let incoming = reply;
        if (incoming && !incoming.senderAvatarUrl && incoming.senderId) {
          const cached = list.find(
            (x) => x.senderId === incoming.senderId && x.senderAvatarUrl
          )?.senderAvatarUrl;
          if (cached) incoming = { ...incoming, senderAvatarUrl: cached };
        }

        return {
          ...prev,
          replies: [...list, incoming],
        };
      });
    };

    connection.on("ReceiveReply", handleReceiveReply);

    connection
      .start()
      .then(() => connection.invoke("JoinTicketGroup", id))
      .catch(() => { });

    return () => {
      connection
        .invoke("LeaveTicketGroup", id)
        .catch(() => { })
        .finally(() => {
          connection.off("ReceiveReply", handleReceiveReply);
          connection.stop().catch(() => { });
        });
    };
  }, [id]);

  const handleMessagesScroll = () => {
    const el = messagesRef.current;
    if (!el) return;
    const threshold = 20;
    const distanceToBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    isAtBottomRef.current = distanceToBottom <= threshold;
  };

  useEffect(() => {
    const el = messagesRef.current;
    if (!el) return;
    const scrollToBottom = () => {
      el.scrollTop = el.scrollHeight;
    };

    if (!initialScrollDoneRef.current) {
      scrollToBottom();
      initialScrollDoneRef.current = true;
      isAtBottomRef.current = true;
      return;
    }

    if (isAtBottomRef.current) {
      scrollToBottom();
    }
  }, [data?.replies]);

  const actions = useMemo(() => {
    const s = normalizeStatus(data?.status);

    // ✅ dùng normalize assignment state để điều kiện luôn đúng dù BE trả khác value
    const asn = getAssignmentStateFromTicket(data);

    return {
      canAssign: s === "New" || (s === "InProgress" && asn === "Unassigned"),
      canClose: s === "New",

      canComplete: s === "InProgress",
      canTransfer: s === "InProgress" && (asn === "Assigned" || asn === "Technical"),
    };
  }, [data]);

  const canReply = useMemo(() => {
    const s = normalizeStatus(data?.status);
    return s === "New" || s === "InProgress";
  }, [data?.status]);

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
      alert(e?.response?.data?.message || e.message || "Chuyển hỗ trợ thất bại.");
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
      await ticketsApi.reply(id, { message: msg, sendEmail });

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

  // ✅ assignment state đã normalize để hiển thị tiếng Việt chắc chắn
  const asnNorm = getAssignmentStateFromTicket(data);

  const customerOrdersRaw = Array.isArray(data.customerOrders) ? data.customerOrders : [];
  const customerOrders = [...customerOrdersRaw].sort((a, b) => {
    const ta = parseApiDateAssumeUtcIfNoTz(a?.createdAt)?.getTime() || 0;
    const tb = parseApiDateAssumeUtcIfNoTz(b?.createdAt)?.getTime() || 0;
    return tb - ta;
  });

  return (
    <div className="tkd-page">
      <div className="ticket-header">
        <div className="left">
          <div className="code">
            Mã: <strong>{data.ticketCode}</strong>
          </div>
          <h3 className="subject">{data.subject}</h3>

          {data.description && (
            <div className="ticket-desc">{data.description}</div>
          )}

          <div className="meta">
            {/* ✅ đổi sang StatusBadge để có màu theo trạng thái */}
            <StatusBadge value={data.status} />

            <span className="chip">
              {MAP_SEV[data.severity] || data.severity}
            </span>
            <span className="chip">
              {MAP_SLA[data.slaStatus] || data.slaStatus}
            </span>

            {/* ✅ luôn hiển thị 1 trong 3: Chưa gán / Đã gán / Đã chuyển */}
            <span className="chip">{MAP_ASN[asnNorm] || "Chưa gán"}</span>

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
        <div className="left-col">
          <div className="thread">
            <div className="thread-title">Lịch sử trao đổi</div>

            <div
              className="thread-messages"
              ref={messagesRef}
              onScroll={handleMessagesScroll}
            >
              {(data.replies || []).length === 0 && (
                <div className="empty small">Chưa có trao đổi nào.</div>
              )}

              {(data.replies || []).map((r) => {
                const isStaff = !!r.isStaffReply;
                const isCustomerMsg = !isStaff;

                const isRightSide = isCustomerView ? isCustomerMsg : isStaff;
                const sender = r.senderName || "Không rõ";

                return (
                  <div
                    key={r.replyId || r.id}
                    className={`msg ${isRightSide ? "msg-me" : "msg-other"}`}
                  >
                    <ChatAvatar name={sender} avatarUrl={r.senderAvatarUrl} />

                    <div className="bubble">
                      <div className="head">
                        <span className="name">
                          {sender}
                          {isStaff && <span className="staff-tag">Staff</span>}
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

            {canReply && (
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
                {replyError && <div className="reply-error">{replyError}</div>}

                <div className="reply-footer">
                  <div className="left">{/* giữ nguyên */}</div>
                  <div className="right">
                    <button type="button" className="btn ghost" onClick={handleSaveDraft}>
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
            )}
          </div>
        </div>

        <div className="right-col">
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
            <div className="kv">
              <span className="k">Mức ưu tiên</span>
              <span className="v">{fmtPriority(data.priorityLevel)}</span>
            </div>
          </div>

          <div className="card">
            <div className="card-title">Thông tin SLA</div>
            <div className="kv">
              <span className="k">Hạn phản hồi đầu tiên</span>
              <span className="v">
                {data.firstResponseDueAt ? fmtDateTime(data.firstResponseDueAt) : "-"}
              </span>
            </div>
            <div className="kv">
              <span className="k">Phản hồi đầu tiên lúc</span>
              <span className="v">
                {data.firstRespondedAt ? fmtDateTime(data.firstRespondedAt) : "-"}
              </span>
            </div>
            <div className="kv">
              <span className="k">Hạn xử lý hoàn tất</span>
              <span className="v">
                {data.resolutionDueAt ? fmtDateTime(data.resolutionDueAt) : "-"}
              </span>
            </div>
            <div className="kv">
              <span className="k">Hoàn tất lúc</span>
              <span className="v">{data.resolvedAt ? fmtDateTime(data.resolvedAt) : "-"}</span>
            </div>
          </div>

          <div className="card">
            <div className="card-title">Thông tin nhân viên</div>

            {/* ✅ luôn hiển thị trạng thái gán (3 trạng thái VN) */}
            <div className="kv">
              <span className="k">Trạng thái</span>
              <span className="v">{MAP_ASN[asnNorm] || "Chưa gán"}</span>
            </div>

            {data.assigneeName || data.assigneeEmail ? (
              <>
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

          <div className="card">
            <div className="card-title">Đơn hàng gần nhất</div>

            {customerOrders.length === 0 && (
              <div className="empty small">Khách hàng chưa có đơn hàng.</div>
            )}

            {customerOrders.length > 0 && (
              <div className="related-list">
                {customerOrders.map((o) => {
                  const amount = (o.finalAmount ?? o.totalAmount);
                  const amountText =
                    typeof amount === "number"
                      ? amount.toLocaleString("vi-VN", { style: "currency", currency: "VND" })
                      : "-";

                  return (
                    <div key={o.orderId} className="related-item">
                      <div className="ri-main">
                        <div className="ri-line1">
                          <span className="ri-code">#{o.orderNumber || o.orderId}</span>
                          <span className="ri-dot">•</span>
                          <span className="ri-time">{fmtDateTime(o.createdAt)}</span>
                        </div>

                        <div className="ri-subject" title={amountText}>
                          Tổng tiền: {amountText}
                        </div>

                        <div className="ri-meta">
                          <OrderStatusPill value={o.status} />
                        </div>
                      </div>

                      <div className="ri-actions">
                        <button
                          className="btn xs ghost"
                          onClick={() => nav(`/admin/orders/${o.orderId}`)}
                          title="Xem chi tiết đơn hàng"
                        >
                          Chi tiết
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          <div className="panel related">
            <div className="panel-title">Ticket liên quan</div>
            {(relatedTickets || []).length === 0 && (
              <div className="empty small">Không có ticket nào khác của khách hàng này.</div>
            )}

            <div className="related-list">
              {(relatedTickets || []).map((t) => (
                <div key={t.ticketId} className="related-item">
                  <div className="ri-main">
                    <div className="ri-line1">
                      <span className="ri-code">#{t.ticketCode}</span>
                      <span className="ri-dot">•</span>
                      <span className="ri-time">{fmtDateTime(t.createdAt)}</span>
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

      <AssignModal
        open={modal.open}
        title={modal.mode === "transfer" ? "Chuyển hỗ trợ" : "Gán nhân viên phụ trách"}
        excludeUserId={modal.excludeUserId}
        onClose={() => setModal({ open: false, mode: "", excludeUserId: null })}
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
              placeholder="Nhập từ khóa..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <div className="staff-list">
            {loading && <div className="empty small">Đang tải...</div>}
            {!loading && (!list || list.length === 0) && (
              <div className="empty small">Không có nhân viên phù hợp.</div>
            )}
            {!loading && list && list.length > 0 && (
              <ul className="staff-ul">
                {list.map((u) => (
                  <li
                    key={u.id}
                    className={"staff-item" + (selected === u.id ? " selected" : "")}
                    onClick={() => setSelected(u.id)}
                  >
                    <span className="staff-info">
                      <span className="staff-name">{u.name}</span>
                      <span className="staff-email">{u.email}</span>
                    </span>
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
            onClick={() => selected && onConfirm(selected)}
          >
            Xác nhận
          </button>
        </div>
      </div>
    </div>,
    document.body
  );
}
