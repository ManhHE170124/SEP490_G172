/**
 * File: AppRoutes.jsx
 * Author: Keytietkiem Team
 * Created: 18/10/2025
 * Last Updated: 15/12/2025
 * Version: 1.0.1
 * Purpose: Application routes with layout separation (Client and Admin)
 */
import { Route, Routes } from "react-router-dom";
import { Suspense, lazy } from "react";
import AdminLayout from "../layout/AdminLayout/AdminLayout";
import ClientLayout from "../layout/ClientLayout/ClientLayout";
import Page404 from "../pages/NotFound/Page404";
import ProtectedRoute from "./ProtectedRoute";
import UserProfilePage from "../pages/profile/UserProfilePage.jsx";
import OrderHistoryDetailPage from "../pages/orders/OrderHistoryDetailPage.jsx";

//Role Management Pages
import RoleManage from "../pages/RoleManage/RoleManage";
// Post Management Pages
import AdminPostList from "../pages/PostManage/AdminPostList";
import PostCreateEdit from "../pages/PostManage/CreateEditPost";
import TagPostTypeManage from "../pages/PostManage/TagAndPostTypeManage";
import PostDashboardPage from "../pages/PostManage/PostDashboardPage";
import AdminPaymentsDashboardPage from "../pages/admin/AdminPaymentsDashboardPage";

// Admin pages
import CategoryPage from "../pages/admin/CategoryPage.jsx";
import ProductAdd from "../pages/admin/ProductAdd.jsx";
import ProductDetail from "../pages/admin/ProductDetail.jsx";
import ProductsPage from "../pages/admin/ProductsPage.jsx";
import AdminUserManagement from "../pages/admin/admin-user-management";
import AdminTicketManagement from "../pages/admin/admin-ticket-management";
import WebsiteConfig from "../pages/admin/WebsiteConfig";
import FaqsPage from "../pages/admin/FaqsPage.jsx";
import AdminProfilePage from "../pages/admin/AdminProfilePage";
import AdminOrderListPage from "../pages/admin/AdminOrderListPage.jsx";
import AdminOrderDetailPage from "../pages/admin/AdminOrderDetailPage.jsx";
import AdminPaymentListPage from "../pages/admin/AdminPaymentListPage.jsx";
import AdminNotificationsPage from "../pages/admin/AdminNotificationsPage.jsx";
import AdminHomePage from "../pages/admin/AdminHomePage.jsx";
import PolicyPage from "../pages/admin/PolicyPage.jsx";
import UserGuidePage from "../pages/admin/UserGuidePage.jsx";
import AboutUsPage from "../pages/admin/AboutUsPage.jsx";

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
import StorefrontHomepagePage from "../pages/storefront/StorefrontHomepagePage.jsx";
import StorefrontProductDetailPage from "../pages/storefront/StorefrontProductDetailPage.jsx";
import StorefrontCartPage from "../pages/storefront/StorefrontCartPage.jsx";
import CartPaymentCancelPage from "../pages/storefront/CartPaymentCancelPage.jsx";
import CartPaymentResultPage from "../pages/storefront/CartPaymentResultPage.jsx";
import BlogDetail from "../pages/blog/BlogDetail.jsx";

// Customer ticket pages
import CustomerTicketCreatePage from "../pages/tickets/customer-ticket-create";
import CustomerTicketDetailPage from "../pages/tickets/customer-ticket-detail.jsx";
import CustomerTicketManagementPage from "../pages/tickets/customer-ticket-management.jsx";

import AdminSupportChatPage from "../pages/admin/admin-support-chat";
import StaffSupportChatPage from "../pages/admin/staff-support-chat";

// Subscription (Support Plan)
import SupportPlanSubscriptionPage from "../pages/subscription/SupportPlanSubscriptionPage.jsx";

// Product Report Pages
import ProductReportManagementPage from "../pages/report/ProductReportManagementPage.jsx";
import ProductReportDetailPage from "../pages/report/ProductReportDetailPage.jsx";

