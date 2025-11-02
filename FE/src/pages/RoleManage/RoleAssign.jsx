/**
 * File: RoleAssign.jsx
 * Author: Keytietkiem Team
 * Created: 20/10/2025
 * Last Updated: 25/10/2025
 * Version: 1.0.0
 * Purpose: Role permission assignment page for managing role-permission relationships.
 */
import React, { useEffect, useState, useCallback } from "react";
import {rbacApi} from "../../services/rbacApi";
import RBACModal from "../../components/RBACModal/RBACModal";
import ToastContainer from "../../components/Toast/ToastContainer";
import useToast from "../../hooks/useToast";
import "./RoleAssign.css";

/**
 * @summary: Role permission assignment page component.
 * @returns {JSX.Element} - Role assignment interface with permission matrix
 */
export default function RoleAssign() {
  const { toasts, showSuccess, showError, showWarning, removeToast, confirmDialog } = useToast();
  
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
  
  /**
   * @summary: Load role-permission matrix for a given role.
   * @param {string} roleId - Role identifier
   * @returns {Promise<void>}
   */
  const loadRolePermissions = useCallback(async (roleId) => {
    try {
      const response = await rbacApi.getRolePermissions(roleId);
      setRolePermissions(response.rolePermissions || []);
    } catch (error) {
      showError("Lỗi tải quyền", error.message || "Không thể tải quyền của Vai trò");
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
        rbacApi.getActiveRoles(),
        rbacApi.getModules(),
        rbacApi.getPermissions()
      ]);
      
      setRoles(rolesData || []);
      setModules(modulesData || []);
      setPermissions(permissionsData || []);
    } catch (error) {
      showError("Lỗi tải dữ liệu", error.message || "Không thể tải dữ liệu");
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
      const created = await rbacApi.createRole({ 
        name: form.name, 
        isSystem: form.isSystem || false 
      });
      setRoles(prev => [...prev, created]);
      setAddRoleOpen(false);
      showSuccess(
        "Tạo Vai trò thành công!",
        `Vai trò "${form.name}" đã được tạo và tự động gán quyền cho tất cả Mô-đun và Quyền.`
      );
    } catch (error) {
      const errorMessage = error.response?.data?.message || error.message || "Không thể tạo Vai trò";
      showError("Tạo Vai trò thất bại!", errorMessage);
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
      const created = await rbacApi.createModule({ 
        moduleName: form.moduleName,
        description: form.description || ""
      });
      setModules(prev => [...prev, created]);
      setAddModuleOpen(false);
      showSuccess(
        "Tạo Mô-đun thành công!",
        `Mô-đun "${form.moduleName}" đã được tạo và tự động gán quyền cho tất cả Vai trò và Quyền.`
      );
    } catch (error) {
      const errorMessage = error.response?.data?.message || error.message || "Không thể tạo Mô-đun";
      showError("Tạo Mô-đun thất bại!", errorMessage);
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
      const created = await rbacApi.createPermission({ 
        permissionName: form.permissionName, 
        description: form.description || ""
      });
      setPermissions(prev => [...prev, created]);
      setAddPermissionOpen(false);
      showSuccess(
        "Tạo Quyền thành công!",
        `Quyền "${form.permissionName}" đã được tạo và tự động gán quyền cho tất cả Vai trò và Mô-đun.`
      );
    } catch (error) {
      const errorMessage = error.response?.data?.message || error.message || "Không thể tạo Quyền";
      showError("Tạo Quyền thất bại!", errorMessage);
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
    if (hasUnsavedChanges) {
      const confirmSwitch = window.confirm(
        "Bạn có thay đổi chưa lưu. Bạn có chắc muốn chuyển sang vai trò khác? Thay đổi sẽ bị mất."
      );
      if (!confirmSwitch) {
        return;
      }
    }
    setSelectedRole(role);
  };
  
  /**
   * @summary: Cancel edits and reload role-permissions from server.
   * @returns {Promise<void>}
   */
  const handleCancel = async () => {
    if (!selectedRole) return;
    
    try {
      await loadRolePermissions(selectedRole.roleId);
      setHasUnsavedChanges(false);
      showSuccess("Đã hủy thay đổi", "Ma trận Quyền đã được reset về trạng thái ban đầu");
    } catch (error) {
      console.error("Lôi khi hủy thay đổi:", error);
      showError("Lỗi khi hủy", "Không thể reset ma trận Quyền");
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
    if (!selectedRole) return;
    
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
      await rbacApi.updateRolePermissions(selectedRole.roleId, {
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
    } catch (error) {
      const errorMessage = error.response?.data?.message || error.message || "Không thể lưu thay đổi";
      showError("Lưu thay đổi thất bại!", errorMessage);
    } finally {
      setSubmitting(false);
    }
  };
  
  /**
   * @summary: Toggle all permissions (select/deselect all) in local state.
   * @returns {void}
   */
  const handleTickAll = () => {
    if (!selectedRole) return;
    
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
  
  if (loading) {
    return (
      <div className="ra-container">
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
          <div>Đang tải dữ liệu...</div>
        </div>
      </div>
    );
  }
  
  return (
    <div className="ra-container">
      {/* Left Sidebar - Roles Panel */}
      <div className="ra-roles-panel">
        <h2 className="ra-roles-title">Quản lý Vai trò</h2>
        
        {/* Action Buttons */}
        <div className="ra-sidebar-buttons">
          <button 
            className="ra-add-role-btn"
            onClick={() => setAddRoleOpen(true)}
          >
            Thêm Vai trò
          </button>
          <button 
            className="ra-add-module-btn"
            onClick={() => setAddModuleOpen(true)}
          >
            Thêm Mô-đun
          </button>
          <button 
            className="ra-add-permission-btn"
            onClick={() => setAddPermissionOpen(true)}
          >
            Thêm Quyền
          </button>
        </div>
        
        {/* Role List */}
        <div className="ra-role-list">
          {loading ? (
            <div className="ra-loading-state">
              <div className="ra-loading-spinner" />
              <div>Đang tải dữ liệu...</div>
            </div>
          ) : roles.length === 0 ? (
            <div className="ra-empty-state">
              <div>Không có dữ liệu</div>
            </div>
          ) : (
            roles.map((role) => (
              <button
                key={role.roleId}
                className={`ra-role-item ${selectedRole?.roleId === role.roleId ? 'selected' : ''}`}
                onClick={() => handleRoleSelect(role)}
                type="button"
              >
                {role.name}
              </button>
            ))
          )}
        </div>
      </div>
      
      {/* Right Panel - Permissions Matrix */}
      <div className="ra-permissions-panel">
         <div className="ra-permissions-header">
           <h2 className="ra-permissions-title">
             {selectedRole ? `Quyền của Vai trò: ${selectedRole.name}` : 'Chọn một Vai trò để xem quyền'}
             {hasUnsavedChanges && selectedRole && (
               <span style={{ 
                 color: '#ffc107', 
                 fontSize: '14px', 
                 marginLeft: '10px',
                 fontWeight: 'normal'
               }}>
                 (Có thay đổi chưa lưu)
               </span>
             )}
           </h2>
          <div className="ra-action-buttons">
             <button 
               className="ra-btn btn-cancel"
               onClick={handleCancel}
               disabled={!selectedRole}
             >
               Hủy
             </button>
            <button 
              className="ra-btn btn-tick-all"
              onClick={handleTickAll}
              disabled={!selectedRole}
            >
              {isAllTicked ? 'Bỏ chọn tất cả' : 'Chọn tất cả'}
            </button>
             <button 
               className="ra-btn btn-save"
               onClick={handleSaveChanges}
               disabled={!selectedRole || submitting || !hasUnsavedChanges}
               style={{
                 opacity: (!selectedRole || submitting || !hasUnsavedChanges) ? 0.6 : 1
               }}
             >
               {submitting ? 'Đang lưu...' : 'Lưu thay đổi'}
             </button>
          </div>
        </div>
        
        {/* Permissions Matrix */}
        {selectedRole && (
          <div className="ra-permissions-table-container">
            <table className="ra-permissions-table">
              <thead>
                <tr>
                  <th>Mô-đun\Quyền</th>
                  {permissions.map((permission) => (
                    <th key={permission.permissionId}  className="ra-permission-name">
                      {permission.permissionName}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {modules.map((module) => (
                  <tr key={module.moduleId}>
                    <td className="ra-module-name">{module.moduleName}</td>
                    {permissions.map((permission) => (
                      <td key={`${permission.permissionId}-${module.moduleId}`}>
                        <input
                          type="checkbox"
                          className="ra-permission-checkbox"
                          checked={isPermissionActive(module.moduleId, permission.permissionId)}
                          onChange={() => handlePermissionToggle(module.moduleId, permission.permissionId)}
                        />
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        
        {!selectedRole && (
          <output 
            style={{ 
              display: 'flex', 
              justifyContent: 'center', 
              alignItems: 'center', 
              height: '300px',
              color: '#6c757d',
              fontSize: '16px'
            }}
            aria-live="polite"
          >
            Vui lòng chọn một Vai trò để xem và chỉnh sửa quyền
          </output>
        )}
      </div>
      
      {/* Modals */}
      <RBACModal
        isOpen={addRoleOpen}
        title="Thêm Vai trò"
        fields={[
          { name: "name", label: "Tên Vai trò", required: true },
        ]}
        onClose={() => setAddRoleOpen(false)}
        onSubmit={handleCreateRole}
        submitting={submitting}
      />
      
      <RBACModal
        isOpen={addModuleOpen}
        title="Thêm Mô-đun"
        fields={[
          { name: "moduleName", label: "Tên Mô-đun", required: true },
          { name: "description", label: "Mô tả", type: "textarea" },
        ]}
        onClose={() => setAddModuleOpen(false)}
        onSubmit={handleCreateModule}
        submitting={submitting}
      />
      
      <RBACModal
        isOpen={addPermissionOpen}
        title="Thêm Quyền"
        fields={[
          { name: "permissionName", label: "Tên Quyền", required: true },
          { name: "description", label: "Mô tả", type: "textarea" },
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
