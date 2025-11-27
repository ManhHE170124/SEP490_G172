// File: src/components/SupportChat/ChatWidget.jsx
import React, { useEffect, useRef, useState, useMemo } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import axiosClient from "../../api/axiosClient";
import { supportChatApi } from "../../api/supportChatApi";
// ✅ THÊM DÒNG NÀY
import "./support-chat-widget.css";

function formatTime(value) {
  if (!value) return "";
  try {
    const d = new Date(value);
    return d.toLocaleTimeString("vi-VN", {
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return String(value);
  }
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

  const toggleOpen = () => {
    setOpen((prev) => !prev);
  };

  const scrollToBottom = (force = false) => {
    const el = messagesRef.current;
    if (!el) return;
    if (!force && !isAtBottomRef.current) return;
    el.scrollTop = el.scrollHeight;
  };

  const handleMessagesScroll = () => {
    const el = messagesRef.current;
    if (!el) return;
    const threshold = 20;
    const distanceToBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    isAtBottomRef.current = distanceToBottom <= threshold;
  };

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
        const s = await supportChatApi.openOrGet();
        if (cancelled) return;

        setSession(s);
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
      const items = Array.isArray(res)
        ? res
        : res?.items ?? res?.Items ?? [];

      setMessages((prev) => {
        if (!force && prev && prev.length > 0) {
          return prev;
        }
        return items;
      });

      setTimeout(() => scrollToBottom(true), 0);
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
    const hubUrl = `${hubBase}/supportChatHub`;

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

    const handleSupportMessage = (msg) => {
      if (!msg || msg.chatSessionId !== session.chatSessionId) return;

      setMessages((prev) => {
        const list = prev || [];
        if (
          msg.messageId &&
          list.some((x) => x.messageId === msg.messageId)
        ) {
          return prev;
        }
        const next = [...list, msg];
        return next;
      });

      if (isAtBottomRef.current) {
        setTimeout(() => scrollToBottom(true), 0);
      }
    };

    const handleSessionUpdated = (item) => {
      if (!item || item.chatSessionId !== session.chatSessionId) return;
      setSession((prev) => ({
        ...(prev || {}),
        ...item,
      }));
    };

    connection.on("SupportMessageReceived", handleSupportMessage);
    connection.on("ReceiveSupportMessage", handleSupportMessage);
    connection.on("SupportSessionUpdated", handleSessionUpdated);

    connection
      .start()
      .then(() =>
        connection
          .invoke("JoinSupportSessionGroup", session.chatSessionId)
          .catch(() => {})
      )
      .catch(() => {});

    return () => {
      disposed = true;
      if (!connection) return;

      connection
        .invoke("LeaveSupportSessionGroup", session.chatSessionId)
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
      const saved = await supportChatApi.postMessage(session.chatSessionId, {
        content: text,
      });

      setInput("");
      setMessages((prev) => {
        const list = prev || [];
        if (
          saved &&
          saved.messageId &&
          list.some((x) => x.messageId === saved.messageId)
        ) {
          return prev;
        }
        const next = [...list, saved];
        return next;
      });
      setTimeout(() => scrollToBottom(true), 0);
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
    const staffName =
      session.assignedStaffName ||
      session.AssignedStaffName ||
      "nhân viên hỗ trợ";

    if (
      status === "waiting" ||
      (!session.assignedStaffName && status !== "closed")
    ) {
      return "Đang chờ kết nối nhân viên…";
    }
    if (status === "open" || status === "active") {
      return `Đang chat với ${staffName}`;
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
                const senderName =
                  m.senderName ||
                  m.SenderName ||
                  (isMine ? "Bạn" : "Nhân viên");
                const time = formatTime(m.sentAt || m.SentAt);

                return (
                  <div
                    key={m.messageId || m.MessageId || `${time}-${m.content}`}
                    className={`msg-row ${isMine ? "mine" : "theirs"}`}
                  >
                    <div className="msg-bubble">
                      <div className="msg-content">
                        {m.content || m.Content}
                      </div>
                      <div className="msg-meta">
                        <span className="sender">{senderName}</span>
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
                canSend
                  ? "Nhập tin nhắn của bạn..."
                  : "Phiên chat đã kết thúc."
              }
              disabled={!canSend || sending}
            />
            <button
              type="submit"
              disabled={!canSend || sending || !input.trim()}
            >
              Gửi
            </button>
          </form>
        </div>
      )}
    </div>
  );
}
