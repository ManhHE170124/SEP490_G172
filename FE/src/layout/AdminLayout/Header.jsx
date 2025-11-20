/**
 * File: Header.jsx
 * Author: HieuNDHE173169
 * Created: 18/10/2025
 * Last Updated: 29/10/2025
 * Version: 1.0.0
 * Purpose: Admin header component with search, notifications, and user dropdown menu.
 *          Provides navigation and user account management interface.
 */
import React, { useState, useRef, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { AuthService } from "../../services/authService";
import "./Header.css";

/**
 * @summary: Header component for admin layout with search, notifications, and user menu.
 * @returns {JSX.Element} - Header with search bar, notification icon, and user dropdown
 */
const Header = ({ profile }) => {
  const navigate = useNavigate();
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const [user, setUser] = useState(null);
  const dropdownRef = useRef(null);

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

  // Load user and listen for changes
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

  // Close dropdown when clicking outside
  /**
   * @summary: Close dropdown menu when clicking outside the avatar container.
   * Effect: Attaches and cleans up click outside event listener.
   */
  useEffect(() => {
    const handleClickOutside = (event) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
        setIsDropdownOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, []);

  /**
   * @summary: Toggle user dropdown menu visibility.
   */
  const handleAvatarClick = () => {
    loadUser();
    setIsDropdownOpen(!isDropdownOpen);
  };

  /**
   * @summary: Handle menu item click actions (profile, settings, logout, etc.).
   * @param {string} action - Action identifier (e.g., 'profile', 'logout')
   */
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
      // Call logout API to revoke tokens
      await AuthService.logout();
    } catch (error) {
      console.error("Logout error:", error);
      // Clear tokens from localStorage even if API call fails
      localStorage.removeItem("access_token");
      localStorage.removeItem("refresh_token");
      localStorage.removeItem("user");
    } finally {
      // Redirect to login page
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
    <div className="alh-header" role="banner">
      <div className="alh-search">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
          <path
            d="M21 21l-4.2-4.2M10.5 18a7.5 7.5 0 1 1 0-15 7.5 7.5 0 0 1 0 15Z"
            stroke="#6b7280"
            strokeWidth="2"
            strokeLinecap="round"
          />
        </svg>
        <input
          type="search"
          placeholder="Tìm kiếm đơn hàng, key..."
          aria-label="Tìm kiếm"
        />
      </div>
      <div className="alh-right">
        <span
          className="alh-pill"
          title="Tháng hiện tại"
          aria-label="Tháng 10/2025"
        >
          10/2025
        </span>
        <span className="alh-pill" title="Thông báo" aria-label="Thông báo">
          🔔
        </span>
        <div className="alh-avatar-container" ref={dropdownRef}>
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
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
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
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
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
  );
};

export default Header;
