// src/pages/admin/ProductSectionsPanel.jsx
import React from "react";
import Quill from "quill";
import "quill/dist/quill.snow.css";

import { ProductSectionsApi } from "../../services/productSections";
import { postsApi } from "../../services/postsApi";
import ToastContainer from "../../components/Toast/ToastContainer";

const TYPES = [
  { value: "DETAIL", label: "Chi tiết" },
  { value: "WARRANTY", label: "Bảo hành" },
  { value: "NOTE", label: "Lưu ý" },
];

const SECTION_TITLE_MAX = 200;

const typeLabel = (v) =>
  TYPES.find((t) => t.value === String(v || "").toUpperCase())?.label ||
  v ||
  "—";

const snippet = (html, n = 120) => {
  const t = String(html || "")
    .replace(/<[^>]+>/g, "")
    .replace(/\s+/g, " ")
    .trim();
  return t.length > n ? t.slice(0, n) + "…" : t || "—";
};

// ===== Helpers: chuẩn hoá field từ BE =====
const getId = (row) => row.sectionId ?? row.SectionId;
const getTitle = (row) => row.title ?? row.Title ?? "";
const getType = (row) =>
  String(
    row.sectionType ?? row.SectionType ?? row.type ?? row.Type ?? ""
  ).toUpperCase();
const getActive = (row) =>
  Boolean(row.isActive ?? row.IsActive ?? row.active ?? row.Active ?? true);
const getSortVal = (row) =>
  Number(row.sortOrder ?? row.SortOrder ?? row.sort ?? row.Sort ?? 0);
const getContent = (row) => row.content ?? row.Content ?? "";

// Map sort key UI -> API
const mapSortKeyForApi = (key) => {
  switch (String(key)) {
    case "type":
      return "sectionType";
    case "sort":
      return "sortOrder";
    case "active":
      return "isActive";
    case "title":
      return "title";
    default:
      return key || "sortOrder";
  }
};

