/**
 * File: RoleManage.jsx
 * Author: Keytietkiem Team
 * Created: 20/10/2025
 * Last Updated: 25/10/2025
 * Version: 1.0.0
 * Purpose: Role management page for Modules, Permissions, and Roles with tabbed navigation.
 */

import React, { useEffect, useMemo, useState } from "react";
import { roleApi } from "../../services/roleApi";
import RoleModal from "../../components/RoleModal/RoleModal";
import ToastContainer from "../../components/Toast/ToastContainer";
import useToast from "../../hooks/useToast";
import "./RoleManage.css"

/** 
 * @summary Tab constants for switching between different management views 
*/
const TABS = {
  ROLES: "roles",
  MODULES: "modules",
  PERMISSIONS: "permissions",
};

/**
 * @summary Custom hook for fetching Role data dynamically based on the active tab.
 * @param {string} activeTab - One of 'modules', 'permissions', or 'roles'.
 * @returns {Object} - { data, loading, error, setData }
 */
function useFetchData(activeTab) {
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let isMounted = true;
    async function load() {
      setLoading(true);
      setError("");
      try {
        let res = [];
        if (activeTab === TABS.MODULES) res = await roleApi.getModules();
        else if (activeTab === TABS.PERMISSIONS) res = await roleApi.getPermissions();
        else if (activeTab === TABS.ROLES) res = await roleApi.getAllRoles();
        if (isMounted) setData(Array.isArray(res) ? res : []);
      } catch (e) {
        if (isMounted) setError(e.message || "Không thể tải dữ liệu");
      } finally {
        if (isMounted) setLoading(false);
      }
    }
    load();
    return () => {
      isMounted = false;
    };
  }, [activeTab]);

  return { data, loading, error, setData };
}
/**
 * @summary Formats a date string into a readable local string.
 * @param {string|Date} value - The date value.
 * @returns {string} - Formatted date string.
 */
function formatDate(value) {
  if (!value) return "";
  try {
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return "";
    return d.toLocaleString();
  } catch {
    return "";
  }
}
/**
 * @summary Handles the user interface and interaction logic for managing
 * modules, permissions, and roles.
 */
