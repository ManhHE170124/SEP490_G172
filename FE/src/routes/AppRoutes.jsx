/**
 * File: AppRoutes.jsx
 * Author: Keytietkiem Team
 * Created: 18/10/2025
 * Last Updated: 25/10/2025
 * Version: 1.0.0
 * Purpose: Application routes with layout separation (Client and Admin)
 */
import { Routes, Route } from "react-router-dom";
import AdminLayout from "../layout/AdminLayout/AdminLayout";
// import ClientLayout from "../layout/ClientLayout/ClientLayout";
import RoleAssign from "../pages/RoleManage/RoleAssign";
import RoleManage from "../pages/RoleManage/RoleManage";
import Page404 from "../pages/NotFound/Page404";
import AdminUserManagement from "../pages/admin-user-management";
import AdminPostList from "../pages/PostManage/AdminPostList"
import PostCreateEdit from "../pages/PostManage/CreateEditPost"
/**
 * @summary: Configure and render application routes with appropriate layouts.
 * @returns {JSX.Element} - Routes configuration with ClientLayout and AdminLayout
 */
export default function AppRoutes() {
  return (
    <Routes>
      {/* Default Access Routes */}
      <Route path="/" element={<AdminLayout> {/* Todo: Link User Hompage and Client Layout */} </AdminLayout>} />

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
      {/* 404 - Default to Client Layout */}
      <Route path="*" element={  <Page404 /> } /> </Routes>
  );
}