import React from 'react';
import './ConfirmDialog.css';

const ConfirmDialog = ({
  isOpen,
  title,
  message,
  confirmText = 'Xác nhận',
  cancelText = 'Hủy',
  onConfirm,
  onCancel,
  type = 'warning' // 'warning', 'danger', 'info'
}) => {
  if (!isOpen) return null;

  return (
    <div className="confirm-dialog-overlay" onClick={onCancel}>
      <div className="confirm-dialog" onClick={(e) => e.stopPropagation()}>
        <div className={`confirm-dialog-header ${type}`}>
          <h3>{title}</h3>
        </div>
        <div className="confirm-dialog-body">
          <p style={{ whiteSpace: 'pre-wrap' }}>{message}</p>
        </div>
        <div className="confirm-dialog-footer">
          <button
            className="btn"
            onClick={onCancel}
          >
            {cancelText}
          </button>
          <button
            className={`btn ${type === 'danger' ? 'danger' : 'primary'}`}
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
