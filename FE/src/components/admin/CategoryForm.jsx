import React from "react"

export default function CategoryForm({ value, onChange }){
  const v = value || { categoryCode:"", categoryName:"", description:"", isActive:true, displayOrder:0 }
  const set = (k,val)=> onChange && onChange({ ...v, [k]: val })
  return (
    <div style={{ display:"grid", gap:8, maxWidth:480 }}>
      <label>
        Code (slug)
        <input className="ipt" value={v.categoryCode||""} onChange={e=>set("categoryCode", e.target.value)} placeholder="utilities-tools"/>
      </label>
      <label>
        Name
        <input className="ipt" value={v.categoryName||""} onChange={e=>set("categoryName", e.target.value)} placeholder="Utilities & Tools"/>
      </label>
      <label>
        Description
        <textarea className="ipt" rows={3} value={v.description||""} onChange={e=>set("description", e.target.value)} />
      </label>
      <label>
        Display Order
        <input className="ipt" type="number" value={v.displayOrder ?? 0} onChange={e=>set("displayOrder", Number(e.target.value))}/>
      </label>
      <label style={{ display:"flex", gap:8, alignItems:"center" }}>
        <input type="checkbox" checked={!!v.isActive} onChange={e=>set("isActive", e.target.checked)}/>
        Active
      </label>
      <style>{`.ipt{width:100%;padding:8px;border:1px solid #ddd;border-radius:8px}`}</style>
    </div>
  )
}
