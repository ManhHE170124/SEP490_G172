/**
 * File: app.js
 * Author: Keytietkiem Team
 * Created: 18/10/2025
 * Last Updated: 25/10/2025
 * Version: 1.0.0
 * Purpose: Application root component. Renders the main AppRoutes component
 *          which handles all routing with layout separation (Client and Admin).
 */
import React from "react";
import { useLocation } from "react-router-dom";
import AppRoutes from "./routes/AppRoutes";
import "./App.css";

/**
 * @summary: Root application component.
 * @returns {JSX.Element} - The AppRoutes component wrapped in the application
 */
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
};

export default App;
