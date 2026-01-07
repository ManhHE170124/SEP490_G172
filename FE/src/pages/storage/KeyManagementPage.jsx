import React, { useState, useEffect, useCallback, useRef } from "react";
import { Link } from "react-router-dom";
import { ProductKeyApi } from "../../services/productKeys";
import { ProductApi } from "../../services/products";
import ToastContainer from "../../components/Toast/ToastContainer";
import ConfirmDialog from "../../components/ConfirmDialog/ConfirmDialog";
import useToast from "../../hooks/useToast";
import "../admin/admin.css";
import { getStatusColor, getStatusLabel } from "../../utils/productKeyHepler";

export default function KeyManagementPage() {
  const { toasts, showSuccess, showError, removeToast } = useToast();

  // Permission checks removed - now role-based on backend
  const canViewList = true;
  const permissionLoading = false;
  const canViewDetail = true;
  const canDelete = true;

  // Global network error handler
  const networkErrorShownRef = useRef(false);
  // Global permission error handler - only show one toast for permission errors
  const permissionErrorShownRef = useRef(false);
  useEffect(() => {
    networkErrorShownRef.current = false;
    permissionErrorShownRef.current = false;
  }, []);

  const [loading, setLoading] = useState(false);
  const [keys, setKeys] = useState([]);
  const [products, setProducts] = useState([]);

  // Filter states
  const [filters, setFilters] = useState({
    searchTerm: "",
    productId: "",
    type: "",
    status: "",
    pageNumber: 1,
    pageSize: 20,
  });

  // Pagination states
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);

  const [confirmDialog, setConfirmDialog] = useState({
    isOpen: false,
    title: "",
    message: "",
    onConfirm: null,
    type: "warning",
  });

  const loadProducts = useCallback(async () => {
    try {
      const data = await ProductApi.list({
        pageNumber: 1,
        pageSize: 100,
      });
      setProducts(data.items || data.data || []);
    } catch (err) {
      console.error("Failed to load products:", err);
    }
  }, []);

  const loadKeys = useCallback(async () => {
    setLoading(true);
    try {
      const data = await ProductKeyApi.list(filters);
      setKeys(data.items || []);
      setTotalCount(data.totalCount || 0);
      setTotalPages(data.totalPages || 0);
    } catch (err) {
      console.error("Failed to load keys:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể tải danh sách key";
      
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
          const errorMsgFinal = err?.response?.data?.message || err.message || "Bạn không có quyền truy cập chức năng này.";
          showError("Lỗi tải dữ liệu", errorMsgFinal);
        } else if (!isPermissionError) {
          showError("Lỗi tải dữ liệu", errorMsg);
        }
      }
    } finally {
      setLoading(false);
    }
  }, [filters, showError]);

  useEffect(() => {
    loadProducts();
  }, [loadProducts]);

  useEffect(() => {
    loadKeys();
  }, [loadKeys]);

  const handleFilterChange = (field, value) => {
    setFilters((prev) => ({
      ...prev,
      [field]: value,
      pageNumber: 1, // Reset to first page when filter changes
    }));
  };

  const handleApplyFilters = () => {
    loadKeys();
  };

  const handlePageChange = (newPage) => {
    setFilters((prev) => ({ ...prev, pageNumber: newPage }));
  };

  const handleDeleteKey = (keyId) => {
    if (!canDelete) {
      showError("Không có quyền", "Bạn không có quyền xóa key.");
      return;
    }
    setConfirmDialog({
      isOpen: true,
      title: "Xác nhận xóa key",
      message:
        "Bạn có chắc muốn xóa key này? Hành động này không thể hoàn tác.",
      type: "danger",
      onConfirm: async () => {
        setConfirmDialog({ ...confirmDialog, isOpen: false });
        try {
          await ProductKeyApi.delete(keyId);
          showSuccess("Thành công", "Key đã được xóa thành công");
          loadKeys();
        } catch (err) {
          console.error("Failed to delete key:", err);
          const errorMsg =
            err.response?.data?.message || err.message || "Không thể xóa key";
          showError("Lỗi xóa key", errorMsg);
        }
      },
    });
  };

  const handleExportCSV = async () => {
    try {
      const blob = await ProductKeyApi.export(filters);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `product-keys-${new Date().toISOString().split("T")[0]}.csv`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      showSuccess("Thành công", "Đã xuất file CSV thành công");
    } catch (err) {
      console.error("Failed to export CSV:", err);
      const errorMsg =
        err.response?.data?.message || err.message || "Không thể xuất file CSV";
      showError("Lỗi xuất file", errorMsg);
    }
  };

  const closeConfirmDialog = () => {
    setConfirmDialog({ ...confirmDialog, isOpen: false });
  };

  // Show loading while checking permission
  if (permissionLoading) {
    return (
      <div className="page">
        <div className="card">
          <h1 style={{ margin: 0 }}>Kho Product Key</h1>
          <div style={{ padding: "20px", textAlign: "center" }}>
            Đang kiểm tra quyền truy cập...
          </div>
        </div>
      </div>
    );
  }

  // Show access denied message if no VIEW_LIST permission
  if (!canViewList) {
    return (
      <div className="page">
        <div className="card">
          <h1 style={{ margin: 0 }}>Kho Product Key</h1>
          <div style={{ padding: "20px" }}>
            <h2>Không có quyền truy cập</h2>
            <p>Bạn không có quyền xem danh sách key. Vui lòng liên hệ quản trị viên để được cấp quyền.</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="page">
      <ToastContainer toasts={toasts} onRemove={removeToast} />
      <ConfirmDialog
        isOpen={confirmDialog.isOpen}
        title={confirmDialog.title}
        message={confirmDialog.message}
        type={confirmDialog.type}
        onConfirm={confirmDialog.onConfirm}
        onCancel={closeConfirmDialog}
      />

      {/* Filters Section */}
      <section className="card">
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 16,
          }}
        >
          <h1 style={{ margin: 0 }}>Kho Key phần mềm</h1>
          <Link 
            className="btn primary" 
            to="/keys/add"
            onClick={(e) => {
              // Note: CREATE permission check will be done in KeyDetailPage
            }}
          >
            + Tạo key mới
          </Link>
        </div>

        <div
          className="filter-inline"
          style={{
            marginTop: 16,
            display: "flex",
            gap: 10,
            flexWrap: "wrap",
            alignItems: "end",
          }}
        >
          <div className="group" style={{ flex: "1 1 200px" }}>
            <span>Tìm kiếm</span>
            <input
              className="input"
              placeholder="Tên/SKU/Key..."
              value={filters.searchTerm}
              onChange={(e) => handleFilterChange("searchTerm", e.target.value)}
            />
          </div>

          <div className="group" style={{ width: 180 }}>
            <span>Sản phẩm</span>
            <select
              className="input"
              value={filters.productId}
              onChange={(e) => handleFilterChange("productId", e.target.value)}
            >
              <option value="">Tất cả</option>
              {products.map((product) => (
                <option key={product.productId} value={product.productId}>
                  {product.productName}
                </option>
              ))}
            </select>
          </div>

          <div className="group" style={{ width: 150 }}>
            <span>Loại key</span>
            <select
              className="input"
              value={filters.type}
              onChange={(e) => handleFilterChange("type", e.target.value)}
            >
              <option value="">Tất cả</option>
              <option value="Individual">Cá nhân</option>
              <option value="Pool">Dùng chung (pool)</option>
            </select>
          </div>

          <div className="group" style={{ width: 150 }}>
            <span>Trạng thái</span>
            <select
              className="input"
              value={filters.status}
              onChange={(e) => handleFilterChange("status", e.target.value)}
            >
              <option value="">Tất cả</option>
              <option value="Available">Còn</option>
              <option value="Sold">Đã bán</option>
              <option value="Error">Lỗi</option>
              <option value="Recalled">Thu hồi</option>
              <option value="Expired">Hết hạn</option>
            </select>
          </div>

          <button className="btn primary" onClick={handleApplyFilters}>
            Lọc
          </button>
        </div>
      </section>

      {/* Keys Table */}
      <section className="card" style={{ marginTop: 14 }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 12,
          }}
        >
          <h2 style={{ margin: 0 }}>Danh sách Key</h2>
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <span className="muted">
              {totalCount} mục · Trang {filters.pageNumber}/{totalPages}
            </span>
            {/* <button className="btn" onClick={handleExportCSV}>
              Xuất CSV
            </button> */}
          </div>
        </div>

        {loading ? (
          <p style={{ textAlign: "center", padding: 40 }}>Đang tải...</p>
        ) : (
          <>
            <div style={{ overflowX: "auto" }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Sản phẩm</th>
                    <th>Key/Pool</th>
                    <th>Loại</th>
                    <th>Trạng thái</th>
                    <th>Cập nhật</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {keys.length === 0 ? (
                    <tr>
                      <td
                        colSpan="7"
                        style={{ textAlign: "center", padding: 20 }}
                      >
                        Không có key nào
                      </td>
                    </tr>
                  ) : (
                    keys.map((key) => {
                      const statusStyle = getStatusColor(key.status);
                      return (
                        <tr key={key.keyId}>
                          <td>{key.productName}</td>
                          <td>
                            <Link
                              to={`/keys/${key.keyId}`}
                              style={{ fontFamily: "monospace" }}
                              onClick={(e) => {
                                if (!canViewDetail) {
                                  e.preventDefault();
                                  showError("Không có quyền", "Bạn không có quyền xem chi tiết key.");
                                }
                              }}
                            >
                              {key.keyString.substring(0, 20)}...
                            </Link>
                          </td>
                          <td>
                            <span
                              style={{
                                padding: "4px 8px",
                                borderRadius: "4px",
                                fontSize: "12px",
                                background: "#f3f4f6",
                              }}
                            >
                              {key.type === "Individual"
                                ? "Cá nhân"
                                : "Dùng chung"}
                            </span>
                          </td>
                          <td>
                            <span
                              style={{
                                display: "inline-block",
                                padding: "4px 8px",
                                borderRadius: "4px",
                                fontSize: "12px",
                                background: statusStyle.bg,
                                color: statusStyle.color,
                                fontWeight: 600,
                              }}
                            >
                              {getStatusLabel(key.status)}
                            </span>
                          </td>
                          <td>
                            {key.updatedAt
                              ? new Date(key.updatedAt).toLocaleDateString(
                                  "vi-VN"
                                )
                              : "—"}
                          </td>
                          <td>
                            <div style={{ display: "flex", gap: 6 }}>
                              <Link
                                className="btn"
                                to={`/keys/${key.keyId}`}
                                style={{ padding: "4px 8px", fontSize: "13px" }}
                                onClick={(e) => {
                                  if (!canViewDetail) {
                                    e.preventDefault();
                                    showError("Không có quyền", "Bạn không có quyền xem chi tiết key.");
                                  }
                                }}
                              >
                                Chi tiết
                              </Link>
                              
                              {!key.orderCode && (
                                <button
                                  className="btn"
                                  onClick={() => handleDeleteKey(key.keyId)}
                                  style={{
                                    padding: "4px 8px",
                                    fontSize: "13px",
                                    color: "#dc2626",
                                  }}
                                >
                                  Xóa
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>

            {/* Pagination */}
            {totalPages > 1 && (
              <div
                style={{
                  display: "flex",
                  justifyContent: "center",
                  gap: 8,
                  marginTop: 16,
                }}
              >
                <button
                  className="btn"
                  onClick={() => handlePageChange(filters.pageNumber - 1)}
                  disabled={filters.pageNumber === 1}
                >
                  « Trước
                </button>
                {Array.from({ length: totalPages }, (_, i) => i + 1)
                  .filter(
                    (page) =>
                      page === 1 ||
                      page === totalPages ||
                      Math.abs(page - filters.pageNumber) <= 2
                  )
                  .map((page, idx, arr) => {
                    if (idx > 0 && arr[idx - 1] !== page - 1) {
                      return (
                        <React.Fragment key={`gap-${page}`}>
                          <span style={{ padding: "8px 4px" }}>...</span>
                          <button
                            className={
                              page === filters.pageNumber
                                ? "btn primary"
                                : "btn"
                            }
                            onClick={() => handlePageChange(page)}
                          >
                            {page}
                          </button>
                        </React.Fragment>
                      );
                    }
                    return (
                      <button
                        key={page}
                        className={
                          page === filters.pageNumber ? "btn primary" : "btn"
                        }
                        onClick={() => handlePageChange(page)}
                      >
                        {page}
                      </button>
                    );
                  })}
                <button
                  className="btn"
                  onClick={() => handlePageChange(filters.pageNumber + 1)}
                  disabled={filters.pageNumber === totalPages}
                >
                  Sau »
                </button>
              </div>
            )}
          </>
        )}
      </section>
    </div>
  );
}
