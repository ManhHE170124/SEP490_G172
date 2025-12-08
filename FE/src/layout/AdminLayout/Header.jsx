/**
 * File: Header.jsx
 * Author: HieuNDHE173169
 * Created: 18/10/2025
 * Last Updated: 09/12/2025
 * Version: 1.1.0
 * Purpose: Admin header component with search, notifications, and user dropdown menu.
 *          Provides navigation and user account management interface.
 */
import React, { useState, useRef, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { AuthService } from "../../services/authService";
import { NotificationsApi } from "../../services/notifications"; // <-- thêm
import "./Header.css";

/**
 * @summary: Header component for admin layout with search, notifications, and user menu.
 * @returns {JSX.Element} - Header with search bar, notification icon, and user dropdown
 */
const Header = ({ profile }) => {
  const navigate = useNavigate();
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const [user, setUser] = useState(null);

  // Dropdown avatar
  const avatarDropdownRef = useRef(null);

  // ====== STATE THÔNG BÁO ======
  const [isNotifDropdownOpen, setIsNotifDropdownOpen] = useState(false);
  const [notifications, setNotifications] = useState([]); // history
  const [unreadCount, setUnreadCount] = useState(0); // số chưa đọc
  const [isNotifLoading, setIsNotifLoading] = useState(false);

  // Toast khi có thông báo mới
  const [toastQueue, setToastQueue] = useState([]);
  const [activeToast, setActiveToast] = useState(null);

  // Ref cho dropdown thông báo
  const notifDropdownRef = useRef(null);

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

  // Load user và listen khi localStorage thay đổi
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

  // Đóng cả dropdown avatar + dropdown thông báo khi click ra ngoài
  useEffect(() => {
    const handleClickOutside = (event) => {
      if (
        avatarDropdownRef.current &&
        !avatarDropdownRef.current.contains(event.target)
      ) {
        setIsDropdownOpen(false);
      }

      if (
        notifDropdownRef.current &&
        !notifDropdownRef.current.contains(event.target)
      ) {
        setIsNotifDropdownOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, []);

  // ====== POLL THÔNG BÁO CHƯA ĐỌC MỖI 5s ======
  useEffect(() => {
    let isMounted = true;
    let lastSeenIds = new Set();

    const extractId = (n) =>
      n.notificationUserId ??
      n.NotificationUserId ??
      n.notificationId ??
      n.NotificationId ??
      n.id ??
      n.Id;

    const fetchUnreadNotifications = async (showToastForNew = false) => {
      const token = localStorage.getItem("access_token");
      if (!token) {
        if (isMounted) {
          setUnreadCount(0);
        }
        return;
      }

      try {
        const res = await NotificationsApi.getMy({
          pageNumber: 1,
          pageSize: 5,
          onlyUnread: true,
          sortBy: "CreatedAtUtc",
          sortDescending: true,
        });

        if (!isMounted) return;

        const items = res.items || res.Items || [];
        const totalUnread =
          res.total ??
          res.totalCount ??
          res.Total ??
          res.TotalCount ??
          items.length;

        setUnreadCount(totalUnread);

        const currentIds = new Set(
          items.map(extractId).filter((id) => id !== undefined && id !== null)
        );

        // Lần đầu load thì không show toast
        if (showToastForNew && lastSeenIds.size > 0) {
          const newItems = items.filter((n) => {
            const id = extractId(n);
            return id && !lastSeenIds.has(id);
          });

          if (newItems.length > 0) {
            setToastQueue((prev) => [
              ...prev,
              ...newItems.map((n) => ({
                id: extractId(n),
                title: n.title || n.Title,
                message: n.message || n.Message,
                severity: n.severity ?? n.Severity ?? 0,
                createdAt: n.createdAtUtc || n.CreatedAtUtc,
              })),
            ]);
          }
        }

        lastSeenIds = currentIds;
      } catch (error) {
        console.error("Failed to fetch unread notifications:", error);
      }
    };

    // load lần đầu (không toast)
    fetchUnreadNotifications(false);

    // Poll 5s/lần
    const intervalId = setInterval(() => fetchUnreadNotifications(true), 5000);

    return () => {
      isMounted = false;
      clearInterval(intervalId);
    };
  }, []);

  // Lấy history thông báo khi mở dropdown
  const fetchNotificationHistory = async () => {
    const token = localStorage.getItem("access_token");
    if (!token) {
      setNotifications([]);
      return;
    }

    setIsNotifLoading(true);
    try {
      const res = await NotificationsApi.getMy({
        pageNumber: 1,
        pageSize: 20,
        sortBy: "CreatedAtUtc",
        sortDescending: true,
      });

      const items = res.items || res.Items || [];
      setNotifications(items);
    } catch (error) {
      console.error("Failed to fetch notification history:", error);
    } finally {
      setIsNotifLoading(false);
    }
  };

  // Lấy toast từ queue ra hiển thị
  useEffect(() => {
    if (activeToast || toastQueue.length === 0) return;
    setActiveToast(toastQueue[0]);
    setToastQueue((prev) => prev.slice(1));
  }, [toastQueue, activeToast]);

  // Tự ẩn toast sau 5s
  useEffect(() => {
    if (!activeToast) return;
    const timer = setTimeout(() => setActiveToast(null), 5000);
    return () => clearTimeout(timer);
  }, [activeToast]);

  const handleAvatarClick = () => {
    loadUser();
    setIsDropdownOpen((prev) => !prev);
  };

  const handleMenuAction = (action) => {
    setIsDropdownOpen(false);

    switch (action) {
      case "profile":
        navigate("/admin/profile");
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

  const getSeverityClass = (sev) => {
    switch (sev) {
      case 1:
        return "badge-severity badge-success";
      case 2:
        return "badge-severity badge-warning";
      case 3:
        return "badge-severity badge-error";
      default:
        return "badge-severity badge-info";
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

  const handleBellClick = () => {
    setIsNotifDropdownOpen((prev) => {
      const next = !prev;
      if (!prev) {
        // vừa mở -> load history
        fetchNotificationHistory();
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

          {/* ====== CHUÔNG THÔNG BÁO ====== */}
          <div className="alh-notif-wrapper" ref={notifDropdownRef}>
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

            {isNotifDropdownOpen && (
              <div className="alh-notif-dropdown">
                <div className="alh-notif-dropdown-header">
                  <div className="alh-notif-title">Thông báo</div>
                  <div className="alh-notif-subtitle">
                    {unreadCount > 0
                      ? `${unreadCount} thông báo chưa đọc`
                      : "Không có thông báo chưa đọc"}
                  </div>
                </div>

                <div className="alh-notif-dropdown-body">
                  {isNotifLoading && (
                    <div className="alh-notif-empty">Đang tải...</div>
                  )}

                  {!isNotifLoading && notifications.length === 0 && (
                    <div className="alh-notif-empty">
                      Chưa có thông báo nào.
                    </div>
                  )}

                  {!isNotifLoading &&
                    notifications.map((n) => {
                      const id =
                        n.notificationUserId ??
                        n.NotificationUserId ??
                        n.notificationId ??
                        n.NotificationId ??
                        n.id ??
                        n.Id;
                      const title = n.title || n.Title || "(Không có tiêu đề)";
                      const message = n.message || n.Message || "";
                      const severity = n.severity ?? n.Severity ?? 0;
                      const createdAt = n.createdAtUtc || n.CreatedAtUtc;
                      const isRead = n.isRead ?? n.IsRead ?? false;

                      return (
                        <div
                          key={id}
                          className={
                            "alh-notif-item" +
                            (isRead ? " read" : " unread")
                          }
                        >
                          <div className="alh-notif-left">
                            <span
                              className={
                                "alh-notif-dot " +
                                getSeverityClass(severity)
                              }
                            />
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
                </div>
              </div>
            )}
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

      {/* ====== TOAST THÔNG BÁO MỚI (tự ẩn sau 5s) ====== */}
      {activeToast && (
        <div className="alh-toast">
          <div className="alh-toast-indicator">
            <span className={getSeverityClass(activeToast.severity)} />
          </div>
          <div className="alh-toast-content">
            <div className="alh-toast-title">
              {getSeverityLabel(activeToast.severity)} · {activeToast.title}
            </div>
            <div className="alh-toast-message">
              {activeToast.message && activeToast.message.length > 100
                ? activeToast.message.slice(0, 100) + "..."
                : activeToast.message}
            </div>
          </div>
        </div>
      )}
    </>
  );
};

export default Header;
