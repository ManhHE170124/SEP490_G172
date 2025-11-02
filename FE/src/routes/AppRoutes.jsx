import { Navigate, Route, Routes } from "react-router-dom";
// Admin pages
import BadgeAdd from "../pages/admin/BadgeAdd.jsx";
import BadgeDetail from "../pages/admin/BadgeDetail.jsx";
import CategoryAdd from "../pages/admin/CategoryAdd.jsx";
import CategoryDetail from "../pages/admin/CategoryDetail.jsx";
import CategoryPage from "../pages/admin/CategoryPage.jsx";
import ProductAdd from "../pages/admin/ProductAdd.jsx";
import ProductDetail from "../pages/admin/ProductDetail.jsx";
import ProductsPage from "../pages/admin/ProductsPage.jsx";
import AdminUserManagement from "../pages/admin/admin-user-management";
import TicketList from "../pages/admin/admin-ticket-management.jsx";
import TicketDetail from "../pages/admin/TicketDetail";


// Auth pages
import LoginPage from "../pages/auth/LoginPage.jsx";
import SignUpPage from "../pages/auth/SignUpPage.jsx";

// Other pages
import Page404 from "../pages/NotFound/Page404";
import RBACManagement from "../pages/RBAC/RBACManagement";
import RoleAssign from "../pages/RBAC/RoleAssign";


export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<SignUpPage />} />
      <Route path="/" element={<Navigate to="/admin/products" replace />} />
      <Route path="/admin" element={<div />} />

      {/* Tickets */}
      <Route path="/admin/tickets" element={<TicketList />} />
      <Route path="/admin/tickets/:id" element={<TicketDetail />} />

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
