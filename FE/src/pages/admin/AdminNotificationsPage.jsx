// File: AdminNotificationsPage.jsx

import React, { useEffect, useState, useCallback } from "react";
import "./admin-notifications-page.css";
import { NotificationsApi } from "../../services/notifications";
import PermissionGuard from "../../components/PermissionGuard";
import { usePermission } from "../../hooks/usePermission";
import { MODULE_CODES, PERMISSION_CODES } from "../../constants/roleConstants";

const severityOptions = [
  { value: "", label: "Tất cả mức độ" },
  { value: "0", label: "Thông tin" },
  { value: "1", label: "Thành công" },
  { value: "2", label: "Cảnh báo" },
  { value: "3", label: "Lỗi" },
];

function severityLabel(sev) {
  switch (sev) {
    case 0:
      return "Thông tin";
    case 1:
      return "Thành công";
    case 2:
      return "Cảnh báo";
    case 3:
      return "Lỗi";
    default:
      return sev;
  }
}

function severityClass(sev) {
  switch (sev) {
    case 0:
      return "badge-severity badge-info";
    case 1:
      return "badge-severity badge-success";
    case 2:
      return "badge-severity badge-warning";
    case 3:
      return "badge-severity badge-error";
    default:
      return "badge-severity";
  }
}

const AdminNotificationsPage = () => {
  // Permission checks
  const { hasPermission: hasCreatePermission } = usePermission(MODULE_CODES.SETTINGS_MANAGER, PERMISSION_CODES.CREATE);
  const { hasPermission: hasViewDetailPermission } = usePermission(MODULE_CODES.SETTINGS_MANAGER, PERMISSION_CODES.VIEW_DETAIL);
  
  const [filters, setFilters] = useState({
    search: "",
    severity: "",
    isSystemGenerated: "",
    isGlobal: "",
    sortBy: "CreatedAtUtc",
    sortDescending: true,
    createdFromUtc: "",
    createdToUtc: "",
  });

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState("");
  const [selectedNotification, setSelectedNotification] = useState(null);

  // Options dropdown user / role
  const [targetOptions, setTargetOptions] = useState({
    roles: [],
    users: [],
  });
  const [targetOptionsLoading, setTargetOptionsLoading] = useState(false);
  const [targetOptionsError, setTargetOptionsError] = useState("");

  // Dropdown open/close (mặc định ĐÓNG)
  const [isUserDropdownOpen, setIsUserDropdownOpen] = useState(false);
  const [isRoleDropdownOpen, setIsRoleDropdownOpen] = useState(false);

  const [createForm, setCreateForm] = useState({
    title: "",
    message: "",
    severity: "0",
    isGlobal: false,
    relatedUrl: "",
    selectedRoleIds: [],
    selectedUserIds: [],
  });
  const [createErrors, setCreateErrors] = useState({});
  const [createSubmitting, setCreateSubmitting] = useState(false);

  // ====== Load options user/role cho modal tạo thông báo ======
  useEffect(() => {
    const loadOptions = async () => {
      setTargetOptionsLoading(true);
      setTargetOptionsError("");
      try {
        const data = await NotificationsApi.getManualTargetOptions();
        setTargetOptions({
          roles: Array.isArray(data.roles) ? data.roles : [],
          users: Array.isArray(data.users) ? data.users : [],
        });
      } catch (err) {
        console.error("Failed to load manual target options", err);
        setTargetOptionsError(
          "Không tải được danh sách người dùng / quyền. Bạn sẽ không thể chọn người nhận."
        );
      } finally {
        setTargetOptionsLoading(false);
      }
    };

    loadOptions();
  }, []);

  // ====== Load list ======
  const refreshList = useCallback(
    async () => {
      setLoading(true);
      setError("");

      try {
        const params = {
          pageNumber,
          pageSize,
          search: filters.search || undefined,
          severity:
            filters.severity !== "" ? Number(filters.severity) : undefined,
          isSystemGenerated:
            filters.isSystemGenerated === ""
              ? undefined
              : filters.isSystemGenerated === "true",
          isGlobal:
            filters.isGlobal === ""
              ? undefined
              : filters.isGlobal === "true",
          sortBy: filters.sortBy || "CreatedAtUtc",
          sortDescending: filters.sortDescending,
          createdFromUtc: filters.createdFromUtc || undefined,
          createdToUtc: filters.createdToUtc || undefined,
        };

        const result = await NotificationsApi.listAdminPaged(params);

        setItems(result.items || []);
        setTotal(result.total || 0);
      } catch (err) {
        console.error("Failed to load notifications", err);
        setError("Không tải được danh sách thông báo. Vui lòng thử lại.");
      } finally {
        setLoading(false);
      }
    },
    [filters, pageNumber, pageSize]
  );

  useEffect(() => {
    refreshList();
  }, [refreshList]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const handleFilterChange = (field, value) => {
    setFilters((prev) => ({
      ...prev,
      [field]: value,
    }));
    setPageNumber(1);
  };

  // Sort theo tiêu đề cột: click lần 1 ASC, lần 2 DESC
  const handleSortClick = (sortBy) => {
    setFilters((prev) => {
      if (prev.sortBy === sortBy) {
        return { ...prev, sortDescending: !prev.sortDescending };
      }
      return { ...prev, sortBy, sortDescending: false }; // lần đầu: ASC
    });
    setPageNumber(1);
  };

  const handleResetFilters = () => {
    setFilters({
      search: "",
      severity: "",
      isSystemGenerated: "",
      isGlobal: "",
      sortBy: "CreatedAtUtc",
      sortDescending: true,
      createdFromUtc: "",
      createdToUtc: "",
    });
    setPageNumber(1);
    setPageSize(20);
  };

  const renderSortIndicator = (columnKey) =>
    filters.sortBy === columnKey && (
      <span className="sort-indicator">
        {filters.sortDescending ? " ↓" : " ↑"}
      </span>
    );

  const handleOpenDetail = async (id) => {
    setIsDetailModalOpen(true);
    setSelectedNotification(null);
    setDetailError("");
    setDetailLoading(true);

    try {
      const data = await NotificationsApi.getDetail(id);
      setSelectedNotification(data);
    } catch (err) {
      console.error("Failed to load notification detail", err);
      setDetailError("Không tải được chi tiết thông báo.");
    } finally {
      setDetailLoading(false);
    }
  };

  const handleOpenCreate = () => {
    setCreateErrors({});
    setCreateForm({
      title: "",
      message: "",
      severity: "0",
      isGlobal: false,
      relatedUrl: "",
      selectedRoleIds: [],
      selectedUserIds: [],
    });
    // Mặc định đóng 2 dropdown khi mở modal
    setIsUserDropdownOpen(false);
    setIsRoleDropdownOpen(false);
    setIsCreateModalOpen(true);
  };

  // Toggle chọn 1 user
  const toggleUserSelection = (userId) => {
    setCreateForm((prev) => {
      const exists = prev.selectedUserIds.includes(userId);
      const selectedUserIds = exists
        ? prev.selectedUserIds.filter((id) => id !== userId)
        : [...prev.selectedUserIds, userId];
      return { ...prev, selectedUserIds };
    });
  };

  // Toggle chọn 1 role + sync với nút Gửi toàn hệ thống
  const toggleRoleSelection = (roleId) => {
    setCreateForm((prev) => {
      let selectedRoleIds;
      if (prev.selectedRoleIds.includes(roleId)) {
        selectedRoleIds = prev.selectedRoleIds.filter((id) => id !== roleId);
      } else {
        selectedRoleIds = [...prev.selectedRoleIds, roleId];
      }

      const allRoleIds = (targetOptions.roles || []).map((r) => r.roleId);
      const allSelected =
        allRoleIds.length > 0 &&
        allRoleIds.every((id) => selectedRoleIds.includes(id));

      return {
        ...prev,
        selectedRoleIds,
        isGlobal: allSelected, // chọn đủ tất cả role thì auto bật Gửi toàn hệ thống
      };
    });
  };

  // Nút gạt Gửi toàn hệ thống
  const handleGlobalToggle = (checked) => {
    setCreateForm((prev) => {
      let selectedRoleIds = prev.selectedRoleIds;
      let selectedUserIds = prev.selectedUserIds;

      if (checked) {
        // Bật: chọn tất cả user + tất cả role
        selectedRoleIds = (targetOptions.roles || []).map((r) => r.roleId);
        selectedUserIds = (targetOptions.users || []).map((u) => u.userId);
      } else {
        // Tắt: bỏ hết cả user lẫn role
        selectedRoleIds = [];
        selectedUserIds = [];
      }

      return {
        ...prev,
        isGlobal: checked,
        selectedRoleIds,
        selectedUserIds,
      };
    });
  };

  const validateCreateForm = () => {
    const errs = {};

    if (!createForm.title.trim()) {
      errs.title = "Title is required.";
    } else if (createForm.title.trim().length > 200) {
      errs.title = "Title must be at most 200 characters.";
    }

    if (!createForm.message.trim()) {
      errs.message = "Message is required.";
    } else if (createForm.message.trim().length > 1000) {
      errs.message = "Message must be at most 1000 characters.";
    }

    // Nếu KHÔNG gửi toàn hệ thống thì bắt buộc phải chọn ít nhất 1 user
    if (
      !createForm.isGlobal &&
      (!createForm.selectedUserIds ||
        createForm.selectedUserIds.length === 0)
    ) {
      errs.selectedUserIds =
        "Vui lòng chọn ít nhất một người nhận (user) hoặc bật chế độ gửi toàn hệ thống.";
    }

    const sevNum = Number(createForm.severity);
    if (Number.isNaN(sevNum) || sevNum < 0 || sevNum > 3) {
      errs.severity = "Severity must be between 0 and 3.";
    }

    if (createForm.relatedUrl && createForm.relatedUrl.length > 500) {
      errs.relatedUrl = "Related url is too long.";
    }

    setCreateErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handleCreateSubmit = async (e) => {
    e.preventDefault();
    if (!validateCreateForm()) return;

    // Tập user nhận: nếu gửi toàn hệ thống -> toàn bộ user trong options
    const allUserIds = (targetOptions.users || []).map((u) => u.userId);
    const targetUserIds = createForm.isGlobal
      ? allUserIds
      : createForm.selectedUserIds;

    // Tập role gửi: nếu gửi toàn hệ thống -> toàn bộ role trong options
    const allRoleIds = (targetOptions.roles || []).map((r) => r.roleId);
    const targetRoleIds = createForm.isGlobal
      ? allRoleIds
      : createForm.selectedRoleIds;

    const payload = {
      title: createForm.title.trim(),
      message: createForm.message.trim(),
      severity: Number(createForm.severity),
      isGlobal: !!createForm.isGlobal,
      relatedEntityType: null,
      relatedEntityId: null,
      relatedUrl: createForm.relatedUrl.trim() || null,
      targetUserIds: targetUserIds,
      targetRoleIds:
        targetRoleIds && targetRoleIds.length > 0 ? targetRoleIds : null,
    };

    setCreateSubmitting(true);

    try {
      await NotificationsApi.createManual(payload);
      setIsCreateModalOpen(false);
      await refreshList();
      window.alert("Tạo thông báo thành công.");
    } catch (err) {
      console.error("Failed to create notification", err);
      window.alert("Tạo thông báo thất bại. Vui lòng kiểm tra lại dữ liệu.");
    } finally {
      setCreateSubmitting(false);
    }
  };

  return (
    <div className="admin-notifications-page">
      <div className="admin-notifications-header">
        <div>
          <h1 className="admin-notifications-title">Thông báo hệ thống</h1>
        </div>
        <div>
          <PermissionGuard moduleCode={MODULE_CODES.SETTINGS_MANAGER} permissionCode={PERMISSION_CODES.CREATE}>
          <button
            type="button"
            className="btn btn-primary"
            onClick={handleOpenCreate}
          >
            + Tạo thông báo
          </button>
          </PermissionGuard>
        </div>
      </div>

      {/* BỘ LỌC */}
      <div className="notif-filters">
        <div className="notif-filters-row">
          <div className="notif-filter-item">
            <label className="notif-filter-label">Tìm kiếm</label>
            <input
              type="text"
              className="notif-input"
              placeholder="Tiêu đề / Nội dung..."
              value={filters.search}
              onChange={(e) => handleFilterChange("search", e.target.value)}
            />
          </div>

          <div className="notif-filter-item">
            <label className="notif-filter-label">Mức độ</label>
            <select
              className="notif-select"
              value={filters.severity}
              onChange={(e) => handleFilterChange("severity", e.target.value)}
            >
              {severityOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          <div className="notif-filter-item">
            <label className="notif-filter-label">Nguồn tạo</label>
            <select
              className="notif-select"
              value={filters.isSystemGenerated}
              onChange={(e) =>
                handleFilterChange("isSystemGenerated", e.target.value)
              }
            >
              <option value="">Tất cả</option>
              <option value="true">Chỉ hệ thống</option>
              <option value="false">Chỉ thủ công</option>
            </select>
          </div>

          <div className="notif-filter-item">
            <label className="notif-filter-label">Phạm vi</label>
            <select
              className="notif-select"
              value={filters.isGlobal}
              onChange={(e) => handleFilterChange("isGlobal", e.target.value)}
            >
              <option value="">Tất cả</option>
              <option value="true">Thông báo toàn hệ thống</option>
              <option value="false">Thông báo cho người dùng cụ thể</option>
            </select>
          </div>
        </div>

        <div className="notif-filters-row">
          <div className="notif-filter-item">
            <label className="notif-filter-label">Tạo từ ngày</label>
            <input
              type="date"
              className="notif-input"
              value={filters.createdFromUtc}
              onChange={(e) =>
                handleFilterChange("createdFromUtc", e.target.value)
              }
            />
          </div>
          <div className="notif-filter-item">
            <label className="notif-filter-label">Tạo đến ngày</label>
            <input
              type="date"
              className="notif-input"
              value={filters.createdToUtc}
              onChange={(e) =>
                handleFilterChange("createdToUtc", e.target.value)
              }
            />
          </div>

          <div className="notif-filter-item notif-filter-pagesize">
            <label className="notif-filter-label">Số dòng / trang</label>
            <select
              className="notif-select"
              value={pageSize}
              onChange={(e) => {
                setPageSize(Number(e.target.value) || 20);
                setPageNumber(1);
              }}
            >
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
            </select>
          </div>

          <div className="notif-filter-item notif-filter-reset">
            <label className="notif-filter-label">&nbsp;</label>
            <button
              type="button"
              className="btn btn-secondary btn-reset"
              onClick={handleResetFilters}
            >
              Đặt lại mặc định
            </button>
          </div>
        </div>
      </div>

      {/* BẢNG DANH SÁCH */}
      <div className="notif-table-wrapper">
        {loading && <div className="notif-loading">Đang tải...</div>}
        {error && <div className="notif-error">{error}</div>}

        {!loading && !error && (
          <>
            <table className="notif-table">
              <thead>
                <tr>
                  <th
                    className="sortable"
                    onClick={() => handleSortClick("CreatedAtUtc")}
                  >
                    Thời gian tạo
                    {renderSortIndicator("CreatedAtUtc")}
                  </th>
                  <th
                    className="sortable"
                    onClick={() => handleSortClick("Title")}
                  >
                    Tiêu đề / Nội dung
                    {renderSortIndicator("Title")}
                  </th>
                  <th
                    className="sortable"
                    onClick={() => handleSortClick("Severity")}
                  >
                    Mức độ
                    {renderSortIndicator("Severity")}
                  </th>
                  <th
                    className="sortable"
                    onClick={() => handleSortClick("IsSystemGenerated")}
                  >
                    Nguồn tạo
                    {renderSortIndicator("IsSystemGenerated")}
                  </th>
                  <th
                    className="sortable"
                    onClick={() => handleSortClick("IsGlobal")}
                  >
                    Phạm vi
                    {renderSortIndicator("IsGlobal")}
                  </th>
                  <th
                    className="sortable"
                    onClick={() => handleSortClick("TotalTargetUsers")}
                  >
                    Người nhận
                    {renderSortIndicator("TotalTargetUsers")}
                  </th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {items.length === 0 && (
                  <tr>
                    <td colSpan={7} className="notif-empty">
                      Không có thông báo nào.
                    </td>
                  </tr>
                )}

                {items.map((n) => (
                  <tr key={n.id}>
                    <td>
                      <span className="notif-date">
                        {new Date(n.createdAtUtc).toLocaleString()}
                      </span>
                    </td>
                    <td className="notif-title-cell">
                      <div className="notif-title-text" title={n.title}>
                        {n.title}
                      </div>
                      <div className="notif-message-preview">
                        {n.message.length > 80
                          ? n.message.slice(0, 80) + "..."
                          : n.message}
                      </div>
                    </td>
                    <td>
                      <span className={severityClass(n.severity)}>
                        {severityLabel(n.severity)}
                      </span>
                    </td>
                    <td>{n.isSystemGenerated ? "Hệ thống" : "Thủ công"}</td>
                    <td>{n.isGlobal ? "Toàn hệ thống" : "User cụ thể"}</td>
                    <td>
                      <span className="notif-targets">
                        Người dùng: {n.totalTargetUsers} | Đã đọc:{" "}
                        {n.readCount}
                      </span>
                    </td>
                    <td className="notif-actions-cell">
                      <PermissionGuard moduleCode={MODULE_CODES.SETTINGS_MANAGER} permissionCode={PERMISSION_CODES.VIEW_DETAIL}>
                      <button
                        type="button"
                        className="btn btn-link"
                        onClick={() => handleOpenDetail(n.id)}
                      >
                        Chi tiết
                      </button>
                      </PermissionGuard>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {/* PHÂN TRANG */}
            <div className="notif-pagination">
              <div className="notif-pagination-left">
                Tổng: <strong>{total}</strong> bản ghi
              </div>
              <div className="notif-pagination-right">
                <button
                  type="button"
                  className="btn btn-secondary"
                  disabled={pageNumber <= 1}
                  onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                >
                  Trang trước
                </button>
                <span className="notif-page-info">
                  Trang {pageNumber} / {totalPages}
                </span>
                <button
                  type="button"
                  className="btn btn-secondary"
                  disabled={pageNumber >= totalPages}
                  onClick={() =>
                    setPageNumber((p) => Math.min(totalPages, p + 1))
                  }
                >
                  Trang sau
                </button>
              </div>
            </div>
          </>
        )}
      </div>

      {/* CREATE MODAL */}
      {isCreateModalOpen && (
        <div className="notif-modal-backdrop">
          <div className="notif-modal">
            <div className="notif-modal-header">
              <h2>Tạo thông báo thủ công</h2>
              <div className="notif-modal-header-right">
                <span className="notif-global-label">Gửi toàn hệ thống</span>
                <label className="switch">
                  <input
                    type="checkbox"
                    checked={createForm.isGlobal}
                    onChange={(e) => handleGlobalToggle(e.target.checked)}
                  />
                  <span className="slider" />
                </label>
                {/* Không có nút X bên phải */}
              </div>
            </div>

            <form className="notif-modal-body" onSubmit={handleCreateSubmit}>
              {/* HÀNG 1: Tiêu đề + Mức độ (trái) & Nội dung chi tiết (phải) */}
              <div className="notif-form-row notif-form-row-top">
                <div className="notif-form-col-left">
                  <div className="notif-form-group">
                    <label>
                      Tiêu đề <span className="required">*</span>
                    </label>
                    <input
                      type="text"
                      className={`notif-input ${
                        createErrors.title ? "has-error" : ""
                      }`}
                      value={createForm.title}
                      onChange={(e) =>
                        setCreateForm((prev) => ({
                          ...prev,
                          title: e.target.value,
                        }))
                      }
                    />
                    {createErrors.title && (
                      <div className="field-error">{createErrors.title}</div>
                    )}
                  </div>

                  <div className="notif-form-group">
                    <label>
                      Mức độ <span className="required">*</span>
                    </label>
                    <select
                      className={`notif-select ${
                        createErrors.severity ? "has-error" : ""
                      }`}
                      value={createForm.severity}
                      onChange={(e) =>
                        setCreateForm((prev) => ({
                          ...prev,
                          severity: e.target.value,
                        }))
                      }
                    >
                      <option value="0">Thông tin</option>
                      <option value="1">Thành công</option>
                      <option value="2">Cảnh báo</option>
                      <option value="3">Lỗi</option>
                    </select>
                    {createErrors.severity && (
                      <div className="field-error">
                        {createErrors.severity}
                      </div>
                    )}
                  </div>
                </div>

                <div className="notif-form-col-right">
                  <div className="notif-form-group">
                    <label>
                      Nội dung chi tiết <span className="required">*</span>
                    </label>
                    <textarea
                      className={`notif-textarea ${
                        createErrors.message ? "has-error" : ""
                      }`}
                      rows={5}
                      value={createForm.message}
                      onChange={(e) =>
                        setCreateForm((prev) => ({
                          ...prev,
                          message: e.target.value,
                        }))
                      }
                    />
                    {createErrors.message && (
                      <div className="field-error">
                        {createErrors.message}
                      </div>
                    )}
                  </div>
                </div>
              </div>

              {/* HÀNG 2: Dropdown Người nhận + Nhóm quyền (cùng hàng) */}
              <div className="notif-form-row notif-form-row-targets">
                {/* Người nhận (user) */}
                <div className="notif-form-col-half">
                  <div className="notif-dropdown-group">
                    <div
                      className="notif-dropdown-header"
                      onClick={() =>
                        setIsUserDropdownOpen((open) => !open)
                      }
                    >
                      <div className="notif-dropdown-header-left">
                        <span className="notif-dropdown-title">
                          Áp dụng cho người dùng
                        </span>
                        <span className="notif-dropdown-count">
                          (
                          {createForm.selectedUserIds.length}/
                          {targetOptions.users.length} đã chọn)
                        </span>
                      </div>
                      <span
                        className={
                          "notif-dropdown-arrow" +
                          (isUserDropdownOpen ? " open" : "")
                        }
                      >
                        ▾
                      </span>
                    </div>

                    {isUserDropdownOpen && (
                      <div className="notif-dropdown-body">
                        {targetOptionsLoading && (
                          <div className="small-muted">
                            Đang tải danh sách người dùng...
                          </div>
                        )}
                        {targetOptionsError && (
                          <div className="field-error">
                            {targetOptionsError}
                          </div>
                        )}

                        <div className="notif-dropdown-list">
                          {targetOptions.users.map((u) => {
                            const selected =
                              createForm.selectedUserIds.includes(
                                u.userId
                              );
                            return (
                              <div
                                key={u.userId}
                                className="notif-dropdown-item"
                              >
                                <div className="notif-select-text">
                                  <div className="notif-select-title">
                                    {u.fullName || "(Chưa có tên)"}
                                  </div>
                                  <div className="notif-select-subtitle">
                                    {u.email}
                                  </div>
                                </div>
                                <label className="switch">
                                  <input
                                    type="checkbox"
                                    checked={selected}
                                    onChange={() =>
                                      toggleUserSelection(u.userId)
                                    }
                                  />
                                  <span className="slider" />
                                </label>
                              </div>
                            );
                          })}

                          {!targetOptionsLoading &&
                            targetOptions.users.length === 0 && (
                              <div className="notif-empty-inline">
                                Không có người dùng nào.
                              </div>
                            )}
                        </div>
                      </div>
                    )}

                    {createErrors.selectedUserIds && (
                      <div className="field-error">
                        {createErrors.selectedUserIds}
                      </div>
                    )}
                  </div>
                </div>

                {/* Nhóm quyền (role) */}
                <div className="notif-form-col-half">
                  <div className="notif-dropdown-group">
                    <div
                      className="notif-dropdown-header"
                      onClick={() =>
                        setIsRoleDropdownOpen((open) => !open)
                      }
                    >
                      <div className="notif-dropdown-header-left">
                        <span className="notif-dropdown-title">
                          Áp dụng cho nhóm quyền
                        </span>
                        <span className="notif-dropdown-count">
                          (
                          {createForm.selectedRoleIds.length}/
                          {targetOptions.roles.length} đã chọn)
                        </span>
                      </div>
                      <span
                        className={
                          "notif-dropdown-arrow" +
                          (isRoleDropdownOpen ? " open" : "")
                        }
                      >
                        ▾
                      </span>
                    </div>

                    {isRoleDropdownOpen && (
                      <div className="notif-dropdown-body">
                        <div className="notif-dropdown-list">
                          {targetOptions.roles.map((r) => {
                            const selected =
                              createForm.selectedRoleIds.includes(
                                r.roleId
                              );
                            const roleName =
                              r.roleName || r.roleId || "Role";
                            return (
                              <div
                                key={r.roleId}
                                className="notif-dropdown-item"
                              >
                                <div className="notif-select-text">
                                  <div className="notif-select-title">
                                    {roleName}
                                  </div>
                                </div>
                                <label className="switch">
                                  <input
                                    type="checkbox"
                                    checked={selected}
                                    onChange={() =>
                                      toggleRoleSelection(r.roleId)
                                    }
                                  />
                                  <span className="slider" />
                                </label>
                              </div>
                            );
                          })}

                          {targetOptions.roles.length === 0 && (
                            <div className="notif-empty-inline">
                              Không có nhóm quyền nào.
                            </div>
                          )}
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              </div>

              {/* HÀNG 3: Đường dẫn liên quan – hàng cuối */}
              <div className="notif-form-group">
                <label>Đường dẫn liên quan (tùy chọn)</label>
                <input
                  type="text"
                  className={`notif-input ${
                    createErrors.relatedUrl ? "has-error" : ""
                  }`}
                  value={createForm.relatedUrl}
                  onChange={(e) =>
                    setCreateForm((prev) => ({
                      ...prev,
                      relatedUrl: e.target.value,
                    }))
                  }
                  placeholder="VD: /admin/orders/123"
                />
                {createErrors.relatedUrl && (
                  <div className="field-error">
                    {createErrors.relatedUrl}
                  </div>
                )}
              </div>

              <div className="notif-modal-footer">
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setIsCreateModalOpen(false)}
                  disabled={createSubmitting}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="btn btn-primary"
                  disabled={createSubmitting}
                >
                  {createSubmitting ? "Đang tạo..." : "Tạo thông báo"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* DETAIL MODAL */}
      {isDetailModalOpen && (
        <div className="notif-modal-backdrop">
          <div className="notif-modal notif-modal-detail">
            <div className="notif-modal-header">
              <h2>Chi tiết thông báo</h2>
              <button
                type="button"
                className="notif-modal-close"
                onClick={() => setIsDetailModalOpen(false)}
              >
                ×
              </button>
            </div>

            <div className="notif-modal-body notif-detail-body">
              {detailLoading && (
                <div className="notif-loading">Đang tải chi tiết...</div>
              )}
              {detailError && (
                <div className="notif-error">{detailError}</div>
              )}
              {!detailLoading && !detailError && selectedNotification && (
                <>
                  <div className="detail-row">
                    <span className="detail-label">ID:</span>
                    <span className="detail-value">
                      {selectedNotification.id}
                    </span>
                  </div>

                  <div className="detail-row">
                    <span className="detail-label">Title:</span>
                    <span className="detail-value">
                      {selectedNotification.title}
                    </span>
                  </div>

                  <div className="detail-row detail-row-message">
                    <span className="detail-label">Message:</span>
                    <div className="detail-value detail-value-message">
                      {selectedNotification.message}
                    </div>
                  </div>

                  <div className="detail-row">
                    <span className="detail-label">Severity:</span>
                    <span className="detail-value">
                      <span
                        className={severityClass(
                          selectedNotification.severity
                        )}
                      >
                        {severityLabel(selectedNotification.severity)}
                      </span>
                    </span>
                  </div>

                  <div className="detail-row">
                    <span className="detail-label">System generated:</span>
                    <span className="detail-value">
                      {selectedNotification.isSystemGenerated
                        ? "Yes"
                        : "No"}
                    </span>
                  </div>

                  <div className="detail-row">
                    <span className="detail-label">Global:</span>
                    <span className="detail-value">
                      {selectedNotification.isGlobal ? "Yes" : "No"}
                    </span>
                  </div>

                  <div className="detail-row">
                    <span className="detail-label">Created At:</span>
                    <span className="detail-value">
                      {new Date(
                        selectedNotification.createdAtUtc
                      ).toLocaleString()}
                    </span>
                  </div>

                  <div className="detail-row">
                    <span className="detail-label">Created By:</span>
                    <span className="detail-value">
                      {selectedNotification.createdByUserEmail ||
                        selectedNotification.createdByUserId ||
                        "-"}
                    </span>
                  </div>

                  <div className="detail-row">
                    <span className="detail-label">Related Entity:</span>
                    <span className="detail-value">
                      {selectedNotification.relatedEntityType ||
                      selectedNotification.relatedEntityId
                        ? `${selectedNotification.relatedEntityType || ""} ${
                            selectedNotification.relatedEntityId || ""
                          }`.trim()
                        : "-"}
                    </span>
                  </div>

                  <div className="detail-row">
                    <span className="detail-label">Related Url:</span>
                    <span className="detail-value">
                      {selectedNotification.relatedUrl || "-"}
                    </span>
                  </div>

                  <div className="detail-row">
                    <span className="detail-label">Targets:</span>
                    <span className="detail-value">
                      Users: {selectedNotification.totalTargetUsers} | Read:{" "}
                      {selectedNotification.readCount} | Unread:{" "}
                      {selectedNotification.unreadCount}
                    </span>
                  </div>

                  <div className="detail-row detail-row-roles">
                    <span className="detail-label">Target Roles:</span>
                    <div className="detail-value">
                      {selectedNotification.targetRoles &&
                      selectedNotification.targetRoles.length > 0 ? (
                        <ul className="detail-role-list">
                          {selectedNotification.targetRoles.map((r) => (
                            <li key={r.roleId}>
                              <span className="role-id">{r.roleId}</span>
                              {r.roleName && (
                                <span className="role-name">
                                  {" "}
                                  - {r.roleName}
                                </span>
                              )}
                            </li>
                          ))}
                        </ul>
                      ) : (
                        <span>-</span>
                      )}
                    </div>
                  </div>
                </>
              )}
            </div>

            <div className="notif-modal-footer">
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => setIsDetailModalOpen(false)}
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminNotificationsPage;
