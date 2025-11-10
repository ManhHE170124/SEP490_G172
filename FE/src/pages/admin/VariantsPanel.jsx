import React from "react";
import { ProductVariantsApi } from "../../services/productVariants";

const fmtMoney = (n) =>
  typeof n === "number"
    ? n.toLocaleString("vi-VN", { style: "currency", currency: "VND", maximumFractionDigits: 0 })
    : "-";

export default function VariantsPanel({ productId, productName, productCode }) {
  // Data + paging
  const [items, setItems] = React.useState([]);
  const [total, setTotal] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(1);
  const [loading, setLoading] = React.useState(false);

  // Query states (đẩy xuống server)
  const [q, setQ] = React.useState("");
  const [status, setStatus] = React.useState("");       // "", ACTIVE, INACTIVE, OUT_OF_STOCK
  const [dur, setDur] = React.useState("");             // "", "<=30", "31-180", ">180"
  const [sort, setSort] = React.useState("created");    // created|title|duration|price|stock|status
  const [dir, setDir] = React.useState("desc");         // asc|desc
  const [page, setPage] = React.useState(1);
  const [size, setSize] = React.useState(10);

  // Modal
  const [showModal, setShowModal] = React.useState(false);
  const [editing, setEditing] = React.useState(null);

  // Badge & label
  const statusBadgeClass = (s) =>
    String(s).toUpperCase() === "ACTIVE"
      ? "badge green"
      : String(s).toUpperCase() === "OUT_OF_STOCK"
      ? "badge status-oo-stock"
      : "badge status-inactive";

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
    setTotalPages(res.totalPages || 1);   // <-- dùng totalPages từ BE
  } finally {
    setLoading(false);
  }
}, [productId, q, status, dur, sort, dir, page, size]);


  React.useEffect(() => { load(); }, [load]);

  // Reset về trang 1 khi đổi điều kiện
  React.useEffect(() => { setPage(1); }, [q, status, dur, sort, dir]);

  const openCreate = () => { setEditing(null); setShowModal(true); };
  const openEdit   = (v) => { setEditing(v);   setShowModal(true); };

  const onSubmit = async (e) => {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    const dto = {
      variantCode:   form.get("variantCode")?.trim() || undefined,
      title:         form.get("title")?.trim(),
      durationDays:  Number(form.get("durationDays") || 0) || 0,
      originalPrice: form.get("originalPrice") ? Number(form.get("originalPrice")) : null,
      price:         Number(form.get("price") || 0) || 0,
      warrantyDays:  form.get("warrantyDays") ? Number(form.get("warrantyDays")) : 0,
      status:        document.getElementById("variantStatusSwitch").checked ? "ACTIVE" : "INACTIVE",
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

  // Toggle theo endpoint /toggle
  // ... giữ nguyên các import & state như bạn đã có

// === SORT: bấm tiêu đề cột để đổi (asc/desc) ===
const headerSort = (key) => {
  setSort((cur) => {
    if (cur === key) {
      setDir((d) => (d === "asc" ? "desc" : "asc"));
      return cur;
    }
    // đổi cột -> mặc định asc
    setDir("asc");
    return key;
  });
};

// helper hiển thị mũi tên sort
const sortMark = (key) => (sort === key ? (dir === "asc" ? " ▲" : " ▼") : "");

// === Toggle status dùng PATCH /toggle ===
const toggleVariantStatus = async (v) => {
  try {
    if ((v.stockQty ?? 0) <= 0) return; // vẫn bảo vệ trường hợp hết hàng

    const payload = await ProductVariantsApi.toggle(productId, v.variantId);
    const nextStatusRaw = payload?.status ?? payload?.Status;
    const nextStatus = typeof nextStatusRaw === "string"
      ? nextStatusRaw.toUpperCase()
      : (v.status === "ACTIVE" ? "INACTIVE" : "ACTIVE");

    setItems(prev =>
      prev.map(it =>
        it.variantId === v.variantId ? { ...it, status: nextStatus } : it
      )
    );
  } catch (e) {
    console.error(e);
    await load(); // đồng bộ lại nếu có lỗi
  }
};


const goto = (p) => setPage(Math.min(Math.max(1, p), totalPages));

const makePageList = React.useMemo(() => {
  const pages = [];
  const win = 2; // window hai bên trang hiện tại
  const from = Math.max(1, page - win);
  const to   = Math.min(totalPages, page + win);

  if (from > 1) {
    pages.push(1);
    if (from > 2) pages.push("ellipsis-left");
  }
  for (let i = from; i <= to; i++) pages.push(i);
  if (to < totalPages) {
    if (to < totalPages - 1) pages.push("ellipsis-right");
    pages.push(totalPages);
  }
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

          {/* Toolbar: đẩy điều kiện xuống server */}
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
                    <col style={{ width: "28%" }} />
                    <col style={{ width: "12%" }} />
                    <col style={{ width: "14%" }} />
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
                            <th onClick={() => headerSort("duration")} style={{ cursor: "pointer" }}>
                            Thời lượng{sortMark("duration")}
                            </th>
                            <th onClick={() => headerSort("price")} style={{ cursor: "pointer" }}>
                            Giá bán{sortMark("price")}
                            </th>
                            <th onClick={() => headerSort("originalPrice")} style={{ cursor: "pointer" }}>
                            Giá gốc{sortMark("originalPrice")}
                            </th>
                            <th onClick={() => headerSort("stock")} style={{ cursor: "pointer" }}>
                            Tồn kho{sortMark("stock")}
                            </th>
                            <th onClick={() => headerSort("status")} style={{ cursor: "pointer" }}>
                            Trạng thái{sortMark("status")}
                            </th>
                            <th>Thao tác</th>
                        </tr>
                </thead>

                  <tbody>
                    {items.map((v) => (
                      <tr key={v.variantId}>
                        <td>
                          <div style={{ fontWeight: 600 }}>{v.title}</div>
                          <div className="muted" style={{ fontSize: 12 }}>
                            {v.variantCode || "—"}
                          </div>
                        </td>
                        {/* Căn trái để khớp tiêu đề cột */}
                        <td>{(v.durationDays ?? 0)} ngày</td>
                        <td>{fmtMoney(v.price)}</td>
                        <td>{v.originalPrice != null ? fmtMoney(v.originalPrice) : "—"}</td>
                        <td>{v.stockQty ?? 0}</td>
                        <td className="col-status">
                          <span className={statusBadgeClass(v.status)} style={{ textTransform: "none" }}>
                            {statusLabel(v.status)}
                          </span>
                        </td>
                        <td className="td-actions td-left">
                          <div className="row" style={{ gap: 8 }}>
                            <button className="action-btn edit-btn" title="Sửa" onClick={() => openEdit(v)}>
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
    disabled={(v.stockQty ?? 0) <= 0}               // chỉ chặn khi hết hàng
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

              {/* Footer phân trang giống trang sản phẩm */}
            <div className="variants-footer" style={{ gap: 12, display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap" }}>
                    <div className="muted">
                      Hiển thị {startIdx}-{endIdx} / {total}
                    </div>

                <div className="row" style={{ gap: 8, alignItems: "center", flexWrap: "wrap" }}>
                    {/* Page size */}
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

                    {/* Nav buttons */}
                    <div className="row" style={{ gap: 6 }}>
                    <button className="btn" disabled={page <= 1} onClick={() => goto(1)} title="Trang đầu">«</button>
                    <button className="btn" disabled={page <= 1} onClick={() => goto(page - 1)} title="Trang trước">←</button>

                    {/* Page numbers với ellipsis */}
                    {makePageList.map((pKey, idx) => {
                        if (typeof pKey !== "number") {
                        return <span key={pKey + idx} className="muted">…</span>;
                        }
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
          <div className="modal">
            {/* Topbar: Tiêu đề + công tắc trạng thái ở góc trên phải */}
            <div className="modal-topbar">
              <h3 style={{ margin: 0 }}>{editing ? "Sửa biến thể" : "Thêm biến thể"}</h3>
              <div className="row" style={{ gap: 8, alignItems: "center" }}>
                <span className="muted" style={{ fontSize: 12 }}>Trạng thái</span>
                <label className="switch" title="Bật/Tắt hiển thị">
                  <input
                    type="checkbox"
                    id="variantStatusSwitch"
                    defaultChecked={String(editing?.status || "ACTIVE").toUpperCase() === "ACTIVE"}
                  />
                  <span className="slider" />
                </label>
              </div>
            </div>

            <form onSubmit={onSubmit} className="input-group" style={{ marginTop: 12 }}>
              {/* Hàng 1: Tên + Mã */}
              <div className="grid cols-2">
                <div className="group">
                  <span>Tên biến thể *</span>
                  <input name="title" defaultValue={editing?.title ?? productName ?? ""} required />
                </div>
                <div className="group">
                  <span>Mã biến thể</span>
                  <input name="variantCode" defaultValue={editing?.variantCode ?? productCode ?? ""} />
                </div>
              </div>

              {/* Hàng 2: Thời lượng + Bảo hành */}
              <div className="grid cols-2" style={{ marginTop: 8 }}>
                <div className="group">
                  <span>Thời lượng (ngày)</span>
                  <input type="number" min={0} step={1} name="durationDays" defaultValue={editing?.durationDays ?? 0} />
                </div>
                <div className="group">
                  <span>Bảo hành (ngày)</span>
                  <input type="number" min={0} step={1} name="warrantyDays" defaultValue={editing?.warrantyDays ?? 0} />
                </div>
              </div>

              {/* Hàng 3: Giá bán + Giá gốc */}
              <div className="grid cols-2" style={{ marginTop: 8 }}>
                <div className="group">
                  <span>Giá bán *</span>
                  <input type="number" min={0} step="1000" name="price" defaultValue={editing?.price ?? 0} required />
                </div>
                <div className="group">
                  <span>Giá gốc</span>
                  <input type="number" min={0} step="1000" name="originalPrice" defaultValue={editing?.originalPrice ?? ""} />
                </div>
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
