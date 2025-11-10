/**
 * File: ConfirmDialog.jsx
 * Author: HieuNDHE173169
 * Created: 21/10/2025
 * Last Updated: 29/10/2025
 * Version: 1.0.0
 * Purpose: Confirmation dialog component for user confirmations.
 */
import React from 'react';
import './ConfirmDialog.css';

/**
 * @summary: Confirmation dialog component.
 * @param {Object} props - Component props
 * @param {boolean} props.isOpen - Whether dialog is visible
 * @param {string} props.title - Dialog title
 * @param {string} props.message - Dialog message
 * @param {Function} props.onConfirm - Callback when user confirms
 * @param {Function} props.onCancel - Callback when user cancels
 * @param {string} props.confirmText - Confirm button text (default: 'Đồng ý')
 * @param {string} props.cancelText - Cancel button text (default: 'Hủy')
 * @returns {JSX.Element|null} - Confirmation dialog element or null if not open
 */
const ConfirmDialog = ({ isOpen, title, message, onConfirm, onCancel, confirmText = 'Đồng ý', cancelText = 'Hủy' }) => {
  if (!isOpen) return null;

  return (
    <div className="confirm-dialog-overlay" onClick={onCancel}>
      <div className="confirm-dialog" onClick={(e) => e.stopPropagation()}>
        <div className="confirm-dialog-icon">
          <svg viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/>
          </svg>
        </div>
        <h3 className="confirm-dialog-title">{title}</h3>
        {message && <p className="confirm-dialog-message">{message}</p>}
        <div className="confirm-dialog-actions">
          <button 
            className="confirm-dialog-btn confirm-dialog-btn-cancel" 
            onClick={onCancel}
          >
            {cancelText}
          </button>
          <button 
            className="confirm-dialog-btn confirm-dialog-btn-confirm" 
            onClick={onConfirm}
          >
            {confirmText}
          </button>
        </div>
      </div>
    </div>
  );
};

export default ConfirmDialog;
