import React, { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { CategoryApi } from "../../services/categories";
import { AuthService } from "../../services/authService";
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
    path: "/support-service",
    dropdown: [
      { label: "Hỗ trợ cài đặt từ xa", path: "/support-service/remote" },
      { label: "Hướng dẫn sử dụng", path: "/support-service/manual" },
      { label: "Fix lỗi phần mềm đã mua", path: "/support-service/fix" },
    ],
  },
  {
    label: "Bài viết",
    anchor: "blog",
    path: "/blog",
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
    return JSON.parse(storedUser);
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

const PublicHeader = () => {
  const navigate = useNavigate();
  const [searchQuery, setSearchQuery] = useState("");
  const [customer, setCustomer] = useState(() => readCustomerFromStorage());
  const [categories, setCategories] = useState([]);
  const [isLoadingCategories, setIsLoadingCategories] = useState(false);
  const [categoriesError, setCategoriesError] = useState("");
  const [openDropdown, setOpenDropdown] = useState(null);
  const [isAccountMenuOpen, setIsAccountMenuOpen] = useState(false);
  const accountMenuRef = useRef(null);
  const isCustomerMode = Boolean(customer);
  const customerInitials = getInitials(
    customer?.fullName || customer?.username || ""
  );

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

  useEffect(() => {
    const syncCustomer = () => {
      setCustomer(readCustomerFromStorage());
    };
    syncCustomer();
    window.addEventListener("storage", syncCustomer);
    return () => window.removeEventListener("storage", syncCustomer);
  }, []);

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (
        accountMenuRef.current &&
        !accountMenuRef.current.contains(event.target)
      ) {
        setIsAccountMenuOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

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
      setIsAccountMenuOpen(false);
      navigate("/login");
    }
  };

  return (
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
          {settings.logoUrl ? (
            <img
              src={settings.logoUrl}
              alt={settings.name}
              style={{ height: '36px', width: 'auto', objectFit: 'contain' }}
            />
          ) : (
            <div className="mark">K</div>
          )}
          <span>{loading ? 'Keytietkiem' : settings.name}</span>
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

        {!isCustomerMode && (
          <div className="account guest-only">
            <a
              className="btn"
              href="/cart"
              onClick={(event) => handleNavigation(event, "/cart")}
            >
              🛒 Giỏ hàng
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

        {isCustomerMode && (
          <div className="account customer-only" ref={accountMenuRef}>
            <a
              className="btn"
              href="/cart"
              onClick={(event) => handleNavigation(event, "/cart")}
            >
              🛒 Giỏ hàng
            </a>
            <a
              className="btn subtle"
              href="/orders"
              onClick={(event) => handleNavigation(event, "/orders")}
            >
              Đơn hàng
            </a>
            <button
              type="button"
              className="account-trigger"
              onClick={toggleAccountMenu}
              aria-haspopup="true"
              aria-expanded={isAccountMenuOpen}
            >
              <div className="avatar" aria-hidden="true">
                {customerInitials}
              </div>
              <div className="account-labels">
                <span>{customer?.fullName || customer?.username}</span>
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
                  onClick={() => handleAccountAction("orders")}
                >
                  Đơn hàng
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
                  onClick={(event) => handleTopItemClick(event, item, isOpen)}
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
                    {item.label === "Danh mục sản phẩm" && categoriesError && (
                      <div className="dropdown-status error">
                        {categoriesError}
                      </div>
                    )}
                    {item.dropdown.map((subItem) => (
                      <a
                        key={subItem.label}
                        href={getNavHref(subItem)}
                        onClick={(event) => {
                          if (subItem.path) {
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
  );
};

export default PublicHeader;
