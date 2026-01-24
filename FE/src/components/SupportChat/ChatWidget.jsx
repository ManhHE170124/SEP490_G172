// File: src/components/SupportChat/ChatWidget.jsx
import React, { useEffect, useRef, useState, useMemo } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import axiosClient from "../../api/axiosClient";
import { supportChatApi } from "../../api/supportChatApi";
import "./support-chat-widget.css";

function formatTime(value) {
  if (!value) return "";
  try {
    const DISPLAY_TZ = "Asia/Bangkok"; // UTC+7 (FE only)

    const hasTimeZoneDesignator = (s) =>
      /[zZ]$/.test(s) ||
      /[+\-]\d{2}:\d{2}$/.test(s) ||
      /[+\-]\d{2}\d{2}$/.test(s);

    let d = null;

    if (value instanceof Date) {
      d = value;
    } else if (typeof value === "number") {
      d = new Date(value);
    } else {
      let s = String(value).trim();
      if (!s) return "";

      // .NET đôi khi trả fractional seconds 7 digits (vd: .1234567) => JS có thể parse lỗi
      // Trim về tối đa 3 digits để chắc chắn parse được.
      s = s.replace(/(\.\d{3})\d+/, "$1");

      // Nếu API/DB trả "2026-01-24T01:23:45" (không Z/offset) => coi là UTC
      const iso = hasTimeZoneDesignator(s) ? s : `${s}Z`;
      d = new Date(iso);
    }

    if (!d || Number.isNaN(d.getTime())) return String(value);

    // Luôn format theo UTC+7 để hiển thị nhất quán (kể cả sau reload)
    return new Intl.DateTimeFormat("vi-VN", {
      timeZone: DISPLAY_TZ,
      hour: "2-digit",
      minute: "2-digit",
    }).format(d);
  } catch {
    return String(value);
  }
}

// --- Helpers normalize từ API / SignalR (camelCase & PascalCase) ---
function normalizeSession(raw) {
  if (!raw) return null;
  return {
    chatSessionId: raw.chatSessionId || raw.ChatSessionId,
    status: raw.status || raw.Status || "",
    assignedStaffName: raw.assignedStaffName || raw.AssignedStaffName || "",
  };
}

function normalizeMessage(raw) {
  if (!raw) return null;
  return {
    messageId: raw.messageId || raw.MessageId,
    chatSessionId: raw.chatSessionId || raw.ChatSessionId,
    isFromStaff:
      typeof raw.isFromStaff === "boolean" ? raw.isFromStaff : !!raw.IsFromStaff,
    senderName: raw.senderName || raw.SenderName || "",
    content: raw.content || raw.Content || "",
    sentAt: raw.sentAt || raw.SentAt || null,
  };
}

