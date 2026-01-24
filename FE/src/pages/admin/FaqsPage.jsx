// src/pages/admin/FaqsPage.jsx
import React from "react";
import { ProductFaqsApi } from "../../services/productFaqs";
import { CategoryApi } from "../../services/categories";
import { ProductApi } from "../../services/products";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./CategoryPage.css"; // tái dùng CSS card/table hiện có

const QUESTION_MIN = 10;
const QUESTION_MAX = 500;
const ANSWER_MIN = 10;

/* ============ Helpers: Label + Error + Ellipsis ============ */
const RequiredMark = () => (
  <span style={{ color: "#dc2626", marginLeft: 4 }}>*</span>
);

const FieldError = ({ message }) =>
  !message ? null : (
    <div
      style={{
        color: "#dc2626",
        fontSize: 12,
        marginTop: 4,
      }}
    >
      {message}
    </div>
  );

const EllipsisCell = ({ children, title, maxWidth = 260, mono = false }) => (
  <div
    className={mono ? "mono" : undefined}
    title={title ?? (typeof children === "string" ? children : "")}
    style={{
      maxWidth,
      whiteSpace: "nowrap",
      overflow: "hidden",
      textOverflow: "ellipsis",
    }}
  >
    {children}
  </div>
);

/* ============ Modal: FAQ (Add / Edit) ============ */
function FaqModal({
  open,
  mode, // "add" | "edit"
  initial,
  categories,
  products,
  onClose,
  onSubmit,
  submitting,
  addToast,
  openConfirm,
}) {
  const isEdit = mode === "edit";

  const [form, setForm] = React.useState({
    question: "",
    answer: "",
    sortOrder: 0,
    isActive: true,
    categoryIds: [],
    productIds: [],
  });
  const [errors, setErrors] = React.useState({});
  const initialRef = React.useRef(null);

  // Panel danh mục / sản phẩm (đóng mở)
  const [showCatsPanel, setShowCatsPanel] = React.useState(false);
  const [showProductsPanel, setShowProductsPanel] = React.useState(false);

  React.useEffect(() => {
    if (open) {
      const next = {
        question: initial?.question || "",
        answer: initial?.answer || "",
        sortOrder:
          typeof initial?.sortOrder === "number" ? initial.sortOrder : 0,
        isActive:
          typeof initial?.isActive === "boolean" ? initial.isActive : true,
        categoryIds: Array.isArray(initial?.categoryIds)
          ? initial.categoryIds
          : [],
        productIds: Array.isArray(initial?.productIds)
          ? initial.productIds
          : [],
      };
      setForm(next);
      setErrors({});
      initialRef.current = next;

      // Khi mở modal, 2 panel danh sách mặc định đóng
      setShowCatsPanel(false);
      setShowProductsPanel(false);
    }
  }, [open, initial]);

  const isDirty = React.useMemo(() => {
    if (!open || !initialRef.current) return false;
    return JSON.stringify(form) !== JSON.stringify(initialRef.current);
  }, [open, form]);

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }));

  const validate = () => {
    const e = {};
    const qText = (form.question || "").trim();
    const aText = (form.answer || "").trim();

    if (!qText) {
      e.question = "Câu hỏi là bắt buộc.";
    } else if (qText.length < QUESTION_MIN || qText.length > QUESTION_MAX) {
      e.question = `Câu hỏi phải từ ${QUESTION_MIN}–${QUESTION_MAX} ký tự.`;
    }

    if (!aText) {
      e.answer = "Trả lời là bắt buộc.";
    } else if (aText.length < ANSWER_MIN) {
      e.answer = `Trả lời phải từ ${ANSWER_MIN} ký tự trở lên.`;
    }

    setErrors(e);
    if (Object.keys(e).length > 0 && typeof addToast === "function") {
      addToast(
        "warning",
        "Vui lòng kiểm tra các trường được đánh dấu.",
        "Dữ liệu FAQ chưa hợp lệ"
      );
    }
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async (evt) => {
    evt.preventDefault();
    if (!validate()) return;

    await onSubmit?.({
      question: form.question.trim(),
      answer: form.answer.trim(),
      sortOrder: Number(form.sortOrder || 0) || 0,
      isActive: !!form.isActive,
      categoryIds: form.categoryIds || [],
      productIds: form.productIds || [],
    });
  };

  const handleClose = () => {
    if (isDirty) {
      if (typeof openConfirm === "function") {
        openConfirm({
          title: "Đóng cửa sổ?",
          message:
            "Bạn có các thay đổi FAQ chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này.",
          onConfirm: () => {
            onClose?.();
          },
        });
      } else {
        const ok = window.confirm(
          "Bạn có các thay đổi FAQ chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này. Bạn có chắc muốn thoát?"
        );
        if (!ok) return;
        onClose?.();
      }
    } else {
      onClose?.();
    }
  };

  if (!open) return null;

  const catIds = form.categoryIds || [];
  const prodIds = form.productIds || [];

  const safeCategories = Array.isArray(categories) ? categories : [];
  const safeProducts = Array.isArray(products) ? products : [];

  // Nếu > 3 thì bật scroll + giới hạn chiều cao ~ 3 item
  const catScrollable = safeCategories.length > 3;
  const prodScrollable = safeProducts.length > 3;
  const catListStyle = catScrollable
    ? { maxHeight: "210px", overflowY: "auto" } // ~ 3 item
    : {};

  const prodListStyle = prodScrollable
    ? { maxHeight: "210px", overflowY: "auto" }
    : {};

  return (
    <div className="cat-modal-backdrop">
      <div className="cat-modal-card">
        <div className="cat-modal-header">
          <h3>{isEdit ? "Chỉnh sửa FAQ" : "Thêm FAQ"}</h3>
          {/* Trạng thái */}
          <div className="group" style={{ marginTop: 8 }}>
            <div className="row" style={{ gap: 8, alignItems: "center" }}>
              <label className="switch" title="Bật/Tắt hiển thị">
                <input
                  type="checkbox"
                  checked={!!form.isActive}
                  onChange={() => set("isActive", !form.isActive)}
                />
                <span className="slider" />
              </label>
              <span
                className={form.isActive ? "badge green" : "badge gray"}
                style={{ textTransform: "none" }}
              >
                {form.isActive ? "Đang hiển thị" : "Đang ẩn"}
              </span>
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="cat-modal-body input-group">
            {/* ===== Hàng 1: Câu hỏi + Trả lời + Thứ tự ===== */}
            <div className="faq-fields-grid">
              {/* Cột trái: Câu hỏi (trên) + Thứ tự (dưới) */}
              <div className="group faq-q">
                <span>
                  Câu hỏi <RequiredMark />
                </span>
                <input
                  value={form.question}
                  onChange={(e) => set("question", e.target.value)}
                  placeholder="Nhập câu hỏi…"
                />
                <FieldError message={errors.question} />
              </div>

              <div className="group faq-sort">
                <span>Thứ tự</span>
                <input
                  type="number"
                  min={0}
                  step={1}
                  value={form.sortOrder}
                  onChange={(e) =>
                    set(
                      "sortOrder",
                      Math.max(0, parseInt(e.target.value, 10) || 0)
                    )
                  }
                />
              </div>

              {/* Cột phải: Trả lời (chiếm 2 hàng) */}
              <div className="group faq-answer">
                <span>
                  Trả lời <RequiredMark />
                </span>
                <textarea
                  rows={6}
                  value={form.answer}
                  onChange={(e) => set("answer", e.target.value)}
                  placeholder="Nhập câu trả lời chi tiết…"
                />
                <FieldError message={errors.answer} />
              </div>
            </div>

            {/* ===== Hàng 2: Danh mục + Sản phẩm ===== */}
            <div className="faq-apply-grid">
              {/* Danh sách danh mục */}
              <div className="group">
                <div className={`panel ${!showCatsPanel ? "collapsed" : ""}`}>
                  <div
                    className="panel-header"
                    onClick={() => setShowCatsPanel((s) => !s)}
                    style={{ cursor: "pointer" }}
                  >
                    <h4>
                      Áp dụng cho danh mục{" "}
                      <span
                        style={{
                          fontSize: 12,
                          color: "var(--muted)",
                          marginLeft: 8,
                        }}
                      >
                        ({catIds.length}/{safeCategories.length} đã chọn)
                      </span>
                    </h4>
                    <div className="caret">▾</div>
                  </div>
                  {showCatsPanel && (
                    <div className="panel-body" style={catListStyle}>
                      {safeCategories.length === 0 && (
                        <div className="muted">
                          Không có danh mục hoạt động nào.
                        </div>
                      )}
                      {safeCategories.map((c) => {
                        const id = c.categoryId ?? c.id;
                        const name =
                          c.categoryName ?? c.name ?? `Danh mục #${id}`;
                        const checked = catIds.includes(id);
                        return (
                          <div key={id} className="list-row">
                            <div className="left">
                              <div>{name}</div>
                            </div>
                            <div>
                              <label className="switch">
                                <input
                                  type="checkbox"
                                  checked={checked}
                                  onChange={(e) => {
                                    if (e.target.checked) {
                                      set(
                                        "categoryIds",
                                        Array.from(new Set([...catIds, id]))
                                      );
                                    } else {
                                      set(
                                        "categoryIds",
                                        catIds.filter((x) => x !== id)
                                      );
                                    }
                                  }}
                                />
                                <span className="slider" />
                              </label>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              </div>

              {/* Danh sách sản phẩm */}
              <div className="group">
                <div
                  className={`panel ${!showProductsPanel ? "collapsed" : ""}`}
                >
                  <div
                    className="panel-header"
                    onClick={() => setShowProductsPanel((s) => !s)}
                    style={{ cursor: "pointer" }}
                  >
                    <h4>
                      Áp dụng cho sản phẩm{" "}
                      <span
                        style={{
                          fontSize: 12,
                          color: "var(--muted)",
                          marginLeft: 8,
                        }}
                      >
                        ({prodIds.length}/{safeProducts.length} đã chọn)
                      </span>
                    </h4>
                    <div className="caret">▾</div>
                  </div>
                  {showProductsPanel && (
                    <div className="panel-body" style={prodListStyle}>
                      {safeProducts.length === 0 && (
                        <div className="muted">
                          Không có sản phẩm nào.
                        </div>
                      )}
                      {safeProducts.map((p) => {
                        const id = p.productId ?? p.id;
                        const name =
                          p.productName ?? p.name ?? `Sản phẩm #${id}`;
                        const checked = prodIds.includes(id);
                        return (
                          <div key={id} className="list-row">
                            <div className="left">
                              {/* chỉ hiển thị TÊN sản phẩm */}
                              <div>{name}</div>
                            </div>
                            <div>
                              <label className="switch">
                                <input
                                  type="checkbox"
                                  checked={checked}
                                  onChange={(e) => {
                                    if (e.target.checked) {
                                      set(
                                        "productIds",
                                        Array.from(new Set([...prodIds, id]))
                                      );
                                    } else {
                                      set(
                                        "productIds",
                                        prodIds.filter((x) => x !== id)
                                      );
                                    }
                                  }}
                                />
                                <span className="slider" />
                              </label>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>

          <div className="cat-modal-footer">
            <button
              type="button"
              className="btn ghost"
              onClick={handleClose}
              disabled={submitting}
            >
              Hủy
            </button>
            <button
              type="submit"
              className="btn primary"
              disabled={submitting}
            >
              {submitting
                ? isEdit
                  ? "Đang lưu…"
                  : "Đang tạo…"
                : isEdit
                ? "Lưu thay đổi"
                : "Tạo FAQ"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ============ MAIN PAGE ============ */
export default function FaqsPage() {
  // ===== Toast & Confirm =====
  const [toasts, setToasts] = React.useState([]);
  const [confirmDialog, setConfirmDialog] = React.useState(null);
  const toastIdRef = React.useRef(1);

  const removeToast = (id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  const addToast = (type, message, title) => {
    const id = toastIdRef.current++;
    setToasts((prev) => [
      ...prev,
      { id, type, message, title: title || undefined },
    ]);
    setTimeout(() => removeToast(id), 5000);
    return id;
  };

  const openConfirm = ({ title, message, onConfirm }) => {
    setConfirmDialog({
      title,
      message,
      onConfirm: async () => {
        setConfirmDialog(null);
        await onConfirm?.();
      },
      onCancel: () => setConfirmDialog(null),
    });
  };

  // ===== FAQ list/query =====
  const [faqQuery, setFaqQuery] = React.useState({
    keyword: "",
    active: "",
    sort: "sortOrder",
    direction: "asc",
  });
  const [faqs, setFaqs] = React.useState([]);
  const [faqLoading, setFaqLoading] = React.useState(false);
  const [faqPage, setFaqPage] = React.useState(1);
  const [faqPageSize, setFaqPageSize] = React.useState(10);
  const [faqTotal, setFaqTotal] = React.useState(0);

  const [faqModal, setFaqModal] = React.useState({
    open: false,
    mode: "add", // "add" | "edit"
    data: null,
  });
  const [faqSubmitting, setFaqSubmitting] = React.useState(false);

  // ===== Danh mục & Sản phẩm =====
  const [categories, setCategories] = React.useState([]);
  const [products, setProducts] = React.useState([]);

  React.useEffect(() => {
    // Danh mục active
    CategoryApi.list({ active: true })
      .then((data) => {
        const arr = Array.isArray(data)
          ? data
          : Array.isArray(data?.items)
          ? data.items
          : Array.isArray(data?.data)
          ? data.data
          : Array.isArray(data?.result)
          ? data.result
          : [];
        setCategories(arr);
      })
      .catch((e) => {
        setCategories([]);
        addToast(
          "error",
          e?.response?.data?.message || e.message,
          "Lỗi tải danh mục"
        );
      });

    // Sản phẩm: lấy TẤT CẢ để có thể gắn FAQ cho cả sản phẩm hết hàng / ngưng bán
    ProductApi.list({})
      .then((data) => {
        const arr = Array.isArray(data)
          ? data
          : Array.isArray(data?.items)
          ? data.items
          : Array.isArray(data?.data)
          ? data.data
          : Array.isArray(data?.result)
          ? data.result
          : [];
        setProducts(arr);
      })
      .catch((e) => {
        setProducts([]);
        addToast(
          "error",
          e?.response?.data?.message || e.message,
          "Lỗi tải sản phẩm"
        );
      });
  }, []); // chỉ load 1 lần

  const loadFaqs = React.useCallback(() => {
    setFaqLoading(true);

    const params = {
      keyword: faqQuery.keyword || undefined,
      active:
        faqQuery.active === ""
          ? undefined
          : faqQuery.active === "true",
      sort: faqQuery.sort || "sortOrder",
      direction: faqQuery.direction || "asc",
      page: faqPage,
      pageSize: faqPageSize,
    };

    ProductFaqsApi.listPaged(params)
      .then((res) => {
        const items = res?.items ?? res ?? [];
        setFaqs(items);
        setFaqTotal(
          typeof res?.total === "number" ? res.total : items.length
        );
      })
      .catch((err) => {
        console.error(err);
        addToast("error", "Không tải được danh sách FAQ.", "Lỗi");
      })
      .finally(() => setFaqLoading(false));
  }, [faqQuery, faqPage, faqPageSize]);

  React.useEffect(() => {
    const t = setTimeout(loadFaqs, 300);
    return () => clearTimeout(t);
  }, [loadFaqs]);

  React.useEffect(() => {
    setFaqPage(1);
  }, [faqQuery.keyword, faqQuery.active, faqQuery.sort, faqQuery.direction]);

  const faqToggle = async (f) => {
    try {
      const resp = await ProductFaqsApi.toggle(f.faqId);
      const isActive =
        resp?.isActive ??
        resp?.IsActive ??
        resp?.data?.isActive ??
        resp?.data?.IsActive ??
        !f.isActive;

      let msg;
      if (isActive === true) msg = "FAQ đang được hiển thị.";
      else if (isActive === false) msg = "FAQ đã được ẩn.";
      else msg = "Đã cập nhật trạng thái FAQ.";

      addToast("success", msg, "Thành công");
      loadFaqs();
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message || "Không thể cập nhật trạng thái FAQ.",
        "Lỗi"
      );
    }
  };

  const deleteFaq = (f) => {
    openConfirm({
      title: "Xoá FAQ?",
      message: `Xoá câu hỏi "${(f.question || "").slice(
        0,
        80
      )}"? Hành động này không thể hoàn tác!`,
      onConfirm: async () => {
        try {
          await ProductFaqsApi.remove(f.faqId);
          addToast("success", "Đã xoá FAQ.", "Thành công");
          loadFaqs();
        } catch (e) {
          console.error(e);
          addToast(
            "error",
            e?.response?.data?.message || "Xoá FAQ thất bại.",
            "Lỗi"
          );
        }
      },
    });
  };

  const openAddFaq = () =>
    setFaqModal({ open: true, mode: "add", data: null });

  const openEditFaq = async (f) => {
    try {
      const detail = await ProductFaqsApi.getById(f.faqId);
      setFaqModal({
        open: true,
        mode: "edit",
        data: detail,
      });
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        e?.response?.data?.message ||
          "Không tải được chi tiết FAQ để chỉnh sửa.",
        "Lỗi"
      );
    }
  };

  const handleFaqSubmit = async (form) => {
    setFaqSubmitting(true);
    try {
      if (faqModal.mode === "add") {
        await ProductFaqsApi.create(form);
        addToast("success", "Đã tạo FAQ.", "Thành công");
      } else if (faqModal.mode === "edit" && faqModal.data) {
        await ProductFaqsApi.update(faqModal.data.faqId, form);
        addToast("success", "Đã lưu thay đổi FAQ.", "Thành công");
      }
      setFaqModal((m) => ({ ...m, open: false }));
      loadFaqs();
    } catch (err) {
      console.error(err);
      addToast(
        "error",
        err?.response?.data?.message || "Lưu FAQ thất bại.",
        "Lỗi"
      );
      throw err;
    } finally {
      setFaqSubmitting(false);
    }
  };

  const faqTotalPages = Math.max(
    1,
    Math.ceil((faqTotal || 0) / (faqPageSize || 1))
  );

  return (
    <>
      <div className="page">
        {/* ===== Khối: FAQ ===== */}
        <div className="card">
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
            }}
          >
            <h2>Câu hỏi thường gặp (FAQ)</h2>
            <div className="row" style={{ gap: 8 }}>
              <button className="btn primary" onClick={openAddFaq}>
                + Thêm FAQ
              </button>
            </div>
          </div>

          {/* Bộ lọc FAQ */}
          <div
            className="row input-group"
            style={{
              gap: 10,
              marginTop: 12,
              flexWrap: "nowrap",
              alignItems: "end",
              overflowX: "auto",
            }}
          >
            <div className="group" style={{ minWidth: 320, maxWidth: 520 }}>
              <span>Tìm kiếm</span>
              <input
                value={faqQuery.keyword}
                onChange={(e) =>
                  setFaqQuery((s) => ({ ...s, keyword: e.target.value }))
                }
                placeholder="Tìm theo câu hỏi hoặc câu trả lời…"
              />
            </div>
            <div className="group" style={{ minWidth: 160 }}>
              <span>Trạng thái</span>
              <select
                value={faqQuery.active}
                onChange={(e) =>
                  setFaqQuery((s) => ({ ...s, active: e.target.value }))
                }
              >
                <option value="">Tất cả</option>
                <option value="true">Hiển thị</option>
                <option value="false">Ẩn</option>
              </select>
            </div>

            {faqLoading && <span className="badge gray">Đang tải…</span>}

            <button
              className="btn"
              onClick={() =>
                setFaqQuery({
                  keyword: "",
                  active: "",
                  sort: "sortOrder",
                  direction: "asc",
                })
              }
              title="Xoá bộ lọc"
            >
              Đặt lại
            </button>
          </div>

          {/* Bảng FAQ */}
          <table
            className="table variants-table faq-table"
            style={{ marginTop: 10 }}
          >
            <thead>
              <tr>
                <th
                  onClick={() =>
                    setFaqQuery((s) => ({
                      ...s,
                      sort: "question",
                      direction:
                        s.sort === "question" && s.direction === "asc"
                          ? "desc"
                          : "asc",
                    }))
                  }
                  style={{ cursor: "pointer" }}
                >
                  Câu hỏi{" "}
                  {faqQuery.sort === "question"
                    ? faqQuery.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th>Trả lời</th>
                <th
                  onClick={() =>
                    setFaqQuery((s) => ({
                      ...s,
                      sort: "sortOrder",
                      direction:
                        s.sort === "sortOrder" && s.direction === "asc"
                          ? "desc"
                          : "asc",
                    }))
                  }
                  style={{ cursor: "pointer", width: 90 }}
                >
                  Thứ tự{" "}
                  {faqQuery.sort === "sortOrder"
                    ? faqQuery.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th>Danh mục</th>
                <th>Sản phẩm</th>
                <th
                  onClick={() =>
                    setFaqQuery((s) => ({
                      ...s,
                      sort: "active",
                      direction:
                        s.sort === "active" && s.direction === "asc"
                          ? "desc"
                          : "asc",
                    }))
                  }
                  style={{ cursor: "pointer", width: 120 }}
                >
                  Trạng thái{" "}
                  {faqQuery.sort === "active"
                    ? faqQuery.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {faqs.map((f) => (
                <tr key={f.faqId}>
                  <td>
                    <EllipsisCell maxWidth={340} title={f.question}>
                      {f.question}
                    </EllipsisCell>
                  </td>
                  <td>
                    <EllipsisCell maxWidth={420} title={f.answer}>
                      {f.answer}
                    </EllipsisCell>
                  </td>
                  <td className="mono">{f.sortOrder ?? 0}</td>
                  <td className="mono">
                    {f.categoryCount ?? f.categoriesCount ?? 0}
                  </td>
                  <td className="mono">
                    {f.productCount ?? f.productsCount ?? 0}
                  </td>
                  <td>
                    <span
                      className={f.isActive ? "badge green" : "badge gray"}
                    >
                      {f.isActive ? "Hiển thị" : "Ẩn"}
                    </span>
                  </td>
                  <td className="td-actions td-left">
                    <div className="td-actions-row">
                      <div className="action-buttons">
                        <button
                          className="action-btn edit-btn"
                          type="button"
                          title="Xem chi tiết / chỉnh sửa"
                          onClick={() => openEditFaq(f)}
                        >
                          <svg
                            viewBox="0 0 24 24"
                            width="16"
                            height="16"
                            fill="currentColor"
                            aria-hidden="true"
                          >
                            <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                            <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                          </svg>
                        </button>
                        <button
                          className="action-btn delete-btn"
                          title="Xoá FAQ"
                          type="button"
                          onClick={() => deleteFaq(f)}
                        >
                          <svg
                            viewBox="0 0 24 24"
                            width="16"
                            height="16"
                            fill="currentColor"
                            aria-hidden="true"
                          >
                            <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                          </svg>
                        </button>
                      </div>

                      <label className="switch" title="Bật/Tắt hiển thị">
                        <input
                          type="checkbox"
                          checked={!!f.isActive}
                          onChange={() => faqToggle(f)}
                        />
                        <span className="slider" />
                      </label>
                    </div>
                  </td>
                </tr>
              ))}

              {faqs.length === 0 && !faqLoading && (
                <tr>
                  <td colSpan={7} style={{ textAlign: "center", padding: 14 }}>
                    Không có FAQ nào.
                  </td>
                </tr>
              )}
            </tbody>
          </table>

          {/* Pagination: FAQ */}
          <div className="pager">
            <button
              disabled={faqPage <= 1}
              onClick={() => setFaqPage((p) => Math.max(1, p - 1))}
            >
              Trước
            </button>
            <span style={{ padding: "0 8px" }}>
              Trang {faqPage} / {faqTotalPages}
            </span>
            <button
              disabled={faqPage >= faqTotalPages}
              onClick={() =>
                setFaqPage((p) => Math.min(faqTotalPages, p + 1))
              }
            >
              Tiếp
            </button>

            <div style={{ marginLeft: "auto", display: "flex", gap: 6 }}>
              <span className="muted" style={{ fontSize: 12 }}>
                Dòng/trang
              </span>
              <select
                className="ctl"
                value={faqPageSize}
                onChange={(e) => {
                  setFaqPageSize(Number(e.target.value));
                  setFaqPage(1);
                }}
              >
                <option value={5}>5</option>
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      {/* Modal FAQ */}
      <FaqModal
        open={faqModal.open}
        mode={faqModal.mode}
        initial={faqModal.data}
        categories={categories}
        products={products}
        onClose={() => setFaqModal((m) => ({ ...m, open: false }))}
        onSubmit={handleFaqSubmit}
        submitting={faqSubmitting}
        addToast={addToast}
        openConfirm={openConfirm}
      />

      {/* Toast + Confirm Dialog */}
      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </>
  );
}
