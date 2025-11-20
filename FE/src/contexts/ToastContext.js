import React, { createContext, useContext, useCallback, useState, useMemo } from 'react';
import ToastContainer from '../components/Toast/ToastContainer';

const ToastContext = createContext(null);

export const useToast = () => {
    const ctx = useContext(ToastContext);
    if (!ctx) throw new Error('useToast must be used within ToastProvider');
    return ctx;
};

let idSeed = 1;

export const ToastProvider = ({ children }) => {
    const [toasts, setToasts] = useState([]);
    const [confirmDialog, setConfirmDialog] = useState(null);

    const addToast = useCallback((opts) => {
        const id = opts.id || `toast_${Date.now()}_${idSeed++}`;
        const toast = {
            id,
            type: opts.type || 'info', // success|error|warning|info
            title: opts.title || '',
            message: opts.message || '',
            duration: typeof opts.duration === 'number' ? opts.duration : 4000,
            onClose: typeof opts.onClose === 'function' ? opts.onClose : undefined
        };

        setToasts(prev => [...prev, toast]);

        if (toast.duration && toast.duration > 0) {
            setTimeout(() => {
                setToasts(prev => prev.filter(t => t.id !== id));
                if (toast.onClose) {
                    try { toast.onClose(); } catch (e) { /* ignore */ }
                }
            }, toast.duration);
        }

        return id;
    }, []);

    const removeToast = useCallback((id) => {
        setToasts(prev => prev.filter(t => t.id !== id));
    }, []);

    const showToast = useCallback((opts) => addToast(opts), [addToast]);
    const hideToast = useCallback((id) => removeToast(id), [removeToast]);

    const showConfirm = useCallback(({ title, message, onConfirm, onCancel, confirmText, cancelText }) => {
        setConfirmDialog({
            title,
            message,
            confirmText,
            cancelText,
            onConfirm: () => {
                setConfirmDialog(null);
                if (typeof onConfirm === 'function') onConfirm();
            },
            onCancel: () => {
                setConfirmDialog(null);
                if (typeof onCancel === 'function') onCancel();
            }
        });
    }, []);

    const value = useMemo(() => ({
        toasts,
        showToast,
        hideToast,
        showConfirm,
        confirmDialog,
        removeToast
    }), [toasts, showToast, hideToast, showConfirm, confirmDialog, removeToast]);

    return (
        <ToastContext.Provider value={value}>
            {children}
            {/* render the container once at app root */}
            <ToastContainer toasts={toasts} onRemove={removeToast} confirmDialog={confirmDialog} />
        </ToastContext.Provider>
    );
};

export default ToastContext;