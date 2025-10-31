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
import "./Header.css";

/**
 * @summary: Header component for admin layout with search, notifications, and user menu.
 * @returns {JSX.Element} - Header with search bar, notification icon, and user dropdown
 */
const Header = () => {
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const dropdownRef = useRef(null);

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

    document.addEventListener('mousedown', handleClickOutside);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, []);

  /**
   * @summary: Toggle user dropdown menu visibility.
   */
  const handleAvatarClick = () => {
    setIsDropdownOpen(!isDropdownOpen);
  };

  /**
   * @summary: Handle menu item click actions (profile, settings, logout, etc.).
   * @param {string} action - Action identifier (e.g., 'profile', 'logout')
   */
  const handleMenuAction = (action) => {
    console.log(`Action: ${action}`);
    setIsDropdownOpen(false);
  };

  return (
    <div className="header" role="banner">
      <div className="search">
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
      <div className="right">
        <span className="pill" title="Tháng hiện tại" aria-label="Tháng 10/2025">
          10/2025
        </span>
        <span className="pill" title="Thông báo" aria-label="Thông báo">
          🔔
        </span>
        <div className="avatar-container" ref={dropdownRef}>
          <div 
            className="avatar" 
            aria-label="Tài khoản"
            onClick={handleAvatarClick}
          >
            <span>R</span>
          </div>
          
          {isDropdownOpen && (
            <div className="dropdown-menu">
              <div className="dropdown-header">
                <div className="user-info">
                  <div className="user-avatar">
                    <span>R</span>
                  </div>
                  <div className="user-details">
                    <div className="user-name">Admin User</div>
                    <div className="user-email">admin@keytietkiem.com</div>
                  </div>
                </div>
              </div>
              
              <div className="dropdown-items">
                <button 
                  className="dropdown-item"
                  onClick={() => handleMenuAction('profile')}
                >
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
                    <path
                      d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"
                      stroke="currentColor"
                      strokeWidth="2"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                    <circle cx="12" cy="7" r="4" stroke="currentColor" strokeWidth="2"/>
                  </svg>
                  Xem Profile
                </button>
                
                <button 
                  className="dropdown-item"
                  onClick={() => handleMenuAction('change-avatar')}
                >
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
                    <path
                      d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2v11Z"
                      stroke="currentColor"
                      strokeWidth="2"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                    <circle cx="12" cy="13" r="4" stroke="currentColor" strokeWidth="2"/>
                  </svg>
                  Thay Avatar
                </button>
                
                <button 
                  className="dropdown-item"
                  onClick={() => handleMenuAction('settings')}
                >
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
                    <circle cx="12" cy="12" r="3" stroke="currentColor" strokeWidth="2"/>
                    <path
                      d="M12 1v6m0 6v6m11-7h-6m-6 0H1m15.5-6.5l-4.24 4.24M8.76 8.76 4.52 4.52m14.96 14.96-4.24-4.24M8.76 15.24 4.52 19.48"
                      stroke="currentColor"
                      strokeWidth="2"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </svg>
                  Cài đặt
                </button>
                
                <div className="dropdown-divider"></div>
                
                <button 
                  className="dropdown-item logout"
                  onClick={() => handleMenuAction('logout')}
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