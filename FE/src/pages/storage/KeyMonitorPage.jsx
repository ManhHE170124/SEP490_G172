import React, { useEffect, useMemo, useState, useCallback } from "react";
import { ProductApi } from "../../services/products";
import { ProductKeyApi } from "../../services/productKeys";
import { ProductAccountApi } from "../../services/productAccounts";
import { SupplierApi } from "../../services/suppliers";
import { ProductVariantsApi } from "../../services/productVariants";
import { ProductReportApi } from "../../services/productReportApi";
import ToastContainer from "../../components/Toast/ToastContainer";
import useToast from "../../hooks/useToast";
import "../admin/admin.css";
// Default threshold for low-stock if backend not provided
const DEFAULT_LOW_STOCK_THRESHOLD = 20;
export default function KeyMonitorPage() {
  const { toasts, showSuccess, showError, removeToast } = useToast();
  // Removed product type dropdown/mode
  // KPI counters
  const [lowStockItems, setLowStockItems] = useState([]);
  const [customerIssueCount, setCustomerIssueCount] = useState(0);
  const [accountErrorCount, setAccountErrorCount] = useState(0);
  const [expiringSoonCount, setExpiringSoonCount] = useState(0);
  const [loadingLowStock, setLoadingLowStock] = useState(false);
  const [loadingCounters, setLoadingCounters] = useState(false);
  // Product reports state
  const [keyReports, setKeyReports] = useState([]);
  const [accountReports, setAccountReports] = useState([]);
  const [loadingKeyReports, setLoadingKeyReports] = useState(false);
  const [loadingAccountReports, setLoadingAccountReports] = useState(false);

  // Expiring accounts state
  const [expiringAccounts, setExpiringAccounts] = useState([]);
  const [loadingExpiringAccounts, setLoadingExpiringAccounts] = useState(false);
  // Pagination state for key reports
  const [keyReportsPage, setKeyReportsPage] = useState(1);
  const [keyReportsPageSize, setKeyReportsPageSize] = useState(10);
  const [keyReportsTotalPages, setKeyReportsTotalPages] = useState(1);
  const [keyReportsTotalItems, setKeyReportsTotalItems] = useState(0);
  // Pagination state for account reports
  const [accountReportsPage, setAccountReportsPage] = useState(1);
  const [accountReportsPageSize, setAccountReportsPageSize] = useState(10);
  const [accountReportsTotalPages, setAccountReportsTotalPages] = useState(1);
  const [accountReportsTotalItems, setAccountReportsTotalItems] = useState(0);
  // Import CSV modal state (keys only)
  const [showImportModal, setShowImportModal] = useState(false);
  const [importProduct, setImportProduct] = useState(null);
  const [csvFile, setCsvFile] = useState(null);
  const [keyType, setKeyType] = useState("Individual");
  const [expiryDate, setExpiryDate] = useState("");
  const [suppliers, setSuppliers] = useState([]);
  const [selectedSupplierId, setSelectedSupplierId] = useState("");
  const [variants, setVariants] = useState([]);
  const [loadingVariants, setLoadingVariants] = useState(false);
  const [selectedVariantId, setSelectedVariantId] = useState("");
  const [cogsPrice, setCogsPrice] = useState("");
  const [uploading, setUploading] = useState(false);
  const minExpiryDate = useMemo(
    () => new Date().toISOString().split("T")[0],
    []
  );
  const isCogsPriceValid = useMemo(() => {
    const parsed = parseFloat(cogsPrice);
    return Number.isFinite(parsed) && parsed > 0;
  }, [cogsPrice]);
  const selectedVariant = useMemo(
    () => variants.find((v) => v.variantId === selectedVariantId) || null,
    [variants, selectedVariantId]
  );
  // Load key reports (product reports with ProductKeyId)
  const loadKeyReports = useCallback(async () => {
    setLoadingKeyReports(true);
    try {
      const response = await ProductReportApi.getKeyErrors({
        pageNumber: keyReportsPage,
        pageSize: keyReportsPageSize,
      });
      const items = response?.items || response?.data || [];
      setKeyReports(items);
      setKeyReportsTotalPages(response?.totalPages || 1);
      setKeyReportsTotalItems(response?.totalItems || response?.total || 0);
    } catch (err) {
      console.error("Failed to load key reports:", err);
      showError(
        "Lỗi tải báo cáo",
        err.message || "Không thể tải danh sách báo cáo key"
      );
      setKeyReports([]);
      setKeyReportsTotalPages(1);
      setKeyReportsTotalItems(0);
    } finally {
      setLoadingKeyReports(false);
    }
  }, [showError, keyReportsPage, keyReportsPageSize]);

  // Load account reports (product reports with ProductAccountId)
  const loadAccountReports = useCallback(async () => {
    setLoadingAccountReports(true);
    try {
      const response = await ProductReportApi.getAccountErrors({
        pageNumber: accountReportsPage,
        pageSize: accountReportsPageSize,
      });
      const items = response?.items || response?.data || [];
      setAccountReports(items);
      setAccountReportsTotalPages(response?.totalPages || 1);
      setAccountReportsTotalItems(response?.totalItems || response?.total || 0);
    } catch (err) {
      console.error("Failed to load account reports:", err);
      showError(
        "Lỗi tải báo cáo",
        err.message || "Không thể tải danh sách báo cáo tài khoản"
      );
      setAccountReports([]);
      setAccountReportsTotalPages(1);
      setAccountReportsTotalItems(0);
    } finally {
      setLoadingAccountReports(false);
    }
  }, [showError, accountReportsPage, accountReportsPageSize]);

  // Load KPI counters
  const loadCounters = useCallback(async () => {
    setLoadingCounters(true);
    try {
      // Customer key issues: get count from key reports API
      try {
        const keyReportCountResponse = await ProductReportApi.countKeyErrors();
        // Use ?? instead of || to handle 0 properly
        const keyReportCount = typeof keyReportCountResponse === 'number'
          ? keyReportCountResponse
          : (keyReportCountResponse?.count ?? keyReportCountResponse?.data?.count ?? 0);
        setCustomerIssueCount(keyReportCount);
      } catch (e) {
        console.error("Failed to count key errors:", e);
        setCustomerIssueCount(0);
      }
      // Accounts error count from product reports API
      try {
        const accountReportCountResponse = await ProductReportApi.countAccountErrors();
        // Use ?? instead of || to handle 0 properly
        const accountReportCount = typeof accountReportCountResponse === 'number'
          ? accountReportCountResponse
          : (accountReportCountResponse?.count ?? accountReportCountResponse?.data?.count ?? 0);
        setAccountErrorCount(accountReportCount);
      } catch (e) {
        console.error("Failed to count account errors:", e);
        setAccountErrorCount(0);
      }
      // Expiring soon (placeholder): backend not provided yet
      setExpiringSoonCount(0);
    } finally {
      setLoadingCounters(false);
    }
  }, []);

  // Load expiring accounts
  const loadExpiringAccounts = useCallback(async () => {
    setLoadingExpiringAccounts(true);
    try {
      const data = await ProductAccountApi.getExpiringSoon(5);
      setExpiringAccounts(Array.isArray(data) ? data : data?.data || []);
      // Update counter
      setExpiringSoonCount(data?.length || 0);
    } catch (err) {
      console.error("Failed to load expiring accounts:", err);
      showError(
        "Lỗi tải dữ liệu",
        err.message || "Không thể tải danh sách tài khoản sắp hết hạn"
      );
      setExpiringAccounts([]);
    } finally {
      setLoadingExpiringAccounts(false);
    }
  }, [showError]);
  // Load low stock (keys only)
  const loadLowStock = useCallback(async () => {
    setLoadingLowStock(true);
    try {
      const prods = await ProductApi.list({
        pageNumber: 1,
        pageSize: 100,
        productTypes: ["PERSONAL_KEY", "SHARED_KEY"],
      });
      const items = prods.items || prods.data || [];
      const results = [];
      // Query available count per product (rely on totalCount from list)
      for (const p of items) {
        try {
          const keyRes = await ProductKeyApi.list({
            productId: p.productId,
            status: "Available",
            pageNumber: 1,
            pageSize: 1,
          });
          const availableCount = keyRes.totalCount || keyRes.total || 0;
          const threshold = p.lowStockThreshold || DEFAULT_LOW_STOCK_THRESHOLD;
          if (availableCount < threshold) {
            results.push({
              productId: p.productId,
              productName: p.productName,
              availableCount,
              threshold,
              updatedAt: p.updatedAt || null,
            });
          }
        } catch (err) {
          // Ignore errors per product
        }
      }
      setLowStockItems(results);
    } catch (err) {
      console.error("Failed to compute low stock:", err);
      showError(
        "Lỗi tải dữ liệu",
        err.message || "Không thể tải cảnh báo tồn kho"
      );
    } finally {
      setLoadingLowStock(false);
    }
  }, [showError]);
  useEffect(() => {
    loadCounters();
  }, [loadCounters]);
  useEffect(() => {
    loadLowStock();
  }, [loadLowStock]);
  useEffect(() => {
    loadKeyReports();
  }, [loadKeyReports]);
  useEffect(() => {
    loadAccountReports();
  }, [loadAccountReports]);

  useEffect(() => {
    loadExpiringAccounts();
  }, [loadExpiringAccounts]);
  const openImportModal = async (product) => {
    setImportProduct(product);
    setCsvFile(null);
    setKeyType("Individual");
    setExpiryDate("");
    setSelectedSupplierId("");
    setSelectedVariantId("");
    setCogsPrice("");
    setVariants([]);
    // Load suppliers for selection
    try {
      const response = await SupplierApi.listByProduct(product.productId);
      const mapped = Array.isArray(response)
        ? response
        : response?.items || response?.data || [];
      setSuppliers(mapped);
      if (!mapped?.length) {
        showError(
          "Chưa có nhà cung cấp",
          "Không tìm thấy nhà cung cấp phù hợp cho sản phẩm này"
        );
      }
    } catch (e) {
      console.error("Failed to load suppliers for product:", e);
      showError("Lỗi tải nhà cung cấp", "Không thể tải danh sách nhà cung cấp");
      setSuppliers([]);
    }
    // Load variants for the selected product
    try {
      setLoadingVariants(true);
      const response = await ProductVariantsApi.list(product.productId, {
        pageNumber: 1,
        pageSize: 100,
      });
      const mappedVariants = response?.items || response?.data || [];
      setVariants(mappedVariants);
      if (!mappedVariants.length) {
        showError(
          "Chưa có biến thể",
          "Sản phẩm này chưa có biến thể nào để nhập key"
        );
      }
    } catch (e) {
      console.error("Failed to load variants for product:", e);
      showError("Lỗi tải biến thể", "Không thể tải danh sách biến thể");
      setVariants([]);
    } finally {
      setLoadingVariants(false);
    }
    setShowImportModal(true);
  };
  const closeImportModal = () => {
    setShowImportModal(false);
    setImportProduct(null);
    setCsvFile(null);
    setExpiryDate("");
    setSelectedSupplierId("");
    setSelectedVariantId("");
    setCogsPrice("");
    setVariants([]);
    setLoadingVariants(false);
  };
  const handleCsvChange = (e) => {
    const f = e.target.files?.[0];
    if (!f) return;
    if (!f.name.endsWith(".csv")) {
      showError("Lỗi file", "Vui lòng chọn file .csv");
      return;
    }
    setCsvFile(f);
  };
  const handleUploadCsv = async () => {
    if (!csvFile || !importProduct || !selectedSupplierId) {
      showError("Thiếu dữ liệu", "Chọn nhà cung cấp và file CSV");
      return;
    }
    if (!selectedVariantId) {
      showError("Thiếu dữ liệu", "Vui lòng chọn biến thể cần nhập key");
      return;
    }
    const parsedCogs = parseFloat(cogsPrice);
    if (!Number.isFinite(parsedCogs) || parsedCogs <= 0) {
      showError("Giá vốn không hợp lệ", "Nhập giá vốn lớn hơn 0");
      return;
    }
    if (expiryDate) {
      const selected = new Date(expiryDate);
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      if (selected < today) {
        showError(
          "Ngày hết hạn không hợp lệ",
          "Không thể chọn ngày trong quá khứ"
        );
        return;
      }
    }
    setUploading(true);
    try {
      const form = new FormData();
      form.append("file", csvFile);
      form.append("productId", importProduct.productId);
      form.append("variantId", selectedVariantId);
      form.append("supplierId", selectedSupplierId);
      form.append("keyType", keyType);
      form.append("cogsPrice", parsedCogs);
      if (expiryDate) form.append("expiryDate", expiryDate);
      await ProductKeyApi.importCsv(form);
      showSuccess("Thành công", "Đã nhập key từ CSV vào kho");
      closeImportModal();
      loadLowStock();
    } catch (err) {
      console.error("Upload CSV error:", err);
      const msg =
        err.response?.data?.message || err.message || "Không thể upload";
      showError("Lỗi upload", msg);
    } finally {
      setUploading(false);
    }
  };
  return (
    <div className="page">
      <ToastContainer toasts={toasts} onRemove={removeToast} />

      <section className="card">
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 12,
          }}
        >
          <h1 style={{ margin: 0 }}>Theo dõi tình trạng</h1>
        </div>
        <div
          className="grid"
          style={{
            gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
            gap: 12,
          }}
        >
          <div
            className="kpi list-row"
            style={{ justifyContent: "space-between" }}
          >
            <div>Key sắp hết</div>
            <div className="v" style={{ color: "#b45309" }}>
              {loadingLowStock ? "..." : `${lowStockItems.length} SP`}
            </div>
          </div>
          <div
            className="kpi list-row"
            style={{ justifyContent: "space-between" }}
          >
            <div>Key lỗi</div>
            <div className="v" style={{ color: "#dc2626" }}>
              {loadingCounters ? "..." : customerIssueCount}
            </div>
          </div>
          <div
            className="kpi list-row"
            style={{ justifyContent: "space-between" }}
          >
            <div>Tài khoản lỗi</div>
            <div className="v" style={{ color: "#991b1b" }}>
              {loadingCounters ? "..." : accountErrorCount}
            </div>
          </div>
          <div
            className="kpi list-row"
            style={{ justifyContent: "space-between" }}
          >
            <div>Sắp hết hạn</div>
            <div className="v">
              {loadingCounters ? "..." : expiringSoonCount}
            </div>
          </div>
        </div>
      </section>

      <section className="card">
        <h2 style={{ margin: 0, marginBottom: 10 }}>Cảnh báo tồn kho thấp</h2>
        <table className="table">
          <thead>
            <tr>
              <th>Sản phẩm</th>
              <th>Tồn hiện tại</th>
              <th>Ngưỡng cảnh báo</th>
              <th>Ngày cập nhật</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loadingLowStock && (
              <tr>
                <td colSpan="5" style={{ padding: 16, textAlign: "center" }}>
                  Đang tải...
                </td>
              </tr>
            )}
            {!loadingLowStock && lowStockItems.length === 0 && (
              <tr>
                <td colSpan="5" style={{ padding: 16, textAlign: "center" }}>
                  Không có sản phẩm nào
                </td>
              </tr>
            )}
            {!loadingLowStock &&
              lowStockItems.map((it) => (
                <tr key={it.productId}>
                  <td>{it.productName}</td>
                  <td style={{ color: "#b45309", fontWeight: 600 }}>
                    {it.availableCount}
                  </td>
                  <td>{it.threshold}</td>
                  <td>
                    {it.updatedAt
                      ? new Date(it.updatedAt).toLocaleDateString()
                      : "-"}
                  </td>
                  <td>
                    <div className="action-buttons">
                      <button
                        className="btn"
                        onClick={() => openImportModal(it)}
                      >
                        Nhập key
                      </button>
                      <button className="btn">Điều chỉnh ngưỡng</button>
                    </div>
                  </td>
                </tr>
              ))}
          </tbody>
        </table>
        <small className="muted">
          Chỉ áp dụng cho Product Key cá nhân/chung.
        </small>
      </section>

      <section className="card">
        <h2 style={{ margin: 0, marginBottom: 10 }}>Key báo lỗi từ khách</h2>
        <table className="table">
          <thead>
            <tr>
              <th>Thời gian</th>
              <th>Tên báo cáo</th>
              <th>Mô tả lỗi</th>
              <th>Trạng thái</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loadingKeyReports && (
              <tr>
                <td colSpan="5" style={{ padding: 16, textAlign: "center" }}>
                  Đang tải...
                </td>
              </tr>
            )}
            {!loadingKeyReports && keyReports.length === 0 && (
              <tr>
                <td colSpan="5" style={{ padding: 16, textAlign: "center" }}>
                  Không có báo cáo nào
                </td>
              </tr>
            )}
            {!loadingKeyReports &&
              keyReports.map((report) => (
                <tr key={report.id}>
                  <td>
                    {report.createdAt
                      ? new Date(report.createdAt).toLocaleString("vi-VN")
                      : "-"}
                  </td>
                  <td>{report.name}</td>
                  <td style={{ color: "#991b1b", fontWeight: 600 }}>
                    {report.description}
                  </td>
                  <td>
                    <span
                      className="label-chip"
                      style={{
                        background:
                          report.status === "Resolved" ? "#16a34a" : "#dc2626",
                      }}
                    >
                      {report.status}
                    </span>
                  </td>
                  <td>
                    <div className="action-buttons">
                      <button className="btn">Xử lý</button>
                    </div>
                  </td>
                </tr>
              ))}
          </tbody>
        </table>
        {!loadingKeyReports && keyReportsTotalItems > 0 && (
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginTop: 12,
              gap: 8,
            }}
          >
            <div style={{ fontSize: 14, color: "#6b7280" }}>
              Hiển thị {(keyReportsPage - 1) * keyReportsPageSize + 1} -{" "}
              {Math.min(keyReportsPage * keyReportsPageSize, keyReportsTotalItems)}{" "}
              / {keyReportsTotalItems} báo cáo
            </div>
            <div style={{ display: "flex", gap: 8 }}>
              <button
                className="btn"
                onClick={() => setKeyReportsPage((p) => Math.max(1, p - 1))}
                disabled={keyReportsPage === 1}
              >
                ← Trước
              </button>
              <div style={{ padding: "6px 12px", fontSize: 14 }}>
                Trang {keyReportsPage} / {keyReportsTotalPages}
              </div>
              <button
                className="btn"
                onClick={() =>
                  setKeyReportsPage((p) => Math.min(keyReportsTotalPages, p + 1))
                }
                disabled={keyReportsPage === keyReportsTotalPages}
              >
                Sau →
              </button>
            </div>
          </div>
        )}
      </section>

      <section className="card">
        <h2 style={{ margin: 0, marginBottom: 10 }}>
          Tài khoản báo lỗi từ khách
        </h2>
        <table className="table">
          <thead>
            <tr>
              <th>Thời gian</th>
              <th>Tên báo cáo</th>
              <th>Mô tả lỗi</th>
              <th>Trạng thái</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loadingAccountReports && (
              <tr>
                <td colSpan="5" style={{ padding: 16, textAlign: "center" }}>
                  Đang tải...
                </td>
              </tr>
            )}
            {!loadingAccountReports && accountReports.length === 0 && (
              <tr>
                <td colSpan="5" style={{ padding: 16, textAlign: "center" }}>
                  Không có báo cáo nào
                </td>
              </tr>
            )}
            {!loadingAccountReports &&
              accountReports.map((report) => (
                <tr key={report.id}>
                  <td>
                    {report.createdAt
                      ? new Date(report.createdAt).toLocaleString("vi-VN")
                      : "-"}
                  </td>
                  <td>{report.name}</td>
                  <td style={{ color: "#991b1b", fontWeight: 600 }}>
                    {report.description}
                  </td>
                  <td>
                    <span
                      className="label-chip"
                      style={{
                        background:
                          report.status === "Resolved" ? "#16a34a" : "#dc2626",
                      }}
                    >
                      {report.status}
                    </span>
                  </td>
                  <td>
                    <div className="action-buttons">
                      <button className="btn">Xử lý</button>
                    </div>
                  </td>
                </tr>
              ))}
          </tbody>
        </table>
        {!loadingAccountReports && accountReportsTotalItems > 0 && (
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              marginTop: 12,
              gap: 8,
            }}
          >
            <div style={{ fontSize: 14, color: "#6b7280" }}>
              Hiển thị {(accountReportsPage - 1) * accountReportsPageSize + 1} -{" "}
              {Math.min(accountReportsPage * accountReportsPageSize, accountReportsTotalItems)}{" "}
              / {accountReportsTotalItems} báo cáo
            </div>
            <div style={{ display: "flex", gap: 8 }}>
              <button
                className="btn"
                onClick={() => setAccountReportsPage((p) => Math.max(1, p - 1))}
                disabled={accountReportsPage === 1}
              >
                ← Trước
              </button>
              <div style={{ padding: "6px 12px", fontSize: 14 }}>
                Trang {accountReportsPage} / {accountReportsTotalPages}
              </div>
              <button
                className="btn"
                onClick={() =>
                  setAccountReportsPage((p) => Math.min(accountReportsTotalPages, p + 1))
                }
                disabled={accountReportsPage === accountReportsTotalPages}
              >
                Sau →
              </button>
            </div>
          </div>
        )}
      </section>

      <section className="card">
        <h2 style={{ margin: 0, marginBottom: 10 }}>
          Tài khoản sắp hết hạn (5 ngày)
        </h2>
        <table className="table">
          <thead>
            <tr>
              <th>Sản phẩm</th>
              <th>Email tài khoản</th>
              <th>Biến thể</th>
              <th>Ngày hết hạn</th>
              <th>Slot</th>
              <th>Trạng thái</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {loadingExpiringAccounts && (
              <tr>
                <td colSpan="7" style={{ padding: 16, textAlign: "center" }}>
                  Đang tải...
                </td>
              </tr>
            )}
            {!loadingExpiringAccounts && expiringAccounts.length === 0 && (
              <tr>
                <td colSpan="7" style={{ padding: 16, textAlign: "center" }}>
                  Không có tài khoản sắp hết hạn
                </td>
              </tr>
            )}
            {!loadingExpiringAccounts &&
              expiringAccounts.map((account) => {
                const daysLeft = account.expiryDate
                  ? Math.ceil(
                      (new Date(account.expiryDate) - new Date()) /
                        (1000 * 60 * 60 * 24)
                    )
                  : 0;

                return (
                  <tr key={account.productAccountId}>
                    <td>{account.productName}</td>
                    <td>{account.accountEmail}</td>
                    <td>{account.variantTitle}</td>
                    <td>
                      <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                        <span>
                          {account.expiryDate
                            ? new Date(account.expiryDate).toLocaleDateString(
                                "vi-VN"
                              )
                            : "-"}
                        </span>
                        <span
                          style={{
                            fontSize: 12,
                            color: daysLeft <= 2 ? "#dc2626" : "#d97706",
                            fontWeight: 600,
                          }}
                        >
                          Còn {daysLeft} ngày
                        </span>
                      </div>
                    </td>
                    <td>
                      {account.currentUsers}/{account.maxUsers}
                    </td>
                    <td>
                      <span
                        className="label-chip"
                        style={{
                          background:
                            account.status === "Active"
                              ? "#16a34a"
                              : account.status === "Full"
                              ? "#ea580c"
                              : "#6b7280",
                        }}
                      >
                        {account.status}
                      </span>
                    </td>
                    <td>
                      <div className="action-buttons">
                        <button
                          className="btn"
                          onClick={() =>
                            (window.location.href = `/accounts/${account.productAccountId}`)
                          }
                        >
                          Chi tiết
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
          </tbody>
        </table>
        <small className="muted">
          Danh sách tài khoản sẽ hết hạn trong vòng 5 ngày tới.
        </small>
      </section>

      {showImportModal && (
        <div className="modal-backdrop" onClick={closeImportModal}>
          <div className="modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Nhập key từ CSV</h3>
            </div>
            <div className="modal-body">
              <div className="grid" style={{ gridTemplateColumns: "1fr" }}>
                <div className="form-row">
                  <label>Sản phẩm</label>
                  <div className="label-chip" style={{ background: "#1e40af" }}>
                    {importProduct?.productName}
                  </div>
                  {selectedVariant && (
                    <div
                      className="label-chip"
                      style={{ background: "#0f766e", marginTop: 6 }}
                    >
                      {selectedVariant.title}
                    </div>
                  )}
                </div>
                <div className="form-row">
                  <label>Biến thể</label>
                  <select
                    className="input"
                    value={selectedVariantId}
                    onChange={(e) => setSelectedVariantId(e.target.value)}
                    disabled={loadingVariants || variants.length === 0}
                  >
                    <option value="">
                      {loadingVariants
                        ? "Đang tải biến thể..."
                        : variants.length === 0
                        ? "Không có biến thể khả dụng"
                        : "Chọn biến thể"}
                    </option>
                    {variants.map((variant) => (
                      <option key={variant.variantId} value={variant.variantId}>
                        {variant.title}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-row">
                  <label>Nhà cung cấp</label>
                  <select
                    className="input"
                    value={selectedSupplierId}
                    onChange={(e) => setSelectedSupplierId(e.target.value)}
                  >
                    <option value="">-- Chọn nhà cung cấp --</option>
                    {suppliers.map((s) => (
                      <option key={s.supplierId} value={s.supplierId}>
                        {s.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-row">
                  <label>Giá vốn / key</label>
                  <input
                    className="input"
                    type="number"
                    min="0"
                    step="0.01"
                    placeholder="VD: 120000"
                    value={cogsPrice}
                    onChange={(e) => setCogsPrice(e.target.value)}
                  />
                </div>
                <div className="form-row">
                  <label>File CSV</label>
                  <input
                    className="input"
                    type="file"
                    accept=".csv"
                    onChange={handleCsvChange}
                  />
                </div>
                <div className="form-row">
                  <label>Loại key</label>
                  <select
                    className="input"
                    value={keyType}
                    onChange={(e) => setKeyType(e.target.value)}
                  >
                    <option value="Individual">Cá nhân (Individual)</option>
                    <option value="Pool">Chung (Pool)</option>
                  </select>
                </div>
                <div className="form-row">
                  <label>Ngày hết hạn</label>
                  <input
                    className="input"
                    type="date"
                    min={minExpiryDate}
                    value={expiryDate}
                    onChange={(e) => setExpiryDate(e.target.value)}
                  />
                  <small className="muted">Để trống nếu không có.</small>
                </div>
              </div>
            </div>
            <div className="modal-footer">
              <button
                className="btn"
                onClick={closeImportModal}
                disabled={uploading}
              >
                Hủy
              </button>
              <button
                className="btn primary"
                onClick={handleUploadCsv}
                disabled={
                  uploading ||
                  !csvFile ||
                  !selectedSupplierId ||
                  !selectedVariantId ||
                  !isCogsPriceValid
                }
              >
                {uploading ? "Đang upload..." : "Upload"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
