/**
 * File: useToast.js
 * Author: HieuNDHE173169
 * Created: 20/10/2025
 * Last Updated: 29/10/2025
 * Version: 1.0.0
 * Purpose: Custom React hook for managing toast notifications and confirmation dialogs.
 *          Provides methods to display success, error, warning, and info toasts with
 *          auto-dismiss functionality and confirm dialog support.
 */
import { useState, useCallback } from 'react';

/**
 * @summary: Custom hook for toast notification and confirmation dialog management.
 * @returns {Object} - Object containing toast state and methods:
 *   - toasts: Array of active toasts
 *   - confirmDialog: Current confirmation dialog state
 *   - addToast: Function to add a toast
 *   - removeToast: Function to remove a toast by id
 *   - clearAllToasts: Function to clear all toasts
 *   - showSuccess: Function to show success toast
 *   - showError: Function to show error toast
 *   - showWarning: Function to show warning toast
 *   - showInfo: Function to show info toast
 *   - showConfirm: Function to show confirmation dialog
 */
const useToast = () => {
  const [toasts, setToasts] = useState([]);
  const [confirmDialog, setConfirmDialog] = useState(null);

  /**
   * @summary: Remove a toast from the list by id.
   * @param {number|string} id - Toast identifier
   * @returns {void}
   */
  const removeToast = useCallback((id) => {
    setToasts(prev => prev.filter(toast => toast.id !== id));
  }, []);

  /**
   * @summary: Add a new toast notification to the list.
   * @param {Object} toast - Toast configuration object
   * @param {string} toast.type - Toast type ('success', 'error', 'warning', 'info')
   * @param {string} toast.title - Toast title
   * @param {string} toast.message - Toast message
   * @param {number} toast.duration - Auto-dismiss duration in ms (0 = persistent)
   * @returns {number|string} - Generated toast id
   */
  const addToast = useCallback((toast) => {
    const id = Date.now() + Math.random();
    const newToast = {
      id,
      type: toast.type || 'info',
      title: toast.title || '',
      message: toast.message || '',
      duration: toast.duration || 5000,
      ...toast
    };

    setToasts(prev => [...prev, newToast]);

    // Auto remove toast after duration
    if (newToast.duration > 0) {
      setTimeout(() => {
        removeToast(id);
      }, newToast.duration);
    }

    return id;
  }, [removeToast]);

  /**
   * @summary: Clear all active toasts.
   * @returns {void}
   */
  const clearAllToasts = useCallback(() => {
    setToasts([]);
  }, []);

  /**
   * @summary: Show a success toast notification.
   * @param {string} title - Toast title
   * @param {string} message - Toast message (optional)
   * @param {Object} options - Additional toast options (optional)
   * @returns {number|string} - Toast id
   */
  const showSuccess = useCallback((title, message = '', options = {}) => {
    return addToast({
      type: 'success',
      title,
      message,
      ...options
    });
  }, [addToast]);

  /**
   * @summary: Show an error toast notification.
   * @param {string} title - Toast title
   * @param {string} message - Toast message (optional)
   * @param {Object} options - Additional toast options (optional)
   * @returns {number|string} - Toast id
   */
  const showError = useCallback((title, message = '', options = {}) => {
    return addToast({
      type: 'error',
      title,
      message,
      ...options
    });
  }, [addToast]);

  /**
   * @summary: Show a warning toast notification.
   * @param {string} title - Toast title
   * @param {string} message - Toast message (optional)
   * @param {Object} options - Additional toast options (optional)
   * @returns {number|string} - Toast id
   */
  const showWarning = useCallback((title, message = '', options = {}) => {
    return addToast({
      type: 'warning',
      title,
      message,
      ...options
    });
  }, [addToast]);

  /**
   * @summary: Show an info toast notification.
   * @param {string} title - Toast title
   * @param {string} message - Toast message (optional)
   * @param {Object} options - Additional toast options (optional)
   * @returns {number|string} - Toast id
   */
  const showInfo = useCallback((title, message = '', options = {}) => {
    return addToast({
      type: 'info',
      title,
      message,
      ...options
    });
  }, [addToast]);

  /**
   * @summary: Show a confirmation dialog.
   * @param {string} title - Dialog title
   * @param {string} message - Dialog message
   * @param {Function} onConfirm - Callback function when user confirms
   * @param {Function} onCancel - Callback function when user cancels
   * @returns {void}
   */
  const showConfirm = useCallback((title, message, onConfirm, onCancel) => {
    setConfirmDialog({
      title,
      message,
      onConfirm: () => {
        setConfirmDialog(null);
        if (onConfirm) onConfirm();
      },
      onCancel: () => {
        setConfirmDialog(null);
        if (onCancel) onCancel();
      }
    });
  }, []);

  return {
    toasts,
    confirmDialog,
    addToast,
    removeToast,
    clearAllToasts,
    showSuccess,
    showError,
    showWarning,
    showInfo,
    showConfirm
  };
};

export default useToast;