export default function ChatWidget() {
  const [open, setOpen] = useState(false);

  const [session, setSession] = useState(null);
  const [messages, setMessages] = useState([]);

  const [loadingSession, setLoadingSession] = useState(false);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [error, setError] = useState("");

  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);

  const connRef = useRef(null);
  const messagesRef = useRef(null);
  const isAtBottomRef = useRef(true);
  const autoScrollRef = useRef(false); // <-- quyết định lần render tới có auto scroll không

  const toggleOpen = () => {
    setOpen((prev) => !prev);
  };

  const scrollToBottom = (force = false) => {
    const el = messagesRef.current;
    if (!el) return;
    if (!force && !isAtBottomRef.current) return;
    el.scrollTop = el.scrollHeight;
    isAtBottomRef.current = true;
  };

  const handleMessagesScroll = () => {
    const el = messagesRef.current;
    if (!el) return;
    const threshold = 20;
    const distanceToBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    isAtBottomRef.current = distanceToBottom <= threshold;
  };

  // Khi số lượng message thay đổi & autoScrollRef đang bật → kéo xuống đáy
  useEffect(() => {
    if (autoScrollRef.current) {
      scrollToBottom(true);
      autoScrollRef.current = false;
    }
  }, [messages.length]);

  // -------- Khởi tạo session khi mở widget --------
  useEffect(() => {
    if (!open) {
      setError("");
      return;
    }

    let cancelled = false;

    async function initSession() {
      if (session && session.chatSessionId) {
        await loadMessages(session.chatSessionId, { silent: true });
        return;
      }

      setLoadingSession(true);
      setError("");

      try {
        const raw = await supportChatApi.openOrGet();
        if (cancelled) return;

        const s = normalizeSession(raw) || raw;
        if (!s.chatSessionId && raw.ChatSessionId) {
          s.chatSessionId = raw.ChatSessionId;
        }

        setSession(s);
        autoScrollRef.current = true; // load lịch sử → luôn kéo xuống
        await loadMessages(s.chatSessionId, { silent: false, force: true });
      } catch (err) {
        if (cancelled) return;

        console.error("init chat widget failed", err);
        const res = err?.response;
        if (res?.status === 401) {
          setError("Bạn cần đăng nhập để sử dụng chat hỗ trợ.");
        } else {
          setError(
            res?.data?.message ||
              "Không thể khởi tạo chat hỗ trợ. Vui lòng thử lại sau."
          );
        }
      } finally {
        if (!cancelled) {
          setLoadingSession(false);
        }
      }
    }

    initSession();

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  // -------- Load messages của 1 session --------
  async function loadMessages(chatSessionId, opts = {}) {
    if (!chatSessionId) return;
    const { silent = false, force = false } = opts;

    if (!silent) {
      setLoadingMessages(true);
    }

    try {
      const res = await supportChatApi.getMessages(chatSessionId);
      const items = Array.isArray(res) ? res : res?.items ?? res?.Items ?? [];

      const mapped = items.map(normalizeMessage).filter(Boolean);

      // Lần load lịch sử (force) → auto scroll
      if (force) {
        autoScrollRef.current = true;
      }

      setMessages((prev) => {
        if (!force && prev && prev.length > 0) {
          return prev;
        }
        return mapped;
      });
    } catch (err) {
      console.error("load chat messages failed", err);
      if (!silent) {
        setError(
          err?.response?.data?.message ||
            "Không tải được lịch sử chat. Vui lòng thử lại."
        );
      }
    } finally {
      if (!silent) {
        setLoadingMessages(false);
      }
    }
  }

  // -------- Kết nối SignalR khi đã có sessionId + widget đang mở --------
  useEffect(() => {
    if (!open) return;
    if (!session || !session.chatSessionId) return;

    if (connRef.current) {
      try {
        connRef.current.stop().catch(() => {});
      } catch {
      } finally {
        connRef.current = null;
      }
    }

    let disposed = false;

    let apiBase = axiosClient?.defaults?.baseURL || "";
    if (!apiBase) {
      apiBase =
        process.env.REACT_APP_API_URL ||
        (typeof import.meta !== "undefined" &&
          import.meta.env &&
          import.meta.env.VITE_API_BASE_URL) ||
        "https://localhost:7292/api";
    }

    let hubBase = apiBase.replace(/\/api\/?$/, "");
    // ✅ Khớp BE: MapHub<SupportChatHub>("/hubs/support-chat")
    const hubUrl = `${hubBase}/hubs/support-chat`;

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => {
          try {
            const raw =
              localStorage.getItem("access_token") ||
              localStorage.getItem("token") ||
              "";
            return raw.replace(/^"|"$/g, "");
          } catch {
            return "";
          }
        },
      })
      .configureLogging(LogLevel.Information)
      .withAutomaticReconnect()
      .build();

    connRef.current = connection;

    const currentSessionId = session.chatSessionId;

    const handleSupportMessage = (raw) => {
      const msg = normalizeMessage(raw);
      if (!msg || msg.chatSessionId !== currentSessionId) return;

      // Tin đến từ SignalR:
      // nếu đang ở đáy → auto scroll; nếu đang cuộn lên → KHÔNG kéo
      autoScrollRef.current = isAtBottomRef.current;

      setMessages((prev) => {
        const list = prev || [];
        if (msg.messageId && list.some((x) => x.messageId === msg.messageId)) {
          return prev;
        }
        return [...list, msg];
      });
    };

    const handleSessionUpdated = (raw) => {
      const updated = normalizeSession(raw);
      if (!updated || updated.chatSessionId !== currentSessionId) return;

      // ✅ Cập nhật session → statusText sẽ re-render
      setSession((prev) => ({
        ...(prev || {}),
        ...raw,
        ...updated,
      }));
    };

    connection.on("SupportMessageReceived", handleSupportMessage);
    connection.on("ReceiveSupportMessage", handleSupportMessage);
    connection.on("SupportSessionUpdated", handleSessionUpdated);

    connection
      .start()
      .then(() =>
        connection
          // ✅ Join đúng session
          .invoke("JoinSession", currentSessionId)
          .catch(() => {})
      )
      .catch(() => {});

    return () => {
      disposed = true;
      if (!connection) return;

      connection
        .invoke("LeaveSession", currentSessionId)
        .catch(() => {})
        .finally(() => {
          connection.off("SupportMessageReceived", handleSupportMessage);
          connection.off("ReceiveSupportMessage", handleSupportMessage);
          connection.off("SupportSessionUpdated", handleSessionUpdated);
          connection.stop().catch(() => {});
          if (!disposed && connRef.current === connection) {
            connRef.current = null;
          }
        });
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, session && session.chatSessionId]);

  // -------- Gửi tin nhắn --------
  async function handleSend(e) {
    e.preventDefault();
    if (!session || !session.chatSessionId) return;
    const text = input.trim();
    if (!text) return;

    setSending(true);
    setError("");

    try {
      const raw = await supportChatApi.postMessage(session.chatSessionId, {
        content: text,
      });

      const saved = normalizeMessage(raw) || raw;

      setInput("");

      // Tin nhắn của mình → luôn kéo xuống đáy
      autoScrollRef.current = true;

      setMessages((prev) => {
        const list = prev || [];
        if (
          saved &&
          saved.messageId &&
          list.some((x) => x.messageId === saved.messageId)
        ) {
          return prev;
        }
        return [...list, saved];
      });
    } catch (err) {
      console.error("send chat message failed", err);
      setError(
        err?.response?.data?.message ||
          "Không gửi được tin nhắn. Vui lòng thử lại."
      );
    } finally {
      setSending(false);
    }
  }

  const statusText = useMemo(() => {
    if (!session) return "";
    const status = String(session.status || session.Status || "").toLowerCase();
    const assignedStaffName =
      session.assignedStaffName || session.AssignedStaffName || "";

    // Chưa có NV hoặc status waiting → vẫn đang chờ
    if (status === "waiting" || (!assignedStaffName && status !== "closed")) {
      return "Đang chờ kết nối nhân viên…";
    }

    if (status === "open" || status === "active") {
      // ✅ Khi SignalR update status/assignedStaffName → text này hiện ra
      return "Đã kết nối với CSKH.";
    }

    if (status === "closed") {
      return "Phiên chat đã kết thúc.";
    }
    return "";
  }, [session]);

  const canSend =
    session &&
    String(session.status || session.Status || "").toLowerCase() !== "closed";

  return (
    <div className="support-chat-widget">
      {/* Nút mở widget */}
      {!open && (
        <button
          type="button"
          className="support-chat-toggle-btn"
          onClick={toggleOpen}
        >
          💬 Hỗ trợ
        </button>
      )}

      {/* Hộp chat */}
      {open && (
        <div className="support-chat-panel">
          <div className="support-chat-header">
            <div className="title">
              <strong>Hỗ trợ trực tuyến</strong>
              {statusText && (
                <div className="status-text">
                  <small>{statusText}</small>
                </div>
              )}
            </div>
            <button
              type="button"
              className="close-btn"
              onClick={toggleOpen}
              aria-label="Đóng"
            >
              ×
            </button>
          </div>

          <div className="support-chat-body">
            {loadingSession && (
              <div className="state-text">Đang khởi tạo phiên chat…</div>
            )}
            {!loadingSession && !session && (
              <div className="state-text">
                Không thể khởi tạo chat. Vui lòng thử lại.
              </div>
            )}

            {error && <div className="error-text">{error}</div>}

            <div
              className="messages-container"
              ref={messagesRef}
              onScroll={handleMessagesScroll}
            >
              {loadingMessages && messages.length === 0 && (
                <div className="state-text">Đang tải lịch sử chat…</div>
              )}

              {messages.map((m) => {
                const isMine = !m.isFromStaff && !m.IsFromStaff;
                const time = formatTime(m.sentAt || m.SentAt);
                const content = m.content || m.Content || "";
                const key =
                  m.messageId || m.MessageId || `${time}-${content}-${isMine}`;

                return (
                  <div
                    key={key}
                    className={`msg-row ${isMine ? "mine" : "theirs"}`}
                  >
                    <div className="msg-bubble">
                      <div className="msg-content">{content}</div>
                      <div className="msg-meta">
                        {/* Không hiển thị tên nhân viên, chỉ hiện "Bạn" cho tin của mình */}
                        {isMine && <span className="sender">Bạn</span>}
                        {time && <span className="time">{time}</span>}
                      </div>
                    </div>
                  </div>
                );
              })}

              {!loadingMessages && messages.length === 0 && session && (
                <div className="state-text">
                  Bắt đầu cuộc trò chuyện với nhân viên hỗ trợ…
                </div>
              )}
            </div>
          </div>

          <form className="support-chat-footer" onSubmit={handleSend}>
            <input
              type="text"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder={
                canSend ? "Nhập tin nhắn của bạn..." : "Phiên chat đã kết thúc."
              }
              disabled={!canSend || sending}
            />
            <button type="submit" disabled={!canSend || sending || !input.trim()}>
              Gửi
            </button>
          </form>
        </div>
      )}
    </div>
  );
}
