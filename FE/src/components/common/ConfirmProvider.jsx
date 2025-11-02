import React from "react"

const ConfirmCtx = React.createContext(null)

export function ConfirmProvider({ children }) {
  const [state, setState] = React.useState({
    open: false, title: "Xác nhận", message: "", okText: "Xác nhận", cancelText: "Hủy",
    resolve: null
  })

  const confirm = React.useCallback((opts={})=>{
    return new Promise((resolve)=>{
      setState(s => ({
        open: true,
        title: opts.title || "Xác nhận thao tác",
        message: opts.message || "Bạn có chắc muốn thực hiện thao tác này?",
        okText: opts.okText || "Đồng ý",
        cancelText: opts.cancelText || "Hủy",
        resolve
      }))
    })
  }, [])

  const onClose = (v)=>{
    setState(s => {
      if (s.resolve) s.resolve(v)
      return { ...s, open:false, resolve:null }
    })
  }

  return (
    <ConfirmCtx.Provider value={confirm}>
      {children}
      {state.open && (
        <div className="modal-backdrop">
          <div className="modal-card">
            <div className="modal-header">
              <h3>{state.title}</h3>
            </div>
            <div className="modal-body">
              <p>{state.message}</p>
            </div>
            <div className="modal-footer">
              <button className="btn" onClick={()=>onClose(false)}>{state.cancelText}</button>
              <button className="btn primary" onClick={()=>onClose(true)}>{state.okText}</button>
            </div>
          </div>
        </div>
      )}
    </ConfirmCtx.Provider>
  )
}

export function useConfirm(){
  const ctx = React.useContext(ConfirmCtx)
  if (!ctx) throw new Error("useConfirm must be used inside <ConfirmProvider>")
  return ctx
}
