import React from "react";

const ModalContext = React.createContext(null);

export function ModalProvider({ children }) {
  const [state, setState] = React.useState({
    open: false,
    type: "info", // 'success', 'error', 'warning', 'info'
    title: "",
    message: "",
    confirmText: "OK",
    onConfirm: null,
  });

  const showModal = React.useCallback((options = {}) => {
    return new Promise((resolve) => {
      setState({
        open: true,
        type: options.type || "info",
        title: options.title || "Thông báo",
        message: options.message || "",
        confirmText: options.confirmText || "OK",
        onConfirm: resolve,
      });
    });
  }, []);

  const showSuccess = React.useCallback(
    (message, title = "Thành công") => {
      return showModal({ type: "success", title, message });
    },
    [showModal]
  );

  const showError = React.useCallback(
    (message, title = "Lỗi") => {
      return showModal({ type: "error", title, message });
    },
    [showModal]
  );

  const showWarning = React.useCallback(
    (message, title = "Cảnh báo") => {
      return showModal({ type: "warning", title, message });
    },
    [showModal]
  );

  const showInfo = React.useCallback(
    (message, title = "Thông báo") => {
      return showModal({ type: "info", title, message });
    },
    [showModal]
  );

  const handleClose = () => {
    setState((prevState) => {
      if (prevState.onConfirm) {
        prevState.onConfirm(true);
      }
      return { ...prevState, open: false, onConfirm: null };
    });
  };

  const getModalIcon = () => {
    switch (state.type) {
      case "success":
        return (
          <div className="modal-icon success">
            <svg width="48" height="48" viewBox="0 0 48 48" fill="none">
              <circle cx="24" cy="24" r="22" fill="#16a34a" opacity="0.1" />
              <path
                d="M16 24l6 6 10-12"
                stroke="#16a34a"
                strokeWidth="3"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </div>
        );
      case "error":
        return (
          <div className="modal-icon error">
            <svg width="48" height="48" viewBox="0 0 48 48" fill="none">
              <circle cx="24" cy="24" r="22" fill="#dc2626" opacity="0.1" />
              <path
                d="M18 18l12 12M30 18l-12 12"
                stroke="#dc2626"
                strokeWidth="3"
                strokeLinecap="round"
              />
            </svg>
          </div>
        );
      case "warning":
        return (
          <div className="modal-icon warning">
            <svg width="48" height="48" viewBox="0 0 48 48" fill="none">
              <circle cx="24" cy="24" r="22" fill="#f59e0b" opacity="0.1" />
              <path
                d="M24 16v12M24 32v2"
                stroke="#f59e0b"
                strokeWidth="3"
                strokeLinecap="round"
              />
            </svg>
          </div>
        );
      default:
        return (
          <div className="modal-icon info">
            <svg width="48" height="48" viewBox="0 0 48 48" fill="none">
              <circle cx="24" cy="24" r="22" fill="#2563eb" opacity="0.1" />
              <path
                d="M24 20v12M24 16v2"
                stroke="#2563eb"
                strokeWidth="3"
                strokeLinecap="round"
              />
            </svg>
          </div>
        );
    }
  };

  return (
    <ModalContext.Provider
      value={{ showModal, showSuccess, showError, showWarning, showInfo }}
    >
      {children}
      {state.open && (
        <div className="modal-backdrop" onClick={handleClose}>
          <div
            className="modal-card modal-alert"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-icon-wrapper">{getModalIcon()}</div>
            <div className="modal-header">
              <h3>{state.title}</h3>
            </div>
            <div className="modal-body">
              <p style={{ textAlign: "center" }}>{state.message}</p>
            </div>
            <div className="modal-footer">
              <button
                className="btn primary"
                onClick={handleClose}
                style={{ width: "100%" }}
              >
                {state.confirmText}
              </button>
            </div>
          </div>
        </div>
      )}
    </ModalContext.Provider>
  );
}

export function useModal() {
  const context = React.useContext(ModalContext);
  if (!context) {
    throw new Error("useModal must be used inside <ModalProvider>");
  }
  return context;
}
