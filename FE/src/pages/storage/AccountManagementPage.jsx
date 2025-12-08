import React, { useState, useEffect, useCallback } from "react";
import { Link } from "react-router-dom";
import { ProductAccountApi } from "../../services/productAccounts";
import { ProductApi } from "../../services/products";
import ToastContainer from "../../components/Toast/ToastContainer";
import ConfirmDialog from "../../components/ConfirmDialog/ConfirmDialog";
import ChunkedText from "../../components/ChunkedText";
import useToast from "../../hooks/useToast";
import PermissionGuard from "../../components/PermissionGuard";
import { usePermission } from "../../hooks/usePermission";
import { MODULE_CODES, PERMISSION_CODES } from "../../constants/roleConstants";
import "../admin/admin.css";
import {
  getAccountStatusColor,
  getAccountStatusLabel,
} from "../../utils/productAccountHelper";

export default function AccountManagementPage() {
  const { toasts, showSuccess, showError, removeToast } = useToast();

  // Permission checks
  const { hasPermission: hasCreatePermission } = usePermission(MODULE_CODES.WAREHOUSE_MANAGER, PERMISSION_CODES.CREATE);
  const { hasPermission: hasDeletePermission } = usePermission(MODULE_CODES.WAREHOUSE_MANAGER, PERMISSION_CODES.DELETE);
  const { hasPermission: hasViewDetailPermission } = usePermission(MODULE_CODES.WAREHOUSE_MANAGER, PERMISSION_CODES.VIEW_DETAIL);

  const [loading, setLoading] = useState(false);
  const [accounts, setAccounts] = useState([]);
  const [products, setProducts] = useState([]);

  // Filter states
  const [filters, setFilters] = useState({
    searchTerm: "",
    productId: "",
    productType: "",
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
        // Include both shared and personal account products
        type: ["SHARED_ACCOUNT", "PERSONAL_ACCOUNT"],
      });
      setProducts(data.items || data.data || []);
    } catch (err) {
      console.error("Failed to load products:", err);
    }
  }, []);

  const loadAccounts = useCallback(async () => {
    setLoading(true);
    try {
      const data = await ProductAccountApi.list(filters);
      setAccounts(data.items || []);
      setTotalCount(data.totalCount || 0);
      setTotalPages(data.totalPages || 0);
    } catch (err) {
      console.error("Failed to load accounts:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể tải danh sách tài khoản";
      showError("Lỗi tải dữ liệu", errorMsg);
    } finally {
      setLoading(false);
    }
  }, [filters, showError]);

  useEffect(() => {
    loadProducts();
  }, [loadProducts]);

  useEffect(() => {
    loadAccounts();
  }, [loadAccounts]);

  const handleFilterChange = (field, value) => {
    setFilters((prev) => ({
      ...prev,
      [field]: value,
      pageNumber: 1, // Reset to first page when filter changes
    }));
  };

  const handleApplyFilters = () => {
    loadAccounts();
  };

  const handlePageChange = (newPage) => {
    setFilters((prev) => ({ ...prev, pageNumber: newPage }));
  };

  const handleDeleteAccount = (accountId) => {
    setConfirmDialog({
      isOpen: true,
      title: "Xác nhận xóa tài khoản",
      message:
        "Bạn có chắc muốn xóa tài khoản này? Hành động này không thể hoàn tác.",
      type: "danger",
      onConfirm: async () => {
        setConfirmDialog({ ...confirmDialog, isOpen: false });
        try {
          await ProductAccountApi.delete(accountId);
          showSuccess("Thành công", "Tài khoản đã được xóa thành công");
          loadAccounts();
        } catch (err) {
          console.error("Failed to delete account:", err);
          const errorMsg =
            err.response?.data?.message ||
            err.message ||
            "Không thể xóa tài khoản";
          showError("Lỗi xóa tài khoản", errorMsg);
        }
      },
    });
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
          <h1 style={{ margin: 0 }}>Kho Tài khoản</h1>
          <PermissionGuard moduleCode={MODULE_CODES.WAREHOUSE_MANAGER} permissionCode={PERMISSION_CODES.CREATE}>
            <Link className="btn primary" to="/accounts/add">
              + Tạo tài khoản mới
            </Link>
          </PermissionGuard>
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
              placeholder="Email/Username/Sản phẩm..."
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
            <label className="muted">Loại tài khoản</label>
            <select
              className="input"
              value={filters.productType}
              onChange={(e) => handleFilterChange("productType", e.target.value)}
            >
              <option value="">Tất cả</option>
              <option value="SHARED_ACCOUNT">Tài khoản dùng chung</option>
              <option value="PERSONAL_ACCOUNT">Tài khoản cá nhân</option>
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
              <option value="Active">Hoạt động</option>
              <option value="Full">Đầy</option>
              <option value="Expired">Hết hạn</option>
              <option value="Error">Lỗi</option>
              <option value="Inactive">Không hoạt động</option>
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

      {/* Accounts Table */}
      <section className="card" style={{ marginTop: 14 }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 12,
          }}
        >
          <h2 style={{ margin: 0 }}>Danh sách Tài khoản</h2>
          <span className="muted">
            {totalCount} mục · Trang {filters.pageNumber}/{totalPages}
          </span>
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
                    <th>Email</th>
                    <th>Username</th>
                    <th>Slot</th>
                    <th>Trạng thái</th>
                    <th>Hết hạn</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {accounts.length === 0 ? (
                    <tr>
                      <td
                        colSpan="7"
                        style={{ textAlign: "center", padding: 20 }}
                      >
                        Không có tài khoản nào
                      </td>
                    </tr>
                  ) : (
                    accounts.map((account) => {
                      const statusStyle = getAccountStatusColor(account.status);
                      return (
                        <tr key={account.productAccountId}>
                          <td>{account.productName}</td>
                          <td>
                            <Link
                              to={`/accounts/${account.productAccountId}`}
                              style={{ fontFamily: "monospace" }}
                            >
                              <ChunkedText
                                value={account.accountEmail}
                                fallback="-"
                                chunkSize={28}
                                className="chunk-text--cell chunk-text--block"
                              />
                            </Link>
                          </td>
                          <td>{account.accountUsername || "—"}</td>
                          <td>
                            <span
                              style={{
                                padding: "4px 8px",
                                borderRadius: "4px",
                                fontSize: "12px",
                                background:
                                  account.currentUsers >= account.maxUsers
                                    ? "#fef3c7"
                                    : "#f3f4f6",
                                color:
                                  account.currentUsers >= account.maxUsers
                                    ? "#92400e"
                                    : "#374151",
                              }}
                            >
                              {account.currentUsers}/{account.maxUsers}
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
                              {getAccountStatusLabel(account.status)}
                            </span>
                          </td>
                          <td>
                            {account.expiryDate
                              ? new Date(account.expiryDate).toLocaleDateString(
                                  "vi-VN"
                                )
                              : "—"}
                          </td>
                          <td>
                            <div style={{ display: "flex", gap: 6 }}>
                              <PermissionGuard moduleCode={MODULE_CODES.WAREHOUSE_MANAGER} permissionCode={PERMISSION_CODES.VIEW_DETAIL}>
                                <Link
                                  className="btn"
                                  to={`/accounts/${account.productAccountId}`}
                                  style={{ padding: "4px 8px", fontSize: "13px" }}
                                >
                                  Chi tiết
                                </Link>
                              </PermissionGuard>
                              {account.currentUsers === 0 && (
                                <PermissionGuard moduleCode={MODULE_CODES.WAREHOUSE_MANAGER} permissionCode={PERMISSION_CODES.DELETE}>
                                  <button
                                    className="btn"
                                    onClick={() =>
                                      handleDeleteAccount(
                                        account.productAccountId
                                      )
                                    }
                                    style={{
                                      padding: "4px 8px",
                                      fontSize: "13px",
                                      color: "#dc2626",
                                    }}
                                  >
                                    Xóa
                                  </button>
                                </PermissionGuard>
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
