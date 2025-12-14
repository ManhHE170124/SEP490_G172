// File: src/layout/ClientLayout/PublicHeader.jsx
import React, { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { CategoryApi } from "../../services/categories";
import { AuthService } from "../../services/authService";
import StorefrontCartApi, {
  CART_UPDATED_EVENT,
} from "../../services/storefrontCartService";
import { NotificationsApi } from "../../services/notifications";
import axiosClient from "../../api/axiosClient";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import "./PublicHeader.css";

const FALLBACK_PRODUCT_LINKS = [
  { label: "AI", anchor: "ai" },
  { label: "Học tập", anchor: "education" },
  { label: "Giải trí / Steam", anchor: "entertainment" },
  { label: "Công việc (Office/Windows)", anchor: "workflows" },
  { label: "Thiết kế (Adobe)", anchor: "design" },
  { label: "Dev & Cloud", anchor: "dev" },
];

const BASE_NAV_ITEMS = [
  {
    label: "Danh mục sản phẩm",
    anchor: "product-list",
    path: "/product-list",
    dropdown: FALLBACK_PRODUCT_LINKS,
  },
  {
    label: "Dịch vụ hỗ trợ",
    anchor: "support-service",
    path: "/support/subscription",
    dropdown: [
      { label: "Các gói hỗ trợ", path: "/support/subscription" },
      {
        label: "Hướng dẫn sử dụng",
        path: "https://drive.google.com/file/d/1g5p5UI9luWWv-yn0VvWmq580WkBhv9JV/view",
      },
      { label: "Ticket hỗ trợ", path: "/tickets" },
    ],
  },
  {
    label: "Bài viết",
    anchor: "blog",
    path: "/blogs",
    dropdown: [
      { label: "Mẹo vặt", path: "/blog/tips" },
      { label: "Tin tức", path: "/blog/news" },
      { label: "Hướng dẫn nhanh", path: "/blog/quick-guides" },
    ],
  },
  {
    label: "Hướng dẫn",
    anchor: "docs",
    path: "/docs",
  },
];

const readCustomerFromStorage = () => {
  if (typeof window === "undefined") {
    return null;
  }
  try {
    const token = window.localStorage.getItem("access_token");
    const storedUser = window.localStorage.getItem("user");
    if (!token || !storedUser) {
      return null;
    }
    const parsed = JSON.parse(storedUser);
    return parsed?.profile ?? parsed;
  } catch (error) {
    console.error("Failed to parse stored user", error);
    return null;
  }
};

const getInitials = (name) => {
  if (!name) return "U";
  const chunks = name.trim().split(" ").filter(Boolean);
  if (!chunks.length) return "U";
  if (chunks.length === 1) {
    return chunks[0].charAt(0).toUpperCase();
  }
  return (
    chunks[0].charAt(0).toUpperCase() +
    chunks[chunks.length - 1].charAt(0).toUpperCase()
  );
};

const slugify = (value = "") =>
  value
    .toString()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");

const buildCategoryLink = (category) => {
  const label =
    category?.displayName ||
    category?.categoryName ||
    category?.name ||
    category?.CategoryName ||
    "";

  if (!label?.trim()) {
    return null;
  }

  const slug = slugify(category?.slug || label);
  const id = category?.categoryId ?? category?.id ?? slug;
  const path =
    category?.categoryId || category?.id
      ? `/product-list?category=${encodeURIComponent(
        category?.categoryId ?? category?.id
      )}`
      : `/product-list/${slug}`;

  return {
    label,
    anchor: `category-${slug}`,
    path,
    id,
  };
};

const getNavHref = (item) => {
  if (item?.path) {
    return item.path;
  }
  if (item?.anchor) {
    return `#${item.anchor}`;
  }
  return "#";
};

// ===== Helpers cho Notifications (giống Admin Header) =====
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

const getNotifSeverityLabel = (sev) => {
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

const formatNotificationTime = (value) => {
  if (!value) return "";
  try {
    return new Date(value).toLocaleString();
  } catch {
    return "";
  }
};

const PublicHeader = ({ settings, loading, profile, profileLoading }) => {
  const navigate = useNavigate();
  const [searchQuery, setSearchQuery] = useState("");
  const [customer, setCustomer] = useState(() =>
    profile ? profile : readCustomerFromStorage()
  );
  const [categories, setCategories] = useState([]);
  const [isLoadingCategories, setIsLoadingCategories] = useState(false);
  const [categoriesError, setCategoriesError] = useState("");
  const [openDropdown, setOpenDropdown] = useState(null);
  const [isAccountMenuOpen, setIsAccountMenuOpen] = useState(false);
  const accountMenuRef = useRef(null);

  // ===== NOTIFICATIONS (customer) =====
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [isNotifWidgetOpen, setIsNotifWidgetOpen] = useState(false);
  const [isNotifLoading, setIsNotifLoading] = useState(false);
  const [hasMoreNotifications, setHasMoreNotifications] = useState(true);
  const notifPagingRef = useRef({
    pageNumber: 0,
    pageSize: 5,
    hasMore: true,
  });
  const notifLoadTimerRef = useRef(null);
  const notifWidgetRef = useRef(null);
  const notifConnectionRef = useRef(null);
  const [toastQueue, setToastQueue] = useState([]);
  const [activeToast, setActiveToast] = useState(null);

  // ===== CART COUNT =====
  const [cartCount, setCartCount] = useState(0);

  const isCustomerMode = Boolean(customer);
  const displayName =
    customer?.fullName || customer?.username || customer?.displayName || "";
  const displayEmail =
    customer?.email || customer?.emailAddress || customer?.mail || "";
  const avatarUrl =
    customer?.avatarUrl || customer?.avatar || customer?.avatarURL || "";
  const customerInitials = getInitials(displayName);

  // ===== Fetch categories =====
  useEffect(() => {
    let isMounted = true;
    const fetchCategories = async () => {
      setIsLoadingCategories(true);
      setCategoriesError("");
      try {
        const result = await CategoryApi.list({
          pageSize: 6,
          active: true,
          sort: "displayorder",
          direction: "asc",
        });
        if (!isMounted) return;
        const mapped = (result || [])
          .map((category) => buildCategoryLink(category))
          .filter(Boolean);
        setCategories(mapped);
      } catch (error) {
        console.error("Cannot fetch categories for header", error);
        if (isMounted) {
          setCategoriesError("Không thể tải danh mục");
        }
      } finally {
        if (isMounted) {
          setIsLoadingCategories(false);
        }
      }
    };

    fetchCategories();

    return () => {
      isMounted = false;
    };
  }, []);

  // ===== Sync customer từ localStorage / props =====
  useEffect(() => {
    if (typeof window === "undefined") return undefined;
    const syncCustomer = () => {
      setCustomer(readCustomerFromStorage());
    };
    syncCustomer();
    window.addEventListener("storage", syncCustomer);
    return () => window.removeEventListener("storage", syncCustomer);
  }, []);

  useEffect(() => {
    if (profile) {
      setCustomer((prev) => ({ ...(prev || {}), ...profile }));
    } else if (!profileLoading) {
      setCustomer(readCustomerFromStorage());
    }
  }, [profile, profileLoading]);

  // ===== Click outside: account menu + notif widget =====
  useEffect(() => {
    const handleClickOutside = (event) => {
      if (
        accountMenuRef.current &&
        !accountMenuRef.current.contains(event.target)
      ) {
        setIsAccountMenuOpen(false);
      }

      if (
        notifWidgetRef.current &&
        !notifWidgetRef.current.contains(event.target) &&
        !event.target.closest(".alh-notif-bell")
      ) {
        setIsNotifWidgetOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () =>
      document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  // ===== CART: load ban đầu + lắng nghe event =====
  useEffect(() => {
    let isMounted = true;

    const initCartCount = async () => {
      // Nếu chưa đăng nhập thì coi như 0, vì cart server-side cần login
      if (!customer) {
        if (isMounted) setCartCount(0);
        return;
      }
      try {
        const res = await StorefrontCartApi.getCart();
        if (!isMounted) return;
        const count = Array.isArray(res.items) ? res.items.length : 0;
        setCartCount(count);
      } catch (error) {
        console.error("Cannot fetch cart in header", error);
        if (isMounted) setCartCount(0);
      }
    };

    initCartCount();

    const handleCartUpdated = (event) => {
      const cart = event.detail?.cart;
      if (!cart) return;
      const count = Array.isArray(cart.items) ? cart.items.length : 0;
      setCartCount(count);
    };

    if (typeof window !== "undefined") {
      window.addEventListener(CART_UPDATED_EVENT, handleCartUpdated);
    }

    return () => {
      isMounted = false;
      if (typeof window !== "undefined") {
        window.removeEventListener(CART_UPDATED_EVENT, handleCartUpdated);
      }
    };
  }, [customer]);

  const productDropdown = useMemo(() => {
    if (categories.length > 0) {
      return categories;
    }
    return FALLBACK_PRODUCT_LINKS;
  }, [categories]);

  const navItems = useMemo(() => {
    return [
      {
        ...BASE_NAV_ITEMS[0],
        dropdown: productDropdown,
      },
      ...BASE_NAV_ITEMS.slice(1),
    ];
  }, [productDropdown]);

  const closeDropdown = () => setOpenDropdown(null);

  const handleSearch = (event) => {
    event.preventDefault();
    const query = searchQuery.trim();
    if (!query) {
      return;
    }
    const params = new URLSearchParams({ q: query });
    navigate(`/search?${params.toString()}`);
  };

  const handleNavigation = (event, path) => {
    event.preventDefault();
    navigate(path);
  };

  const handleTopItemClick = (event, item, isOpen) => {
    if (!item?.dropdown) {
      if (item?.path) {
        closeDropdown();
        handleNavigation(event, item.path);
      }
      return;
    }

    if (!isOpen) {
      event.preventDefault();
      setOpenDropdown(item.label);
      return;
    }

    if (item.path) {
      closeDropdown();
      handleNavigation(event, item.path);
    }
  };

  const handleMenuBlur = (event) => {
    if (!event.currentTarget.contains(event.relatedTarget)) {
      closeDropdown();
    }
  };

  const toggleAccountMenu = () => {
    setIsAccountMenuOpen((open) => !open);
  };

  const handleAccountAction = (action) => {
    setIsAccountMenuOpen(false);
    switch (action) {
      case "profile":
        navigate("/profile");
        break;
      case "orders":
        navigate("/orders");
        break;
      case "support":
        navigate("/support");
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
      console.error("Logout failed", error);
    } finally {
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
      localStorage.removeItem("user");
      setCustomer(null);
      setCartCount(0); // clear luôn badge cart
      setIsAccountMenuOpen(false);
      navigate("/login");
    }
  };

  const siteName = settings?.name || "Keytietkiem";

  // ====== Notifications: poll unread count định kỳ ======
  useEffect(() => {
    let isMounted = true;

    const fetchUnreadNotifications = async () => {
      const token = localStorage.getItem("access_token");
      if (!token || !customer) {
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
        console.error(
          "Failed to fetch unread notifications (public header):",
          error
        );
      }
    };

    fetchUnreadNotifications();
    const intervalId = setInterval(fetchUnreadNotifications, 15000);

    return () => {
      isMounted = false;
      clearInterval(intervalId);
    };
  }, [customer]);

  // ====== Lấy lịch sử thông báo khi mở widget ======
  const fetchNotificationHistory = async ({ append = false } = {}) => {
    const token = localStorage.getItem("access_token");
    if (!token || !customer) {
      setNotifications([]);
      setHasMoreNotifications(false);
      return;
    }

    const paging = notifPagingRef.current;
    const nextPageNumber = append ? paging.pageNumber + 1 : 1;
    const pageSize = paging.pageSize;

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
      console.error("Failed to fetch notification history (public header):", error);
    } finally {
      setIsNotifLoading(false);
    }
  };

  // ====== Auto load thêm mỗi 3s nếu widget đang mở ======
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

  // ====== SignalR: nhận "ReceiveNotification" realtime ======
  useEffect(() => {
    const token = localStorage.getItem("access_token");
    if (!token || !customer) {
      return;
    }

    const apiBase = axiosClient.defaults.baseURL || "";
    const hubUrl = apiBase
      ? apiBase.replace(/\/api\/?$/, "") + "/hubs/notifications"
      : "https://localhost:7292/hubs/notifications";

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

      setNotifications((prev) => {
        const existingIds = new Set(
          prev.map((x) => extractNotificationId(x)).filter(Boolean)
        );
        if (existingIds.has(id)) {
          return prev;
        }
        return [n, ...prev];
      });

      if (!isRead) {
        setUnreadCount((prev) => prev + 1);
      }

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
        console.error("Failed to connect NotificationHub (public header):", err)
      );

    return () => {
      if (notifConnectionRef.current) {
        notifConnectionRef.current.off("ReceiveNotification");
        notifConnectionRef.current.stop();
        notifConnectionRef.current = null;
      }
    };
  }, [customer]);

  // ===== Toast queue (giống admin) =====
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

  const handleToastClose = () => {
    setActiveToast(null);
  };

  // ===== Hover item => mark read =====
  const handleNotificationHover = (item) => {
    const notifUserId = extractNotificationId(item);
    const isRead = item.isRead ?? item.IsRead ?? false;
    if (!notifUserId || isRead) return;

    setNotifications((prev) =>
      prev.map((n) => {
        const id = extractNotificationId(n);
        if (id !== notifUserId) return n;
        return { ...n, isRead: true, IsRead: true };
      })
    );
    setUnreadCount((prev) => (prev > 0 ? prev - 1 : 0));

    NotificationsApi.markMyNotificationRead(notifUserId).catch((err) => {
      console.error("Failed to mark notification as read (public header)", err);
    });
  };

  // ===== Click item => điều hướng tới RelatedUrl =====
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

  // ===== Bấm chuông => mở / đóng widget =====
  const handleNotifBellClick = () => {
    setIsNotifWidgetOpen((prev) => {
      const next = !prev;
      if (!prev && next) {
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

  return (
    <>
      <div
        className="topbar"
        data-mode={isCustomerMode ? "customer" : "guest"}
        role="banner"
      >
        <div className="container header-public">
          <a
            className="logo"
            href="/"
            onClick={(event) => handleNavigation(event, "/")}
          >
            {settings?.logoUrl ? (
              <img
                src={settings.logoUrl}
                alt={siteName}
                style={{ height: "36px", width: "auto", objectFit: "contain" }}
              />
            ) : (
              <div className="mark">K</div>
            )}
            <span>{loading ? "Keytietkiem" : siteName}</span>
          </a>

          <form className="searchbar" onSubmit={handleSearch} role="search">
            <input
              type="search"
              placeholder="Tìm: Office 365, Windows 11 Pro, ChatGPT Plus, Adobe..."
              aria-label="Tìm kiếm sản phẩm"
              value={searchQuery}
              onChange={(event) => setSearchQuery(event.target.value)}
            />
            <button className="btn" type="submit">
              Tìm kiếm
            </button>
          </form>

          {/* Guest mode: giữ nguyên */}
          {!isCustomerMode && (
            <div className="account guest-only">
              <a
                className="btn cart-btn"
                href="/cart"
                onClick={(event) => handleNavigation(event, "/cart")}
              >
                <span className="cart-icon" aria-hidden="true">
                  🛒
                </span>
                <span className="cart-label">Giỏ hàng</span>
                {cartCount > 0 && (
                  <span
                    className="cart-badge"
                    aria-label={`${cartCount} sản phẩm trong giỏ hàng`}
                  >
                    {cartCount}
                  </span>
                )}
              </a>
              <a
                className="btn"
                href="/login"
                onClick={(event) => handleNavigation(event, "/login")}
              >
                Đăng nhập
              </a>
              <a
                className="btn primary"
                href="/register"
                onClick={(event) => handleNavigation(event, "/register")}
              >
                Đăng ký
              </a>
            </div>
          )}

          {/* Customer mode: BỎ nút "Đơn hàng" -> thêm icon chuông thông báo */}
          {isCustomerMode && (
            <div className="account customer-only" ref={accountMenuRef}>
              <a
                className="btn cart-btn"
                href="/cart"
                onClick={(event) => handleNavigation(event, "/cart")}
              >
                <span className="cart-icon" aria-hidden="true">
                  🛒
                </span>
                <span className="cart-label">Giỏ hàng</span>
                {cartCount > 0 && (
                  <span
                    className="cart-badge"
                    aria-label={`${cartCount} sản phẩm trong giỏ hàng`}
                  >
                    {cartCount}
                  </span>
                )}
              </a>

              {/* 🔔 Chuông thông báo giống admin header */}
              <div className="alh-notif-wrapper">
                <button
                  type="button"
                  className="btn cart-btn alh-notif-bell"
                  title="Thông báo"
                  aria-label="Thông báo"
                  onClick={handleNotifBellClick}
                >
                  🔔
                  {unreadCount > 0 && (
                    <span className="alh-notif-badge">
                      {unreadCount > 99 ? "99+" : unreadCount}
                    </span>
                  )}
                </button>
              </div>

              <button
                type="button"
                className="account-trigger"
                onClick={toggleAccountMenu}
                aria-haspopup="true"
                aria-expanded={isAccountMenuOpen}
              >
                <div className="avatar" aria-hidden="true">
                  {avatarUrl ? (
                    <img src={avatarUrl} alt="Ảnh đại diện" />
                  ) : (
                    customerInitials
                  )}
                </div>
                <div className="account-labels">
                  <span>{displayName || "Tài khoản"}</span>
                  {displayEmail && <small>{displayEmail}</small>}
                </div>
                <svg
                  width="16"
                  height="16"
                  viewBox="0 0 24 24"
                  fill="none"
                  aria-hidden="true"
                >
                  <path
                    d="M6 9l6 6 6-6"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                </svg>
              </button>

              {isAccountMenuOpen && (
                <div className="account-dropdown" role="menu">
                  <button
                    className="account-dropdown-item"
                    onClick={() => handleAccountAction("profile")}
                  >
                    Hồ sơ của tôi
                  </button>
                  <button
                    className="account-dropdown-item"
                    onClick={() => handleAccountAction("support")}
                  >
                    Liên hệ hỗ trợ
                  </button>
                  <div className="account-dropdown-divider" />
                  <button
                    className="account-dropdown-item logout"
                    onClick={() => handleAccountAction("logout")}
                  >
                    Đăng xuất
                  </button>
                </div>
              )}
            </div>
          )}

          <nav className="navbar" aria-label="Điều hướng chính">
            {navItems.map((item) => {
              const hasDropdown = Boolean(item.dropdown?.length);
              const isOpen = openDropdown === item.label;

              return (
                <div
                  className={`nav-item${isOpen ? " open" : ""}`}
                  key={item.label}
                  onMouseEnter={() => {
                    if (hasDropdown) {
                      setOpenDropdown(item.label);
                    }
                  }}
                  onMouseLeave={() => {
                    if (hasDropdown) {
                      closeDropdown();
                    }
                  }}
                  onFocus={() => {
                    if (hasDropdown) {
                      setOpenDropdown(item.label);
                    }
                  }}
                  onBlur={hasDropdown ? handleMenuBlur : undefined}
                >
                  <a
                    className="nav-link"
                    href={getNavHref(item)}
                    aria-haspopup={hasDropdown ? "true" : undefined}
                    aria-expanded={hasDropdown ? isOpen : undefined}
                    onClick={(event) =>
                      handleTopItemClick(event, item, isOpen)
                    }
                  >
                    <strong>
                      {item.label}
                      {hasDropdown ? " ▾" : ""}
                    </strong>
                  </a>

                  {hasDropdown && (
                    <div className="dropdown">
                      {item.label === "Danh mục sản phẩm" &&
                        isLoadingCategories && (
                          <div className="dropdown-status">Đang tải...</div>
                        )}
                      {item.label === "Danh mục sản phẩm" &&
                        categoriesError && (
                          <div className="dropdown-status error">
                            {categoriesError}
                          </div>
                        )}
                      {item.dropdown.map((subItem) => (
                        <a
                          key={subItem.label}
                          href={getNavHref(subItem)}
                          target={
                            subItem.path?.startsWith("http")
                              ? "_blank"
                              : undefined
                          }
                          rel={
                            subItem.path?.startsWith("http")
                              ? "noopener noreferrer"
                              : undefined
                          }
                          onClick={(event) => {
                            if (subItem.path) {
                              if (
                                subItem.path.startsWith("http://") ||
                                subItem.path.startsWith("https://")
                              ) {
                                closeDropdown();
                                return;
                              }
                              handleNavigation(event, subItem.path);
                              closeDropdown();
                            }
                          }}
                        >
                          {subItem.label}
                        </a>
                      ))}
                    </div>
                  )}
                </div>
              );
            })}
          </nav>
        </div>
      </div>

      {/* ====== Widget thông báo giống admin header ====== */}
      {isCustomerMode && isNotifWidgetOpen && (
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
                            {getNotifSeverityLabel(severity)}
                          </span>
                          <span className="alh-notif-dot-sep">•</span>
                          <span className="alh-notif-time">
                            {formatNotificationTime(createdAt)}
                          </span>
                        </div>
                      </div>
                    </div>
                  );
                })}

              {isNotifLoading && notifications.length > 0 && (
                <div className="alh-notif-empty">Đang tải thêm...</div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* ====== Toast thông báo mới giống admin header ====== */}
      {isCustomerMode && activeToast && (
        <div className="alh-toast" role="status" aria-live="polite">
          <div className="alh-toast-inner">
            <div className="alh-toast-indicator">
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
                {getNotifSeverityLabel(activeToast.severity)} ·{" "}
                {activeToast.title}
              </div>
              <div className="alh-toast-message">
                {activeToast.message &&
                  activeToast.message.length > 100
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

export default PublicHeader;
