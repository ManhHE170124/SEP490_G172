import React from "react"

export default function ProductForm({ value, onChange, categories }){
  const v = value || {
    productCode:"", productName:"", supplierId:0, productType:"SOFTWARE",
    costPrice:0, salePrice:0, stockQty:0, warrantyDays:0, expiryDate:"",
    autoDelivery:false, status:"ACTIVE", description:"", categoryIds:[]
  }
  const set = (k,val)=> onChange && onChange({ ...v, [k]: val })
  const toggleCategory = (id) => {
    const setIds = new Set(v.categoryIds||[])
    setIds.has(id) ? setIds.delete(id) : setIds.add(id)
    set("categoryIds", Array.from(setIds))
  }
  return (
    <div style={{ display:"grid", gap:8, maxWidth:640 }}>
      <div style={{ display:"grid", gridTemplateColumns:"1fr 1fr", gap:8 }}>
        <label>Code
          <input className="ipt" value={v.productCode||""} onChange={e=>set("productCode", e.target.value)}/>
        </label>
        <label>Name
          <input className="ipt" value={v.productName||""} onChange={e=>set("productName", e.target.value)}/>
        </label>
      </div>
      <div style={{ display:"grid", gridTemplateColumns:"1fr 1fr 1fr", gap:8 }}>
        <label>SupplierId
          <input type="number" className="ipt" value={v.supplierId ?? 0} onChange={e=>set("supplierId", Number(e.target.value))}/>
        </label>
        <label>Type
          <select className="ipt" value={v.productType||""} onChange={e=>set("productType", e.target.value)}>
            <option value="SOFTWARE">SOFTWARE</option>
            <option value="SERVICE">SERVICE</option>
            <option value="LICENSE">LICENSE</option>
          </select>
        </label>
        <label>Status
          <select className="ipt" value={v.status||""} onChange={e=>set("status", e.target.value)}>
            <option value="ACTIVE">ACTIVE</option>
            <option value="INACTIVE">INACTIVE</option>
            <option value="OUT_OF_STOCK">OUT_OF_STOCK</option>
          </select>
        </label>
      </div>
      <div style={{ display:"grid", gridTemplateColumns:"1fr 1fr 1fr 1fr", gap:8 }}>
        <label>Cost
          <input type="number" className="ipt" value={v.costPrice ?? 0} onChange={e=>set("costPrice", Number(e.target.value))}/>
        </label>
        <label>Sale
          <input type="number" className="ipt" value={v.salePrice ?? 0} onChange={e=>set("salePrice", Number(e.target.value))}/>
        </label>
        <label>Stock
          <input type="number" className="ipt" value={v.stockQty ?? 0} onChange={e=>set("stockQty", Number(e.target.value))}/>
        </label>
        <label>Warranty (days)
          <input type="number" className="ipt" value={v.warrantyDays ?? 0} onChange={e=>set("warrantyDays", Number(e.target.value))}/>
        </label>
      </div>
      <div style={{ display:"grid", gridTemplateColumns:"1fr 1fr", gap:8 }}>
        <label>Expiry
          <input type="date" className="ipt" value={v.expiryDate || ""} onChange={e=>set("expiryDate", e.target.value)}/>
        </label>
        <label style={{ display:"flex", gap:8, alignItems:"center" }}>
          <input type="checkbox" checked={!!v.autoDelivery} onChange={e=>set("autoDelivery", e.target.checked)}/>
          Auto Delivery
        </label>
      </div>
      <fieldset style={{ border:"1px solid #ddd", borderRadius:8, padding:8 }}>
        <legend>Categories</legend>
        <div style={{ display:"flex", gap:8, flexWrap:"wrap" }}>
          {(categories||[]).map(c => (
            <label key={c.categoryId} style={{ display:"flex", alignItems:"center", gap:6, padding:"4px 8px", border:"1px solid #eee", borderRadius:8 }}>
              <input type="checkbox" checked={(v.categoryIds||[]).includes(c.categoryId)} onChange={()=>toggleCategory(c.categoryId)}/>
              <span>{c.categoryName}</span>
            </label>
          ))}
        </div>
      </fieldset>
      <label>Description
        <textarea className="ipt" rows={4} value={v.description||""} onChange={e=>set("description", e.target.value)}/>
      </label>
      <style>{`.ipt{width:100%;padding:8px;border:1px solid #ddd;border-radius:8px}`}</style>
    </div>
  )
}
