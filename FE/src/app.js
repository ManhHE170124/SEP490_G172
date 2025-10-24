/**
 * File: app.js
 * Purpose: Application routes for Keytietkiem admin panel.
 * Notes: Routes the User Management page at /admin/users.
 */
import React from "react";
import { Routes, Route, Navigate } from "react-router-dom";
import AdminUserManagement from "./pages/admin-user-management";

function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/admin/users" replace />} />
      <Route path="/admin/users" element={<AdminUserManagement />} />
    </Routes>
  );
}
export default App;
