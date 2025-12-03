/**
 * File: index.js
 * Purpose: React bootstrapper for Keytietkiem admin app.
 */
import React from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import "./index.css";
import App from "./app";
import { ModalProvider } from "./components/common/ModalProvider";
import { SettingsProvider } from "./contexts/SettingContext";
import { ToastProvider } from "./contexts/ToastContext";

const container = document.getElementById("root");
createRoot(container).render(
  <React.StrictMode>
    <BrowserRouter>
      <SettingsProvider>
        <ToastProvider>
          <ModalProvider>
            <App />
          </ModalProvider>
        </ToastProvider>
      </SettingsProvider>
    </BrowserRouter>
  </React.StrictMode>
);
