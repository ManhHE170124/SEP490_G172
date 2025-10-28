
import React from "react"
import { NavLink, Outlet } from "react-router-dom"
import "../../admin.css"

export default function AdminLayout({ children, title }){
  return (
    <div className="admin">
      <aside className="sidebar">
        <div className="sidecard">
          <div style={{display:"flex",alignItems:"center",gap:8,fontWeight:700,fontSize:20}}>
            <span style={{background:"#111827",color:"#fff",borderRadius:12,padding:"6px 10px"}}>@</span>
            Keytietkiem <span style={{color:"#6b7280"}}>• Admin</span>
          </div>
        </div>
        <div className="sidecard">
          <h4>Tổng quan</h4>
          <div className="nav">
            <NavLink to="/admin" end>🏠 Màn hình chính</NavLink>
          </div>
        </div>
        <div className="sidecard">
          <h4>Quản lý</h4>
          <div className="nav">
            <NavLink to="/admin/products">✳️ Sản phẩm</NavLink>
            <NavLink to="/admin/categories">🗂️ Danh mục</NavLink>
            <a href="#">📄 Đơn hàng</a>
            <a href="#">👥 Người dùng</a>
            <a href="#">🧾 Nội dung</a>
            <a href="#">🎫 Quản lý ticket</a>
            <a href="#">🔐 Quyền truy cập (RBAC)</a>
            <a href="#">📜 Audit Logs</a>
          </div>
        </div>
        <div className="sidecard">
          <h4>Cài đặt</h4>
          <div className="nav">
            <a href="#">⚙️ Cấu hình trang web</a>
          </div>
          <div className="footer">© 2025 Keytietkiem</div>
        </div>
      </aside>
      <main className="main">
        <div className="topbar">
          <div className="search">
            <input placeholder="Tìm sản phẩm, SKU, danh mục…" />
          </div>
          <span className="badge gray">10/2025</span>
          <span className="badge">🔔 2</span>
        </div>
        <div style={{marginTop:14}}>
          {children || <Outlet/>}
        </div>
      </main>
    </div>
  )
}
