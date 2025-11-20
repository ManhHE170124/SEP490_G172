/**
 * File: AppRoutes.jsx
 * Author: Keytietkiem Team
 * Created: 18/10/2025
 * Last Updated: 25/10/2025
 * Version: 1.0.0
 * Purpose: Application routes with layout separation (Client and Admin)
 */
import { Navigate, Route, Routes } from "react-router-dom";
import { Suspense, lazy } from "react";
// import { Routes, Route } from "react-router-dom";
import AdminLayout from "../layout/AdminLayout/AdminLayout";
import ClientLayout from "../layout/ClientLayout/ClientLayout";
import Page404 from "../pages/NotFound/Page404";

//Role Management Pages
import RoleAssign from "../pages/RoleManage/RoleAssign";
import RoleManage from "../pages/RoleManage/RoleManage";
// Post Management Pages
import AdminPostList from "../pages/PostManage/AdminPostList";
import PostCreateEdit from "../pages/PostManage/CreateEditPost";
import TagPostTypeManage from "../pages/PostManage/TagAndPostTypeManage";

// Admin pages
import CategoryPage from "../pages/admin/CategoryPage.jsx";
import ProductAdd from "../pages/admin/ProductAdd.jsx";
import ProductDetail from "../pages/admin/ProductDetail.jsx";
import ProductsPage from "../pages/admin/ProductsPage.jsx";
import AdminUserManagement from "../pages/admin/admin-user-management";
import AdminTicketManagement from "../pages/admin/admin-ticket-management";
import WebsiteConfig from "../pages/admin/WebsiteConfig";
import FaqsPage from "../pages/admin/FaqsPage.jsx";
// App.jsx (hoặc routes admin)
import VariantDetail from "../pages/admin/VariantDetail.jsx";

// *** Staff ticket pages ***
import StaffTicketManagement from "../pages/admin/staff-ticket-management";

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
import AccountManagementPage from "../pages/storage/AccountManagementPage.jsx";
import AccountDetailPage from "../pages/storage/AccountDetailPage.jsx";
import KeyMonitorPage from "../pages/storage/KeyMonitorPage.jsx";

//Blog(Client)
import BlogList from "../pages/blog/Bloglist.jsx";
// Order pages
import OrderHistoryPage from "../pages/orders/OrderHistoryPage.jsx";
import OrderDetailPage from "../pages/orders/OrderDetailPage.jsx";

// Customer ticket pages
import CustomerTicketsPage from "../pages/tickets/customer-tickets.jsx";
import CustomerTicketDetailPage from "../pages/tickets/customer-ticket-detail.jsx";

// Lazy admin ticket detail
const AdminTicketDetail = lazy(() =>
  import("../pages/admin/admin-ticket-detail.jsx").then((m) => ({
    default:
      typeof m.default === "function"
        ? m.default
        : typeof m.AdminTicketDetail === "function"
        ? m.AdminTicketDetail
        : () => null,
  }))
);

