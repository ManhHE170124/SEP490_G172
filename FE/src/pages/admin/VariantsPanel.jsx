// src/pages/admin/VariantsPanel.jsx
import React from "react";
import { useNavigate } from "react-router-dom";
import ProductVariantsApi from "../../services/productVariants";
import "./admin.css";

export default function VariantsPanel({ productId, productName, productCode }) {
  const nav = useNavigate();
  const detailPath = (v) => `/admin/products/${productId}/variants/${v.variantId}`;
  const goDetail = (v) => nav(detailPath(v));

  // Data + paging
  const [items, setItems] = React.useState([]);
  const [total, setTotal] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(1);
  const [loading, setLoading] = React.useState(false);

  // Query states (đẩy xuống server)
  const [q, setQ] = React.useState("");
  const [status, setStatus] = React.useState("");       // "", ACTIVE, INACTIVE, OUT_OF_STOCK
  const [dur, setDur] = React.useState("");             // "", "<=30", "31-180", ">180"
  const [sort, setSort] = React.useState("created");    // created|title|duration|stock|status|views
  const [dir, setDir] = React.useState("desc");         // asc|desc
  const [page, setPage] = React.useState(1);
  const [size, setSize] = React.useState(10);

  // Modal
  const [showModal, setShowModal] = React.useState(false);
  const [editing, setEditing] = React.useState(null);
  const fileInputRef = React.useRef(null);

  // Upload preview/state (tham khảo từ CreateEditPost)
  const [thumbPreview, setThumbPreview] = React.useState(null); // DataURL/URL để xem trước
  const [thumbUrl, setThumbUrl] = React.useState(null);         // URL sau khi upload (gửi lên BE)

  // Badge & label
  const statusBadgeClass = (s) =>
    String(s).toUpperCase() === "ACTIVE"
      ? "badge green"
      : String(s).toUpperCase() === "OUT_OF_STOCK"
      ? "badge red"
      : "badge gray";
const sanitizeThumbnail = (url, max = 255) => {
  if (!url) return null;
  const noQuery = url.split("?")[0];       // bỏ phần ?...
  return noQuery.length > max ? noQuery.slice(0, max) : noQuery;
};
  const statusLabel = (s) =>
    ({
      ACTIVE: "Hoạt động",
      INACTIVE: "Ẩn",
      OUT_OF_STOCK: "Hết hàng",
    }[String(s || "").toUpperCase()] || s);

  // Load từ server
  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      const res = await ProductVariantsApi.list(productId, { q, status, dur, sort, dir, page, pageSize: size });
      setItems(res.items || []);
      setTotal(res.totalItems || 0);
      setTotalPages(res.totalPages || 1);
    } finally {
      setLoading(false);
    }
  }, [productId, q, status, dur, sort, dir, page, size]);

  React.useEffect(() => { load(); }, [load]);

  // Reset về trang 1 khi đổi điều kiện
  React.useEffect(() => { setPage(1); }, [q, status, dur, sort, dir]);

  const openCreate = () => {
    setEditing(null);
    setThumbPreview(null);
    setThumbUrl(null);
    setShowModal(true);
  };

  // Sửa → điều hướng sang trang chi tiết (như yêu cầu cũ)
  const openEdit = (v) => goDetail(v);

  // == Upload helpers (tham khảo CreateEditPost, rút gọn) ==
  const urlToFile = async (url) => {
    const res = await fetch(url);
    const blob = await res.blob();
    return new File([blob], 'image.' + (blob.type.split('/')[1] || 'png'), { type: blob.type });
  };

  const handleLocalPreview = (file) => {
    const reader = new FileReader();
    reader.onload = (ev) => setThumbPreview(ev.target.result);
    reader.readAsDataURL(file);
  };

  const uploadThumbnailFile = async (file) => {
    // xem trước ngay
    handleLocalPreview(file);
    // upload lên server (Cloudinary qua controller)
    const up = await ProductVariantsApi.uploadImage(file);
    const imageUrl =
      up?.path || up?.Path || up?.url || up?.Url || (typeof up === "string" ? up : null);
    if (!imageUrl) throw new Error("Không lấy được URL ảnh sau khi upload.");
    setThumbUrl(imageUrl);
  };

  const onPickThumb = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      await uploadThumbnailFile(file);
    } catch (err) {
      alert(err?.message || "Upload ảnh thất bại.");
      setThumbPreview(null);
      setThumbUrl(null);
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
      try { await uploadThumbnailFile(dt.files[0]); }
      catch (err) { alert(err?.message || "Upload ảnh thất bại."); }
      return;
    }
    // Support paste URL vào drop
    const text = dt.getData("text/uri-list") || dt.getData("text/plain");
    if (text && /^https?:\/\//i.test(text)) {
      try {
        const f = await urlToFile(text);
        await uploadThumbnailFile(f);
      } catch {
        alert("Không thể tải ảnh từ URL này.");
      }
    }
  };

  const onPaste = async (e) => {
    const items = Array.from(e.clipboardData?.items || []);
    for (const it of items) {
      if (it.kind === "file" && it.type.startsWith("image/")) {
        const f = it.getAsFile();
        if (f) {
          try { await uploadThumbnailFile(f); } catch { alert("Upload ảnh thất bại."); }
          break;
        }
      } else if (it.kind === "string" && it.type === "text/plain") {
        it.getAsString(async (text) => {
          if (/^https?:\/\/.+\.(jpg|jpeg|png|gif|webp)$/i.test(text)) {
            try {
              const f = await urlToFile(text);
              await uploadThumbnailFile(f);
            } catch {
              alert("Không thể tải ảnh từ URL này.");
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

  const onSubmit = async (e) => {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    const dto = {
      variantCode:   form.get("variantCode")?.trim() || undefined,
      title:         form.get("title")?.trim(),
      durationDays:  form.get("durationDays") === "" ? null : Number(form.get("durationDays") || 0),
      warrantyDays:  form.get("warrantyDays") === "" ? null : Number(form.get("warrantyDays") || 0),
      status:        document.getElementById("variantStatusSwitch").checked ? "ACTIVE" : "INACTIVE",
 thumbnail:     sanitizeThumbnail(thumbUrl) || null
    };
    if (!dto.title) return;

    if (editing?.variantId) {
      await ProductVariantsApi.update(productId, editing.variantId, dto);
    } else {
      await ProductVariantsApi.create(productId, dto);
    }
    setShowModal(false);
    setPage(1);
    await load();
  };

  const onDelete = async (id) => {
    if (!window.confirm("Xóa biến thể này?")) return;
    await ProductVariantsApi.remove(productId, id);
    await load();
  };

  // === SORT: bấm tiêu đề cột để đổi (asc/desc) ===
  const headerSort = (key) => {
    setSort((cur) => {
      if (cur === key) {
        setDir((d) => (d === "asc" ? "desc" : "asc"));
        return cur;
      }
      setDir("asc"); // đổi cột -> mặc định asc
      return key;
    });
  };

  // helper hiển thị mũi tên sort
  const sortMark = (key) => (sort === key ? (dir === "asc" ? " ▲" : " ▼") : "");

  const toggleVariantStatus = async (v) => {
    try {
      if ((v.stockQty ?? 0) <= 0) return; // bảo vệ khi hết hàng
      const payload = await ProductVariantsApi.toggle(productId, v.variantId);
      const next = (payload?.Status || payload?.status || "").toUpperCase();
      setItems(prev => prev.map(x => x.variantId === v.variantId ? { ...x, status: next || x.status } : x));
    } catch (e) {
      console.error(e);
      await load();
    }
  };

  const goto = (p) => setPage(Math.min(Math.max(1, p), totalPages));

  const makePageList = React.useMemo(() => {
    const pages = [];
    const win = 2;
    const from = Math.max(1, page - win);
    const to   = Math.min(totalPages, page + win);
    if (from > 1) { pages.push(1); if (from > 2) pages.push("…l"); }
    for (let i = from; i <= to; i++) pages.push(i);
    if (to < totalPages) { if (to < totalPages - 1) pages.push("…r"); pages.push(totalPages); }
    return pages;
  }, [page, totalPages]);

  const startIdx = total === 0 ? 0 : (page - 1) * size + 1;
  const endIdx   = Math.min(total, page * size);

  return (
    <div className="group" style={{ gridColumn: "1 / 3" }}>
      <div className="panel">
        <div className="panel-header" style={{ alignItems: "center" }}>
          <h4>
            Biến thể thời gian{" "}
            <span style={{ fontSize: 12, color: "var(--muted)", marginLeft: 8 }}>
              ({total})
            </span>
          </h4>

          {/* Toolbar */}
          <div className="variants-toolbar">
            <input
              className="ctl"
              placeholder="Tìm theo tiêu đề / mã…"
              value={q}
              onChange={(e) => setQ(e.target.value)}
            />
            <select className="ctl" value={status} onChange={(e)=>setStatus(e.target.value)}>
              <option value="">Tất cả trạng thái</option>
              <option value="ACTIVE">Hoạt động</option>
              <option value="INACTIVE">Ẩn</option>
              <option value="OUT_OF_STOCK">Hết hàng</option>
            </select>
            <select className="ctl" value={dur} onChange={(e)=>setDur(e.target.value)}>
              <option value="">Thời lượng</option>
              <option value="<=30">≤ 30 ngày</option>
              <option value="31-180">31–180 ngày</option>
              <option value=">180">&gt; 180 ngày</option>
            </select>
            <button className="btn primary" onClick={openCreate}>+ Thêm biến thể</button>
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
                    <col style={{ width: "30%" }} />
                    <col style={{ width: "12%" }} />
                    <col style={{ width: "12%" }} />
                    <col style={{ width: "12%" }} />
                    <col style={{ width: "12%" }} />
                    <col style={{ width: "10%" }} />
                    <col style={{ width: "12%" }} />
                  </colgroup>
                  <thead>
                    <tr>
                      <th onClick={() => headerSort("title")} style={{ cursor: "pointer" }}>
                        Tên biến thể{sortMark("title")}
                      </th>
                      <th>Ảnh</th>
                      <th onClick={() => headerSort("duration")} style={{ cursor: "pointer" }}>
                        Thời lượng{sortMark("duration")}
                      </th>
                      <th onClick={() => headerSort("stock")} style={{ cursor: "pointer" }}>
                        Tồn kho{sortMark("stock")}
                      </th>
                      <th onClick={() => headerSort("status")} style={{ cursor: "pointer" }}>
                        Trạng thái{sortMark("status")}
                      </th>
                      <th onClick={() => headerSort("views")} style={{ cursor: "pointer" }}>
                        Lượt xem{sortMark("views")}
                      </th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>

                  <tbody>
                    {items.map((v) => (
                      <tr key={v.variantId}>
                        <td>
                          <div style={{ fontWeight: 600 }}>{v.title || "—"}</div>
                          <div className="muted" style={{ fontSize: 12 }}>
                            {v.variantCode || ""}
                          </div>
                        </td>

                        <td>
                          {v.thumbnail ? (
                            <img
                              src={v.thumbnail}
                              alt=""
                              style={{ width: 64, height: 44, objectFit: "cover", borderRadius: 6, border: "1px solid var(--line)" }}
                            />
                          ) : "—"}
                        </td>

                        <td>{v.durationDays ?? 0} ngày</td>
                        <td>{v.stockQty ?? 0}</td>

                        <td className="col-status">
                          <span className={statusBadgeClass(v.status)} style={{ textTransform: "none" }}>
                            {statusLabel(v.status)}
                          </span>
                        </td>

                        <td className="mono">{v.viewCount ?? 0}</td>

                        <td className="td-actions td-left">
                          <div className="row" style={{ gap: 8 }}>
                            <button className="action-btn edit-btn" title="Xem chi tiết" onClick={() => goDetail(v)}>
                              <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                                <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                                <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                              </svg>
                            </button>

                            <button className="action-btn delete-btn" title="Xoá" onClick={() => onDelete(v.variantId)}>
                              <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                                <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                              </svg>
                            </button>

                            <label
                              className="switch"
                              title={(v.stockQty ?? 0) <= 0 ? "Hết hàng – không thể bật" : "Bật/Tắt hiển thị"}
                            >
                              <input
                                type="checkbox"
                                disabled={(v.stockQty ?? 0) <= 0}
                                checked={String(v.status).toUpperCase() === "ACTIVE"}
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
                        <td colSpan={7} style={{ textAlign: "center", color: "var(--muted)", padding: 18 }}>
                          Chưa có biến thể nào.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              {/* Footer phân trang */}
              <div className="variants-footer" style={{ gap: 12, display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap" }}>
                <div className="muted">Hiển thị {startIdx}-{endIdx} / {total}</div>

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

      {/* ===== MODAL CREATE ===== */}
      {showModal && (
        <div className="modal-backdrop">
          <div className="modal" onPaste={onPaste} onDrop={onDrop} onDragOver={(e) => { e.preventDefault(); e.stopPropagation(); }}>
            <div className="modal-topbar">
              <h3 style={{ margin: 0 }}>Thêm biến thể</h3>
              <div className="row" style={{ gap: 8, alignItems: "center" }}>
                <span className="muted" style={{ fontSize: 12 }}>Trạng thái</span>
                <label className="switch" title="Bật/Tắt hiển thị">
                  <input type="checkbox" id="variantStatusSwitch" defaultChecked />
                  <span className="slider" />
                </label>
              </div>
            </div>

            <form onSubmit={onSubmit} className="input-group" style={{ marginTop: 12 }}>
              <div className="grid cols-2">
                <div className="group">
                  <span>Tên biến thể *</span>
                  <input name="title" defaultValue={productName ?? ""} required />
                </div>
                <div className="group">
                  <span>Mã biến thể</span>
                  <input name="variantCode" defaultValue={productCode ?? ""} />
                </div>
              </div>

              <div className="grid cols-2" style={{ marginTop: 8 }}>
                <div className="group">
                  <span>Thời lượng (ngày)</span>
                  <input type="number" min={0} step={1} name="durationDays" defaultValue={0} />
                </div>
                <div className="group">
                  <span>Bảo hành (ngày)</span>
                  <input type="number" min={0} step={1} name="warrantyDays" defaultValue={0} />
                </div>
              </div>

              {/* Upload thumbnail (tham khảo CreateEditPost) */}
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
                  className={`cep-featured-image-upload ${thumbPreview ? "has-image" : ""}`}
                  onClick={() => fileInputRef.current?.click()}
                  tabIndex={0}
                  role="button"
                  style={{ outline: "none", border: "1px dashed var(--line)", borderRadius: 10, padding: 12, textAlign: "center", background: "#fafafa" }}
                >
                  {thumbPreview ? (
                    <img
                      src={thumbPreview}
                      alt="thumbnail"
                      style={{ width: "100%", maxHeight: 220, objectFit: "contain", borderRadius: 8 }}
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
                  <button type="button" className="btn" style={{ marginTop: 8 }} onClick={clearThumb}>
                    Xoá ảnh
                  </button>
                )}
              </div>

              <div className="row" style={{ marginTop: 12, justifyContent: "flex-end", gap: 8 }}>
                <button type="button" className="btn" onClick={() => setShowModal(false)}>Hủy</button>
                <button type="submit" className="btn primary">Thêm</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
