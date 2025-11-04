/**
 * File: app.js
 * Purpose: Application routes for Keytietkiem admin panel.
 * Notes: Routes the User Management page at /admin/users.
 */
import React from "react";
import { useLocation } from "react-router-dom";
import Sidebar from "./layout/Sidebar.jsx";
import Header from "./layout/Header.jsx";
import AppRoutes from "./routes/AppRoutes";
import "./App.css";

const App = () => {
  const location = useLocation();

  // Public routes that should not show admin layout
  const publicRoutes = [
    "/login",
    "/register",
    "/forgot-password",
    "/check-reset-email",
    "/reset-password",
  ];

  const isPublicRoute = publicRoutes.some((route) =>
    location.pathname.startsWith(route)
  );

  if (isPublicRoute) {
    return <AppRoutes />;
  }

  return (
    <div className="app">
      <Sidebar />
      <div className="content">
        <Header />
        <AppRoutes />
      </div>
    </div>
  );
};

export default App;
