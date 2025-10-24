/**
 * File: index.js
 * Purpose: React bootstrapper for Keytietkiem admin app.
 */
import React from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import "./index.css";
import App from "./app";

const container = document.getElementById("root");
createRoot(container).render(
  <React.StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </React.StrictMode>
);
