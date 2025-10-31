/**
 * File: ToastContainer.jsx
 * Author: HieuNDHE173169
 * Created: 21/10/2025
 * Last Updated: 295/10/2025
 * Version: 1.0.0
 * Purpose: Container component for toast notifications and confirmation dialogs.
 */

import React from 'react';
import Toast from './Toast';
import ConfirmDialog from './ConfirmDialog';
import './Toast.css';

/**
 * @summary: Container component that renders all active toasts and confirmation dialog.
 * @param {Object} props - Component props
 * @param {Array} props.toasts - Array of active toast notifications
 * @param {Function} props.onRemove - Callback to remove a toast by id
 * @param {Object|null} props.confirmDialog - Confirmation dialog configuration
 * @returns {JSX.Element} - Toast container with toasts and dialog
 */
const ToastContainer = ({ toasts, onRemove, confirmDialog }) => {
  return (
    <>
      {toasts && toasts.length > 0 && (
        <div className="toast-container">
          {toasts.map((toast) => (
            <Toast
              key={toast.id}
              toast={toast}
              onRemove={onRemove}
            />
          ))}
        </div>
      )}
      {confirmDialog && (
        <ConfirmDialog
          isOpen={true}
          title={confirmDialog.title}
          message={confirmDialog.message}
          onConfirm={confirmDialog.onConfirm}
          onCancel={confirmDialog.onCancel}
        />
      )}
    </>
  );
};

export default ToastContainer;