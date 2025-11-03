import React from "react";
import "./BulkPercentModal.css";

export default function BulkPercentModal({
  open,
  defaultPercent = 5,
  totalPreview,              // số SP đang lọc (tuỳ chọn, để hiển thị)
  busy = false,
  onClose,
  onConfirm,                 // (percent:number) => Promise|void
}) {
  const [val, setVal] = React.useState(defaultPercent);

  React.useEffect(() => {
    if (open) setVal(defaultPercent);
  }, [open, defaultPercent]);

  const setFromInput = (e) => {
    const n = Number(e.target.value);
    setVal(Number.isFinite(n) ? n : 0);
  };

  const quicks = [-20, -10, -5, 5, 10, 20];
  const valid = Number.isFinite(val) && val !== 0 && Math.abs(val) <= 200;

  const onKeyDown = (e) => {
    if (e.key === "Escape") onClose?.();
    if (e.key === "Enter" && valid && !busy) onConfirm?.(val);
  };

  if (!open) return null;

  return (
    <div className="modal-backdrop" onKeyDown={onKeyDown}>
      <div className="modal-sheet" role="dialog" aria-modal="true" aria-label="Cập nhật giá theo %">
        <div className="modal-header">
          <h3>Cập nhật giá theo %</h3>
          <button className="icon-btn" onClick={onClose} aria-label="Đóng">✕</button>
        </div>

        <div className="modal-body">
          <div className="inline-inputs">
            <div className="number-box">
              <button type="button" className="step" onClick={() => setVal((v) => v - 1)}>-</button>
              <input
                type="number"
                inputMode="decimal"
                step={1}
                min={-200}
                max={200}
                value={val}
                onChange={setFromInput}
                aria-label="Phần trăm tăng/giảm"
              />
              <span className="suffix">%</span>
              <button type="button" className="step" onClick={() => setVal((v) => v + 1)}>+</button>
            </div>

            <input
              className="range"
              type="range"
              min={-100}
              max={100}
              step={1}
              value={Math.max(-100, Math.min(100, Math.round(val)))}
              onChange={(e) => setVal(Number(e.target.value))}
              aria-label="Slider phần trăm"
            />
          </div>

          <div className="chips">
            {quicks.map((q) => (
              <button
                key={q}
                type="button"
                className={`chip ${q === val ? "active" : ""}`}
                onClick={() => setVal(q)}
                title={`${q > 0 ? "+" : ""}${q}%`}
              >
                {q > 0 ? `+${q}%` : `${q}%`}
              </button>
            ))}
          </div>

          <div className="preview">
            <span className={`pill ${val > 0 ? "up" : "down"}`}>
              {val > 0 ? `+${val}%` : `${val}%`}
            </span>
            <span className="muted">
              {totalPreview != null
                ? `Sẽ áp dụng cho ${totalPreview} sản phẩm (theo bộ lọc hiện tại).`
                : `Sẽ áp dụng cho các sản phẩm theo bộ lọc hiện tại.`}
            </span>
          </div>

          {!valid && (
            <div className="warn">Giá trị phải khác 0 và trong khoảng ±200%.</div>
          )}
        </div>

        <div className="modal-actions">
          <button className="btn" onClick={onClose} disabled={busy}>Huỷ</button>
          <button className="btn primary" onClick={() => onConfirm?.(val)} disabled={!valid || busy}>
            {busy ? "Đang áp dụng…" : "Áp dụng"}
          </button>
        </div>
      </div>
    </div>
  );
}
