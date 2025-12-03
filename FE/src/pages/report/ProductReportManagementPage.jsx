import React, { useState, useEffect, useCallback } from "react";
import { Link } from "react-router-dom";
import { ProductReportApi } from "../../services/productReportApi";
import ToastContainer from "../../components/Toast/ToastContainer";
import useToast from "../../hooks/useToast";
import "../admin/admin.css";

export default function ProductReportManagementPage() {
  const { toasts, showError, removeToast } = useToast();

  const [loading, setLoading] = useState(false);
  const [reports, setReports] = useState([]);

  // Filter states
  const [filters, setFilters] = useState({
    status: "",
    pageNumber: 1,
    pageSize: 20,
  });

  // Pagination states
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);

  const loadReports = useCallback(async () => {
    setLoading(true);
    try {
      const data = await ProductReportApi.list(filters);
      setReports(data.items || []);
      setTotalCount(data.totalCount || 0);
      setTotalPages(data.totalPages || 0);
    } catch (err) {
      console.error("Failed to load reports:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể tải danh sách báo cáo";
      showError("Lỗi tải dữ liệu", errorMsg);
    } finally {
      setLoading(false);
    }
  }, [filters, showError]);

  useEffect(() => {
    loadReports();
  }, [loadReports]);

  const handleFilterChange = (field, value) => {
    setFilters((prev) => ({
      ...prev,
      [field]: value,
      pageNumber: 1, // Reset to first page when filter changes
    }));
  };

  const handleApplyFilters = () => {
    loadReports();
  };

  const handlePageChange = (newPage) => {
    setFilters((prev) => ({ ...prev, pageNumber: newPage }));
  };

  const getStatusLabel = (status) => {
    switch (status) {
      case "Pending":
        return "Chờ xử lý";
      case "Processing":
        return "Đang xử lý";
      case "Resolved":
        return "Đã giải quyết";
      default:
        return status;
    }
  };

  const getStatusColor = (status) => {
    switch (status) {
      case "Pending":
        return { bg: "#fef3c7", color: "#92400e" }; // Yellow
      case "Processing":
        return { bg: "#dbeafe", color: "#1e40af" }; // Blue
      case "Resolved":
        return { bg: "#d1fae5", color: "#065f46" }; // Green
      default:
        return { bg: "#f3f4f6", color: "#374151" }; // Gray
    }
  };

  return (
    <div className="page">
      <ToastContainer toasts={toasts} onRemove={removeToast} />

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
          <h1 style={{ margin: 0 }}>Quản lý Báo cáo Sản phẩm</h1>
          <Link className="btn primary" to="/reports/add">
            + Tạo báo cáo mới
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
            <label className="muted">Trạng thái</label>
            <select
              className="input"
              value={filters.status}
              onChange={(e) => handleFilterChange("status", e.target.value)}
            >
              <option value="">Tất cả</option>
              <option value="Pending">Chờ xử lý</option>
              <option value="Processing">Đang xử lý</option>
              <option value="Resolved">Đã giải quyết</option>
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

      {/* Reports Table */}
      <section className="card" style={{ marginTop: 14 }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 12,
          }}
        >
          <h2 style={{ margin: 0 }}>Danh sách Báo cáo</h2>
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
                    <th>Tiêu đề</th>
                    <th>Người báo cáo</th>
                    <th>Ngày tạo</th>
                    <th>Trạng thái</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {reports.length === 0 ? (
                    <tr>
                      <td
                        colSpan="5"
                        style={{ textAlign: "center", padding: 20 }}
                      >
                        Không có báo cáo nào
                      </td>
                    </tr>
                  ) : (
                    reports.map((report) => {
                      const statusStyle = getStatusColor(report.status);
                      return (
                        <tr key={report.id}>
                          <td>{report.title}</td>
                          <td>{report.userEmail || "—"}</td>
                          <td>
                            {report.createdAt
                              ? new Date(report.createdAt).toLocaleDateString(
                                  "vi-VN"
                                )
                              : "—"}
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
                              {getStatusLabel(report.status)}
                            </span>
                          </td>
                          <td>
                            <div style={{ display: "flex", gap: 6 }}>
                              <Link
                                className="btn"
                                to={`/reports/${report.id}`}
                                style={{ padding: "4px 8px", fontSize: "13px" }}
                              >
                                Chi tiết
                              </Link>
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
