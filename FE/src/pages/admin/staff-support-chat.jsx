// File: src/pages/admin/staff-support-chat.jsx
import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { useSearchParams } from "react-router-dom";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import axiosClient from "../../api/axiosClient";
import { supportChatApi } from "../../api/supportChatApi";
import "../../styles/staff-support-chat.css";

// ---- Helpers ----

function formatTimeShort(value) {
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

function normalizeSession(raw) {
  if (!raw) return null;
  return {
    chatSessionId: raw.chatSessionId || raw.ChatSessionId,
    customerName:
      raw.customerName || raw.CustomerName || raw.customerEmail || "Khách hàng",
    customerEmail: raw.customerEmail || raw.CustomerEmail || "",
    assignedStaffName: raw.assignedStaffName || raw.AssignedStaffName || "",
    status: raw.status || raw.Status || "",
    priorityLevel:
      raw.priorityLevel ?? raw.PriorityLevel ?? raw.priority ?? null,
    lastMessagePreview: raw.lastMessagePreview || raw.LastMessagePreview || "",
    lastMessageAt: raw.lastMessageAt || raw.LastMessageAt,
    startedAt: raw.startedAt || raw.StartedAt,
  };
}

function normalizeMessage(raw) {
  if (!raw) return null;
  return {
    messageId: raw.messageId || raw.MessageId,
    chatSessionId: raw.chatSessionId || raw.ChatSessionId,
    senderId: raw.senderId || raw.SenderId,
    senderName: raw.senderName || raw.SenderName || "",
    isFromStaff:
      typeof raw.isFromStaff === "boolean"
        ? raw.isFromStaff
        : !!raw.IsFromStaff,
    content: raw.content || raw.Content || "",
    createdAt: raw.createdAt || raw.CreatedAt,
  };
}

function getStatusLabel(session) {
  if (!session) return "";
  const status = String(session.status || "").toLowerCase();
  if (status === "waiting") return "Đang chờ nhận";
  if (status === "open") return "Đang mở";
  if (status === "active") return "Đang chat";
  if (status === "closed") return "Đã đóng";
  return session.status || "";
}

function getStatusTextForHeader(session) {
  if (!session) return "";
  const status = String(session.status || "").toLowerCase();
  const staffName = session.assignedStaffName || "nhân viên hỗ trợ";

  if (status === "waiting") {
    return "Phiên chat đang chờ nhân viên nhận.";
  }
  if (status === "open" || status === "active") {
    return `Đang chat với ${session.customerName}. Nhân viên: ${staffName}.`;
  }
  if (status === "closed") {
    return "Phiên chat đã kết thúc.";
  }
  return "";
}

function getPriorityLabel(level) {
  if (level === null || level === undefined) return "Tiêu chuẩn";
  const n = Number(level);
  if (!Number.isFinite(n)) return "Tiêu chuẩn";
  if (n === 1) return "Ưu tiên";
  if (n === 2) return "VIP";
  return "Tiêu chuẩn";
}

// === MỚI: helper đọc tab từ query string ===
function getTabFromQuery(searchParams) {
  if (!searchParams) return null;
  try {
    const raw = (
      searchParams.get("tab") ||
      searchParams.get("view") ||
      ""
    )
      .toString()
      .toLowerCase();

    if (raw === "unassigned" || raw === "mine") {
      return raw;
    }

    return null;
  } catch {
    return null;
  }
}

// ---- Staff Support Chat Page ----

export default function StaffSupportChatPage() {
  const isAdmin = false; // staff bị hạn chế

  const [searchParams, setSearchParams] = useSearchParams();
  const initialSelectedId = searchParams.get("sessionId") || null;
  const initialActiveTab = getTabFromQuery(searchParams) || "unassigned";

  const [activeTab, setActiveTab] = useState(initialActiveTab); // "unassigned" | "mine"
  const [includeClosed] = useState(false); // staff không dùng, luôn false

  const [queue, setQueue] = useState([]);
  const [mine, setMine] = useState([]);

  const [selectedSessionId, setSelectedSessionId] = useState(initialSelectedId);
  const [messages, setMessages] = useState([]);
  const [loadingQueue, setLoadingQueue] = useState(false);
  const [loadingMine, setLoadingMine] = useState(false);
  const [loadingMessages, setLoadingMessages] = useState(false);

  const [sending, setSending] = useState(false);
  const [newMessage, setNewMessage] = useState("");

  const [stateText, setStateText] = useState("");
  const [errorText, setErrorText] = useState("");

  const messagesEndRef = useRef(null);
  const connectionRef = useRef(null);
  const joinedSessionIdRef = useRef(null);

  const effectiveIncludeClosed = isAdmin && includeClosed; // => luôn false

  // Đồng bộ selectedSessionId với query param ?sessionId=...
  useEffect(() => {
    const paramId = searchParams.get("sessionId") || null;
    setSelectedSessionId((prev) => (prev === paramId ? prev : paramId));
  }, [searchParams]);

  // === MỚI: Đồng bộ activeTab với query param ?tab=... (nếu có) ===
  useEffect(() => {
    const queryTab = getTabFromQuery(searchParams);
    if (!queryTab) return;
    setActiveTab((prev) => (prev === queryTab ? prev : queryTab));
  }, [searchParams]);

  const selectedSession = useMemo(() => {
    if (!selectedSessionId) return null;
    return (
      queue.find((s) => s.chatSessionId === selectedSessionId) ||
      mine.find((s) => s.chatSessionId === selectedSessionId) ||
      null
    );
  }, [queue, mine, selectedSessionId]);

  const pageTitle = "Chat hỗ trợ (Staff)";

  // ---- Load danh sách ----

  const loadQueue = useCallback(async () => {
    setLoadingQueue(true);
    try {
      const res = await supportChatApi.getUnassigned();
      const items = Array.isArray(res?.items ?? res?.Items)
        ? res.items ?? res.Items
        : Array.isArray(res)
        ? res
        : [];
      const mapped = items.map(normalizeSession).filter(Boolean);
      setQueue(mapped);
    } catch (e) {
      console.error(e);
      setErrorText(
        e?.response?.data?.message ||
          e.message ||
          "Không tải được danh sách hàng chờ."
      );
    } finally {
      setLoadingQueue(false);
    }
  }, []);

  const loadMine = useCallback(async () => {
    setLoadingMine(true);
    try {
      const res = await supportChatApi.getMySessions({
        includeClosed: effectiveIncludeClosed,
      });
      const items = Array.isArray(res?.items ?? res?.Items)
        ? res.items ?? res.Items
        : Array.isArray(res)
        ? res
        : [];
      const mapped = items.map(normalizeSession).filter(Boolean);
      setMine(mapped);
    } catch (e) {
      console.error(e);
      setErrorText(
        e?.response?.data?.message ||
          e.message ||
          "Không tải được danh sách phiên của bạn."
      );
    } finally {
      setLoadingMine(false);
    }
  }, [effectiveIncludeClosed]);

  const refreshAll = useCallback(async () => {
    setStateText("Đang tải dữ liệu...");
    setErrorText("");
    await Promise.all([loadQueue(), loadMine()]);
    setStateText("");
  }, [loadQueue, loadMine]);

  // ---- SignalR connection ----
  useEffect(() => {
    let alive = true;

    const setupConnection = async () => {
      try {
        // Lấy base URL giống các màn khác (ticket, widget)
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
        // BE: MapHub<SupportChatHub>("/supportChatHub") hoặc "/hubs/support-chat"
        const hubUrl = `${hubRoot}/supportChatHub`;

        const conn = new HubConnectionBuilder()
          .withUrl(hubUrl, {
            accessTokenFactory: () => {
              try {
                const raw =
                  localStorage.getItem("token") ||
                  sessionStorage.getItem("token") ||
                  "";
                return raw;
              } catch {
                return "";
              }
            },
          })
          .configureLogging(LogLevel.Information)
          .withAutomaticReconnect()
          .build();

        conn.on("ReceiveSupportChatMessage", (raw) => {
          const msg = normalizeMessage(raw);
          if (!msg) return;
          // Nếu tin nhắn thuộc phiên hiện đang join thì thêm vào
          setMessages((prev) => {
            if (!prev) return [msg];
            if (prev.some((x) => x.messageId === msg.messageId)) return prev;
            return [...prev, msg];
          });

          // Nếu là tin nhắn mới cho phiên trong list, update lastMessagePreview
          setQueue((prev) =>
            prev.map((s) =>
              s.chatSessionId === msg.chatSessionId
                ? {
                    ...s,
                    lastMessagePreview: msg.content,
                    lastMessageAt: msg.createdAt,
                  }
                : s
            )
          );
          setMine((prev) =>
            prev.map((s) =>
              s.chatSessionId === msg.chatSessionId
                ? {
                    ...s,
                    lastMessagePreview: msg.content,
                    lastMessageAt: msg.createdAt,
                  }
                : s
            )
          );
        });

        conn.onclose((e) => {
          console.warn("SupportChat SignalR connection closed:", e);
        });

        await conn.start();
        if (!alive) {
          await conn.stop();
          return;
        }

        connectionRef.current = conn;

        if (joinedSessionIdRef.current) {
          try {
            await conn.invoke("JoinSupportChatSession", joinedSessionIdRef.current);
          } catch (e) {
            console.error("Failed to re-join support chat session:", e);
          }
        }
      } catch (e) {
        console.error("Failed to setup SupportChat SignalR connection:", e);
      }
    };

    setupConnection();

    return () => {
      alive = false;
      if (connectionRef.current) {
        connectionRef.current
          .stop()
          .catch((e) => console.error("Error stopping SignalR connection:", e));
        connectionRef.current = null;
      }
    };
  }, []);

  // ---- Join/leave session trên SignalR khi selectedSessionId thay đổi ----
  useEffect(() => {
    const conn = connectionRef.current;
    const sessionId = selectedSessionId;

    if (!conn) return;

    const run = async () => {
      try {
        if (joinedSessionIdRef.current && joinedSessionIdRef.current !== sessionId) {
          await conn.invoke("LeaveSupportChatSession", joinedSessionIdRef.current);
          joinedSessionIdRef.current = null;
        }

        if (sessionId) {
          await conn.invoke("JoinSupportChatSession", sessionId);
          joinedSessionIdRef.current = sessionId;
        }
      } catch (e) {
        console.error("Failed to join/leave support chat session:", e);
      }
    };

    run();
  }, [selectedSessionId]);

  // ---- Scroll xuống cuối khi có tin nhắn ----
  useEffect(() => {
    if (!messagesEndRef.current) return;
    messagesEndRef.current.scrollIntoView({ behavior: "smooth" });
  }, [messages, selectedSessionId]);

  // ---- Load list lần đầu ----
  useEffect(() => {
    refreshAll();
  }, [refreshAll]);

  // ---- Khi chọn session thì load messages ----
  useEffect(() => {
    if (!selectedSessionId) {
      setMessages([]);
      return;
    }
    loadMessages(selectedSessionId);
  }, [selectedSessionId, loadMessages]);

  // ---- Helpers: select session + sync URL ----

  const handleSelectSession = (sessionId) => {
    setSelectedSessionId(sessionId || null);

    const next = new URLSearchParams(searchParams);
    if (sessionId) {
      next.set("sessionId", sessionId);
    } else {
      next.delete("sessionId");
    }
    setSearchParams(next, { replace: false });
  };

  // === MỚI: đổi tab + sync ?tab=... ===
  const handleChangeTab = (nextTab) => {
    if (nextTab !== "unassigned" && nextTab !== "mine") return;

    setActiveTab(nextTab);

    const next = new URLSearchParams(searchParams);
    next.set("tab", nextTab);
    setSearchParams(next, { replace: false });
  };

  // ---- Actions ----

  const handleClaim = async (sessionId) => {
    if (!sessionId) return;
    if (!window.confirm("Bạn có chắc muốn nhận phiên chat này?")) return;

    try {
      setStateText("Đang nhận phiên chat...");
      await supportChatApi.claimSession(sessionId);
      await refreshAll();
      handleSelectSession(sessionId);
    } catch (e) {
      console.error(e);
      setErrorText(
        e?.response?.data?.message || e.message || "Nhận phiên chat thất bại."
      );
    } finally {
      setStateText("");
    }
  };

  const handleUnassign = async (sessionId) => {
    if (!sessionId) return;
    if (
      !window.confirm(
        "Bạn có chắc muốn trả lại phiên chat này về hàng chờ? Khách sẽ không nhận được phản hồi từ bạn nữa."
      )
    )
      return;

    try {
      setStateText("Đang trả lại phiên chat...");
      await supportChatApi.unassignSession(sessionId);
      await refreshAll();
      handleSelectSession(null);
    } catch (e) {
      console.error(e);
      setErrorText(
        e?.response?.data?.message || e.message || "Trả lại phiên chat thất bại."
      );
    } finally {
      setStateText("");
    }
  };

  const handleClose = async (sessionId) => {
    if (!sessionId) return;
    if (!window.confirm("Đóng phiên chat này?")) return;

    try {
      setStateText("Đang đóng phiên chat...");
      await supportChatApi.closeSession(sessionId);
      await refreshAll();
      handleSelectSession(null);
    } catch (e) {
      console.error(e);
      setErrorText(
        e?.response?.data?.message || e.message || "Đóng phiên chat thất bại."
      );
    } finally {
      setStateText("");
    }
  };

  const handleSendMessage = async (e) => {
    e?.preventDefault();
    const content = newMessage.trim();
    if (!content) return;

    if (!selectedSessionId) {
      setErrorText("Hãy chọn 1 phiên chat trước khi gửi tin nhắn.");
      return;
    }

    try {
      setSending(true);
      setErrorText("");
      const msg = await supportChatApi.createMessage(selectedSessionId, {
        content,
      });
      const normalized = normalizeMessage(msg);
      if (normalized) {
        setMessages((prev) => [...prev, normalized]);
      }
      setNewMessage("");
    } catch (e) {
      console.error(e);
      setErrorText(
        e?.response?.data?.message || e.message || "Gửi tin nhắn thất bại."
      );
    } finally {
      setSending(false);
    }
  };

  // ---- Load messages helper ----

  const loadMessages = useCallback(async (sessionId) => {
    if (!sessionId) {
      setMessages([]);
      return;
    }

    setLoadingMessages(true);
    try {
      const res = await supportChatApi.getMessages(sessionId);
      const items = Array.isArray(res?.items ?? res?.Items)
        ? res.items ?? res.Items
        : Array.isArray(res)
        ? res
        : [];
      const mapped = items.map(normalizeMessage).filter(Boolean);
      setMessages(mapped);
    } catch (e) {
      console.error(e);
      setErrorText(
        e?.response?.data?.message ||
          e.message ||
          "Không tải được lịch sử tin nhắn."
      );
    } finally {
      setLoadingMessages(false);
    }
  }, []);

  // ---- Render helpers ----

  const renderSessionItem = (session, { inQueue }) => {
    const isSelected =
      selectedSessionId && selectedSessionId === session.chatSessionId;
    const initials =
      (session.customerName || "?")
        .trim()
        .split(" ")
        .map((x) => x[0])
        .join("")
        .toUpperCase();

    return (
      <button
        key={session.chatSessionId}
        type="button"
        className={
          "support-chat-session-item" + (isSelected ? " selected" : "")
        }
        onClick={() => handleSelectSession(session.chatSessionId)}
      >
        <div className="session-avatar">{initials}</div>
        <div className="session-main">
          <div className="session-row">
            <span className="session-name">{session.customerName}</span>
            <span className="session-time">
              {formatTimeShort(session.lastMessageAt || session.startedAt)}
            </span>
          </div>
          <div className="session-row">
            <span className="session-preview">
              {session.lastMessagePreview || "Chưa có tin nhắn nào."}
            </span>
          </div>
          <div className="session-row session-meta">
            <span className="session-status">
              {getStatusLabel(session)} • Ưu tiên:{" "}
              {getPriorityLabel(session.priorityLevel)}
            </span>
            {inQueue && session.assignedStaffName && (
              <span className="session-assigned">
                Đang gán cho: {session.assignedStaffName}
              </span>
            )}
          </div>
        </div>
      </button>
    );
  };

  const renderSidebarList = () => {
    const list = activeTab === "unassigned" ? queue : mine;
    const inQueue = activeTab === "unassigned";

    if (inQueue && loadingQueue) {
      return <div className="sidebar-empty">Đang tải hàng chờ...</div>;
    }
    if (!inQueue && loadingMine) {
      return <div className="sidebar-empty">Đang tải phiên của bạn...</div>;
    }

    if (!list || list.length === 0) {
      return (
        <div className="sidebar-empty">
          {inQueue ? "Không có phiên chat nào đang chờ." : "Bạn chưa có phiên chat nào."}
        </div>
      );
    }

    return list.map((s) => renderSessionItem(s, { inQueue }));
  };

  return (
    <div className="support-chat-page">
      <header className="support-chat-header">
        <div>
          <h1>{pageTitle}</h1>
          <div className="support-chat-header-stats">
            <span>Chờ nhận: {queue.length}</span>
            <span>•</span>
            <span>Phiên của bạn: {mine.length}</span>
          </div>
        </div>
        <div className="support-chat-header-actions">
          <button
            type="button"
            className="btn ghost"
            onClick={() => handleSelectSession(null)}
          >
            Bỏ chọn
          </button>
          <button
            type="button"
            className="btn primary"
            onClick={refreshAll}
          >
            Làm mới
          </button>
        </div>
      </header>

      {stateText && <div className="alert info">{stateText}</div>}
      {errorText && <div className="alert error">{errorText}</div>}

      <div className="support-chat-layout">
        {/* Sidebar: danh sách phiên */}
        <aside className="support-chat-sidebar">
          <div className="tabs tabs-boxed">
            <button
              type="button"
              className={
                "tab" + (activeTab === "unassigned" ? " tab-active" : "")
              }
              onClick={() => handleChangeTab("unassigned")}
            >
              Hàng chờ
              <span className="badge">{queue.length}</span>
            </button>
            <button
              type="button"
              className={"tab" + (activeTab === "mine" ? " tab-active" : "")}
              onClick={() => handleChangeTab("mine")}
            >
              Phiên của tôi
              <span className="badge">{mine.length}</span>
            </button>
          </div>
          <div className="support-chat-session-list">{renderSidebarList()}</div>
        </aside>

        {/* Main content: khung chat */}
        <main className="support-chat-main">
          {!selectedSession && (
            <div className="support-chat-empty">
              <p>Hãy chọn một phiên chat ở bên trái để bắt đầu hỗ trợ khách hàng.</p>
            </div>
          )}

          {selectedSession && (
            <div className="support-chat-main-inner">
              <div className="support-chat-main-header">
                <div>
                  <h2>{selectedSession.customerName}</h2>
                  <p className="status-text">
                    {getStatusTextForHeader(selectedSession)}
                  </p>
                </div>
                <div className="support-chat-main-actions">
                  {selectedSession.status === "waiting" && (
                    <button
                      type="button"
                      className="btn primary"
                      onClick={() => handleClaim(selectedSession.chatSessionId)}
                    >
                      Nhận phiên
                    </button>
                  )}
                  {selectedSession.status !== "waiting" && (
                    <>
                      <button
                        type="button"
                        className="btn ghost"
                        onClick={() => handleUnassign(selectedSession.chatSessionId)}
                      >
                        Trả lại hàng chờ
                      </button>
                      <button
                        type="button"
                        className="btn danger"
                        onClick={() => handleClose(selectedSession.chatSessionId)}
                      >
                        Đóng phiên
                      </button>
                    </>
                  )}
                </div>
              </div>

              <div className="support-chat-messages">
                {loadingMessages ? (
                  <div className="support-chat-messages-loading">
                    Đang tải lịch sử chat...
                  </div>
                ) : messages.length === 0 ? (
                  <div className="support-chat-messages-empty">
                    Chưa có tin nhắn nào trong phiên chat này.
                  </div>
                ) : (
                  messages.map((msg) => (
                    <div
                      key={msg.messageId}
                      className={
                        "chat-message" +
                        (msg.isFromStaff ? " from-staff" : " from-customer")
                      }
                    >
                      <div className="chat-message-meta">
                        <span className="chat-sender">
                          {msg.isFromStaff ? msg.senderName || "Nhân viên" : "Khách hàng"}
                        </span>
                        <span className="chat-time">
                          {formatTimeShort(msg.createdAt)}
                        </span>
                      </div>
                      <div className="chat-message-content">{msg.content}</div>
                    </div>
                  ))
                )}
                <div ref={messagesEndRef} />
              </div>

              <form className="support-chat-input" onSubmit={handleSendMessage}>
                <textarea
                  rows={2}
                  placeholder="Nhập nội dung tin nhắn..."
                  value={newMessage}
                  onChange={(e) => setNewMessage(e.target.value)}
                  disabled={sending || !selectedSession}
                />
                <button
                  type="submit"
                  className="btn primary"
                  disabled={sending || !newMessage.trim() || !selectedSession}
                >
                  {sending ? "Đang gửi..." : "Gửi"}
                </button>
              </form>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
