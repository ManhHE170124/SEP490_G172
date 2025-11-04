import React from "react";
import { Link } from "react-router-dom";
import ProductApi from "../../services/products";
import { CategoryApi } from "../../services/categories";
import { BadgesApi } from "../../services/badges";
import "./admin.css";

export default function ProductsPage() {
  const [query, setQuery] = React.useState({
    keyword: "",
    categoryId: "",
    type: "",
    status: "",
    badge: "",          // NEW: lọc theo nhãn
    sort: "name",
    direction: "asc",
  });
  const [page, setPage] = React.useState(1);
  const [pageSize] = React.useState(10); // cố định 10
  const [total, setTotal] = React.useState(0);
  const [loading, setLoading] = React.useState(false);
  const [items, setItems] = React.useState([]);

  const [categories, setCategories] = React.useState([]);
  const [categoriesDict, setCategoriesDict] = React.useState({});
  const [badges, setBadges] = React.useState([]);      // NEW: danh sách nhãn để render select
  const [badgesDict, setBadgesDict] = React.useState({});

  React.useEffect(() => {
    // Danh mục
    CategoryApi.listPaged({ active: true, page: 1, pageSize: 1000 }).then((res) => {
      const list = res?.items ?? [];
      setCategories(list);
      const dict = {};
      for (const c of list) dict[c.categoryId] = c.categoryName || `#${c.categoryId}`;
      setCategoriesDict(dict);
    });

    // Nhãn
    BadgesApi.listPaged({ active: true, page: 1, pageSize: 1000 }).then((res) => {
      const items = res?.items ?? [];
      setBadges(items); // NEW
      const dict = {};
      for (const b of items) {
        dict[b.badgeCode] = { name: b.displayName || b.badgeCode, color: b.colorHex || "#1e40af" };
      }
      setBadgesDict(dict);
    });
  }, []);

  const load = React.useCallback(async () => {
    setLoading(true);
    const params = {
      keyword: query.keyword || undefined,
      categoryId: query.categoryId || undefined,
      type: query.type || undefined,
      status: query.status || undefined,
      badge: query.badge || undefined,    // NEW: gửi badgeCode lên BE qua ?badge=
      sort: query.sort || "name",
      direction: query.direction || "asc",
      page,
      pageSize,
    };
    try {
      const res = await ProductApi.list(params);
      const arr = res?.items ?? res ?? [];
      setItems(arr);
      setTotal(typeof res?.total === "number" ? res.total : arr.length);
    } finally {
      setLoading(false);
    }
  }, [query, page, pageSize]);

  React.useEffect(() => {
    const t = setTimeout(load, 250);
    return () => clearTimeout(t);
  }, [load]);

  React.useEffect(() => {
    setPage(1);
  }, [
    query.keyword,
    query.categoryId,
    query.type,
    query.status,
    query.badge,         // NEW: reset trang khi đổi nhãn
    query.sort,
    query.direction,
  ]);

  const TYPES    = ProductApi?.types ?? [];
  const STATUSES = ProductApi?.statuses ?? [];

  const fmtType   = (t) => ProductApi.typeLabelOf?.(t) || t;
const fmtStatus = (s) => ProductApi.statusLabelOf?.(s) || s;
  const statusBadge = (s) =>
    s === "ACTIVE" ? "badge green" : s === "OUT_OF_STOCK" ? "badge red" : "badge gray";

  const headerSort = (key) => {
    setQuery((q) => ({
      ...q,
      sort: key,
      direction: q.sort === key && q.direction === "asc" ? "desc" : "asc",
    }));
  };

  const toggleStatus = async (id) => {
    try {
      await ProductApi.toggle(id);
      await load();
    } catch (e) {
      alert(e?.response?.data?.message || e.message || "Đổi trạng thái thất bại");
    }
  };

  const deleteProduct = async (p) => {
    const ok = window.confirm(`Xoá sản phẩm "${p.productName}"? Hành động này không thể hoàn tác!`);
    if (!ok) return;
    try {
      await ProductApi.remove(p.productId);
      await load();
    } catch (e) {
      alert(e?.response?.data?.message || e.message || "Xoá thất bại");
    }
  };

  return (
    <div className="page">
      <div className="card">
        <div className="row" style={{ justifyContent: "space-between", alignItems: "center" }}>
          <h2>Danh sách sản phẩm</h2>
          <Link className="btn primary" to="/admin/products/add">+ Thêm sản phẩm</Link>
        </div>

        {/* Filters */}
        <div className="filter-inline input-group" style={{ marginTop: 12 }}>
          <div className="group">
            <span>Tìm kiếm</span>
            <input
              value={query.keyword}
              onChange={(e) => setQuery((s) => ({ ...s, keyword: e.target.value }))}
              placeholder="Tìm theo tên hoặc mã…"
            />
          </div>

          <div className="group w-180">
            <span>Danh mục sản phẩm</span> {/* VI: cập nhật label */}
            <select
              value={query.categoryId}
              onChange={(e) => setQuery((s) => ({ ...s, categoryId: e.target.value }))}
            >
              <option value="">Tất cả</option>
              {categories.map((c) => (
                <option key={c.categoryId} value={c.categoryId}>
                  {c.categoryName}
                </option>
              ))}
            </select>
          </div>

          <div className="group w-180">
            <span>Loại sản phẩm</span>
            <select
              value={query.type}
              onChange={(e) => setQuery((s) => ({ ...s, type: e.target.value }))}
            >
              <option value="">Tất cả</option>
              {TYPES.map((t) => (
                <option key={t.value} value={t.value}>{t.label}</option>
              ))}
            </select>
          </div>

          <div className="group w-160">
            <span>Trạng thái</span>
            <select
              value={query.status}
              onChange={(e) => setQuery((s) => ({ ...s, status: e.target.value }))}
            >
              <option value="">Tất cả</option>
              {STATUSES.map((s) => (
                <option key={s.value} value={s.value}>{s.label}</option>
              ))}
            </select>
          </div>

          {/* NEW: Nhãn sản phẩm (hiển thị theo tên hiển thị) */}
          <div className="group w-180">
            <span>Nhãn sản phẩm</span>
            <select
              value={query.badge}
              onChange={(e) => setQuery((s) => ({ ...s, badge: e.target.value }))}
            >
              <option value="">Tất cả</option>
              {badges.map((b) => (
                <option key={b.badgeCode} value={b.badgeCode}>
                  {b.displayName || b.badgeCode}
                </option>
              ))}
            </select>
          </div>

          {loading && <span className="badge gray">Đang tải…</span>}
          <button
            className="btn"
            onClick={() =>
              setQuery({
                keyword: "",
                categoryId: "",
                type: "",
                status: "",
                badge: "",      // reset nhãn
                sort: "name",
                direction: "asc",
              })
            }
            title="Xoá bộ lọc"
          >
            Đặt lại
          </button>
        </div>

        {/* Table */}
        <table className="table" style={{ marginTop: 10 }}>
          <thead>
            <tr>
              <th onClick={() => headerSort("name")} style={{ cursor:"pointer" }}>
                Tên {query.sort==="name" ? (query.direction==="asc"?" ▲":" ▼") : ""}
              </th>
              <th onClick={() => headerSort("type")} style={{ cursor:"pointer" }}>
                Loại {query.sort==="type" ? (query.direction==="asc"?" ▲":" ▼") : ""}
              </th>
              <th onClick={() => headerSort("stock")} style={{ cursor:"pointer" }}>
                Tồn kho (tổng) {query.sort==="stock" ? (query.direction==="asc"?" ▲":" ▼") : ""}
              </th>
              <th>Danh mục</th>
              <th>Nhãn</th>
            <th className="col-status" onClick={() => headerSort("status")} style={{ cursor: "pointer" }}>
  Trạng thái {query.sort==="status" ? (query.direction==="asc"?" ▲":" ▼") : ""}
</th>

              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {(items ?? []).map((p) => (
              <tr key={p.productId}>
                <td>{p.productName}</td>
                <td>{fmtType(p.productType)}</td>
                <td className="mono">{p.totalStockQty ?? 0}</td>

                {/* Danh mục: tên + dấu phẩy */}
                <td style={{ maxWidth: 360 }}>
                  {(p.categoryIds ?? []).length === 0 ? "—" : (
                    (p.categoryIds ?? []).map((cid, idx, arr) => {
                      const name = categoriesDict[cid] ?? `#${cid}`;
                      return (
                        <React.Fragment key={cid}>
                          <span className="chip">{name}</span>
                          {idx < arr.length - 1 ? <span>,&nbsp;</span> : null}
                        </React.Fragment>
                      );
                    })
                  )}
                </td>

                {/* Nhãn (chips màu) */}
                <td style={{ maxWidth: 360 }}>
                  {(p.badgeCodes ?? []).map((code) => {
                    const meta = badgesDict[code] || { name: code, color: "#6b7280" };
                    return (
                      <span
                        key={code}
                        className="label-chip"
                        style={{ background: meta.color, color:"#fff", marginRight:6, marginBottom:4 }}
                        title={meta.name}
                      >
                        {meta.name}
                      </span>
                    );
                  })}
                </td>

 <td className="col-status">
  <span className={statusBadge(p.status)} style={{ textTransform: "none" }}>
    {fmtStatus(p.status)}
  </span>
</td>


                {/* Thao tác */}
                <td style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <div className="action-buttons">
                    <Link
                      className="action-btn edit-btn"
                      to={`/admin/products/${p.productId}`}
                      title="Chi tiết / Biến thể"
                    >
                      <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                        <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                        <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                      </svg>
                    </Link>
                    <button
                      className="action-btn delete-btn"
                      title="Xoá sản phẩm"
                      onClick={() => deleteProduct(p)}
                    >
                      <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                        <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                      </svg>
                    </button>
                  </div>

                  <label className="switch" title="Bật/Tắt hiển thị">
                    <input
                      type="checkbox"
                      checked={p.status === "ACTIVE"}
                      onChange={() => toggleStatus(p.productId)}
                    />
                    <span className="slider" />
                  </label>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {/* Pager: căn giữa */}
        <div className="pager">
          <button disabled={page<=1} onClick={()=>setPage((x)=>Math.max(1,x-1))}>Trước</button>
          <span style={{ padding:"0 8px" }}>Trang {page}</span>
          <button disabled={page*pageSize>=total} onClick={()=>setPage((x)=>x+1)}>Tiếp</button>
        </div>
      </div>
    </div>
  );
}