export default function RoleManagement() {
  const [activeTab, setActiveTab] = useState(TABS.MODULES);
  const { data, loading, error, setData } = useFetchData(activeTab);
  const { toasts, showSuccess, showError, showWarning, removeToast, showConfirm, confirmDialog } = useToast();

  const [search, setSearch] = useState("");
  const [sortKey, setSortKey] = useState("");
  const [sortOrder, setSortOrder] = useState("asc");

  // Roles-only status filter
  const [roleStatus, setRoleStatus] = useState("all"); // all | active | inactive

  // Modal for adding role (Modal)
  const [addRoleOpen, setAddRoleOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const [page, setPage] = useState(1);
  const [pageSize] = useState(10); // Fixed at 10 items per page

  useEffect(() => {
    setSearch("");
    setSortKey("");
    setSortOrder("asc");
    setPage(1);
    setRoleStatus("all");
  }, [activeTab]);

  // Handle column sort
  const handleColumnSort = (columnKey) => {
    if (sortKey === columnKey) {
      setSortOrder(sortOrder === "asc" ? "desc" : "asc");
    } else {
      setSortKey(columnKey);
      setSortOrder("asc");
    }
  };
  /**
   * @summary  Dynamically generates column definitions and the "Add" button label 
   * based on the currently active tab (Modules, Permissions, or Roles).
   * @returns column definitions and button labels dynamically per active tab.
   */
  const { columns, addButtonText } = useMemo(() => {
    if (activeTab === TABS.MODULES) {
      return {
        addButtonText: "Thêm Mô-đun",
        columns: [
          { key: "moduleName", label: "Tên Mô-đun" },
          { key: "description", label: "Mô tả" },
          { key: "createdAt", label: "Thời điểm tạo", render: formatDate },
          { key: "updatedAt", label: "Thời điểm cập nhật", render: formatDate },
        ],
      };
    }
    if (activeTab === TABS.PERMISSIONS) {
      return {
        addButtonText: "Thêm Quyền",
        columns: [
          { key: "permissionName", label: "Tên quyền" },
          { key: "description", label: "Mô tả" },
          { key: "createdAt", label: "Thời điểm tạo", render: formatDate },
          { key: "updatedAt", label: "Thời điểm cập nhật", render: formatDate },
        ],
      };
    }
    return {
      addButtonText: "Thêm Vai trò",
      columns: [
        { key: "name", label: "Tên Vai trò" },
        { key: "isSystem", label: "System Role", render: (v) => (v ? "Có" : "Không") },
        { key: "isActive", label: "Active", render: (v) => (v ? "Có" : "Không") },
        { key: "createdAt", label: "Thời điểm tạo", render: formatDate },
        { key: "updatedAt", label: "Thời điểm cập nhật", render: formatDate },
      ],
    };
  }, [activeTab]);


  const filteredSorted = useMemo(() => {
    const normalized = (v) => (v ?? "").toString().toLowerCase();
    const searchLower = normalized(search);

    // Determine name key per tab
    const nameKey = activeTab === TABS.MODULES ? "moduleName" : activeTab === TABS.PERMISSIONS ? "permissionName" : "name";

    let rows = data.filter((row) => {
      // search by name only
      if (!searchLower) return true;
      return normalized(row[nameKey]).includes(searchLower);
    });

    // roles-only status filter
    if (activeTab === TABS.ROLES && roleStatus !== "all") {
      const wantActive = roleStatus === "active";
      rows = rows.filter((r) => Boolean(r.isActive) === wantActive);
    }

    if (sortKey) {
      rows = [...rows].sort((a, b) => {
        const av = a[sortKey];
        const bv = b[sortKey];
        if (av == null && bv == null) return 0;
        if (av == null) return sortOrder === "asc" ? -1 : 1;
        if (bv == null) return sortOrder === "asc" ? 1 : -1;
        if (typeof av === "string" && typeof bv === "string") {
          return sortOrder === "asc" ? av.localeCompare(bv) : bv.localeCompare(av);
        }
        const aNum = new Date(av).getTime();
        const bNum = new Date(bv).getTime();
        const bothDates = !Number.isNaN(aNum) && !Number.isNaN(bNum);
        if (bothDates) return sortOrder === "asc" ? aNum - bNum : bNum - aNum;
        if (av > bv) return sortOrder === "asc" ? 1 : -1;
        if (av < bv) return sortOrder === "asc" ? -1 : 1;
        return 0;
      });
    }
    return rows;
  }, [data, columns, search, sortKey, sortOrder, roleStatus, activeTab]);

  const total = filteredSorted.length;
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const currentPage = Math.min(page, totalPages);
  const paginated = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredSorted.slice(start, start + pageSize);
  }, [filteredSorted, currentPage, pageSize]);

  const [addModuleOpen, setAddModuleOpen] = useState(false);
  const [addPermissionOpen, setAddPermissionOpen] = useState(false);

  /**
   * @summary: Handle clicking the Add button based on active tab.
   * @returns {void}
   */
  function onClickAdd() {
    if (activeTab === TABS.ROLES) {
      setAddRoleOpen(true);
      return;
    }
    if (activeTab === TABS.MODULES) {
      setAddModuleOpen(true);
      return;
    }
    if (activeTab === TABS.PERMISSIONS) {
      setAddPermissionOpen(true);
      return;
    }
  }

  /**
   * @summary: Create a new Role entity.
   * @param {{ name: string, isSystem?: boolean }} form - Role form payload
   * @returns {Promise<void>}
   */
  async function handleCreateRole(form) {
    try {
      setSubmitting(true);
      const created = await roleApi.createRole({
        name: form.name,
        isSystem: form.isSystem || false
      });
      setData((prev) => Array.isArray(prev) ? [...prev, created] : [created]);
      setAddRoleOpen(false);
      showSuccess(
        "Tạo Vai trò thành công!",
        `Vai trò "${form.name}" đã được tạo và tự động gán quyền cho tất cả Mô-đun và Quyền.`
      );
    } catch (e) {
      const errorMessage = e.response?.data?.message || e.message || "Không thể tạo Vai trò";
      showError("Tạo Vai trò thất bại!", errorMessage);
    } finally {
      setSubmitting(false);
    }
  }

  /**
   * @summary: Create a new Module entity.
   * @param {{ moduleName: string, description?: string }} form - Module form payload
   * @returns {Promise<void>}
   */
  async function handleCreateModule(form) {
    try {
      setSubmitting(true);
      const created = await roleApi.createModule({
        moduleName: form.moduleName,
        description: form.description || ""
      });
      setData((prev) => Array.isArray(prev) ? [...prev, created] : [created]);
      setAddModuleOpen(false);
      showSuccess(
        "Tạo Mô-đun thành công!",
        `Mô-đun "${form.moduleName}" đã được tạo và tự động gán quyền cho tất cả Vai trò và quyền.`
      );
    } catch (e) {
      const errorMessage = e.response?.data?.message || e.message || "Không thể tạo Mô-đun";
      showError("Tạo Mô-đun thất bại!", errorMessage);
    } finally {
      setSubmitting(false);
    }
  }

  /**
   * @summary: Create a new Permission entity.
   * @param {{ permissionName: string, description?: string }} form - Permission form payload
   * @returns {Promise<void>}
   */
  async function handleCreatePermission(form) {
    try {
      setSubmitting(true);
      const created = await roleApi.createPermission({
        permissionName: form.permissionName,
        description: form.description || ""
      });
      setData((prev) => Array.isArray(prev) ? [...prev, created] : [created]);
      setAddPermissionOpen(false);
      showSuccess(
        "Tạo Quyền thành công!",
        `Quyền "${form.permissionName}" đã được tạo và tự động gán quyền cho tất cả Vai trò và Mô-đun.`
      );
    } catch (e) {
      const errorMessage = e.response?.data?.message || e.message || "Không thể tạo Quyền";
      showError("Tạo Quyền thất bại!", errorMessage);
    } finally {
      setSubmitting(false);
    }
  }

  const [editOpen, setEditOpen] = useState(false);
  const [editFields, setEditFields] = useState([]);
  const [editTitle, setEditTitle] = useState("");
  const [editSubmitting, setEditSubmitting] = useState(false);
  const [editingRow, setEditingRow] = useState(null);

  /**
   * @summary: Open edit modal for the selected row with dynamic fields.
   * @param {Object} row - Selected row data
   * @returns {void}
   */
  function onEdit(row) {
    setEditingRow(row);
    if (activeTab === TABS.MODULES) {
      setEditTitle("Sửa Mô-đun");
      setEditFields([
        { name: "moduleName", label: "Tên mô-đun", required: true, defaultValue: row.moduleName },
        { name: "description", label: "Mô tả", type: "textarea", defaultValue: row.description || "" },
      ]);
    } else if (activeTab === TABS.PERMISSIONS) {
      setEditTitle("Sửa Quyền");
      setEditFields([
        { name: "permissionName", label: "Tên quyền", required: true, defaultValue: row.permissionName },
        { name: "description", label: "Mô tả", type: "textarea", defaultValue: row.description || "" },
      ]);
    } else {
      setEditTitle("Sửa Role");
      setEditFields([
        { name: "name", label: "Tên vai trò", required: true, defaultValue: row.name },
        { name: "isActive", label: "Active", type: "checkbox", defaultValue: row.isActive },
      ]);
    }
    setEditOpen(true);
  }

  /**
   * @summary: Delete an entity (Module/Permission/Role) after confirmation.
   * @param {Object} row - Selected row data
   * @returns {Promise<void>}
   */
  async function onDelete(row) {
    const label = activeTab === TABS.MODULES ? row.moduleName : activeTab === TABS.PERMISSIONS ? row.permissionName : row.name;
    const entityType = activeTab === TABS.MODULES ? "Module" : activeTab === TABS.PERMISSIONS ? "Permission" : "Role";

    showWarning(
      `Xác nhận xóa ${entityType}`,
      `Bạn sắp xóa ${entityType.toLowerCase()} "${label}". Hành động này không thể hoàn tác!`
    );

    // Show confirm dialog instead of alert
    showConfirm(
      `Xác nhận xóa ${entityType}`,
      `Bạn có chắc chắn muốn xóa "${label}"? Hành động này không thể hoàn tác.`,
      async () => {
        try {
          if (activeTab === TABS.MODULES) await roleApi.deleteModule(row.moduleId || row.id);
          else if (activeTab === TABS.PERMISSIONS) await roleApi.deletePermission(row.permissionId || row.id);
          else await roleApi.deleteRole(row.roleId || row.id);
          setData((prev) => prev.filter((x) => {
            const key = activeTab === TABS.MODULES ? "moduleId" : activeTab === TABS.PERMISSIONS ? "permissionId" : "roleId";
            const targetId = row[key] ?? row.id;
            const currentId = x[key] ?? x.id;
            return currentId !== targetId;
          }));
          showSuccess(
            `Xóa ${entityType} thành công!`,
            `${entityType} "${label}" đã được xóa và tất cả quyền liên quan cũng đã được xóa.`
          );
        } catch (e) {
          const errorMessage = e.response?.data?.message || e.message || "Xoá thất bại";
          showError(`Xóa ${entityType} thất bại!`, errorMessage);
        }
      },
      () => {
        // User cancelled, no action needed
      }
    );
  }

  /**
   * @summary: Submit edit modal and persist changes to server.
   * @param {Object} form - Edited form data
   * @returns {Promise<void>}
   */
  async function onSubmitEdit(form) {
    try {
      setEditSubmitting(true);
      const entityType = activeTab === TABS.MODULES ? "Module" : activeTab === TABS.PERMISSIONS ? "Permission" : "Role";
      const entityName = activeTab === TABS.MODULES ? form.moduleName : activeTab === TABS.PERMISSIONS ? form.permissionName : form.name;

      if (activeTab === TABS.MODULES) {
        await roleApi.updateModule(editingRow.moduleId, {
          moduleName: form.moduleName,
          description: form.description || ""
        });
        setData((prev) => prev.map((x) => x.moduleId === editingRow.moduleId ? {
          ...x,
          moduleName: form.moduleName,
          description: form.description,
          updatedAt: new Date().toISOString(),
        } : x));
      } else if (activeTab === TABS.PERMISSIONS) {
        await roleApi.updatePermission(editingRow.permissionId, {
          permissionName: form.permissionName,
          description: form.description || ""
        });
        setData((prev) => prev.map((x) => x.permissionId === editingRow.permissionId ? {
          ...x,
          permissionName: form.permissionName,
          description: form.description,
          updatedAt: new Date().toISOString(),
        } : x));
      } else {
        const payload = {
          name: form.name,
          isActive: form.isActive
        };
        await roleApi.updateRole(editingRow.roleId, payload);
        setData((prev) => prev.map((x) => x.roleId === editingRow.roleId ? {
          ...x,
          ...payload,
          updatedAt: new Date().toISOString(),
        } : x));
      }
      setEditOpen(false);
      showSuccess(
        `Cập nhật ${entityType} thành công!`,
        `${entityType} "${entityName}" đã được cập nhật thành công.`
      );
    } catch (e) {
      const errorMessage = e.response?.data?.message || e.message || "Cập nhật thất bại";
      const entityType = activeTab === TABS.MODULES ? "Module" : activeTab === TABS.PERMISSIONS ? "Permission" : "Role";
      showError(`Cập nhật ${entityType} thất bại!`, errorMessage);
    } finally {
      setEditSubmitting(false);
    }
  }

  return (
    <div className="role-management-container">
      <div className="role-header">
        <h1 className="role-title">Quản lý phân quyền</h1>
        <p className="role-subtitle">Quản lý Mô-đun, Quyền và Vai trò</p>
      </div>

      <div className="role-tabs">
        <button
          className={`role-tab-button ${activeTab === TABS.MODULES ? "active" : ""}`}
          onClick={() => setActiveTab(TABS.MODULES)}
        >
          Mô-đun
        </button>
        <button
          className={`role-tab-button ${activeTab === TABS.PERMISSIONS ? "active" : ""}`}
          onClick={() => setActiveTab(TABS.PERMISSIONS)}
        >
          Quyền
        </button>
        <button
          className={`role-tab-button ${activeTab === TABS.ROLES ? "active" : ""}`}
          onClick={() => setActiveTab(TABS.ROLES)}
        >
          Vai trò
        </button>
      </div>

      <div className="role-controls">
        <div className="role-controls-left">
          <div className="role-search-box">
            <input
              type="text"
              placeholder={activeTab === TABS.MODULES ? "Tìm tên Mô-đun" : activeTab === TABS.PERMISSIONS ? "Tìm tên Quyền" : "Tìm tên Vai trò"}
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          </div>
          {activeTab === TABS.ROLES && (
            <select
              aria-label="Lọc trạng thái"
              value={roleStatus}
              onChange={(e) => { setRoleStatus(e.target.value); setPage(1); }}
              className="role-btn-secondary"
              style={{ padding: "8px 12px" }}
            >
              <option value="all">Tất cả</option>
              <option value="active">Active</option>
              <option value="inactive">Không active</option>
            </select>
          )}
        </div>
        <div className="role-controls-right">

          <button className="role-add-button" onClick={onClickAdd}>{addButtonText}</button>
        </div>
      </div>
      {activeTab === TABS.ROLES && (
        <RoleModal
          isOpen={addRoleOpen}
          title="Thêm Vai trò"
          fields={[
            { name: "name", label: "Tên Vai trò", required: true },
            { name: "isSystem", label: "System Role", type: "checkbox" },
          ]}
          onClose={() => setAddRoleOpen(false)}
          onSubmit={handleCreateRole}
          submitting={submitting}
        />
      )}
      {activeTab === TABS.MODULES && (
        <RoleModal
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
      )}
      {activeTab === TABS.PERMISSIONS && (
        <RoleModal
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
      )}

      <div className="role-table-container">
        {loading ? (
          <div className="role-loading-state">
            <div className="role-loading-spinner" />
            <div>Đang tải dữ liệu...</div>
          </div>
        ) : error ? (
          <div className="role-empty-state">
            <div>Lỗi: {error}</div>
          </div>
        ) : paginated.length === 0 ? (
          <div className="role-empty-state">
            <div>Không có dữ liệu</div>
          </div>
        ) : (
          <table className="role-table">
            <thead>
              <tr>
                {columns.map((col) => (
                  <th key={col.key}>
                    <div
                      className="role-sortable-header"
                      onClick={() => handleColumnSort(col.key)}
                      onKeyDown={(e) => e.key === "Enter" && handleColumnSort(col.key)}
                      role="button"
                      tabIndex={0}
                    >
                      {col.label}
                      {sortKey === col.key && (sortOrder === "asc" ? " ↑" : " ↓")}
                    </div>
                  </th>
                ))}
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {paginated.map((row, idx) => (
                <tr key={idx}>
                  {columns.map((col) => {
                    const raw = row[col.key];
                    const value = col.render ? col.render(raw, row) : raw;
                    return <td key={col.key}>{value}</td>;
                  })}
                  <td>
                    <div className="role-action-buttons">
                      <button className="role-action-btn role-update-btn" title="Sửa" onClick={() => onEdit(row)}>
                        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" /></svg>
                      </button>
                      <button className="role-action-btn role-delete-btn" title="Xoá" onClick={() => onDelete(row)}>
                        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" /></svg>
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "12px", gap: 8 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <button
            className="role-btn-secondary"
            onClick={() => setPage(1)}
            disabled={currentPage === 1}
          >
            «
          </button>
          <button
            className="role-btn-secondary"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={currentPage === 1}
          >
            ‹
          </button>
          <span style={{ padding: "0 8px" }}>
            Trang {currentPage}/{totalPages} ({total} bản ghi)
          </span>
          <button
            className="role-btn-secondary"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={currentPage >= totalPages}
          >
            ›
          </button>
          <button
            className="role-btn-secondary"
            onClick={() => setPage(totalPages)}
            disabled={currentPage >= totalPages}
          >
            »
          </button>
        </div>
      </div>
      <RoleModal
        isOpen={editOpen}
        title={editTitle}
        fields={editFields}
        onClose={() => setEditOpen(false)}
        onSubmit={onSubmitEdit}
        submitting={editSubmitting}
      />

      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </div>
  );
}

