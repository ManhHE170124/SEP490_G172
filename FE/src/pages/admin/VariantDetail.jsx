import React from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import ProductVariantsApi from "../../services/productVariants";
import ProductSectionsPanel from "../admin/ProductSectionsPanel";
import ToastContainer from "../../components/Toast/ToastContainer";
import "../admin/admin.css";

const TITLE_MAX = 60;
const CODE_MAX = 50;
const ALLOWED_IMAGE_TYPES = [
  "image/jpeg",
  "image/png",
  "image/gif",
  "image/webp",
];
const MAX_IMAGE_SIZE = 2 * 1024 * 1024; // 2MB

const sanitizeThumbnail = (url, max = 255) => {
  if (!url) return null;
  const noQuery = url.split("?")[0];
  return noQuery.length > max ? noQuery.slice(0, max) : noQuery;
};

const parseMoney = (value) => {
  if (value === null || value === undefined) return { num: null, raw: "" };
  // Accept strings formatted like vi-VN (1.234.567 or 1.234,56)
  const s = String(value).trim();
  if (!s) return { num: null, raw: "" };
  // Normalize: remove thousand separators (.) then convert decimal comma -> dot
  const normalized = s.replace(/\./g, "").replace(/,/g, ".");
  const num = Number(normalized);
  if (!Number.isFinite(num)) return { num: null, raw: s };
  return { num, raw: s };
};

const isValidDecimal18_2 = (raw) => {
  if (!raw) return false;
  // Normalize similar to parseMoney: remove thousand separators and unify decimal to dot
  const normalized = String(raw).trim().replace(/\./g, "").replace(/,/g, ".");
  if (!normalized) return false;

  const neg = normalized[0] === "-";
  const unsigned = neg ? normalized.slice(1) : normalized;

  const parts = unsigned.split(".");
  const intPart = parts[0] || "0";
  const fracPart = parts[1] || "";

  if (intPart.replace(/^0+/, "").length > 16) return false;
  if (fracPart.length > 2) return false;

  return true;
};

// Format a number/string for input display: vi-VN thousands (.) and decimal (,)
const formatForInput = (value) => {
  if (value === null || value === undefined || value === "") return "";
  const s = String(value).trim();
  // Try to parse using the same normalization
  const normalized = s.replace(/\./g, "").replace(/,/g, ".");
  const num = Number(normalized);
  if (!Number.isFinite(num)) return s;
  // Use vi-VN formatting: thousand '.' and decimal ','
  return num.toLocaleString("vi-VN", {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  });
};

