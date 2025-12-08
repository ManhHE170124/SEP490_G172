// File: src/pages/admin/admin-support-chat.jsx
import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { useSearchParams } from "react-router-dom";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { createPortal } from "react-dom";
import axiosClient from "../../api/axiosClient";
import { supportChatApi } from "../../api/supportChatApi";
import { ticketsApi } from "../../api/ticketsApi";
import "../../styles/staff-support-chat.css";

// ⚠️ Điều chỉnh lại path cho đúng với project của bạn
import ConfirmDialog from "../../components/ConfirmDialog/ConfirmDialog";
import Toast from "../../components/Toast/Toast";
import PermissionGuard from "../../components/PermissionGuard";
import { usePermission } from "../../hooks/usePermission";
import useToast from "../../hooks/useToast";

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
    customerId: raw.customerId || raw.CustomerId || null,
    customerName:
      raw.customerName ||
      raw.CustomerName ||
      raw.customerEmail ||
      raw.CustomerEmail ||
      "Khách hàng",
    customerEmail: raw.customerEmail || raw.CustomerEmail || "",
    assignedStaffId: raw.assignedStaffId || raw.AssignedStaffId || null,
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

  if (status === "waiting") {
    return "Phiên chat đang chờ nhân viên nhận.";
  }
  if (status === "open" || status === "active") {
    return "Bạn đang xem và có thể hỗ trợ khách trong phiên chat này.";
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

// helper đọc tab từ query string (?tab=unassigned|assigned)
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

    if (raw === "unassigned" || raw === "assigned") {
      return raw;
    }

    return null;
  } catch {
    return null;
  }
}

// ---- Main component: Admin Support Chat Page ----

