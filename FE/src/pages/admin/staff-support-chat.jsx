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
      raw.customerName ||
      raw.CustomerName ||
      raw.customerEmail ||
      raw.CustomerEmail ||
      "Khách hàng",
    customerEmail: raw.customerEmail || raw.CustomerEmail || "",
    assignedStaffName: raw.assignedStaffName || raw.AssignedStaffName || "",
    status: raw.status || raw.Status || "",
    priorityLevel:
      raw.priorityLevel ?? raw.PriorityLevel ?? raw.priority ?? null,
    lastMessagePreview: raw.lastMessagePreview || raw.LastMessagePreview || "",
    lastMessageAt: raw.lastMessageAt || raw.LastMessageAt || null,
    startedAt: raw.startedAt || raw.StartedAt || null,
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
    sentAt:
      raw.sentAt ||
      raw.SentAt ||
      raw.createdAt ||
      raw.CreatedAt ||
      null,
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

// helper đọc tab từ query string (?tab=unassigned|mine)
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
  const isAdmin = false; // Staff bị hạn chế, không xem closed

  const [searchParams, setSearchParams] = useSearchParams();
  const initialSelectedId = searchParams.get("sessionId") || null;
  const initialActiveTab = getTabFromQuery(searchParams) || "unassigned";

  const [activeTab, setActiveTab] = useState(initialActiveTab); // "unassigned" | "mine"
  const [includeClosed] = useState(false); // staff không dùng, luôn false

  const [queue, setQueue] = useState([]); // hàng chờ chưa nhận
  const [mine, setMine] = useState([]); // phiên của tôi

  const [selectedSessionId, setSelectedSessionId] = useState(initialSelectedId);
  const [messages, setMessages] = useState([]);

  const [loadingQueue, setLoadingQueue] = useState(false);
  const [loadingMine, setLoadingMine] = useState(false);
  const [loadingMessages, setLoadingMessages] = useState(false);

  const [sending, setSending] = useState(false);
  const [newMessage, setNewMessage] = useState("");

  const [stateText, setStateText] = useState("");
  const [errorText, setErrorText] = useState("");

  // ==== Scroll state cho khung chat giống ticket detail ====
  const messagesRef = useRef(null);
  const isAtBottomRef = useRef(true);
  const initialScrollDoneRef = useRef(false);

  // Connection & group state
  const [connection, setConnection] = useState(null);
  const joinedSessionIdRef = useRef(null);

  const effectiveIncludeClosed = isAdmin && includeClosed; // staff => luôn false

  // Đồng bộ selectedSessionId với query param ?sessionId=...
  useEffect(() => {
    const paramId = searchParams.get("sessionId") || null;
    setSelectedSessionId((prev) => (prev === paramId ? prev : paramId));
  }, [searchParams]);

  // Đồng bộ tab với query param ?tab=...
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
      const rawItems = Array.isArray(res?.items ?? res?.Items)
        ? res.items ?? res.Items
        : Array.isArray(res)
        ? res
        : [];
      const mapped = rawItems.map(normalizeSession).filter(Boolean);
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

  const loadMine = useCallback(
    async () => {
      setLoadingMine(true);
      try {
        const res = await supportChatApi.getMySessions({
          includeClosed: effectiveIncludeClosed,
        });
        const rawItems = Array.isArray(res?.items ?? res?.Items)
          ? res.items ?? res.Items
          : Array.isArray(res)
          ? res
          : [];
        const mapped = rawItems.map(normalizeSession).filter(Boolean);
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
    },
    [effectiveIncludeClosed]
  );

  // ---- Load messages ----
  const loadMessages = useCallback(async (sessionId) => {
    if (!sessionId) {
      setMessages([]);
      return;
    }

    setLoadingMessages(true);
    try {
      const res = await supportChatApi.getMessages(sessionId);
      const rawItems = Array.isArray(res?.items ?? res?.Items)
        ? res.items ?? res.Items
        : Array.isArray(res)
        ? res
        : [];
      const mapped = rawItems.map(normalizeMessage).filter(Boolean);
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

  const refreshAll = useCallback(async () => {
    setStateText("Đang tải dữ liệu...");
    setErrorText("");
    await Promise.all([loadQueue(), loadMine()]);
    setStateText("");
  }, [loadQueue, loadMine]);

  // ---- SignalR connection (khởi tạo 1 lần) ----
  useEffect(() => {
    let stopped = false;
    let connInstance = null;

    const setupConnection = async () => {
      try {
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
        // ✅ Khớp BE: MapHub<SupportChatHub>("/hubs/support-chat")
        const hubUrl = `${hubRoot}/hubs/support-chat`;

        const conn = new HubConnectionBuilder()
          .withUrl(hubUrl, {
            accessTokenFactory: () => {
              try {
                const raw =
                  localStorage.getItem("access_token") ||
                  localStorage.getItem("token") ||
                  sessionStorage.getItem("token") ||
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

        connInstance = conn;

        // Handlers
        const handleIncomingMessage = (raw) => {
          const msg = normalizeMessage(raw);
          if (!msg) return;

          // Cập nhật preview ở list
          setQueue((prev) =>
            prev.map((s) =>
              s.chatSessionId === msg.chatSessionId
                ? {
                    ...s,
                    lastMessagePreview: msg.content,
                    lastMessageAt: msg.sentAt ?? s.lastMessageAt,
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
                    lastMessageAt: msg.sentAt ?? s.lastMessageAt,
                  }
                : s
            )
          );

          // Chỉ push vào panel chat nếu đang mở đúng session
          if (joinedSessionIdRef.current !== msg.chatSessionId) {
            return;
          }

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
        };

        const handleSessionUpdated = (raw) => {
          const s = normalizeSession(raw);
          if (!s) return;

          setQueue((prev) => {
            const exist = prev.some(
              (x) => x.chatSessionId === s.chatSessionId
            );
            if (!exist) return prev;
            return prev.map((x) =>
              x.chatSessionId === s.chatSessionId ? { ...x, ...s } : x
            );
          });
          setMine((prev) => {
            const exist = prev.some(
              (x) => x.chatSessionId === s.chatSessionId
            );
            if (!exist) return prev;
            return prev.map((x) =>
              x.chatSessionId === s.chatSessionId ? { ...x, ...s } : x
            );
          });
        };

        const handleSessionCreated = (raw) => {
          const s = normalizeSession(raw);
          if (!s) return;
          setQueue((prev) => [s, ...prev]);
        };

        const handleSessionClosed = (raw) => {
          const s = normalizeSession(raw);
          if (!s) return;
          setQueue((prev) =>
            prev.filter((x) => x.chatSessionId !== s.chatSessionId)
          );
          setMine((prev) =>
            prev.filter((x) => x.chatSessionId !== s.chatSessionId)
          );

          if (joinedSessionIdRef.current === s.chatSessionId) {
            joinedSessionIdRef.current = null;
            setSelectedSessionId(null);
          }
        };

        conn.on("SupportMessageReceived", handleIncomingMessage);
        conn.on("ReceiveSupportMessage", handleIncomingMessage);
        conn.on("ReceiveSupportChatMessage", handleIncomingMessage); // legacy
        conn.on("SupportSessionUpdated", handleSessionUpdated);
        conn.on("SupportSessionCreated", handleSessionCreated);
        conn.on("SupportSessionClosed", handleSessionClosed);

        conn.onclose((e) => {
          console.warn("[SupportChat] SignalR connection closed:", e);
        });

        await conn.start();
        if (stopped) {
          await conn.stop().catch(() => {});
          return;
        }

        // ✅ Staff join group queue để nhận realtime hàng chờ
        try {
          await conn.invoke("JoinStaffQueue");
        } catch (err) {
          console.error("[SupportChat] JoinStaffQueue failed:", err);
        }

        setConnection(conn);
      } catch (e) {
        console.error("Failed to setup SupportChat SignalR connection:", e);
      }
    };

    setupConnection();

    return () => {
      stopped = true;
      if (connInstance) {
        connInstance
          .stop()
          .catch((e) =>
            console.error("Error stopping SupportChat SignalR connection:", e)
          );
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // chỉ khởi tạo 1 lần

  // ---- Join/leave session group khi selectedSessionId hoặc connection thay đổi ----
  useEffect(() => {
    if (!connection) return;

    const run = async () => {
      try {
        if (
          joinedSessionIdRef.current &&
          joinedSessionIdRef.current !== selectedSessionId
        ) {
          await connection.invoke(
            "LeaveSession",
            joinedSessionIdRef.current
          );
          joinedSessionIdRef.current = null;
        }

        if (selectedSessionId) {
          await connection.invoke("JoinSession", selectedSessionId);
          joinedSessionIdRef.current = selectedSessionId;
        }
      } catch (e) {
        console.error("Failed to join/leave support session group:", e);
      }
    };

    run();
  }, [connection, selectedSessionId]);

  // 🧷 Theo dõi scroll trong khung chat – giống thread ticket detail
  const handleMessagesScroll = () => {
    const el = messagesRef.current;
    if (!el) return;
    const threshold = 20; // px – cho phép lệch chút vẫn coi như ở đáy
    const distanceToBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    isAtBottomRef.current = distanceToBottom <= threshold;
  };

  // 🧷 Auto scroll
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
  }, [messages, selectedSessionId]);

  // ---- Load list lần đầu ----
  useEffect(() => {
    refreshAll();
  }, [refreshAll]);

  // ---- Khi chọn session thì load messages ----
  useEffect(() => {
    initialScrollDoneRef.current = false;
    isAtBottomRef.current = true;

    if (!selectedSessionId) {
      setMessages([]);
      return;
    }
    loadMessages(selectedSessionId);
  }, [selectedSessionId, loadMessages]);

  // ---- Helpers: select session + sync URL ----

  const handleSelectSession = (sessionId) => {
    const id = sessionId || null;
    setSelectedSessionId(id);

    const next = new URLSearchParams(searchParams);
    if (id) {
      next.set("sessionId", id);
    } else {
      next.delete("sessionId");
    }
    setSearchParams(next, { replace: false });
  };

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
        e?.response?.data?.message ||
          e.message ||
          "Trả lại phiên chat thất bại."
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

  const handleSend = async (e) => {
    e.preventDefault();
    if (!selectedSession) return;

    const text = (newMessage || "").trim();
    if (!text) return;

    setSending(true);
    setErrorText("");

    try {
      const saved = await supportChatApi.createMessage(
        selectedSession.chatSessionId,
        { content: text }
      );

      const msg = normalizeMessage(saved) || saved;
      setNewMessage("");

      if (msg) {
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
      }
    } catch (e2) {
      console.error(e2);
      setErrorText(
        e2?.response?.data?.message ||
          e2.message ||
          "Không gửi được tin nhắn. Vui lòng thử lại."
      );
    } finally {
      setSending(false);
    }
  };

  const sessionStatusText = getStatusTextForHeader(selectedSession);

  const canSend =
    !!selectedSession &&
    String(selectedSession.status || "")
      .toLowerCase() !== "closed";

  // ---- Render helpers ----

  const renderSessionItem = (s, isQueue) => {
    if (!s) return null;
    const isSelected = selectedSessionId === s.chatSessionId;
    const firstChar = (s.customerName || "K")[0]?.toUpperCase?.() || "K";

    const time =
      s.lastMessageAt || s.startedAt
        ? formatTimeShort(s.lastMessageAt || s.startedAt)
        : "";

    return (
      <div
        key={s.chatSessionId}
        className={
          "session-item" + (isSelected ? " session-item-selected" : "")
        }
        onClick={() => handleSelectSession(s.chatSessionId)}
      >
        <div className="session-avatar">{firstChar}</div>
        <div className="session-info">
          <div className="session-line1">
            <span className="session-customer">{s.customerName}</span>
            {time && <span className="session-time">{time}</span>}
          </div>
          <div className="session-line2">
            <span className="session-status">{getStatusLabel(s)}</span>
            <span className="session-priority">
              {getPriorityLabel(s.priorityLevel)}
            </span>
          </div>
          <div className="session-preview">
            {s.lastMessagePreview || "Chưa có tin nhắn."}
          </div>
        </div>
        <div className="session-actions">
          {isQueue && (
            <button
              type="button"
              className="btn-xs-primary"
              onClick={(e) => {
                e.stopPropagation();
                handleClaim(s.chatSessionId);
              }}
            >
              Nhận
            </button>
          )}
        </div>
      </div>
    );
  };

  const renderMessages = () => {
    if (!selectedSession) {
      return (
        <div className="chat-empty">
          Chọn một phiên chat ở bên trái để bắt đầu.
        </div>
      );
    }

    return (
      <div
        className="chat-messages"
        ref={messagesRef}
        onScroll={handleMessagesScroll}
      >
        {loadingMessages && !messages.length && (
          <div className="empty small">Đang tải tin nhắn...</div>
        )}

        {!loadingMessages && messages.length === 0 && (
          <div className="empty small">Chưa có tin nhắn nào.</div>
        )}

        {messages.map((msg) => {
          const key = msg.messageId || `${msg.chatSessionId}_${msg.sentAt}`;
          const rowCls =
            "msg-row " +
            (msg.isFromStaff ? "msg-row-staff" : "msg-row-customer");
          const msgCls =
            "msg " + (msg.isFromStaff ? "msg-staff" : "msg-customer");

          return (
            <div key={key} className={rowCls}>
              <div className={msgCls}>
                <div className="msg-meta">
                  <span>
                    {msg.isFromStaff ? "Bạn" : msg.senderName || "Khách"}
                  </span>
                  <span>{formatTimeShort(msg.sentAt)}</span>
                </div>
                <div className="msg-bubble">{msg.content}</div>
              </div>
            </div>
          );
        })}
      </div>
    );
  };

  // ---- Render ----

  return (
    <div className="support-chat-page">
      <div className="support-chat-header">
        <div>
          <h1 className="page-title">{pageTitle}</h1>
          <div className="support-chat-header-stats">
            <span>Chờ nhận: {queue.length}</span>
            <span>• Phiên của tôi: {mine.length}</span>
          </div>
        </div>
        <div className="support-chat-header-actions">
          <button type="button" className="tab" onClick={refreshAll}>
            Làm mới
          </button>
        </div>
      </div>

      <div className="support-chat-state">
        {stateText && <span className="state-text">{stateText}</span>}
        {errorText && <span className="error-text">{errorText}</span>}
      </div>

      <div className="support-chat-layout">
        {/* Sidebar */}
        <div className="support-chat-sidebar">
          <div className="tabs">
            <button
              type="button"
              className={
                "tab" + (activeTab === "unassigned" ? " tab-active" : "")
              }
              onClick={() => handleChangeTab("unassigned")}
            >
              Chờ nhận
              <span className="badge">{queue.length}</span>
            </button>
            <button
              type="button"
              className={
                "tab" + (activeTab === "mine" ? " tab-active" : "")
              }
              onClick={() => handleChangeTab("mine")}
            >
              Của tôi
              <span className="badge">{mine.length}</span>
            </button>
          </div>

          <div className="sidebar-toolbar">
            <span className="muted">
              {activeTab === "unassigned"
                ? "Các phiên chat đang chờ nhân viên nhận."
                : "Các phiên chat bạn đang phụ trách."}
            </span>
          </div>

          <div className="session-list">
            {activeTab === "unassigned" && (
              <>
                {loadingQueue && (
                  <div className="empty small">Đang tải hàng chờ...</div>
                )}
                {!loadingQueue && queue.length === 0 && (
                  <div className="empty">
                    Chưa có phiên chat nào trong hàng chờ.
                  </div>
                )}
                {!loadingQueue &&
                  queue.map((s) => renderSessionItem(s, true))}
              </>
            )}

            {activeTab === "mine" && (
              <>
                {loadingMine && (
                  <div className="empty small">Đang tải phiên của bạn...</div>
                )}
                {!loadingMine && mine.length === 0 && (
                  <div className="empty">Bạn chưa có phiên chat nào.</div>
                )}
                {!loadingMine &&
                  mine.map((s) => renderSessionItem(s, false))}
              </>
            )}
          </div>
        </div>

        {/* Main chat */}
        <div className="support-chat-main">
          {!selectedSession && (
            <div className="chat-empty">
              Chọn một phiên chat ở cột bên trái để bắt đầu hỗ trợ khách.
            </div>
          )}

          {selectedSession && (
            <div className="chat-panel">
              <div className="chat-header">
                <div className="chat-header-main">
                  <div className="chat-avatar">
                    {(selectedSession.customerName || "K")
                      .substring(0, 1)
                      .toUpperCase()}
                  </div>
                  <div>
                    <div className="chat-customer-name">
                      {selectedSession.customerName}
                    </div>
                    <div className="chat-meta">
                      <span className="meta-item">
                        <strong>Trạng thái:</strong>{" "}
                        {getStatusLabel(selectedSession)}
                      </span>
                      {selectedSession.priorityLevel !== undefined && (
                        <span className="meta-item">
                          <strong>Ưu tiên:</strong>{" "}
                          {getPriorityLabel(selectedSession.priorityLevel)}
                        </span>
                      )}
                      {selectedSession.customerEmail && (
                        <span className="meta-item">
                          <strong>Email:</strong>{" "}
                          {selectedSession.customerEmail}
                        </span>
                      )}
                    </div>
                    {sessionStatusText && (
                      <div className="chat-meta-sub">{sessionStatusText}</div>
                    )}
                  </div>
                </div>
              </div>

              <div className="chat-body">
                {renderMessages()}

                <form className="chat-footer" onSubmit={handleSend}>
                  <textarea
                    className="chat-input"
                    placeholder={
                      canSend
                        ? "Nhập nội dung tin nhắn..."
                        : "Phiên chat đã đóng, không thể gửi thêm."
                    }
                    value={newMessage}
                    onChange={(e) => setNewMessage(e.target.value)}
                    disabled={!canSend || sending}
                  />
                  <div className="chat-footer-actions">
                    <div className="chat-footer-row">
                      <button
                        type="submit"
                        className="btn primary"
                        disabled={!canSend || sending}
                      >
                        {sending ? "Đang gửi..." : "Gửi"}
                      </button>
                    </div>
                    <div className="chat-footer-row secondary">
                      <button
                        type="button"
                        className="btn ghost"
                        disabled={!selectedSession}
                        onClick={() =>
                          selectedSession &&
                          handleUnassign(selectedSession.chatSessionId)
                        }
                      >
                        Trả lại hàng chờ
                      </button>
                      <button
                        type="button"
                        className="btn danger"
                        disabled={!selectedSession}
                        onClick={() =>
                          selectedSession &&
                          handleClose(selectedSession.chatSessionId)
                        }
                      >
                        Đóng phiên
                      </button>
                    </div>
                    {errorText && (
                      <div className="error-text chat-error">{errorText}</div>
                    )}
                  </div>
                </form>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
