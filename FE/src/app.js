/**
 * File: app.js
 * Purpose: Application routes for Keytietkiem admin panel.
 * Notes: Routes the User Management page at /admin/users.
 */
import Sidebar from "./layout/Sidebar.jsx";
import Header from "./layout/Header.jsx";
import AppRoutes from "./routes/AppRoutes";
import "./App.css";

const App = () => {
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
