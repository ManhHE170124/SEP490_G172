/**
 * @file: AdminPostList.jsx
 * @author: HieuNDHE173169
 * @created 2025-10-30
 * @lastUpdated 2025-10-30
 * @version: 1.0.0
 * @summary: Admin page for managing posts with full CRUD operations, search, filter, sort, and pagination
 */

import React, { useEffect, useState, useMemo, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { postsApi } from "../../services/postsApi";
import useToast from "../../hooks/useToast";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./AdminPostList.css";

export default function AdminPostList() {
  const navigate = useNavigate();
  const { toasts, showSuccess, showError, removeToast, confirmDialog, showConfirm } = useToast();

  // Data state
  const [posts, setPosts] = useState([]);
  const [postTypes, setPostTypes] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // Filter & Search state
  const [search, setSearch] = useState("");
  const [postTypeFilter, setPostTypeFilter] = useState("all");
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
      const [postsData, postTypesData] = await Promise.all([
        postsApi.getAllPosts(),
        postsApi.getAllPostTypes()
      ]);
      setPosts(Array.isArray(postsData) ? postsData : []);
      setPostTypes(Array.isArray(postTypesData) ? postTypesData : []);
    } catch (err) {
      setError(err.message || "Không thể tải dữ liệu");
      showError("Lỗi", err.message || "Không thể tải danh sách bài viết");
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
    if (postTypeFilter !== "all") {
      const typeId = Number.parseInt(postTypeFilter, 10);
      filtered = filtered.filter(post => post.postTypeId === typeId);
    }

    // Status filter
    if (statusFilter !== "all") {
      filtered = filtered.filter(post => post.status === statusFilter);
    }

    // Sort
    filtered.sort((a, b) => {
      let aVal, bVal;

      // Handle postTypeName (special case - nested property)
      if (sortKey === "postTypeName") {
        aVal = a.postTypeName || "";
        bVal = b.postTypeName || "";
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
  }, [posts, search, postTypeFilter, statusFilter, sortKey, sortOrder]);

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
      navigate(`/post-create-edit/${postId}`);
  };

  const handlePreview = (post) => {
    const previewWindow = window.open("", "_blank");
    previewWindow.document.write(`
      <!DOCTYPE html>
      <html>
        <head>
          <title>${post.title || "Preview"}</title>
          <meta charset="utf-8">
          <style>
            body {
              font-family: Arial, sans-serif;
              max-width: 800px;
              margin: 0 auto;
              padding: 20px;
              line-height: 1.6;
            }
            img { max-width: 100%; height: auto; }
            h1 { color: #333; }
            .meta { color: #666; font-size: 14px; margin: 10px 0; }
            .content { margin-top: 20px; }
          </style>
        </head>
        <body>
          <h1>${post.title || ""}</h1>
          <div class="meta">
            ${post.authorName ? `Tác giả: ${post.authorName} | ` : ""}
            ${post.createdAt ? `Ngày: ${formatDate(post.createdAt)} | ` : ""}
            ${post.viewCount !== null ? `Lượt xem: ${post.viewCount}` : ""}
          </div>
          ${post.shortDescription ? `<p><em>${post.shortDescription}</em></p>` : ""}
          ${post.thumbnail ? `<img src="${post.thumbnail}" alt="${post.title}" style="max-width: 100%; margin: 20px 0;" />` : ""}
          <div class="content">${post.content || ""}</div>
        </body>
      </html>
    `);
    previewWindow.document.close();
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
          showError("Lỗi khi xóa bài viết", err.message || "Không thể xóa bài viết");
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
          showError("Lỗi khi xóa nhiều bài viết", err.message || "Không thể xóa bài viết");
        }
      }
    );
  };

  const handleStatusChange = async (postId, newStatus) => {
    try {
      const post = posts.find(p => p.postId === postId);
      if (!post) return;

      await postsApi.updatePost(postId, {
        title: post.title,
        shortDescription: post.shortDescription || "",
        content: post.content || "",
        thumbnail: post.thumbnail || "",
        postTypeId: post.postTypeId,
        status: newStatus,
        metaTitle: post.metaTitle || "",
        metaDescription: post.metaDescription || "",
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
      showError("Lỗi thay đổi trạng thái", err.message || "Không thể cập nhật trạng thái");
    }
  };

  // Export CSV function
  const handleExportCSV = () => {
    try {
      const headers = ["Tiêu đề", "Danh mục", "Người phụ trách", "Trạng thái", "Lượt xem", "Ngày tạo", "Ngày cập nhật"];
      const csvRows = [headers.join(",")];

      filteredSorted.forEach(post => {
        const row = [
          `"${(post.title || "").replace(/"/g, '""')}"`,
          `"${(post.postTypeName || "").replace(/"/g, '""')}"`,
          `"${(post.authorName || "").replace(/"/g, '""')}"`,
          `"${getStatusLabel(post.status).replace(/"/g, '""')}"`,
          post.viewCount || 0,
          `"${formatDate(post.createdAt)}"`,
          `"${formatDate(post.updatedAt)}"`
        ];
        csvRows.push(row.join(","));
      });

      const csvContent = csvRows.join("\n");
      const blob = new Blob(["\ufeff" + csvContent], { type: "text/csv;charset=utf-8;" });
      const link = document.createElement("a");
      const url = URL.createObjectURL(blob);
      link.setAttribute("href", url);
      link.setAttribute("download", `danh-sach-bai-viet-${new Date().toISOString().split("T")[0]}.csv`);
      link.style.visibility = "hidden";
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      showSuccess("Thành công", "Đã xuất file CSV");
    } catch (err) {
      console.log("Lỗi khi xuất file CSV:", err);
      showError("Lỗi khi xuất file CSV", err.message || "Không thể xuất file CSV");
    }
  };

  // Get status label
  const getStatusLabel = (status) => {
    const statusMap = {
      Draft: "Bản nháp",
      Published: "Công khai",
      Archived: "Đã lưu trữ",
      Private: "Riêng tư"
    };
    return statusMap[status] || status;
  };

  // Reset filters
  const handleResetFilters = () => {
    setSearch("");
    setPostTypeFilter("all");
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
      Archived: { label: "Đã lưu trữ", color: "#ffc107" },
      Private: { label: "Riêng tư", color: "#007bff" }
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
  }, [search, postTypeFilter, statusFilter, sortKey, sortOrder]);

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
          <button className="apl-btn-secondary" onClick={handleExportCSV} style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true" width="16" height="16">
              <path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/>
            </svg>
            Xuất CSV
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
              placeholder="Tìm kiếm tiêu đề, nội dung..."
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
              value={postTypeFilter}
              onChange={(e) => setPostTypeFilter(e.target.value)}
              className="apl-filter-select"
            >
              <option value="all">Tất cả</option>
              {postTypes.map((pt) => (
                <option key={pt.postTypeId} value={pt.postTypeId}>
                  {pt.postTypeName}
                </option>
              ))}
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
            {(search || postTypeFilter !== "all" || statusFilter !== "all") && (
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
                    onClick={() => handleColumnSort("postTypeName")}
                    onKeyDown={(e) => e.key === "Enter" && handleColumnSort("postTypeName")}
                    role="button"
                    tabIndex={0}
                  >
                    Danh mục
                    {sortKey === "postTypeName" && (sortOrder === "asc" ? " ↑" : " ↓")}
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
                      <div className="apl-post-title">{post.title || "(Không có tiêu đề)"}</div>
                      {post.shortDescription && (
                        <div className="apl-post-short-desc">{post.shortDescription}</div>
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
                  <td>{post.postTypeName || "-"}</td>
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
                <div className="apl-post-card-content">
                  <div className="apl-post-card-title">{post.title || "(Không có tiêu đề)"}</div>
                  {post.shortDescription && (
                    <div className="apl-post-card-desc">{post.shortDescription}</div>
                  )}
                  <div className="apl-post-card-meta">
                    <span>{post.postTypeName || "Không có danh mục"}</span>
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
      <div className="apl-post-list-pagination">
        <div className="apl-pagination-controls">
          <button
            className="apl-btn-secondary"
            onClick={() => setPage(1)}
            disabled={currentPage === 0 || currentPage === 1}
          >
            «
          </button>
          <button
            className="apl-btn-secondary"
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={currentPage === 0 || currentPage === 1}
          >
            ‹
          </button>
          <span className="apl-pagination-page">
            Trang {currentPage}/{totalPages} ({total} bài viết)
          </span>
          <button
            className="apl-btn-secondary"
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
            disabled={currentPage === 0 || currentPage >= totalPages}
          >
            ›
          </button>
          <button
            className="apl-btn-secondary"
            onClick={() => setPage(totalPages)}
            disabled={currentPage === 0 || currentPage >= totalPages}
          >
            »
          </button>
        </div>
      </div>
    </div>
  );
}

