import React from "react";

export default function CsvUploadModal({
  isOpen,
  onClose,
  selectedPackage,
  csvFile,
  uploading,
  onFileChange,
  onUpload,
  expiryDate,
  onExpiryDateChange,
  keyType,
  onKeyTypeChange,
}) {
  if (!isOpen) return null;

  return (
    <div
      style={{
        position: "fixed",
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        background: "rgba(0, 0, 0, 0.5)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 9999,
      }}
      onClick={onClose}
    >
      <div
        style={{
          background: "white",
          borderRadius: "8px",
          padding: "24px",
          maxWidth: "500px",
          width: "90%",
          boxShadow: "0 4px 6px rgba(0, 0, 0, 0.1)",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <h2 style={{ margin: "0 0 16px" }}>Upload File CSV License Keys</h2>

        {selectedPackage && (
          <div
            style={{
              marginBottom: "16px",
              padding: "12px",
              background: "#f8fafc",
              borderRadius: "4px",
            }}
          >
            <p style={{ margin: "0 0 8px", fontSize: "14px" }}>
              <strong>Sản phẩm:</strong> {selectedPackage.productName}
            </p>
            <p style={{ margin: "0 0 8px", fontSize: "14px" }}>
              <strong>Số lượng còn lại:</strong>{" "}
              {selectedPackage.remainingQuantity}
            </p>
            <p style={{ margin: "0", fontSize: "12px", color: "#666" }}>
              File CSV cần có cột "key" chứa license key (mỗi dòng một key)
            </p>
          </div>
        )}

        <div style={{ marginBottom: "16px" }}>
          <label
            style={{
              display: "block",
              marginBottom: "8px",
              fontWeight: "500",
            }}
          >
            Chọn file CSV
          </label>
          <input
            type="file"
            accept=".csv"
            onChange={onFileChange}
            style={{
              display: "block",
              width: "100%",
              padding: "8px",
              border: "1px solid #ddd",
              borderRadius: "4px",
            }}
          />
          {csvFile && (
            <p
              style={{
                margin: "8px 0 0",
                fontSize: "14px",
                color: "#059669",
              }}
            >
              Đã chọn: {csvFile.name}
            </p>
          )}
        </div>

        <div style={{ marginBottom: "16px" }}>
          <label
            style={{
              display: "block",
              marginBottom: "8px",
              fontWeight: "500",
            }}
          >
            Loại key (áp dụng cho tất cả keys)
          </label>
          <select
            value={keyType || "Individual"}
            onChange={(e) => onKeyTypeChange && onKeyTypeChange(e.target.value)}
            style={{
              display: "block",
              width: "100%",
              padding: "8px",
              border: "1px solid #ddd",
              borderRadius: "4px",
            }}
          >
            <option value="Individual">Cá nhân (Individual)</option>
            <option value="Pool">Dùng chung (Pool)</option>
          </select>
        </div>

        <div style={{ marginBottom: "16px" }}>
          <label
            style={{
              display: "block",
              marginBottom: "8px",
              fontWeight: "500",
            }}
          >
            Ngày hết hạn (áp dụng cho tất cả keys)
          </label>
          <input
            type="date"
            value={expiryDate || ""}
            onChange={(e) => onExpiryDateChange && onExpiryDateChange(e.target.value)}
            style={{
              display: "block",
              width: "100%",
              padding: "8px",
              border: "1px solid #ddd",
              borderRadius: "4px",
            }}
          />
          <small style={{ color: "#666", fontSize: "12px", marginTop: "4px", display: "block" }}>
            Để trống nếu không có ngày hết hạn
          </small>
        </div>

        <div
          style={{
            display: "flex",
            gap: "8px",
            justifyContent: "flex-end",
          }}
        >
          <button className="btn" onClick={onClose} disabled={uploading}>
            Hủy
          </button>
          <button
            className="btn primary"
            onClick={onUpload}
            disabled={!csvFile || uploading}
          >
            {uploading ? "Đang upload..." : "Upload"}
          </button>
        </div>
      </div>
    </div>
  );
}
