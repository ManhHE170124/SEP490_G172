import React, { useEffect } from "react";
import "./CheckoutConfirmDialog.css";

export default function CheckoutConfirmDialog({
  open,
  email,
  accountName,
  accountEmail,
  qty,
  totalText,
  onCancel,
  onConfirm,
  confirmDisabled = false,
}) {
  useEffect(() => {
    if (!open) return;

    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const onKeyDown = (e) => {
      if (e.key === "Escape") onCancel?.();
    };
    window.addEventListener("keydown", onKeyDown);

    return () => {
      document.body.style.overflow = prev;
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [open, onCancel]);

  if (!open) return null;

  const onBackdrop = (e) => {
    if (e.target === e.currentTarget) onCancel?.();
  };

  return (
    <div className="sfcf-backdrop" onMouseDown={onBackdrop} role="presentation">
      <div className="sfcf-modal" role="dialog" aria-modal="true" aria-label="Xác nhận thanh toán">
        <div className="sfcf-icon" aria-hidden="true">
          !
        </div>

        <div className="sfcf-title">Xác nhận thanh toán</div>
        <div className="sfcf-subtitle">
          Kiểm tra thông tin trước khi chuyển đến cổng thanh toán.
        </div>

        <div className="sfcf-kv">
          <div className="sfcf-row">
            <span>Email nhận hàng</span>
            <b className="sfcf-mono">{email || "—"}</b>
          </div>

          {accountEmail ? (
            <div className="sfcf-row">
              <span>Tài khoản</span>
              <b className="sfcf-ellipsis" title={`${accountName || "—"} (${accountEmail})`}>
                {accountName || "—"} ({accountEmail})
              </b>
            </div>
          ) : null}

          <div className="sfcf-row">
            <span>Số lượng</span>
            <b>{qty ?? "—"}</b>
          </div>

          <div className="sfcf-row">
            <span>Tổng thanh toán</span>
            <b className="sfcf-money">{totalText || "—"}</b>
          </div>

          <div className="sfcf-row">
            <span>Thời hạn thanh toán</span>
            <b>5 phút</b>
          </div>
        </div>

        <div className="sfcf-note">
          <div className="sfcf-note-title">Lưu ý</div>
          <ul className="sfcf-ul">
            <li>Hãy kiểm tra kỹ email nhận hàng. Nhập sai email có thể không nhận được hàng.</li>
            <li>Nếu không thanh toán trong 5 phút, đơn hàng và giao dịch sẽ tự động hủy.</li>
          </ul>
        </div>

        <div className="sfcf-actions">
          <button type="button" className="sfcf-btn sfcf-btn-ghost" onClick={onCancel}>
            Hủy
          </button>
          <button
            type="button"
            className="sfcf-btn sfcf-btn-primary"
            onClick={onConfirm}
            disabled={confirmDisabled}
          >
            Đồng ý
          </button>
        </div>
      </div>
    </div>
  );
}
