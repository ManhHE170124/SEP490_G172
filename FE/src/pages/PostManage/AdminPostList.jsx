/**
 * @file: AdminPostList.jsx
 * @author: HieuNDHE173169
 * @created 2025-10-30
 * @lastUpdated 2025-10-30
 * @version: 1.0.0
 * @summary: Admin page for managing posts with full CRUD operations, search, filter, sort, and pagination
 */

import React, { useEffect, useState, useMemo, useCallback, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { postsApi } from "../../services/postsApi";
import useToast from "../../hooks/useToast";
import ToastContainer from "../../components/Toast/ToastContainer";
import { usePermission } from "../../hooks/usePermission";
import { MODULE_CODES } from "../../constants/accessControl";
import "./AdminPostList.css";

export default function AdminPostList() {
  const navigate = useNavigate();
  const { toasts, showInfo, showSuccess, showError, removeToast, confirmDialog, showConfirm } = useToast();
  
  // Check permission to view list
  const { hasPermission: canViewList, loading: permissionLoading } = usePermission(MODULE_CODES.POST_MANAGER, "VIEW_LIST");
  
  // Check permission to view detail (for preview and edit)
  const { hasPermission: canViewDetail } = usePermission(MODULE_CODES.POST_MANAGER, "VIEW_DETAIL");
  
  // Global network error handler - only show one toast for network errors
  const networkErrorShownRef = useRef(false);
  // Global permission error handler - only show one toast for permission errors
  const permissionErrorShownRef = useRef(false);
  useEffect(() => {
    // Reset the flags when component mounts
    networkErrorShownRef.current = false;
    permissionErrorShownRef.current = false;
  }, []);

  // Data state
  const [posts, setPosts] = useState([]);
  const [posttypes, setPosttypes] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // Filter & Search state
  const [search, setSearch] = useState("");
  const [posttypeFilter, setPosttypeFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [sortKey, setSortKey] = useState("createdAt");
  const [sortOrder, setSortOrder] = useState("desc");
  const [viewMode, setViewMode] = useState("table"); // table | grid

  // Pagination state
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  // Selection state
  const [selectedPosts, setSelectedPosts] = useState([]);

  // Load data
  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    setError("");
    try {
      const [postsData, posttypesData] = await Promise.all([
        postsApi.getAllPosts(),
        postsApi.getPosttypes()
      ]);
      setPosts(Array.isArray(postsData) ? postsData : []);
      setPosttypes(Array.isArray(posttypesData) ? posttypesData : []);
    } catch (err) {
      setError(err.message || "Không thể tải dữ liệu");
      // Handle network errors globally - only show one toast
      if (err.isNetworkError || err.message === 'Lỗi kết nối đến máy chủ') {
        if (!networkErrorShownRef.current) {
          networkErrorShownRef.current = true;
          showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
        }
      } else {
        // Check if error message contains permission denied - only show once
        const isPermissionError = err.message?.includes('không có quyền') || 
                                  err.message?.includes('quyền truy cập') ||
                                  err.response?.status === 403;
        if (isPermissionError && !permissionErrorShownRef.current) {
          permissionErrorShownRef.current = true;
          showError("Lỗi tải dữ liệu", err.message || "Bạn không có quyền truy cập chức năng này.");
        } else if (!isPermissionError) {
        showError("Lỗi", err.message || "Không thể tải danh sách bài viết");
        }
      }
    } finally {
      setLoading(false);
    }
  };

  // Format date helper
  const formatDate = (value) => {
    if (!value) return "";
    try {
      const d = new Date(value);
      if (Number.isNaN(d.getTime())) return "";
      return d.toLocaleString("vi-VN", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit"
      });
    } catch {
      return "";
    }
  };

  // Truncate text helper
  const truncateText = (text, maxLength = 20) => {
    if (!text) return "";
    if (text.length <= maxLength) return text;
    return text.substring(0, maxLength).trim() + "...";
  };

  // Filter & Sort logic
  const filteredSorted = useMemo(() => {
    let filtered = [...posts];

    // Search filter
    if (search.trim()) {
      const searchLower = search.toLowerCase();
      filtered = filtered.filter(post =>
        (post.title || "").toLowerCase().includes(searchLower) ||
        (post.shortDescription || "").toLowerCase().includes(searchLower) ||
        (post.authorName || "").toLowerCase().includes(searchLower)
      );
    }

    // PostType filter
    if (posttypeFilter !== "all") {
      const filterTypeId = String(posttypeFilter);
      filtered = filtered.filter(post => {
        const postTypeId = post.posttypeId || post.postTypeId || post.PosttypeId || post.PostTypeId;
        if (!postTypeId) return false;
        return String(postTypeId) === filterTypeId;
      });
    }

    // Status filter
    if (statusFilter !== "all") {
      filtered = filtered.filter(post => post.status === statusFilter);
    }

    // Sort
    filtered.sort((a, b) => {
      let aVal, bVal;

      // Handle posttypeName (special case - nested property)
      if (sortKey === "posttypeName") {
        aVal = a.posttypeName || a.postTypeName || a.PosttypeName || "";
        bVal = b.posttypeName || b.postTypeName || b.PosttypeName || "";
      } else {
        aVal = a[sortKey];
        bVal = b[sortKey];
      }

      // Handle null/undefined
      if (aVal == null && bVal == null) return 0;
      if (aVal == null) return sortOrder === "asc" ? -1 : 1;
      if (bVal == null) return sortOrder === "asc" ? 1 : -1;

      // Handle dates
      if (sortKey === "createdAt" || sortKey === "updatedAt") {
        const aDate = new Date(aVal).getTime();
        const bDate = new Date(bVal).getTime();
        return sortOrder === "asc" ? aDate - bDate : bDate - aDate;
      }

      // Handle numbers
      if (typeof aVal === "number" && typeof bVal === "number") {
        return sortOrder === "asc" ? aVal - bVal : bVal - aVal;
      }

      // Handle strings
      if (typeof aVal === "string" && typeof bVal === "string") {
        return sortOrder === "asc"
          ? aVal.localeCompare(bVal)
          : bVal.localeCompare(aVal);
      }

      return 0;
    });

    return filtered;
  }, [posts, search, posttypeFilter, statusFilter, sortKey, sortOrder]);

  // Pagination
  const total = filteredSorted.length;
  const totalPages = total === 0 ? 0 : Math.max(1, Math.ceil(total / pageSize));
  const currentPage = totalPages === 0 ? 0 : Math.min(page, totalPages);
  const paginated = useMemo(() => {
    if (total === 0) return [];
    const start = (currentPage - 1) * pageSize;
    return filteredSorted.slice(start, start + pageSize);
  }, [filteredSorted, currentPage, pageSize, total]);

  // Selection handlers
  const handleSelectAll = useCallback(() => {
    if (selectedPosts.length === paginated.length) {
      setSelectedPosts([]);
    } else {
      setSelectedPosts(paginated.map(p => p.postId));
    }
  }, [selectedPosts.length, paginated]);

  const handleSelectPost = useCallback((postId) => {
    setSelectedPosts(prev =>
      prev.includes(postId)
        ? prev.filter(id => id !== postId)
        : [...prev, postId]
    );
  }, []);

  // Actions
  const handleCreate = () => {
    navigate("/post-create-edit");
  };

  const handleEdit = (postId) => {
    if (!canViewDetail) {
      showError(
        "Không có quyền",
        "Bạn không có quyền xem chi tiết và chỉnh sửa bài viết."
      );
      return;
    }
      navigate(`/post-create-edit/${postId}`);
  };

  const handlePreview = (post) => {
    if (!canViewDetail) {
      showError(
        "Không có quyền",
        "Bạn không có quyền xem chi tiết bài viết."
      );
      return;
    }
    if (!post.slug) {
      showError("Lỗi", "Bài viết chưa có slug. Vui lòng cập nhật bài viết trước.");
      return;
    }
    // Open preview in new tab
    window.open(`/blog/${post.slug}`, '_blank');
  };

  const handleDelete = (postId) => {
    const post = posts.find(p => p.postId === postId);
    showConfirm(
      "Xác nhận xóa",
      `Bạn có chắc chắn muốn xóa bài viết "${post?.title || ""}"? Hành động này không thể hoàn tác.`,
      async () => {
        try {
          await postsApi.deletePost(postId);
          setPosts(prev => prev.filter(p => p.postId !== postId));
          setSelectedPosts(prev => prev.filter(id => id !== postId));
          showSuccess("Thành công", "Bài viết đã được xóa");
        } catch (err) {
          console.log("Lỗi khi xóa bài viết:", err);
          // Handle network errors globally - only show one toast
          if (err.isNetworkError || err.message === 'Lỗi kết nối đến máy chủ') {
            if (!networkErrorShownRef.current) {
              networkErrorShownRef.current = true;
              showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
            }
          } else {
            showError("Lỗi khi xóa bài viết", err.message || "Không thể xóa bài viết");
          }
        }
      }
    );
  };

  const handleBulkDelete = () => {
    if (selectedPosts.length === 0) {
      showError("Lỗi", "Vui lòng chọn ít nhất một bài viết");
      return;
    }

    showConfirm(
      "Xác nhận xóa",
      `Bạn có chắc chắn muốn xóa ${selectedPosts.length} bài viết đã chọn? Hành động này không thể hoàn tác.`,
      async () => {
        try {
          await Promise.all(selectedPosts.map(id => postsApi.deletePost(id)));
          setPosts(prev => prev.filter(p => !selectedPosts.includes(p.postId)));
          setSelectedPosts([]);
          showSuccess("Thành công", `Đã xóa ${selectedPosts.length} bài viết`);
        } catch (err) {
          console.log("Lỗi khi xóa nhiều bài viết:", err);
          // Handle network errors globally - only show one toast
          if (err.isNetworkError || err.message === 'Lỗi kết nối đến máy chủ') {
            if (!networkErrorShownRef.current) {
              networkErrorShownRef.current = true;
              showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
            }
          } else {
            showError("Lỗi khi xóa nhiều bài viết", err.message || "Không thể xóa bài viết");
          }
        }
      }
    );
  };

  const handleStatusChange = async (postId, newStatus) => {
    try {
      const post = posts.find(p => p.postId === postId);
      if (!post) return;

      const postTypeId = post.posttypeId || post.postTypeId || post.PosttypeId || post.id;
      await postsApi.updatePost(postId, {
        title: post.title,
        shortDescription: post.shortDescription || "",
        content: post.content || "",
        thumbnail: post.thumbnail || "",
        posttypeId: postTypeId,
        status: newStatus,
        metaTitle: post.metaTitle || "",
        tagIds: post.tags?.map(t => t.tagId) || []
      });

      setPosts(prev =>
        prev.map(p =>
          p.postId === postId ? { ...p, status: newStatus } : p
        )
      );
      showSuccess("Thành công", "Trạng thái đã được cập nhật");
    } catch (err) {
      console.log("Lỗi khi thay đổi trạng thái:", err);
      // Handle network errors globally - only show one toast
      if (err.isNetworkError || err.message === 'Lỗi kết nối đến máy chủ') {
        if (!networkErrorShownRef.current) {
          networkErrorShownRef.current = true;
          showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
        }
      } else {
        showError("Lỗi thay đổi trạng thái", err.message || "Không thể cập nhật trạng thái");
      }
    }
  };


  // Get status label
  const getStatusLabel = (status) => {
    const statusMap = {
      Draft: "Bản nháp",
      Published: "Công khai",
      Private: "Riêng tư"
    };
    return statusMap[status] || status;
  };

  // Reset filters
  const handleResetFilters = () => {
    setSearch("");
    setPosttypeFilter("all");
    setStatusFilter("all");
    setSortKey("createdAt");
    setSortOrder("desc");
    setPage(1);
  };

  // Handle column sort
  const handleColumnSort = (columnKey) => {
    if (sortKey === columnKey) {
      setSortOrder(sortOrder === "asc" ? "desc" : "asc");
    } else {
      setSortKey(columnKey);
      setSortOrder("asc");
    }
  };

  // Status badge
  const getStatusBadge = (status) => {
    const statusMap = {
      Draft: { label: "Bản nháp", color: "#6c757d" },
      Published: { label: "Công khai", color: "#28a745" },
      Private: { label: "Riêng tư", color: "#dc3545" }
    };
    const statusInfo = statusMap[status] || { label: status, color: "#6c757d" };
    return (
      <span
        style={{
          padding: "4px 8px",
          borderRadius: "12px",
          fontSize: "12px",
          fontWeight: "500",
          background: statusInfo.color + "20",
          color: statusInfo.color,
          border: `1px solid ${statusInfo.color}40`
        }}
      >
        {statusInfo.label}
      </span>
    );
  };

  // Reset filters when change
  useEffect(() => {
    setPage(1);
  }, [search, posttypeFilter, statusFilter, sortKey, sortOrder]);

  // Show loading while checking permission
  if (permissionLoading) {
    return (
      <div className="apl-post-list-container">
        <div style={{ textAlign: 'center', padding: '60px 20px' }}>
          <div className="apl-loading-spinner" />
          <div>Đang kiểm tra quyền...</div>
        </div>
      </div>
    );
  }

  // Show access denied message if no VIEW_LIST permission
  if (!canViewList) {
    return (
      <div className="apl-post-list-container">
        <ToastContainer
          toasts={toasts}
          onRemove={removeToast}
          confirmDialog={confirmDialog}
        />
        <div className="apl-post-list-header">
          <div>
            <h1 className="apl-post-list-title">Quản lý bài viết</h1>
            <p className="apl-post-list-subtitle">Quản lý, chỉnh sửa và xóa bài viết</p>
          </div>
        </div>
        <div style={{ textAlign: 'center', padding: '60px 20px' }}>
          <div style={{ fontSize: '48px', marginBottom: '16px' }}>🔒</div>
          <h2>Không có quyền xem danh sách</h2>
          <p style={{ color: '#666', marginBottom: '24px' }}>
            Bạn không có quyền xem danh sách bài viết. Vui lòng liên hệ quản trị viên để được cấp quyền.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="apl-post-list-container">
      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />

      {/* Header */}
      <div className="apl-post-list-header">
        <div>
          <h1 className="apl-post-list-title">Quản lý bài viết</h1>
          <p className="apl-post-list-subtitle">Quản lý, chỉnh sửa và xóa bài viết</p>
        </div>
        <div style={{ display: "flex", gap: "0.75rem" }}>
          <button 
            className="apl-btn-secondary" 
            onClick={() => navigate("/post-dashboard")}
            style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}
          >
            <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16">
              <path d="M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z"/>
            </svg>
            Dashboard
          </button>
          <button className="apl-add-button" onClick={handleCreate}>
            + Tạo bài viết mới
          </button>
        </div>
      </div>

      {/* Controls */}
      <div className="apl-post-list-controls">
        <div className="apl-controls-left">
          {selectedPosts.length > 0 && (
            <div className="apl-bulk-actions">
              <span className="apl-selected-count">{selectedPosts.length} đã chọn</span>
              <button
                className="apl-btn-secondary apl-btn-danger"
                onClick={handleBulkDelete}
                style={{ marginLeft: "8px" }}
              >
                Xóa đã chọn
              </button>
            </div>
          )}
          {/* Search - Always on left */}
          <div className="apl-search-box">
            <input
              type="text"
              placeholder="Tìm kiếm tiêu đề..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </div>

        <div className="apl-controls-right">
          {/* Filters with labels */}
          <div className="apl-filter-group">
            <label className="apl-filter-label">Danh mục:</label>
            <select
              value={posttypeFilter}
              onChange={(e) => setPosttypeFilter(e.target.value)}
              className="apl-filter-select"
            >
              <option value="all">Tất cả</option>
              {posttypes.map((pt) => {
                const ptId = pt.posttypeId || pt.postTypeId || pt.PosttypeId || pt.id;
                const ptName = pt.posttypeName || pt.postTypeName || pt.PosttypeName || pt.name || "";
                return (
                  <option key={ptId} value={ptId}>
                    {ptName}
                  </option>
                );
              })}
            </select>
          </div>

          <div className="apl-filter-group">
            <label className="apl-filter-label">Trạng thái:</label>
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="apl-filter-select"
            >
              <option value="all">Tất cả</option>
              <option value="Published">Công khai</option>
              <option value="Private">Riêng tư</option>
              <option value="Draft">Bản nháp</option>
            </select>
          </div>

          {/* Reset Button */}
          <button
            className="apl-btn-secondary"
            onClick={handleResetFilters}
            title="Đặt lại bộ lọc"
            style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}
          >
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" width="16" height="16">
              <path d="M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z"/>
            </svg>
            Đặt lại
          </button>

          {/* View Mode Toggle */}
          <div className="apl-view-toggle">
            <button
              className={`apl-view-btn ${viewMode === "table" ? "active" : ""}`}
              onClick={() => setViewMode("table")}
              title="Xem dạng bảng"
            >
              ≡
            </button>
            <button
              className={`apl-view-btn ${viewMode === "grid" ? "active" : ""}`}
              onClick={() => setViewMode("grid")}
              title="Xem dạng lưới"
            >
              ⊞
            </button>
          </div>
        </div>
      </div>

      {/* Content */}
      <div className="apl-post-list-content">
        {loading ? (
          <div className="apl-loading-state">
            <div className="apl-loading-spinner" />
            <div>Đang tải dữ liệu...</div>
          </div>
        ) : error ? (
          <div className="apl-empty-state">
            <div>Lỗi: {error}</div>
            <button className="apl-btn-secondary" onClick={loadData} style={{ marginTop: "12px" }}>
              Thử lại
            </button>
          </div>
        ) : total === 0 ? (
          <div className="apl-empty-state">
            <div>Không có bài viết nào</div>
            {(search || posttypeFilter !== "all" || statusFilter !== "all") && (
              <button className="apl-btn-secondary" onClick={handleResetFilters} style={{ marginTop: "12px" }}>
                Đặt lại bộ lọc
              </button>
            )}
          </div>
        ) : paginated.length === 0 ? (
          <div className="apl-empty-state">
            <div>Không có bài viết nào</div>
            <button className="apl-add-button" onClick={handleCreate} style={{ marginTop: "12px" }}>
              Tạo bài viết đầu tiên
            </button>
          </div>
        ) : viewMode === "table" ? (
          <table className="apl-post-list-table">
            <thead>
              <tr>
                <th style={{ width: "40px" }}>
                  <input
                    type="checkbox"
                    checked={selectedPosts.length === paginated.length && paginated.length > 0}
                    onChange={handleSelectAll}
                  />
                </th>
                <th style={{ width: "80px" }}>Ảnh</th>
                <th>
                  <div 
                    className="apl-sortable-header" 
                    onClick={() => handleColumnSort("title")}
                    onKeyDown={(e) => e.key === "Enter" && handleColumnSort("title")}
                    role="button"
                    tabIndex={0}
                  >
                    Tiêu đề
                    {sortKey === "title" && (sortOrder === "asc" ? " ↑" : " ↓")}
                  </div>
                </th>
                <th style={{ width: "120px" }}>
                  <div 
                    className="apl-sortable-header" 
                    onClick={() => handleColumnSort("posttypeName")}
                    onKeyDown={(e) => e.key === "Enter" && handleColumnSort("posttypeName")}
                    role="button"
                    tabIndex={0}
                  >
                    Danh mục
                    {sortKey === "posttypeName" && (sortOrder === "asc" ? " ↑" : " ↓")}
                  </div>
                </th>
                <th style={{ width: "120px" }}>
                  <div 
                    className="apl-sortable-header" 
                    onClick={() => handleColumnSort("authorName")}
                    onKeyDown={(e) => e.key === "Enter" && handleColumnSort("authorName")}
                    role="button"
                    tabIndex={0}
                  >
                    Người phụ trách
                    {sortKey === "authorName" && (sortOrder === "asc" ? " ↑" : " ↓")}
                  </div>
                </th>
                <th style={{ width: "120px" }}>
                  <div 
                    className="apl-sortable-header" 
                    onClick={() => handleColumnSort("status")}
                    onKeyDown={(e) => e.key === "Enter" && handleColumnSort("status")}
                    role="button"
                    tabIndex={0}
                  >
                    Trạng thái
                    {sortKey === "status" && (sortOrder === "asc" ? " ↑" : " ↓")}
                  </div>
                </th>
                <th style={{ width: "100px" }}>
                  <div 
                    className="apl-sortable-header" 
                    onClick={() => handleColumnSort("viewCount")}
                    onKeyDown={(e) => e.key === "Enter" && handleColumnSort("viewCount")}
                    role="button"
                    tabIndex={0}
                  >
                    Lượt xem
                    {sortKey === "viewCount" && (sortOrder === "asc" ? " ↑" : " ↓")}
                  </div>
                </th>
                <th style={{ width: "150px" }}>
                  <div 
                    className="apl-sortable-header" 
                    onClick={() => handleColumnSort("createdAt")}
                    onKeyDown={(e) => e.key === "Enter" && handleColumnSort("createdAt")}
                    role="button"
                    tabIndex={0}
                  >
                    Ngày tạo
                    {sortKey === "createdAt" && (sortOrder === "asc" ? " ↑" : " ↓")}
                  </div>
                </th>
                <th style={{ width: "150px" }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {paginated.map((post) => (
                <tr key={post.postId}>
                  <td>
                    <input
                      type="checkbox"
                      checked={selectedPosts.includes(post.postId)}
                      onChange={() => handleSelectPost(post.postId)}
                    />
                  </td>
                  <td>
                    {post.thumbnail ? (
                      <img
                        src={post.thumbnail}
                        alt={post.title}
                        className="apl-post-thumbnail"
                        onError={(e) => {
                          e.target.style.display = "none";
                        }}
                      />
                    ) : (
                      <div className="apl-post-thumbnail-placeholder">📄</div>
                    )}
                  </td>
                  <td>
                    <div className="apl-post-title-cell">
                      <div className="apl-post-title" title={post.title || "(Không có tiêu đề)"}>
                        {truncateText(post.title || "(Không có tiêu đề)", 20)}
                      </div>
                      {post.shortDescription && (
                        <div className="apl-post-short-desc" title={post.shortDescription}>
                          {truncateText(post.shortDescription, 20)}
                        </div>
                      )}
                      {post.tags && post.tags.length > 0 && (
                        <div className="apl-post-tags">
                          {post.tags.slice(0, 3).map((tag) => (
                            <span key={tag.tagId} className="apl-tag-badge">
                              {tag.tagName}
                            </span>
                          ))}
                          {post.tags.length > 3 && (
                            <span className="apl-tag-badge">+{post.tags.length - 3}</span>
                          )}
                        </div>
                      )}
                    </div>
                  </td>
                  <td>{post.posttypeName || post.postTypeName || post.PosttypeName || "-"}</td>
                  <td>{post.authorName || "-"}</td>
                  <td>
                    {getStatusBadge(post.status)}
                  </td>
                  <td>
                    <span className="apl-view-count">{post.viewCount || 0}</span>
                  </td>
                  <td>{formatDate(post.createdAt)}</td>
                  <td>
                    <div className="apl-action-buttons">
                      <button
                        className="apl-action-btn apl-view-btn"
                        title="Xem trước"
                        onClick={() => handlePreview(post)}
                      >
                        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" width="16" height="16">
                          <path d="M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z"/>
                        </svg>
                      </button>
                      <button
                        className="apl-action-btn apl-update-btn"
                        title="Sửa"
                        onClick={() => handleEdit(post.postId)}
                      >
                        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" width="16" height="16">
                          <path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z"/>
                        </svg>
                      </button>
                      <button
                        className="apl-action-btn apl-delete-btn"
                        title="Xóa"
                        onClick={() => handleDelete(post.postId)}
                      >
                        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" width="16" height="16">
                          <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z"/>
                        </svg>
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <div className="apl-post-grid">
            {paginated.map((post) => (
              <div key={post.postId} className="apl-post-card">
                <div className="apl-post-card-checkbox">
                  <input
                    type="checkbox"
                    checked={selectedPosts.includes(post.postId)}
                    onChange={() => handleSelectPost(post.postId)}
                  />
                </div>
                <div className="apl-post-card-image-wrapper">
                  {post.thumbnail ? (
                    <img
                      src={post.thumbnail}
                      alt={post.title}
                      className="apl-post-card-image"
                      onError={(e) => {
                        e.target.style.display = "none";
                      }}
                    />
                  ) : (
                    <div className="apl-post-card-image-placeholder">📄</div>
                  )}
                  {post.tags && post.tags.length > 0 && (
                    <div className="apl-post-card-tags-overlay">
                      {post.tags.slice(0, 3).map((tag) => (
                        <span key={tag.tagId || tag.TagId || tag.id} className="apl-tag-badge">
                          {tag.tagName || tag.TagName || tag.name}
                        </span>
                      ))}
                      {post.tags.length > 3 && (
                        <span className="apl-tag-badge">
                          +{post.tags.length - 3}
                        </span>
                      )}
                    </div>
                  )}
                </div>
                <div className="apl-post-card-content">
                  <div className="apl-post-card-title" title={post.title || "(Không có tiêu đề)"}>
                    {truncateText(post.title || "(Không có tiêu đề)", 20)}
                  </div>
                  {post.shortDescription && (
                    <div className="apl-post-card-desc" title={post.shortDescription}>
                      Mô tả ngắn: {truncateText(post.shortDescription, 20)}
                    </div>
                  )}
                  <div className="apl-post-card-meta">
                    <span>Danh mục: {post.posttypeName || post.postTypeName || post.PosttypeName || "Không có danh mục"}</span>
                    <span>•</span>
                    <span>{formatDate(post.createdAt)}</span>
                  </div>
                  <div className="apl-post-card-footer">
                    <div className="apl-post-card-status">{getStatusBadge(post.status)}</div>
                    <div className="apl-post-card-actions">
                      <button
                        className="apl-action-btn apl-view-btn"
                        onClick={() => handlePreview(post)}
                        title="Xem trước"
                      >
                        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" width="16" height="16">
                          <path d="M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z"/>
                        </svg>
                      </button>
                      <button
                        className="apl-action-btn apl-update-btn"
                        onClick={() => handleEdit(post.postId)}
                        title="Sửa"
                      >
                        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" width="16" height="16">
                          <path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z"/>
                        </svg>
                      </button>
                      <button
                        className="apl-action-btn apl-delete-btn"
                        onClick={() => handleDelete(post.postId)}
                        title="Xóa"
                      >
                        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" width="16" height="16">
                          <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z"/>
                        </svg>
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Pagination */}
      {!loading && !error && (
        <div className="apl-pagination">
          <div className="apl-pagination-info">
            Hiển thị {total === 0 ? 0 : ((currentPage - 1) * pageSize) + 1}-{Math.min(currentPage * pageSize, total)}/{total} bài viết
          </div>
          <div className="apl-pagination-controls">
            <button
              className="apl-pagination-btn"
              onClick={() => setPage(page - 1)}
              disabled={page <= 1}
              title="Trang trước"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="15 18 9 12 15 6"></polyline>
              </svg>
              Trước
            </button>
            
            <div className="apl-pagination-numbers">
              {[...Array(totalPages)].map((_, idx) => {
                const pageNum = idx + 1;
                // Show first, last, current, and ±1 around current
                if (
                  pageNum === 1 ||
                  pageNum === totalPages ||
                  (pageNum >= page - 1 && pageNum <= page + 1)
                ) {
                  return (
                    <button
                      key={pageNum}
                      className={`apl-pagination-number ${page === pageNum ? "active" : ""}`}
                      onClick={() => setPage(pageNum)}
                    >
                      {pageNum}
                    </button>
                  );
                } else if (pageNum === page - 2 || pageNum === page + 2) {
                  return <span key={pageNum} className="apl-pagination-ellipsis">...</span>;
                }
                return null;
              })}
            </div>

            <button
              className="apl-pagination-btn"
              onClick={() => setPage(page + 1)}
              disabled={page >= totalPages}
              title="Trang sau"
            >
              Sau
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="9 18 15 12 9 6"></polyline>
              </svg>
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

