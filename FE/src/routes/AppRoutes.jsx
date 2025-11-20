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
import ProtectedRoute from "./ProtectedRoute";
import { MODULE_CODES } from "../constants/accessControl";

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
import AdminSupportChatPage from "../pages/admin/admin-support-chat";
// App.jsx (hoặc routes admin)
import VariantDetail from "../pages/admin/VariantDetail.jsx";
import AccessDenied from "../pages/errors/AccessDenied";

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
import StorefrontProductListPage from "../pages/storefront/StorefrontProductListPage.jsx";

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
  const renderAdminPage = (moduleCode, component) => (
    <ProtectedRoute moduleCode={moduleCode}>
      <AdminLayout>{component}</AdminLayout>
    </ProtectedRoute>
  );

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
      <Route path="/admin/support-chats" element={<AdminSupportChatPage />} />
      {/* Admin Tickets */}
      <Route
        path="/admin/tickets"
        element={renderAdminPage(
          MODULE_CODES.SUPPORT_MANAGER,
          <AdminTicketManagement />
        )}
      />
      <Route
        path="/admin/tickets/:id"
        element={
          <ProtectedRoute moduleCode={MODULE_CODES.SUPPORT_MANAGER}>
            <Suspense fallback={<div>Đang tải chi tiết...</div>}>
              <AdminLayout>
                <AdminTicketDetail />
              </AdminLayout>
            </Suspense>
          </ProtectedRoute>
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
        element={renderAdminPage(
          MODULE_CODES.PRODUCT_MANAGER,
          <ProductsPage />
        )}
      />
      <Route
        path="/admin/products/add"
        element={renderAdminPage(
          MODULE_CODES.PRODUCT_MANAGER,
          <ProductAdd />
        )}
      />
      <Route
        path="/admin/products/:id"
        element={renderAdminPage(
          MODULE_CODES.PRODUCT_MANAGER,
          <ProductDetail />
        )}
      />
      <Route
        path="/admin/products/:id/variants/:variantId"
        element={renderAdminPage(
          MODULE_CODES.PRODUCT_MANAGER,
          <VariantDetail />
        )}
      />

      {/* Categories */}
      <Route
        path="/admin/categories"
        element={renderAdminPage(
          MODULE_CODES.PRODUCT_MANAGER,
          <CategoryPage />
        )}
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
        element={renderAdminPage(null, <Page404 />)}
      />
      <Route
        path="/admin/users"
        element={renderAdminPage(
          MODULE_CODES.USER_MANAGER,
          <AdminUserManagement />
        )}
      />
      <Route
        path="/admin-user-management"
        element={renderAdminPage(
          MODULE_CODES.USER_MANAGER,
          <AdminUserManagement />
        )}
      />
      <Route
        path="/role-manage"
        element={renderAdminPage(
          MODULE_CODES.ROLE_MANAGER,
          <RoleManage />
        )}
      />
      <Route
        path="/role-assign"
        element={renderAdminPage(
          MODULE_CODES.ROLE_MANAGER,
          <RoleAssign />
        )}
      />
      {/* Post Routes */}
      <Route
        path="admin-post-list"
        element={renderAdminPage(
          MODULE_CODES.POST_MANAGER,
          <AdminPostList />
        )}
      />
      <Route
        path="post-create-edit"
        element={renderAdminPage(
          MODULE_CODES.POST_MANAGER,
          <PostCreateEdit />
        )}
      />
      <Route
        path="post-create-edit/:postId"
        element={renderAdminPage(
          MODULE_CODES.POST_MANAGER,
          <PostCreateEdit />
        )}
      />
      <Route
        path="tag-post-type-manage"
        element={renderAdminPage(
          MODULE_CODES.POST_MANAGER,
          <TagPostTypeManage />
        )}
      />

      {/* 404 - Default to Client Layout - Fallbacks*/}
      <Route path="*" element={<Page404 />} />

      {/* Suppliers */}
      <Route
        path="/suppliers"
        element={renderAdminPage(
          MODULE_CODES.WAREHOUSE_MANAGER,
          <SuppliersPage />
        )}
      />
      <Route
        path="/suppliers/add"
        element={renderAdminPage(
          MODULE_CODES.WAREHOUSE_MANAGER,
          <SupplierDetailPage />
        )}
      />
      <Route
        path="/suppliers/:id"
        element={renderAdminPage(
          MODULE_CODES.WAREHOUSE_MANAGER,
          <SupplierDetailPage />
        )}
      />

      {/* Product Keys */}
      <Route
        path="/keys"
        element={renderAdminPage(
          MODULE_CODES.WAREHOUSE_MANAGER,
          <KeyManagementPage />
        )}
      />
      <Route
        path="/keys/add"
        element={renderAdminPage(
          MODULE_CODES.WAREHOUSE_MANAGER,
          <KeyDetailPage />
        )}
      />
      <Route
        path="/keys/:id"
        element={renderAdminPage(
          MODULE_CODES.WAREHOUSE_MANAGER,
          <KeyDetailPage />
        )}
      />

      {/* Key Monitor */}
      <Route
        path="/key-monitor"
        element={renderAdminPage(
          MODULE_CODES.WAREHOUSE_MANAGER,
          <KeyMonitorPage />
        )}
      />

      {/* Product Accounts */}
      <Route
        path="/accounts"
        element={renderAdminPage(
          MODULE_CODES.WAREHOUSE_MANAGER,
          <AccountManagementPage />
        )}
      />
      <Route
        path="/accounts/add"
        element={renderAdminPage(
          MODULE_CODES.WAREHOUSE_MANAGER,
          <AccountDetailPage />
        )}
      />
      <Route
        path="/accounts/:id"
        element={renderAdminPage(
          MODULE_CODES.WAREHOUSE_MANAGER,
          <AccountDetailPage />
        )}
      />

      <Route
        path="/admin/website-config"
        element={renderAdminPage(
          MODULE_CODES.SETTINGS_MANAGER,
          <WebsiteConfig />
        )}
      />

      <Route path="/blogs" element={<ClientLayout><BlogList /></ClientLayout>} />
      <Route
        path="/access-denied"
        element={
          <ClientLayout>
            <AccessDenied />
          </ClientLayout>
        }
      />

      {/* Fallbacks */}
      <Route path="*" element={<Page404 />} />
    </Routes>
  );
}
