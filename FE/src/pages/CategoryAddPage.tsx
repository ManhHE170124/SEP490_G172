import { useState } from "react";
import { CategoriesApi, CategoryCreateDto } from "../api";
import { useNavigate } from "react-router-dom";

export default function CategoryAddPage(){
  const nav = useNavigate();
  const [form,setForm] = useState<CategoryCreateDto>({
    categoryCode:"", categoryName:"", description:"", isActive:true, displayOrder:0
  });
  const change = (k:keyof CategoryCreateDto, v:any)=> setForm(s=>({...s,[k]:v}));

  return (
    <div className="max-w-5xl mx-auto px-4 py-6">
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-2xl font-bold">Thêm danh mục</h1>
        <button onClick={()=>nav(-1)} className="btn">⬅ Quay lại</button>
      </div>

      <div className="bg-white rounded-2xl shadow border p-5 grid grid-cols-12 gap-4">
        <div className="col-span-5">
          <label className="text-sm text-gray-600">Tên danh mục</label>
          <input className="input w-full" placeholder="VD: Office" value={form.categoryName}
            onChange={e=>change("categoryName", e.target.value)} />
        </div>
        <div className="col-span-4">
          <label className="text-sm text-gray-600">Slug</label>
          <input className="input w-full" placeholder="office, windows…" value={form.categoryCode}
            onChange={e=>change("categoryCode", e.target.value)} />
        </div>
        <div className="col-span-3">
          <label className="text-sm text-gray-600">Hiển thị</label>
          <select className="input w-full" value={form.isActive? "1":"0"} onChange={e=>change("isActive", e.target.value==="1")}>
            <option value="1">Hiện</option>
            <option value="0">Ẩn</option>
          </select>
        </div>

        <div className="col-span-12">
          <label className="text-sm text-gray-600">Mô tả</label>
          <textarea className="input w-full h-28" placeholder="Mô tả ngắn sẽ hiển thị trên website…"
            value={form.description ?? ""} onChange={e=>change("description", e.target.value)} />
        </div>

        <div className="col-span-12 flex gap-3">
          <button className="btn" onClick={()=>nav(-1)}>Lưu nháp</button>
          <button className="btn-primary" onClick={()=>CategoriesApi.create(form).then(r=>nav("/products"))}>Lưu & Kích hoạt</button>
        </div>
      </div>
    </div>
  );
}
