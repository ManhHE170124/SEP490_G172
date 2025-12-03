import { useCallback, useEffect, useRef, useState } from "react";
import { postsApi, extractPublicId } from "../services/postsApi";

const pickImageUrl = (resp) => {
  const payload = resp?.data ?? resp;
  if (typeof payload === "string") return payload;
  if (payload?.path) return payload.path;
  if (payload?.imageUrl) return payload.imageUrl;
  if (payload?.url) return payload.url;
  if (payload && typeof payload === "object") {
    const values = Object.values(payload);
    const stringValue = values.find((val) => typeof val === "string");
    if (stringValue) return stringValue;
  }
  return "";
};

const urlToFile = async (url) => {
  const response = await fetch(url);
  const blob = await response.blob();
  const ext = blob.type.split("/")[1] || "jpg";
  return new File([blob], `image.${ext}`, { type: blob.type });
};

export const useImageUploader = ({
  initialUrl = "",
  onUpload,
  onRemove,
  onError,
  onUploadStateChange,
} = {}) => {
  const [preview, setPreview] = useState(initialUrl || "");
  const [value, setValue] = useState(initialUrl || "");
  const [uploading, setUploading] = useState(false);
  const inputRef = useRef(null);

  useEffect(() => {
    setPreview(initialUrl || "");
    setValue(initialUrl || "");
  }, [initialUrl]);

  const resetInput = () => {
    if (inputRef.current) {
      inputRef.current.value = "";
    }
  };

  const emitUploadState = useCallback(
    (state) => {
      if (typeof onUploadStateChange === "function") {
        onUploadStateChange(state);
      }
    },
    [onUploadStateChange]
  );

  const handleUpload = useCallback(
    async (file) => {
      if (!file) return null;
      setUploading(true);
      emitUploadState(true);

      try {
        const reader = new FileReader();
        reader.onload = (ev) => setPreview(ev.target.result);
        reader.readAsDataURL(file);

        const resp = await postsApi.uploadImage(file);
        const imageUrl = pickImageUrl(resp);

        if (!imageUrl) {
          throw new Error("Khong lay duoc duong dan anh tu server");
        }

        setValue(imageUrl);
        onUpload?.(imageUrl);
        return imageUrl;
      } catch (err) {
        console.error("Upload image failed", err);
        onError?.(
          err?.response?.data?.message ||
            err?.message ||
            "Kh�ng th? t?i ?nh. Vui l�ng th? l?i."
        );
        setPreview(initialUrl || "");
        return null;
      } finally {
        setUploading(false);
        emitUploadState(false);
        resetInput();
      }
    },
    [emitUploadState, initialUrl, onError, onUpload]
  );

  const handleFileInput = useCallback(
    async (event) => {
      const file = event?.target?.files?.[0];
      if (file) {
        await handleUpload(file);
      }
    },
    [handleUpload]
  );

  const handleDrop = useCallback(
    async (event) => {
      event.preventDefault();
      event.stopPropagation();
      const items = Array.from(event.dataTransfer.items || []);

      for (const item of items) {
        if (item.kind === "file" && item.type.startsWith("image/")) {
          const file = item.getAsFile();
          await handleUpload(file);
          return;
        }
        if (item.kind === "string" && item.type === "text/uri-list") {
          item.getAsString(async (url) => {
            try {
              const file = await urlToFile(url);
              await handleUpload(file);
            } catch (err) {
            onError?.("Kh�ng th? tai ?nh t? URL n�y.");
            }
          });
          return;
        }
      }
    },
    [handleUpload, onError]
  );

  const handlePaste = useCallback(
    async (event) => {
      const items = Array.from(event.clipboardData?.items || []);

      for (const item of items) {
        if (item.kind === "file" && item.type.startsWith("image/")) {
          const file = item.getAsFile();
          await handleUpload(file);
          return;
        }

        if (item.kind === "string" && item.type === "text/plain") {
          item.getAsString(async (text) => {
            if (/^https?:\/\/.+\.(jpg|jpeg|png|gif|webp)$/i.test(text)) {
              try {
                const file = await urlToFile(text);
                await handleUpload(file);
              } catch (err) {
                onError?.("Kh�ng th? tai ?nh t? URL n�y.");
              }
            }
          });
          return;
        }
      }
    },
    [handleUpload, onError]
  );

  const handleUrlUpload = useCallback(
    async (url) => {
      if (!url) return;
      try {
        const file = await urlToFile(url);
        await handleUpload(file);
      } catch (err) {
        onError?.("Kh�ng th? tai ?nh t? URL n�y.");
      }
    },
    [handleUpload, onError]
  );

  const triggerSelect = useCallback(() => {
    inputRef.current?.click();
  }, []);

  const removeImage = useCallback(async () => {
    try {
      if (value) {
        const publicId = extractPublicId(value);
        if (publicId) {
          await postsApi.deleteImage(publicId);
        }
      }
    } catch (err) {
      console.error("Failed to delete image", err);
    } finally {
      setValue("");
      setPreview("");
      onUpload?.("");
      onRemove?.();
      resetInput();
    }
  }, [onRemove, onUpload, value]);

  return {
    inputRef,
    preview,
    value,
    uploading,
    handleFileInput,
    handleDrop,
    handlePaste,
    handleUrlUpload,
    triggerSelect,
    removeImage,
  };
};

export default useImageUploader;
