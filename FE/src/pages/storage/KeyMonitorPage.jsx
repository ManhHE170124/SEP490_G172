import React, { useEffect, useMemo, useState, useCallback } from "react";
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  Cell,
} from "recharts";
import { ProductApi } from "../../services/products";
import { ProductKeyApi } from "../../services/productKeys";
import { ProductAccountApi } from "../../services/productAccounts";
import { SupplierApi } from "../../services/suppliers";
import { ProductVariantsApi } from "../../services/productVariants";
import { ProductReportApi } from "../../services/productReportApi";
import ToastContainer from "../../components/Toast/ToastContainer";
import useToast from "../../hooks/useToast";
import "../admin/admin.css";
import "../../pages/PostManage/TagAndPostTypeManage.css";

const DEFAULT_LOW_STOCK_THRESHOLD = 20;

export default function KeyMonitorPage() {
  const { toasts, showSuccess, showError, removeToast } = useToast();
  const [activeTab, setActiveTab] = useState("keys"); // "keys" | "accounts"

  // ================= STATE: KEYS =================
  const [lowStockKeys, setLowStockKeys] = useState([]);
  const [loadingLowStockKeys, setLoadingLowStockKeys] = useState(false);

  const [keyReports, setKeyReports] = useState([]);
  const [loadingKeyReports, setLoadingKeyReports] = useState(false);
  const [keyReportsPage, setKeyReportsPage] = useState(1);
  const [keyReportsPageSize] = useState(10);
  const [keyReportsTotalPages, setKeyReportsTotalPages] = useState(1);
  const [keyReportsTotalItems, setKeyReportsTotalItems] = useState(0);

  const [expiringKeys, setExpiringKeys] = useState([]); // Chart data + (optional list)
  const [loadingExpiringKeys, setLoadingExpiringKeys] = useState(false);

  const [expiredKeys, setExpiredKeys] = useState([]);
  const [loadingExpiredKeys, setLoadingExpiredKeys] = useState(false);
  const [expiredKeysPage, setExpiredKeysPage] = useState(1);
  const [expiredKeysPageSize] = useState(10);
  const [expiredKeysTotalPages, setExpiredKeysTotalPages] = useState(1);
  const [expiredKeysTotalItems, setExpiredKeysTotalItems] = useState(0);

  // ================= STATE: ACCOUNTS =================
  const [lowStockAccounts, setLowStockAccounts] = useState([]);
  const [loadingLowStockAccounts, setLoadingLowStockAccounts] = useState(false);

  const [accountReports, setAccountReports] = useState([]);
  const [loadingAccountReports, setLoadingAccountReports] = useState(false);
  const [accountReportsPage, setAccountReportsPage] = useState(1);
  const [accountReportsPageSize] = useState(10);
  const [accountReportsTotalPages, setAccountReportsTotalPages] = useState(1);
  const [accountReportsTotalItems, setAccountReportsTotalItems] = useState(0);

  const [expiringAccounts, setExpiringAccounts] = useState([]);
  const [loadingExpiringAccounts, setLoadingExpiringAccounts] = useState(false);

  const [expiredAccounts, setExpiredAccounts] = useState([]);
  const [loadingExpiredAccounts, setLoadingExpiredAccounts] = useState(false);
  const [expiredAccountsPage, setExpiredAccountsPage] = useState(1);
  const [expiredAccountsPageSize] = useState(10);
  const [expiredAccountsTotalPages, setExpiredAccountsTotalPages] = useState(1);
  const [expiredAccountsTotalItems, setExpiredAccountsTotalItems] = useState(0);

  // ================= STATE: IMPORT MODAL (KEYS) =================
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

  const [showImportResultModal, setShowImportResultModal] = useState(false);
  const [importResult, setImportResult] = useState(null);

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

  // ================= FETCH DATA FUNCTIONS =================

  // 1. LOW STOCK
  const loadLowStockKeys = useCallback(async () => {
    setLoadingLowStockKeys(true);
    try {
      const res = await ProductApi.getLowStock("KEYS", DEFAULT_LOW_STOCK_THRESHOLD);
      setLowStockKeys(Array.isArray(res) ? res : res?.data || []);
    } catch (err) {
      console.error("Failed to load low stock keys:", err);
    } finally {
      setLoadingLowStockKeys(false);
    }
  }, []);

  const loadLowStockAccounts = useCallback(async () => {
    setLoadingLowStockAccounts(true);
    try {
      const res = await ProductApi.getLowStock("ACCOUNTS", DEFAULT_LOW_STOCK_THRESHOLD);
      setLowStockAccounts(Array.isArray(res) ? res : res?.data || []);
    } catch (err) {
      console.error("Failed to load low stock accounts:", err);
    } finally {
      setLoadingLowStockAccounts(false);
    }
  }, []);

  // 2. EXPIRED / EXPIRING KEYS
  const loadExpiredKeys = useCallback(async () => {
    setLoadingExpiredKeys(true);
    try {
      const response = await ProductKeyApi.getExpired({
        pageNumber: expiredKeysPage,
        pageSize: expiredKeysPageSize,
      });
      const items = response?.items || response?.data || [];
      setExpiredKeys(items);
      setExpiredKeysTotalPages(response?.totalPages || 1);
      setExpiredKeysTotalItems(response?.totalCount || response?.total || 0);
    } catch (err) {
      console.error("Failed to load expired keys:", err);
      setExpiredKeys([]);
    } finally {
      setLoadingExpiredKeys(false);
    }
  }, [expiredKeysPage, expiredKeysPageSize]);

  const loadExpiringKeys = useCallback(async () => {
    setLoadingExpiringKeys(true);
    try {
      // Fetch keys expiring in 5 days for chart
      const response = await ProductKeyApi.getExpiringSoon(5);
      const items = response?.items || response?.data || [];
      setExpiringKeys(items);
    } catch (err) {
      console.error("Failed to load expiring keys:", err);
      setExpiringKeys([]);
    } finally {
      setLoadingExpiringKeys(false);
    }
  }, []);

  // 3. EXPIRED / EXPIRING ACCOUNTS
  const loadExpiredAccounts = useCallback(async () => {
    setLoadingExpiredAccounts(true);
    try {
      const response = await ProductAccountApi.getExpired({
        pageNumber: expiredAccountsPage,
        pageSize: expiredAccountsPageSize,
      });
      const items = response?.items || response?.data || [];
      setExpiredAccounts(items);
      setExpiredAccountsTotalPages(response?.totalPages || 1);
      setExpiredAccountsTotalItems(response?.totalCount || response?.total || 0);
    } catch (err) {
      console.error("Failed to load expired accounts:", err);
      setExpiredAccounts([]);
    } finally {
      setLoadingExpiredAccounts(false);
    }
  }, [expiredAccountsPage, expiredAccountsPageSize]);

  const loadExpiringAccounts = useCallback(async () => {
    setLoadingExpiringAccounts(true);
    try {
      const data = await ProductAccountApi.getExpiringSoon(5);
      setExpiringAccounts(Array.isArray(data) ? data : data?.data || []);
    } catch (err) {
      console.error("Failed to load expiring accounts:", err);
      setExpiringAccounts([]);
    } finally {
      setLoadingExpiringAccounts(false);
    }
  }, []);

  // 4. REPORTS
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
      setKeyReports([]);
    } finally {
      setLoadingKeyReports(false);
    }
  }, [keyReportsPage, keyReportsPageSize]);

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
      setAccountReports([]);
    } finally {
      setLoadingAccountReports(false);
    }
  }, [accountReportsPage, accountReportsPageSize]);

  // ================= EFFECTS =================
  useEffect(() => {
    if (activeTab === "keys") {
      loadLowStockKeys();
      loadKeyReports();
      loadExpiredKeys();
      loadExpiringKeys();
    } else {
      loadLowStockAccounts();
      loadAccountReports();
      loadExpiredAccounts();
      loadExpiringAccounts();
    }
  }, [
    activeTab,
    loadLowStockKeys,
    loadKeyReports,
    loadExpiredKeys,
    loadExpiringKeys,
    loadLowStockAccounts,
    loadAccountReports,
    loadExpiredAccounts,
    loadExpiringAccounts,
  ]);

  // ================= IMPORT MODAL LOGIC =================
  const openImportModal = async (product) => {
    setImportProduct(product);
    setCsvFile(null);
    setKeyType("Individual");
    setExpiryDate("");
    setSelectedSupplierId("");
    setSelectedVariantId("");
    setCogsPrice("");
    setVariants([]);
    // Load suppliers
    try {
      const response = await SupplierApi.listByProduct(product.productId);
      const mapped = Array.isArray(response)
        ? response
        : response?.items || response?.data || [];
      setSuppliers(mapped);
      if (!mapped?.length) {
        showError("Chưa có nhà cung cấp", "Không tìm thấy nhà cung cấp phù hợp");
      }
    } catch (e) {
      showError("Lỗi tải nhà cung cấp", "Không thể tải danh sách nhà cung cấp");
      setSuppliers([]);
    }
    // Load variants
    try {
      setLoadingVariants(true);
      const response = await ProductVariantsApi.list(product.productId, {
        pageNumber: 1,
        pageSize: 100,
      });
      const mappedVariants = response?.items || response?.data || [];
      setVariants(mappedVariants);
      if (!mappedVariants.length) {
        showError("Chưa có biến thể", "Sản phẩm này chưa có biến thể nào");
      }
    } catch (e) {
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
  };

  const handleUploadCsv = async () => {
     // ... same logic as before ...
     if (!csvFile || !importProduct || !selectedSupplierId || !selectedVariantId) return;
     const parsedCogs = parseFloat(cogsPrice);
     if (!Number.isFinite(parsedCogs) || parsedCogs <= 0) {
        showError("Giá vốn không hợp lệ", "Nhập giá vốn lớn hơn 0");
        return;
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
       
       const response = await ProductKeyApi.importCsv(form);
       const data = response?.data || response;
       setImportResult(data);
       if (data.errors && data.errors.length > 0) {
         showError("Có lỗi xảy ra", "Vui lòng kiểm tra lại kết quả nhập liệu");
       } else {
         showSuccess("Thành công", "Đã nhập key từ CSV vào kho");
       }
       closeImportModal();
       setShowImportResultModal(true);
       loadLowStockKeys(); // Refresh
     } catch (err) {
       console.error("Upload CSV error:", err);
       const msg = err.response?.data?.message || err.message || "Không thể upload";
       showError("Lỗi upload", msg);
     } finally {
       setUploading(false);
     }
  };

  const handleCsvChange = (e) => {
    const f = e.target.files?.[0];
    if (f && !f.name.endsWith(".csv")) {
      showError("Lỗi file", "Vui lòng chọn file .csv");
      return;
    }
    setCsvFile(f);
  };

  const closeImportResultModal = () => {
    setShowImportResultModal(false);
    setImportResult(null);
  };

  // ================= RENDER HELPERS =================
  const renderPagination = (page, totalPages, setPage) => {
    if (totalPages <= 1) return null;
    return (
        <div style={{ display: "flex", gap: 8 }}>
            <button
            className="btn"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page === 1}
            >
            ← Trước
            </button>
            <div style={{ padding: "6px 12px", fontSize: 14 }}>
            Trang {page} / {totalPages}
            </div>
            <button
            className="btn"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={page === totalPages}
            >
            Sau →
            </button>
        </div>
    );
  };

  return (
    <div className="page">
      <ToastContainer toasts={toasts} onRemove={removeToast} />

      <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
          {/* Header & Tabs */}
          <div>
            <h1 style={{ marginBottom: 16 }}>Theo dõi tình trạng</h1>
            <div className="tag-pt-tabs">
                <button 
                    className={`tag-pt-tab-button ${activeTab === 'keys' ? 'active' : ''}`}
                    onClick={() => setActiveTab('keys')}
                >
                    Keys
                </button>
                <button 
                    className={`tag-pt-tab-button ${activeTab === 'accounts' ? 'active' : ''}`}
                    onClick={() => setActiveTab('accounts')}
                >
                    Tài khoản
                </button>
            </div>
          </div>

          {/* CONTENT: KEYS */}
          {activeTab === 'keys' && (
              <>
                 {/* Chart */}
                 <section className="card" style={{ padding: "20px" }}>
                    <div style={{ width: "100%", height: 350 }}>
                    <ResponsiveContainer>
                        <BarChart
                        data={[
                            {
                                name: "Key sắp hết",
                                value: lowStockKeys.length,
                                color: "#b45309",
                            },
                            {
                                name: "Key lỗi",
                                value: keyReportsTotalItems, // Use totalItems from reports
                                color: "#dc2626",
                            },
                            {
                                name: "Key sắp hết hạn",
                                value: expiringKeys.length,
                                color: "#ea580c",
                            },
                        ]}
                        margin={{ top: 20, right: 30, left: 20, bottom: 5 }}
                        >
                        <CartesianGrid strokeDasharray="3 3" />
                        <XAxis dataKey="name" />
                        <YAxis allowDecimals={false} />
                        <Tooltip />
                        <Legend />
                        <Bar dataKey="value" name="Số lượng">
                            {[
                                { color: "#b45309" },
                                { color: "#dc2626" },
                                { color: "#ea580c" },
                            ].map((entry, index) => (
                            <Cell key={`cell-${index}`} fill={entry.color} />
                            ))}
                        </Bar>
                        </BarChart>
                    </ResponsiveContainer>
                    </div>
                 </section>

                 {/* Low Stock Keys */}
                 <section className="card" style={{ padding: "20px" }}>
                    <h2 style={{ margin: "0 0 10px 0" }}>Cảnh báo tồn kho thấp</h2>
                    <table className="table">
                        <thead>
                            <tr>
                                <th>Sản phẩm</th>
                                <th>Tồn hiện tại</th>
                                <th>Ngưỡng</th>
                                <th>Loại</th>
                                <th>Hành động</th>
                            </tr>
                        </thead>
                        <tbody>
                            {loadingLowStockKeys ? (
                                <tr><td colSpan="5" className="text-center p-4">Đang tải...</td></tr>
                            ) : lowStockKeys.length === 0 ? (
                                <tr><td colSpan="5" className="text-center p-4">Không có sản phẩm nào</td></tr>
                            ) : (
                                lowStockKeys.map((item) => (
                                    <tr key={item.productId}>
                                        <td>{item.productName}</td>
                                        <td style={{ color: "#b45309", fontWeight: 600 }}>{item.availableCount}</td>
                                        <td>{item.threshold}</td>
                                        <td>{item.productType}</td>
                                        <td>
                                            <button className="btn" onClick={() => openImportModal(item)}>Nhập Key</button>
                                        </td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                 </section>

                 {/* Key Reports */}
                 <section className="card" style={{ padding: "20px" }}>
                    <h2 style={{ margin: "0 0 10px 0" }}>Key báo lỗi từ khách</h2>
                    <table className="table">
                        <thead>
                            <tr>
                                <th>Thời gian</th>
                                <th>Tên báo cáo</th>
                                <th>Mô tả</th>
                                <th>Trạng thái</th>
                                <th></th>
                            </tr>
                        </thead>
                        <tbody>
                             {loadingKeyReports ? (
                                 <tr><td colSpan="5" className="text-center p-4">Đang tải...</td></tr>
                             ) : keyReports.length === 0 ? (
                                 <tr><td colSpan="5" className="text-center p-4">Không có báo cáo nào</td></tr>
                             ) : (
                                 keyReports.map((report) => (
                                     <tr key={report.id}>
                                         <td>{report.createdAt ? new Date(report.createdAt).toLocaleString("vi-VN") : "-"}</td>
                                         <td>{report.name}</td>
                                         <td style={{ color: "#991b1b", fontWeight: 600 }}>{report.description}</td>
                                         <td>
                                            <span className="label-chip" style={{ background: report.status === "Resolved" ? "#16a34a" : "#dc2626" }}>
                                                {report.status}
                                            </span>
                                         </td>
                                         <td>
                                            <button className="btn" onClick={() => window.location.href=`/reports/${report.id}`}>Xử lý</button>
                                         </td>
                                     </tr>
                                 ))
                             )}
                        </tbody>
                    </table>
                    <div style={{ marginTop: 12, display: 'flex', justifyContent: 'flex-end' }}>
                         {renderPagination(keyReportsPage, keyReportsTotalPages, setKeyReportsPage)}
                    </div>
                 </section>


                 {/* Expiring Keys (5 days) */}
                 <section className="card" style={{ padding: "20px" }}>
                    <h2 style={{ margin: "0 0 10px 0" }}>Key sắp hết hạn</h2>
                    <table className="table">
                        <thead>
                            <tr>
                                <th>Key</th>
                                <th>Sản phẩm</th>
                                <th>Hết hạn</th>
                                <th>Trạng thái</th>
                            </tr>
                        </thead>
                        <tbody>
                            {loadingExpiringKeys ? (
                                <tr><td colSpan="4" className="text-center p-4">Đang tải...</td></tr>
                            ) : expiringKeys.length === 0 ? (
                                <tr><td colSpan="4" className="text-center p-4">Không có key sắp hết hạn</td></tr>
                            ) : (
                                expiringKeys.map((key) => {
                                    const daysLeft = key.expiryDate ? Math.ceil((new Date(key.expiryDate) - new Date()) / (86400000)) : 0;
                                    return (
                                        <tr key={key.keyId}>
                                            <td title={key.keyString}>{key.keyString?.substring(0, 15)}...</td>
                                            <td>{key.productName}</td>
                                            <td>
                                                <div>{key.expiryDate ? new Date(key.expiryDate).toLocaleDateString("vi-VN") : "-"}</div>
                                                <small style={{ color: daysLeft <= 2 ? "#dc2626" : "#d97706", fontWeight: 600 }}>Còn {daysLeft} ngày</small>
                                            </td>
                                            <td>{key.status}</td>
                                        </tr>
                                    );
                                })
                            )}
                        </tbody>
                    </table>
                 </section>

                 {/* Expired Keys */}
                 <section className="card" style={{ padding: "20px" }}>
                    <h2 style={{ margin: "0 0 10px 0" }}>Key đã hết hạn</h2>
                    <table className="table">
                        <thead>
                            <tr>
                                <th>Key</th>
                                <th>Sản phẩm</th>
                                <th>Ngày hết hạn</th>
                                <th>Trạng thái</th>
                            </tr>
                        </thead>
                        <tbody>
                             {loadingExpiredKeys ? (
                                 <tr><td colSpan="4" className="text-center p-4">Đang tải...</td></tr>
                             ) : expiredKeys.length === 0 ? (
                                 <tr><td colSpan="4" className="text-center p-4">Không có key hết hạn</td></tr>
                             ) : (
                                expiredKeys.map((key) => (
                                     <tr key={key.keyId}>
                                         <td title={key.keyString}>{key.keyString?.substring(0, 15)}...</td>
                                         <td>{key.productName}</td>
                                         <td style={{ color: "#dc2626", fontWeight: "bold" }}>
                                             {key.expiryDate ? new Date(key.expiryDate).toLocaleDateString("vi-VN") : "-"}
                                         </td>
                                         <td>{key.status}</td>
                                     </tr>
                                 ))
                             )}
                        </tbody>
                    </table>
                    <div style={{ marginTop: 12, display: 'flex', justifyContent: 'flex-end' }}>
                         {renderPagination(expiredKeysPage, expiredKeysTotalPages, setExpiredKeysPage)}
                    </div>
                 </section>
              </>
          )}

          {/* CONTENT: ACCOUNTS */}
          {activeTab === 'accounts' && (
              <>
                {/* Chart */}
                <section className="card" style={{ padding: "20px" }}>
                    <div style={{ width: "100%", height: 350 }}>
                    <ResponsiveContainer>
                        <BarChart
                        data={[
                            {
                                name: "Tài khoản sắp hết",
                                value: lowStockAccounts.length,
                                color: "#b45309",
                            },
                            {
                                name: "Tài khoản lỗi",
                                value: accountReportsTotalItems,
                                color: "#991b1b",
                            },
                            {
                                name: "Tài khoản sắp hết hạn",
                                value: expiringAccounts.length,
                                color: "#ea580c",
                            },
                        ]}
                        margin={{ top: 20, right: 30, left: 20, bottom: 5 }}
                        >
                        <CartesianGrid strokeDasharray="3 3" />
                        <XAxis dataKey="name" />
                        <YAxis allowDecimals={false} />
                        <Tooltip />
                        <Legend />
                        <Bar dataKey="value" name="Số lượng">
                            {[
                                { color: "#b45309" },
                                { color: "#991b1b" },
                                { color: "#ea580c" },
                            ].map((entry, index) => (
                            <Cell key={`cell-${index}`} fill={entry.color} />
                            ))}
                        </Bar>
                        </BarChart>
                    </ResponsiveContainer>
                    </div>
                 </section>

                 {/* Low Stock Accounts */}
                 <section className="card" style={{ padding: "20px" }}>
                    <h2 style={{ margin: "0 0 10px 0" }}>Cảnh báo tồn kho thấp</h2>
                    <table className="table">
                        <thead>
                            <tr>
                                <th>Sản phẩm</th>
                                <th>Slot khả dụng</th>
                                <th>Ngưỡng</th>
                                <th>Loại</th>
                                <th>Hành động</th>
                            </tr>
                        </thead>
                        <tbody>
                            {loadingLowStockAccounts ? (
                                <tr><td colSpan="5" className="text-center p-4">Đang tải...</td></tr>
                            ) : lowStockAccounts.length === 0 ? (
                                <tr><td colSpan="5" className="text-center p-4">Không có sản phẩm nào</td></tr>
                            ) : (
                                lowStockAccounts.map((item) => (
                                    <tr key={item.productId}>
                                        <td>{item.productName}</td>
                                        <td style={{ color: "#b45309", fontWeight: 600 }}>{item.availableCount}</td>
                                        <td>{item.threshold}</td>
                                        <td>{item.productType}</td>
                                        <td>
                                            <button className="btn" onClick={() => window.location.href=`/accounts/add?productId=${item.productId}`}>Thêm tài khoản</button>
                                        </td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                 </section>

                 {/* Account Reports */}
                 <section className="card" style={{ padding: "20px" }}>
                    <h2 style={{ margin: "0 0 10px 0" }}>Tài khoản báo lỗi từ khách</h2>
                    <table className="table">
                        <thead>
                            <tr>
                                <th>Thời gian</th>
                                <th>Tên báo cáo</th>
                                <th>Mô tả</th>
                                <th>Trạng thái</th>
                                <th></th>
                            </tr>
                        </thead>
                        <tbody>
                             {loadingAccountReports ? (
                                 <tr><td colSpan="5" className="text-center p-4">Đang tải...</td></tr>
                             ) : accountReports.length === 0 ? (
                                 <tr><td colSpan="5" className="text-center p-4">Không có báo cáo nào</td></tr>
                             ) : (
                                 accountReports.map((report) => (
                                     <tr key={report.id}>
                                         <td>{report.createdAt ? new Date(report.createdAt).toLocaleString("vi-VN") : "-"}</td>
                                         <td>{report.name}</td>
                                         <td style={{ color: "#991b1b", fontWeight: 600 }}>{report.description}</td>
                                         <td>
                                            <span className="label-chip" style={{ background: report.status === "Resolved" ? "#16a34a" : "#dc2626" }}>
                                                {report.status}
                                            </span>
                                         </td>
                                         <td>
                                            <button className="btn" onClick={() => window.location.href=`/reports/${report.id}`}>Xử lý</button>
                                         </td>
                                     </tr>
                                 ))
                             )}
                        </tbody>
                    </table>
                    <div style={{ marginTop: 12, display: 'flex', justifyContent: 'flex-end' }}>
                         {renderPagination(accountReportsPage, accountReportsTotalPages, setAccountReportsPage)}
                    </div>
                 </section>

                 {/* Expiring Accounts (5 days) */}
                 <section className="card" style={{ padding: "20px" }}>
                    <h2 style={{ margin: "0 0 10px 0" }}>Tài khoản sắp hết hạn</h2>
                    <table className="table">
                        <thead>
                            <tr>
                                <th>Sản phẩm</th>
                                <th>Email</th>
                                <th>Hết hạn</th>
                                <th>Slot</th>
                                <th>Trạng thái</th>
                                <th></th>
                            </tr>
                        </thead>
                        <tbody>
                            {loadingExpiringAccounts ? (
                                <tr><td colSpan="6" className="text-center p-4">Đang tải...</td></tr>
                            ) : expiringAccounts.length === 0 ? (
                                <tr><td colSpan="6" className="text-center p-4">Không có tài khoản sắp hết hạn</td></tr>
                            ) : (
                                expiringAccounts.map((acc) => {
                                    const daysLeft = acc.expiryDate ? Math.ceil((new Date(acc.expiryDate) - new Date()) / (86400000)) : 0;
                                    return (
                                        <tr key={acc.productAccountId}>
                                            <td>{acc.productName}</td>
                                            <td>{acc.accountEmail}</td>
                                            <td>
                                                <div>{acc.expiryDate ? new Date(acc.expiryDate).toLocaleDateString("vi-VN") : "-"}</div>
                                                <small style={{ color: daysLeft <= 2 ? "#dc2626" : "#d97706", fontWeight: 600 }}>Còn {daysLeft} ngày</small>
                                            </td>
                                            <td>{acc.currentUsers}/{acc.maxUsers}</td>
                                            <td>{acc.status}</td>
                                            <td><button className="btn" onClick={() => window.location.href=`/accounts/${acc.productAccountId}`}>Chi tiết</button></td>
                                        </tr>
                                    );
                                })
                            )}
                        </tbody>
                    </table>
                 </section>

                 {/* Expired Accounts */}
                 <section className="card" style={{ padding: "20px" }}>
                    <h2 style={{ margin: "0 0 10px 0" }}>Tài khoản đã hết hạn</h2>
                    <table className="table">
                        <thead>
                            <tr>
                                <th>Sản phẩm</th>
                                <th>Email</th>
                                <th>Hết hạn</th>
                                <th>Slot</th>
                                <th>Trạng thái</th>
                                <th></th>
                            </tr>
                        </thead>
                        <tbody>
                             {loadingExpiredAccounts ? (
                                 <tr><td colSpan="6" className="text-center p-4">Đang tải...</td></tr>
                             ) : expiredAccounts.length === 0 ? (
                                 <tr><td colSpan="6" className="text-center p-4">Không có tài khoản hết hạn</td></tr>
                             ) : (
                                expiredAccounts.map((acc) => (
                                     <tr key={acc.productAccountId}>
                                         <td>{acc.productName}</td>
                                         <td>{acc.accountEmail}</td>
                                         <td style={{ color: "#dc2626", fontWeight: "bold" }}>
                                             {acc.expiryDate ? new Date(acc.expiryDate).toLocaleDateString("vi-VN") : "-"}
                                         </td>
                                         <td>{acc.currentUsers}/{acc.maxUsers}</td>
                                         <td>{acc.status}</td>
                                         <td><button className="btn" onClick={() => window.location.href=`/accounts/${acc.productAccountId}`}>Chi tiết</button></td>
                                     </tr>
                                 ))
                             )}
                        </tbody>
                    </table>
                    <div style={{ marginTop: 12, display: 'flex', justifyContent: 'flex-end' }}>
                         {renderPagination(expiredAccountsPage, expiredAccountsTotalPages, setExpiredAccountsPage)}
                    </div>
                 </section>
              </>
          )}

      </div>

      {/* IMPORT MODAL (Reused logic) */}
      {showImportModal && (
        <div className="modal-backdrop" onClick={closeImportModal}>
          <div className="modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header"><h3>Nhập key từ CSV</h3></div>
            <div className="modal-body">
               {/* Simplified inputs for brevity in this rewrite, assuming similar fields */}
               <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                 <div className="group">
                    <span>Sản phẩm: <strong>{importProduct?.productName}</strong></span>
                 </div>
                 <div className="group">
                    <span>Biến thể</span>
                    <select className="input" value={selectedVariantId} onChange={(e) => setSelectedVariantId(e.target.value)}>
                        <option value="">Chọn biến thể</option>
                        {variants.map(v => <option key={v.variantId} value={v.variantId}>{v.title}</option>)}
                    </select>
                 </div>
                 <div className="group">
                    <span>Nhà cung cấp</span>
                    <select className="input" value={selectedSupplierId} onChange={(e) => setSelectedSupplierId(e.target.value)}>
                        <option value="">Chọn nhà cung cấp</option>
                        {suppliers.map(s => <option key={s.supplierId} value={s.supplierId}>{s.name}</option>)}
                    </select>
                 </div>
                 <div className="group">
                    <span>Giá vốn</span>
                    <input className="input" type="number" value={cogsPrice} onChange={(e) => setCogsPrice(e.target.value)} />
                 </div>
                 <div className="group">
                    <span>File CSV</span>
                    <input className="input" type="file" accept=".csv" onChange={handleCsvChange} />
                 </div>
                 <div className="group">
                    <span>Ngày hết hạn</span>
                    <input className="input" type="date" min={minExpiryDate} value={expiryDate} onChange={(e) => setExpiryDate(e.target.value)} />
                 </div>
               </div>
            </div>
            <div className="modal-footer">
               <button className="btn" onClick={closeImportModal}>Hủy</button>
               <button className="btn primary" onClick={handleUploadCsv} disabled={uploading}>
                  {uploading ? "Đang upload..." : "Upload"}
               </button>
            </div>
          </div>
        </div>
      )}

      {showImportResultModal && importResult && (
         <div className="modal-backdrop" onClick={closeImportResultModal}>
           <div className="modal-card" style={{ maxWidth: 500 }} onClick={(e) => e.stopPropagation()}>
             <div className="modal-header"><h3>Kết quả</h3></div>
             <div className="modal-body">
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10, textAlign: 'center' }}>
                    <div style={{ background: '#dcfce7', padding: 10, borderRadius: 8 }}>
                        <div style={{ color: '#166534', fontWeight: 'bold' }}>Thành công</div>
                        <div style={{ fontSize: 24, color: '#15803d' }}>{importResult.successfullyImported}</div>
                    </div>
                    <div style={{ background: '#fef9c3', padding: 10, borderRadius: 8 }}>
                        <div style={{ color: '#854d0e', fontWeight: 'bold' }}>Trùng</div>
                        <div style={{ fontSize: 24, color: '#a16207' }}>{importResult.duplicateKeys}</div>
                    </div>
                    <div style={{ background: '#fee2e2', padding: 10, borderRadius: 8 }}>
                        <div style={{ color: '#991b1b', fontWeight: 'bold' }}>Lỗi</div>
                        <div style={{ fontSize: 24, color: '#b91c1c' }}>{importResult.invalidKeys}</div>
                    </div>
                </div>
                {importResult.errors?.length > 0 && (
                    <div style={{ marginTop: 15, background: '#fff1f2', padding: 10, borderRadius: 8, maxHeight: 150, overflowY: 'auto' }}>
                        <ul style={{ margin: 0, paddingLeft: 20, color: '#b91c1c', fontSize: 13 }}>
                            {importResult.errors.map((e, i) => <li key={i}>{e}</li>)}
                        </ul>
                    </div>
                )}
             </div>
             <div className="modal-footer">
                <button className="btn primary" onClick={closeImportResultModal}>Đóng</button>
             </div>
           </div>
         </div>
      )}
    </div>
  );
}
