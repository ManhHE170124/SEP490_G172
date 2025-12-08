import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import { roleApi } from "../services/roleApi";

const PermissionContext = createContext({
  allowedModuleCodes: null,
  loading: false,
  error: "",
  refreshPermissions: () => {},
});

const extractRoleCodes = (rawRoles) => {
  if (!Array.isArray(rawRoles)) {
    return [];
  }

  return rawRoles
    .map((role) => {
      if (!role) return null;
      if (typeof role === "string") return role;
      if (typeof role === "object") {
        return role.code || role.roleCode || role.name || null;
      }
      return null;
    })
    .filter(Boolean)
    .map((code) => String(code).trim().toUpperCase());
};

export const PermissionProvider = ({ children }) => {
  const [allowedModuleCodes, setAllowedModuleCodes] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const parseRoleCodes = useCallback(() => {
    try {
      const rawUser = localStorage.getItem("user");
      if (!rawUser) return [];
      const parsed = JSON.parse(rawUser);
      if (Array.isArray(parsed?.roles)) {
        return extractRoleCodes(parsed.roles);
      }
      if (parsed?.role) {
        return [String(parsed.role).trim().toUpperCase()];
      }
      return [];
    } catch (err) {
      console.warn("Không thể đọc dữ liệu người dùng từ localStorage", err);
      return [];
    }
  }, []);

  const fetchModuleAccess = useCallback(async () => {
    const roleCodes = parseRoleCodes();
    if (roleCodes.length === 0) {
      setAllowedModuleCodes(new Set());
      setError("");
      return;
    }

    try {
      setLoading(true);
      setError("");
      const response = await roleApi.getModuleAccess(roleCodes);
      const modules = Array.isArray(response) ? response : [];
      const nextSet = new Set(
        modules
          .map((module) => module?.moduleCode)
          .filter(Boolean)
          .map((code) => String(code).trim().toUpperCase())
      );
      setAllowedModuleCodes(nextSet);
    } catch (err) {
      console.error("Không thể tải quyền module", err);
      setAllowedModuleCodes(new Set());
      setError("Không thể tải quyền module.");
    } finally {
      setLoading(false);
    }
  }, [parseRoleCodes]);

  useEffect(() => {
    fetchModuleAccess();
  }, [fetchModuleAccess]);

  useEffect(() => {
    const handleRefresh = () => fetchModuleAccess();
    window.addEventListener("role-permissions-updated", handleRefresh);
    window.addEventListener("storage", handleRefresh);

    return () => {
      window.removeEventListener("role-permissions-updated", handleRefresh);
      window.removeEventListener("storage", handleRefresh);
    };
  }, [fetchModuleAccess]);

  const contextValue = useMemo(
    () => ({
      allowedModuleCodes,
      loading,
      error,
      refreshPermissions: fetchModuleAccess,
    }),
    [allowedModuleCodes, loading, error, fetchModuleAccess]
  );

  return (
    <PermissionContext.Provider value={contextValue}>
      {children}
    </PermissionContext.Provider>
  );
};

export const usePermissions = () => useContext(PermissionContext);

