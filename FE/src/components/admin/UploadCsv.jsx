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
      <input
        type="file"
        accept=".csv,text/csv"
        onChange={onPick}
        style={{
          padding: "8px 12px",
          border: "1px solid var(--line)",
          borderRadius: "6px",
          background: "var(--card)",
          color: "var(--text)",
          fontSize: "14px",
          cursor: "pointer"
        }}
      />
      <button
        className="btn primary"
        onClick={upload}
        disabled={!file || disabled}
      >
        Upload
      </button>
    </div>
  )
}
