// File: src/pages/admin/admin-support-chat.jsx
import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import axiosClient from "../../api/axiosClient";
import { supportChatApi } from "../../api/supportChatApi";

function formatDateTime(value) {
  if (!value) return "";
  try {
    return new Date(value).toLocaleString("vi-VN");
  } catch {
    return String(value);
  }
}

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

function StatusBadge({ status }) {
  const s = (status || "").toString();
  const map = {
    Waiting: { text: "Chờ nhận", cls: "badge bg-warning text-dark" },
    Active: { text: "Đang chat", cls: "badge bg-success" },
    Closed: { text: "Đã đóng", cls: "badge bg-secondary" },
  };
  const d = map[s] || { text: s || "-", cls: "badge bg-light text-dark" };
  return <span className={d.cls}>{d.text}</span>;
}

export default function AdminSupportChatPage() {
  const [tab, setTab] = useState("queue"); // "queue" | "mine"

  const [queueSessions, setQueueSessions] = useState([]);
  const [mySessions, setMySessions] = useState([]);

  const [loadingQueue, setLoadingQueue] = useState(false);
  const [loadingMine, setLoadingMine] = useState(false);

  const [selectedSessionId, setSelectedSessionId] = useState(null);
  const [messages, setMessages] = useState([]);
  const [loadingMessages, setLoadingMessages] = useState(false);

  const [input, setInput] = useState("");
  const [sending, setSending] = useState(false);

  const [error, setError] = useState("");

  const connRef = useRef(null);
  const messagesRef = useRef(null);
  const isAtBottomRef = useRef(true);

  const selectedSession = useMemo(() => {
    const all = [...queueSessions, ...mySessions];
    return (
      all.find((s) => s.chatSessionId === selectedSessionId) || null
    );
  }, [queueSessions, mySessions, selectedSessionId]);

  const loadQueue = useCallback(async () => {
    setLoadingQueue(true);
    try {
      const res = await supportChatApi.getUnassigned();
      const items = Array.isArray(res) ? res : res.items || [];
      setQueueSessions(items);
    } catch (err) {
      console.error("load queue failed", err);
    } finally {
      setLoadingQueue(false);
    }
  }, []);

  const loadMine = useCallback(async () => {
    setLoadingMine(true);
    try {
      const res = await supportChatApi.getMySessions({
        includeClosed: false,
      });
      const items = Array.isArray(res) ? res : res.items || [];
      setMySessions(items);
    } catch (err) {
      console.error("load my sessions failed", err);
    } finally {
      setLoadingMine(false);
    }
  }, []);

  const loadMessages = useCallback(async (sessionId) => {
    if (!sessionId) return;
    setLoadingMessages(true);
    setError("");
    try {
      const res = await supportChatApi.getMessages(sessionId);
      const items = Array.isArray(res) ? res : res.items || [];
      setMessages(items);
      setTimeout(() => scrollToBottom(true), 0);
    } catch (err) {
      console.error("load messages failed", err);
      setError(
        err?.response?.data?.message ||
          "Không tải được lịch sử chat. Vui lòng thử lại."
      );
    } finally {
      setLoadingMessages(false);
    }
  }, []);

  // Lần đầu load danh sách
  useEffect(() => {
    loadQueue();
    loadMine();
  }, [loadQueue, loadMine]);

  // Chọn session từ danh sách
  const handleSelectSession = (session) => {
    if (!session) return;
    setSelectedSessionId(session.chatSessionId);
    loadMessages(session.chatSessionId);
  };

  // Claim (Nhận chat)
  const handleClaim = async (session) => {
    if (!session) return;
    try {
      await supportChatApi.claim(session.chatSessionId);
      // Sau claim: reload queue + mine
      await Promise.all([loadQueue(), loadMine()]);
      setTab("mine");
      setSelectedSessionId(session.chatSessionId);
      await loadMessages(session.chatSessionId);
    } catch (err) {
      console.error("claim chat failed", err);
      alert(
        err?.response?.data?.message ||
          "Không nhận được chat. Có thể phiên đã được nhân viên khác nhận."
      );
    }
  };

  // Gửi message
  const handleSend = async (e) => {
    e.preventDefault();
    if (!selectedSessionId) return;
    const text = input.trim();
    if (!text) return;

    setSending(true);
    setError("");

    try {
      const saved = await supportChatApi.postMessage(selectedSessionId, {
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
        return [...list, saved];
      });
      setTimeout(() => scrollToBottom(true), 0);
    } catch (err) {
      console.error("send support message failed", err);
      setError(
        err?.response?.data?.message ||
          "Không gửi được tin nhắn. Vui lòng thử lại."
      );
    } finally {
      setSending(false);
    }
  };

  // Đóng session
  const handleCloseSession = async () => {
    if (!selectedSessionId || !selectedSession) return;
    if (!window.confirm("Bạn có chắc chắn muốn đóng phiên chat này?")) return;

    try {
      await supportChatApi.close(selectedSessionId);
      await Promise.all([loadQueue(), loadMine()]);
      setSelectedSessionId(null);
      setMessages([]);
    } catch (err) {
      console.error("close session failed", err);
      alert(
        err?.response?.data?.message || "Không đóng được phiên chat. Thử lại."
      );
    }
  };

  // SignalR – connect 1 lần cho staff
  useEffect(() => {
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
    const hubUrl = `${hubRoot}/hubs/support-chats`;

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => localStorage.getItem("access_token") || "",
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();

    connRef.current = connection;

    const upsertSession = (sessionItem) => {
      if (!sessionItem) return;

      // Cập nhật queueSessions
      setQueueSessions((prev) => {
        let list = [...(prev || [])];
        const idx = list.findIndex(
          (s) => s.chatSessionId === sessionItem.chatSessionId
        );

        // Nếu session đã có AssignedStaffId -> không còn trong queue
        if (sessionItem.assignedStaffId) {
          if (idx >= 0) list.splice(idx, 1);
        } else {
          if (idx >= 0) list[idx] = sessionItem;
          else list.push(sessionItem);
        }
        return list;
      });

      // Cập nhật mySessions
      setMySessions((prev) => {
        let list = [...(prev || [])];
        const idx = list.findIndex(
          (s) => s.chatSessionId === sessionItem.chatSessionId
        );

        // Nếu session đã assign cho mình (back-end sẽ trả assignedStaffId = current user)
        // thì ít nhất nó sẽ xuất hiện trong getMySessions (khi reload).
        // Ở realtime, ta chỉ "upsert" nếu đang tồn tại trong mySessions.
        if (idx >= 0) {
          list[idx] = { ...list[idx], ...sessionItem };
        }
        return list;
      });
    };

    const removeSession = (sessionItem) => {
      if (!sessionItem) return;
      const id = sessionItem.chatSessionId;
      setQueueSessions((prev) =>
        (prev || []).filter((s) => s.chatSessionId !== id)
      );
      setMySessions((prev) =>
        (prev || []).filter((s) => s.chatSessionId !== id)
      );
      if (selectedSessionId === id) {
        setSelectedSessionId(null);
        setMessages([]);
      }
    };

    const handleSessionCreated = (item) => {
      // phiên mới Waiting -> xuất hiện trong queue
      upsertSession(item);
    };

    const handleSessionUpdated = (item) => {
      // cập nhật priority/status/assignedStaff, v.v.
      upsertSession(item);
    };

    const handleSessionClosed = (item) => {
      removeSession(item);
    };

    const handleMessage = (msg) => {
      if (!msg) return;

      // nếu đang mở đúng session -> thêm message
      if (msg.chatSessionId === selectedSessionId) {
        setMessages((prev) => {
          const list = prev || [];
          if (
            msg.messageId &&
            list.some((x) => x.messageId === msg.messageId)
          ) {
            return prev;
          }
          return [...list, msg];
        });
        setTimeout(() => scrollToBottom(), 0);
      }
    };

    connection.on("SupportSessionCreated", handleSessionCreated);
    connection.on("SupportSessionUpdated", handleSessionUpdated);
    connection.on("SupportSessionClosed", handleSessionClosed);
    connection.on("SupportMessageReceived", handleMessage);
    connection.on("ReceiveSupportMessage", handleMessage);

    connection
      .start()
      .then(() =>
        connection.invoke("JoinSupportQueue").catch(() => {
          // ignore lỗi nhỏ
        })
      )
      .catch(() => {
        // ignore lỗi nhỏ khi connect
      });

    return () => {
      connection
        .stop()
        .catch(() => {})
        .finally(() => {
          connection.off("SupportSessionCreated", handleSessionCreated);
          connection.off("SupportSessionUpdated", handleSessionUpdated);
          connection.off("SupportSessionClosed", handleSessionClosed);
          connection.off("SupportMessageReceived", handleMessage);
          connection.off("ReceiveSupportMessage", handleMessage);
          if (connRef.current === connection) {
            connRef.current = null;
          }
        });
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedSessionId]);

  // Khi selectedSessionId đổi, join group session tương ứng
  useEffect(() => {
    const connection = connRef.current;
    if (!connection || !selectedSessionId) return;

    let disposed = false;

    connection
      .invoke("JoinSupportSessionGroup", selectedSessionId)
      .catch(() => {});

    return () => {
      if (!connection) return;
      connection
        .invoke("LeaveSupportSessionGroup", selectedSessionId)
        .catch(() => {})
        .finally(() => {
          if (!disposed) {
            // nothing else
          }
        });
    };
  }, [selectedSessionId]);

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

  // ========= RENDER =========

  const sessionsToShow = tab === "queue" ? queueSessions : mySessions;

  return (
    <div className="container my-4">
      <h1 className="mb-3">Hỗ trợ khách hàng (Chat)</h1>

      <div className="row g-3">
        {/* Sidebar: Queue / My sessions */}
        <div className="col-md-4">
          <div className="card h-100">
            <div className="card-header pb-0">
              <ul className="nav nav-tabs card-header-tabs">
                <li className="nav-item">
                  <button
                    type="button"
                    className={`nav-link ${
                      tab === "queue" ? "active" : ""
                    }`}
                    onClick={() => setTab("queue")}
                  >
                    Chờ nhận
                  </button>
                </li>
                <li className="nav-item">
                  <button
                    type="button"
                    className={`nav-link ${
                      tab === "mine" ? "active" : ""
                    }`}
                    onClick={() => setTab("mine")}
                  >
                    Của tôi
                  </button>
                </li>
              </ul>
            </div>
            <div
              className="card-body p-0 d-flex flex-column"
              style={{ minHeight: "300px" }}
            >
              {(tab === "queue" && loadingQueue) ||
              (tab === "mine" && loadingMine) ? (
                <div className="p-2 small text-muted">
                  Đang tải danh sách phiên chat…
                </div>
              ) : sessionsToShow.length === 0 ? (
                <div className="p-2 small text-muted">
                  Không có phiên chat nào trong tab này.
                </div>
              ) : (
                <div
                  style={{
                    overflowY: "auto",
                    maxHeight: "480px",
                  }}
                >
                  {sessionsToShow.map((s) => {
                    const isSelected =
                      s.chatSessionId === selectedSessionId;
                    return (
                      <div
                        key={s.chatSessionId}
                        className={`p-2 border-bottom ${
                          isSelected ? "bg-light" : ""
                        }`}
                        style={{ cursor: "pointer" }}
                        onClick={() => handleSelectSession(s)}
                      >
                        <div className="d-flex justify-content-between align-items-center">
                          <div className="fw-semibold">
                            {s.customerName || s.customerEmail || "Khách hàng"}
                          </div>
                          <StatusBadge status={s.status} />
                        </div>
                        <div className="small text-muted">
                          Ưu tiên: {s.priorityLevel ?? "-"} • Bắt đầu:{" "}
                          {formatDateTime(s.startedAt)}
                        </div>
                        {s.lastMessagePreview && (
                          <div className="small text-truncate">
                            {s.lastMessagePreview}
                          </div>
                        )}
                        {tab === "queue" && (
                          <div className="mt-1 d-flex justify-content-end">
                            <button
                              type="button"
                              className="btn btn-sm btn-outline-primary"
                              onClick={(e) => {
                                e.stopPropagation();
                                handleClaim(s);
                              }}
                            >
                              Nhận chat
                            </button>
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Main chat panel */}
        <div className="col-md-8">
          <div className="card h-100">
            <div className="card-header d-flex justify-content-between align-items-center">
              {selectedSession ? (
                <>
                  <div>
                    <div className="fw-semibold">
                      {selectedSession.customerName ||
                        selectedSession.customerEmail ||
                        "Khách hàng"}
                    </div>
                    <div className="small text-muted">
                      Bắt đầu: {formatDateTime(selectedSession.startedAt)}
                    </div>
                  </div>
                  <div className="d-flex align-items-center gap-2">
                    <StatusBadge status={selectedSession.status} />
                    <button
                      type="button"
                      className="btn btn-sm btn-outline-danger"
                      onClick={handleCloseSession}
                    >
                      Đóng phiên
                    </button>
                  </div>
                </>
              ) : (
                <span className="small text-muted">
                  Chọn một phiên chat ở bên trái để bắt đầu.
                </span>
              )}
            </div>

            <div className="card-body d-flex flex-column">
              {/* Messages */}
              <div
                ref={messagesRef}
                className="flex-grow-1 mb-2"
                style={{
                  border: "1px solid #eee",
                  borderRadius: "4px",
                  padding: "6px",
                  overflowY: "auto",
                  minHeight: "240px",
                  maxHeight: "480px",
                  backgroundColor: "#fafafa",
                }}
                onScroll={handleMessagesScroll}
              >
                {!selectedSession ? (
                  <div className="small text-muted">
                    Chưa chọn phiên chat.
                  </div>
                ) : loadingMessages && !messages.length ? (
                  <div className="small text-muted">
                    Đang tải lịch sử chat…
                  </div>
                ) : messages.length === 0 ? (
                  <div className="small text-muted">
                    Chưa có tin nhắn nào. Hãy gửi lời chào cho khách.
                  </div>
                ) : (
                  <div className="d-flex flex-column gap-1">
                    {messages.map((m) => {
                      const isStaff = !!m.isFromStaff;
                      return (
                        <div
                          key={m.messageId || `${m.sentAt}_${m.senderId}`}
                          className={`d-flex ${
                            isStaff
                              ? "justify-content-end"
                              : "justify-content-start"
                          }`}
                        >
                          <div
                            className="px-2 py-1 rounded"
                            style={{
                              maxWidth: "80%",
                              fontSize: "0.85rem",
                              backgroundColor: isStaff
                                ? "#d1ffd6"
                                : "#e9f3ff",
                              border:
                                "1px solid " +
                                (isStaff ? "#a3f3b0" : "#c0d9ff"),
                            }}
                          >
                            <div className="small mb-1 fw-semibold">
                              {isStaff
                                ? m.senderName || "Bạn"
                                : m.senderName || "Khách hàng"}
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

              {/* Error */}
              {error && (
                <div className="alert alert-warning py-1 small mb-2">
                  {error}
                </div>
              )}

              {/* Input */}
              <form onSubmit={handleSend}>
                <div className="mb-2">
                  <textarea
                    rows={2}
                    className="form-control"
                    placeholder={
                      selectedSession
                        ? "Nhập nội dung tin nhắn..."
                        : "Chọn một phiên chat trước khi gửi."
                    }
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    disabled={!selectedSession || sending}
                  />
                </div>
                <div className="d-flex justify-content-end">
                  <button
                    type="submit"
                    className="btn btn-primary"
                    disabled={
                      !selectedSession || sending || !input.trim()
                    }
                  >
                    {sending ? "Đang gửi..." : "Gửi"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
