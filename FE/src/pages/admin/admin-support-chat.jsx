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
import "../../styles/admin-support-chat.css";

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
    sentAt: raw.sentAt || raw.SentAt,
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

// ---- Admin Support Chat Page ----

export default function AdminSupportChatPage() {
  const isAdmin = true;

  const [activeTab, setActiveTab] = useState("unassigned"); // "unassigned" | "mine"
  const [includeClosed, setIncludeClosed] = useState(false); // chỉ dùng cho admin

  const [queue, setQueue] = useState([]);
  const [mine, setMine] = useState([]);

  const [selectedSessionId, setSelectedSessionId] = useState(null);
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

  const effectiveIncludeClosed = isAdmin && includeClosed;

  const selectedSession = useMemo(() => {
    if (!selectedSessionId) return null;
    return (
      queue.find((s) => s.chatSessionId === selectedSessionId) ||
      mine.find((s) => s.chatSessionId === selectedSessionId) ||
      null
    );
  }, [queue, mine, selectedSessionId]);

  const pageTitle = "Chat hỗ trợ (Admin)";

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
      const mapped = items.map(normalizeSession);
      setQueue(mapped);
    } catch (e) {
      console.error(e);
      setErrorText(
        e?.response?.data?.message || e.message || "Không tải được danh sách chờ."
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
      const mapped = items.map(normalizeSession);
      setMine(mapped);
    } catch (e) {
      console.error(e);
      setErrorText(
        e?.response?.data?.message ||
          e.message ||
          "Không tải được danh sách chat của bạn."
      );
    } finally {
      setLoadingMine(false);
    }
  }, [effectiveIncludeClosed]);

  const loadMessages = useCallback(
    async (sessionId) => {
      if (!sessionId) return;
      setLoadingMessages(true);
      try {
        const res = await supportChatApi.getMessages(sessionId);
        const items = Array.isArray(res?.items ?? res?.Items)
          ? res.items ?? res.Items
          : Array.isArray(res)
          ? res
          : [];
        const mapped = items.map(normalizeMessage);
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
    },
    []
  );

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
        const baseUrl = axiosClient.getUri().replace(/\/+$/, "");
        const hubUrl = `${baseUrl}/hubs/support-chat`;

        const conn = new HubConnectionBuilder()
          .withUrl(hubUrl)
          .configureLogging(LogLevel.Information)
          .withAutomaticReconnect()
          .build();

        conn.on("ReceiveSupportChatMessage", (raw) => {
          const msg = normalizeMessage(raw);
          if (!msg) return;

          setMessages((prev) => {
            const list = Array.isArray(prev) ? prev : [];
            const exists = list.some((m) => m.messageId === msg.messageId);
            if (exists) return list;
            const next = [...list, msg];
            return next;
          });
        });

        await conn.start();
        if (!alive) {
          await conn.stop();
          return;
        }
        connectionRef.current = conn;
        setStateText("Đã kết nối realtime chat.");
      } catch (e) {
        console.error(e);
        setErrorText(
          e?.message ||
            "Không kết nối được realtime chat. Bạn vẫn có thể chat nhưng không realtime."
        );
      }
    };

    setupConnection();

    return () => {
      alive = false;
      if (connectionRef.current) {
        connectionRef.current.stop().catch(() => {});
        connectionRef.current = null;
      }
    };
  }, []);

  // ---- Scroll xuống cuối khi có tin nhắn ----

  useEffect(() => {
    if (!messagesEndRef.current) return;
    messagesEndRef.current.scrollIntoView({ behavior: "smooth" });
  }, [messages, selectedSessionId]);

  // ---- Load list lần đầu & khi includeClosed thay đổi ----

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

  // ---- Actions ----

  const handleClaim = async (sessionId) => {
    if (!sessionId) return;
    try {
      setStateText("Đang nhận phiên chat...");
      await supportChatApi.claimSession(sessionId);
      await refreshAll();
      setSelectedSessionId(sessionId);
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
    if (!window.confirm("Bạn có chắc chắn trả lại phiên chat này về hàng chờ?"))
      return;
    try {
      setStateText("Đang trả lại phiên chat...");
      await supportChatApi.unassignSession(sessionId);
      await refreshAll();
      if (selectedSessionId === sessionId) {
        setSelectedSessionId(null);
        setMessages([]);
      }
    } catch (e) {
      console.error(e);
      setErrorText(
        e?.response?.data?.message ||
          e.message ||
          "Trả lại phiên chat thất bại."
      );
    } finally {
      setStateText("");
    }
  };

  const handleSendMessage = async (e) => {
    e?.preventDefault?.();
    if (!selectedSessionId) {
      alert("Vui lòng chọn phiên chat trước.");
      return;
    }
    if (!newMessage.trim()) return;

    try {
      setSending(true);
      const payload = {
        chatSessionId: selectedSessionId,
        content: newMessage.trim(),
      };
      const res = await supportChatApi.sendMessage(payload);
      const msg = normalizeMessage(res);
      if (msg) {
        setMessages((prev) => [...prev, msg]);
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

  // ---- UI helpers ----

  const unassignedCount = queue.length;
  const myCount = mine.length;

  const currentList = activeTab === "unassigned" ? queue : mine;
  const currentLoading =
    activeTab === "unassigned" ? loadingQueue : loadingMine;

  return (
    <div className="support-chat-page">
      <div className="support-chat-header">
        <div>
          <h1 className="page-title">{pageTitle}</h1>
          <div className="support-chat-header-stats">
            <span>Đang chờ: {unassignedCount}</span>
            <span>•</span>
            <span>Phiên của tôi: {myCount}</span>
          </div>
        </div>

        <div className="support-chat-header-actions">
          {isAdmin && (
            <label className="include-closed-toggle">
              <input
                type="checkbox"
                checked={includeClosed}
                onChange={(e) => setIncludeClosed(e.target.checked)}
              />
              <span>Hiện cả phiên đã đóng</span>
            </label>
          )}
          <button className="btn ghost" onClick={refreshAll}>
            Làm mới
          </button>
        </div>
      </div>

      {(stateText || errorText) && (
        <div className="support-chat-state">
          {stateText && <span className="state-text">{stateText}</span>}
          {errorText && <span className="error-text">{errorText}</span>}
        </div>
      )}

      <div className="support-chat-layout">
        {/* Sidebar: hàng chờ & phiên của tôi */}
        <aside className="support-chat-sidebar">
          <div className="tabs">
            <button
              type="button"
              className={
                "tab" + (activeTab === "unassigned" ? " tab-active" : "")
              }
              onClick={() => setActiveTab("unassigned")}
            >
              Chờ nhận
              {unassignedCount > 0 && (
                <span className="badge">{unassignedCount}</span>
              )}
            </button>
            <button
              type="button"
              className={"tab" + (activeTab === "mine" ? " tab-active" : "")}
              onClick={() => setActiveTab("mine")}
            >
              Của tôi
              {myCount > 0 && <span className="badge">{myCount}</span>}
            </button>
          </div>

          <div className="sidebar-toolbar">
            <span className="muted">
              {activeTab === "unassigned"
                ? "Danh sách phiên chat đang chờ nhân viên nhận."
                : "Các phiên chat bạn đang phụ trách."}
            </span>
          </div>

          <div className="session-list">
            {currentLoading && (
              <div className="empty small">Đang tải danh sách...</div>
            )}
            {!currentLoading && currentList.length === 0 && (
              <div className="empty small">Không có phiên chat nào.</div>
            )}
            {!currentLoading &&
              currentList.map((s) => {
                const statusLabel = getStatusLabel(s);
                const isSelected = s.chatSessionId === selectedSessionId;
                const timeText =
                  formatTimeShort(s.lastMessageAt || s.startedAt) || "";
                const priorityText =
                  s.priorityLevel === 3
                    ? "VIP"
                    : s.priorityLevel === 2
                    ? "Ưu tiên"
                    : s.priorityLevel === 1
                    ? "Tiêu chuẩn"
                    : "";

                return (
                  <div
                    key={s.chatSessionId}
                    className={
                      "session-item" + (isSelected ? " session-item-selected" : "")
                    }
                    onClick={() => setSelectedSessionId(s.chatSessionId)}
                  >
                    <div className="session-avatar">
                      {(s.customerName || "?").trim().charAt(0).toUpperCase()}
                    </div>
                    <div className="session-info">
                      <div className="session-line1">
                        <span className="session-customer">
                          {s.customerName}
                        </span>
                        {timeText && (
                          <span className="session-time">{timeText}</span>
                        )}
                      </div>
                      <div className="session-line2">
                        {statusLabel && (
                          <span className="session-status">{statusLabel}</span>
                        )}
                        {priorityText && (
                          <span className="session-priority">
                            {priorityText}
                          </span>
                        )}
                      </div>
                      {s.lastMessagePreview && (
                        <div className="session-preview">
                          {s.lastMessagePreview}
                        </div>
                      )}
                    </div>
                    <div className="session-actions">
                      {activeTab === "unassigned" ? (
                        <button
                          type="button"
                          className="btn xs primary"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleClaim(s.chatSessionId);
                          }}
                        >
                          Nhận
                        </button>
                      ) : (
                        <button
                          type="button"
                          className="btn xs ghost"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleUnassign(s.chatSessionId);
                          }}
                        >
                          Trả lại
                        </button>
                      )}
                    </div>
                  </div>
                );
              })}
          </div>
        </aside>

        {/* Main chat */}
        <section className="support-chat-main">
          {!selectedSession && (
            <div className="chat-empty">
              <p>Chọn một phiên chat ở bên trái để bắt đầu.</p>
            </div>
          )}

          {selectedSession && (
            <div className="chat-panel">
              <header className="chat-header">
                <div className="chat-header-main">
                  <div className="chat-avatar">
                    {(selectedSession.customerName || "?")
                      .trim()
                      .charAt(0)
                      .toUpperCase()}
                  </div>
                  <div>
                    <div className="chat-customer-name">
                      {selectedSession.customerName}
                    </div>
                    <div className="chat-meta">
                      <span className="meta-item">
                        Trạng thái:{" "}
                        <strong>{getStatusLabel(selectedSession)}</strong>
                      </span>
                      {selectedSession.assignedStaffName && (
                        <span className="meta-item">
                          Nhân viên:{" "}
                          <strong>{selectedSession.assignedStaffName}</strong>
                        </span>
                      )}
                      {selectedSession.priorityLevel && (
                        <span className="meta-item">
                          Cấp ưu tiên:{" "}
                          <strong>{selectedSession.priorityLevel}</strong>
                        </span>
                      )}
                    </div>
                    <div className="chat-meta-sub">
                      {getStatusTextForHeader(selectedSession)}
                    </div>
                  </div>
                </div>
              </header>

              <div className="chat-body">
                <div className="chat-messages">
                  {loadingMessages && (
                    <div className="empty small">Đang tải tin nhắn...</div>
                  )}
                  {!loadingMessages && messages.length === 0 && (
                    <div className="empty small">
                      Chưa có tin nhắn nào trong phiên chat này.
                    </div>
                  )}

                  {!loadingMessages &&
                    messages.map((m) => {
                      const timeText = formatTimeShort(m.sentAt);
                      const isStaff = !!m.isFromStaff;
                      return (
                        <div
                          key={m.messageId}
                          className={
                            "msg-row" + (isStaff ? " msg-row-staff" : " msg-row-customer")
                          }
                        >
                          <div
                            className={
                              "msg" + (isStaff ? " msg-staff" : " msg-customer")
                            }
                          >
                            <div className="msg-meta">
                              <span className="msg-sender">
                                {m.senderName || (isStaff ? "Nhân viên" : "Khách hàng")}
                              </span>
                              {timeText && (
                                <span className="msg-time">{timeText}</span>
                              )}
                            </div>
                            <div className="msg-bubble">{m.content}</div>
                          </div>
                        </div>
                      );
                    })}
                  <div ref={messagesEndRef} />
                </div>

                <form className="chat-input" onSubmit={handleSendMessage}>
                  <textarea
                    className="ip ip-textarea"
                    placeholder={
                      selectedSession?.status?.toLowerCase() === "closed"
                        ? "Phiên chat đã đóng, không thể gửi thêm tin nhắn."
                        : "Nhập nội dung tin nhắn..."
                    }
                    value={newMessage}
                    onChange={(e) => setNewMessage(e.target.value)}
                    disabled={
                      sending ||
                      !selectedSession ||
                      String(selectedSession.status || "").toLowerCase() ===
                        "closed"
                    }
                    rows={3}
                  />
                  <div className="chat-input-actions">
                    <button
                      type="submit"
                      className="btn primary"
                      disabled={
                        sending ||
                        !newMessage.trim() ||
                        !selectedSession ||
                        String(selectedSession.status || "").toLowerCase() ===
                          "closed"
                      }
                    >
                      {sending ? "Đang gửi..." : "Gửi"}
                    </button>
                  </div>
                </form>
              </div>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
