import React, { useEffect, useState, useCallback } from "react";
import { rbacApi } from "../../api";
import RBACModal from "../../components/RBACModal/RBACModal";
import ToastContainer from "../../components/Toast/ToastContainer";
import useToast from "../../hooks/useToast";
import "./RoleAssign.css";

export default function RoleAssign() {
  const { toasts, showSuccess, showError, showWarning, removeToast } = useToast();
  
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
  
  // Load initial data
  useEffect(() => {
    loadData();
  }, []);
  
  // Load role permissions when role is selected
  useEffect(() => {
    if (selectedRole) {
      loadRolePermissions(selectedRole.roleId);
      setHasUnsavedChanges(false);
    }
  }, [selectedRole]);
  
  const loadData = async () => {
    try {
      setLoading(true);
      const [rolesData, modulesData, permissionsData] = await Promise.all([
        rbacApi.GetActiveRoles(),
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
  };
  
  const loadRolePermissions = async (roleId) => {
    try {
      const response = await rbacApi.getRolePermissions(roleId);
      setRolePermissions(response.rolePermissions || []);
    } catch (error) {
      showError("Lỗi tải quyền", error.message || "Không thể tải quyền của role");
    }
  };
  
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
        "Tạo Role thành công!",
        `Role "${form.name}" đã được tạo và tự động gán quyền cho tất cả modules và permissions.`
      );
    } catch (error) {
      const errorMessage = error.response?.data?.message || error.message || "Không thể tạo Role";
      showError("Tạo Role thất bại!", errorMessage);
    } finally {
      setSubmitting(false);
    }
  };
  
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
        "Tạo Module thành công!",
        `Module "${form.moduleName}" đã được tạo và tự động gán quyền cho tất cả roles và permissions.`
      );
    } catch (error) {
      const errorMessage = error.response?.data?.message || error.message || "Không thể tạo Module";
      showError("Tạo Module thất bại!", errorMessage);
    } finally {
      setSubmitting(false);
    }
  };
  
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
        "Tạo Permission thành công!",
        `Permission "${form.permissionName}" đã được tạo và tự động gán quyền cho tất cả roles và modules.`
      );
    } catch (error) {
      const errorMessage = error.response?.data?.message || error.message || "Không thể tạo Permission";
      showError("Tạo Permission thất bại!", errorMessage);
    } finally {
      setSubmitting(false);
    }
  };
  
  // Handle role selection
  const handleRoleSelect = (role) => {
    if (hasUnsavedChanges) {
      const confirmSwitch = window.confirm(
        "Bạn có thay đổi chưa lưu. Bạn có chắc muốn chuyển sang role khác? Thay đổi sẽ bị mất."
      );
      if (!confirmSwitch) {
        return;
      }
    }
    setSelectedRole(role);
  };
  
  // Handle cancel - reload permissions from server
  const handleCancel = async () => {
    if (!selectedRole) return;
    
    try {
      await loadRolePermissions(selectedRole.roleId);
      setHasUnsavedChanges(false);
      showSuccess("Đã hủy thay đổi", "Ma trận permissions đã được reset về trạng thái ban đầu");
    } catch (error) {
      console.error("Error canceling changes:", error);
      showError("Lỗi khi hủy", "Không thể reset ma trận permissions");
      throw error;
    }
  };
  
  /**
   * Handle permission toggle in the matrix
   * This only updates local state - changes are not persisted until save
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
  
  // Check if permission is active
  const isPermissionActive = (moduleId, permissionId) => {
    const rolePermission = rolePermissions.find(rp => 
      rp.moduleId === moduleId && rp.permissionId === permissionId
    );
    return rolePermission ? rolePermission.isActive : false;
  };
  
  /**
   * Handle saving all permission changes to the server
   * Creates a complete matrix of all module-permission combinations
   */
    const handleSaveChanges = async () => {
    if (!selectedRole) {
      showWarning("Chưa chọn Role", "Vui lòng chọn một role để lưu thay đổi");
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
        `Đã cập nhật tất cả quyền cho role "${selectedRole.name}"`
      );
    } catch (error) {
      const errorMessage = error.response?.data?.message || error.message || "Không thể lưu thay đổi";
      showError("Lưu thay đổi thất bại!", errorMessage);
    } finally {
      setSubmitting(false);
    }
  };
  
  // Handle tick all/untick all (local state only)
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
  
  // Check if all permissions are ticked
  const isAllTicked = permissions.every(permission => 
    modules.every(module => isPermissionActive(module.moduleId, permission.permissionId))
  );
  
  if (loading) {
    return (
      <div className="role-assign-container">
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
          <div>Đang tải dữ liệu...</div>
        </div>
      </div>
    );
  }
  
  return (
    <div className="role-assign-container">
      {/* Left Sidebar - Roles Panel */}
      <div className="roles-panel">
        <h2 className="roles-title">Quản lý Role</h2>
        
        {/* Action Buttons */}
        <div className="sidebar-buttons">
          <button 
            className="add-role-btn"
            onClick={() => setAddRoleOpen(true)}
          >
            Thêm Role
          </button>
          <button 
            className="add-module-btn"
            onClick={() => setAddModuleOpen(true)}
          >
            Thêm Module
          </button>
          <button 
            className="add-permission-btn"
            onClick={() => setAddPermissionOpen(true)}
          >
            Thêm Permission
          </button>
        </div>
        
        {/* Role List */}
        <div className="role-list">
          {loading ? (
            <div className="loading-state">
              <div className="loading-spinner" />
              <div>Đang tải dữ liệu...</div>
            </div>
          ) : roles.length === 0 ? (
            <div className="empty-state">
              <div>Không có dữ liệu</div>
            </div>
          ) : (
            roles.map((role) => (
              <button
                key={role.roleId}
                className={`role-item ${selectedRole?.roleId === role.roleId ? 'selected' : ''}`}
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
      <div className="permissions-panel">
         <div className="permissions-header">
           <h2 className="permissions-title">
             {selectedRole ? `Quyền của Role: ${selectedRole.name}` : 'Chọn một Role để xem quyền'}
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
          <div className="action-buttons">
             <button 
               className="btn btn-cancel"
               onClick={handleCancel}
               disabled={!selectedRole}
             >
               Hủy
             </button>
            <button 
              className="btn btn-tick-all"
              onClick={handleTickAll}
              disabled={!selectedRole}
            >
              {isAllTicked ? 'Untick All' : 'Tick All'}
            </button>
             <button 
               className="btn btn-save"
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
          <div className="permissions-table-container">
            <table className="permissions-table">
              <thead>
                <tr>
                  <th>Permission</th>
                  {modules.map((module) => (
                    <th key={module.moduleId} className="module-name">
                      {module.moduleName}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {permissions.map((permission) => (
                  <tr key={permission.permissionId}>
                    <td className="permission-name">{permission.permissionName}</td>
                    {modules.map((module) => (
                      <td key={`${permission.permissionId}-${module.moduleId}`}>
                        <input
                          type="checkbox"
                          className="permission-checkbox"
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
            Vui lòng chọn một Role để xem và chỉnh sửa quyền
          </output>
        )}
      </div>
      
      {/* Modals */}
      <RBACModal
        isOpen={addRoleOpen}
        title="Thêm Role"
        fields={[
          { name: "name", label: "Role Name", required: true },
          { name: "isSystem", label: "System Role", type: "checkbox" },
        ]}
        onClose={() => setAddRoleOpen(false)}
        onSubmit={handleCreateRole}
        submitting={submitting}
      />
      
      <RBACModal
        isOpen={addModuleOpen}
        title="Thêm Module"
        fields={[
          { name: "moduleName", label: "Module Name", required: true },
          { name: "description", label: "Description", type: "textarea" },
        ]}
        onClose={() => setAddModuleOpen(false)}
        onSubmit={handleCreateModule}
        submitting={submitting}
      />
      
      <RBACModal
        isOpen={addPermissionOpen}
        title="Thêm Permission"
        fields={[
          { name: "permissionName", label: "Permission Name", required: true },
          { name: "description", label: "Description", type: "textarea" },
        ]}
        onClose={() => setAddPermissionOpen(false)}
        onSubmit={handleCreatePermission}
        submitting={submitting}
      />
      {/* Toast */}
      <ToastContainer 
        toasts={toasts} 
        onRemove={removeToast} 
      />
    </div>
  );
}
