/**
 * File: app.js
 * Author: Keytietkiem Team
 * Created: 18/10/2025
 * Last Updated: 25/10/2025
 * Version: 1.0.0
 * Purpose: Application root component.
 */
import React from "react";
import AppRoutes from "./routes/AppRoutes";
import "./App.css";

/**
 * @summary: Root application component.
 * @returns {JSX.Element} - The AppRoutes component wrapped in the application
 */
const App = () => {
    return <AppRoutes />;
  
};

export default App;