export default function VariantDetail() {
  const { id: productId, variantId } = useParams();
  const nav = useNavigate();

  const [loading, setLoading] = React.useState(true);
  const [notFound, setNotFound] = React.useState(false);
  const [saving, setSaving] = React.useState(false);
  const [toasts, setToasts] = React.useState([]);
  const [errors, setErrors] = React.useState({});
  const [variant, setVariant] = React.useState(null);

  const initialVariantRef = React.useRef(null);

  const removeToast = React.useCallback(
    (id) => setToasts((ts) => ts.filter((t) => t.id !== id)),
    []
  );

  const addToast = React.useCallback(
    (type, title, message) => {
      const id = `${Date.now()}-${Math.random()}`;
      setToasts((ts) => [...ts, { id, type, title, message }]);
      setTimeout(() => removeToast(id), 5000);
    },
    [removeToast]
  );

  const [confirmDialog, setConfirmDialog] = React.useState(null);

  const openConfirm = React.useCallback(({ title, message, onConfirm }) => {
    setConfirmDialog({
      title,
      message,
      onConfirm: async () => {
        setConfirmDialog(null);
        await onConfirm?.();
      },
      onCancel: () => setConfirmDialog(null),
    });
  }, []);

  const [thumbPreview, setThumbPreview] = React.useState(null);
  const [thumbUrl, setThumbUrl] = React.useState(null);
  const fileInputRef = React.useRef(null);

  const statusClass = (s) =>
    String(s).toUpperCase() === "ACTIVE"
      ? "badge green"
      : String(s).toUpperCase() === "OUT_OF_STOCK"
      ? "badge warning"
      : "badge gray";

  const statusText = (s) =>
    ({
      ACTIVE: "Hiển thị",
      INACTIVE: "Ẩn",
      OUT_OF_STOCK: "Hết hàng",
    }[String(s || "").toUpperCase()] ||
    s ||
    "-");

  const load = React.useCallback(async () => {
    try {
      setLoading(true);
      setNotFound(false);

      const v = await ProductVariantsApi.get(productId, variantId);
      if (!v) {
        setNotFound(true);
        return;
      }

      const serverThumb = v.thumbnail ?? v.Thumbnail ?? null;

      const mapped = {
        variantId: v.variantId ?? v.VariantId ?? variantId,
        productId: v.productId ?? v.ProductId ?? productId,
        variantCode: v.variantCode ?? v.VariantCode ?? "",
        title: v.title ?? v.Title ?? "",
        durationDays: v.durationDays ?? v.DurationDays ?? 0,
        warrantyDays: v.warrantyDays ?? v.WarrantyDays ?? 0,
        stockQty: v.stockQty ?? v.StockQty ?? 0,
        thumbnail: serverThumb,
        status: (v.status ?? v.Status ?? "INACTIVE").toString().toUpperCase(),
        hasSections: v.hasSections ?? v.HasSections ?? false,
        sellPrice: formatForInput(v.sellPrice ?? v.SellPrice ?? 0),
        listPrice: formatForInput(
          v.listPrice ?? v.ListPrice ?? v.cogsPrice ?? v.CogsPrice ?? 0
        ),
        cogsPrice: formatForInput(v.cogsPrice ?? v.CogsPrice ?? 0), // giá vốn – chỉ hiển thị
      };

      setVariant(mapped);

      if (!initialVariantRef.current) {
        initialVariantRef.current = mapped;
      }

      setThumbPreview(serverThumb);
      setThumbUrl(serverThumb);
    } catch (e) {
      setNotFound(true);
    } finally {
      setLoading(false);
    }
  }, [productId, variantId]);

  React.useEffect(() => {
    load();
  }, [load]);

  const setVar = (k, val) => setVariant((s) => (s ? { ...s, [k]: val } : s));

  const isDirty = React.useMemo(() => {
    if (!initialVariantRef.current || !variant) return false;
    return (
      JSON.stringify(initialVariantRef.current) !== JSON.stringify(variant)
    );
  }, [variant]);

  React.useEffect(() => {
    const handler = (e) => {
      if (!isDirty) return;
      e.preventDefault();
      e.returnValue = "";
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [isDirty]);

  const handleBack = () => {
    if (!isDirty) {
      nav(`/admin/products/${productId}`);
      return;
    }

    openConfirm({
      title: "Rời khỏi trang?",
      message:
        "Bạn có các thay đổi chưa lưu. Rời khỏi trang sẽ làm mất các thay đổi này.",
      onConfirm: () => {
        nav(`/admin/products/${productId}`);
      },
    });
  };

  const toggleActive = async () => {
    if (!variant) return;

    if ((variant.stockQty ?? 0) <= 0) {
      addToast(
        "warning",
        "Không thể đổi trạng thái",
        "Gói sản phẩm này đang hết hàng (tồn kho = 0). Vui lòng nhập thêm tồn kho trước khi bật hiển thị."
      );
      return;
    }

    try {
      const payload = await ProductVariantsApi.toggle(
        productId,
        variant.variantId
      );
      const next = (payload?.Status || payload?.status || "").toUpperCase();
      if (next) {
        setVariant((prev) => {
          if (!prev) return prev;
          const updated = { ...prev, status: next };
          if (initialVariantRef.current) {
            initialVariantRef.current = {
              ...initialVariantRef.current,
              status: next,
            };
          } else {
            initialVariantRef.current = updated;
          }
          return updated;
        });

        addToast(
          "success",
          "Cập nhật trạng thái",
          `Trạng thái gói sản phẩm hiện tại: ${statusText(next)}.`
        );
      }
    } catch (e) {
      addToast(
        "error",
        "Đổi trạng thái thất bại",
        e?.response?.data?.message || e.message
      );
    }
  };

  // Upload helpers
  const urlToFile = async (url) => {
    const res = await fetch(url);
    const blob = await res.blob();
    return new File([blob], "image." + (blob.type.split("/")[1] || "png"), {
      type: blob.type,
    });
  };

  const handleLocalPreview = (file) => {
    const reader = new FileReader();
    reader.onload = (ev) => setThumbPreview(ev.target.result);
    reader.readAsDataURL(file);
  };

  const validateImageFile = (file) => {
    if (!file) return false;

    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      addToast(
        "warning",
        "Định dạng ảnh không hỗ trợ",
        "Vui lòng chọn ảnh JPG, PNG, GIF hoặc WEBP."
      );
      return false;
    }

    if (file.size > MAX_IMAGE_SIZE) {
      addToast(
        "warning",
        "Ảnh quá lớn",
        "Dung lượng tối đa cho ảnh thumbnail là 2MB."
      );
      return false;
    }

    return true;
  };

  const uploadThumbnailFile = async (file) => {
    if (!validateImageFile(file)) return;

    handleLocalPreview(file);
    try {
      const up = await ProductVariantsApi.uploadImage(file);
      const imageUrl =
        up?.path ||
        up?.Path ||
        up?.url ||
        up?.Url ||
        (typeof up === "string" ? up : null);
      if (!imageUrl) throw new Error("Không lấy được URL ảnh sau khi upload.");
      setThumbUrl(imageUrl);
      setVar("thumbnail", imageUrl);
      addToast(
        "success",
        "Upload ảnh thành công",
        "Ảnh thumbnail đã được tải lên."
      );
    } catch (err) {
      console.error(err);
      setThumbPreview(null);
      setThumbUrl(null);
      setVar("thumbnail", null);
      addToast(
        "error",
        "Upload ảnh thất bại",
        err?.response?.data?.message || err.message
      );
    }
  };

  const onPickThumb = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      await uploadThumbnailFile(file);
    } catch (err) {
      addToast(
        "error",
        "Upload ảnh thất bại",
        err?.response?.data?.message || err.message
      );
      setThumbPreview(null);
      setThumbUrl(null);
      setVar("thumbnail", null);
    } finally {
      e.target.value = "";
    }
  };

  const onDrop = async (e) => {
    e.preventDefault();
    e.stopPropagation();
    const dt = e.dataTransfer;
    if (!dt) return;

    if (dt.files && dt.files[0]) {
      try {
        await uploadThumbnailFile(dt.files[0]);
      } catch (err) {
        addToast(
          "error",
          "Upload ảnh thất bại",
          err?.response?.data?.message || err.message
        );
      }
      return;
    }
    const text = dt.getData("text/uri-list") || dt.getData("text/plain");
    if (text && /^https?:\/\//i.test(text)) {
      try {
        const f = await urlToFile(text);
        await uploadThumbnailFile(f);
      } catch (err) {
        addToast(
          "error",
          "Không thể tải ảnh từ URL này.",
          err?.response?.data?.message || err.message
        );
      }
    }
  };

  const onPaste = async (e) => {
    const items = Array.from(e.clipboardData?.items || []);
    for (const it of items) {
      if (it.kind === "file" && it.type.startsWith("image/")) {
        const f = it.getAsFile();
        if (f) {
          try {
            await uploadThumbnailFile(f);
          } catch (err) {
            addToast(
              "error",
              "Upload ảnh thất bại",
              err?.response?.data?.message || err.message
            );
          }
          break;
        }
      } else if (it.kind === "string" && it.type === "text/plain") {
        it.getAsString(async (text) => {
          if (/^https?:\/\/.+\.(jpg|jpeg|png|gif|webp)$/i.test(text)) {
            try {
              const f = await urlToFile(text);
              await uploadThumbnailFile(f);
            } catch (err) {
              addToast(
                "error",
                "Không thể tải ảnh từ URL này.",
                err?.response?.data?.message || err.message
              );
            }
          }
        });
      }
    }
  };

  const clearThumb = () => {
    setThumbPreview(null);
    setThumbUrl(null);
    setVar("thumbnail", null);
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  const save = async () => {
    if (!variant) return;

    const nextErrors = {};

    const title = (variant.title || "").trim();
    const variantCode = (variant.variantCode || "").trim();

    if (!title) {
      nextErrors.title = "Tên gói sản phẩm là bắt buộc.";
    } else if (title.length > TITLE_MAX) {
      nextErrors.title = `Tên gói sản phẩm không được vượt quá ${TITLE_MAX} ký tự.`;
    }

    if (!variantCode) {
      nextErrors.variantCode = "Mã gói sản phẩm là bắt buộc.";
    } else if (variantCode.length > CODE_MAX) {
      nextErrors.variantCode = `Mã gói sản phẩm không được vượt quá ${CODE_MAX} ký tự.`;
    }

    const parseIntOrNull = (v) => {
      if (v === "" || v == null) return null;
      const n = parseInt(v, 10);
      return Number.isNaN(n) ? null : n;
    };

    const durationDays = parseIntOrNull(variant.durationDays);
    const warrantyDays = parseIntOrNull(variant.warrantyDays);

    if (durationDays == null) {
      nextErrors.durationDays = "Thời lượng (ngày) là bắt buộc.";
    } else if (durationDays < 0) {
      nextErrors.durationDays = "Thời lượng (ngày) phải lớn hơn hoặc bằng 0.";
    }

    if (warrantyDays != null && warrantyDays < 0) {
      nextErrors.warrantyDays = "Bảo hành (ngày) phải lớn hơn hoặc bằng 0.";
    }

    if (
      durationDays != null &&
      warrantyDays != null &&
      durationDays <= warrantyDays
    ) {
      nextErrors.durationDays =
        "Thời lượng (ngày) phải lớn hơn số ngày bảo hành.";
    }

    const { num: listNum, raw: listRaw } = parseMoney(variant.listPrice);
    const { num: sellNum, raw: sellRaw } = parseMoney(variant.sellPrice);
    const cogsNum = Number(variant.cogsPrice ?? 0);

    if (listRaw === "" || listNum === null) {
      nextErrors.listPrice = "Giá niêm yết là bắt buộc.";
    } else if (listNum < 0) {
      nextErrors.listPrice = "Giá niêm yết phải lớn hơn hoặc bằng 0.";
    } else if (!isValidDecimal18_2(listRaw)) {
      nextErrors.listPrice =
        "Giá niêm yết không được vượt quá decimal(18,2) (tối đa 16 chữ số phần nguyên và 2 chữ số thập phân).";
    }

    if (sellRaw === "" || sellNum === null) {
      nextErrors.sellPrice = "Giá bán là bắt buộc.";
    } else if (sellNum < 0) {
      nextErrors.sellPrice = "Giá bán phải lớn hơn hoặc bằng 0.";
    } else if (!isValidDecimal18_2(sellRaw)) {
      nextErrors.sellPrice =
        "Giá bán không được vượt quá decimal(18,2) (tối đa 16 chữ số phần nguyên và 2 chữ số thập phân).";
    }

    if (
      !nextErrors.listPrice &&
      !nextErrors.sellPrice &&
      listNum != null &&
      sellNum != null &&
      sellNum > listNum
    ) {
      nextErrors.sellPrice = "Giá bán không được lớn hơn giá niêm yết.";
    }

    if (
      !nextErrors.listPrice &&
      listNum != null &&
      cogsNum > 0 &&
      listNum < cogsNum
    ) {
      nextErrors.listPrice = "Giá niêm yết không được nhỏ hơn giá vốn.";
    }

    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      addToast(
        "warning",
        "Dữ liệu chưa hợp lệ",
        "Vui lòng kiểm tra các trường được đánh dấu."
      );
      return;
    }

    setErrors({});

    try {
      setSaving(true);

      const dto = {
        title,
        variantCode,
        durationDays,
        stockQty: Number(variant.stockQty ?? 0),
        warrantyDays,
        thumbnail: sanitizeThumbnail(thumbUrl) || null,
        status: variant.status,
        sellPrice: Number(sellNum.toFixed(2)),
        listPrice: Number(listNum.toFixed(2)),
        // cogsPrice không gửi – BE giữ giá vốn từ module nhập key/account
      };

      await ProductVariantsApi.update(productId, variant.variantId, dto);

      addToast(
        "success",
        "Cập nhật gói sản phẩm",
        "Gói sản phẩm đã được lưu thành công."
      );

      setTimeout(() => {
        nav(`/admin/products/${productId}`);
      }, 400);
    } catch (e) {
      const status = e?.response?.status;
      const data = e?.response?.data || {};
      const code = data.code;
      const msg = data.message || e.message;

      if (status === 409 && code === "VARIANT_TITLE_DUPLICATE") {
        setErrors((prev) => ({
          ...prev,
          title: msg || "Tên gói sản phẩm đã tồn tại trong sản phẩm này.",
        }));
        addToast(
          "warning",
          "Tên gói sản phẩm trùng",
          msg || "Tên gói sản phẩm đã tồn tại trong sản phẩm này."
        );
      } else if (status === 409 && code === "VARIANT_CODE_DUPLICATE") {
        setErrors((prev) => ({
          ...prev,
          variantCode: msg || "Mã gói sản phẩm đã tồn tại trong sản phẩm này.",
        }));
        addToast(
          "warning",
          "Mã gói sản phẩm trùng",
          msg || "Mã gói sản phẩm đã tồn tại trong sản phẩm này."
        );
      } else if (status === 409 && code === "VARIANT_IN_USE_SECTION") {
        addToast(
          "warning",
          "Không thể chỉnh sửa gói sản phẩm",
          msg ||
            "Gói sản phẩm này đang được sử dụng trong các section, không thể chỉnh sửa mã gói sản phẩm."
        );
      } else {
        addToast("error", "Lưu gói sản phẩm thất bại", msg);
      }
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <>
        <div className="page">
          <div className="card">Đang tải chi tiết gói sản phẩm…</div>
        </div>
        <ToastContainer
          toasts={toasts}
          onRemove={removeToast}
          confirmDialog={confirmDialog}
        />
      </>
    );
  }

  if (notFound || !variant) {
    return (
      <>
        <div className="page">
          <div className="card">
            <h2>Không tìm thấy gói sản phẩm</h2>
            <div className="row" style={{ marginTop: 10 }}>
              <Link className="btn" to={`/admin/products/${productId}`}>
                ← Quay lại sản phẩm
              </Link>
            </div>
          </div>
        </div>
        <ToastContainer
          toasts={toasts}
          onRemove={removeToast}
          confirmDialog={confirmDialog}
        />
      </>
    );
  }

  return (
    <>
      <div className="page">
        <div className="card">
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: 10,
            }}
          >
            <h2>Chi tiết gói sản phẩm</h2>

            <div className="row" style={{ gap: 10, alignItems: "center" }}>
              <span className="badge gray">
                Tồn kho: {variant.stockQty ?? 0}
              </span>

              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <label className="switch" title="Bật/Tắt hiển thị gói sản phẩm">
                  <input
                    type="checkbox"
                    checked={variant.status === "ACTIVE"}
                    onChange={toggleActive}
                    aria-label="Bật/Tắt hiển thị gói sản phẩm"
                  />
                  <span className="slider" />
                </label>

                <span
                  className={statusClass(variant.status)}
                  style={{ textTransform: "none" }}
                >
                  {statusText(variant.status)}
                </span>
              </div>

              <button className="btn ghost" onClick={handleBack}>
                ⬅ Quay lại SP
              </button>
            </div>
          </div>

          <div
            className="grid cols-3 input-group"
            style={{ gridColumn: "1 / 3" }}
          >
            <div className="group">
              <span>
                Tên gói sản phẩm<span style={{ color: "#dc2626" }}>*</span>
              </span>
              <input
                value={variant.title || ""}
                onChange={(e) => setVar("title", e.target.value)}
                placeholder="VD: Gói 12 tháng"
                maxLength={TITLE_MAX}
                className={errors.title ? "input-error" : ""}
              />
              {errors.title && (
                <div className="field-error">{errors.title}</div>
              )}
            </div>
            <div className="group">
              <span>
                Mã gói sản phẩm<span style={{ color: "#dc2626" }}>*</span>
              </span>
              <input
                value={variant.variantCode || ""}
                onChange={(e) => setVar("variantCode", e.target.value)}
                placeholder="VD: VAR_12M"
                maxLength={CODE_MAX}
                disabled={variant.hasSections}
                className={errors.variantCode ? "input-error" : ""}
              />
              {variant.hasSections && (
                <div className="field-error">
                  Gói sản phẩm đang được sử dụng trong các section, không thể chỉnh
                  sửa mã.
                </div>
              )}
              {errors.variantCode && (
                <div className="field-error">{errors.variantCode}</div>
              )}
            </div>
            <div className="group">
              <span>
                Thời lượng (ngày)
                <span style={{ color: "#dc2626" }}>*</span>
              </span>
              <input
                type="number"
                min={0}
                step={1}
                value={variant.durationDays ?? ""}
                onChange={(e) => setVar("durationDays", e.target.value)}
                className={errors.durationDays ? "input-error" : ""}
              />
              {errors.durationDays && (
                <div className="field-error">{errors.durationDays}</div>
              )}
            </div>
          </div>

          <div
            className="grid cols-3 input-group"
            style={{ gridColumn: "1 / 3" }}
          >
            <div className="group">
              <span>Bảo hành (ngày)</span>
              <input
                type="number"
                min={0}
                step={1}
                value={variant.warrantyDays ?? ""}
                onChange={(e) => setVar("warrantyDays", e.target.value)}
                className={errors.warrantyDays ? "input-error" : ""}
              />
              {errors.warrantyDays && (
                <div className="field-error">{errors.warrantyDays}</div>
              )}
            </div>
            <div className="group">
              <span>
                Giá niêm yết (đ)<span style={{ color: "#dc2626" }}>*</span>
              </span>
              <input
                type="text"
                value={formatForInput(variant.listPrice ?? "")}
                onChange={(e) => {
                  const cleaned = (e.target.value || "").replace(/[^0-9.,]/g, "");
                  setVar("listPrice", formatForInput(cleaned));
                }}
                className={errors.listPrice ? "input-error" : ""}
              />
              {errors.listPrice && (
                <div className="field-error">{errors.listPrice}</div>
              )}
            </div>
            <div className="group">
              <span>
                Giá bán (đ)<span style={{ color: "#dc2626" }}>*</span>
              </span>
              <input
                type="text"
                value={formatForInput(variant.sellPrice ?? "")}
                onChange={(e) => {
                  const cleaned = (e.target.value || "").replace(/[^0-9.,]/g, "");
                  setVar("sellPrice", formatForInput(cleaned));
                }}
                className={errors.sellPrice ? "input-error" : ""}
              />
              {errors.sellPrice && (
                <div className="field-error">{errors.sellPrice}</div>
              )}
            </div>
          </div>

          {/* Giá vốn chỉ hiển thị, không chỉnh sửa */}
          <div
            className="grid cols-3 input-group"
            style={{ gridColumn: "1 / 3" }}
          >
            <div className="group">
              <span>Giá vốn (chỉ hiển thị) (đ)</span>
              <input
                type="text"
                value={formatForInput(variant.cogsPrice ?? "")}
                disabled
              />
            </div>
          </div>

          <div className="input-group" style={{ gridColumn: "1 / 3" }}>
            <div className="group">
              <span>Ảnh gói sản phẩm (thumbnail)</span>

              <input
                ref={fileInputRef}
                type="file"
                accept="image/*"
                style={{ display: "none" }}
                onChange={onPickThumb}
              />

              <div
                className={`cep-featured-image-upload ${
                  thumbPreview ? "has-image" : ""
                }`}
                onClick={() => fileInputRef.current?.click()}
                onDrop={onDrop}
                onDragOver={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                }}
                onPaste={onPaste}
                tabIndex={0}
                role="button"
                style={{
                  outline: "none",
                  border: "1px dashed var(--line)",
                  borderRadius: 10,
                  padding: 12,
                  textAlign: "center",
                  background: "#fafafa",
                }}
              >
                {thumbPreview ? (
                  <img
                    src={thumbPreview}
                    alt="thumbnail"
                    style={{
                      width: "100%",
                      maxHeight: 220,
                      objectFit: "contain",
                      borderRadius: 8,
                    }}
                  />
                ) : (
                  <div>
                    <div>Kéo thả ảnh vào đây</div>
                    <div>hoặc</div>
                    <div>Click để chọn ảnh</div>
                    <div>hoặc</div>
                    <div>Dán URL ảnh (Ctrl+V)</div>
                  </div>
                )}
              </div>

              {thumbPreview && (
                <button
                  type="button"
                  className="btn"
                  style={{ marginTop: 8 }}
                  onClick={clearThumb}
                >
                  Xoá ảnh
                </button>
              )}
            </div>
          </div>

          <div className="group" style={{ gridColumn: "1 / 3", marginTop: 12 }}>
            <ProductSectionsPanel
              productId={productId}
              variantId={variant.variantId}
            />
          </div>

          <div className="row" style={{ marginTop: 12 }}>
            <button className="btn primary" disabled={saving} onClick={save}>
              {saving ? "Đang lưu…" : "Lưu thay đổi"}
            </button>
          </div>
        </div>
      </div>

      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </>
  );
}
