import { useEffect, useState } from "react";
import { CategoriesApi, CategoryListItemDto, ProductCreateDto, ProductsApi } from "../api";
import { useNavigate } from "react-router-dom";

const TYPES = ["Single","Combo","Pool","Account"];
const STATUSES = ["Available","OutOfStock","Sold","Expired","Recalled","Error"];

export default function ProductAddPage(){
  const nav = useNavigate();
  const [cats,setCats] = useState<CategoryListItemDto[]>([]);
  useEffect(()=>{ CategoriesApi.list().then(r=>setCats(r.data)); },[]);

  const [form,setForm] = useState<ProductCreateDto>({
    productCode:"", productName:"", supplierId:1, productType:"Single",
    costPrice: null as any, salePrice: 0, stockQty: 0, warrantyDays: 0,
    expiryDate: null, autoDelivery: true, status:"Available", description:"", categoryIds:[]
  });

  const change = (k:keyof ProductCreateDto, v:any)=> setForm(s=>({...s,[k]:v}));

  return (
    <div className="max-w-6xl mx-auto px-4 py-6">
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-2xl font-bold">Thêm sản phẩm</h1>
        <button onClick={()=>nav(-1)} className="btn">⬅ Quay lại</button>
      </div>

      <div className="bg-white rounded-2xl shadow border p-5 grid grid-cols-12 gap-4">
        <div className="col-span-6">
          <label className="text-sm text-gray-600">Tên sản phẩm</label>
          <input className="input w-full" placeholder="VD: Microsoft 365 Family"
            value={form.productName} onChange={e=>change("productName", e.target.value)} />
        </div>
        <div className="col-span-6">
          <label className="text-sm text-gray-600">SKU</label>
          <input className="input w-full" placeholder="OFF_365_FAM"
            value={form.productCode} onChange={e=>change("productCode", e.target.value)} />
        </div>

        <div className="col-span-4">
          <label className="text-sm text-gray-600">Danh mục</label>
          <select multiple className="input w-full h-10"
            value={form.categoryIds.map(String)}
            onChange={(e)=>{
              const ids = Array.from(e.target.selectedOptions).map(o=>Number(o.value));
              change("categoryIds", ids);
            }}>
            {cats.map(c=> <option key={c.categoryId} value={c.categoryId}>{c.categoryName}</option>)}
          </select>
        </div>
        <div className="col-span-4">
          <label className="text-sm text-gray-600">Loại</label>
          <select className="input w-full" value={form.productType} onChange={e=>change("productType", e.target.value)}>
            {TYPES.map(t=><option key={t}>{t}</option>)}
          </select>
        </div>
        <div className="col-span-4">
          <label className="text-sm text-gray-600">Trạng thái hiển thị</label>
          <select className="input w-full" value={form.status} onChange={e=>change("status", e.target.value)}>
            {STATUSES.map(s=><option key={s}>{s}</option>)}
          </select>
        </div>

        <div className="col-span-4">
          <label className="text-sm text-gray-600">Giá bán (đ)</label>
          <input type="number" className="input w-full" value={form.salePrice}
            onChange={e=>change("salePrice", Number(e.target.value))}/>
        </div>
        <div className="col-span-4">
          <label className="text-sm text-gray-600">Giá gốc/niêm yết (đ)</label>
          <input type="number" className="input w-full" value={form.costPrice ?? 0}
            onChange={e=>change("costPrice", Number(e.target.value))}/>
        </div>
        <div className="col-span-4">
          <label className="text-sm text-gray-600">SupplierId</label>
          <input type="number" className="input w-full" value={form.supplierId}
            onChange={e=>change("supplierId", Number(e.target.value))}/>
        </div>

        <div className="col-span-6">
          <label className="text-sm text-gray-600">Mô tả ngắn</label>
          <textarea className="input w-full h-24" value={form.description ?? ""} onChange={e=>change("description", e.target.value)} />
        </div>
        <div className="col-span-6">
          <label className="text-sm text-gray-600">Mô tả chi tiết (landing)</label>
          <textarea className="input w-full h-24" placeholder="— (không lưu vào DB, bạn có thể bỏ qua)" disabled/>
        </div>

        <div className="col-span-3">
          <label className="text-sm text-gray-600">Stock</label>
          <input type="number" className="input w-full" value={form.stockQty}
            onChange={e=>change("stockQty", Number(e.target.value))}/>
        </div>
        <div className="col-span-3">
          <label className="text-sm text-gray-600">Bảo hành (ngày)</label>
          <input type="number" className="input w-full" value={form.warrantyDays}
            onChange={e=>change("warrantyDays", Number(e.target.value))}/>
        </div>
        <div className="col-span-3">
          <label className="text-sm text-gray-600">Hạn dùng (yyyy-MM-dd)</label>
          <input type="date" className="input w-full" value={form.expiryDate ?? ""} onChange={e=>change("expiryDate", e.target.value || null)}/>
        </div>
        <div className="col-span-3">
          <label className="text-sm text-gray-600">Tự động giao hàng</label>
          <select className="input w-full" value={form.autoDelivery? "1":"0"} onChange={e=>change("autoDelivery", e.target.value==="1")}>
            <option value="1">Có</option>
            <option value="0">Không</option>
          </select>
        </div>

        <div className="col-span-12 flex gap-3">
          <button className="btn">Lưu nháp</button>
          <button className="btn-primary" onClick={()=>{
            ProductsApi.create(form).then(r=>nav(`/products`));
          }}>Lưu & Xuất bản</button>
        </div>
      </div>
    </div>
  );
}
