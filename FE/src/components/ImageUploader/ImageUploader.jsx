import React, { useMemo } from "react";
import useImageUploader from "../../hooks/useImageUploader";
import "./ImageUploader.css";

const ImageUploader = ({
  label,
  helperText,
  value = "",
  onChange,
  onError,
  onUploadingChange,
  allowRemove = true,
  height = 180,
}) => {
  const {
    inputRef,
    preview,
    uploading,
    handleFileInput,
    handleDrop,
    handlePaste,
    triggerSelect,
    removeImage,
  } = useImageUploader({
    initialUrl: value,
    onUpload: onChange,
    onRemove: () => onChange?.(""),
    onError,
    onUploadStateChange: onUploadingChange,
  });

  const displayImage = useMemo(() => preview || value, [preview, value]);

  return (
    <div className="iu-field">
      {label && <div className="iu-label">{label}</div>}

      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        style={{ display: "none" }}
        onChange={handleFileInput}
      />

      <div
        className={`iu-dropzone ${displayImage ? "has-image" : ""}`}
        style={{ minHeight: height }}
        onClick={triggerSelect}
        onDrop={handleDrop}
        onDragOver={(e) => {
          e.preventDefault();
          e.stopPropagation();
        }}
        onPaste={handlePaste}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => {
          if (e.key === "Enter" || e.key === " ") triggerSelect();
        }}
      >
        {uploading && (
          <div className="iu-overlay">
            <span>Uploading...</span>
          </div>
        )}
        {displayImage ? (
          <img src={displayImage} alt="Preview" className="iu-preview" />
        ) : (
          <div className="iu-placeholder">
            <div>Kéo thả ảnh vào</div>
            <div>hoặc nhấn để chọn</div>
            <div>hoặc dán URL hình ảnh</div>
          </div>
        )}
      </div>

      <div className="iu-actions">
        <button type="button" className="iu-btn" onClick={triggerSelect}>
          Chọn ảnh
        </button>
        {allowRemove && (displayImage || uploading) && (
          <button
            type="button"
            className="iu-btn secondary"
            onClick={removeImage}
            disabled={uploading}
          >
            Xoá ảnh
          </button>
        )}
      </div>

      {helperText && <div className="iu-helper">{helperText}</div>}
    </div>
  );
};

export default ImageUploader;
