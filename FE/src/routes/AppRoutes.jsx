/**
 * File: AppRoutes.jsx
 * Author: Keytietkiem Team
 * Created: 18/10/2025
 * Last Updated: 25/10/2025
 * Version: 1.0.0
 * Purpose: Application routes with layout separation (Client and Admin)
 */
// import { Routes, Route } from "react-router-dom";
import AdminLayout from "../layout/AdminLayout/AdminLayout";
import ClientLayout from "../layout/ClientLayout/ClientLayout";
import RoleAssign from "../pages/RoleManage/RoleAssign";
import RoleManage from "../pages/RoleManage/RoleManage";
import Page404 from "../pages/NotFound/Page404";
import AdminPostList from "../pages/PostManage/AdminPostList"
import PostCreateEdit from "../pages/PostManage/CreateEditPost"

import { Navigate, Route, Routes } from "react-router-dom";
import { Suspense, lazy } from "react";
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
import AdminTicketManagement from "../pages/admin/admin-ticket-management";
import WebsiteConfig from "../pages/admin/WebsiteConfig";



// Auth pages
import LoginPage from "../pages/auth/LoginPage.jsx";
import SignUpPage from "../pages/auth/SignUpPage.jsx";
import ForgotPasswordPage from "../pages/auth/ForgotPasswordPage.jsx";
import CheckEmailPage from "../pages/auth/CheckEmailPage.jsx";
import ResetPasswordPage from "../pages/auth/ResetPasswordPage.jsx";

// Supplier pages
import SuppliersPage from "../pages/supplier/SuppliersPage.jsx";
import SupplierDetailPage from "../pages/supplier/SupplierDetailPage.jsx";

// Storage pages
import KeyManagementPage from "../pages/storage/KeyManagementPage.jsx";
import KeyDetailPage from "../pages/storage/KeyDetailPage.jsx";


const AdminTicketDetail = lazy(() =>
  import("../pages/admin/admin-ticket-detail.jsx").then((m) => ({
    default:
      typeof m.default === "function"
        ? m.default
        : (typeof m.AdminTicketDetail === "function" ? m.AdminTicketDetail : (() => null)),
  }))
);


/**
 * @summary: Configure and render application routes with appropriate layouts.
 * @returns {JSX.Element} - Routes configuration with ClientLayout and AdminLayout
 */
export default function AppRoutes() {
  return (
    <Routes>
      {/* Default Access Routes */}
      <Route path="/" element={<ClientLayout> <LoginPage /> </ClientLayout> } />

      <Route path="/login" element={ <ClientLayout> <LoginPage /> </ClientLayout> } />
      <Route path="/register" element={<ClientLayout> <SignUpPage /></ClientLayout>} />
      <Route path="/forgot-password" element={<ClientLayout> <ForgotPasswordPage /></ClientLayout>} />
      <Route path="/check-reset-email" element={<ClientLayout> <CheckEmailPage /></ClientLayout>} />
      <Route path="/reset-password" element={<ClientLayout> <ResetPasswordPage /></ClientLayout>} />
      <Route path="/admin" element={<div />} />

      {/* Tickets */}
      <Route path="/admin/tickets" element={<AdminLayout><AdminTicketManagement /></AdminLayout>} />
      <Route
        path="/admin/tickets/:id"
        element={
          <Suspense fallback={<div>Đang tải chi tiết...</div>}>
            <AdminLayout><AdminTicketDetail /></AdminLayout>
          </Suspense>
        }
      />


      {/* Products */}
      <Route path="/admin/products" element={<AdminLayout><ProductsPage /></AdminLayout>} />
      <Route path="/admin/products/add" element={<AdminLayout><ProductAdd /></AdminLayout> }/>
      <Route path="/admin/products/:id" element={<AdminLayout><ProductDetail /></AdminLayout>} />

      {/* Categories */}
      <Route path="/admin/categories" element={<AdminLayout><CategoryPage /></AdminLayout>} />
      <Route path="/admin/categories/add" element={<AdminLayout><CategoryAdd /></AdminLayout>} />
      <Route path="/admin/categories/:id" element={<AdminLayout><CategoryDetail /></AdminLayout>} />

      {/* Badges */}
      <Route path="/admin/badges/add" element={<AdminLayout><BadgeAdd /></AdminLayout>} />
      <Route path="/admin/badges/:code" element={<AdminLayout><BadgeDetail /> </AdminLayout>} />
      {/* Client/Public Routes */}

      {/* Admin Routes */}
      <Route path="/admin-dashboard" element={ <AdminLayout> <Page404 /> </AdminLayout> } />
      <Route path="/admin/users" element={ <AdminLayout> <AdminUserManagement /> </AdminLayout> } />
      <Route path="/admin-user-management" element={ <AdminLayout> <AdminUserManagement /> </AdminLayout> } />
      <Route path="/role-manage" element={ <AdminLayout> <RoleManage /> </AdminLayout> } />
      <Route path="/role-assign" element={ <AdminLayout> <RoleAssign /> </AdminLayout> } />

      <Route path="admin-post-list" element={ <AdminLayout> <AdminPostList /> </AdminLayout> } />
      <Route path="post-create-edit" element={ <AdminLayout> <PostCreateEdit /> </AdminLayout> }/>
      <Route path="post-create-edit/:postId" element={ <AdminLayout> <PostCreateEdit /> </AdminLayout> }/>
      {/* 404 - Default to Client Layout - Fallbacks*/}
      <Route path="*" element={  <Page404 /> } /> 
      {/* Suppliers */}
      <Route path="/suppliers" element={<AdminLayout><SuppliersPage /></AdminLayout>} /> 
      <Route path="/suppliers/add" element={<AdminLayout><SupplierDetailPage /></AdminLayout>} />
      <Route path="/suppliers/:id" element={<AdminLayout><SupplierDetailPage /></AdminLayout>} />

      {/* Product Keys */}
      <Route path="/keys" element={<AdminLayout><KeyManagementPage /></AdminLayout>} />
      <Route path="/keys/add" element={<AdminLayout><KeyDetailPage /></AdminLayout>} />
      <Route path="/keys/:id" element={<AdminLayout><KeyDetailPage /></AdminLayout>} />

      {/* RBAC & Users */}
      <Route path="/admin/users" element={<AdminLayout><AdminUserManagement /></AdminLayout>} />
      <Route path="/admin-user-management" element={<AdminLayout><AdminUserManagement /></AdminLayout>} />

      <Route path="/admin/website-config" element={<AdminLayout><WebsiteConfig /></AdminLayout>} />

      {/* Fallbacks */}
      <Route path="*" element={<Page404 />} />
    </Routes>
  );
}