export default function AdminSupportChatPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const initialSelectedId = searchParams.get("sessionId") || null;
  const initialActiveTab = getTabFromQuery(searchParams) || "unassigned";
  const { showError } = useToast();
  const { hasPermission: hasEditPermission } = usePermission("SUPPORT_MANAGER", "EDIT");

  const [activeTab, setActiveTab] = useState(initialActiveTab); // "unassigned" | "assigned"

  const [queue, setQueue] = useState([]); // hàng chờ chưa nhận
  const [assigned, setAssigned] = useState([]); // tất cả phiên đã được bất kỳ staff nào nhận

  const [selectedSessionId, setSelectedSessionId] = useState(initialSelectedId);
  const [messages, setMessages] = useState([]);

  const [loadingQueue, setLoadingQueue] = useState(false);
  const [loadingAssigned, setLoadingAssigned] = useState(false);
  const [loadingMessages, setLoadingMessages] = useState(false);

  const [sending, setSending] = useState(false);
  const [newMessage, setNewMessage] = useState("");

  const [stateText, setStateText] = useState("");
  const [errorText, setErrorText] = useState("");

  // ==== Toast & Confirm dialog state (MỚI) ====
  const [toasts, setToasts] = useState([]);
  const [confirmState, setConfirmState] = useState({
    isOpen: false,
    title: "",
    message: "",
    onConfirm: null,
  });

  // ==== Scroll state cho khung chat giống ticket detail ====
  const messagesRef = useRef(null);
  const isAtBottomRef = useRef(true);
  const initialScrollDoneRef = useRef(false);

  // Connection & group state
  const [connection, setConnection] = useState(null);
  const joinedSessionIdRef = useRef(null);

  // ---- State cho panel "Các phiên chat trước với user này" ----
  const [previousSessions, setPreviousSessions] = useState([]);
  const [loadingPreviousSessions, setLoadingPreviousSessions] = useState(false);
  const [previewSession, setPreviewSession] = useState(null);
  const [previewMessages, setPreviewMessages] = useState([]);
  const [loadingPreviewMessages, setLoadingPreviewMessages] = useState(false);

  // ---- Modal assign/transfer staff ----
  const [assignModal, setAssignModal] = useState({
    open: false,
    mode: "", // 'assign' | 'transfer'
    sessionId: null,
    excludeUserId: null,
  });

  // ==== Toast helpers ====
  const showToast = useCallback((type, title, message) => {
    const id = `${Date.now()}_${Math.random().toString(36).slice(2)}`;
    const toast = { id, type, title, message };
    setToasts((prev) => [...prev, toast]);
    // Auto close sau 4s
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, 4000);
  }, []);

  const handleRemoveToast = (id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  // ==== Confirm dialog helpers ====
  const openConfirm = (title, message, onConfirm) => {
    setConfirmState({
      isOpen: true,
      title,
      message,
      onConfirm,
    });
  };

  const handleConfirmCancel = () => {
    setConfirmState((prev) => ({ ...prev, isOpen: false, onConfirm: null }));
  };

  const handleConfirmOk = () => {
    if (confirmState.onConfirm) {
      confirmState.onConfirm();
    }
    setConfirmState((prev) => ({ ...prev, isOpen: false, onConfirm: null }));
  };

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
      assigned.find((s) => s.chatSessionId === selectedSessionId) ||
      null
    );
  }, [queue, assigned, selectedSessionId]);

  const pageTitle = "Chat hỗ trợ (Admin)";

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
      const msg =
        e?.response?.data?.message ||
        e.message ||
        "Không tải được danh sách hàng chờ.";
      setErrorText(msg);
      showToast("error", "Lỗi", msg);
    } finally {
      setLoadingQueue(false);
    }
  }, [showToast]);

  const loadAssigned = useCallback(async () => {
    setLoadingAssigned(true);
    try {
      const res = await supportChatApi.adminGetAssignedSessions({
        includeClosed: false,
      });
      const rawItems = Array.isArray(res?.items ?? res?.Items)
        ? res.items ?? res.Items
        : Array.isArray(res)
        ? res
        : [];
      const mapped = rawItems.map(normalizeSession).filter(Boolean);
      setAssigned(mapped);
    } catch (e) {
      console.error(e);
      const msg =
        e?.response?.data?.message ||
        e.message ||
        "Không tải được danh sách phiên đã nhận.";
      setErrorText(msg);
      showToast("error", "Lỗi", msg);
    } finally {
      setLoadingAssigned(false);
    }
  }, [showToast]);

  // ---- Load messages ----
  const loadMessages = useCallback(
    async (sessionId) => {
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
        const msg =
          e?.response?.data?.message ||
          e.message ||
          "Không tải được lịch sử tin nhắn.";
        setErrorText(msg);
        showToast("error", "Lỗi", msg);
      } finally {
        setLoadingMessages(false);
      }
    },
    [showToast]
  );

  const refreshAll = useCallback(async () => {
    setStateText("Đang tải dữ liệu...");
    setErrorText("");
    await Promise.all([loadQueue(), loadAssigned()]);
    setStateText("");
    showToast("success", "Đã làm mới", "Dữ liệu chat đã được cập nhật.");
  }, [loadQueue, loadAssigned, showToast]);

  // ---- Admin assign / transfer helpers ----

  const doAdminAssign = async (sessionId, assigneeId) => {
    if (!hasEditPermission) {
      showError("Không có quyền", "Bạn không có quyền gán nhân viên cho phiên chat");
      return;
    }
    if (!sessionId || !assigneeId) return;
    try {
      setStateText("Đang gán nhân viên cho phiên chat...");
      await supportChatApi.adminAssignStaff(sessionId, assigneeId);
      await refreshAll();
      showToast(
        "success",
        "Gán nhân viên thành công",
        "Đã gán nhân viên cho phiên chat."
      );
    } catch (e) {
      console.error(e);
      const msg =
        e?.response?.data?.message ||
        e.message ||
        "Gán nhân viên cho phiên chat thất bại.";
      setErrorText(msg);
      showToast("error", "Lỗi", msg);
    } finally {
      setStateText("");
    }
  };

  const doAdminTransfer = async (sessionId, assigneeId) => {
    if (!hasEditPermission) {
      showError("Không có quyền", "Bạn không có quyền chuyển nhân viên phụ trách phiên chat");
      return;
    }
    if (!sessionId || !assigneeId) return;
    try {
      setStateText("Đang chuyển nhân viên phụ trách...");
      await supportChatApi.adminTransferStaff(sessionId, assigneeId);
      await refreshAll();
      showToast(
        "success",
        "Chuyển nhân viên thành công",
        "Đã chuyển nhân viên phụ trách phiên chat."
      );
    } catch (e) {
      console.error(e);
      const msg =
        e?.response?.data?.message ||
        e.message ||
        "Chuyển nhân viên phụ trách phiên chat thất bại.";
      setErrorText(msg);
      showToast("error", "Lỗi", msg);
    } finally {
      setStateText("");
    }
  };

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
        // Khớp BE: MapHub<SupportChatHub>("/hubs/support-chat")
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
          setAssigned((prev) =>
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

        const upsertIntoLists = (rawSession) => {
          const s = normalizeSession(rawSession);
          if (!s) return;

          const status = String(s.status || "").toLowerCase();
          const isWaitingUnassigned =
            status === "waiting" && !s.assignedStaffId;
          const isAssignedActive = !!s.assignedStaffId && status !== "closed";

          // Queue (chờ nhận)
          setQueue((prev) => {
            let next = [...prev];
            const idx = next.findIndex(
              (x) => x.chatSessionId === s.chatSessionId
            );
            if (isWaitingUnassigned) {
              if (idx >= 0) next[idx] = s;
              else next.push(s);
            } else if (idx >= 0) {
              next.splice(idx, 1);
            }
            return next;
          });

          // Đã nhận: tất cả phiên có assignedStaffId và chưa đóng
          setAssigned((prev) => {
            let next = [...prev];
            const idx = next.findIndex(
              (x) => x.chatSessionId === s.chatSessionId
            );
            if (isAssignedActive) {
              if (idx >= 0) next[idx] = s;
              else next.unshift(s);
            } else if (idx >= 0) {
              next.splice(idx, 1);
            }
            return next;
          });
        };

        const handleSessionUpdated = (raw) => {
          upsertIntoLists(raw);
        };

        const handleSessionCreated = (raw) => {
          upsertIntoLists(raw);
        };

        const handleSessionClosed = (raw) => {
          const s = normalizeSession(raw);
          if (!s) return;

          setQueue((prev) =>
            prev.filter((x) => x.chatSessionId !== s.chatSessionId)
          );
          setAssigned((prev) =>
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

        // Admin vẫn join group queue để nhận realtime hàng chờ
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
  }, []);

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

    // Lần đầu load messages cho session hiện tại: luôn kéo xuống cuối
    if (!initialScrollDoneRef.current) {
      scrollToBottom();
      initialScrollDoneRef.current = true;
      isAtBottomRef.current = true;
      return;
    }

    // Các lần sau: chỉ auto scroll nếu đang ở đáy
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

  // ---- Load các phiên chat trước của cùng customer cho side panel ----
  useEffect(() => {
    // Reset khi đổi session
    setPreviousSessions([]);
    setPreviewSession(null);
    setPreviewMessages([]);
    setLoadingPreviousSessions(false);
    setLoadingPreviewMessages(false);

    if (!selectedSession || !selectedSession.customerId) {
      return;
    }

    let cancelled = false;

    const fetchPrevious = async () => {
      setLoadingPreviousSessions(true);
      try {
        const res = await supportChatApi.getCustomerSessions(
          selectedSession.customerId,
          {
            includeClosed: true,
            excludeSessionId: selectedSession.chatSessionId,
          }
        );

        const rawItems = Array.isArray(res?.items ?? res?.Items)
          ? res.items ?? res.Items
          : Array.isArray(res)
          ? res
          : [];

        const mapped = rawItems.map(normalizeSession).filter(Boolean);
        if (!cancelled) {
          setPreviousSessions(mapped);
        }
      } catch (e) {
        if (!cancelled) {
          console.error(e);
          const msg =
            e?.response?.data?.message ||
            e.message ||
            "Không tải được danh sách phiên chat trước.";
          setErrorText(msg);
          showToast("error", "Lỗi", msg);
        }
      } finally {
        if (!cancelled) {
          setLoadingPreviousSessions(false);
        }
      }
    };

    fetchPrevious();

    return () => {
      cancelled = true;
    };
  }, [selectedSession, showToast]);

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
    if (nextTab !== "unassigned" && nextTab !== "assigned") return;

    setActiveTab(nextTab);

    const next = new URLSearchParams(searchParams);
    next.set("tab", nextTab);
    setSearchParams(next, { replace: false });
  };

  // ---- Actions ----

  // Admin "Gán" nhân viên → mở popup
  const handleOpenAssignModalForSession = (session) => {
    if (!session?.chatSessionId) return;
    setAssignModal({
      open: true,
      mode: "assign",
      sessionId: session.chatSessionId,
      excludeUserId: null,
    });
  };

  // Admin "Chuyển nhân viên" → popup, exclude current staff
  const handleOpenTransferModalForSession = (session) => {
    if (!session?.chatSessionId) return;
    if (!session.assignedStaffId) return;
    setAssignModal({
      open: true,
      mode: "transfer",
      sessionId: session.chatSessionId,
      excludeUserId: session.assignedStaffId,
    });
  };

  // TÁCH logic cũ ra hàm riêng, dùng ConfirmDialog để xác nhận
  const doUnassign = async (sessionId) => {
    if (!hasEditPermission) {
      showError("Không có quyền", "Bạn không có quyền trả lại phiên chat về hàng chờ");
      return;
    }
    if (!sessionId) return;

    try {
      setStateText("Đang trả lại phiên chat...");
      await supportChatApi.unassignSession(sessionId);
      await refreshAll();
      handleSelectSession(null);
      showToast(
        "success",
        "Đã trả lại phiên chat",
        "Phiên chat đã được trả lại hàng chờ."
      );
    } catch (e) {
      console.error(e);
      const msg =
        e?.response?.data?.message ||
        e.message ||
        "Trả lại phiên chat thất bại.";
      setErrorText(msg);
      showToast("error", "Lỗi", msg);
    } finally {
      setStateText("");
    }
  };

  const handleUnassign = (sessionId) => {
    if (!hasEditPermission) {
      showError("Không có quyền", "Bạn không có quyền trả lại phiên chat về hàng chờ");
      return;
    }
    if (!sessionId) return;
    openConfirm(
      "Trả lại phiên chat về hàng chờ",
      "Bạn có chắc muốn trả lại phiên chat này về hàng chờ? Khách sẽ không nhận được phản hồi từ bạn nữa.",
      () => doUnassign(sessionId)
    );
  };

  const doClose = async (sessionId) => {
    if (!hasEditPermission) {
      showError("Không có quyền", "Bạn không có quyền đóng phiên chat");
      return;
    }
    if (!sessionId) return;

    try {
      setStateText("Đang đóng phiên chat...");
      await supportChatApi.closeSession(sessionId);
      await refreshAll();
      handleSelectSession(null);
      showToast(
        "success",
        "Đã đóng phiên chat",
        "Phiên chat đã được đóng thành công."
      );
    } catch (e) {
      console.error(e);
      const msg =
        e?.response?.data?.message ||
        e.message ||
        "Đóng phiên chat thất bại.";
      setErrorText(msg);
      showToast("error", "Lỗi", msg);
    } finally {
      setStateText("");
    }
  };

  const handleClose = (sessionId) => {
    if (!hasEditPermission) {
      showError("Không có quyền", "Bạn không có quyền đóng phiên chat");
      return;
    }
    if (!sessionId) return;
    openConfirm(
      "Đóng phiên chat",
      "Bạn có chắc muốn đóng phiên chat này không?",
      () => doClose(sessionId)
    );
  };

  // ✅ Admin gửi message: luôn có thể gửi trong Chờ nhận / Đã nhận,
  // và dùng API adminPostMessage để KHÔNG đổi AssignedStaff/Status
  const handleSend = async (e) => {
    e.preventDefault();
    if (!hasEditPermission) {
      showError("Không có quyền", "Bạn không có quyền gửi tin nhắn");
      return;
    }
    if (!selectedSession) return;

    const text = (newMessage || "").trim();
    if (!text) return;

    setSending(true);
    setErrorText("");

    try {
      const saved = await supportChatApi.adminPostMessage(
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
      showToast("success", "Đã gửi tin nhắn", "Tin nhắn đã được gửi tới khách.");
    } catch (e2) {
      console.error(e2);
      const msg =
        e2?.response?.data?.message ||
        e2.message ||
        "Không gửi được tin nhắn. Vui lòng thử lại.";
      setErrorText(msg);
      showToast("error", "Lỗi", msg);
    } finally {
      setSending(false);
    }
  };

  const handleOpenTranscript = async (session) => {
    if (!session || !session.chatSessionId) return;

    setPreviewSession(session);
    setPreviewMessages([]);
    setLoadingPreviewMessages(true);

    try {
      const res = await supportChatApi.getMessages(session.chatSessionId);
      const rawItems = Array.isArray(res?.items ?? res?.Items)
        ? res.items ?? res.Items
        : Array.isArray(res)
        ? res
        : [];
      const mapped = rawItems.map(normalizeMessage).filter(Boolean);
      setPreviewMessages(mapped);
    } catch (e) {
      console.error(e);
      const msg =
        e?.response?.data?.message ||
        e.message ||
        "Không tải được transcript phiên chat trước.";
      setErrorText(msg);
      showToast("error", "Lỗi", msg);
    } finally {
      setLoadingPreviewMessages(false);
    }
  };

  const sessionStatusText = getStatusTextForHeader(selectedSession);

  // ✅ Admin: chỉ cần phiên không closed là gửi được (không phụ thuộc "của tôi")
  const canSend =
    !!selectedSession &&
    String(selectedSession.status || "").toLowerCase() !== "closed";

  // ---- Render helpers ----

  const renderSessionItem = (s, isQueue) => {
    if (!s) return null;
    const isSelected = selectedSessionId === s.chatSessionId;
    const firstChar = (s.customerName || "K")[0]?.toUpperCase?.() || "K";

    let timeLabel = "";
    if (s.lastMessageAt) {
      timeLabel = `Tin cuối: ${formatTimeShort(s.lastMessageAt)}`;
    } else if (s.startedAt) {
      timeLabel = `Bắt đầu: ${formatTimeShort(s.startedAt)}`;
    }

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
            {timeLabel && <span className="session-time">{timeLabel}</span>}
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
            <PermissionGuard moduleCode="SUPPORT_MANAGER" permissionCode="EDIT" fallback={
              <button
                type="button"
                className="btn-xs-primary disabled"
                disabled
                title="Bạn không có quyền gán nhân viên"
                onClick={(e) => {
                  e.stopPropagation();
                  showError("Không có quyền", "Bạn không có quyền gán nhân viên cho phiên chat");
                }}
              >
                Gán
              </button>
            }>
              <button
                type="button"
                className="btn-xs-primary"
                onClick={(e) => {
                  e.stopPropagation();
                  handleOpenAssignModalForSession(s);
                }}
              >
                Gán
              </button>
            </PermissionGuard>
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
                  <span className="msg-meta-name">
                    {msg.isFromStaff ? "CSKH" : msg.senderName || "Khách"}
                  </span>
                  <span className="msg-meta-time">
                    {formatTimeShort(msg.sentAt)}
                  </span>
                </div>
                <div className="msg-bubble">{msg.content}</div>
              </div>
            </div>
          );
        })}
      </div>
    );
  };

  const renderPreviousSessionsPanel = () => {
    if (!selectedSession) return null;

    // ✅ Admin: chỉ cần có customerId là xem được lịch sử, không cần "nhận" phiên
    const canShowPanel = !!selectedSession.customerId;

    return (
      <div className="previous-sessions-panel">
        <div className="previous-sessions-header">
          <div className="previous-sessions-title">
            Các phiên chat trước với user này
          </div>
          {loadingPreviousSessions && (
            <span className="previous-sessions-tag">Đang tải...</span>
          )}
        </div>

        {!canShowPanel && (
          <div className="previous-sessions-empty">
            Phiên này không gắn khách hàng, không có lịch sử trước đó.
          </div>
        )}

        {canShowPanel &&
          !loadingPreviousSessions &&
          !previewSession && (
            <>
              {previousSessions.length === 0 && (
                <div className="previous-sessions-empty">
                  Chưa có phiên chat trước nào.
                </div>
              )}

              {previousSessions.length > 0 && (
                <div className="previous-sessions-list">
                  {previousSessions.map((s) => {
                    let timeLabel = "";
                    if (s.lastMessageAt) {
                      timeLabel = `Tin cuối: ${formatTimeShort(
                        s.lastMessageAt
                      )}`;
                    } else if (s.startedAt) {
                      timeLabel = `Bắt đầu: ${formatTimeShort(
                        s.startedAt
                      )}`;
                    }

                    return (
                      <button
                        key={s.chatSessionId}
                        type="button"
                        className="previous-session-item"
                        onClick={() => handleOpenTranscript(s)}
                      >
                        <div className="previous-session-line1">
                          <span className="previous-session-status">
                            {getStatusLabel(s)}
                          </span>
                          {timeLabel && (
                            <span className="previous-session-time">
                              {timeLabel}
                            </span>
                          )}
                        </div>
                        {s.lastMessagePreview && (
                          <div className="previous-session-preview">
                            {s.lastMessagePreview}
                          </div>
                        )}
                      </button>
                    );
                  })}
                </div>
              )}
            </>
          )}

        {canShowPanel && previewSession && (
          <div className="previous-transcript">
            <div className="previous-transcript-header">
              <button
                type="button"
                className="link-button"
                onClick={() => setPreviewSession(null)}
              >
                ← Quay lại danh sách phiên
              </button>
              <div className="previous-transcript-sub">
                <span>{getStatusLabel(previewSession)}</span>
                {previewSession.startedAt && (
                  <span>
                    Bắt đầu:{" "}
                    {formatTimeShort(previewSession.startedAt)}
                  </span>
                )}
              </div>
            </div>
            <div className="previous-transcript-body">
              {loadingPreviewMessages && (
                <div className="empty small">Đang tải transcript...</div>
              )}
              {!loadingPreviewMessages &&
                (!previewMessages.length ? (
                  <div className="empty small">
                    Không có tin nhắn trong phiên này.
                  </div>
                ) : (
                  previewMessages.map((msg) => {
                    const key =
                      msg.messageId || `${msg.chatSessionId}_${msg.sentAt}`;
                    const rowCls =
                      "msg-row msg-row-compact " +
                      (msg.isFromStaff
                        ? "msg-row-staff"
                        : "msg-row-customer");
                    const msgCls =
                      "msg msg-compact " +
                      (msg.isFromStaff ? "msg-staff" : "msg-customer");

                    return (
                      <div key={key} className={rowCls}>
                        <div className={msgCls}>
                          <div className="msg-meta">
                            <span className="msg-meta-name">
                              {msg.isFromStaff
                                ? "CSKH"
                                : msg.senderName || "Khách"}
                            </span>
                            <span className="msg-meta-time">
                              {formatTimeShort(msg.sentAt)}
                            </span>
                          </div>
                          <div className="msg-bubble">{msg.content}</div>
                        </div>
                      </div>
                    );
                  })
                ))}
            </div>
          </div>
        )}
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
            <span>• Đã nhận: {assigned.length}</span>
          </div>
        </div>
        <div className="support-chat-header-actions">
          <button
            type="button"
            className="btn ghost refresh-button"
            onClick={refreshAll}
          >
            <span className="refresh-icon">⟳</span>
            <span>Làm mới</span>
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
                "tab" + (activeTab === "assigned" ? " tab-active" : "")
              }
              onClick={() => handleChangeTab("assigned")}
            >
              Đã nhận
              <span className="badge">{assigned.length}</span>
            </button>
          </div>

          <div className="sidebar-toolbar">
            <span className="muted">
              {activeTab === "unassigned"
                ? "Các phiên chat đang chờ nhân viên nhận."
                : "Các phiên chat đã được nhân viên nhận."}
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

            {activeTab === "assigned" && (
              <>
                {loadingAssigned && (
                  <div className="empty small">
                    Đang tải phiên đã nhận...
                  </div>
                )}
                {!loadingAssigned && assigned.length === 0 && (
                  <div className="empty">
                    Chưa có phiên chat nào đã được nhận.
                  </div>
                )}
                {!loadingAssigned &&
                  assigned.map((s) => renderSessionItem(s, false))}
              </>
            )}
          </div>
        </div>

        {/* Main chat */}
        <div className="support-chat-main">
          {!selectedSession && (
            <div className="chat-empty">
              Chọn một phiên chat ở cột bên trái để xem và hỗ trợ khách.
            </div>
          )}

          {selectedSession && (
            <>
              <div className="chat-panel">
                <div className="chat-header">
                  <div className="chat-header-main">
                    <div className="chat-header-left">
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
                              {getPriorityLabel(
                                selectedSession.priorityLevel
                              )}
                            </span>
                          )}
                          {selectedSession.customerEmail && (
                            <span className="meta-item">
                              <strong>Email:</strong>{" "}
                              {selectedSession.customerEmail}
                            </span>
                          )}
                          {selectedSession.assignedStaffName && (
                            <span className="meta-item">
                              <strong>Nhân viên phụ trách:</strong>{" "}
                              {selectedSession.assignedStaffName}
                            </span>
                          )}
                        </div>
                        {sessionStatusText && (
                          <div className="chat-meta-sub">
                            {sessionStatusText}
                          </div>
                        )}
                      </div>
                    </div>

                    {/* Admin actions */}
                    <div className="chat-header-actions">
                      <button
                        type="button"
                        className={`btn ghost ${!hasEditPermission ? 'disabled' : ''}`}
                        title={!hasEditPermission ? "Bạn không có quyền trả lại phiên chat về hàng chờ" : "Trả lại hàng chờ"}
                        disabled={!hasEditPermission}
                        onClick={() =>
                          selectedSession &&
                          handleUnassign(selectedSession.chatSessionId)
                        }
                      >
                        Trả lại hàng chờ
                      </button>

                      {selectedSession.assignedStaffId && (
                        <button
                          type="button"
                          className={`btn warning ${!hasEditPermission ? 'disabled' : ''}`}
                          title={!hasEditPermission ? "Bạn không có quyền chuyển nhân viên phụ trách" : "Chuyển nhân viên"}
                          disabled={!hasEditPermission}
                          onClick={() =>
                            handleOpenTransferModalForSession(
                              selectedSession
                            )
                          }
                        >
                          Chuyển nhân viên
                        </button>
                      )}

                      <button
                        type="button"
                        className={`btn danger ${!hasEditPermission ? 'disabled' : ''}`}
                        title={!hasEditPermission ? "Bạn không có quyền đóng phiên chat" : "Đóng phiên"}
                        disabled={!hasEditPermission}
                        onClick={() =>
                          selectedSession &&
                          handleClose(selectedSession.chatSessionId)
                        }
                      >
                        Đóng phiên
                      </button>
                    </div>
                  </div>
                </div>

                <div className="chat-body">
                  {renderMessages()}

                  <form className="chat-footer" onSubmit={handleSend}>
                    <textarea
                      className="chat-input"
                      placeholder={
                        !hasEditPermission
                          ? "Bạn không có quyền gửi tin nhắn"
                          : canSend
                          ? "Nhập nội dung tin nhắn..."
                          : "Phiên chat đã đóng, không thể gửi thêm."
                      }
                      value={newMessage}
                      onChange={(e) => setNewMessage(e.target.value)}
                      disabled={!canSend || sending || !hasEditPermission}
                    />
                    <div className="chat-footer-actions">
                      <div className="chat-footer-row">
                        <button
                          type="submit"
                          className={`btn primary ${!hasEditPermission ? 'disabled' : ''}`}
                          disabled={!canSend || sending || !hasEditPermission}
                          title={!hasEditPermission ? "Bạn không có quyền gửi tin nhắn" : ""}
                        >
                          {sending ? "Đang gửi..." : "Gửi"}
                        </button>
                      </div>

                      {errorText && (
                        <div className="error-text chat-error">
                          {errorText}
                        </div>
                      )}
                    </div>
                  </form>
                </div>
              </div>

              {renderPreviousSessionsPanel()}
            </>
          )}
        </div>
      </div>

      {/* Modal gán / chuyển nhân viên cho phiên chat */}
      <AssignModal
        open={assignModal.open}
        title={
          assignModal.mode === "transfer"
            ? "Chuyển nhân viên phụ trách"
            : "Gán nhân viên phụ trách"
        }
        excludeUserId={assignModal.excludeUserId}
        onClose={() =>
          setAssignModal({
            open: false,
            mode: "",
            sessionId: null,
            excludeUserId: null,
          })
        }
        onConfirm={async (userId) => {
          try {
            if (assignModal.mode === "transfer") {
              await doAdminTransfer(assignModal.sessionId, userId);
            } else {
              await doAdminAssign(assignModal.sessionId, userId);
            }
          } finally {
            setAssignModal({
              open: false,
              mode: "",
              sessionId: null,
              excludeUserId: null,
            });
          }
        }}
      />

      {/* Confirm dialog dùng chung */}
      <ConfirmDialog
        isOpen={confirmState.isOpen}
        title={confirmState.title}
        message={confirmState.message}
        onConfirm={handleConfirmOk}
        onCancel={handleConfirmCancel}
      />

      {/* Toasts hiển thị thông báo */}
      <div className="toast-container">
        {toasts.map((t) => (
          <Toast key={t.id} toast={t} onRemove={handleRemoveToast} />
        ))}
      </div>
    </div>
  );
}

// ===== Shared helpers for popup =====

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
                    className={
                      "staff-item" + (selected === u.id ? " selected" : "")
                    }
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
