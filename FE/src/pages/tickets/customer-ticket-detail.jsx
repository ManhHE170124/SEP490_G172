// File: src/pages/tickets/customer-ticket-detail.jsx
import React, { useEffect, useState, useMemo, useRef } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { ticketsApi } from "../../api/ticketsApi";
import axiosClient from "../../api/axiosClient";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import "../../styles/customer-ticket-detail.css";

const MAP_STATUS = {
  New: "Mới",
  InProgress: "Đang xử lý",
  Completed: "Đã hoàn thành",
  Closed: "Đã đóng",
};

const MAP_SLA = {
  OK: "Đúng hạn",
  Warning: "Cảnh báo",
  Overdue: "Quá hạn",
};

const MAP_ASN = {
  Unassigned: "Chưa gán",
  Assigned: "Đã gán",
  Technical: "Đã chuyển",
};

const MAP_PRIORITY = {
  0: "Tiêu chuẩn",
  1: "Ưu tiên",
  2: "VIP",
};

function fmtDateTime(value) {
  if (!value) return "";
  try {
    const d =
      typeof value === "string" || typeof value === "number"
        ? new Date(value)
        : value;
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

function normalizeStatus(status) {
  const v = String(status || "").toLowerCase();
  if (v === "open" || v === "new") return "New";
  if (["processing", "inprogress", "in_process"].includes(v)) return "InProgress";
  if (["done", "resolved", "completed"].includes(v)) return "Completed";
  if (v === "closed" || v === "close") return "Closed";
  return "New";
}

function isTicketClosed(status) {
  const v = normalizeStatus(status);
  return v === "Completed" || v === "Closed";
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

function StatusPill({ value }) {
  const v = normalizeStatus(value);
  const text = MAP_STATUS[v] || v || "-";
  const key =
    v === "New"
      ? "new"
      : v === "InProgress"
        ? "processing"
        : v === "Completed"
          ? "completed"
          : "closed";
  return <span className={`ctd-pill ctd-pill-status-${key}`}>{text}</span>;
}

// Đã giữ helper SlaPill để không phá vỡ cấu trúc code,
// nhưng KHÔNG sử dụng ở UI nữa theo yêu cầu (không hiển thị trạng thái SLA).
function SlaPill({ value }) {
  const v = String(value || "");
  const text = MAP_SLA[v] || v || "-";
  const key =
    v === "OK" ? "ok" : v === "Overdue" ? "overdue" : v ? "warning" : "none";
  return <span className={`ctd-pill ctd-pill-sla-${key}`}>{text}</span>;
}

export default function CustomerTicketDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [ticket, setTicket] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");

  const [replyText, setReplyText] = useState("");
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState("");

  const messagesRef = useRef(null);
  const isAtBottomRef = useRef(true);
  const initialScrollDoneRef = useRef(false);

  // Load ticket detail lần đầu / khi đổi id
  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setLoadError("");
      try {
        const data = await ticketsApi.customerDetail(id);
        if (!cancelled) {
          setTicket(data);
          // KHÔNG scroll ở đây nữa – để useEffect [ticket?.replies] xử lý
        }
      } catch (err) {
        console.error("Failed to load ticket detail", err);
        if (!cancelled) {
          // If 403 Forbidden, show as NotFound (Ticket không tồn tại)
          if (err?.response?.status === 403) {
            setLoadError("Ticket không tồn tại.");
          } else {
            setLoadError(
              err?.response?.data?.message ||
              "Không tải được thông tin ticket. Vui lòng thử lại."
            );
          }
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    if (id) {
      load();
    }

    return () => {
      cancelled = true;
    };
  }, [id]);

  // ===== SignalR: lắng nghe tin nhắn mới (ReceiveReply) =====
  useEffect(() => {
    if (!id) return;

    // base URL giống axiosClient / admin-ticket-detail
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
      setTicket((prev) => {
        if (!prev) return prev;
        const list = prev.replies || [];

        // Dedupe theo replyId / id để tránh trùng khi vừa append từ REST vừa nhận qua SignalR
        const incomingId = reply?.replyId ?? reply?.id ?? null;
        if (
          incomingId !== null &&
          list.some((x) => (x.replyId ?? x.id) === incomingId)
        ) {
          return prev;
        }

        const next = {
          ...prev,
          replies: [...list, reply],
        };

        // KHÔNG scroll ở đây – để useEffect [ticket?.replies] xử lý theo isAtBottomRef
        return next;
      });
    };

    connection.on("ReceiveReply", handleReceiveReply);

    connection
      .start()
      .then(() => connection.invoke("JoinTicketGroup", id))
      .catch(() => {
        // Có thể log nếu cần, nhưng không làm crash UI
      });

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

  // 🧷 Theo dõi scroll trong khung chat để biết người dùng đang ở đáy hay không
  const handleMessagesScroll = () => {
    const el = messagesRef.current;
    if (!el) return;
    const threshold = 20; // px – cho phép lệch chút vẫn coi như ở đáy
    const distanceToBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    isAtBottomRef.current = distanceToBottom <= threshold;
  };

  // 🧷 Auto scroll:
  //  - Lần load đầu: luôn kéo xuống cuối
  //  - Sau đó: chỉ auto scroll nếu đang ở cuối
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
  }, [ticket?.replies]);

  const canReply = useMemo(() => {
    if (!ticket) return false;
    const s = normalizeStatus(ticket.status);
    return s === "New" || s === "InProgress";
  }, [ticket]);

  async function handleSendReply(e) {
    e.preventDefault();
    if (!ticket) return;

    const msg = replyText.trim();
    if (!msg) {
      setSendError("Vui lòng nhập nội dung phản hồi.");
      return;
    }

    setSending(true);
    setSendError("");

    try {
      const createdReply = await ticketsApi.reply(ticket.ticketId || id, {
        message: msg,
      });

      setReplyText("");

      // Append tin nhắn mới vào list, nhưng có kiểm tra trùng để không bị double
      setTicket((prev) => {
        if (!prev) return prev;
        const list = prev.replies || [];

        const newId = createdReply?.replyId ?? createdReply?.id ?? null;
        if (
          newId !== null &&
          list.some((r) => (r.replyId ?? r.id) === newId)
        ) {
          // Reply này đã được SignalR đẩy vào trước rồi
          return prev;
        }

        const next = {
          ...prev,
          replies: [...list, createdReply],
        };

        // KHÔNG scroll ở đây – để useEffect [ticket?.replies] xử lý theo isAtBottomRef
        return next;
      });
    } catch (err) {
      console.error("Failed to send reply", err);
      setSendError(
        err?.response?.data?.message ||
        "Không gửi được phản hồi. Vui lòng thử lại."
      );
    } finally {
      setSending(false);
    }
  }

  if (loading) {
    return (
      <div className="ctd-page">
        <div className="ctd-state">Đang tải thông tin ticket...</div>
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="ctd-page">
        <div className="ctd-state ctd-state-error">{loadError}</div>
        <div className="ctd-top-actions">
          <button
            type="button"
            className="ctd-btn-secondary"
            onClick={() => navigate("/tickets")}
          >
            Quay lại danh sách ticket
          </button>
        </div>
      </div>
    );
  }

  if (!ticket) {
    return (
      <div className="ctd-page">
        <div className="ctd-state ctd-state-error">
          Không tìm thấy thông tin ticket.
        </div>
        <div className="ctd-top-actions">
          <button
            type="button"
            className="ctd-btn-secondary"
            onClick={() => navigate("/tickets")}
          >
            Quay lại danh sách ticket
          </button>
        </div>
      </div>
    );
  }

  const closed = isTicketClosed(ticket.status);

  return (
    <div className="ctd-page">
      <div className="ctd-top-actions">
        <button
          type="button"
          className="ctd-link-back"
          onClick={() => navigate("/tickets")}
        >
          &laquo; Quay lại danh sách ticket
        </button>
        <Link to="/tickets/create" className="ctd-link-create">
          Tạo ticket mới
        </Link>
      </div>

      {/* HEADER – KHÔNG hiển thị SLA, chỉ trạng thái + timestamp */}
      <div className="ctd-header">
        <div className="ctd-header-left">
          <div className="ctd-code">
            Mã ticket: <strong>{ticket.ticketCode}</strong>
          </div>
          <h1 className="ctd-subject">
            {ticket.subject || "Ticket hỗ trợ khách hàng"}
          </h1>

          {ticket.description && (
            <div className="ctd-desc">{ticket.description}</div>
          )}

          <div className="ctd-meta">
            <StatusPill value={ticket.status} />
            {/* ĐÃ BỎ SlaPill theo yêu cầu: không hiển thị trạng thái SLA */}
            <span className="ctd-meta-text">
              Tạo lúc: {fmtDateTime(ticket.createdAt)}
            </span>
            {ticket.updatedAt && (
              <span className="ctd-meta-text">
                Cập nhật: {fmtDateTime(ticket.updatedAt)}
              </span>
            )}
          </div>
        </div>
      </div>

      <div className="ctd-layout">
        {/* Cột trái: lịch sử trao đổi + form phản hồi */}
        <div className="ctd-left">
          <div className="ctd-thread">
            <div className="ctd-thread-title">Lịch sử trao đổi</div>
            <div
              className="ctd-thread-messages"
              ref={messagesRef}
              onScroll={handleMessagesScroll}
            >
              {(!ticket.replies || ticket.replies.length === 0) && (
                <div className="ctd-empty">Chưa có trao đổi nào.</div>
              )}

              {(ticket.replies || []).map((reply) => {
                const isStaff =
                  reply?.isStaffReply ??
                  reply?.isFromStaff ??
                  reply?.isStaff ??
                  reply?.fromStaff ??
                  false;
                const isMe = !isStaff;

                const rawSenderName =
                  reply?.senderName ||
                  reply?.senderFullName ||
                  reply?.customerName ||
                  "";

                // 👇 Logic theo yêu cầu:
                // - Nếu là nhân viên → luôn hiển thị "Nhân viên hỗ trợ"
                // - Ngược lại → dùng tên thật (hoặc fallback "Bạn")
                const senderName = isStaff ? "Nhân viên hỗ trợ" : rawSenderName || "Bạn";

                const timeValue = reply?.sentAt || reply?.createdAt;

                const firstChar = (rawSenderName || "?").charAt(0).toUpperCase();

                return (
                  <div
                    key={reply.replyId || reply.id}
                    className={`ctd-msg ${isMe ? "ctd-msg-me" : "ctd-msg-other"
                      }`}
                  >
                    <div className="ctd-msg-avatar">{firstChar}</div>
                    <div className="ctd-msg-bubble">
                      <div className="ctd-msg-head">
                        <span className="ctd-msg-name">
                          {senderName}
                        </span>
                        <span className="ctd-msg-time">
                          {fmtDateTime(timeValue)}
                        </span>
                      </div>
                      <div className="ctd-msg-text">
                        {reply.message || ""}
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>

            <div className="ctd-reply-box">
              {closed || !canReply ? (
                <div className="ctd-alert-info">
                  Ticket đã được xử lý xong, bạn không thể gửi thêm phản hồi.
                  Nếu vẫn còn vấn đề, vui lòng tạo ticket mới.
                </div>
              ) : (
                <form onSubmit={handleSendReply}>
                  <div className="ctd-reply-title">
                    Gửi phản hồi cho nhân viên hỗ trợ
                  </div>
                  <textarea
                    className="ctd-reply-textarea"
                    rows={4}
                    value={replyText}
                    onChange={(e) => {
                      setReplyText(e.target.value);
                      if (sendError) setSendError("");
                    }}
                    placeholder="Nhập nội dung bạn muốn gửi cho nhân viên hỗ trợ..."
                  />
                  {sendError && (
                    <div className="ctd-reply-error">{sendError}</div>
                  )}
                  <div className="ctd-reply-footer">
                    <span className="ctd-reply-hint">
                      Vui lòng không chia sẻ mật khẩu hay thông tin nhạy cảm.
                    </span>
                    <button
                      type="submit"
                      className="ctd-btn-primary"
                      disabled={sending}
                    >
                      {sending ? "Đang gửi..." : "Gửi phản hồi"}
                    </button>
                  </div>
                </form>
              )}
            </div>
          </div>
        </div>

        {/* Cột phải: chỉ tóm tắt ticket – KHÔNG SLA, KHÔNG nhân viên phụ trách, KHÔNG panel ticket khác */}
        <div className="ctd-right">
          <div className="ctd-card">
            <div className="ctd-card-title">Thông tin ticket</div>
            <div className="ctd-kv">
              <span className="ctd-k">Trạng thái</span>
              <span className="ctd-v">
                {MAP_STATUS[normalizeStatus(ticket.status)] || ticket.status}
              </span>
            </div>
            <div className="ctd-kv">
              <span className="ctd-k">Mức ưu tiên</span>
              <span className="ctd-v">{fmtPriority(ticket.priorityLevel)}</span>
            </div>
            <div className="ctd-kv">
              <span className="ctd-k">Tạo lúc</span>
              <span className="ctd-v">{fmtDateTime(ticket.createdAt)}</span>
            </div>
            {ticket.updatedAt && (
              <div className="ctd-kv">
                <span className="ctd-k">Cập nhật</span>
                <span className="ctd-v">{fmtDateTime(ticket.updatedAt)}</span>
              </div>
            )}
          </div>

          {/* ĐÃ BỎ HOÀN TOÀN panel "Ticket khác của bạn" */}
        </div>
      </div>
    </div>
  );
}
