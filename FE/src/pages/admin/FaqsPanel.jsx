// src/pages/admin/components/FaqsPanel.jsx
import React from "react";
import { ProductFaqsApi } from "../../services/productFaqs";

const truncate = (s, n = 140) => (s && s.length > n ? s.slice(0, n) + "…" : (s ?? "—"));

export default function FaqsPanel({ productId }) {
  // Data + paging
  const [items, setItems] = React.useState([]);
  const [total, setTotal] = React.useState(0);
  const [page, setPage] = React.useState(1);
  const [size, setSize] = React.useState(10);
  const [loading, setLoading] = React.useState(false);
  const totalPages = Math.max(1, Math.ceil(total / size));

  // Query (đẩy xuống server)
  const [q, setQ] = React.useState("");
  const [active, setActive] = React.useState("");         // "", "true", "false"
  const [sort, setSort] = React.useState("sortOrder");    // question|sortOrder|active
  const [dir, setDir] = React.useState("asc");            // asc|desc

  // Modal
  const [showModal, setShowModal] = React.useState(false);
  const [editing, setEditing] = React.useState(null);

  // ===== Load =====
  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      const res = await ProductFaqsApi.listPaged(productId, {
        keyword: q || undefined,
        active: active === "" ? undefined : active === "true",
        sort,
        direction: dir,
        page,
        pageSize: size,
      });
      setItems(res?.items ?? []);
      setTotal(Number(res?.total ?? 0));
    } finally {
      setLoading(false);
    }
  }, [productId, q, active, sort, dir, page, size]);

  React.useEffect(() => { load(); }, [load]);
  React.useEffect(() => { setPage(1); }, [q, active, sort, dir, size]);

  // ===== Sort bằng tiêu đề cột =====
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
  const openCreate = () => { setEditing(null); setShowModal(true); };
  const openEdit   = (f) => { setEditing(f);   setShowModal(true); };

  const onSubmit = async (e) => {
    e.preventDefault();
    const form = new FormData(e.currentTarget);

    // LẤY TRẠNG THÁI TỪ SWITCH TRÊN TOPBAR
    const isActiveSwitch = document.getElementById("faqActiveSwitch");
    const isActiveValue = isActiveSwitch ? !!isActiveSwitch.checked : true;

    const dto = {
      question:  (form.get("question") || "").trim(),
      answer:    (form.get("answer") || "").trim(),
      sortOrder: Number(form.get("sortOrder") || 0) || 0,
      isActive:  isActiveValue,
    };
    if (!dto.question) return;

    if (editing?.faqId) await ProductFaqsApi.update(productId, editing.faqId, dto);
    else                await ProductFaqsApi.create(productId, dto);

    setShowModal(false);
    setPage(1);
    await load();
  };

  const onDelete = async (faqId) => {
    if (!window.confirm("Xoá câu hỏi này?")) return;
    await ProductFaqsApi.remove(productId, faqId);
    await load();
  };

  const toggleActive = async (f) => {
    await ProductFaqsApi.toggle(productId, f.faqId);
    await load();
  };

  // ===== Pager =====
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

  return (
    <div className="group" style={{ gridColumn: "1 / 3" }}>
      <div className="panel">
        <div className="panel-header" style={{ alignItems: "center" }}>
          <h4>
            Câu hỏi thường gặp (FAQ)
            <span style={{ fontSize: 12, color: "var(--muted)", marginLeft: 8 }}>({total})</span>
          </h4>

          {/* Toolbar: search + filter trạng thái + nút thêm */}
          <div className="variants-toolbar">
            <input
              className="ctl"
              placeholder="Tìm theo câu hỏi / đáp án…"
              value={q}
              onChange={(e) => setQ(e.target.value)}
            />
            <select className="ctl" value={active} onChange={(e)=>setActive(e.target.value)}>
              <option value="">Tất cả trạng thái</option>
              <option value="true">Đang hiển thị</option>
              <option value="false">Đang ẩn</option>
            </select>
            <button className="btn primary" onClick={openCreate}>+ Thêm FAQ</button>
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
                    <col style={{ width: "46%" }} /> {/* dồn rộng cho Trả lời */}
                    <col style={{ width: "10%" }} />
                    <col style={{ width: "14%" }} />
                  </colgroup>
                  <thead>
                    <tr>
                      <th onClick={() => headerSort("question")} style={{ cursor:"pointer" }}>
                        Câu hỏi{sortMark("question")}
                      </th>
                      <th>Trả lời</th>
                      <th onClick={() => headerSort("sortOrder")} style={{ cursor:"pointer" }}>
                        Thứ tự{sortMark("sortOrder")}
                      </th>
                      <th onClick={() => headerSort("active")} style={{ cursor:"pointer" }}>
                        Trạng thái{sortMark("active")}
                      </th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>

                  <tbody>
                    {items.map((f) => (
                      <tr key={f.faqId}>
                        <td><div style={{ fontWeight: 600 }}>{f.question}</div></td>
                        <td className="muted">{truncate(f.answer, 220)}</td>
                        <td className="mono">{f.sortOrder ?? 0}</td>
                        <td className="col-status">
                          <span className={`badge ${f.isActive ? "green" : "gray"}`} style={{ textTransform: "none" }}>
                            {f.isActive ? "Hiển thị" : "Ẩn"}
                          </span>
                        </td>
                        <td className="td-actions td-left">
                          <div className="row" style={{ gap: 8 }}>
                            <button className="action-btn edit-btn" title="Sửa" onClick={() => openEdit(f)}>
                              <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                                <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                                <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                              </svg>
                            </button>

                            <button className="action-btn delete-btn" title="Xoá" onClick={() => onDelete(f.faqId)}>
                              <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                                <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                              </svg>
                            </button>

                            <label className="switch" title="Bật/Tắt hiển thị">
                              <input
                                type="checkbox"
                                checked={Boolean(f.isActive)}
                                onChange={() => toggleActive(f)}
                              />
                              <span className="slider" />
                            </label>
                          </div>
                        </td>
                      </tr>
                    ))}

                    {items.length === 0 && (
                      <tr>
                        <td colSpan={5} style={{ textAlign: "center", color: "var(--muted)", padding: 18 }}>
                          Chưa có câu hỏi nào.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

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
                      const activeBtn = pKey === page;
                      return (
                        <button
                          key={pKey}
                          className={`btn ${activeBtn ? "primary" : ""}`}
                          onClick={() => goto(pKey)}
                          disabled={activeBtn}
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

      {/* Modal create/edit */}
      {showModal && (
        <div className="modal-backdrop">
          <div className="modal">
            <div className="modal-topbar">
              <h3 style={{ margin: 0 }}>{editing ? "Sửa FAQ" : "Thêm FAQ"}</h3>
              <div className="row" style={{ gap: 8, alignItems: "center" }}>
                <span className="muted" style={{ fontSize: 12 }}>Hiển thị</span>
                <label className="switch">
                  <input type="checkbox" id="faqActiveSwitch" defaultChecked={editing?.isActive ?? true} />
                  <span className="slider" />
                </label>
              </div>
            </div>

            <form onSubmit={onSubmit} className="input-group" style={{ marginTop: 12 }}>
              <div className="group">
                <span>Câu hỏi *</span>
                <input name="question" defaultValue={editing?.question ?? ""} required />
              </div>
              <div className="group">
                <span>Trả lời *</span>
                <textarea name="answer" rows={4} defaultValue={editing?.answer ?? ""} required />
              </div>
              <div className="grid cols-2" style={{ marginTop: 8 }}>
                <div className="group">
                  <span>Thứ tự</span>
                  <input type="number" min={0} step={1} name="sortOrder" defaultValue={editing?.sortOrder ?? 0} />
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
