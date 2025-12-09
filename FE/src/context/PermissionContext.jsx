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
  moduleAccessPermissions: null, // Map of moduleCode -> has ACCESS permission
  allPermissions: null, // Map of moduleCode -> Set of permissionCodes
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
      // If role is a string, use it directly (should be role code)
      if (typeof role === "string") return role;
      // If role is an object, try to extract code
      if (typeof role === "object") {
        return role.code || role.roleCode || role.roleId || role.name || null;
      }
      return null;
    })
    .filter(Boolean)
    .map((code) => String(code).trim().toUpperCase());
};

export const PermissionProvider = ({ children }) => {
  const [allowedModuleCodes, setAllowedModuleCodes] = useState(null);
  const [moduleAccessPermissions, setModuleAccessPermissions] = useState(null); // Map of moduleCode -> boolean
  const [allPermissions, setAllPermissions] = useState(null); // Map of moduleCode -> Set of permissionCodes
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

  const fetchModuleAccess = useCallback(async (isInitialLoad = false) => {
    // Check if user is logged in before fetching permissions
    const accessToken = localStorage.getItem("access_token");
    const user = localStorage.getItem("user");
    
    // If no token or user, don't fetch permissions (user is not logged in)
    if (!accessToken || !user) {
      setAllowedModuleCodes(new Set());
      setModuleAccessPermissions(new Map());
      setAllPermissions(new Map());
      setError("");
      setLoading(false);
      return;
    }

    const roleCodes = parseRoleCodes();
    if (roleCodes.length === 0) {
      setAllowedModuleCodes(new Set());
      setModuleAccessPermissions(new Map());
      setAllPermissions(new Map());
      setError("");
      setLoading(false);
      return;
    }

    try {
      // Only set loading to true on initial load to prevent ProtectedRoute from returning null
      // during permission refresh, which could cause page reload
      if (isInitialLoad) {
      setLoading(true);
      }
      setError("");
      
      // Fetch ACCESS permissions for module access (for ProtectedRoute and Sidebar)
      const accessResponse = await roleApi.getModuleAccess(roleCodes, "ACCESS");
      const modules = Array.isArray(accessResponse) ? accessResponse : [];
      const nextSet = new Set(
        modules
          .map((module) => module?.moduleCode)
          .filter(Boolean)
          .map((code) => String(code).trim().toUpperCase())
      );
      setAllowedModuleCodes(nextSet);
      
      // Create a map of moduleCode -> has ACCESS permission
      const accessMap = new Map();
      modules.forEach((module) => {
        if (module?.moduleCode) {
          const code = String(module.moduleCode).trim().toUpperCase();
          accessMap.set(code, true);
        }
      });
      setModuleAccessPermissions(accessMap);

      // Fetch all permissions for the user
      const permissionsResponse = await roleApi.getUserPermissions(roleCodes);
      const permissions = Array.isArray(permissionsResponse?.permissions) 
        ? permissionsResponse.permissions 
        : [];
      
      // Create a map of moduleCode -> Set of permissionCodes
      const permissionsMap = new Map();
      permissions.forEach((perm) => {
        if (perm?.moduleCode && perm?.permissionCode) {
          const moduleCode = String(perm.moduleCode).trim().toUpperCase();
          const permissionCode = String(perm.permissionCode).trim().toUpperCase();
          
          if (!permissionsMap.has(moduleCode)) {
            permissionsMap.set(moduleCode, new Set());
          }
          permissionsMap.get(moduleCode).add(permissionCode);
        }
      });
      setAllPermissions(permissionsMap);
    } catch (err) {
      console.error("Không thể tải quyền module", err);
      
      // Double-check if user is still logged in (might have logged out during fetch)
      const accessToken = localStorage.getItem("access_token");
      const user = localStorage.getItem("user");
      
      // If no token or user, clear permissions (user logged out)
      if (!accessToken || !user) {
        setAllowedModuleCodes(new Set());
        setModuleAccessPermissions(new Map());
        setAllPermissions(new Map());
        setError("");
        return;
      }
      
      // If error is 401 (Unauthorized), user is not logged in or token expired
      // Clear permissions and let axiosClient handle redirect
      if (err?.response?.status === 401) {
        setAllowedModuleCodes(new Set());
        setModuleAccessPermissions(new Map());
        setAllPermissions(new Map());
        setError("");
      } else {
        // Only clear permissions on error if it's an initial load
        // Otherwise keep existing permissions to prevent redirect
        if (isInitialLoad) {
          setAllowedModuleCodes(new Set());
          setModuleAccessPermissions(new Map());
          setAllPermissions(new Map());
        }
        setError("Không thể tải quyền module.");
      }
    } finally {
      if (isInitialLoad) {
      setLoading(false);
    }
    }
  }, [parseRoleCodes]);

  useEffect(() => {
    // Check if we're on a public page (login, register, etc.) before fetching
    const publicPaths = ['/login', '/register', '/forgot-password', '/check-reset-email', '/reset-password'];
    const currentPath = window.location.pathname;
    const isPublicPage = publicPaths.some(path => currentPath.startsWith(path));
    
    // Don't fetch permissions on public pages
    if (isPublicPage) {
      setAllowedModuleCodes(new Set());
      setModuleAccessPermissions(new Map());
      setAllPermissions(new Map());
      setLoading(false);
      return;
    }
    
    // Check if user is logged in before fetching
    const accessToken = localStorage.getItem("access_token");
    const user = localStorage.getItem("user");
    
    // If no token or user, don't fetch permissions
    if (!accessToken || !user) {
      setAllowedModuleCodes(new Set());
      setModuleAccessPermissions(new Map());
      setAllPermissions(new Map());
      setLoading(false);
      return;
    }
    
    // Initial load - set loading to true
    fetchModuleAccess(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // Only run once on mount

  useEffect(() => {
    // Handle logout - clear permissions immediately when tokens are removed
    const handleLogout = () => {
      const accessToken = localStorage.getItem("access_token");
      const user = localStorage.getItem("user");
      
      // If tokens are cleared, clear permissions immediately
      if (!accessToken || !user) {
        setAllowedModuleCodes(new Set());
        setModuleAccessPermissions(new Map());
        setAllPermissions(new Map());
        setError("");
        setLoading(false);
      }
    };

    // Refresh without setting loading to prevent ProtectedRoute from returning null
    const handleRefresh = () => {
      // Check if user is still logged in before refreshing
      const accessToken = localStorage.getItem("access_token");
      const user = localStorage.getItem("user");
      
      // If no token or user, don't refresh (user logged out)
      if (!accessToken || !user) {
        handleLogout();
        return;
      }

      // Always fetch permissions when refresh is triggered (e.g., after login)
      // This ensures permissions are loaded even if we're on a public page
      fetchModuleAccess(false);
    };

    // Listen for storage changes (logout clears localStorage)
    const handleStorageChange = (e) => {
      if (e.key === 'access_token' || e.key === 'user') {
        handleLogout();
      } else if (e.key === null) {
        // localStorage.clear() was called
        handleLogout();
      }
    };

    window.addEventListener("role-permissions-updated", handleRefresh);
    window.addEventListener("storage", handleStorageChange);
    // Also listen for custom logout event
    window.addEventListener("user-logged-out", handleLogout);

    return () => {
      window.removeEventListener("role-permissions-updated", handleRefresh);
      window.removeEventListener("storage", handleStorageChange);
      window.removeEventListener("user-logged-out", handleLogout);
    };
  }, [fetchModuleAccess]);

  const contextValue = useMemo(
    () => ({
      allowedModuleCodes,
      moduleAccessPermissions,
      allPermissions,
      loading,
      error,
      refreshPermissions: fetchModuleAccess,
    }),
    [allowedModuleCodes, moduleAccessPermissions, allPermissions, loading, error, fetchModuleAccess]
  );

  return (
    <PermissionContext.Provider value={contextValue}>
      {children}
    </PermissionContext.Provider>
  );
};

export const usePermissions = () => useContext(PermissionContext);

