
import React from "react"
import { Routes, Route, Navigate } from "react-router-dom"
import ProductsPage from "./pages/admin/ProductsPage.jsx"
import CategoryPage from "./pages/admin/CategoryPage.jsx"
import CategoryAdd from "./pages/admin/CategoryAdd.jsx"
import ProductAdd from "./pages/admin/ProductAdd.jsx"
import BadgeAdd from "./pages/admin/BadgeAdd.jsx"
import BadgeDetail from "./pages/admin/BadgeDetail.jsx"
import AdminLayout from "./components/admin/Layout.jsx"
import CategoryDetail from "./pages/admin/CategoryDetail.jsx" 
import ProductDetail from "./pages/admin/ProductDetail.jsx" 
function Home(){
  return (
    <AdminLayout>
      <div className="card"><h2>Màn hình chính</h2><p>Bảng điều khiển nhanh.</p></div>
    </AdminLayout>
  )
}

export default function App(){
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/admin" replace />} />
      <Route path="/admin" element={<Home/>} />
      <Route path="/admin/products" element={<ProductsPage />} />
  <Route path="/admin/categories" element={<CategoryPage />} />
    <Route path="/admin/badges/add" element={<BadgeAdd />} />
    <Route path="/admin/badges/:code" element={<BadgeDetail />} />
      <Route path="/admin/products/add" element={<ProductAdd />} />
      <Route path="/admin/categories/add" element={<CategoryAdd />} />
      <Route path="/admin/categories/:id" element={<CategoryDetail />} /> 
      <Route path="/admin/products/:id" element={<ProductDetail />} /> 
      <Route path="*" element={<Navigate to="/admin" replace />} />
    </Routes>
  )
}
