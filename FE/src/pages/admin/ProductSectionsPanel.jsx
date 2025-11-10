// src/pages/admin/components/ProductSectionsPanel.jsx
import React from "react";
import Quill from "quill";
import "quill/dist/quill.snow.css";

import { ProductSectionsApi } from "../../services/productSections";
import { postsApi, extractPublicId } from "../../services/postsApi";

const TYPES = [
  { value: "DETAIL",   label: "Chi tiết" },
  { value: "WARRANTY", label: "Bảo hành" },
  { value: "NOTE",     label: "Lưu ý" },
];

const typeLabel = (v) => TYPES.find(t => t.value === String(v).toUpperCase())?.label || v || "—";
const fmtDT = (s) => (s ? new Date(s).toLocaleString("vi-VN", { hour12: false }) : "—");
const snippet = (html, n = 120) => {
  const t = String(html || "").replace(/<[^>]+>/g, "").replace(/\s+/g, " ").trim();
  return t.length > n ? t.slice(0, n) + "…" : t || "—";
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
  const [type, setType] = React.useState("");     // "", DETAIL|WARRANTY|NOTE
  const [active, setActive] = React.useState(""); // "", true|false
  const [sort, setSort] = React.useState("sort"); // sort|title|type|active|updated|created
  const [dir, setDir] = React.useState("asc");    // asc|desc

  // Modal
  const [showModal, setShowModal] = React.useState(false);
  const [editing, setEditing] = React.useState(null);

  // Form state in modal
  const [fTitle, setFTitle] = React.useState("");
  const [fType, setFType]   = React.useState("DETAIL");
  const [fActive, setFActive] = React.useState(true);
  const [fSort, setFSort] = React.useState(0);
  const [fContent, setFContent] = React.useState("");

  // Quill
  const quillRef = React.useRef(null);
  const editorWrapRef = React.useRef(null);
  const imageInputRef = React.useRef(null);

  // ===== Load list =====
  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      const res = await ProductSectionsApi.listPaged(productId, variantId, {
        q, type, active, sort, dir, page, pageSize: size
      });
      setItems(res.items || []);
      setTotal(res.totalItems || 0);
      setTotalPages(res.totalPages || 1);
    } finally {
      setLoading(false);
    }
  }, [productId, variantId, q, type, active, sort, dir, page, size]);

  React.useEffect(() => { load(); }, [load]);
  React.useEffect(() => { setPage(1); }, [q, type, active, sort, dir, size]);

  // ===== Sort header =====
  const headerSort = (key) => {
    setSort((cur) => {
      if (cur === key) { setDir((d) => (d === "asc" ? "desc" : "asc")); return cur; }
      setDir("asc"); return key;
    });
  };
  const sortMark = (key) => (sort === key ? (dir === "asc" ? " ▲" : " ▼") : "");

  // ===== CRUD =====
  const openCreate = () => {
    setEditing(null);
    setFTitle("");
    setFType("DETAIL");
    setFActive(true);
    setFSort(0);
    setFContent("");
    setShowModal(true);
  };

  const openEdit = (row) => {
    setEditing(row);
    setFTitle(row.title || "");
    setFType((row.type || "DETAIL").toUpperCase());
    setFActive(Boolean(row.active ?? true));
    setFSort(Number(row.sort ?? 0));
    setFContent(row.content || "");
    setShowModal(true);
  };

  const onDelete = async (id) => {
    if (!window.confirm("Xoá mục này?")) return;
    await ProductSectionsApi.remove(productId, variantId, id);
    await load();
  };

  const toggleActive = async (row) => {
    await ProductSectionsApi.toggle(productId, variantId, row.sectionId);
    await load();
  };

  const onSubmit = async (e) => {
    e.preventDefault();
    const dto = {
      title: fTitle?.trim(),
      type: fType,
      content: quillRef.current ? quillRef.current.root.innerHTML : (fContent || "<p></p>"),
      active: !!fActive,
      sort: Number.isFinite(Number(fSort)) ? Number(fSort) : 0
    };
    if (!dto.title) return;

    if (editing?.sectionId) {
      await ProductSectionsApi.update(productId, variantId, editing.sectionId, dto);
    } else {
      await ProductSectionsApi.create(productId, variantId, dto);
    }
    setShowModal(false);
    setPage(1);
    await load();
  };

  // ===== Paging helpers =====
  const goto = (p) => setPage(Math.min(Math.max(1, p), totalPages));
  const makePageList = React.useMemo(() => {
    const pages = [];
    const win = 2;
    const from = Math.max(1, page - win);
    const to   = Math.min(totalPages, page + win);
    if (from > 1) { pages.push(1); if (from > 2) pages.push("…L"); }
    for (let i = from; i <= to; i++) pages.push(i);
    if (to < totalPages) { if (to < totalPages - 1) pages.push("…R"); pages.push(totalPages); }
    return pages;
  }, [page, totalPages]);
  const startIdx = total === 0 ? 0 : (page - 1) * size + 1;
  const endIdx   = Math.min(total, page * size);

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
            image: function () { imageInputRef.current && imageInputRef.current.click(); }
          }
        }
      }
    });

    if (fContent) q.clipboard.dangerouslyPasteHTML(fContent);
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
      alert("Tải ảnh thất bại. Vui lòng thử lại.");
    } finally {
      e.target.value = "";
    }
  };

  return (
    <div className="group" style={{ gridColumn: "1 / 3" }}>
      <div className="panel">
        <div className="panel-header" style={{ alignItems: "center" }}>
          <h4>
            Thông tin sản phẩm / Sections{" "}
            <span style={{ fontSize: 12, color: "var(--muted)", marginLeft: 8 }}>
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
            <select className="ctl" value={type} onChange={(e)=>setType(e.target.value)}>
              <option value="">Tất cả loại</option>
              {TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
            </select>
            <select className="ctl" value={active} onChange={(e)=>setActive(e.target.value)}>
              <option value="">Tất cả trạng thái</option>
              <option value="true">Đang hiển thị</option>
              <option value="false">Đang ẩn</option>
            </select>
            <button className="btn primary" onClick={openCreate}>+ Thêm section</button>
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
                    <col style={{ width: "12%" }} />
                    <col style={{ width: "30%" }} />
                    <col style={{ width: "8%" }} />
                    <col style={{ width: "12%" }} />
                    <col style={{ width: "12%" }} />
                  </colgroup>
                  <thead>
                    <tr>
                      <th onClick={() => headerSort("title")} style={{ cursor: "pointer" }}>
                        Tiêu đề{sortMark("title")}
                      </th>
                      <th onClick={() => headerSort("type")} style={{ cursor: "pointer" }}>
                        Loại{sortMark("type")}
                      </th>
                      <th>Nội dung</th>
                      <th onClick={() => headerSort("sort")} style={{ cursor: "pointer" }}>
                        Thứ tự{sortMark("sort")}
                      </th>
                      <th onClick={() => headerSort("updated")} style={{ cursor: "pointer" }}>
                        Cập nhật{sortMark("updated")}
                      </th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((r) => (
                      <tr key={r.sectionId}>
                        <td>
                          <div style={{ fontWeight: 600 }}>{r.title || "—"}</div>
                          <div className="muted" style={{ fontSize: 12 }}>
                            {String(r.active ?? true) === "true" || r.active
                              ? <span className="badge green">Hiển thị</span>
                              : <span className="badge gray">Ẩn</span>}
                          </div>
                        </td>
                        <td>{typeLabel(r.type)}</td>
                        <td title={snippet(r.content, 300)}>{snippet(r.content, 80)}</td>
                        <td className="td-right">{Number(r.sort ?? 0)}</td>
                        <td>{fmtDT(r.updatedAt || r.modifiedAt || r.createdAt)}</td>
                        <td className="td-actions td-left">
                          <div className="row" style={{ gap: 8 }}>
                            <button className="action-btn edit-btn" title="Sửa" onClick={() => openEdit(r)}>
                              <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                                <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                                <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                              </svg>
                            </button>

                            <button className="action-btn delete-btn" title="Xoá" onClick={() => onDelete(r.sectionId)}>
                              <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                                <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                              </svg>
                            </button>

                            <label className="switch" title="Bật/Tắt hiển thị">
                              <input
                                type="checkbox"
                                checked={!!(r.active ?? true)}
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
                        <td colSpan={6} style={{ textAlign: "center", color: "var(--muted)", padding: 18 }}>
                          Chưa có section nào.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              {/* Footer phân trang */}
              <div className="variants-footer" style={{ gap: 12, display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap" }}>
                <div className="muted">
                  Hiển thị {startIdx}-{endIdx} / {total}
                </div>

                <div className="row" style={{ gap: 8, alignItems: "center", flexWrap: "wrap" }}>
                  <div className="row" style={{ gap: 6, alignItems: "center" }}>
                    <span className="muted" style={{ fontSize: 12 }}>Dòng/trang</span>
                    <select
                      className="ctl"
                      value={size}
                      onChange={(e) => { setSize(Number(e.target.value)); setPage(1); }}
                    >
                      <option value={5}>5</option>
                      <option value={10}>10</option>
                      <option value={20}>20</option>
                      <option value={50}>50</option>
                    </select>
                  </div>

                  <div className="row" style={{ gap: 6 }}>
                    <button className="btn" disabled={page <= 1} onClick={() => goto(1)} title="Trang đầu">«</button>
                    <button className="btn" disabled={page <= 1} onClick={() => goto(page - 1)} title="Trang trước">←</button>

                    {makePageList.map((pKey, idx) => {
                      if (typeof pKey !== "number") return <span key={pKey + idx} className="muted">…</span>;
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

                    <button className="btn" disabled={page >= totalPages} onClick={() => goto(page + 1)} title="Trang sau">→</button>
                    <button className="btn" disabled={page >= totalPages} onClick={() => goto(totalPages)} title="Trang cuối">»</button>
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
          <div className="modal" style={{ width: "min(920px, 96vw)", background: "var(--card)", borderRadius: 16, padding: 16 }}>
            <div className="modal-topbar">
              <h3 style={{ margin: 0 }}>{editing ? "Sửa section" : "Thêm section"}</h3>
              <div className="row" style={{ gap: 12, alignItems: "center" }}>
                <div className="row" style={{ gap: 8, alignItems: "center" }}>
                  <span className="muted" style={{ fontSize: 12 }}>Hiển thị</span>
                  <label className="switch" title="Bật/Tắt hiển thị">
                    <input
                      type="checkbox"
                      checked={!!fActive}
                      onChange={(e) => setFActive(e.target.checked)}
                    />
                    <span className="slider" />
                  </label>
                </div>
              </div>
            </div>

            <form onSubmit={onSubmit} className="input-group" style={{ marginTop: 12 }}>
              {/* Hàng 1: Title - Type - Sort */}
              <div className="grid cols-3">
                <div className="group">
                  <span>Tiêu đề *</span>
                  <input value={fTitle} onChange={(e) => setFTitle(e.target.value)} required />
                </div>
                <div className="group">
                  <span>Loại *</span>
                  <select value={fType} onChange={(e) => setFType(e.target.value)}>
                    {TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
                  </select>
                </div>
                <div className="group">
                  <span>Thứ tự (sort)</span>
                  <input type="number" value={fSort} onChange={(e) => setFSort(e.target.value)} />
                </div>
              </div>

              {/* Content (Quill) */}
              <div className="group" style={{ marginTop: 10 }}>
                <span>Nội dung</span>
                <div ref={editorWrapRef} />
                <input
                  type="file"
                  accept="image/*"
                  ref={imageInputRef}
                  style={{ display: "none" }}
                  onChange={handleQuillImage}
                />
              </div>

              <div className="row" style={{ marginTop: 12, justifyContent: "flex-end", gap: 8 }}>
                <button type="button" className="btn" onClick={() => setShowModal(false)}>Hủy</button>
                <button type="submit" className="btn primary">{editing ? "Lưu" : "Thêm"}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
