import React, { useEffect, useMemo, useState, useCallback } from "react";
import { ProductApi } from "../../services/products";
import { ProductKeyApi } from "../../services/productKeys";
import { ProductAccountApi } from "../../services/productAccounts";
import { SupplierApi } from "../../services/suppliers";
import { ProductVariantsApi } from "../../services/productVariants";
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
  const [customerIssueCount, setCustomerIssueCount] = useState(0); // mock
  const [accountErrorCount, setAccountErrorCount] = useState(0);
  const [expiringSoonCount, setExpiringSoonCount] = useState(0);
  const [loadingLowStock, setLoadingLowStock] = useState(false);
  const [loadingCounters, setLoadingCounters] = useState(false);
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
  // Mock: customer reported issues list
  const customerIssues = useMemo(
    () => [
      {
        time: "2025-10-19 12:33",
        productName: "Adobe Photoshop",
        ref: "ABCD-8899-FF77",
        error: "Activation failed",
        orderCode: "KTTK-10277",
      },
      {
        time: "2025-10-19 11:10",
        productName: "M365 Family",
        ref: "Pool #12",
        error: "Vượt giới hạn",
        orderCode: "KTTK-10233",
      },
    ],
    []
  );
  // Mock: customer reported account issues list
  const customerAccountIssues = useMemo(
    () => [
      {
        time: "2025-10-19 13:05",
        productName: "Spotify Premium",
        account: "user01@example.com",
        error: "Khong dang nhap duoc",
        orderCode: "KTTK-10288",
      },
      {
        time: "2025-10-19 09:42",
        productName: "Netflix 4K",
        account: "acc.family02@gmail.com",
        error: "Het slot / bi kick",
        orderCode: "KTTK-10212",
      },
    ],
    []
  );
  // Load KPI counters
  const loadCounters = useCallback(async () => {
    setLoadingCounters(true);
    try {
      // Customer issues: mock
      setCustomerIssueCount(customerIssues.length);
      // Accounts error count
      try {
        const accData = await ProductAccountApi.list({
          status: "Error",
          pageNumber: 1,
          pageSize: 1,
        });
        const totalAccErrors = accData.totalCount || accData.total || 0;
        setAccountErrorCount(totalAccErrors);
      } catch (e) {
        setAccountErrorCount(0);
      }
      // Expiring soon (placeholder): backend not provided yet
      setExpiringSoonCount(0);
    } finally {
      setLoadingCounters(false);
    }
  }, [customerIssues.length]);
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
              <th>Sản phẩm</th>
              <th>Key/Pool</th>
              <th>Lỗi</th>
              <th>Đơn</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {customerIssues.map((row, idx) => (
              <tr key={idx}>
                <td>{row.time}</td>
                <td>{row.productName}</td>
                <td
                  style={{
                    fontFamily:
                      'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace',
                  }}
                >
                  {row.ref}
                </td>
                <td style={{ color: "#991b1b", fontWeight: 600 }}>
                  {row.error}
                </td>
                <td>{row.orderCode}</td>
                <td>
                  <div className="action-buttons">
                    <button className="btn">Xử lý</button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <small className="muted">
          Dữ liệu mock - sẽ kết nối API feedback/customer reports sau.
        </small>
      </section>

      <section className="card">
        <h2 style={{ margin: 0, marginBottom: 10 }}>
          Tài khoản báo lỗi từ khách
        </h2>
        <table className="table">
          <thead>
            <tr>
              <th>Thời gian</th>
              <th>Sản phẩm</th>
              <th>Tài khoản</th>
              <th>Lỗi</th>
              <th>Đơn</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {customerAccountIssues.map((row, idx) => (
              <tr key={idx}>
                <td>{row.time}</td>
                <td>{row.productName}</td>
                <td
                  style={{
                    fontFamily:
                      'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace',
                  }}
                >
                  {row.account}
                </td>
                <td style={{ color: "#991b1b", fontWeight: 600 }}>
                  {row.error}
                </td>
                <td>{row.orderCode}</td>
                <td>
                  <div className="action-buttons">
                    <button className="btn">Xử lý</button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <small className="muted">
          Dữ liệu mock - sẽ kết nối API feedback/customer reports sau.
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
