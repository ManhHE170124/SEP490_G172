import React from 'react'
import { Routes, Route, Link, Navigate } from 'react-router-dom'
import CategoriesPage from './src/pages/admin/CategoriesPage.jsx'
import ProductsPage from './src/pages/admin/ProductsPage.jsx'

export default function App(){
  return (
    <div style={{padding:16}}>
      <h1>Keytietkiem Admin</h1>
      <nav style={{display:'flex', gap:12, marginBottom:16}}>
        <Link to="/admin/categories">Categories</Link>
        <Link to="/admin/products">Products</Link>
      </nav>
      <Routes>
        <Route path="/" element={<Navigate to="/admin/categories" replace />} />
        <Route path="/admin/categories" element={<CategoriesPage />} />
        <Route path="/admin/products" element={<ProductsPage />} />
        <Route path="*" element={<Navigate to="/admin/categories" replace />} />
      </Routes>
    </div>
  )
}