// *** Lazy staff ticket detail ***
const StaffTicketDetail = lazy(() =>
  import("../pages/admin/staff-ticket-detail.jsx").then((m) => ({
    default:
      typeof m.default === "function"
        ? m.default
        : typeof m.StaffTicketDetail === "function"
        ? m.StaffTicketDetail
        : () => null,
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
      <Route path="/" element={<ClientLayout> </ClientLayout>} />

      <Route
        path="/login"
        element={
          <ClientLayout>
            <LoginPage />
          </ClientLayout>
        }
      />
      <Route
        path="/register"
        element={
          <ClientLayout>
            <SignUpPage />
          </ClientLayout>
        }
      />
      <Route
        path="/forgot-password"
        element={
          <ClientLayout>
            <ForgotPasswordPage />
          </ClientLayout>
        }
      />
      <Route
        path="/check-reset-email"
        element={
          <ClientLayout>
            <CheckEmailPage />
          </ClientLayout>
        }
      />
      <Route
        path="/reset-password"
        element={
          <ClientLayout>
            <ResetPasswordPage />
          </ClientLayout>
        }
      />
      <Route path="/admin" element={<div />} />

      {/* Admin Tickets */}
      <Route
        path="/admin/tickets"
        element={
          <AdminLayout>
            <AdminTicketManagement />
          </AdminLayout>
        }
      />
      <Route
        path="/admin/tickets/:id"
        element={
          <Suspense fallback={<div>Đang tải chi tiết...</div>}>
            <AdminLayout>
              <AdminTicketDetail />
            </AdminLayout>
          </Suspense>
        }
      />

      {/* Staff Tickets */}
      <Route
        path="/staff/tickets"
        element={
          <AdminLayout>
            <StaffTicketManagement />
          </AdminLayout>
        }
      />
      <Route
        path="/staff/tickets/:id"
        element={
          <Suspense fallback={<div>Đang tải chi tiết...</div>}>
            <AdminLayout>
              <StaffTicketDetail />
            </AdminLayout>
          </Suspense>
        }
      />

      {/* Customer tickets */}
      <Route
        path="/tickets"
        element={
          <ClientLayout>
            <CustomerTicketsPage />
          </ClientLayout>
        }
      />
      <Route
        path="/tickets/:id"
        element={
          <ClientLayout>
            <CustomerTicketDetailPage />
          </ClientLayout>
        }
      />

      {/* Orders */}
      <Route
        path="/orders/history"
        element={
          <ClientLayout>
            <OrderHistoryPage />
          </ClientLayout>
        }
      />
      <Route
        path="/orders/:id"
        element={
          <ClientLayout>
            <OrderDetailPage />
          </ClientLayout>
        }
      />

      {/* Products */}
      <Route
        path="/admin/products"
        element={
          <AdminLayout>
            <ProductsPage />
          </AdminLayout>
        }
      />
      <Route
        path="/admin/products/add"
        element={
          <AdminLayout>
            <ProductAdd />
          </AdminLayout>
        }
      />
      <Route
        path="/admin/products/:id"
        element={
          <AdminLayout>
            <ProductDetail />
          </AdminLayout>
        }
      />
      <Route
        path="/admin/products/:id/variants/:variantId"
        element={
          <AdminLayout>
            <VariantDetail />
          </AdminLayout>
        }
      />

      {/* Categories */}
      <Route
        path="/admin/categories"
        element={
          <AdminLayout>
            <CategoryPage />
          </AdminLayout>
        }
      />
      {/* FAQs */}
      <Route
        path="/admin/faqs"
        element={
          <AdminLayout>
            <FaqsPage />
          </AdminLayout>
        }
      />

      {/* Admin Routes */}
      <Route
        path="/admin-dashboard"
        element={
          <AdminLayout>
            <Page404 />
          </AdminLayout>
        }
      />
      <Route
        path="/admin/users"
        element={
          <AdminLayout>
            <AdminUserManagement />
          </AdminLayout>
        }
      />
      <Route
        path="/admin-user-management"
        element={
          <AdminLayout>
            <AdminUserManagement />
          </AdminLayout>
        }
      />
      <Route
        path="/role-manage"
        element={
          <AdminLayout>
            <RoleManage />
          </AdminLayout>
        }
      />
      <Route
        path="/role-assign"
        element={
          <AdminLayout>
            <RoleAssign />
          </AdminLayout>
        }
      />
      {/* Post Routes */}
      <Route
        path="admin-post-list"
        element={
          <AdminLayout>
            <AdminPostList />
          </AdminLayout>
        }
      />
      <Route
        path="post-create-edit"
        element={
          <AdminLayout>
            <PostCreateEdit />
          </AdminLayout>
        }
      />
      <Route
        path="post-create-edit/:postId"
        element={
          <AdminLayout>
            <PostCreateEdit />
          </AdminLayout>
        }
      />
      <Route
        path="tag-post-type-manage"
        element={
          <AdminLayout>
            <TagPostTypeManage />
          </AdminLayout>
        }
      />

      {/* 404 - Default to Client Layout - Fallbacks*/}
      <Route path="*" element={<Page404 />} />

      {/* Suppliers */}
      <Route
        path="/suppliers"
        element={
          <AdminLayout>
            <SuppliersPage />
          </AdminLayout>
        }
      />
      <Route
        path="/suppliers/add"
        element={
          <AdminLayout>
            <SupplierDetailPage />
          </AdminLayout>
        }
      />
      <Route
        path="/suppliers/:id"
        element={
          <AdminLayout>
            <SupplierDetailPage />
          </AdminLayout>
        }
      />

      {/* Product Keys */}
      <Route
        path="/keys"
        element={
          <AdminLayout>
            <KeyManagementPage />
          </AdminLayout>
        }
      />
      <Route
        path="/keys/add"
        element={
          <AdminLayout>
            <KeyDetailPage />
          </AdminLayout>
        }
      />
      <Route
        path="/keys/:id"
        element={
          <AdminLayout>
            <KeyDetailPage />
          </AdminLayout>
        }
      />

      {/* Key Monitor */}
      <Route
        path="/key-monitor"
        element={
          <AdminLayout>
            <KeyMonitorPage />
          </AdminLayout>
        }
      />

      {/* Product Accounts */}
      <Route
        path="/accounts"
        element={
          <AdminLayout>
            <AccountManagementPage />
          </AdminLayout>
        }
      />
      <Route
        path="/accounts/add"
        element={
          <AdminLayout>
            <AccountDetailPage />
          </AdminLayout>
        }
      />
      <Route
        path="/accounts/:id"
        element={
          <AdminLayout>
            <AccountDetailPage />
          </AdminLayout>
        }
      />

      {/* RBAC & Users (duplicated paths giữ nguyên) */}
      <Route
        path="/admin/users"
        element={
          <AdminLayout>
            <AdminUserManagement />
          </AdminLayout>
        }
      />
      <Route
        path="/admin-user-management"
        element={
          <AdminLayout>
            <AdminUserManagement />
          </AdminLayout>
        }
      />

      <Route
        path="/admin/website-config"
        element={
          <AdminLayout>
            <WebsiteConfig />
          </AdminLayout>
        }
      />

      <Route
        path="/blogs"
        element={
          <ClientLayout>
            <BlogList />
          </ClientLayout>
        }
      />

      {/* Fallbacks */}
      <Route path="*" element={<Page404 />} />
    </Routes>
  );
}