export default function ProductSectionsPanel({
  productId,
  variantId = null, // có thể bỏ nếu section nằm ở cấp product
}) {
  // List + paging
  const [items, setItems] = React.useState([]);
  const [total, setTotal] = React.useState(0);
  const [page, setPage] = React.useState(1);
  const [size, setSize] = React.useState(10);
  const [totalPages, setTotalPages] = React.useState(1);
  const [loading, setLoading] = React.useState(false);

  // Query
  const [q, setQ] = React.useState("");
  const [type, setType] = React.useState(""); // "", DETAIL|WARRANTY|NOTE
  const [active, setActive] = React.useState(""); // "", true|false
  const [sort, setSort] = React.useState("sort"); // sort|title|type|active
  const [dir, setDir] = React.useState("asc"); // asc|desc

  // Toast + Confirm
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

  // Modal
  const [showModal, setShowModal] = React.useState(false);
  const [editing, setEditing] = React.useState(null);

  // Form state in modal
  const [fTitle, setFTitle] = React.useState("");
  const [fType, setFType] = React.useState("DETAIL");
  const [fActive, setFActive] = React.useState(true);
  const [fSort, setFSort] = React.useState(0);
  const [fContent, setFContent] = React.useState("");

  const [modalErrors, setModalErrors] = React.useState({});
  const [saving, setSaving] = React.useState(false);

  // Snapshot form ban đầu trong modal để detect unsaved changes
  const modalInitialRef = React.useRef(null);

  // Quill
  const quillRef = React.useRef(null);
  const editorWrapRef = React.useRef(null);
  const imageInputRef = React.useRef(null);

  // ===== Load list =====
  const load = React.useCallback(
    async () => {
      setLoading(true);
      try {
        const apiSort = mapSortKeyForApi(sort);
        const res = await ProductSectionsApi.listPaged(productId, variantId, {
          q,
          type,
          active,
          sort: apiSort,
          dir,
          page,
          pageSize: size,
        });
        // Chuẩn hoá items ngay tại UI
        const normItems = (res.items || []).map((r) => ({
          sectionId: getId(r),
          title: getTitle(r),
          sectionType: getType(r),
          isActive: getActive(r),
          sortOrder: getSortVal(r),
          content: getContent(r),
        }));
        setItems(normItems);
        setTotal(res.totalItems || 0);
        setTotalPages(res.totalPages || 1);
      } catch (err) {
        console.error(err);
        addToast(
          "error",
          "Lỗi tải sections",
          err?.response?.data?.message || err.message
        );
      } finally {
        setLoading(false);
      }
    },
    [productId, variantId, q, type, active, sort, dir, page, size, addToast]
  );

  React.useEffect(() => {
    load();
  }, [load]);

  React.useEffect(() => {
    setPage(1);
  }, [q, type, active, sort, dir, size]);

  // ===== Sort header =====
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
  const sortMark = (key) => (sort === key ? (dir === "asc" ? " ▲" : " ▼") : "");

  // ===== CRUD =====
  const openCreate = () => {
    const initial = {
      title: "",
      type: "DETAIL",
      active: true,
      sort: 0,
      content: "",
    };
    setEditing(null);
    setFTitle(initial.title);
    setFType(initial.type);
    setFActive(initial.active);
    setFSort(initial.sort);
    setFContent(initial.content);
    setModalErrors({});
    modalInitialRef.current = initial;
    setShowModal(true);
  };

  const openEdit = (row) => {
    const initial = {
      title: getTitle(row),
      type: getType(row) || "DETAIL",
      active: getActive(row),
      sort: getSortVal(row),
      content: getContent(row),
    };
    setEditing(row);
    setFTitle(initial.title);
    setFType(initial.type);
    setFActive(initial.active);
    setFSort(initial.sort);
    setFContent(initial.content);
    setModalErrors({});
    modalInitialRef.current = initial;
    setShowModal(true);
  };

  const onDelete = async (id) => {
    const ok = await askConfirm(
      "Xoá section",
      "Bạn có chắc chắn muốn xoá section này?"
    );
    if (!ok) return;
    try {
      await ProductSectionsApi.remove(productId, variantId, id);
      addToast("success", "Đã xoá section", "Section đã được xoá.");
      await load();
    } catch (err) {
      console.error(err);
      addToast(
        "error",
        "Xoá section thất bại",
        err?.response?.data?.message || err.message
      );
    }
  };

  const toggleActive = async (row) => {
    const id = getId(row);
    try {
      const resp = await ProductSectionsApi.toggle(productId, variantId, id);

      setItems((prev) => {
        const before = prev.find((x) => x.sectionId === id);
        if (!before) return prev;

        // Nếu BE trả về record đầy đủ/partial -> MERGE với dữ liệu cũ
        const isObject =
          resp && typeof resp === "object" && !Array.isArray(resp);

        if (isObject) {
          const mergedRaw = { ...before, ...resp };
          const patched = {
            sectionId: getId(mergedRaw) ?? before.sectionId,
            title: getTitle(mergedRaw) || before.title,
            sectionType: getType(mergedRaw) || before.sectionType,
            isActive:
              typeof getActive(mergedRaw) === "boolean"
                ? getActive(mergedRaw)
                : before.isActive,
            sortOrder: Number.isFinite(getSortVal(mergedRaw))
              ? getSortVal(mergedRaw)
              : before.sortOrder,
            content: getContent(mergedRaw) || before.content,
          };
          return prev.map((x) => (x.sectionId === id ? patched : x));
        }

        // Fallback: BE trả true/false hoặc rỗng -> chỉ flip isActive
        return prev.map((x) =>
          x.sectionId === id ? { ...x, isActive: !x.isActive } : x
        );
      });

      addToast(
        "success",
        "Cập nhật trạng thái",
        resp?.isActive ?? resp?.IsActive
          ? "Section đang được hiển thị."
          : "Section đã được ẩn."
      );
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        "Đổi trạng thái thất bại",
        e?.response?.data?.message || e.message
      );
    }
  };

  // ===== VALIDATION =====
  const validateForm = React.useCallback(
    ({ silent = true } = {}) => {
      const errors = {};

      // Tiêu đề: bắt buộc, <= 200, không chỉ whitespace
      const titleTrimmed = (fTitle || "").trim();
      if (!titleTrimmed) {
        errors.title = "Tiêu đề section là bắt buộc.";
      } else if (titleTrimmed.length > SECTION_TITLE_MAX) {
        errors.title = `Tiêu đề section không được vượt quá ${SECTION_TITLE_MAX} ký tự.`;
      }

      // Sort: số nguyên không âm (blank => 0)
      let sortNum = 0;
      const rawSort = fSort;
      if (rawSort === "" || rawSort == null) {
        sortNum = 0;
      } else if (!/^-?\d+$/.test(String(rawSort))) {
        errors.sortOrder = "Thứ tự phải là số nguyên không âm.";
      } else {
        sortNum = Number(rawSort);
        if (!Number.isInteger(sortNum) || sortNum < 0) {
          errors.sortOrder = "Thứ tự phải là số nguyên không âm.";
        }
      }

      // Nội dung: bắt buộc (check rỗng sau khi bỏ tag & khoảng trắng)
      const html = (fContent || "").trim();
      const plain = html
        .replace(/<[^>]+>/g, "")
        .replace(/&nbsp;/g, " ")
        .replace(/\s+/g, "")
        .trim();
      if (!plain) {
        errors.content = "Nội dung section là bắt buộc.";
      }

      if (!silent) {
        setModalErrors(errors);
      }

      const isValid = Object.keys(errors).length === 0;

      const dto = isValid
        ? {
            title: titleTrimmed,
            sectionType: (fType || "DETAIL").toUpperCase(), // WARRANTY|NOTE|DETAIL
            content: html || "<p></p>",
            isActive: !!fActive,
            sortOrder: sortNum,
          }
        : null;

      return { errors, isValid, dto };
    },
    [fTitle, fType, fActive, fSort, fContent]
  );

  // Detect unsaved change trong modal
  const isModalDirty = React.useMemo(() => {
    if (!showModal || !modalInitialRef.current) return false;
    const cur = {
      title: fTitle,
      type: fType,
      active: fActive,
      sort: fSort,
      content: fContent,
    };
    return JSON.stringify(cur) !== JSON.stringify(modalInitialRef.current);
  }, [showModal, fTitle, fType, fActive, fSort, fContent]);

  const handleCloseModal = async () => {
    if (isModalDirty) {
      const ok = await askConfirm(
        "Huỷ thay đổi?",
        "Bạn có các thay đổi chưa lưu. Đóng cửa sổ sẽ làm mất các thay đổi này."
      );
      if (!ok) return;
    }
    setShowModal(false);
  };

  const onSubmit = async (e) => {
    e.preventDefault();
    const { isValid, dto } = validateForm({ silent: false });
    if (!isValid || !dto) {
      addToast(
        "warning",
        "Dữ liệu chưa hợp lệ",
        "Vui lòng kiểm tra các trường được đánh dấu."
      );
      return;
    }

    try {
      setSaving(true);
      if (editing?.sectionId) {
        await ProductSectionsApi.update(
          productId,
          variantId,
          editing.sectionId,
          dto
        );
        addToast("success", "Cập nhật mô tả", "Mô tả đã được lưu thay đổi.");
      } else {
        await ProductSectionsApi.create(productId, variantId, dto);
        addToast("success", "Thêm mô tả", "Mô tả mới đã được tạo.");
      }

      await load();
      setShowModal(false);
    } catch (err) {
      console.error(err);
      addToast(
        "error",
        "Lưu section thất bại",
        err?.response?.data?.message || err.message
      );
    } finally {
      setSaving(false);
    }
  };

  // ===== Paging helpers =====
  const goto = (p) => setPage(Math.min(Math.max(1, p), totalPages));
  const makePageList = React.useMemo(() => {
    const pages = [];
    const win = 2;
    const from = Math.max(1, page - win);
    const to = Math.min(totalPages, page + win);
    if (from > 1) {
      pages.push(1);
      if (from > 2) pages.push("…L");
    }
    for (let i = from; i <= to; i++) pages.push(i);
    if (to < totalPages) {
      if (to < totalPages - 1) pages.push("…R");
      pages.push(totalPages);
    }
    return pages;
  }, [page, totalPages]);
  const startIdx = total === 0 ? 0 : (page - 1) * size + 1;
  const endIdx = Math.min(total, page * size);

  // ===== Quill init/unmount mỗi khi mở modal =====
  React.useEffect(() => {
    if (!showModal) return;
    if (!editorWrapRef.current) return;

    // cleanup quill trước đó (nếu có)
    if (quillRef.current) {
      try {
        const container = quillRef.current.root?.parentNode?.parentNode;
        if (container && container.parentNode) container.parentNode.innerHTML = "";
      } catch (_) {}
      quillRef.current = null;
    }

    const mount = document.createElement("div");
    editorWrapRef.current.appendChild(mount);

    const toolbarOptions = [
      ["bold", "italic", "underline", "strike"],
      [{ header: 1 }, { header: 2 }],
      [{ list: "ordered" }, { list: "bullet" }, { list: "check" }],
      ["blockquote", "code-block"],
      ["link", "image", "video"],
      [{ color: [] }, { background: [] }],
      [{ align: [] }],
      ["clean"],
    ];

    const q = new Quill(mount, {
      theme: "snow",
      placeholder: "Nhập nội dung…",
      modules: {
        toolbar: {
          container: toolbarOptions,
          handlers: {
            image: function () {
              imageInputRef.current && imageInputRef.current.click();
            },
          },
        },
      },
    });

    if (fContent) q.clipboard.dangerouslyPasteHTML(fContent);

    // Sync nội dung về state để validation realtime
    q.on("text-change", () => {
      setFContent(q.root.innerHTML);
    });

    quillRef.current = q;

    return () => {
      try {
        q.off("text-change");
        const container = q.root?.parentNode?.parentNode;
        if (container && container.parentNode) container.parentNode.innerHTML = "";
      } catch (_) {}
      quillRef.current = null;
    };
  }, [showModal]); // fContent đã inject khi openEdit/openCreate

  // Upload ảnh cho Quill
  const handleQuillImage = async (e) => {
    const file = e.target.files?.[0];
    if (!file || !quillRef.current) return;
    const range = quillRef.current.getSelection(true) || { index: 0 };
    try {
      const resp = await postsApi.uploadImage(file);
      let imageUrl = null;
      if (typeof resp === "string") imageUrl = resp;
      else if (resp.path) imageUrl = resp.path;
      else if (resp.imageUrl) imageUrl = resp.imageUrl;
      else if (resp.url) imageUrl = resp.url;
      else if (resp.data && typeof resp.data === "string") imageUrl = resp.data;
      else {
        const vals = Object.values(resp);
        if (vals.length && typeof vals[0] === "string") imageUrl = vals[0];
      }
      if (!imageUrl) throw new Error("Không lấy được URL ảnh từ server");

      quillRef.current.insertEmbed(range.index, "image", imageUrl);
      quillRef.current.setSelection(range.index + 1);
    } catch (err) {
      console.error("Upload ảnh thất bại:", err);
      addToast("error", "Tải ảnh thất bại", "Vui lòng thử lại sau.");
    } finally {
      e.target.value = "";
    }
  };

  return (
    <div className="group" style={{ gridColumn: "1 / 3" }}>
      <div className="panel">
        <div className="panel-header" style={{ alignItems: "center" }}>
          <h4>
            Thông tin sản phẩm{" "}
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

          {/* Toolbar */}
          <div className="variants-toolbar">
            <input
              className="ctl"
              placeholder="Tìm theo tiêu đề/nội dung…"
              value={q}
              onChange={(e) => setQ(e.target.value)}
            />
            <select
              className="ctl"
              value={type}
              onChange={(e) => setType(e.target.value)}
            >
              <option value="">Tất cả loại</option>
              {TYPES.map((t) => (
                <option key={t.value} value={t.value}>
                  {t.label}
                </option>
              ))}
            </select>
            <select
              className="ctl"
              value={active}
              onChange={(e) => setActive(e.target.value)}
            >
              <option value="">Tất cả trạng thái</option>
              <option value="true">Đang hiển thị</option>
              <option value="false">Đang ẩn</option>
            </select>
            <button className="btn primary" onClick={openCreate}>
              + Thêm mô tả
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
                    <col style={{ width: "26%" }} />
                    <col style={{ width: "10%" }} />
                    <col style={{ width: "32%" }} />
                    <col style={{ width: "8%" }} />
                    <col style={{ width: "8%" }} />
                    <col style={{ width: "16%" }} />
                  </colgroup>
                  <thead>
                    <tr>
                      <th
                        onClick={() => headerSort("title")}
                        style={{ cursor: "pointer" }}
                      >
                        Tiêu đề{sortMark("title")}
                      </th>
                      <th
                        onClick={() => headerSort("type")}
                        style={{ cursor: "pointer" }}
                      >
                        Loại{sortMark("type")}
                      </th>
                      <th>Nội dung</th>
                      <th
                        onClick={() => headerSort("sort")}
                        style={{ cursor: "pointer" }}
                      >
                        Thứ tự{sortMark("sort")}
                      </th>
                      <th
                        onClick={() => headerSort("active")}
                        style={{ cursor: "pointer" }}
                      >
                        Trạng thái{sortMark("active")}
                      </th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                 <tbody>
  {items.map((r) => (
    <tr key={r.sectionId}>
      <td>
        <div
          style={{
            fontWeight: 600,
            maxWidth: 260,
            whiteSpace: "nowrap",
            overflow: "hidden",
            textOverflow: "ellipsis",
          }}
          title={r.title || "—"}
        >
          {r.title || "—"}
        </div>
      </td>

      <td>{typeLabel(r.sectionType)}</td>

      <td title={snippet(r.content, 300)}>
        {snippet(r.content, 80)}
      </td>

                        <td className="td-right">
                          {Number(r.sortOrder || 0)}
                        </td>

                        <td className="col-status">
                          {r.isActive ? (
                            <span className="badge green">Hiển thị</span>
                          ) : (
                            <span className="badge gray">Ẩn</span>
                          )}
                        </td>

                        <td className="td-actions td-left">
  <div className="action-buttons">
    <button
      className="action-btn edit-btn"
      title="Sửa"
      onClick={() => openEdit(r)}
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
                              onClick={() => onDelete(r.sectionId)}
                            >
                              <svg
                                viewBox="0 0 24 24"
                                fill="currentColor"
                                aria-hidden="true"
                              >
                                <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                              </svg>
                            </button>

                            <label className="switch" title="Bật/Tắt hiển thị">
                              <input
                                type="checkbox"
                                checked={!!r.isActive}
                                onChange={() => toggleActive(r)}
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
                          colSpan={6}
                          style={{
                            textAlign: "center",
                            color: "var(--muted)",
                            padding: 18,
                          }}
                        >
                          Chưa có section nào.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              {/* Footer phân trang */}
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
                  <div className="row" style={{ gap: 6, alignItems: "center" }}>
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
                      const activeP = pKey === page;
                      return (
                        <button
                          key={pKey}
                          className={`btn ${activeP ? "primary" : ""}`}
                          onClick={() => goto(pKey)}
                          disabled={activeP}
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

      {/* ===== MODAL CREATE/EDIT ===== */}
      {showModal && (
        <div className="modal-backdrop">
          <div
            className="modal"
            style={{
              width: "min(920px, 96vw)",
              background: "var(--card)",
              borderRadius: 16,
              padding: 16,
            }}
          >
            <div className="modal-topbar">
              <h3 style={{ margin: 0 }}>
                {editing ? "Sửa mô tả" : "Thêm mô tả"}
              </h3>
              <div className="row" style={{ gap: 8, alignItems: "center" }}>
                <label className="switch" title="Bật/Tắt hiển thị">
                  <input
                    type="checkbox"
                    checked={!!fActive}
                    onChange={(e) => setFActive(e.target.checked)}
                  />
                  <span className="slider" />
                </label>
                <span
                  className={fActive ? "badge green" : "badge gray"}
                  style={{ textTransform: "none", fontSize: 12 }}
                >
                  {fActive ? "Đang hiển thị" : "Đang ẩn"}
                </span>
              </div>
            </div>

            <form
              onSubmit={onSubmit}
              className="input-group"
              style={{ marginTop: 12 }}
            >
              {/* Hàng 1: Title - Type - Sort */}
              <div className="grid cols-3">
                <div className="group">
                  <span>
                    Tiêu đề{" "}
                    <span style={{ color: "#dc2626" }}>*</span>
                  </span>
                  <input
                    value={fTitle}
                    onChange={(e) => setFTitle(e.target.value)}
                    maxLength={SECTION_TITLE_MAX}
                    className={modalErrors.title ? "input-error" : ""}
                  />
                  {modalErrors.title && (
                    <div className="field-error">{modalErrors.title}</div>
                  )}
                </div>
                <div className="group">
                  <span>Loại</span>
                  <select
                    value={fType}
                    onChange={(e) => setFType(e.target.value)}
                  >
                    {TYPES.map((t) => (
                      <option key={t.value} value={t.value}>
                        {t.label}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="group">
                  <span>Thứ tự (sort)</span>
                  <input
                    type="number"
                    value={fSort}
                    min={0}
                    step={1}
                    onChange={(e) => setFSort(e.target.value)}
                    className={modalErrors.sortOrder ? "input-error" : ""}
                  />
                  {modalErrors.sortOrder && (
                    <div className="field-error">{modalErrors.sortOrder}</div>
                  )}
                </div>
              </div>

              {/* Content (Quill) */}
              <div className="group" style={{ marginTop: 10 }}>
                <span>
                  Nội dung{" "}
                  <span style={{ color: "#dc2626" }}>*</span>
                </span>
                <div
                  ref={editorWrapRef}
                  className={modalErrors.content ? "input-error" : ""}
                />
                <input
                  type="file"
                  accept="image/*"
                  ref={imageInputRef}
                  style={{ display: "none" }}
                  onChange={handleQuillImage}
                />
                {modalErrors.content && (
                  <div className="field-error">{modalErrors.content}</div>
                )}
              </div>

              <div
                className="row"
                style={{ marginTop: 12, justifyContent: "flex-end", gap: 8 }}
              >
                <button
                  type="button"
                  className="btn"
                  onClick={handleCloseModal}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="btn primary"
                  disabled={saving}
                >
                  {saving
                    ? "Đang lưu…"
                    : editing
                    ? "Lưu thay đổi"
                    : "Thêm"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Toast + Confirm */}
      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </div>
  );
}
