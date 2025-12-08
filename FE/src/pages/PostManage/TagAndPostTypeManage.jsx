import React, { useEffect, useMemo, useState, useRef, useCallback } from "react";
import ToastContainer from "../../components/Toast/ToastContainer";
import useToast from "../../hooks/useToast";
import "./TagAndPostTypeManage.css"
import { postsApi } from "../../services/postsApi";
import RoleModal from "../../components/RoleModal/RoleModal";
import PermissionGuard from "../../components/PermissionGuard";
import { usePermission } from "../../hooks/usePermission";

/** 
 * @summary Tab constants for switching between different management views 
*/
const TABS = {
    TAGS: "tags",
    POSTTYPES: "postTypes"
};
function useFetchData(activeTab, showError, networkErrorShownRef) {
    const [data, setData] = useState([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    const loadData = useCallback(async () => {
        setLoading(true);
        setError("");
        try {
            let res = [];
            if (activeTab === TABS.TAGS) res = await postsApi.getTags();
            else if (activeTab === TABS.POSTTYPES) res = await postsApi.getPosttypes();
            setData(Array.isArray(res) ? res : []);
        } catch (e) {
            setError(e.message || "Không thể tải dữ liệu");
            // Handle network errors globally - only show one toast
            if (e.isNetworkError || e.message === 'Lỗi kết nối đến máy chủ') {
                if (networkErrorShownRef && !networkErrorShownRef.current) {
                    networkErrorShownRef.current = true;
                    if (showError) {
                        showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
                    }
                }
            } else if (showError) {
                showError('Lỗi tải dữ liệu', e.message || 'Không thể tải dữ liệu');
            }
        } finally {
            setLoading(false);
        }
    }, [activeTab, showError, networkErrorShownRef]);

    useEffect(() => {
        loadData();
    }, [loadData]);

    return { data, loading, error, setData, reloadData: loadData };
}

export default function TagAndPosttypeManage() {
    const [activeTab, setActiveTab] = useState(TABS.TAGS);
    const { toasts, showSuccess, showError, showWarning, removeToast, showConfirm, confirmDialog } = useToast();
    
    // Permission checks
    const { hasPermission: hasCreatePermission } = usePermission("POST_MANAGER", "CREATE");
    const { hasPermission: hasEditPermission } = usePermission("POST_MANAGER", "EDIT");
    const { hasPermission: hasDeletePermission } = usePermission("POST_MANAGER", "DELETE");
    
    // Global network error handler - only show one toast for network errors
    const networkErrorShownRef = useRef(false);
    useEffect(() => {
        // Reset the flag when component mounts
        networkErrorShownRef.current = false;
    }, []);
    
    const { data, loading, error, setData, reloadData } = useFetchData(activeTab, showError, networkErrorShownRef);

    const [search, setSearch] = useState("");
    const [sortKey, setSortKey] = useState("");
    const [sortOrder, setSortOrder] = useState("asc");

    // Modal states
    const [addModalOpen, setAddModalOpen] = useState(false);
    const [editModalOpen, setEditModalOpen] = useState(false);
    const [submitting, setSubmitting] = useState(false);
    const [editingItem, setEditingItem] = useState(null);

    // Pagination
    const [page, setPage] = useState(1);
    const [pageSize] = useState(10);

    useEffect(() => {
        setSearch("");
        setSortKey("");
        setSortOrder("asc");
        setPage(1);
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

    // Handle reset filters
    const handleReset = () => {
        setSearch("");
        setSortKey("");
        setSortOrder("asc");
        setPage(1);
    };

    // Check if any filters are active
    const hasActiveFilters = search || sortKey;

    // Format date helper
    const formatDate = (dateValue) => {
        if (!dateValue) return "-";
        try {
            const date = new Date(dateValue);
            if (Number.isNaN(date.getTime())) return "-";
            return date.toLocaleString("vi-VN", {
                year: "numeric",
                month: "2-digit",
                day: "2-digit",
                hour: "2-digit",
                minute: "2-digit"
            });
        } catch {
            return "-";
        }
    };

    const { columns, addButtonText } = useMemo(() => {
        if (activeTab === TABS.TAGS) {
            return {
                addButtonText: "Thêm Thẻ Mới",
                columns: [
                    { key: "tagName", label: "Tên Thẻ" },
                    { key: "slug", label: "Slug" },
                    { 
                        key: "createdAt", 
                        label: "Ngày tạo",
                        render: (value) => formatDate(value)
                    },
                    { 
                        key: "updatedAt", 
                        label: "Ngày cập nhật",
                        render: (value) => formatDate(value)
                    }
                ]
            };
        }
        return {
            addButtonText: "Thêm Danh Mục Mới",
            columns: [
                { key: "postTypeName", label: "Tên danh mục" },
                { key: "description", label: "Mô tả" },
                { 
                    key: "createdAt", 
                    label: "Ngày tạo",
                    render: (value) => formatDate(value)
                },
                { 
                    key: "updatedAt", 
                    label: "Ngày cập nhật",
                    render: (value) => formatDate(value)
                }
            ]
        };
    }, [activeTab]);
 const toSlug = (text) => {
  return text
    .normalize('NFD') 
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd').replace(/Đ/g, 'D') 
    .replace(/[^a-zA-Z0-9\s-]/g, '') 
    .trim() 
    .replace(/\s+/g, '-') 
    .replace(/-+/g, '-') 
    .toLowerCase(); 
};
    const filteredSorted = useMemo(() => {
        // Convert search text to slug format for comparison
        const searchSlug = toSlug(search);
        
        let rows = data.filter((row) => {
            if (!searchSlug) return true;
            
            // For Tags: check both slug and name-derived slug
            if (activeTab === TABS.TAGS) {
                const rowSlug = row.slug || row.Slug || "";
                const nameValue = row.tagName || row.TagName || "";
                const nameSlug = toSlug(nameValue);
                return rowSlug.includes(searchSlug) || nameSlug.includes(searchSlug);
            }
            
            // For PostTypes: only check name-derived slug (no slug field)
            const nameKey = "PostTypeName";
            const nameValue = row[nameKey] || row[nameKey.toLowerCase()] || row[nameKey.charAt(0).toUpperCase() + nameKey.slice(1)] || "";
            const nameSlug = toSlug(nameValue);
            return nameSlug.includes(searchSlug);
        });

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
    }, [data, columns, search, sortKey, sortOrder, activeTab]);
    const total = filteredSorted.length;
    const totalPages = Math.max(1, Math.ceil(total / pageSize));
    const currentPage = Math.min(page, totalPages);
    const paginated = useMemo(() => {
        const start = (currentPage - 1) * pageSize;
        return filteredSorted.slice(start, start + pageSize);
    }, [filteredSorted, currentPage, pageSize]);

    const [addTagOpen, setAddTagOpen] = useState(false);
    const [addPosttypeOpen, setAddPosttypeOpen] = useState(false);

    function onClickAdd() {
        if (activeTab === TABS.TAGS) {
            setAddTagOpen(true);
            return;
        }
        if (activeTab === TABS.POSTTYPES) {
            setAddPosttypeOpen(true);
            return;
        }
    }

    async function handleCreateTag(form) {
        try {
            setSubmitting(true);
            const created = await postsApi.createTag({
                tagName: form.tagName,
                // always generate slug from tag name
                slug: toSlug(form.tagName)
            });
            setData((prev) => Array.isArray(prev) ? [...prev, created] : [created]);
            setAddTagOpen(false);
            showSuccess(
                "Tạo Thẻ thành công!",
                `Thẻ "${form.tagName}" đã được tạo thành công.`
            );
        } catch (e) {
            // Handle network errors globally - only show one toast
            if (e.isNetworkError || e.message === 'Lỗi kết nối đến máy chủ') {
                if (!networkErrorShownRef.current) {
                    networkErrorShownRef.current = true;
                    showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
                }
            } else {
                const errorMessage = e.response?.data?.message || e.message || "Không thể tạo Thẻ";
                showError("Tạo Thẻ thất bại!", errorMessage);
            }
        } finally {
            setSubmitting(false);
        }
    }

    async function handleCreatePosttype(form) {
        try {
            setSubmitting(true);
            const created = await postsApi.createPosttype({
                postTypeName: form.postTypeName || form.posttypeName,
                description: form.description || ""
            });
            setData((prev) => Array.isArray(prev) ? [...prev, created] : [created]);
            setAddPosttypeOpen(false);
            showSuccess(
                "Tạo Danh mục thành công!",
                `Danh mục "${form.postTypeName || form.posttypeName}" đã được tạo thành công.`
            );
        } catch (e) {
            // Handle network errors globally - only show one toast
            if (e.isNetworkError || e.message === 'Lỗi kết nối đến máy chủ') {
                if (!networkErrorShownRef.current) {
                    networkErrorShownRef.current = true;
                    showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
                }
            } else {
                const errorMessage = e.response?.data?.message || e.message || "Không thể tạo danh mục";
                showError("Tạo Danh mục thất bại!", errorMessage);
            }
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
        if (activeTab === TABS.TAGS) {
            setEditTitle("Sửa Thẻ");
            setEditFields([
                { name: "tagName", label: "Tên Thẻ", required: true, minLength: 2, maxLength: 100, defaultValue: row.tagName || row.TagName || '' },
                // slug is auto-generated and not editable
                { name: "slug", label: "Slug", defaultValue: row.slug || row.Slug || "", disabled: true, syncWith: "tagName", format: "slug", minLength: 2, maxLength: 100 },
            ]);
        } else {
            setEditTitle("Sửa Danh mục");
            setEditFields([
                { name: "postTypeName", label: "Tên Danh mục", required: true, minLength: 2, maxLength: 100, defaultValue: row.postTypeName || row.posttypeName || row.PostTypeName || '' },
                { name: "description", label: "Mô tả", type: "textarea", maxLength: 500, defaultValue: row.description || "" },
            ]);
        }
        setEditOpen(true);
    }
    async function onDelete(row) {
        const label = activeTab === TABS.TAGS ? (row.tagName || row.TagName) : (row.postTypeName || row.posttypeName || row.PostTypeName);
        const entityType = activeTab === TABS.TAGS ? "Thẻ" : "Danh mục";

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
                    if (activeTab === TABS.TAGS) await postsApi.deleteTag(row.tagId || row.TagId || row.id);
                    else await postsApi.deletePosttype(row.posttypeId || row.postTypeId || row.id);

                    setData((prev) => prev.filter((x) => {
                        const targetId = activeTab === TABS.TAGS ? (row.tagId || row.TagId || row.id) : (row.posttypeId || row.postTypeId || row.id);
                        const currentId = activeTab === TABS.TAGS ? (x.tagId || x.TagId || x.id) : (x.posttypeId || x.postTypeId || x.id);
                        return currentId !== targetId;
                    }));
                    showSuccess(
                        `Xóa ${entityType} thành công!`,
                        `${entityType} "${label}" đã được xóa.`
                    );
                } catch (e) {
                    // Handle network errors globally - only show one toast
                    if (e.isNetworkError || e.message === 'Lỗi kết nối đến máy chủ') {
                        if (!networkErrorShownRef.current) {
                            networkErrorShownRef.current = true;
                            showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
                        }
                    } else {
                        const errorMessage = e.response?.data?.message || e.message || "Xoá thất bại";
                        showError(`Xóa ${entityType} thất bại!`, errorMessage);
                    }
                }
            },
            () => {
                // User cancelled, no action needed
            }
        );
    }

    async function onSubmitEdit(form) {
        try {
            setEditSubmitting(true);
            const entityType = activeTab === TABS.TAGS ? "Thẻ" : "Danh mục";
            const entityName = activeTab === TABS.TAGS ? form.tagName : (form.postTypeName || form.posttypeName);

            if (activeTab === TABS.TAGS) {
                // compute slug from tag name on update
                const newSlug = toSlug(form.tagName);
                const tagId = editingRow?.tagId || editingRow?.TagId || editingRow?.id;
                if (!tagId) throw new Error('Missing tag id for update');

                await postsApi.updateTag(tagId, {
                    tagName: form.tagName,
                    slug: newSlug
                });
            } else {
                // update post type
                const postTypeId = editingRow?.posttypeId || editingRow?.postTypeId || editingRow?.id;
                if (!postTypeId) throw new Error('Missing post type id for update');

                await postsApi.updatePosttype(postTypeId, {
                    postTypeName: form.postTypeName || form.posttypeName,
                    description: form.description || ""
                });
            }
            
            // Reload data from server to get updated UpdatedAt
            await reloadData();
            
            setEditOpen(false);
            showSuccess(
                `Cập nhật ${entityType} thành công!`,
                `${entityType} "${entityName}" đã được cập nhật thành công.`
            );
        } catch (e) {
            // Handle network errors globally - only show one toast
            if (e.isNetworkError || e.message === 'Lỗi kết nối đến máy chủ') {
                if (!networkErrorShownRef.current) {
                    networkErrorShownRef.current = true;
                    showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
                }
            } else {
                const errorMessage = e.response?.data?.message || e.message || "Cập nhật thất bại";
                const entityType = activeTab === TABS.TAGS ? "Thẻ" : "Danh mục";
                showError(`Cập nhật ${entityType} thất bại!`, errorMessage);
            }
        } finally {
            setEditSubmitting(false);
        }
    }

    return (
        <div className="tag-pt-container">
            <div className="tag-pt-header">
                <h1 className="tag-pt-title">
                    {activeTab === TABS.TAGS ? "Quản lý Thẻ" : "Quản lý Danh mục"}
                </h1>
                <p className="tag-pt-subtitle">
                    {activeTab === TABS.TAGS
                        ? "Quản lý các thẻ được sử dụng"
                        : "Quản lý các danh mục bài viết"}
                </p>
            </div>

            <div className="tag-pt-tabs">
                <button
                    className={`tag-pt-tab-button ${activeTab === TABS.TAGS ? "active" : ""}`}
                    onClick={() => setActiveTab(TABS.TAGS)}
                >
                    Danh sách Thẻ
                </button>
                <button
                    className={`tag-pt-tab-button ${activeTab === TABS.POSTTYPES ? "active" : ""}`}
                    onClick={() => setActiveTab(TABS.POSTTYPES)}
                >
                    Danh sách Danh mục
                </button>
            </div>

            <div className="tag-pt-controls">
                <div className="tag-pt-controls-left">
                    <div className="tag-pt-search-box">
                        <input
                            type="text"
                            placeholder={activeTab === TABS.TAGS ? "Tìm tên Thẻ" : "Tìm tên Danh mục"}
                            value={search}
                            onChange={(e) => {
                                setSearch(e.target.value);
                                setPage(1);
                            }}
                        />
                    </div>
                    {hasActiveFilters && (
                        <button 
                            className="tag-pt-reset-button" 
                            onClick={handleReset}
                            title="Đặt lại bộ lọc"
                        >
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                <polyline points="23 4 23 10 17 10"></polyline>
                                <polyline points="1 20 1 14 7 14"></polyline>
                                <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path>
                            </svg>
                            Đặt lại
                        </button>
                    )}
                </div>
                <div className="tag-pt-controls-right">
                    <PermissionGuard moduleCode="POST_MANAGER" permissionCode="CREATE">
                        <button className="tag-pt-add-button" onClick={onClickAdd} >
                            {addButtonText}
                        </button>
                    </PermissionGuard>
                </div>
            </div>
            {activeTab === TABS.TAGS && (
                <RoleModal
                    isOpen={addTagOpen}
                    title="Thêm Thẻ"
                    fields={[
                        { name: "tagName", label: "Tên Thẻ", required: true, minLength: 2, maxLength: 100 },
                        { name: "slug", label: "Slug", disabled: true, syncWith: "tagName", format: "slug", minLength: 2, maxLength: 100 },
                    ]}
                    onClose={() => setAddTagOpen(false)}
                    onSubmit={handleCreateTag}
                    submitting={submitting}
                />
            )}
            {activeTab === TABS.POSTTYPES && (
                <RoleModal
                    isOpen={addPosttypeOpen}
                    title="Thêm Danh mục"
                    fields={[
                        { name: "postTypeName", label: "Tên Danh mục", required: true, minLength: 2, maxLength: 100 },
                        { name: "description", label: "Mô tả", type: "textarea", maxLength: 500 },
                    ]}
                    onClose={() => setAddPosttypeOpen(false)}
                    onSubmit={handleCreatePosttype}
                    submitting={submitting}
                />
            )}

            <div className="tag-pt-table-container">
                {loading ? (
                    <div className="tag-pt-loading-state">
                        <div className="tag-pt-loading-spinner" />
                        <div>Đang tải dữ liệu...</div>
                    </div>
                ) : error ? (
                    <div className="tag-pt-empty-state">
                        <div>Lỗi: {error}</div>
                    </div>
                ) : paginated.length === 0 ? (
                    <div className="tag-pt-empty-state">
                        <div>Không có dữ liệu</div>
                    </div>
                ) : (
                    <table className="tag-pt-table">
                        <thead>
                            <tr>
                                {columns.map((col) => (
                                    <th key={col.key}>
                                        <div
                                            className="tag-pt-sortable-header"
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
                                        <div className="tag-pt-action-buttons">
                                            <PermissionGuard moduleCode="POST_MANAGER" permissionCode="EDIT">
                                                <button className="tag-pt-action-btn tag-pt-update-btn" title="Sửa" onClick={() => onEdit(row)}>
                                                    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" /></svg>
                                                </button>
                                            </PermissionGuard>
                                            <button 
                                                className="tag-pt-action-btn tag-pt-delete-btn" 
                                                title={!hasDeletePermission ? "Bạn không có quyền xóa" : "Xoá"} 
                                                onClick={() => {
                                                    if (!hasDeletePermission) {
                                                        showError("Không có quyền", "Bạn không có quyền xóa");
                                                        return;
                                                    }
                                                    onDelete(row);
                                                }}
                                                disabled={!hasDeletePermission}
                                            >
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

            {/* Pagination */}
            {!loading && !error && (
                <div className="tag-pt-pagination">
                    <div className="tag-pt-pagination-info">
                        Hiển thị {((currentPage - 1) * pageSize) + 1}-{Math.min(currentPage * pageSize, total)}/{total} {activeTab === TABS.TAGS ? "thẻ" : "danh mục"}
                    </div>
                    <div className="tag-pt-pagination-controls">
                        <button
                            className="tag-pt-pagination-btn"
                            onClick={() => setPage(page - 1)}
                            disabled={page <= 1}
                            title="Trang trước"
                        >
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                                <polyline points="15 18 9 12 15 6"></polyline>
                            </svg>
                            Trước
                        </button>
                        
                        <div className="tag-pt-pagination-numbers">
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
                                            className={`tag-pt-pagination-number ${page === pageNum ? "active" : ""}`}
                                            onClick={() => setPage(pageNum)}
                                        >
                                            {pageNum}
                                        </button>
                                    );
                                } else if (pageNum === page - 2 || pageNum === page + 2) {
                                    return <span key={pageNum} className="tag-pt-pagination-ellipsis">...</span>;
                                }
                                return null;
                            })}
                        </div>

                        <button
                            className="tag-pt-pagination-btn"
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
            <ToastContainer toasts={toasts} removeToast={removeToast} />
        </div>
    );
}