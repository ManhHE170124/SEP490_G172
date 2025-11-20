// File: src/components/ChatWidget.jsx
import React, { useEffect, useRef, useState } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import axiosClient from "../api/axiosClient";
import { supportChatApi } from "../api/supportChatApi";

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

  // Toggle widget
  const toggleOpen = () => {
    setOpen((prev) => !prev);
  };

  // Khi widget mở lần đầu -> gọi open-or-get + load messages
  useEffect(() => {
    if (!open) {
      // đóng widget: không phá session, chỉ ẩn UI
      setError("");
      return;
    }

    let cancelled = false;

    async function initSession() {
      if (session && session.chatSessionId) {
        // đã có session -> chỉ reload messages
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

  // Hàm load messages cho 1 session
  async function loadMessages(chatSessionId, opts = {}) {
    if (!chatSessionId) return;
    const { silent = false, force = false } = opts;

    if (!silent) {
      setLoadingMessages(true);
    }

    try {
      const res = await supportChatApi.getMessages(chatSessionId);
      const items = Array.isArray(res) ? res : res.items || [];
      setMessages(items);
      if (!force) {
        // nếu không force thì chỉ auto-scroll nếu đang ở đáy
        if (isAtBottomRef.current) scrollToBottom();
      } else {
        scrollToBottom(true);
      }
    } catch (err) {
      console.error("load messages failed", err);
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

  // Kết nối SignalR khi đã có sessionId + widget đang mở
  useEffect(() => {
    if (!open) return;
    if (!session || !session.chatSessionId) return;

    let connection = null;
    let disposed = false;

    // base URL giống admin-ticket-detail
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
    const hubUrl = `${hubRoot}/hubs/support-chats`; // <-- nếu backend dùng path khác, chỉnh ở đây

    connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => localStorage.getItem("access_token") || "",
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();

    connRef.current = connection;

    const handleSupportMessage = (msg) => {
      if (!msg || msg.chatSessionId !== session.chatSessionId) return;
      setMessages((prev) => {
        const list = prev || [];
        // tránh trùng messageId
        if (list.some((x) => x.messageId === msg.messageId)) return prev;
        const next = [...list, msg];
        return next;
      });
      if (isAtBottomRef.current) {
        // auto scroll khi đang ở đáy
        setTimeout(() => scrollToBottom(), 0);
      }
    };

    const handleSessionUpdated = (item) => {
      if (!item || item.chatSessionId !== session.chatSessionId) return;
      setSession((prev) => ({ ...(prev || {}), ...item }));
    };

    // Đăng ký event (support nhiều tên để tránh lệch nhỏ giữa BE/FE)
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
      .catch(() => {
        // ignore lỗi nhỏ khi negotiate
      });

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

  // Scroll handler để biết đang ở đáy hay không
  const handleMessagesScroll = () => {
    const el = messagesRef.current;
    if (!el) return;
    const threshold = 20;
    const distanceToBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    isAtBottomRef.current = distanceToBottom <= threshold;
  };

  function scrollToBottom(force) {
    const el = messagesRef.current;
    if (!el) return;
    if (!force && !isAtBottomRef.current) return;
    el.scrollTop = el.scrollHeight;
  }

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

  // Nếu widget đang đóng chỉ render nút
  return (
    <>
      {/* Nút mở widget */}
      <button
        type="button"
        className="btn btn-primary rounded-circle"
        style={{
          position: "fixed",
          right: "20px",
          bottom: "20px",
          zIndex: 1050,
          width: "56px",
          height: "56px",
          boxShadow: "0 2px 6px rgba(0,0,0,0.2)",
        }}
        onClick={toggleOpen}
      >
        💬
      </button>

      {/* Popup chat */}
      {open && (
        <div
          className="card"
          style={{
            position: "fixed",
            right: "20px",
            bottom: "90px",
            width: "320px",
            maxHeight: "70vh",
            display: "flex",
            flexDirection: "column",
            zIndex: 1050,
            boxShadow: "0 4px 12px rgba(0,0,0,0.15)",
          }}
        >
          <div className="card-header d-flex justify-content-between align-items-center py-2">
            <span className="fw-semibold">Hỗ trợ trực tuyến</span>
            <button
              type="button"
              className="btn btn-sm btn-outline-secondary"
              onClick={toggleOpen}
            >
              ✕
            </button>
          </div>

          <div
            className="card-body p-2 d-flex flex-column"
            style={{ flex: 1, minHeight: "220px" }}
          >
            {loadingSession && !session && (
              <div className="small text-muted">Đang khởi tạo phiên chat…</div>
            )}

            {error && (
              <div className="alert alert-warning py-1 small mb-2">
                {error}
              </div>
            )}

            {/* Message list */}
            <div
              ref={messagesRef}
              className="flex-grow-1 mb-2"
              style={{
                overflowY: "auto",
                border: "1px solid #eee",
                borderRadius: "4px",
                padding: "4px",
                backgroundColor: "#fafafa",
              }}
              onScroll={handleMessagesScroll}
            >
              {loadingMessages && !messages.length ? (
                <div className="small text-muted px-1 py-1">
                  Đang tải lịch sử chat…
                </div>
              ) : !messages || messages.length === 0 ? (
                <div className="small text-muted px-1 py-1">
                  Hãy gửi tin nhắn đầu tiên để chúng tôi hỗ trợ bạn.
                </div>
              ) : (
                <div className="d-flex flex-column gap-1">
                  {messages.map((m) => {
                    const isStaff = !!m.isFromStaff;
                    return (
                      <div
                        key={m.messageId || `${m.sentAt}_${m.senderId}`}
                        className={`d-flex ${
                          isStaff ? "justify-content-start" : "justify-content-end"
                        }`}
                      >
                        <div
                          className="px-2 py-1 rounded"
                          style={{
                            maxWidth: "80%",
                            fontSize: "0.85rem",
                            backgroundColor: isStaff ? "#e9f3ff" : "#d1ffd6",
                            border:
                              "1px solid " + (isStaff ? "#c0d9ff" : "#a3f3b0"),
                          }}
                        >
                          <div className="small mb-1 fw-semibold">
                            {isStaff ? m.senderName || "Nhân viên hỗ trợ" : "Bạn"}
                          </div>
                          <div>{m.content}</div>
                          <div className="text-muted small text-end mt-1">
                            {formatTime(m.sentAt)}
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>

            {/* Form gửi tin */}
            <form onSubmit={handleSend}>
              <div className="mb-2">
                <textarea
                  rows={2}
                  className="form-control"
                  placeholder="Nhập nội dung tin nhắn..."
                  value={input}
                  onChange={(e) => setInput(e.target.value)}
                  disabled={sending || !!error || !session}
                />
              </div>
              <div className="d-flex justify-content-end">
                <button
                  type="submit"
                  className="btn btn-primary btn-sm"
                  disabled={sending || !input.trim() || !session}
                >
                  {sending ? "Đang gửi..." : "Gửi"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
