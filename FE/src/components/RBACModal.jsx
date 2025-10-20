import React, { useEffect, useState } from "react";
import "./RBACModal.css";
export default function RBACModal({ isOpen, title, fields, onClose, onSubmit, submitting }) {
  const [form, setForm] = useState({});

  useEffect(() => {
    if (isOpen) {
      const init = {};
      (fields || []).forEach(f => { init[f.name] = f.defaultValue ?? ""; });
      setForm(init);
    }
  }, [isOpen, fields]);

  if (!isOpen) return null;

  const handleChange = (name, value) => {
    setForm(prev => ({ ...prev, [name]: value }));
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    onSubmit?.(form);
  };

  return (
    <div className="modal-overlay active">
      <div className="modal">
        <div className="modal-header">
          <h3 className="modal-title">{title}</h3>
          <button className="modal-close" onClick={onClose}>&times;</button>
        </div>
        <form onSubmit={handleSubmit}>
          <div className="modal-body">
            {(fields || []).map((f) => (
              <div className="form-group" key={f.name}>
                <label className="form-label" htmlFor={f.name}>{f.label}{f.required ? " *" : ""}</label>
                {f.type === "textarea" ? (
                  <textarea
                    id={f.name}
                    className="form-input form-textarea"
                    required={!!f.required}
                    value={form[f.name] ?? ""}
                    onChange={(e) => handleChange(f.name, e.target.value)}
                  />
                ) : (
                  <input
                    id={f.name}
                    className="form-input"
                    type={f.type || "text"}
                    required={!!f.required}
                    value={form[f.name] ?? ""}
                    onChange={(e) => handleChange(f.name, e.target.value)}
                  />
                )}
              </div>
            ))}
          </div>
          <div className="modal-footer">
            <button type="button" className="btn-modal btn-modal-secondary" onClick={onClose}>Huỷ</button>
            <button type="submit" disabled={!!submitting} className="btn-modal btn-modal-primary">
              {submitting ? "Đang lưu..." : "Lưu"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}


