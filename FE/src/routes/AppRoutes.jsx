import { Routes, Route, Navigate } from "react-router-dom";
// Admin pages
import ProductsPage from "../pages/admin/ProductsPage.jsx";
import ProductAdd from "../pages/admin/ProductAdd.jsx";
import ProductDetail from "../pages/admin/ProductDetail.jsx";
import CategoryPage from "../pages/admin/CategoryPage.jsx";
import CategoryAdd from "../pages/admin/CategoryAdd.jsx";
import CategoryDetail from "../pages/admin/CategoryDetail.jsx";
import BadgeAdd from "../pages/admin/BadgeAdd.jsx";
import BadgeDetail from "../pages/admin/BadgeDetail.jsx";

// Other pages
import RoleAssign from "../pages/RBAC/RoleAssign";
import RBACManagement from "../pages/RBAC/RBACManagement";
import Page404 from "../pages/NotFound/Page404";
import AdminUserManagement from "../pages/admin-user-management";

export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/admin/products" replace />} />
      <Route path="/admin" element={<div />} />

      {/* Products */}
      <Route path="/admin/products" element={<ProductsPage />} />
      <Route path="/admin/products/add" element={<ProductAdd />} />
      <Route path="/admin/products/:id" element={<ProductDetail />} />

      {/* Categories */}
      <Route path="/admin/categories" element={<CategoryPage />} />
      <Route path="/admin/categories/add" element={<CategoryAdd />} />
      <Route path="/admin/categories/:id" element={<CategoryDetail />} />

      {/* Badges */}
      <Route path="/admin/badges/add" element={<BadgeAdd />} />
      <Route path="/admin/badges/:code" element={<BadgeDetail />} />

      {/* RBAC & Users */}
      <Route path="/admin/users" element={<AdminUserManagement />} />
      <Route path="/admin-user-management" element={<AdminUserManagement />} />
      <Route path="/rbac" element={<RBACManagement />} />
      <Route path="/roleassign" element={<RoleAssign />} />

      {/* Fallbacks */}
      <Route path="*" element={<Page404 />} />
    </Routes>
  );
}