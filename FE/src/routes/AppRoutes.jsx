import { Routes, Route } from "react-router-dom";
import RoleAssign from "../pages/RBAC/RoleAssign";
import RBACManagement from "../pages/RBAC/RBACManagement";
import Page404 from "../pages/NotFound/Page404";

export default function AppRoutes() {
  return (
    <Routes>
          <Route path="/" element={<Page404 />} />
          <Route path="/home" element={<Page404 />} />
          <Route path="/rbac" element={<RBACManagement />} />
          <Route path="/roleassign" element={<RoleAssign />} />
          <Route path="*" element={<Page404 />} />
        </Routes>
  );
}