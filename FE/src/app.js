import React from "react";
import { BrowserRouter as Router, Route, Routes } from "react-router-dom";
import Sidebar from "./layout/Sidebar.jsx";
import Header from "./layout/Header.jsx";
import AppRoutes from "./routes/AppRoutes";
import "./App.css";

const AppContent = () => {
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

const App = () => {
  return (
    <Router>
      <AppContent />
    </Router>
  );
};

export default App;