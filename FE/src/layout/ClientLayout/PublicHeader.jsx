import React, { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { CategoryApi } from "../../services/categories";
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
  if (!name) {
    return "U";
  }

  const chunks = name.trim().split(" ").filter(Boolean);
  if (!chunks.length) {
    return "U";
  }

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

        if (!isMounted) {
          return;
        }

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
          <div className="mark">K</div>
          Keytietkiem
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

        {/* <div className="account customer-only">
          <a
            className="btn"
            href="/cart"
            onClick={(event) => handleNavigation(event, "/cart")}
          >
            🛒 Giỏ hàng
          </a>
          <a
            className="btn"
            href="/orders"
            onClick={(event) => handleNavigation(event, "/orders")}
          >
            Đơn hàng
          </a>
          <a
            className="btn"
            href="/support"
            onClick={(event) => handleNavigation(event, "/support")}
          >
            Hỗ trợ
          </a>
          <div
            className="avatar"
            aria-label={
              customer?.fullName
                ? `Tài khoản ${customer.fullName}`
                : "Tài khoản khách hàng"
            }
          >
            {customerInitials}
          </div>
        </div> */}

        <nav className="navbar" aria-label="Điều hướng chính">
          {navItems.map((item) => (
            <div className="nav-item" key={item.label}>
              <a
                className="nav-link"
                href={getNavHref(item)}
                onClick={(event) => {
                  if (item.path) {
                    handleNavigation(event, item.path);
                  }
                }}
              >
                <strong>
                  {item.label}
                  {item.dropdown ? " ▾" : ""}
                </strong>
              </a>

              {item.dropdown && (
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
                        }
                      }}
                    >
                      {subItem.label}
                    </a>
                  ))}
                </div>
              )}
            </div>
          ))}
        </nav>
      </div>
    </div>
  );
};

export default PublicHeader;