import SupportPriorityLoyaltyRulesPage from "../pages/admin/SupportPriorityLoyaltyRulesPage.jsx";
import SupportPlansAdminPage from "../pages/admin/SupportPlansAdminPage.jsx";
import TicketSubjectTemplatesAdminPage from "../pages/admin/TicketSubjectTemplatesAdminPage.jsx";
import SlaRulesAdminPage from "../pages/admin/SlaRulesAdminPage.jsx";
import AuditLogsPage from "../pages/admin/AuditLogsPage.jsx";

import SupportDashboardAdminPage from "../pages/admin/SupportDashboardAdminPage";

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

// Role constants
const ROLES = {
  ADMIN: "ADMIN",
  STORAGE_STAFF: "STORAGE_STAFF",
  CUSTOMER_CARE: "CUSTOMER_CARE",
  CONTENT_CREATOR: "CONTENT_CREATOR",
};

// Helper function để tạo role arrays
const role = (...roles) => roles;

export default function AppRoutes() {
  // Helper function để render admin page với allowedRoles
  const renderAdminPage = (component, allowedRoles) => (
    <ProtectedRoute allowedRoles={allowedRoles}>
      <AdminLayout>{component}</AdminLayout>
    </ProtectedRoute>
  );

  return (
    <Routes>
      {/* Default Access Routes */}
      <Route
        path="/"
        element={
          <ClientLayout>
            <StorefrontHomepagePage />
          </ClientLayout>
        }
      />

      {/* Auth */}
      <Route path="/login" element={<ClientLayout><LoginPage /></ClientLayout>} />
      <Route path="/register" element={<ClientLayout><SignUpPage /></ClientLayout>} />
      <Route path="/forgot-password" element={<ClientLayout><ForgotPasswordPage /></ClientLayout>} />
      <Route path="/check-reset-email" element={<ClientLayout><CheckEmailPage /></ClientLayout>} />
      <Route path="/reset-password" element={<ClientLayout><ResetPasswordPage /></ClientLayout>} />

      {/* Profile */}
      <Route path="/account/profile" element={<ClientLayout><UserProfilePage /></ClientLayout>} />
      <Route path="/profile" element={<ClientLayout><UserProfilePage /></ClientLayout>} />

      {/* Order history (cũ) */}
      <Route path="/orderhistory/:id" element={<ClientLayout><OrderHistoryDetailPage /></ClientLayout>} />

      {/* Admin Profile */}
      <Route path="/admin/profile" element={renderAdminPage(<AdminProfilePage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF, ROLES.CUSTOMER_CARE, ROLES.CONTENT_CREATOR))} />
      <Route path="/staff/profile" element={renderAdminPage(<AdminProfilePage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF, ROLES.CUSTOMER_CARE, ROLES.CONTENT_CREATOR))} />

      {/* Admin Home Dashboard */}
      <Route
        path="/admin/home"
        element={renderAdminPage(<AdminHomePage />, role(ROLES.ADMIN))}
      />

      {/* Admin Tickets */}
      <Route
        path="/admin/tickets"
        element={renderAdminPage(<AdminTicketManagement />, role(ROLES.ADMIN))}
      />
      <Route
        path="/admin/tickets/:id"
        element={
          <ProtectedRoute allowedRoles={role(ROLES.ADMIN)}>
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
        element={renderAdminPage(<StaffTicketManagement />, role(ROLES.ADMIN, ROLES.CUSTOMER_CARE))}
      />
      <Route
        path="/staff/tickets/:id"
        element={
          <ProtectedRoute allowedRoles={role(ROLES.ADMIN, ROLES.CUSTOMER_CARE)}>
            <Suspense fallback={<div>Đang tải chi tiết...</div>}>
              <AdminLayout>
                <StaffTicketDetail />
              </AdminLayout>
            </Suspense>
          </ProtectedRoute>
        }
      />

      {/* Product Reports */}
      <Route
        path="/reports"
        element={renderAdminPage(<ProductReportManagementPage />, role(ROLES.ADMIN, ROLES.CUSTOMER_CARE))}
      />
      <Route
        path="/reports/:id"
        element={renderAdminPage(<ProductReportDetailPage />, role(ROLES.ADMIN, ROLES.CUSTOMER_CARE, ROLES.STORAGE_STAFF))}
      />

      {/* Customer tickets */}
      <Route
        path="/tickets/create"
        element={
          <ProtectedRoute>
            <ClientLayout>
              <CustomerTicketCreatePage />
            </ClientLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/tickets/:id"
        element={
          <ProtectedRoute>
            <ClientLayout>
              <CustomerTicketDetailPage />
            </ClientLayout>
          </ProtectedRoute>
        }
      />
      <Route
        path="/tickets"
        element={
          <ProtectedRoute>
            <ClientLayout>
              <CustomerTicketManagementPage />
            </ClientLayout>
          </ProtectedRoute>
        }
      />

      {/* Products */}
      <Route
        path="/admin/products"
        element={renderAdminPage(<ProductsPage />, role(ROLES.ADMIN))}
      />
      <Route
        path="/admin/products/add"
        element={renderAdminPage(<ProductAdd />, role(ROLES.ADMIN))}
      />
      <Route
        path="/admin/products/:id"
        element={renderAdminPage(<ProductDetail />, role(ROLES.ADMIN))}
      />
      <Route
        path="/admin/products/:id/variants/:variantId"
        element={renderAdminPage(<VariantDetail />, role(ROLES.ADMIN))}
      />

      <Route
        path="/admin/categories"
        element={renderAdminPage(<CategoryPage />, role(ROLES.ADMIN))}
      />
      <Route
        path="/admin/orders"
        element={renderAdminPage(<AdminOrderListPage />, role(ROLES.ADMIN))}
      />
      <Route
        path="/admin/payments"
        element={renderAdminPage(<AdminPaymentListPage />, role(ROLES.ADMIN))}
      />
      <Route
  path="/admin/payments/dashboard"
  element={renderAdminPage(<AdminPaymentsDashboardPage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF))}
/>

<Route
        path="/admin/orders/:id"
        element={renderAdminPage(<AdminOrderDetailPage />, role(ROLES.ADMIN))}
      />
      {/* FAQs */}
      <Route
        path="/admin/faqs"
        element={renderAdminPage(<FaqsPage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF))}
      />

      {/* Admin Routes */}
      <Route
        path="/admin-dashboard"
        element={renderAdminPage(<Page404 />, role(ROLES.ADMIN))}
      />
      <Route
        path="/admin/users"
        element={renderAdminPage(<AdminUserManagement />, role(ROLES.ADMIN))}
      />
      <Route
        path="/role-manage"
        element={renderAdminPage(<RoleManage />, role(ROLES.ADMIN))}
      />
      {/* Post Routes */}
      <Route
        path="/post-dashboard"
        element={renderAdminPage(<PostDashboardPage />, role(ROLES.ADMIN, ROLES.CONTENT_CREATOR))}
      />
      <Route
        path="/admin-post-list"
        element={renderAdminPage(<AdminPostList />, role(ROLES.ADMIN, ROLES.CONTENT_CREATOR))}
      />
      <Route
        path="/post-create-edit"
        element={renderAdminPage(<PostCreateEdit />, role(ROLES.ADMIN, ROLES.CONTENT_CREATOR))}
      />
      <Route
        path="/post-create-edit/:postId"
        element={renderAdminPage(<PostCreateEdit />, role(ROLES.ADMIN, ROLES.CONTENT_CREATOR))}
      />
      <Route
        path="/tag-post-type-manage"
        element={renderAdminPage(<TagPostTypeManage />, role(ROLES.ADMIN, ROLES.CONTENT_CREATOR))}
      />
      {/* Static Content Routes */}
      <Route
        path="/admin/policy"
        element={renderAdminPage(<PolicyPage />, role(ROLES.ADMIN, ROLES.CONTENT_CREATOR))}
      />
      <Route
        path="/admin/user-guide"
        element={renderAdminPage(<UserGuidePage />, role(ROLES.ADMIN, ROLES.CONTENT_CREATOR))}
      />
      <Route
        path="/admin/about-us"
        element={renderAdminPage(<AboutUsPage />, role(ROLES.ADMIN, ROLES.CONTENT_CREATOR))}
      />
      {/* Suppliers */}
      <Route
        path="/suppliers"
        element={renderAdminPage(<SuppliersPage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF))}
      />
      <Route
        path="/suppliers/add"
        element={renderAdminPage(<SupplierDetailPage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF))}
      />
      <Route
        path="/suppliers/:id"
        element={renderAdminPage(<SupplierDetailPage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF))}
      />

      {/* Product Keys */}
      <Route
        path="/keys"
        element={renderAdminPage(<KeyManagementPage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF))}
      />
      <Route
        path="/keys/add"
        element={renderAdminPage(<KeyDetailPage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF))}
      />
      <Route
        path="/keys/:id"
        element={renderAdminPage(<KeyDetailPage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF))}
      />

      {/* Key Monitor */}
      <Route
        path="/key-monitor"
        element={renderAdminPage(<KeyMonitorPage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF))}
      />

      {/* Product Accounts */}
      <Route path="/accounts" element={renderAdminPage(<AccountManagementPage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF))} />
      <Route path="/accounts/add" element={renderAdminPage(<AccountDetailPage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF))} />
      <Route path="/accounts/:id" element={renderAdminPage(<AccountDetailPage />, role(ROLES.ADMIN, ROLES.STORAGE_STAFF))} />

      {/* Settings */}
      <Route path="/admin/website-config" element={renderAdminPage(<WebsiteConfig />, role(ROLES.ADMIN))} />
       <Route
        path="/admin/notifications"
        element={renderAdminPage(<AdminNotificationsPage />, role(ROLES.ADMIN))}
      />
      <Route
        path="/admin/support-dashboard"
        element={renderAdminPage(<SupportDashboardAdminPage />, role(ROLES.ADMIN, ROLES.CUSTOMER_CARE))}
      />
      <Route
        path="/admin/support-chats"
        element={renderAdminPage(<AdminSupportChatPage />, role(ROLES.ADMIN))}
      />
      <Route
        path="/staff/support-chats"
        element={renderAdminPage(<StaffSupportChatPage />, role(ROLES.ADMIN, ROLES.CUSTOMER_CARE))}
      />
      {/* Support plan subscription */}
      <Route
        path="/support/subscription"
        element={
          <ClientLayout>
            <SupportPlanSubscriptionPage />
          </ClientLayout>
        }
      />
      <Route
        path="/admin/support-priority-loyalty-rules"
        element={renderAdminPage(<SupportPriorityLoyaltyRulesPage />, role(ROLES.ADMIN))}
      />
      <Route
        path="/admin/support-plans"
        element={renderAdminPage(<SupportPlansAdminPage />, role(ROLES.ADMIN))}
      />
      <Route
        path="/admin/sla-rules"
        element={renderAdminPage(<SlaRulesAdminPage />, role(ROLES.ADMIN))}
      />
      <Route
        path="/admin/ticket-subject-templates"
        element={renderAdminPage(<TicketSubjectTemplatesAdminPage />, role(ROLES.ADMIN))}
      />
      <Route
        path="/admin/audit-logs"
        element={renderAdminPage(<AuditLogsPage />, role(ROLES.ADMIN))}
      />

      {/* Blogs / Products / Cart */}
      <Route path="/blogs" element={<ClientLayout><BlogList /></ClientLayout>} />
      <Route path="/blog/:slug" element={<ClientLayout><BlogDetail /></ClientLayout>} />
      <Route path="/products" element={<ClientLayout><StorefrontProductListPage /></ClientLayout>} />
      <Route path="/products/:productId" element={<ClientLayout><StorefrontProductDetailPage /></ClientLayout>} />
      <Route path="/cart" element={<ClientLayout><StorefrontCartPage /></ClientLayout>} />

      {/* ✅ BE default PayOS redirect */}
      <Route path="/checkout/return" element={<ClientLayout><CartPaymentResultPage /></ClientLayout>} />
      <Route path="/checkout/cancel" element={<ClientLayout><CartPaymentCancelPage /></ClientLayout>} />

      {/* ✅ Alias route (giữ nếu FE đang dùng link cũ) */}
      <Route path="/cart/payment-result" element={<ClientLayout><CartPaymentResultPage /></ClientLayout>} />
      <Route path="/cart/payment-cancel" element={<ClientLayout><CartPaymentCancelPage /></ClientLayout>} />

      <Route path="/homepage" element={<ClientLayout><StorefrontHomepagePage /></ClientLayout>} />

      <Route path="/access-denied" element={<ClientLayout><AccessDenied /></ClientLayout>} />

      {/* Fallback */}
      <Route path="*" element={<Page404 />} />
    </Routes>
  );
}
