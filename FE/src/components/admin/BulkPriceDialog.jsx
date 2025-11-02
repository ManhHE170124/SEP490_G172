import React from "react"

export default function BulkPriceDialog({ value, onChange }){
  const v = value || { percent: 0, productType:"", categoryIds:[] }
  const set = (k,val)=> onChange && onChange({ ...v, [k]: val })
  return (
    <div style={{ display:"grid", gap:8, maxWidth:420 }}>
      <label>Percent (+/-)
        <input className="ipt" type="number" value={v.percent ?? 0} onChange={e=>set("percent", Number(e.target.value))}/>
      </label>
      <label>Product Type (optional)
        <input className="ipt" value={v.productType||""} onChange={e=>set("productType", e.target.value)} placeholder="SOFTWARE / SERVICE / ..."/>
      </label>
      <label>Category IDs (comma) (optional)
        <input className="ipt" value={(v.categoryIds||[]).join(",")} onChange={e=>set("categoryIds", e.target.value.split(",").map(s=>Number(s.trim())).filter(x=>!Number.isNaN(x)))} placeholder="1,2,3"/>
      </label>
      <style>{`.ipt{width:100%;padding:8px;border:1px solid #ddd;border-radius:8px}`}</style>
    </div>
  )
}
