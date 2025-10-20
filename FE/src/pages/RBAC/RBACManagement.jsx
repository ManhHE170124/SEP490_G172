import React, { useEffect, useMemo, useState } from "react";
import { rbacApi } from "../../api";
import RBACModal from "../../components/RBACModal";
import "./RBACManagement.css";

const TABS = {
  ROLES: "roles",
  MODULES: "modules",
  PERMISSIONS: "permissions",
};

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
        if (activeTab === TABS.MODULES) res = await rbacApi.getModules();
        else if (activeTab === TABS.PERMISSIONS) res = await rbacApi.getPermissions();
        else if (activeTab === TABS.ROLES) res = await rbacApi.getRoles();
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

export default function RBACManagement() {
  const [activeTab, setActiveTab] = useState(TABS.MODULES);
  const { data, loading, error, setData } = useFetchData(activeTab);

  const [search, setSearch] = useState("");
  const [sortKey, setSortKey] = useState("");
  const [sortOrder, setSortOrder] = useState("asc");

  // Roles-only status filter
  const [roleStatus, setRoleStatus] = useState("all"); // all | active | inactive

  // Modal for adding role (RBACModal)
  const [addRoleOpen, setAddRoleOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  useEffect(() => {
    setSearch("");
    setSortKey("");
    setSortOrder("asc");
    setPage(1);
    setRoleStatus("all");
  }, [activeTab]);

  const { columns, addButtonText } = useMemo(() => {
    if (activeTab === TABS.MODULES) {
      return {
        addButtonText: "Thêm Module",
        columns: [
          { key: "moduleName", label: "Module Name" },
          { key: "createdAt", label: "Created At", render: formatDate },
          { key: "updatedAt", label: "Updated At", render: formatDate },
        ],
      };
    }
    if (activeTab === TABS.PERMISSIONS) {
      return {
        addButtonText: "Thêm Permission",
        columns: [
          { key: "permissionName", label: "Permission Name" },
          { key: "description", label: "Description" },
          { key: "createdAt", label: "Created At", render: formatDate },
          { key: "updatedAt", label: "Updated At", render: formatDate },
        ],
      };
    }
    return {
      addButtonText: "Thêm Role",
      columns: [
        { key: "name", label: "Role Name" },
        { key: "desc", label: "Description" },
        { key: "isActive", label: "Active", render: (v) => (v ? "Yes" : "No") },
        { key: "createdAt", label: "Created At", render: formatDate },
        { key: "updatedAt", label: "Updated At", render: formatDate },
      ],
    };
  }, [activeTab]);

  const sortOptions = useMemo(() => {
    return columns.map((c) => ({ value: c.key, label: c.label }));
  }, [columns]);

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
  }, [data, columns, search, sortKey, sortOrder]);

  const total = filteredSorted.length;
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const currentPage = Math.min(page, totalPages);
  const paginated = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredSorted.slice(start, start + pageSize);
  }, [filteredSorted, currentPage, pageSize]);

  const [addModuleOpen, setAddModuleOpen] = useState(false);
  const [addPermissionOpen, setAddPermissionOpen] = useState(false);

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

  async function handleCreateRole(form) {
    try {
      setSubmitting(true);
      const created = await rbacApi.createRole({ name: form.name, desc: form.desc });
      setData((prev) => Array.isArray(prev) ? [...prev, created] : [created]);
      setAddRoleOpen(false);
    } catch (e) {
      alert(e.message || "Không thể tạo Role");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleCreateModule(form) {
    try {
      setSubmitting(true);
      const created = await rbacApi.createModule({ moduleName: form.moduleName });
      setData((prev) => Array.isArray(prev) ? [...prev, created] : [created]);
      setAddModuleOpen(false);
    } catch (e) {
      alert(e.message || "Không thể tạo Module");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleCreatePermission(form) {
    try {
      setSubmitting(true);
      const created = await rbacApi.createPermission({ permissionName: form.permissionName, description: form.description });
      setData((prev) => Array.isArray(prev) ? [...prev, created] : [created]);
      setAddPermissionOpen(false);
    } catch (e) {
      alert(e.message || "Không thể tạo Permission");
    } finally {
      setSubmitting(false);
    }
  }

  const [editOpen, setEditOpen] = useState(false);
  const [editFields, setEditFields] = useState([]);
  const [editTitle, setEditTitle] = useState("");
  const [editSubmitting, setEditSubmitting] = useState(false);
  const [editingRow, setEditingRow] = useState(null);

  function onEdit(row) {
    setEditingRow(row);
    if (activeTab === TABS.MODULES) {
      setEditTitle("Sửa Module");
      setEditFields([
        { name: "moduleName", label: "Module Name", required: true, defaultValue: row.moduleName },
      ]);
    } else if (activeTab === TABS.PERMISSIONS) {
      setEditTitle("Sửa Permission");
      setEditFields([
        { name: "permissionName", label: "Permission Name", required: true, defaultValue: row.permissionName },
        { name: "description", label: "Description", type: "textarea", defaultValue: row.description },
      ]);
    } else {
      setEditTitle("Sửa Role");
      setEditFields([
        { name: "name", label: "Role Name", required: true, defaultValue: row.name },
        { name: "desc", label: "Description", type: "textarea", defaultValue: row.desc },
        { name: "isActive", label: "Active (true/false)", defaultValue: row.isActive, type: "text" },
      ]);
    }
    setEditOpen(true);
  }

  async function onDelete(row) {
    const label = activeTab === TABS.MODULES ? row.moduleName : activeTab === TABS.PERMISSIONS ? row.permissionName : row.name;
    const ok = window.confirm(`Xoá mục: ${label}?`);
    if (!ok) return;
    try {
      if (activeTab === TABS.MODULES) await rbacApi.deleteModule(row.moduleId || row.id);
      else if (activeTab === TABS.PERMISSIONS) await rbacApi.deletePermission(row.permissionId || row.id);
      else await rbacApi.deleteRole(row.roleId || row.id);
      setData((prev) => prev.filter((x) => {
        const key = activeTab === TABS.MODULES ? "moduleId" : activeTab === TABS.PERMISSIONS ? "permissionId" : "roleId";
        return x[key] !== row[key];
      }));
    } catch (e) {
      alert(e.message || "Xoá thất bại");
    }
  }

  async function onSubmitEdit(form) {
    try {
      setEditSubmitting(true);
      if (activeTab === TABS.MODULES) {
        await rbacApi.updateModule(editingRow.moduleId, { moduleName: form.moduleName });
        setData((prev) => prev.map((x) => x.moduleId === editingRow.moduleId ? { ...x, moduleName: form.moduleName } : x));
      } else if (activeTab === TABS.PERMISSIONS) {
        await rbacApi.updatePermission(editingRow.permissionId, { permissionName: form.permissionName, description: form.description });
        setData((prev) => prev.map((x) => x.permissionId === editingRow.permissionId ? { ...x, permissionName: form.permissionName, description: form.description } : x));
      } else {
        const payload = { name: form.name, desc: form.desc, isActive: String(form.isActive).toLowerCase() === "true" };
        await rbacApi.updateRole(editingRow.roleId, payload);
        setData((prev) => prev.map((x) => x.roleId === editingRow.roleId ? { ...x, ...payload } : x));
      }
      setEditOpen(false);
    } catch (e) {
      alert(e.message || "Cập nhật thất bại");
    } finally {
      setEditSubmitting(false);
    }
  }

  return (
    <div className="rbac-management-container">
      <div className="rbac-header">
        <h1 className="rbac-title">RBAC Management</h1>
        <p className="rbac-subtitle">Quản lý Modules, Permissions, và Roles</p>
      </div>

      <div className="rbac-tabs">
        <button
          className={`tab-button ${activeTab === TABS.MODULES ? "active" : ""}`}
          onClick={() => setActiveTab(TABS.MODULES)}
        >
          Modules
        </button>
        <button
          className={`tab-button ${activeTab === TABS.PERMISSIONS ? "active" : ""}`}
          onClick={() => setActiveTab(TABS.PERMISSIONS)}
        >
          Permissions
        </button>
        <button
          className={`tab-button ${activeTab === TABS.ROLES ? "active" : ""}`}
          onClick={() => setActiveTab(TABS.ROLES)}
        >
          Roles
        </button>
      </div>

      <div className="rbac-controls">
        <div className="controls-left">
          <button className="add-button" onClick={onClickAdd}>{addButtonText}</button>
        </div>
        <div className="controls-right">
          {activeTab === TABS.ROLES && (
            <select
              aria-label="Lọc trạng thái"
              value={roleStatus}
              onChange={(e) => { setRoleStatus(e.target.value); setPage(1); }}
              className="btn-secondary"
              style={{ padding: "8px 12px" }}
            >
              <option value="all">Tất cả</option>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </select>
          )}
          <div className="search-box">
            <input
              type="text"
              placeholder={activeTab === TABS.MODULES ? "Tìm Module name..." : activeTab === TABS.PERMISSIONS ? "Tìm Permission name..." : "Tìm Role name..."}
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          </div>
          <div className="sort-controls">
            <select
              value={sortKey}
              onChange={(e) => {
                setSortKey(e.target.value);
                setPage(1);
              }}
            >
              <option value="">-- Sắp xếp theo --</option>
              {sortOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>{opt.label}</option>
              ))}
            </select>
            <button
              className="sort-order-btn"
              onClick={() => setSortOrder((o) => (o === "asc" ? "desc" : "asc"))}
              title="Thay đổi thứ tự"
            >
              {sortOrder === "asc" ? "↑" : "↓"}
            </button>
            <select
              value={pageSize}
              onChange={(e) => {
                setPageSize(Number(e.target.value));
                setPage(1);
              }}
            >
              {[10, 20, 50].map((n) => (
                <option key={n} value={n}>{n}/trang</option>
              ))}
            </select>
          </div>
        </div>
      </div>
      {activeTab === TABS.ROLES && (
        <RBACModal
          isOpen={addRoleOpen}
          title="Thêm Role"
          fields={[
            { name: "name", label: "Role Name", required: true },
            { name: "desc", label: "Description", type: "textarea" },
          ]}
          onClose={() => setAddRoleOpen(false)}
          onSubmit={handleCreateRole}
          submitting={submitting}
        />
      )}
      {activeTab === TABS.MODULES && (
        <RBACModal
          isOpen={addModuleOpen}
          title="Thêm Module"
          fields={[
            { name: "moduleName", label: "Module Name", required: true },
          ]}
          onClose={() => setAddModuleOpen(false)}
          onSubmit={handleCreateModule}
          submitting={submitting}
        />
      )}
      {activeTab === TABS.PERMISSIONS && (
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
      )}

      <div className="rbac-table-container">
        {loading ? (
          <div className="loading-state">
            <div className="loading-spinner" />
            <div>Đang tải dữ liệu...</div>
          </div>
        ) : error ? (
          <div className="empty-state">
            <div>Lỗi: {error}</div>
          </div>
        ) : paginated.length === 0 ? (
          <div className="empty-state">
            <div>Không có dữ liệu</div>
          </div>
        ) : (
          <table className="rbac-table">
            <thead>
              <tr>
                {columns.map((col) => (
                  <th key={col.key}>{col.label}</th>
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
                    <div className="action-buttons">
                      <button className="action-btn edit-btn" title="Sửa" onClick={() => onEdit(row)}>
                        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z"/></svg>
                      </button>
                      <button className="action-btn delete-btn" title="Xoá" onClick={() => onDelete(row)}>
                        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z"/></svg>
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
            className="btn-secondary"
            onClick={() => setPage(1)}
            disabled={currentPage === 1}
          >
            «
          </button>
          <button
            className="btn-secondary"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={currentPage === 1}
          >
            ‹
          </button>
          <span style={{ padding: "0 8px" }}>
            Trang {currentPage}/{totalPages} ({total} bản ghi)
          </span>
          <button
            className="btn-secondary"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={currentPage >= totalPages}
          >
            ›
          </button>
          <button
            className="btn-secondary"
            onClick={() => setPage(totalPages)}
            disabled={currentPage >= totalPages}
          >
            »
          </button>
        </div>
      </div>
      <RBACModal
        isOpen={editOpen}
        title={editTitle}
        fields={editFields}
        onClose={() => setEditOpen(false)}
        onSubmit={onSubmitEdit}
        submitting={editSubmitting}
      />
    </div>
  );
}

