import React, { useState, useEffect, useCallback } from "react";
import { Link } from "react-router-dom";
import { ProductKeyApi } from "../../services/productKeys";
import { ProductApi } from "../../services/products";
import ToastContainer from "../../components/Toast/ToastContainer";
import ConfirmDialog from "../../components/ConfirmDialog/ConfirmDialog";
import useToast from "../../hooks/useToast";
import PermissionGuard from "../../components/PermissionGuard";
import { usePermission } from "../../hooks/usePermission";
import "../admin/admin.css";
import { getStatusColor, getStatusLabel } from "../../utils/productKeyHepler";

export default function KeyManagementPage() {
  const { toasts, showSuccess, showError, removeToast } = useToast();

  // Permission checks
  const { hasPermission: hasDeletePermission } = usePermission("WAREHOUSE_MANAGER", "DELETE");

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
      showError("Lỗi tải dữ liệu", errorMsg);
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
    if (!hasDeletePermission) {
      showError("Không có quyền", "Bạn không có quyền xóa key");
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
          <Link className="btn primary" to="/keys/add">
            + Tạo key mới
          </Link>
        </div>

        <div
          className="grid"
          style={{
            gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
            gap: 12,
          }}
        >
          <div className="form-row">
            <label className="muted">Tìm kiếm</label>
            <input
              className="input"
              placeholder="Tên/SKU/Key..."
              value={filters.searchTerm}
              onChange={(e) => handleFilterChange("searchTerm", e.target.value)}
            />
          </div>

          <div className="form-row">
            <label className="muted">Sản phẩm</label>
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

          <div className="form-row">
            <label className="muted">Loại key</label>
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

          <div className="form-row">
            <label className="muted">Trạng thái</label>
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

          <div className="form-row">
            <label>&nbsp;</label>
            <button className="btn primary" onClick={handleApplyFilters}>
              Lọc
            </button>
          </div>
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
            <button className="btn" onClick={handleExportCSV}>
              Xuất CSV
            </button>
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
                    <th>Gắn đơn</th>
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
                          <td>{key.orderCode || "—"}</td>
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
                              >
                                Chi tiết
                              </Link>
                              {key.status === "Available" && (
                                <button
                                  className="btn"
                                  style={{
                                    padding: "4px 8px",
                                    fontSize: "13px",
                                  }}
                                >
                                  Gắn đơn
                                </button>
                              )}
                              {!key.orderCode && (
                                <button
                                  className="btn"
                                  onClick={() => handleDeleteKey(key.keyId)}
                                  disabled={!hasDeletePermission}
                                  title={!hasDeletePermission ? "Bạn không có quyền xóa key" : "Xóa"}
                                  style={{
                                    padding: "4px 8px",
                                    fontSize: "13px",
                                    color: "#dc2626",
                                    opacity: !hasDeletePermission ? 0.5 : 1,
                                    cursor: !hasDeletePermission ? "not-allowed" : "pointer",
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
