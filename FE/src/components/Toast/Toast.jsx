/**
 * File: Toast.jsx
 * Author: HieuNDHE173169
 * Created: 21/10/2025
 * Last Updated: 29/10/2025
 * Version: 1.0.0
 * Purpose: Toast notification component. Displays success, error, warning, and info messages.
 */
import React from 'react';
import './Toast.css';

/**
 * @summary: Toast notification component.
 * @param {Object} props - Component props
 * @param {Object} props.toast - Toast configuration object
 * @param {Function} props.onRemove - Callback to remove toast
 * @returns {JSX.Element} - Toast notification element
 */
const Toast = ({ toast, onRemove }) => {

  /**
   * @summary: Get icon SVG for toast type.
   * @param {string} type - Toast type ('success', 'error', 'warning', 'info')
   * @returns {JSX.Element|null} - Icon SVG element
   */
  const getToastIcon = (type) => {
    switch (type) {
      case 'success':
        return (
          <svg viewBox="0 0 24 24" fill="currentColor" className="toast-icon">
            <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/>
          </svg>
        );
      case 'error':
        return (
          <svg viewBox="0 0 24 24" fill="currentColor" className="toast-icon">
            <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
          </svg>
        );
      case 'warning':
        return (
          <svg viewBox="0 0 24 24" fill="currentColor" className="toast-icon">
            <path d="M12 2l9 20H3l9-20zm0 3.5L5.5 20h13L12 5.5zM11 15h2v2h-2v-2zm0-6h2v4h-2V9z"/>
          </svg>
        );
      default:
        return null;
    }
  };

  /**
   * @summary: Get CSS class name for toast type.
   * @param {string} type - Toast type ('success', 'error', 'warning', 'info')
   * @returns {string} - CSS class name
   */
  const getToastClass = (type) => {
    switch (type) {
      case 'success':
        return 'toast-success';
      case 'error':
        return 'toast-error';
      case 'warning':
        return 'toast-warning';
      default:
        return 'toast-info';
    }
  };

  return (
    <div className={`toast ${getToastClass(toast.type)}`}>
      <div className="toast-content">
        <div className="toast-icon-container">
          {getToastIcon(toast.type)}
        </div>
        <div className="toast-message">
          <div className="toast-title">{toast.title}</div>
          {toast.message && <div className="toast-description">{toast.message}</div>}
        </div>
        <button 
          className="toast-close" 
          onClick={() => onRemove(toast.id)}
          aria-label="Đóng thông báo"
        >
          <svg viewBox="0 0 24 24" fill="currentColor">
            <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
          </svg>
        </button>
      </div>
    </div>
  );
};

export default Toast;