import React, { useState, useCallback, useEffect } from "react";
import { Link } from "react-router-dom";
import { SupplierApi } from "../../services/suppliers";
import ToastContainer from "../../components/Toast/ToastContainer";
import ConfirmDialog from "../../components/ConfirmDialog/ConfirmDialog";
import useToast from "../../hooks/useToast";
import "../admin/admin.css";

export default function SuppliersPage() {
  const { toasts, showSuccess, showError, removeToast } =
    useToast();
  const [suppliers, setSuppliers] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [query, setQuery] = useState({
    searchTerm: "",
    status: "", // "" = all, "Active", "Deactive"
    pageNumber: 1,
    pageSize: 10,
  });
  const [confirmDialog, setConfirmDialog] = useState({
    isOpen: false,
    title: "",
    message: "",
    onConfirm: null,
    type: "warning",
  });

  const loadSuppliers = useCallback(() => {
    setLoading(true);
    const params = { ...query };

    // Normalize search term
    if (typeof params.searchTerm === "string") {
      params.searchTerm = params.searchTerm.trim();
      if (params.searchTerm === "") delete params.searchTerm;
    }

    // Remove status from params if empty (we'll filter on client-side)
    if (params.status === "") delete params.status;

    SupplierApi.list(params)
      .then((res) => {
        let items = res.items || res.data || [];

        // Client-side filtering by status if specified
        if (query.status) {
          items = items.filter((supplier) => supplier.status === query.status);
        }

        setSuppliers(items);
        setTotal(
          query.status ? items.length : res.total || res.totalCount || 0
        );
      })
      .catch((err) => {
        console.error("Failed to load suppliers:", err);
        const errorMsg =
          err.response?.data?.message ||
          err.message ||
          "Không thể tải danh sách nhà cung cấp";
        showError("Lỗi tải dữ liệu", errorMsg);
      })
      .finally(() => setLoading(false));
  }, [query, showError]);

  // Debounce search
  useEffect(() => {
    const timer = setTimeout(loadSuppliers, 400);
    return () => clearTimeout(timer);
  }, [query, loadSuppliers]);

  const handleToggleStatus = async (supplier) => {
    const isActive = supplier.status === "Active";
    const action = isActive ? "tạm dừng" : "kích hoạt lại";

    setConfirmDialog({
      isOpen: true,
      title: `Xác nhận ${action} nhà cung cấp`,
      message: `Bạn có chắc muốn ${action} nhà cung cấp "${supplier.name}"?`,
      type: isActive ? "danger" : "warning",
      onConfirm: async () => {
        setConfirmDialog({ ...confirmDialog, isOpen: false });

        try {
          await SupplierApi.toggleStatus(supplier.supplierId);

          showSuccess(
            "Thành công",
            `Nhà cung cấp đã được ${action} thành công`
          );
          loadSuppliers();
        } catch (err) {
          console.error("Failed to toggle supplier status:", err);
          const errorMsg =
            err.response?.data?.message ||
            err.message ||
            `Không thể ${action} nhà cung cấp`;
          showError(`Lỗi ${action}`, errorMsg);
        }
      },
    });
  };

  const formatDate = (dateString) => {
    if (!dateString) return "-";
    const date = new Date(dateString);
    return date.toLocaleDateString("vi-VN");
  };

  const getStatusBadge = (status) => {
    if (status === "Active") {
      return (
        <span
          style={{
            display: "inline-block",
            padding: "2px 8px",
            border: "1px solid #d1fae5",
            borderRadius: "999px",
            fontSize: "12px",
            background: "#d1fae5",
            color: "#065f46",
          }}
        >
          Đang hợp tác
        </span>
      );
    }
    return (
      <span
        style={{
          display: "inline-block",
          padding: "2px 8px",
          border: "1px solid #fee2e2",
          borderRadius: "999px",
          fontSize: "12px",
          background: "#fee2e2",
          color: "#991b1b",
        }}
      >
        Tạm dừng
      </span>
    );
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

      {/* Supplier List Section */}
      <section className="card">
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <h1 style={{ margin: 0 }}>Nhà cung cấp Key</h1>
          <Link className="btn primary" to="/suppliers/add">
            + Thêm nhà cung cấp
          </Link>
        </div>

        {/* Filters */}
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
          <div className="group" style={{ minWidth: 260 }}>
            <span>Từ khóa</span>
            <input
              className="input"
              placeholder="Tên/Email nhà cung cấp..."
              value={query.searchTerm}
              onChange={(e) =>
                setQuery((prev) => ({
                  ...prev,
                  searchTerm: e.target.value,
                  pageNumber: 1,
                }))
              }
            />
          </div>

          <div className="group" style={{ minWidth: 180 }}>
            <span>Trạng thái</span>
            <select
              className="input"
              value={query.status}
              onChange={(e) =>
                setQuery((prev) => ({
                  ...prev,
                  status: e.target.value,
                  pageNumber: 1,
                }))
              }
            >
              <option value="">Tất cả</option>
              <option value="Active">Đang hợp tác</option>
              <option value="Deactive">Tạm dừng</option>
            </select>
          </div>

          <button
            className="btn"
            onClick={() =>
              setQuery({
                searchTerm: "",
                status: "",
                pageNumber: 1,
                pageSize: 10,
              })
            }
          >
            Đặt lại
          </button>
        </div>

        {/* Table */}
        <table className="table" style={{ marginTop: 16 }}>
          <thead>
            <tr>
              <th>Tên</th>
              <th>Email liên hệ</th>
              <th>Số điện thoại</th>
              <th>Số sản phẩm</th>
              <th>Trạng thái</th>
              <th>Ngày tạo</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {loading && (
              <tr>
                <td colSpan="7" style={{ textAlign: "center", padding: 20 }}>
                  Đang tải...
                </td>
              </tr>
            )}
            {!loading && suppliers.length === 0 && (
              <tr>
                <td colSpan="7" style={{ textAlign: "center", padding: 20 }}>
                  Không có nhà cung cấp nào
                </td>
              </tr>
            )}
            {!loading &&
              suppliers.map((supplier) => (
                <tr key={supplier.supplierId}>
                  <td>
                    <Link to={`/suppliers/${supplier.supplierId}`}>
                      {supplier.name}
                    </Link>
                  </td>
                  <td>{supplier.contactEmail || "-"}</td>
                  <td>{supplier.contactPhone || "-"}</td>
                  <td>{supplier.activeProductCount || 0}</td>
                  <td>{getStatusBadge(supplier.status)}</td>
                  <td>{formatDate(supplier.createdAt)}</td>
                  <td>
                    <div style={{ display: "flex", gap: 6 }}>
                      <Link
                        className="btn"
                        to={`/suppliers/${supplier.supplierId}`}
                        title="Xem chi tiết"
                      >
                        Chi tiết
                      </Link>
                      <button
                        className="btn"
                        onClick={() => handleToggleStatus(supplier)}
                        title={
                          supplier.status === "Active"
                            ? "Tạm dừng"
                            : "Kích hoạt lại"
                        }
                      >
                        {supplier.status === "Active"
                          ? "Tạm dừng"
                          : "Kích hoạt lại"}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
          </tbody>
        </table>

        {/* Pagination */}
        <div className="pager" style={{ marginTop: 16 }}>
          <button
            disabled={query.pageNumber <= 1}
            onClick={() =>
              setQuery((prev) => ({ ...prev, pageNumber: prev.pageNumber - 1 }))
            }
          >
            Trước
          </button>
          <span style={{ padding: "0 12px" }}>
            Trang {query.pageNumber} / {Math.ceil(total / query.pageSize) || 1}
          </span>
          <button
            disabled={query.pageNumber * query.pageSize >= total}
            onClick={() =>
              setQuery((prev) => ({ ...prev, pageNumber: prev.pageNumber + 1 }))
            }
          >
            Tiếp
          </button>
        </div>
      </section>
    </div>
  );
}
