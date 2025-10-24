import { useEffect, useMemo, useState } from "react";
import { CategoriesApi, CategoryListItemDto, ProductsApi, ProductListItemDto, PagedResult } from "../api";
import Badge from "../components/Badge";
import Pagination from "../components/Pagination";

const types = ["Single","Combo","Pool","Account"];
const statuses = ["Available","Sold","OutOfStock","Expired","Recalled","Error"];

export default function ProductsPage(){
  // filters
  const [keyword,setKeyword] = useState("");
  const [categoryId,setCategoryId] = useState<number|undefined>(undefined);
  const [type,setType] = useState<string|undefined>(undefined);
  const [status,setStatus] = useState<string|undefined>(undefined);
  const [cats,setCats] = useState<CategoryListItemDto[]>([]);

  // data
  const [page,setPage] = useState(1);
  const [pageSize] = useState(10);
  const [data,setData] = useState<PagedResult<ProductListItemDto>>({items:[],total:0,page:1,pageSize:10});
  const [loading,setLoading] = useState(false);

  useEffect(()=>{ CategoriesApi.list().then(r=>setCats(r.data)); },[]);

  const load = async ()=>{
    setLoading(true);
    const res = await ProductsApi.list({ keyword, categoryId, type, status, page, pageSize });
    setData(res.data);
    setLoading(false);
  };

  useEffect(()=>{ load(); },[keyword,categoryId,type,status,page]);

  const categoryOptions = useMemo(()=>[{id:undefined,name:"Tất cả"}, ...cats.map(c=>({id:c.categoryId,name:c.categoryName}))], [cats]);

  // bulk price state
  const [percent,setPercent] = useState<number>(0);
  const [bulkType,setBulkType] = useState<string|undefined>(undefined);
  const [bulkCats,setBulkCats] = useState<number[]>([]);

  const badgeForStatus = (s:string)=>{
    if (s==="Available") return <Badge text="Đang bán" color="green"/>;
    if (s==="OutOfStock") return <Badge text="Hết hàng" color="gray"/>;
    if (s==="Expired") return <Badge text="Hết hạn" color="red"/>;
    if (s==="Recalled") return <Badge text="Kiểm soát lại" color="yellow"/>;
    return <Badge text={s} color="gray"/>;
  };

  return (
    <div className="max-w-6xl mx-auto px-4 py-6">
      <h1 className="text-2xl font-bold mb-4">Danh mục sản phẩm (theo loại phần mềm)</h1>

      {/* Category quick table */}
      <div className="bg-white rounded-2xl shadow border mb-8">
        <div className="p-4 grid grid-cols-12 gap-3 items-center">
          <input placeholder="Tên danh mục…" className="col-span-4 input" />
          <input placeholder="Mã (slug)…" className="col-span-4 input" />
          <select className="col-span-2 input"><option>Hiện</option><option>Ẩn</option></select>
          <div className="col-span-2 flex justify-end gap-2">
            <a href="/categories/add" className="btn-primary">+ Thêm danh mục</a>
          </div>
        </div>
        <div className="px-4 pb-4">
          <table className="w-full text-sm">
            <thead className="text-left text-gray-500">
              <tr><th className="py-2">Tên</th><th>Slug</th><th>Số SP</th><th>Trạng thái</th><th className="text-right">Thao tác</th></tr>
            </thead>
            <tbody>
              {cats.map(c=>(
                <tr key={c.categoryId} className="border-t">
                  <td className="py-3">{c.categoryName}</td>
                  <td>{c.categoryCode}</td>
                  <td>{c.productCount}</td>
                  <td>{c.isActive ? <Badge text="Hiện" color="green"/> : <Badge text="Ẩn" color="gray"/>}</td>
                  <td className="text-right">
                    <button onClick={()=>CategoriesApi.toggle(c.categoryId).then(()=>CategoriesApi.list().then(r=>setCats(r.data)))} className="btn ghost">🔁 Toggle</button>
                    <button onClick={()=>CategoriesApi.remove(c.categoryId).then(()=>CategoriesApi.list().then(r=>setCats(r.data)))} className="btn ghost ml-2">🗑</button>
                  </td>
                </tr>
              ))}
              {cats.length===0 && <tr><td className="py-6 text-center text-gray-500" colSpan={5}>Chưa có danh mục</td></tr>}
            </tbody>
          </table>
        </div>
      </div>

      <h2 className="text-xl font-semibold mb-3">Danh sách sản phẩm</h2>
      {/* Filters */}
      <div className="bg-white rounded-2xl shadow border p-4 mb-4 grid grid-cols-12 gap-3">
        <input value={keyword} onChange={e=>{setPage(1);setKeyword(e.target.value)}} className="input col-span-4" placeholder="Tên, SKU, mô tả…" />
        <select value={categoryId as any} onChange={e=>{setPage(1); setCategoryId(e.target.value?Number(e.target.value):undefined);}} className="input col-span-3">
          {categoryOptions.map(o=> <option key={`${o.id ?? "all"}`} value={o.id as any}>{o.name}</option>)}
        </select>
        <select value={type ?? ""} onChange={e=>{setPage(1); setType(e.target.value || undefined);}} className="input col-span-2">
          <option value="">Tất cả loại</option>
          {types.map(t=> <option key={t} value={t}>{t}</option>)}
        </select>
        <select value={status ?? ""} onChange={e=>{setPage(1); setStatus(e.target.value || undefined);}} className="input col-span-2">
          <option value="">Tất cả trạng thái</option>
          {statuses.map(s=> <option key={s} value={s}>{s}</option>)}
        </select>
        <a href="/products/add" className="btn-primary col-span-1 text-center">+ Thêm SP</a>
      </div>

      {/* Table */}
      <div className="bg-white rounded-2xl shadow border">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="text-left text-gray-500">
              <tr>
                <th className="py-2 px-4">Mã SP</th>
                <th>Tên sản phẩm</th>
                <th>Mô tả</th>
                <th>Danh mục</th>
                <th>Loại</th>
                <th>Giá</th>
                <th>Trạng thái</th>
                <th className="text-right pr-4">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map(p=>(
                <tr key={p.productId} className="border-t">
                  <td className="py-3 px-4">{p.productCode}</td>
                  <td>{p.productName}</td>
                  <td className="text-gray-500">—</td>
                  <td>{p.categoryIds.map(id=>cats.find(c=>c.categoryId===id)?.categoryName).filter(Boolean).join(", ")}</td>
                  <td>{p.productType}</td>
                  <td>{p.salePrice.toLocaleString()} đ</td>
                  <td>{badgeForStatus(p.status)}</td>
                  <td className="text-right pr-4">
                    <select className="input inline w-36 mr-2" value={p.status}
                      onChange={(e)=>ProductsApi.changeStatus(p.productId, e.target.value).then(load)}>
                      {statuses.map(s=><option key={s}>{s}</option>)}
                    </select>
                    <button className="btn ghost" onClick={()=>ProductsApi.remove(p.productId).then(load)}>🗑</button>
                  </td>
                </tr>
              ))}
              {data.items.length===0 && <tr><td className="py-6 text-center text-gray-500" colSpan={8}>{loading? "Đang tải…" : "Không có dữ liệu"}</td></tr>}
            </tbody>
          </table>
        </div>
        <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onChange={setPage}/>
      </div>

      {/* Bulk price */}
      <div className="bg-white rounded-2xl shadow border mt-6 p-4">
        <div className="flex items-center justify-between mb-3">
          <h3 className="font-semibold">Cập nhật giá hàng loạt</h3>
          <div className="flex gap-2">
            <button className="btn" onClick={()=>ProductsApi.exportCsv().then(res=>{
              const url = URL.createObjectURL(res.data); const a = document.createElement("a");
              a.href = url; a.download = "products_price.csv"; a.click(); URL.revokeObjectURL(url);
            })}>Tải mẫu CSV</button>
            <label className="btn">
              <input type="file" accept=".csv" className="hidden" onChange={(e)=>{
                const f = e.target.files?.[0]; if (!f) return;
                ProductsApi.importCsv(f).then(()=>load());
              }}/>
              Xuất CSV sản phẩm
            </label>
          </div>
        </div>
        <div className="grid grid-cols-12 gap-3 items-end">
          <div className="col-span-3">
            <label className="text-sm text-gray-600">Phạm vi</label>
            <select className="input w-full" onChange={e=>{
              const v = e.target.value;
              if (v==="type") { setBulkCats([]); setBulkType("Single"); }
              else { setBulkType(undefined); setBulkCats([]); }
            }}>
              <option value="all">Tất cả sản phẩm</option>
              <option value="type">Theo loại</option>
              <option value="cat">Theo danh mục</option>
            </select>
          </div>
          <div className="col-span-4">
            <label className="text-sm text-gray-600">Loại / Danh mục</label>
            <div className="flex gap-2">
              <select className="input flex-1" value={bulkType ?? ""} onChange={e=>setBulkType(e.target.value || undefined)}>
                <option value="">— Loại —</option>
                {types.map(t=><option key={t}>{t}</option>)}
              </select>
              <select multiple className="input flex-1 h-10" value={bulkCats.map(String)} onChange={(e)=>{
                const opts = Array.from(e.target.selectedOptions).map(o=>Number(o.value));
                setBulkCats(opts);
              }}>
                {cats.map(c=><option key={c.categoryId} value={c.categoryId}>{c.categoryName}</option>)}
              </select>
            </div>
          </div>
          <div className="col-span-3">
            <label className="text-sm text-gray-600">Tăng/giảm theo %</label>
            <input type="number" className="input w-full" value={percent} onChange={e=>setPercent(Number(e.target.value))} placeholder="%"/>
          </div>
          <div className="col-span-2 flex justify-end">
            <button className="btn-primary" onClick={()=>{
              ProductsApi.bulkPrice({
                productType: bulkType ?? null,
                categoryIds: bulkCats.length? bulkCats : null,
                percent
              }).then(load);
            }}>Áp dụng</button>
          </div>
        </div>
      </div>
    </div>
  );
}

/** tiny css helpers via tailwind classes */
declare global {
  interface HTMLElementTagNameMap { }
}
