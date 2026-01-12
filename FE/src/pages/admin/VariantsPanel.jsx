// src/pages/admin/VariantsPanel.jsx
import React from "react";
import { useNavigate } from "react-router-dom";
import ProductVariantsApi from "../../services/productVariants";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./admin.css";

const TITLE_MAX = 60;
const CODE_MAX = 50;
const ALLOWED_IMAGE_TYPES = ["image/jpeg", "image/png", "image/gif", "image/webp"];
const MAX_IMAGE_SIZE = 2 * 1024 * 1024; // 2MB

// Helper: parse số tiền (supports vi-VN formatted numbers like 1.234.567 or 1.234,56)
const parseMoney = (value) => {
  if (value === null || value === undefined) return { num: null, raw: "" };
  const s = String(value).trim();
  if (!s) return { num: null, raw: "" };
  const normalized = s.replace(/\./g, "").replace(/,/g, ".");
  const num = Number(normalized);
  if (!Number.isFinite(num)) return { num: null, raw: s };
  return { num, raw: s };
};

// Helper: validate decimal(18,2)
const isValidDecimal18_2 = (raw) => {
  if (!raw) return false;
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

const formatMoney = (val) => {
  const num = Number(val);
  if (!Number.isFinite(num)) return "0 đ";
  return (
    num.toLocaleString("vi-VN", {
      minimumFractionDigits: 0,
      maximumFractionDigits: 2,
    }) + " đ"
  );
};

// Format for input (vi-VN style, thousands '.' and decimal ',')
const formatForInput = (value) => {
  if (value === null || value === undefined || value === "") return "";
  const s = String(value).trim();
  const normalized = s.replace(/\./g, "").replace(/,/g, ".");
  const num = Number(normalized);
  if (!Number.isFinite(num)) return s;
  return num.toLocaleString("vi-VN", { minimumFractionDigits: 0, maximumFractionDigits: 2 });
};

export default function VariantsPanel({
  productId,
  productName,
  productCode,
  onTotalChange,
}) {
  const nav = useNavigate();
  const detailPath = (v) =>
    `/admin/products/${productId}/variants/${v.variantId}`;
  const goDetail = (v) => nav(detailPath(v));

  const [items, setItems] = React.useState([]);
  const [total, setTotal] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(1);
  const [loading, setLoading] = React.useState(false);

  // Query states
  const [q, setQ] = React.useState("");
  const [status, setStatus] = React.useState("");
  const [dur, setDur] = React.useState("");
  const [priceMin, setPriceMin] = React.useState("");
  const [priceMax, setPriceMax] = React.useState("");
  const [sort, setSort] = React.useState("created");
  const [dir, setDir] = React.useState("desc");
  const [page, setPage] = React.useState(1);
  const [size, setSize] = React.useState(10);

  const [showModal, setShowModal] = React.useState(false);
  const [editing] = React.useState(null); // hiện tại modal chỉ dùng để tạo mới
  const [modalErrors, setModalErrors] = React.useState({});
  const fileInputRef = React.useRef(null);

  const [thumbPreview, setThumbPreview] = React.useState(null);
  const [thumbUrl, setThumbUrl] = React.useState(null);
  const [modalStatus, setModalStatus] = React.useState("ACTIVE");

  const [toasts, setToasts] = React.useState([]);
  const [confirmDialog, setConfirmDialog] = React.useState(null);

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

  const askConfirm = React.useCallback((title, message) => {
    return new Promise((resolve) => {
      setConfirmDialog({
        title,
        message,
        onConfirm: () => {
          setConfirmDialog(null);
          resolve(true);
        },
        onCancel: () => {
          setConfirmDialog(null);
          resolve(false);
        },
      });
    });
  }, []);

  // Mapping status theo quy ước mới
  const statusBadgeClass = (s, stockQty) => {
    const upper = String(s || "").toUpperCase();
    if (upper === "INACTIVE") return "badge gray";
    if (upper === "OUT_OF_STOCK" || (stockQty ?? 0) <= 0) return "badge warning";
    if (upper === "ACTIVE") return "badge green";
    return "badge gray";
  };

  const statusLabel = (s, stockQty) => {
    const upper = String(s || "").toUpperCase();
    if (upper === "INACTIVE") return "Ẩn";
    if (upper === "OUT_OF_STOCK" || (stockQty ?? 0) <= 0) return "Hết hàng";
    if (upper === "ACTIVE") return "Hiển thị";
    return "Ẩn";
  };

  const sanitizeThumbnail = (url, max = 255) => {
    if (!url) return null;
    const noQuery = url.split("?")[0];
    return noQuery.length > max ? noQuery.slice(0, max) : noQuery;
  };

  const load = React.useCallback(
    async () => {
      setLoading(true);
      try {
        const res = await ProductVariantsApi.list(productId, {
          q,
          status,
          dur,
          sort,
          dir,
          page,
          pageSize: size,
          minPrice: priceMin || undefined,
          maxPrice: priceMax || undefined,
        });

        const list = res.items || [];
        setItems(list);

        const totalItems =
          typeof res.totalItems === "number"
            ? res.totalItems
            : Array.isArray(list)
            ? list.length
            : 0;

        setTotal(totalItems);

        const pageSizeFromRes =
          typeof res.pageSize === "number" && res.pageSize > 0
            ? res.pageSize
            : size;

        const totalPagesCalc = Math.max(
          1,
          Math.ceil(totalItems / pageSizeFromRes || 1)
        );
        setTotalPages(totalPagesCalc);

        if (typeof onTotalChange === "function") {
          // ưu tiên lấy tổng tồn kho & tổng biến thể từ response nếu BE có trả,
          // fallback tính từ list trang hiện tại
          const totalStockFromRes =
            typeof res.totalStock === "number"
              ? res.totalStock
              : typeof res.TotalStock === "number"
              ? res.TotalStock
              : Array.isArray(list)
              ? list.reduce(
                  (acc, v) => acc + (Number(v.stockQty) || 0),
                  0
                )
              : 0;

          const variantCountFromRes =
            typeof res.totalItems === "number"
              ? res.totalItems
              : Array.isArray(list)
              ? list.length
              : 0;

          onTotalChange({
            totalStock: totalStockFromRes,
            variantCount: variantCountFromRes,
          });
        }
      } catch (e) {
        console.error(e);
        addToast(
          "error",
          "Lỗi tải biến thể",
          e?.response?.data?.message || e.message
        );
      } finally {
        setLoading(false);
      }
    },
    [
      productId,
      q,
      status,
      dur,
      sort,
      dir,
      page,
      size,
      priceMin,
      priceMax,
      onTotalChange,
      addToast,
    ]
  );

  React.useEffect(() => {
    load();
  }, [load]);

  React.useEffect(() => {
    setPage(1);
  }, [q, status, dur, sort, dir, priceMin, priceMax]);

  const resetFilters = () => {
    setQ("");
    setStatus("");
    setDur("");
    setPriceMin("");
    setPriceMax("");
    setSort("created");
    setDir("desc");
    setPage(1);
    setSize(10);
  };

  const openCreate = () => {
    // hiện chỉ dùng để tạo mới – editing = null
    setThumbPreview(null);
    setThumbUrl(null);
    setModalErrors({});
    setModalStatus("ACTIVE");
    setShowModal(true);
  };

  // Upload helpers
  const urlToFile = async (url) => {
    const res = await fetch(url);
    const blob = await res.blob();
    return new File(
      [blob],
      "image." + (blob.type.split("/")[1] || "png"),
      { type: blob.type }
    );
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
      addToast(
        "success",
        "Upload ảnh thành công",
        "Ảnh thumbnail đã được tải lên."
      );
    } catch (err) {
      console.error(err);
      setThumbPreview(null);
      setThumbUrl(null);
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
    await uploadThumbnailFile(file);
    e.target.value = "";
  };

  const onDrop = async (e) => {
    e.preventDefault();
    e.stopPropagation();
    const dt = e.dataTransfer;
    if (!dt) return;

    if (dt.files && dt.files[0]) {
      await uploadThumbnailFile(dt.files[0]);
      return;
    }

    const text = dt.getData("text/uri-list") || dt.getData("text/plain");
    if (text && /^https?:\/\//i.test(text)) {
      try {
        const f = await urlToFile(text);
        await uploadThumbnailFile(f);
      } catch {
        addToast("error", "Không thể tải ảnh từ URL này", "");
      }
    }
  };

  const onPaste = async (e) => {
    const itemsClip = Array.from(e.clipboardData?.items || []);
    for (const it of itemsClip) {
      if (it.kind === "file" && it.type.startsWith("image/")) {
        const f = it.getAsFile();
        if (f) {
          await uploadThumbnailFile(f);
          break;
        }
      } else if (it.kind === "string" && it.type === "text/plain") {
        it.getAsString(async (text) => {
          if (/^https?:\/\/.+\.(jpg|jpeg|png|gif|webp)$/i.test(text)) {
            try {
              const f = await urlToFile(text);
              await uploadThumbnailFile(f);
            } catch {
              addToast("error", "Không thể tải ảnh từ URL này", "");
            }
          }
        });
      }
    }
  };

  const clearThumb = () => {
    setThumbPreview(null);
    setThumbUrl(null);
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  const parseIntOrNull = (v) => {
    if (v === "" || v == null) return null;
    const n = parseInt(v, 10);
    return Number.isNaN(n) ? null : n;
  };

  // ===== Validate & Submit modal (CREATE) =====
  const onSubmit = async (e) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);

    const title = (fd.get("title") || "").trim();
    const variantCode = (fd.get("variantCode") || "").trim();
    const durationRaw = fd.get("durationDays");
    const warrantyRaw = fd.get("warrantyDays");
    const listRaw = (fd.get("listPrice") || "").toString().trim();
    const sellRaw = (fd.get("sellPrice") || "").toString().trim();

    const durationDays = parseIntOrNull(durationRaw);
    const warrantyDays = parseIntOrNull(warrantyRaw);

    const { num: listNum } = parseMoney(listRaw);
    const { num: sellNum } = parseMoney(sellRaw);

    const errors = {};

    // Tên biến thể
    if (!title) {
      errors.title = "Tên biến thể là bắt buộc.";
    } else if (title.length > TITLE_MAX) {
      errors.title = `Tên biến thể không được vượt quá ${TITLE_MAX} ký tự.`;
    }

    // Mã biến thể
    if (!variantCode) {
      errors.variantCode = "Mã biến thể là bắt buộc.";
    } else if (variantCode.length > CODE_MAX) {
      errors.variantCode = `Mã biến thể không được vượt quá ${CODE_MAX} ký tự.`;
    }

    // Không trùng trong cùng sản phẩm (so với list hiện tại)
    const lowerTitle = title.toLowerCase();
    const lowerCode = variantCode.toLowerCase();

    items.forEach((v) => {
      if ((v.title || "").trim().toLowerCase() === lowerTitle) {
        errors.title = "Tên biến thể đã tồn tại trong sản phẩm này.";
      }
      if ((v.variantCode || "").trim().toLowerCase() === lowerCode) {
        errors.variantCode = "Mã biến thể đã tồn tại trong sản phẩm này.";
      }
    });

    // Thời lượng / Bảo hành
    if (durationDays == null) {
      errors.durationDays = "Thời lượng (ngày) là bắt buộc.";
    } else if (durationDays < 0) {
      errors.durationDays = "Thời lượng (ngày) phải lớn hơn hoặc bằng 0.";
    }

    if (warrantyDays != null && warrantyDays < 0) {
      errors.warrantyDays = "Bảo hành (ngày) phải lớn hơn hoặc bằng 0.";
    }

    if (
      durationDays != null &&
      warrantyDays != null &&
      durationDays <= warrantyDays
    ) {
      errors.durationDays =
        "Thời lượng (ngày) phải lớn hơn số ngày bảo hành.";
    }

    // ===== Validate giá niêm yết / giá bán (khớp với decimal(18,2) + rule Sell <= List) =====
    if (!listRaw || listNum === null) {
      errors.listPrice = "Giá niêm yết là bắt buộc.";
    } else if (listNum < 0) {
      errors.listPrice = "Giá niêm yết phải lớn hơn hoặc bằng 0.";
    } else if (!isValidDecimal18_2(listRaw)) {
      errors.listPrice =
        "Giá niêm yết không được vượt quá decimal(18,2) (tối đa 16 chữ số phần nguyên và 2 chữ số thập phân).";
    }

    if (!sellRaw || sellNum === null) {
      errors.sellPrice = "Giá bán là bắt buộc.";
    } else if (sellNum < 0) {
      errors.sellPrice = "Giá bán phải lớn hơn hoặc bằng 0.";
    } else if (!isValidDecimal18_2(sellRaw)) {
      errors.sellPrice =
        "Giá bán không được vượt quá decimal(18,2) (tối đa 16 chữ số phần nguyên và 2 chữ số thập phân).";
    }

    if (
      !errors.listPrice &&
      !errors.sellPrice &&
      listNum != null &&
      sellNum != null &&
      sellNum > listNum
    ) {
      errors.sellPrice = "Giá bán không được lớn hơn giá niêm yết.";
    }

    if (Object.keys(errors).length > 0) {
      setModalErrors(errors);
      addToast(
        "warning",
        "Dữ liệu chưa hợp lệ",
        "Vui lòng kiểm tra các trường được đánh dấu."
      );
      return;
    }

    setModalErrors({});

    try {
      const dto = {
        variantCode,
        title,
        durationDays,
        warrantyDays,
        status: modalStatus, // controller sẽ resolve theo stockQty
        thumbnail: sanitizeThumbnail(thumbUrl) || null,
        stockQty: 0, // tạo mới luôn 0, controller sẽ set OUT_OF_STOCK hoặc INACTIVE
        listPrice: Number(listNum.toFixed(2)),
        sellPrice: Number(sellNum.toFixed(2)),
      };

      await ProductVariantsApi.create(productId, dto);
      addToast(
        "success",
        "Thêm biến thể",
        "Biến thể mới đã được tạo thành công."
      );

      setShowModal(false);
      setPage(1);
      await load();
    } catch (err) {
      console.error(err);
      addToast(
        "error",
        "Lưu biến thể thất bại",
        err?.response?.data?.message || err.message
      );
    }
  };

  const onDelete = async (id) => {
    const ok = await askConfirm(
      "Xoá biến thể",
      "Bạn có chắc chắn muốn xoá biến thể này?"
    );
    if (!ok) return;

    try {
      await ProductVariantsApi.remove(productId, id);
      addToast("success", "Đã xoá biến thể", "Biến thể đã được xoá.");
      await load();
    } catch (err) {
      console.error(err);

      const status = err?.response?.status;
      const data = err?.response?.data || {};
      const code = data.code;
      const msg = data.message || err.message;

      if (status === 409) {
        let detail = msg;
        if (!detail) {
          if (code === "VARIANT_IN_USE_SECTION") {
            detail =
              "Biến thể này đang được sử dụng trong các section, vui lòng chỉnh sửa hoặc xoá section trước.";
          } else {
            detail =
              "Biến thể này đang được sử dụng bởi dữ liệu khác nên không thể xoá.";
          }
        }

        addToast("warning", "Không thể xoá biến thể", detail);
      } else {
        addToast(
          "error",
          "Xoá biến thể thất bại",
          msg || "Đã xảy ra lỗi khi xoá biến thể."
        );
      }
    }
  };

  const headerSort = (key) => {
    setSort((cur) => {
      if (cur === key) {
        setDir((d) => (d === "asc" ? "desc" : "asc"));
        return cur;
      }
      setDir("asc");
      return key;
    });
  };

  const sortMark = (key) =>
    sort === key ? (dir === "asc" ? " ▲" : " ▼") : "";

  // Toggle theo controller mới: stock = 0 => OUT_OF_STOCK (vẫn hiển thị, không ẩn)
  const toggleVariantStatus = async (v) => {
    try {
      const payload = await ProductVariantsApi.toggle(productId, v.variantId);
      const nextRaw = payload?.Status ?? payload?.status;
      const next = (nextRaw || "").toUpperCase();

      setItems((prev) =>
        prev.map((x) =>
          x.variantId === v.variantId ? { ...x, status: next || x.status } : x
        )
      );

      if (!next) {
        addToast(
          "success",
          "Cập nhật trạng thái",
          "Đã cập nhật trạng thái biến thể."
        );
        return;
      }

      if (next === "OUT_OF_STOCK" || (v.stockQty ?? 0) <= 0) {
        addToast(
          "info",
          "Biến thể hết hàng",
          "Biến thể hiện đang ở trạng thái 'Hết hàng' (khách vẫn có thể xem nhưng không thể mua cho đến khi nhập thêm tồn kho)."
        );
      } else if (next === "INACTIVE") {
        addToast(
          "info",
          "Biến thể đã ẩn",
          "Biến thể đã được ẩn khỏi trang bán."
        );
      } else {
        addToast(
          "success",
          "Cập nhật trạng thái",
          `Biến thể hiện đang ở trạng thái "${statusLabel(
            next,
            v.stockQty
          )}".`
        );
      }
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        "Đổi trạng thái thất bại",
        e?.response?.data?.message || e.message
      );
      await load();
    }
  };

  const goto = (p) => setPage(Math.min(Math.max(1, p), totalPages));

  const makePageList = React.useMemo(() => {
    const pages = [];
    const win = 2;
    const from = Math.max(1, page - win);
    const to = Math.min(totalPages, page + win);
    if (from > 1) {
      pages.push(1);
      if (from > 2) pages.push("…l");
    }
    for (let i = from; i <= to; i++) pages.push(i);
    if (to < totalPages) {
      if (to < totalPages - 1) pages.push("…r");
      pages.push(totalPages);
    }
    return pages;
  }, [page, totalPages]);

  const startIdx = total === 0 ? 0 : (page - 1) * size + 1;
  const endIdx = Math.min(total, page * size);

  return (
    <div className="group" style={{ gridColumn: "1 / 3" }}>
      <div className="panel">
        <div className="panel-header" style={{ alignItems: "center" }}>
          <h4>
            Biến thể thời gian{" "}
            <span
              style={{
                fontSize: 12,
                color: "var(--muted)",
                marginLeft: 8,
              }}
            >
              ({total})
            </span>
          </h4>

          <div className="variants-toolbar">
            <input
              className="ctl"
              placeholder="Tìm theo tiêu đề / mã…"
              value={q}
              onChange={(e) => setQ(e.target.value)}
            />
            <select
              className="ctl"
              value={status}
              onChange={(e) => setStatus(e.target.value)}
            >
              <option value="">Tất cả trạng thái</option>
              <option value="ACTIVE">Hiển thị</option>
              <option value="INACTIVE">Ẩn</option>
              <option value="OUT_OF_STOCK">Hết hàng</option>
            </select>
            <select
              className="ctl"
              value={dur}
              onChange={(e) => setDur(e.target.value)}
            >
              <option value="">Thời lượng</option>
              <option value="<=30">≤ 30 ngày</option>
              <option value="31-180">31–180 ngày</option>
              <option value=">180">&gt; 180 ngày</option>
            </select>
            <input
              className="ctl"
              type="number"
              placeholder="Giá từ"
              value={priceMin}
              onChange={(e) => setPriceMin(e.target.value)}
              style={{ maxWidth: 120 }}
            />
            <input
              className="ctl"
              type="number"
              placeholder="Giá đến"
              value={priceMax}
              onChange={(e) => setPriceMax(e.target.value)}
              style={{ maxWidth: 120 }}
            />
            <button className="btn" onClick={resetFilters}>
              Đặt lại
            </button>
            <button className="btn primary" onClick={openCreate}>
              + Thêm biến thể
            </button>
          </div>
        </div>

        <div className="panel-body variants-area">
          {loading ? (
            <div>Đang tải…</div>
          ) : (
            <div className="variants-wrap">
              <div className="variants-scroller">
                <table className="variants-table">
                  <colgroup>
                    <col style={{ width: "20%" }} />
                    <col style={{ width: "10%" }} />
                    <col style={{ width: "10%" }} />
                    <col style={{ width: "10%" }} />
                    <col style={{ width: "10%" }} /> {/* Giá bán */}
                    <col style={{ width: "10%" }} /> {/* Giá niêm yết */}
                    <col style={{ width: "8%" }} /> {/* Trạng thái */}
                    <col style={{ width: "7%" }} /> {/* Lượt xem */}
                    <col style={{ width: "15%" }} /> {/* Thao tác */}
                  </colgroup>
                  <thead>
                    <tr>
                      <th
                        onClick={() => headerSort("title")}
                        style={{ cursor: "pointer" }}
                      >
                        Tên biến thể{sortMark("title")}
                      </th>
                      <th>Ảnh</th>
                      <th
                        onClick={() => headerSort("duration")}
                        style={{ cursor: "pointer" }}
                      >
                        Thời lượng{sortMark("duration")}
                      </th>
                      <th
                        onClick={() => headerSort("stock")}
                        style={{ cursor: "pointer" }}
                      >
                        Tồn kho{sortMark("stock")}
                      </th>
                      <th
                        onClick={() => headerSort("price")}
                        style={{ cursor: "pointer" }}
                      >
                        Giá bán{sortMark("price")}
                      </th>
                      <th>Giá niêm yết</th>
                      <th
                        onClick={() => headerSort("status")}
                        style={{ cursor: "pointer" }}
                      >
                        Trạng thái{sortMark("status")}
                      </th>
                      <th
                        onClick={() => headerSort("views")}
                        style={{ cursor: "pointer" }}
                      >
                        Lượt xem{sortMark("views")}
                      </th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>

                  <tbody>
                    {items.map((v) => (
                      <tr key={v.variantId}>
                        <td>
                          <div style={{ fontWeight: 600 }}>
                            {v.title || "—"}
                          </div>
                          <div className="muted" style={{ fontSize: 12 }}>
                            {v.variantCode || ""}
                          </div>
                        </td>

                        <td>
                          {v.thumbnail ? (
                            <img
                              src={v.thumbnail}
                              alt=""
                              style={{
                                width: 64,
                                height: 44,
                                objectFit: "cover",
                                borderRadius: 6,
                                border: "1px solid var(--line)",
                              }}
                            />
                          ) : (
                            "—"
                          )}
                        </td>

                        <td>{v.durationDays ?? 0} ngày</td>
                        <td>{v.stockQty ?? 0}</td>

                        <td className="mono">
                          {formatMoney(v.sellPrice ?? 0)}
                        </td>

                        <td className="mono">
                          {formatMoney(v.listPrice ?? 0)}
                        </td>

                        <td className="col-status">
                          <span
                            className={statusBadgeClass(
                              v.status,
                              v.stockQty
                            )}
                            style={{ textTransform: "none" }}
                          >
                            {statusLabel(v.status, v.stockQty)}
                          </span>
                        </td>

                        <td className="mono">{v.viewCount ?? 0}</td>

                        <td className="td-actions td-left">
                          <div className="action-buttons">
                            <button
                              className="action-btn edit-btn"
                              title="Xem chi tiết"
                              onClick={() => goDetail(v)}
                            >
                              <svg
                                viewBox="0 0 24 24"
                                fill="currentColor"
                                aria-hidden="true"
                              >
                                <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                                <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                              </svg>
                            </button>

                            <button
                              className="action-btn delete-btn"
                              title="Xoá"
                              onClick={() => onDelete(v.variantId)}
                            >
                              <svg
                                viewBox="0 0 24 24"
                                fill="currentColor"
                                aria-hidden="true"
                              >
                                <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                              </svg>
                            </button>

                            <label
                              className="switch"
                              title={
                                (v.stockQty ?? 0) <= 0
                                  ? "Hết hàng – bật/tắt chỉ chuyển giữa 'Hết hàng' và 'Ẩn'"
                                  : "Bật/Tắt hiển thị"
                              }
                            >
                              <input
                                type="checkbox"
                                checked={
                                  String(v.status).toUpperCase() === "ACTIVE"
                                }
                                onChange={() => toggleVariantStatus(v)}
                              />
                              <span className="slider" />
                            </label>
                          </div>
                        </td>
                      </tr>
                    ))}
                    {items.length === 0 && (
                      <tr>
                        <td
                          colSpan={9}
                          style={{
                            textAlign: "center",
                            color: "var(--muted)",
                            padding: 18,
                          }}
                        >
                          Chưa có biến thể nào.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              <div
                className="variants-footer"
                style={{
                  gap: 12,
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  flexWrap: "wrap",
                }}
              >
                <div className="muted">
                  Hiển thị {startIdx}-{endIdx} / {total}
                </div>

                <div
                  className="row"
                  style={{ gap: 8, alignItems: "center", flexWrap: "wrap" }}
                >
                  <div
                    className="row"
                    style={{ gap: 6, alignItems: "center" }}
                  >
                    <span className="muted" style={{ fontSize: 12 }}>
                      Dòng/trang
                    </span>
                    <select
                      className="ctl"
                      value={size}
                      onChange={(e) => {
                        setSize(Number(e.target.value));
                        setPage(1);
                      }}
                    >
                      <option value={5}>5</option>
                      <option value={10}>10</option>
                      <option value={20}>20</option>
                      <option value={50}>50</option>
                    </select>
                  </div>

                  <div className="row" style={{ gap: 6 }}>
                    <button
                      className="btn"
                      disabled={page <= 1}
                      onClick={() => goto(1)}
                      title="Trang đầu"
                    >
                      «
                    </button>
                    <button
                      className="btn"
                      disabled={page <= 1}
                      onClick={() => goto(page - 1)}
                      title="Trang trước"
                    >
                      ←
                    </button>

                    {makePageList.map((pKey, idx) => {
                      if (typeof pKey !== "number")
                        return (
                          <span key={pKey + idx} className="muted">
                            …
                          </span>
                        );
                      const active = pKey === page;
                      return (
                        <button
                          key={pKey}
                          className={`btn ${active ? "primary" : ""}`}
                          onClick={() => goto(pKey)}
                          disabled={active}
                          style={{ minWidth: 36 }}
                          title={`Trang ${pKey}`}
                        >
                          {pKey}
                        </button>
                      );
                    })}

                    <button
                      className="btn"
                      disabled={page >= totalPages}
                      onClick={() => goto(page + 1)}
                      title="Trang sau"
                    >
                      →
                    </button>
                    <button
                      className="btn"
                      disabled={page >= totalPages}
                      onClick={() => goto(totalPages)}
                      title="Trang cuối"
                    >
                      »
                    </button>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* MODAL CREATE */}
      {showModal && (
        <div className="modal-backdrop">
          <div
            className="modal"
            onPaste={onPaste}
            onDrop={onDrop}
            onDragOver={(e) => {
              e.preventDefault();
              e.stopPropagation();
            }}
          >
            <div className="modal-topbar">
              <h3 style={{ margin: 0 }}>Thêm biến thể</h3>
              <div className="row" style={{ gap: 8, alignItems: "center" }}>
                <label className="switch" title="Bật/Tắt hiển thị">
                  <input
                    type="checkbox"
                    checked={modalStatus === "ACTIVE"}
                    onChange={(e) =>
                      setModalStatus(e.target.checked ? "ACTIVE" : "INACTIVE")
                    }
                  />
                  <span className="slider" />
                </label>
                <span
                  className={
                    modalStatus === "ACTIVE" ? "badge green" : "badge gray"
                  }
                  style={{ textTransform: "none", fontSize: 12 }}
                >
                  {modalStatus === "ACTIVE" ? "Đang hiển thị" : "Đang ẩn"}
                </span>
              </div>
            </div>

            <form
              onSubmit={onSubmit}
              className="input-group"
              style={{ marginTop: 12 }}
            >
              <div className="grid cols-2">
                <div className="group">
                  <span>
                    Tên biến thể <span style={{ color: "#dc2626" }}>*</span>
                  </span>
                  <input
                    name="title"
                    defaultValue={productName ?? ""}
                    maxLength={TITLE_MAX}
                    className={modalErrors.title ? "input-error" : ""}
                  />
                  {modalErrors.title && (
                    <div className="field-error">{modalErrors.title}</div>
                  )}
                </div>
                <div className="group">
                  <span>
                    Mã biến thể <span style={{ color: "#dc2626" }}>*</span>
                  </span>
                  <input
                    name="variantCode"
                    defaultValue={productCode ?? ""}
                    maxLength={CODE_MAX}
                    className={modalErrors.variantCode ? "input-error" : ""}
                  />
                  {modalErrors.variantCode && (
                    <div className="field-error">
                      {modalErrors.variantCode}
                    </div>
                  )}
                </div>
              </div>

              <div className="grid cols-2" style={{ marginTop: 8 }}>
                <div className="group">
                  <span>
                    Thời lượng (ngày)
                    <span style={{ color: "#dc2626" }}>*</span>
                  </span>
                  <input
                    type="number"
                    min={0}
                    step={1}
                    name="durationDays"
                    defaultValue={0}
                    className={modalErrors.durationDays ? "input-error" : ""}
                  />
                  {modalErrors.durationDays && (
                    <div className="field-error">
                      {modalErrors.durationDays}
                    </div>
                  )}
                </div>
                <div className="group">
                  <span>Bảo hành (ngày)</span>
                  <input
                    type="number"
                    min={0}
                    step={1}
                    name="warrantyDays"
                    defaultValue={0}
                    className={modalErrors.warrantyDays ? "input-error" : ""}
                  />
                  {modalErrors.warrantyDays && (
                    <div className="field-error">
                      {modalErrors.warrantyDays}
                    </div>
                  )}
                </div>
              </div>

              <div className="grid cols-2" style={{ marginTop: 8 }}>
                <div className="group">
                  <span>
                    Giá niêm yết (đ)
                    <span style={{ color: "#dc2626" }}>*</span>
                  </span>
                  <input
                    type="text"
                    name="listPrice"
                    defaultValue={formatForInput(0)}
                    onInput={(e) => {
                      const cleaned = (e.target.value || "").replace(/[^0-9.,]/g, "");
                      e.target.value = formatForInput(cleaned);
                    }}
                    className={modalErrors.listPrice ? "input-error" : ""}
                  />
                  {modalErrors.listPrice && (
                    <div className="field-error">{modalErrors.listPrice}</div>
                  )}
                </div>
                <div className="group">
                  <span>
                    Giá bán (đ)<span style={{ color: "#dc2626" }}>*</span>
                  </span>
                  <input
                    type="text"
                    name="sellPrice"
                    defaultValue={formatForInput(0)}
                    onInput={(e) => {
                      const cleaned = (e.target.value || "").replace(/[^0-9.,]/g, "");
                      e.target.value = formatForInput(cleaned);
                    }}
                    className={modalErrors.sellPrice ? "input-error" : ""}
                  />
                  {modalErrors.sellPrice && (
                    <div className="field-error">
                      {modalErrors.sellPrice}
                    </div>
                  )}
                </div>
              </div>

              {/* Upload thumbnail */}
              <div className="group" style={{ marginTop: 8 }}>
                <span>Ảnh biến thể (thumbnail)</span>
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

              <div
                className="row"
                style={{ marginTop: 12, justifyContent: "flex-end", gap: 8 }}
              >
                <button
                  type="button"
                  className="btn"
                  onClick={() => setShowModal(false)}
                >
                  Hủy
                </button>
                <button type="submit" className="btn primary">
                  Thêm
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </div>
  );
}
