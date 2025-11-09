import React from "react"

export default function UploadCsv({ onUpload, disabled }){
  const [file, setFile] = React.useState(null)
  const onPick = e => {
    const f = e.target.files?.[0] || null
    setFile(f)
  }
  const upload = () => {
    if (!file) return alert("Select a CSV file first")
    onUpload && onUpload(file)
  }
  return (
    <div style={{ display:"flex", gap:8, alignItems:"center" }}>
      <input type="file" accept=".csv,text/csv" onChange={onPick}/>
      <button onClick={upload} disabled={!file || disabled}>Upload</button>
    </div>
  )
}
