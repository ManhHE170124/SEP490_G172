import React from "react";
import { CategoryApi } from "../../services/categories";
import { ProductApi } from "../../services/products";
import { Link } from "react-router-dom";
import "./admin.css";
// no confirmation for quick toggles

export default function ProductsPage() {
  

  // Lightweight categories loader for product filters (category UI moved to CategoryPage)
  const [categories, setCategories] = React.useState([]);
  const [, setCatLoading] = React.useState(false);

  const loadCategoriesForProducts = React.useCallback(() => {
    setCatLoading(true);
    CategoryApi.list()
      .then(setCategories)
      .finally(() => setCatLoading(false));
  }, []);

  React.useEffect(() => {
    loadCategoriesForProducts();
  }, [loadCategoriesForProducts]);

  // ====== Sản phẩm ======
  const [products, setProducts] = React.useState([]);
  const [total, setTotal] = React.useState(0);
  const [q, setQ] = React.useState({
    keyword: "",
    categoryId: "",
    type: "",
    status: "",
    sort: "createdAt",
    direction: "desc",
    page: 1,
    pageSize: 10,
  });
const [, setProdLoading] = React.useState(false);
  const loadProducts = React.useCallback(() => {
    setProdLoading(true);
    const params = { ...q };
    // normalize/trims
    if (typeof params.keyword === "string") {
      params.keyword = params.keyword.trim();
      if (params.keyword === "") delete params.keyword;
    }
    if (params.categoryId === "") delete params.categoryId;
    if (params.type === "") delete params.type;
    if (params.status === "") delete params.status;

    ProductApi.list(params)
      .then((res) => {
        setProducts(res.items || res.data || res);
        setTotal(res.total ?? res.totalCount ?? 0);
      })
      .finally(() => setProdLoading(false));
  }, [q]);

  // Debounce 400ms cho filter sản phẩm (bỏ nút Áp dụng)
  React.useEffect(() => {
    const t = setTimeout(loadProducts, 400);
    return () => clearTimeout(t);
  }, [q, loadProducts]);
const statusLabel = (s) =>
  s === "ACTIVE" ? "Hiện"
  : s === "INACTIVE" ? "Ẩn"
  : s === "OUT_OF_STOCK" ? "Hết hàng"
  : s || "-";

const statusClass = (s) =>
  s === "ACTIVE" ? "badge green"
  : s === "OUT_OF_STOCK" ? "badge warning"
  : "badge gray";

const typeLabel = (t) =>
  t === "PERSONAL_KEY" ? "Key cá nhân"
  : t === "SHARED_KEY" ? "Key dùng chung"
  : t === "PERSONAL_ACCOUNT" ? "Tài khoản cá nhân"
  : t === "SHARED_ACCOUNT" ? "Tài khoản dùng chung"
  : t || "-";

const toggleProductStatus = async (p) => {
  // compute desired status and send to backend; backend will enforce OUT_OF_STOCK when stockQty <= 0
  const next = p.status === "ACTIVE" ? "INACTIVE" : "ACTIVE";
  try {
    await ProductApi.changeStatus(p.productId, next);
  } catch (err) {
    // fallback to toggle endpoint if direct status patch fails
    try { await ProductApi.toggle(p.productId); } catch (e) { console.error(e); }
  }
  loadProducts();
};
  // CSV + bulk % (Sản phẩm)
  const exportCsv = async () => {
    const blob = await ProductApi.exportCsv();
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url; a.download = "products_price.csv"; a.click();
    URL.revokeObjectURL(url);
  };
  const importCsv = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const res = await ProductApi.importPriceCsv(file);
    alert(`Total: ${res.total}, updated: ${res.updated}, notFound: ${res.notFound}, invalid: ${res.invalid}`);
    e.target.value = "";
    loadProducts();
  };
  const doBulkPercent = async () => {
    const percent = Number(prompt("Nhập % tăng/giảm (âm để giảm):", "5"));
    if (!percent || Number.isNaN(percent)) return;
    const res = await ProductApi.bulkPrice({ percent });
    alert(`Updated: ${res.updated} items`);
    loadProducts();
  };

  // ====== Cấu hình hiển thị (demo lưu local) ======
  const [ui, setUi] = React.useState({
    badgeLowStock: true,
    showShortDesc: true,
    filterShared: true,
    filterPersonal: true,
    filterService: true,
    sortDefault: "Nổi bật",
    perPage: 24,
    titleSeo: "Mua key phần mềm giá tốt",
    descSeo: "Key bản quyền chính hãng, giá rẻ.",
  });


  return (
    <div className="page">
       {/* ===== Khối 2: Sản phẩm ===== */}
      <div className="card" style={{ marginTop: 14 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <h2>Danh sách sản phẩm</h2>
          <div className="row">
            <button className="btn" onClick={doBulkPercent}>↻ Cập nhật giá</button>
            <label className="btn">
              ⬆ Nhập CSV
              <input type="file" accept=".csv" style={{ display: "none" }} onChange={importCsv} />
            </label>
            <button className="btn" onClick={exportCsv}>⬇ Xuất CSV</button>
            <Link className="btn primary" to="/admin/products/add">+ Thêm sản phẩm</Link>
          </div>
        </div>

  {/* Bộ lọc sản phẩm: bỏ nút Áp dụng, tự load 400ms */}
  <div className="filter-inline" style={{ marginTop: 10, display: 'flex', gap: 10, flexWrap: 'nowrap', overflowX: 'auto', alignItems: 'end' }}>
          <div className="group">
            <span>Từ khoá</span>
            <input
              placeholder="Tên, SKU, mô tả…"
              value={q.keyword}
              onChange={(e) => setQ((s) => ({ ...s, keyword: e.target.value, page: 1 }))}
            />
          </div>
          <div className="group w-180">
            <span>Danh mục</span>
            <select
              value={q.categoryId}
              onChange={(e) => setQ((s) => ({ ...s, categoryId: e.target.value, page: 1 }))}
            >
              <option value="">Tất cả</option>
              {categories.map((c) => (
                <option key={c.categoryId} value={c.categoryId}>{c.categoryName}</option>
              ))}
            </select>
          </div>
          <div className="group w-160">
            <span>Loại</span>
            <select value={q.type} onChange={(e) => setQ((s) => ({ ...s, type: e.target.value, page: 1 }))}>
              <option value="">Tất cả</option>
              <option value="PERSONAL_KEY">Key cá nhân</option>
              <option value="SHARED_KEY">Key dùng chung</option>
              <option value="PERSONAL_ACCOUNT">Tài khoản cá nhân</option>
              <option value="SHARED_ACCOUNT">Tài khoản dùng chung</option>
            </select>
          </div>
          <div className="group w-160">
            <span>Trạng thái</span>
            <select value={q.status} onChange={(e) => setQ((s) => ({ ...s, status: e.target.value, page: 1 }))}>
              <option value="">Tất cả</option>
              <option value="ACTIVE">ACTIVE</option>
              <option value="INACTIVE">INACTIVE</option>
              <option value="OUT_OF_STOCK">OUT_OF_STOCK</option>
            </select>
          </div>
          {/* Sort bằng cách nhấn vào tiêu đề bảng (th) - bộ lọc gọn gàng, giữ sort hiện tại */}
          <button className="btn" onClick={() => setQ((s) => ({ ...s, keyword: "", categoryId: "", type: "", status: "", page: 1 }))}>
            Đặt lại
          </button>
        </div>

        <table className="table" style={{ marginTop: 10 }}>
          <thead>
            <tr>
              <th onClick={() => setQ((s) => ({ ...s, sort: "name", direction: s.sort === "name" && s.direction === "asc" ? "desc" : "asc", page: 1 }))} style={{ cursor: "pointer" }}>
                Tên sản phẩm {q.sort === "name" ? (q.direction === "asc" ? " ▲" : " ▼") : ""}
              </th>
              <th onClick={() => setQ((s) => ({ ...s, sort: "type", direction: s.sort === "type" && s.direction === "asc" ? "desc" : "asc", page: 1 }))} style={{ cursor: "pointer" }}>
                Loại {q.sort === "type" ? (q.direction === "asc" ? " ▲" : " ▼") : ""}
              </th>
              <th className="mono" onClick={() => setQ((s) => ({ ...s, sort: "price", direction: s.sort === "price" && s.direction === "asc" ? "desc" : "asc", page: 1 }))} style={{ cursor: "pointer" }}>
                Giá {q.sort === "price" ? (q.direction === "asc" ? " ▲" : " ▼") : ""}
              </th>
              <th onClick={() => setQ((s) => ({ ...s, sort: "stock", direction: s.sort === "stock" && s.direction === "asc" ? "desc" : "asc", page: 1 }))} style={{ cursor: "pointer" }}>
                Tồn {q.sort === "stock" ? (q.direction === "asc" ? " ▲" : " ▼") : ""}
              </th>
              <th onClick={() => setQ((s) => ({ ...s, sort: "status", direction: s.sort === "status" && s.direction === "asc" ? "desc" : "asc", page: 1 }))} style={{ cursor: "pointer" }}>
                Trạng thái {q.sort === "status" ? (q.direction === "asc" ? " ▲" : " ▼") : ""}
              </th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {products.map((p) => (
              <tr key={p.productId}>
                <td>{p.productName}</td>
                <td>{typeLabel(p.productType)}</td>
                <td className="mono">{p.salePrice}</td>
                <td>{p.stockQty}</td>
                <td>
                  <span className={statusClass(p.status)}>{statusLabel(p.status)}</span>
                </td>
                <td className="row" style={{ gap: 8 }}>
                  <Link
                    className="btn"
                    to={`/admin/products/${p.productId}`}
                    title="Xem chi tiết / chỉnh sửa"
                  >
                    ✏️
                  </Link>
                  <label className="switch" title="Bật/Tắt hiển thị">
                    <input type="checkbox" checked={p.status === 'ACTIVE'} onChange={() => toggleProductStatus(p)} />
                    <span className="slider" />
                  </label>
                </td>
              </tr>
            ))}
          </tbody>
        </table>


        <div className="pager">
          <button disabled={q.page <= 1} onClick={() => setQ((s) => ({ ...s, page: s.page - 1 }))}>Trước</button>
          <span style={{ padding: "0 8px" }}>Trang {q.page}</span>
          <button disabled={q.page * q.pageSize >= total} onClick={() => setQ((s) => ({ ...s, page: s.page + 1 }))}>Tiếp</button>
        </div>
      </div>

      {/* ===== Khối 3: Cập nhật giá hàng loạt ===== */}
      <div className="card" style={{ marginTop: 14 }}>
  <h2>Cập nhật giá hàng loạt</h2>
  <div className="row input-group" style={{ gap:10, marginTop:10, flexWrap:"wrap", alignItems:"end" }}>
    <div className="group w-180">
      <span>Phạm vi</span>
      <select><option>Tất cả sản phẩm</option></select>
    </div>
    <div className="group w-180">
      <span>Kiểu cập nhật</span>
      <select><option>Tăng/giảm theo %</option></select>
    </div>
    <div className="group w-180">
      <span>Giá trị</span>
      <input placeholder="% hoặc số tiền" />
    </div>
    <div className="group" style={{ minWidth:260 }}>
      <span>Tải tệp (tuỳ chọn)</span>
      <input type="file" accept=".csv" onChange={importCsv} />
    </div>
    <div className="group" style={{ minWidth:260 }}>
      <span>Xem trước thay đổi</span>
      <div className="kbd">CSV: sku, new_price</div>
    </div>
  </div>

  <div className="row" style={{ marginTop: 12 }}>
    <button className="btn">Hủy</button>
    <button className="btn primary" onClick={doBulkPercent}>Áp dụng giá</button>
    <button className="btn" onClick={exportCsv}>Xuất CSV sản phẩm</button>
  </div>
</div>


      {/* ===== Khối 4: Cấu hình hiển thị trên website (demo) ===== */}
      <div className="card" style={{ marginTop: 14 }}>
  <h2>Cấu hình hiển thị trên website</h2>
  <div className="row input-group" style={{ gap:10, marginTop:10, flexWrap:"wrap" }}>
    <div className="group" style={{ minWidth:260 }}>
      <span>Tùy chọn: Hiện badge “Sắp hết hàng”</span>
      <label className="badge">
        <input type="checkbox"
               checked={ui.badgeLowStock}
               onChange={e=>setUi(s=>({...s, badgeLowStock:e.target.checked}))}/> Bật
      </label>
    </div>
    <div className="group" style={{ minWidth:260 }}>
      <span>Hiển mô tả rút gọn</span>
      <label className="badge">
        <input type="checkbox"
               checked={ui.showShortDesc}
               onChange={e=>setUi(s=>({...s, showShortDesc:e.target.checked}))}/> Bật
      </label>
    </div>
    <div className="group w-180">
      <span>Mặc định sắp xếp</span>
      <input value={ui.sortDefault} onChange={(e)=>setUi(s=>({...s, sortDefault:e.target.value}))}/>
    </div>
    <div className="group w-160">
      <span>Số sản phẩm / trang</span>
      <select value={ui.perPage} onChange={(e)=>setUi(s=>({...s, perPage:Number(e.target.value)}))}>
        <option value={12}>12</option>
        <option value={24}>24</option>
        <option value={36}>36</option>
      </select>
    </div>
    <div className="group" style={{ minWidth:280 }}>
      <span>Tiêu đề trang (SEO)</span>
      <input value={ui.titleSeo} onChange={(e)=>setUi(s=>({...s, titleSeo:e.target.value}))}/>
    </div>
    <div className="group" style={{ minWidth:360 }}>
      <span>Mô tả trang (SEO)</span>
      <input value={ui.descSeo} onChange={(e)=>setUi(s=>({...s, descSeo:e.target.value}))}/>
    </div>
  </div>

  <div className="row" style={{ marginTop: 12 }}>
    <button className="btn">Khôi phục mặc định</button>
    <button className="btn primary" onClick={()=>alert("Đã lưu cấu hình (demo local)")}>Lưu cấu hình</button>
  </div>
</div>
    </div>
  );
}
