// File: src/layout/AdminLayout/Header.jsx
/**
 * File: Header.jsx
 * Author: HieuNDHE173169
 * Created: 18/10/2025
 * Last Updated: 10/12/2025
 * Version: 1.4.0
 * Purpose: Admin header component with notification widget (similar style to chatbox),
 *          search placeholder and user dropdown menu + realtime-like notification polling.
 */
import React, { useState, useRef, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { AuthService } from "../../services/authService";
import { NotificationsApi } from "../../services/notifications";
import axiosClient from "../../api/axiosClient"; 
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr"; // ✅ thêm
import "./Header.css";

const Header = ({ profile }) => {
  const navigate = useNavigate();

  // ===== USER DROPDOWN =====
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const [user, setUser] = useState(null);
  const avatarDropdownRef = useRef(null);

  // ===== NOTIFICATION STATE =====
  const [notifications, setNotifications] = useState([]); // history list trong widget
  const [unreadCount, setUnreadCount] = useState(0); // số chưa đọc
  const [isNotifLoading, setIsNotifLoading] = useState(false);
  const [hasMoreNotifications, setHasMoreNotifications] = useState(true);

  // Widget mở/đóng (giống chatbox)
  const [isNotifWidgetOpen, setIsNotifWidgetOpen] = useState(false);
  const notifWidgetRef = useRef(null);

  // Paging cho widget (5 item/ page)
  const notifPagingRef = useRef({
    pageNumber: 0,
    pageSize: 5,
    hasMore: true,
  });
  const notifLoadTimerRef = useRef(null);

  // Toast khi có thông báo mới
  const [toastQueue, setToastQueue] = useState([]);
  const [activeToast, setActiveToast] = useState(null);

const notifConnectionRef = useRef(null);


  // ===== COMMON HELPERS =====
  const loadUser = () => {
    const storedUser = localStorage.getItem("user");
    if (storedUser) {
      try {
        setUser(JSON.parse(storedUser));
      } catch (error) {
        console.error("Error parsing user data:", error);
        setUser(null);
      }
    } else {
      setUser(null);
    }
  };

  // Hàm gộp để đọc kết quả từ NotificationsApi (dù trả về axios response hay data thuần)
  const normalizeNotificationResponse = (res) => {
    const data = res && res.data !== undefined ? res.data : res;
    if (!data) {
      return { items: [], total: 0 };
    }

    const items = data.items || data.Items || [];
    const total =
      data.totalCount ??
      data.TotalCount ??
      data.total ??
      data.Total ??
      items.length;

    return { items, total };
  };

  const extractNotificationId = (n) =>
    n.notificationUserId ??
    n.NotificationUserId ??
    n.notificationId ??
    n.NotificationId ??
    n.id ??
    n.Id;

  // ===== INIT USER & STORAGE LISTENER =====
  useEffect(() => {
    loadUser();
    const handleStorage = (event) => {
      if (event.key === "user" || event.key === "access_token") {
        loadUser();
      }
    };
    window.addEventListener("storage", handleStorage);
    return () => {
      window.removeEventListener("storage", handleStorage);
    };
  }, []);

  // ===== CLICK OUTSIDE: đóng avatar dropdown + widget thông báo =====
  useEffect(() => {
    const handleClickOutside = (event) => {
      if (
        avatarDropdownRef.current &&
        !avatarDropdownRef.current.contains(event.target)
      ) {
        setIsDropdownOpen(false);
      }

      if (
        notifWidgetRef.current &&
        !notifWidgetRef.current.contains(event.target)
      ) {
        setIsNotifWidgetOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, []);

  // ===== POLL UNREAD COUNT ĐỊNH KỲ (CHỈ ĐỂ ĐỒNG BỘ SỐ LƯỢNG) =====
  useEffect(() => {
    let isMounted = true;

    const fetchUnreadNotifications = async () => {
      const token = localStorage.getItem("access_token");
      if (!token) {
        if (isMounted) {
          setUnreadCount(0);
        }
        return;
      }

      try {
        const res = await NotificationsApi.listMyPaged({
          pageNumber: 1,
          pageSize: 5,
          onlyUnread: true,
          sortBy: "CreatedAtUtc",
          sortDescending: true,
        });

        if (!isMounted) return;

        const { total } = normalizeNotificationResponse(res);
        setUnreadCount(total);
      } catch (error) {
        console.error("Failed to fetch unread notifications:", error);
      }
    };

    // Lần đầu
    fetchUnreadNotifications();

    // 15s/lần để đồng bộ số lượng (không show toast)
    const intervalId = setInterval(fetchUnreadNotifications, 15000);

    return () => {
      isMounted = false;
      clearInterval(intervalId);
    };
  }, []);

  // ===== LẤY LỊCH SỬ THÔNG BÁO KHI MỞ WIDGET (PAGING 5, LOAD THÊM SAU 3s) =====
  const fetchNotificationHistory = async ({ append = false } = {}) => {
    const token = localStorage.getItem("access_token");
    if (!token) {
      setNotifications([]);
      setHasMoreNotifications(false);
      return;
    }

    const paging = notifPagingRef.current;
    const nextPageNumber = append ? paging.pageNumber + 1 : 1;
    const pageSize = paging.pageSize;

    // Không còn gì để load thì thôi
    if (append && !paging.hasMore) {
      setHasMoreNotifications(false);
      return;
    }

    setIsNotifLoading(true);
    try {
      const res = await NotificationsApi.listMyPaged({
        pageNumber: nextPageNumber,
        pageSize,
        sortBy: "CreatedAtUtc",
        sortDescending: true,
      });

      const { items, total } = normalizeNotificationResponse(res);

      // update widget list
      setNotifications((prev) => {
        if (!append) return items;

        const existingIds = new Set(
          prev.map((x) => extractNotificationId(x)).filter(Boolean)
        );
        const merged = [...prev];
        items.forEach((it) => {
          const id = extractNotificationId(it);
          if (!id || existingIds.has(id)) return;
          merged.push(it);
        });
        return merged;
      });

      const hasMore = nextPageNumber * pageSize < total;
      notifPagingRef.current = {
        pageNumber: nextPageNumber,
        pageSize,
        hasMore,
      };
      setHasMoreNotifications(hasMore);
    } catch (error) {
      console.error("Failed to fetch notification history:", error);
    } finally {
      setIsNotifLoading(false);
    }
  };

  // ===== TỰ ĐỘNG LOAD THÊM MỖI 3s NẾU VẪN MỞ WIDGET & CÒN DATA =====
  useEffect(() => {
    if (!isNotifWidgetOpen) {
      if (notifLoadTimerRef.current) {
        clearTimeout(notifLoadTimerRef.current);
        notifLoadTimerRef.current = null;
      }
      return;
    }

    if (!hasMoreNotifications || isNotifLoading) {
      return;
    }

    notifLoadTimerRef.current = setTimeout(() => {
      fetchNotificationHistory({ append: true });
    }, 3000);

    return () => {
      if (notifLoadTimerRef.current) {
        clearTimeout(notifLoadTimerRef.current);
        notifLoadTimerRef.current = null;
      }
    };
  }, [isNotifWidgetOpen, notifications.length, hasMoreNotifications, isNotifLoading]);
// ===== REALTIME SIGNALR: nhận "ReceiveNotification" từ NotificationHub =====
useEffect(() => {
  const token = localStorage.getItem("access_token");
  if (!token) {
    return;
  }

  // Lấy baseURL từ axiosClient, ví dụ: https://localhost:7292/api
  const apiBase = axiosClient.defaults.baseURL || "";
  // Bỏ đuôi /api => https://localhost:7292 rồi thêm /hubs/notifications
  const hubUrl = apiBase
    ? apiBase.replace(/\/api\/?$/, "") + "/hubs/notifications"
    : "https://localhost:7292/hubs/notifications"; // fallback khi thiếu baseURL

  const connection = new HubConnectionBuilder()
    .withUrl(hubUrl, {
      accessTokenFactory: () => token,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();

  notifConnectionRef.current = connection;

  connection.on("ReceiveNotification", (n) => {
    const id = extractNotificationId(n);
    if (!id) return;

    const isRead = n.isRead ?? n.IsRead ?? false;

    // Cập nhật list trong widget (prepend, tránh trùng)
    setNotifications((prev) => {
      const existingIds = new Set(
        prev.map((x) => extractNotificationId(x)).filter(Boolean)
      );
      if (existingIds.has(id)) {
        return prev;
      }
      return [n, ...prev];
    });

    // Tăng unread nếu thông báo mới chưa đọc
    if (!isRead) {
      setUnreadCount((prev) => prev + 1);
    }

    // Đẩy vào toast queue
    setToastQueue((prev) => [
      ...prev,
      {
        id,
        title: n.title || n.Title,
        message: n.message || n.Message,
        severity: n.severity ?? n.Severity ?? 0,
        createdAt: n.createdAtUtc || n.CreatedAtUtc,
      },
    ]);
  });

  connection
    .start()
    .catch((err) =>
      console.error("Failed to connect NotificationHub:", err)
    );

  return () => {
    if (notifConnectionRef.current) {
      notifConnectionRef.current.off("ReceiveNotification");
      notifConnectionRef.current.stop();
      notifConnectionRef.current = null;
    }
  };
}, []);

  // ===== TOAST QUEUE =====
  useEffect(() => {
    if (activeToast || toastQueue.length === 0) return;
    setActiveToast(toastQueue[0]);
    setToastQueue((prev) => prev.slice(1));
  }, [toastQueue, activeToast]);

  useEffect(() => {
    if (!activeToast) return;
    const timer = setTimeout(() => setActiveToast(null), 5000);
    return () => clearTimeout(timer);
  }, [activeToast]);

  // ===== HANDLERS / HELPERS UI =====
  const handleAvatarClick = () => {
    loadUser();
    setIsDropdownOpen((prev) => !prev);
  };
  const handleToastClose = () => {
    setActiveToast(null);
  };

  const handleMenuAction = (action) => {
    setIsDropdownOpen(false);
    switch (action) {
      case "profile":
        navigate("/admin/profile");
        break;
      case "home":
        navigate("/");
        break;
      case "logout":
        handleLogout();
        break;
      default:
        break;
    }
  };

  const handleLogout = async () => {
    try {
      await AuthService.logout();
    } catch (error) {
      console.error("Logout error:", error);
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
      localStorage.removeItem("user");
    } finally {
      navigate("/login");
    }
  };

  const getInitials = (fullName) => {
    if (!fullName) return "U";
    const nameParts = fullName.trim().split(" ");
    if (nameParts.length === 1) {
      return nameParts[0].charAt(0).toUpperCase();
    }
    return (
      nameParts[0].charAt(0) + nameParts[nameParts.length - 1].charAt(0)
    ).toUpperCase();
  };

  const getSeverityLabel = (sev) => {
    switch (sev) {
      case 1:
        return "Thành công";
      case 2:
        return "Cảnh báo";
      case 3:
        return "Lỗi";
      default:
        return "Thông tin";
    }
  };

  const formatTime = (value) => {
    if (!value) return "";
    try {
      return new Date(value).toLocaleString();
    } catch {
      return "";
    }
  };

  // Hover vào item → mark read (gửi API + update UI + giảm unreadCount)
  const handleNotificationHover = (item) => {
    const notifUserId = extractNotificationId(item);
    const isRead = item.isRead ?? item.IsRead ?? false;
    if (!notifUserId || isRead) return;

    // Optimistic update
    setNotifications((prev) =>
      prev.map((n) => {
        const id = extractNotificationId(n);
        if (id !== notifUserId) return n;
        return { ...n, isRead: true, IsRead: true };
      })
    );
    setUnreadCount((prev) => (prev > 0 ? prev - 1 : 0));

    NotificationsApi.markMyNotificationRead(notifUserId).catch((err) => {
      console.error("Failed to mark notification as read", err);
    });
  };

  // Click vào item → chuyển tới RelatedUrl (nếu có)
  const handleNotificationClick = (item) => {
    const url = item.relatedUrl || item.RelatedUrl;
    if (!url) return;

    if (url.startsWith("http://") || url.startsWith("https://")) {
      window.open(url, "_blank", "noopener,noreferrer");
    } else if (url.startsWith("/")) {
      navigate(url);
    } else {
      navigate("/" + url);
    }
  };

  // Bấm chuông → mở/đóng widget giống chatbox, reset paging
  const handleBellClick = () => {
    setIsNotifWidgetOpen((prev) => {
      const next = !prev;
      if (!prev && next) {
        // vừa mở → reset paging & load 5 thông báo gần nhất
        notifPagingRef.current = {
          pageNumber: 0,
          pageSize: 5,
          hasMore: true,
        };
        setHasMoreNotifications(true);
        fetchNotificationHistory({ append: false });
      }
      return next;
    });
  };

  const resolvedUser = profile || user;
  const displayName =
    resolvedUser?.fullName ||
    resolvedUser?.displayName ||
    resolvedUser?.username ||
    "Tài khoản";
  const displayEmail =
    resolvedUser?.email ||
    resolvedUser?.emailAddress ||
    resolvedUser?.mail ||
    "Chưa có email";
  const avatarUrl =
    profile?.avatarUrl ||
    profile?.avatar ||
    resolvedUser?.avatarUrl ||
    resolvedUser?.avatar ||
    resolvedUser?.avatarURL ||
    resolvedUser?.avatarUrlProfile ||
    null;
  const initials = getInitials(displayName);

  return (
    <>
      <div className="alh-header" role="banner">
        <div className="alh-search">{/* (giữ nguyên, hiện chưa dùng) */}</div>

        <div className="alh-right">
          <span
            className="alh-pill"
            title="Tháng hiện tại"
            aria-label="Tháng hiện tại"
          >
            10/2025
          </span>

          {/* ====== NÚT CHUÔNG THÔNG BÁO (MỞ WIDGET) ====== */}
          <div className="alh-notif-wrapper">
            <button
              type="button"
              className="alh-pill alh-notif-bell"
              title="Thông báo"
              aria-label="Thông báo"
              onClick={handleBellClick}
            >
              🔔
              {unreadCount > 0 && (
                <span className="alh-notif-badge">
                  {unreadCount > 99 ? "99+" : unreadCount}
                </span>
              )}
            </button>
          </div>

          {/* ====== AVATAR / USER DROPDOWN ====== */}
          <div className="alh-avatar-container" ref={avatarDropdownRef}>
            <div
              className="alh-avatar"
              aria-label="Tài khoản"
              onClick={handleAvatarClick}
            >
              {avatarUrl ? (
                <img src={avatarUrl} alt="Ảnh đại diện" />
              ) : (
                <span>{initials}</span>
              )}
            </div>

            {isDropdownOpen && (
              <div className="alh-dropdown-menu">
                <div className="alh-dropdown-header">
                  <div className="alh-user-info">
                    <div className="alh-user-avatar">
                      {avatarUrl ? (
                        <img src={avatarUrl} alt="Ảnh đại diện" />
                      ) : (
                        <span>{initials}</span>
                      )}
                    </div>
                    <div className="alh-user-details">
                      <div className="alh-user-name">{displayName}</div>
                      <div className="alh-user-email">{displayEmail}</div>
                    </div>
                  </div>
                </div>

                <div className="alh-dropdown-items">
                  <button
                    className="alh-dropdown-item"
                    onClick={() => handleMenuAction("profile")}
                  >
                    <svg
                      width="18"
                      height="18"
                      viewBox="0 0 24 24"
                      fill="none"
                    >
                      <path
                        d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      />
                      <circle
                        cx="12"
                        cy="7"
                        r="4"
                        stroke="currentColor"
                        strokeWidth="2"
                      />
                    </svg>
                    Xem Profile
                  </button>

                  <button
                    className="alh-dropdown-item"
                    onClick={() => handleMenuAction("home")}
                  >
                    <svg
                      width="18"
                      height="18"
                      viewBox="0 0 24 24"
                      fill="none"
                    >
                      <path
                        d="M3 11L12 3l9 8v9a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1v-9Z"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      />
                    </svg>
                    Về trang chủ
                  </button>

                  <div className="alh-dropdown-divider"></div>

                  <button
                    className="alh-dropdown-item logout"
                    onClick={() => handleMenuAction("logout")}
                  >
                    <svg
                      width="18"
                      height="18"
                      viewBox="0 0 24 24"
                      fill="none"
                    >
                      <path
                        d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      />
                      <polyline
                        points="16,17 21,12 16,7"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      />
                      <line
                        x1="21"
                        y1="12"
                        x2="9"
                        y2="12"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      />
                    </svg>
                    Đăng xuất
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ====== WIDGET THÔNG BÁO (GIỐNG CHATBOX) ====== */}
      {isNotifWidgetOpen && (
        <div className="alh-notif-widget">
          <div className="alh-notif-widget-panel" ref={notifWidgetRef}>
            <div className="alh-notif-widget-header">
              <div className="alh-notif-header-left">
                <div className="alh-notif-title">Thông báo</div>
                <div className="alh-notif-subtitle">
                  {unreadCount > 0
                    ? `${unreadCount} thông báo chưa đọc`
                    : "Không có thông báo chưa đọc"}
                </div>
              </div>
              <button
                type="button"
                className="alh-notif-close-btn"
                onClick={() => setIsNotifWidgetOpen(false)}
                aria-label="Đóng thông báo"
              >
                ×
              </button>
            </div>

            <div className="alh-notif-widget-body">
              {isNotifLoading && notifications.length === 0 && (
                <div className="alh-notif-empty">Đang tải...</div>
              )}

              {!isNotifLoading && notifications.length === 0 && (
                <div className="alh-notif-empty">
                  Chưa có thông báo nào.
                </div>
              )}

              {!isNotifLoading &&
                notifications.map((n) => {
                  const id = extractNotificationId(n);
                  const title =
                    n.title || n.Title || "(Không có tiêu đề)";
                  const message = n.message || n.Message || "";
                  const severity = n.severity ?? n.Severity ?? 0;
                  const createdAt = n.createdAtUtc || n.CreatedAtUtc;
                  const isRead = n.isRead ?? n.IsRead ?? false;

                  return (
                    <div
                      key={id}
                      className={
                        "alh-notif-item" + (isRead ? " read" : " unread")
                      }
                      onMouseEnter={() => handleNotificationHover(n)}
                      onClick={() => handleNotificationClick(n)}
                    >
                      <div className="alh-notif-left">
                        {/* Dot xanh dương chỉ hiển thị khi chưa đọc */}
                        {!isRead && (
                          <span className="alh-notif-unread-dot" />
                        )}
                      </div>
                      <div className="alh-notif-content">
                        <div className="alh-notif-line">
                          <span className="alh-notif-item-title">
                            {title}
                          </span>
                        </div>
                        <div className="alh-notif-message">
                          {message.length > 80
                            ? message.slice(0, 80) + "..."
                            : message}
                        </div>
                        <div className="alh-notif-meta">
                          <span className="alh-notif-severity">
                            {getSeverityLabel(severity)}
                          </span>
                          <span className="alh-notif-dot-sep">•</span>
                          <span className="alh-notif-time">
                            {formatTime(createdAt)}
                          </span>
                        </div>
                      </div>
                    </div>
                  );
                })}

              {/* Nếu còn data mà đang load thêm thì show nhẹ 1 dòng */}
              {isNotifLoading && notifications.length > 0 && (
                <div className="alh-notif-empty">Đang tải thêm...</div>
              )}
            </div>
          </div>
        </div>
      )}

       {/* ====== TOAST THÔNG BÁO MỚI (tự ẩn sau 5s, cho đóng thủ công) ====== */}
      {activeToast && (
        <div className="alh-toast" role="status" aria-live="polite">
          <div className="alh-toast-inner">
            <div className="alh-toast-indicator">
              {/* Toast vẫn dùng màu theo severity */}
              <span
                className={`badge-severity ${(() => {
                  const sev = activeToast.severity ?? 0;
                  if (sev === 1) return "badge-success";
                  if (sev === 2) return "badge-warning";
                  if (sev === 3) return "badge-error";
                  return "badge-info";
                })()}`}
              />
            </div>
            <div className="alh-toast-content">
              <div className="alh-toast-title">
                {getSeverityLabel(activeToast.severity)} ·{" "}
                {activeToast.title}
              </div>
              <div className="alh-toast-message">
                {activeToast.message && activeToast.message.length > 100
                  ? activeToast.message.slice(0, 100) + "..."
                  : activeToast.message}
              </div>
            </div>
            <button
              type="button"
              className="alh-toast-close"
              aria-label="Đóng thông báo"
              onClick={handleToastClose}
            >
              ×
            </button>
          </div>
        </div>
      )}

    </>
  );
};

export default Header;
