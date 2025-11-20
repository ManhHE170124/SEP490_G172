/**
 * File: RoleAssign.jsx
 * Author: Keytietkiem Team
 * Created: 20/10/2025
 * Last Updated: 25/10/2025
 * Version: 1.0.0
 * Purpose: Role permission assignment page for managing role-permission relationships.
 */
import React, { useEffect, useState, useCallback, useMemo, useRef } from "react";
import { roleApi } from "../../services/roleApi";
import RoleModal from "../../components/RoleModal/RoleModal";
import ToastContainer from "../../components/Toast/ToastContainer";
import useToast from "../../hooks/useToast";
import "./RoleAssign.css";

const ADMIN_ROLE_CODE = "ADMIN";
const HIDDEN_ROLE_CODE = "CUSTOMER";

const filterOutCustomerRoles = (roles) =>
  (Array.isArray(roles) ? roles : []).filter(
    (role) => String(role?.code || "").toUpperCase() !== HIDDEN_ROLE_CODE
  );

/**
 * @summary: Role permission assignment page component.
 * @returns {JSX.Element} - Role assignment interface with permission matrix
 */
export default function RoleAssign() {
  const { toasts, showSuccess, showError, showWarning, removeToast, showConfirm, confirmDialog } = useToast();
  
  // Global network error handler - only show one toast for network errors
  const networkErrorShownRef = useRef(false);
  useEffect(() => {
    // Reset the flag when component mounts
    networkErrorShownRef.current = false;
  }, []);
  
  // State for data
  const [roles, setRoles] = useState([]);
  const [modules, setModules] = useState([]);
  const [permissions, setPermissions] = useState([]);
  const [rolePermissions, setRolePermissions] = useState([]);
  
  // State for UI
  const [selectedRole, setSelectedRole] = useState(null);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  
  // Modal states
  const [addRoleOpen, setAddRoleOpen] = useState(false);
  const [addModuleOpen, setAddModuleOpen] = useState(false);
  const [addPermissionOpen, setAddPermissionOpen] = useState(false);
  const filteredRoles = roles;
  const isAdminRoleSelected =
    selectedRole?.code?.toUpperCase() === ADMIN_ROLE_CODE;

  const totalPermissionSlots = modules.length * permissions.length;

  const activePermissionCount = useMemo(() => {
    if (!selectedRole) return 0;
    if (!Array.isArray(rolePermissions) || rolePermissions.length === 0) return 0;

    return rolePermissions.reduce(
      (sum, rp) => (rp.isActive ? sum + 1 : sum),
      0
    );
  }, [rolePermissions, selectedRole]);

  const activePermissionPercent =
    totalPermissionSlots > 0
      ? Math.round((activePermissionCount / totalPermissionSlots) * 100)
      : 0;

  const filteredRoleCount = filteredRoles.length;
  const isInitialLoading =
    loading && roles.length === 0 && modules.length === 0 && permissions.length === 0;
  const hasMatrixData =
    Boolean(selectedRole) && modules.length > 0 && permissions.length > 0;
  
  /**
   * @summary: Load role-permission matrix for a given role.
   * @param {string} roleId - Role identifier
   * @returns {Promise<void>}
   */
  const loadRolePermissions = useCallback(async (roleId) => {
    try {
      const response = await roleApi.getRolePermissions(roleId);
      setRolePermissions(response.rolePermissions || []);
    } catch (error) {
      // Handle network errors globally - only show one toast
      if (error.isNetworkError || error.message === 'Lỗi kết nối đến máy chủ') {
        if (!networkErrorShownRef.current) {
          networkErrorShownRef.current = true;
          showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
        }
      } else {
        showError("Lỗi tải quyền", error.message || "Không thể tải quyền của Vai trò");
      }
    }
  }, [showError]);
  
  /**
   * @summary: Load initial data for roles, modules, and permissions in parallel.
   * @returns {Promise<void>}
   */
  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      const [rolesData, modulesData, permissionsData] = await Promise.all([
        roleApi.getActiveRoles(),
        roleApi.getModules(),
        roleApi.getPermissions()
      ]);

      const sanitizedRoles = filterOutCustomerRoles(rolesData);
      setRoles(sanitizedRoles);
      setModules(modulesData || []);
      setPermissions(permissionsData || []);

      setSelectedRole((previous) => {
        if (!previous) {
          return sanitizedRoles?.[0] ?? null;
        }
        const stillExists = sanitizedRoles.find(
          (role) => role.roleId === previous.roleId
        );
        return stillExists ?? sanitizedRoles?.[0] ?? null;
      });
    } catch (error) {
      // Handle network errors globally - only show one toast
      if (error.isNetworkError || error.message === 'Lỗi kết nối đến máy chủ') {
        if (!networkErrorShownRef.current) {
          networkErrorShownRef.current = true;
          showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
        }
      } else {
        showError("Lỗi tải dữ liệu", error.message || "Không thể tải dữ liệu");
      }
    } finally {
      setLoading(false);
    }
  }, [showError]);
  
  // Load initial data
  useEffect(() => {
    loadData();
  }, [loadData]);
  
  // Load role permissions when role is selected
  useEffect(() => {
    if (selectedRole) {
      loadRolePermissions(selectedRole.roleId);
      setHasUnsavedChanges(false);
    }
  }, [selectedRole, loadRolePermissions]);
  
  // Create handlers
  const handleCreateRole = async (form) => {
    try {
      setSubmitting(true);
      const created = await roleApi.createRole({ 
        name: form.name, 
        code: form.code,
        isSystem: form.isSystem || false 
      });
      setRoles((prev) => filterOutCustomerRoles([...(prev || []), created]));
      if (String(created?.code || "").toUpperCase() !== HIDDEN_ROLE_CODE) {
        setSelectedRole(created);
      }
      setHasUnsavedChanges(false);
      setAddRoleOpen(false);
      showSuccess(
        "Tạo Vai trò thành công!",
        `Vai trò "${form.name}" đã được tạo và tự động gán quyền cho tất cả Mô-đun và Quyền.`
      );
    } catch (error) {
      // Handle network errors globally - only show one toast
      if (error.isNetworkError || error.message === 'Lỗi kết nối đến máy chủ') {
        if (!networkErrorShownRef.current) {
          networkErrorShownRef.current = true;
          showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
        }
      } else {
        const errorMessage = error.response?.data?.message || error.message || "Không thể tạo Vai trò";
        showError("Tạo Vai trò thất bại!", errorMessage);
      }
    } finally {
      setSubmitting(false);
    }
  };
  
  /**
   * @summary: Create a new Module entity and add into local state.
   * @param {{ moduleName: string, description?: string }} form - Module payload
   * @returns {Promise<void>}
   */
  const handleCreateModule = async (form) => {
    try {
      setSubmitting(true);
      const created = await roleApi.createModule({ 
        moduleName: form.moduleName,
        code: form.code,
        description: form.description || ""
      });
      setModules(prev => [...prev, created]);
      setAddModuleOpen(false);
      showSuccess(
        "Tạo Mô-đun thành công!",
        `Mô-đun "${form.moduleName}" đã được tạo và tự động gán quyền cho tất cả Vai trò và Quyền.`
      );
    } catch (error) {
      // Handle network errors globally - only show one toast
      if (error.isNetworkError || error.message === 'Lỗi kết nối đến máy chủ') {
        if (!networkErrorShownRef.current) {
          networkErrorShownRef.current = true;
          showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
        }
      } else {
        const errorMessage = error.response?.data?.message || error.message || "Không thể tạo Mô-đun";
        showError("Tạo Mô-đun thất bại!", errorMessage);
      }
    } finally {
      setSubmitting(false);
    }
  };
  
  /**
   * @summary: Create a new Permission entity and add into local state.
   * @param {{ permissionName: string, description?: string }} form - Permission payload
   * @returns {Promise<void>}
   */
  const handleCreatePermission = async (form) => {
    try {
      setSubmitting(true);
      const created = await roleApi.createPermission({ 
        permissionName: form.permissionName, 
        code: form.code,
        description: form.description || ""
      });
      setPermissions(prev => [...prev, created]);
      setAddPermissionOpen(false);
      showSuccess(
        "Tạo Quyền thành công!",
        `Quyền "${form.permissionName}" đã được tạo và tự động gán quyền cho tất cả Vai trò và Mô-đun.`
      );
    } catch (error) {
      // Handle network errors globally - only show one toast
      if (error.isNetworkError || error.message === 'Lỗi kết nối đến máy chủ') {
        if (!networkErrorShownRef.current) {
          networkErrorShownRef.current = true;
          showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
        }
      } else {
        const errorMessage = error.response?.data?.message || error.message || "Không thể tạo Quyền";
        showError("Tạo Quyền thất bại!", errorMessage);
      }
    } finally {
      setSubmitting(false);
    }
  };
  
  /**
   * @summary: Select a role for editing its permissions. Warn on unsaved changes.
   * @param {Object} role - Role object
   * @returns {void}
   */
  const handleRoleSelect = (role) => {
    const isSameRole = selectedRole?.roleId === role.roleId;
    if (isSameRole) return;

    if (hasUnsavedChanges) {
      showConfirm(
        "Chưa lưu thay đổi",
        `Bạn có chắc muốn chuyển sang vai trò "${role.name}"? Những thay đổi hiện tại sẽ bị mất.`,
        () => {
          setSelectedRole(role);
        }
      );
      return;
    }

    setSelectedRole(role);
  };
  
  /**
   * @summary: Cancel edits and reload role-permissions from server.
   * @returns {Promise<void>}
   */
  const handleCancel = async () => {
    if (!selectedRole || isAdminRoleSelected) return;
    
    try {
      await loadRolePermissions(selectedRole.roleId);
      setHasUnsavedChanges(false);
      showSuccess("Đã hủy thay đổi", "Ma trận Quyền đã được reset về trạng thái ban đầu");
    } catch (error) {
      console.error("Lôi khi hủy thay đổi:", error);
      // Handle network errors globally - only show one toast
      if (error.isNetworkError || error.message === 'Lỗi kết nối đến máy chủ') {
        if (!networkErrorShownRef.current) {
          networkErrorShownRef.current = true;
          showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
        }
      } else {
        showError("Lỗi khi hủy", "Không thể reset ma trận Quyền");
      }
      throw error;
    }
  };
  
  /**
   * @summary: Toggle permission state locally (not persisted until saved).
   * @param {number} moduleId - Module identifier
   * @param {number} permissionId - Permission identifier
   * @returns {void}
   */ 
  const handlePermissionToggle = (moduleId, permissionId) => {
    if (!selectedRole || isAdminRoleSelected) return;
    
    setRolePermissions(prev => {
      // Find existing permission for this module-permission combination
      const existing = prev.find(rp => 
        rp.moduleId === moduleId && rp.permissionId === permissionId
      );
      
      if (existing) {
        // Toggle existing permission - flip the isActive status
        return prev.map(rp => 
          rp.moduleId === moduleId && rp.permissionId === permissionId
            ? { ...rp, isActive: !rp.isActive }
            : rp
        );
      } else {
        // Add new permission - create new role permission entry
        return [...prev, {
          roleId: selectedRole.roleId,
          moduleId,
          permissionId,
          isActive: true
        }];
      }
    });
    
    // Mark as having unsaved changes - this triggers save button activation
    setHasUnsavedChanges(true);
  };
  
  /**
   * @summary: Check if a permission is active for the selected role and module.
   * @param {number} moduleId - Module identifier
   * @param {number} permissionId - Permission identifier
   * @returns {boolean}
   */
  const isPermissionActive = (moduleId, permissionId) => {
    const rolePermission = rolePermissions.find(rp => 
      rp.moduleId === moduleId && rp.permissionId === permissionId
    );
    return rolePermission ? rolePermission.isActive : false;
  };
  
  /**
   * @summary: Save all permission changes by submitting a full matrix to server.
   * @returns {Promise<void>}
   */
    const handleSaveChanges = async () => {
    if (!selectedRole) {
      showWarning("Chưa chọn Vai trò", "Vui lòng chọn một Vai trò để lưu thay đổi");
      return;
    }
    if (isAdminRoleSelected) {
      showWarning("Quản trị viên", "Quản trị viên đã có toàn quyền hệ thống.");
      return;
    }
    
    try {
      setSubmitting(true);
      
      // Prepare complete role permissions matrix - every module x permission combination
      const allRolePermissions = [];
      for (const module of modules) {
        for (const permission of permissions) {          
          // Check if this combination exists in current state
          const existing = rolePermissions.find(rp => 
            rp.moduleId === module.moduleId && rp.permissionId === permission.permissionId
          );    

          // Add to matrix - use existing state or default to false
          allRolePermissions.push({
            roleId: selectedRole.roleId,
            moduleId: module.moduleId,
            permissionId: permission.permissionId,
            isActive: existing ? existing.isActive : false
          });
        }
      }

      // Send complete matrix to server
      await roleApi.updateRolePermissions(selectedRole.roleId, {
        roleId: selectedRole.roleId,
        rolePermissions: allRolePermissions
      });
      
      // Reload from server to get authoritative state
      await loadRolePermissions(selectedRole.roleId);
      
      // Clear unsaved changes flag
      setHasUnsavedChanges(false);
      
      showSuccess(
        "Lưu thay đổi thành công!",
        `Đã cập nhật tất cả quyền cho Vai trò "${selectedRole.name}"`
      );
      window.dispatchEvent(new Event("role-permissions-updated"));
    } catch (error) {
      // Handle network errors globally - only show one toast
      if (error.isNetworkError || error.message === 'Lỗi kết nối đến máy chủ') {
        if (!networkErrorShownRef.current) {
          networkErrorShownRef.current = true;
          showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
        }
      } else {
        const errorMessage = error.response?.data?.message || error.message || "Không thể lưu thay đổi";
        showError("Lưu thay đổi thất bại!", errorMessage);
      }
    } finally {
      setSubmitting(false);
    }
  };
  
  /**
   * @summary: Toggle all permissions (select/deselect all) in local state.
   * @returns {void}
   */
  const handleTickAll = () => {
    if (!selectedRole || isAdminRoleSelected) return;
    
    const allActive = permissions.every(permission => 
      modules.every(module => isPermissionActive(module.moduleId, permission.permissionId))
    );
    
    const newIsActive = !allActive;
    
    // Update local state only
    const allRolePermissions = [];
    for (const module of modules) {
      for (const permission of permissions) {
        allRolePermissions.push({
          roleId: selectedRole.roleId,
          moduleId: module.moduleId,
          permissionId: permission.permissionId,
          isActive: newIsActive
        });
      }
    }
    
    setRolePermissions(allRolePermissions);
    
    // Mark as having unsaved changes
    setHasUnsavedChanges(true);
  };
  
  /**
   * @summary: Determine if all permissions are currently selected for the role.
   * @returns {boolean}
   */
  const isAllTicked = permissions.every(permission => 
    modules.every(module => isPermissionActive(module.moduleId, permission.permissionId))
  );
  
  if (isInitialLoading) {
    return (
      <div className="ra-page ra-page--loading">
        <output className="ra-loading-card" aria-live="polite">
          <div className="ra-spinner" aria-hidden="true" />
          <p>Đang tải dữ liệu...</p>
        </output>
      </div>
    );
  }
  
  let permissionContent;
  if (hasMatrixData) {
    permissionContent = (
      <table className="ra-permissions-table">
        <thead>
          <tr>
            <th scope="col">Mô-đun\Quyền</th>
            {permissions.map((permission) => (
              <th
                key={permission.permissionId}
                scope="col"
                className="ra-permission-name"
              >
                {permission.permissionName}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {modules.map((module) => (
            <tr key={module.moduleId}>
              <th scope="row" className="ra-module-name">
                {module.moduleName}
              </th>
              {permissions.map((permission) => (
                <td key={`${permission.permissionId}-${module.moduleId}`}>
                  <input
                    type="checkbox"
                    className="ra-permission-checkbox"
                    checked={isPermissionActive(
                      module.moduleId,
                      permission.permissionId
                    )}
                    disabled={isAdminRoleSelected}
                    onChange={() =>
                      handlePermissionToggle(
                        module.moduleId,
                        permission.permissionId
                      )
                    }
                  />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    );
  } else if (selectedRole) {
    permissionContent = (
      <div className="ra-empty ra-empty--center">
        <h3>Chưa có dữ liệu phân quyền</h3>
        <p>Hãy tạo thêm mô-đun hoặc quyền để cấu hình cho vai trò này.</p>
      </div>
    );
  } else {
    permissionContent = (
      <div className="ra-empty ra-empty--center">
        <h3>Chưa chọn vai trò</h3>
        <p>Chọn một vai trò ở danh sách bên trái để hiển thị ma trận quyền.</p>
      </div>
    );
  }

  return (
    <div className="ra-page">
      <header className="ra-page-header">
        <div className="ra-page-heading">
          <h1>Phân công vai trò</h1>
          <p>Quản lý quyền truy cập theo mô-đun cho từng vai trò trong hệ thống.</p>
        </div>
        <div className="ra-header-actions">
          <button
            className="ra-btn ra-btn--primary"
            onClick={() => setAddRoleOpen(true)}
            type="button"
          >
            + Thêm Vai trò
          </button>
          <button
            className="ra-btn ra-btn--primary"
            onClick={() => setAddModuleOpen(true)}
            type="button"
          >
            + Thêm Mô-đun
          </button>
          <button
            className="ra-btn ra-btn--primary"
            onClick={() => setAddPermissionOpen(true)}
            type="button"
          >
            + Thêm Quyền
          </button>
        </div>
      </header>

      <div className="ra-layout">
        <aside className="ra-card ra-role-card">
        <div className="ra-card-header">
          <div>
            <h2>Danh sách vai trò</h2>
            <p>Chọn một vai trò để xem hoặc cập nhật quyền.</p>
          </div>
          <span className="ra-chip" aria-label={`Có ${filteredRoleCount} vai trò`}>
            {filteredRoleCount}
          </span>
        </div>

          <div className="ra-card-body ra-role-list">
            {filteredRoleCount === 0 ? (
              <div className="ra-empty ra-empty--compact">
                <h3>Chưa có vai trò nào</h3>
                <p>Hãy thêm vai trò mới để bắt đầu phân quyền.</p>
              </div>
            ) : (
              filteredRoles.map((role) => {
                const isActiveRole = selectedRole?.roleId === role.roleId;
                return (
                  <button
                    key={role.roleId}
                    className={`ra-role-item${isActiveRole ? " is-active" : ""}`}
                    onClick={() => handleRoleSelect(role)}
                    type="button"
                    aria-pressed={isActiveRole}
                  >
                    <div className="ra-role-item__info">
                      <span className="ra-role-item__name">{role.name}</span>
                    </div>
                      <span className="ra-role-code">#{role.code}</span>
                  </button>
                );
              })
            )}
          </div>
        </aside>

        <section className="ra-card ra-permission-card">
          <div className="ra-card-header ra-card-header--between">
            <div className="ra-card-title-group">
              <div className="ra-card-title">
                {selectedRole ? selectedRole.name : "Chưa chọn vai trò"}
                {hasUnsavedChanges && selectedRole && (
                  <span className="ra-tag ra-tag--warning">Chưa lưu</span>
                )}
              </div>
              <p>
                {selectedRole
                  ? "Bật/tắt quyền truy cập cho từng mô-đun bên dưới."
                  : "Chọn một vai trò ở bảng bên trái để bắt đầu phân quyền."}
              </p>
            </div>

            {selectedRole && (
              <div className="ra-permission-stats" aria-live="polite">
                <span className="ra-permission-count">
                  {activePermissionCount}/{totalPermissionSlots}({activePermissionPercent}%) Quyền
                </span>
                <progress
                  className="ra-progress"
                  max={100}
                  value={activePermissionPercent}
                >
                  {activePermissionPercent}%
                </progress>
              </div>
            )}
          </div>

          {isAdminRoleSelected && (
            <div className="ra-alert ra-alert--info" role="status">
              Quản trị viên được toàn quyền hệ thống và không thể chỉnh sửa quyền.
            </div>
          )}

          <div className="ra-card-toolbar">
            <button
              className="ra-btn ra-btn--ghost"
              onClick={handleCancel}
              type="button"
              disabled={!selectedRole || isAdminRoleSelected}
            >
              Hủy
            </button>
            <button
              className="ra-btn ra-btn--outline"
              onClick={handleTickAll}
              type="button"
              disabled={!selectedRole || isAdminRoleSelected}
            >
              {isAllTicked ? "Bỏ chọn tất cả" : "Chọn tất cả"}
            </button>
            <button
              className="ra-btn ra-btn--primary"
              onClick={handleSaveChanges}
              type="button"
              disabled={
                !selectedRole ||
                submitting ||
                !hasUnsavedChanges ||
                isAdminRoleSelected
              }
            >
              {submitting ? "Đang lưu..." : "Lưu thay đổi"}
            </button>
          </div>

          <div className="ra-permission-scroll">{permissionContent}</div>
        </section>
      </div>

      {/* Modals */}
      <RoleModal
        isOpen={addRoleOpen}
        title="Thêm Vai trò"
        fields={[
          { name: "name", label: "Tên Vai trò", required: true, minLength: 2, maxLength: 60 },
          { name: "code", label: "Mã", required: true, minLength: 2, maxLength: 50, format: "code" },
          { name: "isSystem", label: "Vai trò hệ thống", type: "checkbox" },
        ]}
        onClose={() => setAddRoleOpen(false)}
        onSubmit={handleCreateRole}
        submitting={submitting}
      />
      
      <RoleModal
        isOpen={addModuleOpen}
        title="Thêm Mô-đun"
        fields={[
          { name: "moduleName", label: "Tên Mô-đun", required: true, minLength: 2, maxLength: 80 },
          { name: "code", label: "Mã", required: true, minLength: 2, maxLength: 50, format: "code" },
          { name: "description", label: "Mô tả", type: "textarea", maxLength: 200 },
        ]}
        onClose={() => setAddModuleOpen(false)}
        onSubmit={handleCreateModule}
        submitting={submitting}
      />
      
      <RoleModal
        isOpen={addPermissionOpen}
        title="Thêm Quyền"
        fields={[
          { name: "permissionName", label: "Tên Quyền", required: true, minLength: 2, maxLength: 100 },
          { name: "code", label: "Mã", required: true, minLength: 2, maxLength: 50, format: "code" },
          { name: "description", label: "Mô tả", type: "textarea", maxLength: 300 },
        ]}
        onClose={() => setAddPermissionOpen(false)}
        onSubmit={handleCreatePermission}
        submitting={submitting}
      />
      {/* Toast */}
      <ToastContainer 
        toasts={toasts} 
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </div>
  );
}
