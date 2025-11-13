import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";

const NAV_ITEMS = [
  {
    label: "Danh mục sản phẩm",
    anchor: "product-list",
    dropdown: [
      { label: "AI", anchor: "ai" },
      { label: "Học tập", anchor: "education" },
      { label: "Giải trí / Steam", anchor: "entertainment" },
      { label: "Công việc (Office/Windows)", anchor: "workflows" },
      { label: "Thiết kế (Adobe)", anchor: "design" },
      { label: "Dev & Cloud", anchor: "dev" },
    ],
  },
  {
    label: "Dịch vụ hỗ trợ",
    anchor: "support",
    dropdown: [
      { label: "Hỗ trợ cài đặt từ xa", anchor: "remote-support" },
      { label: "Hướng dẫn sử dụng", anchor: "how-to" },
      { label: "Fix lỗi phần mềm đã mua", anchor: "fix-issues" },
    ],
  },
  {
    label: "Bài viết",
    anchor: "blog",
    dropdown: [
      { label: "Mẹo vặt", anchor: "tips" },
      { label: "Tin tức", anchor: "news" },
      { label: "Hướng dẫn nhanh", anchor: "quick-guides" },
    ],
  },
  {
    label: "Hướng dẫn",
    anchor: "docs",
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

const PublicHeader = () => {
  const navigate = useNavigate();
  const [searchQuery, setSearchQuery] = useState("");
  const [customer, setCustomer] = useState(() => readCustomerFromStorage());
  const isCustomerMode = Boolean(customer);
  const customerInitials = getInitials(
    customer?.fullName || customer?.username || ""
  );

  useEffect(() => {
    const syncCustomer = () => {
      setCustomer(readCustomerFromStorage());
    };

    syncCustomer();
    window.addEventListener("storage", syncCustomer);
    return () => window.removeEventListener("storage", syncCustomer);
  }, []);

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
            Giỏ hàng
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

        <div className="account customer-only">
          <a
            className="btn"
            href="/cart"
            onClick={(event) => handleNavigation(event, "/cart")}
          >
            Giỏ hàng
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
        </div>

        <nav className="navbar" aria-label="Điều hướng chính">
          {NAV_ITEMS.map((item) => (
            <div className="nav-item" key={item.label}>
              <a className="nav-link" href={`#${item.anchor}`}>
                <strong>
                  {item.label}
                  {item.dropdown ? " ▾" : ""}
                </strong>
              </a>

              {item.dropdown && (
                <div className="dropdown">
                  {item.dropdown.map((subItem) => (
                    <a key={subItem.label} href={`#${subItem.anchor}`}>
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
