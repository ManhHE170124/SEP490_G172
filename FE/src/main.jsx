import React from "react"
import { createRoot } from "react-dom/client"
import { BrowserRouter } from "react-router-dom"
import App from "./App.jsx"
import "./admin.css"
import { ConfirmProvider } from "./components/common/ConfirmProvider.jsx"
createRoot(document.getElementById("root")).render(
  <BrowserRouter>
    <ConfirmProvider>
      <App />
    </ConfirmProvider>
  </BrowserRouter>
)
