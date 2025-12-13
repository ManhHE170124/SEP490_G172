import React, { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate, useLocation, Link } from "react-router-dom";
import { SupplierApi } from "../../services/suppliers";
import { LicensePackageApi } from "../../services/licensePackages";
import { ProductApi } from "../../services/products";
import { ProductVariantsApi } from "../../services/productVariants";
import { ProductAccountApi } from "../../services/productAccounts";
import ToastContainer from "../../components/Toast/ToastContainer";
import ConfirmDialog from "../../components/ConfirmDialog/ConfirmDialog";
import CsvUploadModal from "../../components/Modal/CsvUploadModal";
import ViewKeysModal from "../../components/Modal/ViewKeysModal";
import ChunkedText from "../../components/ChunkedText";
import useToast from "../../hooks/useToast";
import { formatDate } from "../../utils/formatDate";
import "../admin/admin.css";

export default function SupplierDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const isNew = location.pathname.endsWith("/add") || !id || id === "add";
  const { toasts, showSuccess, showError, showWarning, removeToast } =
    useToast();

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formData, setFormData] = useState({
    name: "",
    contactEmail: "",
    contactPhone: "",
    licenseTerms: "",
    notes: "",
  });
  const [supplierInfo, setSupplierInfo] = useState(null);
  const [errors, setErrors] = useState({});
  const [confirmDialog, setConfirmDialog] = useState({
    isOpen: false,
    title: "",
    message: "",
    onConfirm: null,
    type: "warning",
  });

  // License Package states
  const [packages, setPackages] = useState([]);
  const [products, setProducts] = useState([]);
  const [variants, setVariants] = useState([]);
  const [loadingPackages, setLoadingPackages] = useState(false);
  const [loadingVariants, setLoadingVariants] = useState(false);
  const [packageForm, setPackageForm] = useState({
    productId: "",
    variantId: "",
    quantity: "",
    pricePerUnit: "",
    effectiveDate: "",
  });

  // Product search states
  const [productSearch, setProductSearch] = useState("");
  const [productPage, setProductPage] = useState(1);
  const [loadingProducts, setLoadingProducts] = useState(false);
  const [hasMoreProducts, setHasMoreProducts] = useState(true);
  const [showProductDropdown, setShowProductDropdown] = useState(false);
  const [selectedProduct, setSelectedProduct] = useState(null);

  // CSV upload modal states
  const [showUploadModal, setShowUploadModal] = useState(false);
  const [selectedPackageForImport, setSelectedPackageForImport] =
    useState(null);
  const [csvFile, setCsvFile] = useState(null);
  const [csvKeyType, setCsvKeyType] = useState("Individual");
  const [csvExpiryDate, setCsvExpiryDate] = useState("");
  const [uploading, setUploading] = useState(false);

  // View keys modal states
  const [showKeysModal, setShowKeysModal] = useState(false);
  const [selectedPackageKeys, setSelectedPackageKeys] = useState(null);
  const [loadingKeys, setLoadingKeys] = useState(false);

  // Product accounts states
  const [productAccounts, setProductAccounts] = useState([]);
  const [loadingProductAccounts, setLoadingProductAccounts] = useState(false);

  const loadSupplier = useCallback(async () => {
    if (!id || id === "add") {
      console.warn("Attempted to load supplier with invalid id:", id);
      return;
    }

    setLoading(true);
    try {
      const data = await SupplierApi.get(id);
      setSupplierInfo(data);
      setFormData({
        name: data.name || "",
        contactEmail: data.contactEmail || "",
        contactPhone: data.contactPhone || "",
        licenseTerms: data.licenseTerms || "",
        notes: data.notes || "",
      });
    } catch (err) {
      console.error("Failed to load supplier:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể tải thông tin nhà cung cấp";
      showError("Lỗi tải dữ liệu", errorMsg);
      navigate("/suppliers");
    } finally {
      setLoading(false);
    }
  }, [id, navigate, showError]);

  const loadLicensePackages = useCallback(async () => {
    if (!id || id === "add") return;

    setLoadingPackages(true);
    try {
      const data = await LicensePackageApi.list({
        supplierId: parseInt(id),
        pageNumber: 1,
        pageSize: 100,
      });
      setPackages(data.items || data.data || []);
    } catch (err) {
      console.error("Failed to load license packages:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể tải danh sách gói license";
      showError("Lỗi tải dữ liệu", errorMsg);
    } finally {
      setLoadingPackages(false);
    }
  }, [id, showError]);

  const loadProductAccounts = useCallback(async () => {
    if (!id || id === "add") return;

    setLoadingProductAccounts(true);
    try {
      const data = await ProductAccountApi.list({
        supplierId: parseInt(id),
        pageNumber: 1,
        pageSize: 100,
      });
      setProductAccounts(data.items || data.data || []);
    } catch (err) {
      console.error("Failed to load product accounts:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể tải danh sách tài khoản sản phẩm";
      showError("Lỗi tải dữ liệu", errorMsg);
    } finally {
      setLoadingProductAccounts(false);
    }
  }, [id, showError]);

  const loadProducts = useCallback(
    async (page = 1, search = "", append = false) => {
      setLoadingProducts(true);
      try {
        const data = await ProductApi.list({
          pageNumber: page,
          pageSize: 20,
          searchTerm: search || undefined,
          productTypes: ["PERSONAL_KEY", "SHARED_KEY"],
        });

        const newProducts = data.items || data.data || [];

        if (append) {
          setProducts((prev) => [...prev, ...newProducts]);
        } else {
          setProducts(newProducts);
        }

        // Check if there are more products
        const total = data.total || data.totalCount || 0;
        setHasMoreProducts(page * 20 < total);
      } catch (err) {
        console.error("Failed to load products:", err);
      } finally {
        setLoadingProducts(false);
      }
    },
    []
  );

  // Load supplier, license packages, and product accounts on mount
  useEffect(() => {
    if (!isNew) {
      loadSupplier();
      loadLicensePackages();
      loadProductAccounts();
    }
  }, [id, isNew, loadSupplier, loadLicensePackages, loadProductAccounts]);

  // Load initial products when dropdown opens
  useEffect(() => {
    if (showProductDropdown && products.length === 0) {
      loadProducts(1, productSearch);
    }
  }, [showProductDropdown, productSearch, products.length, loadProducts]);

  // Search products with debounce
  useEffect(() => {
    if (!showProductDropdown) return;

    const timer = setTimeout(() => {
      setProductPage(1);
      loadProducts(1, productSearch, false);
    }, 300);

    return () => clearTimeout(timer);
  }, [productSearch, showProductDropdown, loadProducts]);

  const validateForm = () => {
    const newErrors = {};
    const email = (formData.contactEmail || "").trim();
    const phone = (formData.contactPhone || "").trim();

    if (!formData.name.trim()) {
      newErrors.name = "Tên nhà cung cấp là bắt buộc";
    } else if (formData.name.length > 100) {
      newErrors.name = "Tên không được vượt quá 100 ký tự";
    }

    if (!email) {
      newErrors.contactEmail = "Email liên hệ là bắt buộc";
    } else {
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
        newErrors.contactEmail = "Email không hợp lệ";
      } else if (email.length > 254) {
        newErrors.contactEmail = "Email không được vượt quá 254 ký tự";
      }
    }

    if (!phone) {
      newErrors.contactPhone = "Số điện thoại là bắt buộc";
    } else if (phone.length > 32) {
      newErrors.contactPhone = "Số điện thoại không được vượt quá 32 ký tự";
    }

    if (formData.licenseTerms && formData.licenseTerms.length < 10) {
      newErrors.licenseTerms = "Điều khoản giấy phép phải có ít nhất 10 ký tự";
    }

    if (formData.licenseTerms && formData.licenseTerms.length > 500) {
      newErrors.licenseTerms =
        "Điều khoản giấy phép không được vượt quá 500 ký tự";
    }

    if (formData.notes && formData.notes.length > 1000) {
      newErrors.notes = "Ghi chú không được vượt quá 1000 ký tự";
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!validateForm()) {
      showWarning("Dữ liệu không hợp lệ", "Vui lòng kiểm tra lại thông tin");
      return;
    }

    setSaving(true);
    try {
      if (isNew) {
        // Create new supplier
        await SupplierApi.create(formData);
        showSuccess("Thành công", "Nhà cung cấp đã được tạo thành công");
        navigate("/suppliers");
      } else {
        // Update existing supplier
        await SupplierApi.update(id, {
          supplierId: parseInt(id),
          ...formData,
        });
        showSuccess("Thành công", "Nhà cung cấp đã được cập nhật thành công");
        loadSupplier();
      }
    } catch (err) {
      console.error("Failed to save supplier:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể lưu nhà cung cấp";
      showError("Lỗi lưu dữ liệu", errorMsg);
    } finally {
      setSaving(false);
    }
  };

  const handleToggleStatus = async () => {
    const isActive = supplierInfo?.status === "Active";
    const action = isActive ? "tạm dừng" : "kích hoạt lại";

    setConfirmDialog({
      isOpen: true,
      title: `Xác nhận ${action} nhà cung cấp`,
      message: `Bạn có chắc muốn ${action} nhà cung cấp "${formData.name}"?`,
      type: isActive ? "danger" : "warning",
      onConfirm: async () => {
        setConfirmDialog({ ...confirmDialog, isOpen: false });

        try {
          await SupplierApi.toggleStatus(id);

          showSuccess(
            "Thành công",
            `Nhà cung cấp đã được ${action} thành công`
          );
          loadSupplier();
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

  const handleChange = (field, value) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
    // Clear error when user types
    if (errors[field]) {
      setErrors((prev) => ({ ...prev, [field]: undefined }));
    }
  };

  const handlePackageFormChange = (field, value) => {
    setPackageForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleProductSelect = (product) => {
    setSelectedProduct(product);
    setPackageForm((prev) => ({
      ...prev,
      productId: product.productId,
      variantId: "",
    }));
    setShowProductDropdown(false);
    setProductSearch("");
  };

  const handleProductSearchChange = (e) => {
    setProductSearch(e.target.value);
    if (!showProductDropdown) {
      setShowProductDropdown(true);
    }
  };

  const handleProductDropdownScroll = (e) => {
    const bottom =
      e.target.scrollHeight - e.target.scrollTop === e.target.clientHeight;
    if (bottom && hasMoreProducts && !loadingProducts) {
      const nextPage = productPage + 1;
      setProductPage(nextPage);
      loadProducts(nextPage, productSearch, true);
    }
  };

  const loadVariantsForProduct = useCallback(async (productId) => {
    if (!productId) {
      setVariants([]);
      return;
    }
    setLoadingVariants(true);
    try {
      const data = await ProductVariantsApi.list(productId, {
        pageNumber: 1,
        pageSize: 100,
      });
      setVariants(data.items || data.data || []);
    } catch (err) {
      console.error("Failed to load variants:", err);
      setVariants([]);
    } finally {
      setLoadingVariants(false);
    }
  }, []);

  useEffect(() => {
    if (packageForm.productId) {
      loadVariantsForProduct(packageForm.productId);
    } else {
      setVariants([]);
    }
  }, [packageForm.productId, loadVariantsForProduct]);

  const handleAddPackage = async (e) => {
    e.preventDefault();

    if (
      !packageForm.productId ||
      !packageForm.variantId ||
      !packageForm.quantity ||
      !packageForm.pricePerUnit
    ) {
      showWarning(
        "Dữ liệu không đầy đủ",
        "Vui lòng điền đầy đủ thông tin gói license"
      );
      return;
    }

    try {
      await LicensePackageApi.create({
        supplierId: parseInt(id),
        productId: packageForm.variantId, // backend expects variant id
        quantity: parseInt(packageForm.quantity),
        pricePerUnit: parseFloat(packageForm.pricePerUnit),
        effectiveDate: packageForm.effectiveDate || null,
      });

      showSuccess("Thành công", "Gói license đã được thêm thành công");
      setPackageForm({
        productId: "",
        variantId: "",
        quantity: "",
        pricePerUnit: "",
        effectiveDate: "",
      });
      setSelectedProduct(null);
      setVariants([]);
      setProductSearch("");
      loadLicensePackages();
    } catch (err) {
      console.error("Failed to add license package:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể thêm gói license";
      showError("Lỗi thêm gói", errorMsg);
    }
  };

  const handleImportToStock = (pkg) => {
    setSelectedPackageForImport(pkg);
    setShowUploadModal(true);
    setCsvFile(null);
    setCsvKeyType("Individual");
    setCsvExpiryDate("");
  };

  const handleCsvFileChange = (e) => {
    const file = e.target.files?.[0];
    if (file) {
      if (!file.name.endsWith(".csv")) {
        showError("Lỗi file", "Vui lòng chọn file CSV");
        return;
      }
      setCsvFile(file);
    }
  };

  const handleUploadCsv = async () => {
    if (!csvFile || !selectedPackageForImport) {
      showWarning("Chưa chọn file", "Vui lòng chọn file CSV để upload");
      return;
    }

    const variantId =
      selectedPackageForImport.variantId ||
      selectedPackageForImport.productVariantId ||
      selectedPackageForImport.productId;

    if (!variantId) {
      showError(
        "Thiếu thông tin",
        "Không tìm thấy biến thể cho gói license này. Vui lòng tải lại dữ liệu."
      );
      return;
    }

    setUploading(true);
    try {
      const formData = new FormData();
      formData.append("file", csvFile);
      formData.append("packageId", selectedPackageForImport.packageId);
      formData.append("variantId", variantId);
      formData.append("supplierId", id);

      // Add keyType
      formData.append("keyType", csvKeyType);

      // Add expiryDate if provided
      if (csvExpiryDate) {
        formData.append("expiryDate", csvExpiryDate);
      }

      await LicensePackageApi.uploadCsv(formData);

      showSuccess(
        "Thành công",
        "Đã nhập license từ file CSV vào kho thành công"
      );
      setShowUploadModal(false);
      setCsvFile(null);
      setCsvKeyType("Individual");
      setCsvExpiryDate("");
      setSelectedPackageForImport(null);
      loadLicensePackages();
    } catch (err) {
      console.error("Failed to upload CSV:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể upload file CSV";
      showError("Lỗi upload", errorMsg);
    } finally {
      setUploading(false);
    }
  };

  const closeUploadModal = () => {
    setShowUploadModal(false);
    setCsvFile(null);
    setCsvKeyType("Individual");
    setCsvExpiryDate("");
    setSelectedPackageForImport(null);
  };

  const handleViewKeys = async (pkg) => {
    setLoadingKeys(true);
    setShowKeysModal(true);
    try {
      const data = await LicensePackageApi.getKeysByPackage(
        pkg.packageId,
        parseInt(id)
      );
      setSelectedPackageKeys(data);
    } catch (err) {
      console.error("Failed to load keys:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể tải danh sách key";
      showError("Lỗi tải dữ liệu", errorMsg);
      setShowKeysModal(false);
    } finally {
      setLoadingKeys(false);
    }
  };

  const closeKeysModal = () => {
    setShowKeysModal(false);
    setSelectedPackageKeys(null);
  };

  if (loading) {
    return (
      <div className="page">
        <div className="card">
          <p style={{ textAlign: "center", padding: 40 }}>Đang tải...</p>
        </div>
      </div>
    );
  }

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

      <section className="card ">
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 16,
          }}
        >
          <h1 style={{ margin: 0 }}>
            {isNew ? (
              "Thêm nhà cung cấp mới"
            ) : (
              <>
                Chi tiết nhà cung cấp —{" "}
                <ChunkedText
                  value={formData.name}
                  fallback="(Không có tên)"
                  chunkSize={32}
                  className="chunk-text--wide chunk-text--block"
                />
              </>
            )}
          </h1>
          <Link className="btn" to="/suppliers">
            Quay lại
          </Link>
        </div>

        <form onSubmit={handleSubmit}>
          <div
            className="grid"
            style={{
              gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
              gap: 12,
            }}
          >
            <div className="form-row">
              <label>
                Tên <span style={{ color: "red" }}>*</span>
              </label>
              <div>
                <input
                  className="input"
                  value={formData.name}
                  onChange={(e) => handleChange("name", e.target.value)}
                  placeholder="Nhập tên nhà cung cấp"
                />
                {errors.name && (
                  <small style={{ color: "red" }}>{errors.name}</small>
                )}
              </div>
            </div>

            <div className="form-row">
              <label>
                Email liên hệ <span style={{ color: "red" }}>*</span>
              </label>
              <div>
                <input
                  className="input"
                  type="email"
                  value={formData.contactEmail}
                  onChange={(e) => handleChange("contactEmail", e.target.value)}
                  placeholder="email@example.com"
                  required
                />
                {errors.contactEmail && (
                  <small style={{ color: "red" }}>{errors.contactEmail}</small>
                )}
              </div>
            </div>

            <div className="form-row">
              <label>
                Số điện thoại <span style={{ color: "red" }}>*</span>
              </label>
              <div>
                <input
                  className="input"
                  type="tel"
                  value={formData.contactPhone}
                  onChange={(e) => handleChange("contactPhone", e.target.value)}
                  placeholder="0123456789"
                  required
                />
                {errors.contactPhone && (
                  <small style={{ color: "red" }}>{errors.contactPhone}</small>
                )}
              </div>
            </div>

            <div className="form-row" style={{ gridColumn: "1 / -1" }}>
              <label>Điều khoản giấy phép</label>
              <div>
                <input
                  className="input"
                  value={formData.licenseTerms}
                  onChange={(e) => handleChange("licenseTerms", e.target.value)}
                  placeholder="VD: Net 7, đổi trả 7 ngày"
                />
                {errors.licenseTerms && (
                  <small style={{ color: "red" }}>{errors.licenseTerms}</small>
                )}
              </div>
            </div>

            <div className="form-row" style={{ gridColumn: "1 / -1" }}>
              <label>Ghi chú</label>
              <div>
                <textarea
                  className="textarea"
                  value={formData.notes}
                  onChange={(e) => handleChange("notes", e.target.value)}
                  placeholder="Ghi chú thêm về nhà cung cấp..."
                  rows={4}
                />
                {errors.notes && (
                  <small style={{ color: "red" }}>{errors.notes}</small>
                )}
              </div>
            </div>
          </div>

          {!isNew && supplierInfo && (
            <div
              style={{
                marginTop: 16,
                padding: 12,
                background: "#f8fafc",
                borderRadius: 8,
              }}
            >
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
                  gap: 12,
                }}
              >
                <div>
                  <small className="muted">Tên hiển thị:</small>
                  <ChunkedText
                    value={formData.name}
                    fallback="(Không có tên)"
                    chunkSize={32}
                    className="chunk-text--wide chunk-text--block"
                  />
                </div>
                <div>
                  <small className="muted">Email liên hệ:</small>
                  <ChunkedText
                    value={formData.contactEmail}
                    fallback="Chưa cập nhật"
                    chunkSize={32}
                    className="chunk-text--wide chunk-text--block"
                  />
                </div>
                <div>
                  <small className="muted">Số điện thoại:</small>
                  <ChunkedText
                    value={formData.contactPhone}
                    fallback="Chưa cập nhật"
                    chunkSize={24}
                    className="chunk-text--wide chunk-text--block"
                  />
                </div>
                <div>
                  <small className="muted">Trạng thái:</small>
                  <div>
                    {supplierInfo.status === "Active" ? (
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
                    ) : (
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
                    )}
                  </div>
                </div>
                <div>
                  <small className="muted">Số sản phẩm đang cung cấp:</small>
                  <div>{supplierInfo.activeProductCount || 0}</div>
                </div>
                <div>
                  <small className="muted">Tổng số product key:</small>
                  <div>{supplierInfo.totalProductKeyCount || 0}</div>
                </div>
                <div>
                  <small className="muted">Ngày tạo:</small>
                  <div>{formatDate(supplierInfo.createdAt)}</div>
                </div>
              </div>
            </div>
          )}

          <div className="row" style={{ marginTop: 16 }}>
            <button type="submit" className="btn primary" disabled={saving}>
              {saving ? "Đang lưu..." : "Lưu"}
            </button>
            <button
              type="button"
              className="btn"
              onClick={() => navigate("/suppliers")}
            >
              Hủy
            </button>
            {!isNew && supplierInfo && (
              <button
                type="button"
                className={
                  supplierInfo?.status === "Active"
                    ? "btn secondary"
                    : "btn success"
                }
                onClick={handleToggleStatus}
                style={{ marginLeft: "auto" }}
              >
                {supplierInfo.status === "Active"
                  ? "Tạm dừng"
                  : "Kích hoạt lại"}
              </button>
            )}
          </div>
        </form>
      </section>

      {/* License Packages Section - Only shown for existing suppliers */}
      {!isNew && (
        <section className="card " style={{ marginTop: 14 }}>
          <h2 style={{ margin: "0 0 12px" }}>Thông tin gói mua</h2>

          {supplierInfo?.status !== "Active" && (
            <div
              style={{
                marginBottom: 16,
                padding: 12,
                background: "#fef3c7",
                borderRadius: 8,
                color: "#92400e",
              }}
            >
              Nhà cung cấp đang tạm dừng. Không thể thêm gói hoặc nhập kho.
            </div>
          )}

          {/* Add Package Form */}
          <div
            style={{
              display: "flex",
              gap: 12,
              flexWrap: "wrap",
              alignItems: "end",
              marginBottom: 16,
            }}
          >
            <div className="group" style={{ minWidth: 200, position: "relative" }}>
              <span>Sản phẩm</span>
              <input
                className="input"
                type="text"
                placeholder="Tìm kiếm sản phẩm..."
                value={
                  selectedProduct
                    ? selectedProduct.productName
                    : productSearch
                }
                onChange={handleProductSearchChange}
                onFocus={() => setShowProductDropdown(true)}
                onBlur={() => {
                  // Delay to allow click on dropdown item
                  setTimeout(() => setShowProductDropdown(false), 200);
                }}
                disabled={supplierInfo?.status !== "Active"}
              />
              {showProductDropdown && (
                <div
                  style={{
                    position: "absolute",
                    top: "100%",
                    left: 0,
                    right: 0,
                    maxHeight: "300px",
                    overflowY: "auto",
                    background: "white",
                    border: "1px solid #ddd",
                    borderRadius: "4px",
                    boxShadow: "0 2px 8px rgba(0,0,0,0.15)",
                    zIndex: 1000,
                    marginTop: "4px",
                  }}
                  onScroll={handleProductDropdownScroll}
                >
                  {loadingProducts && products.length === 0 ? (
                    <div
                      style={{
                        padding: "12px",
                        textAlign: "center",
                        color: "#666",
                      }}
                    >
                      Đang tải...
                    </div>
                  ) : products.length === 0 ? (
                    <div
                      style={{
                        padding: "12px",
                        textAlign: "center",
                        color: "#666",
                      }}
                    >
                      Không tìm thấy sản phẩm
                    </div>
                  ) : (
                    <>
                      {products.map((product) => (
                        <div
                          key={product.productId}
                          style={{
                            padding: "10px 12px",
                            cursor: "pointer",
                            borderBottom: "1px solid #f0f0f0",
                            transition: "background-color 0.2s",
                          }}
                          onMouseDown={(e) => {
                            e.preventDefault();
                            handleProductSelect(product);
                          }}
                          onMouseEnter={(e) => {
                            e.currentTarget.style.backgroundColor = "#f5f5f5";
                          }}
                          onMouseLeave={(e) => {
                            e.currentTarget.style.backgroundColor = "white";
                          }}
                        >
                          {product.productName}
                        </div>
                      ))}
                      {loadingProducts && (
                        <div
                          style={{
                            padding: "12px",
                            textAlign: "center",
                            color: "#666",
                          }}
                        >
                          Đang tải thêm...
                        </div>
                      )}
                      {!loadingProducts && hasMoreProducts && (
                        <div
                          style={{
                            padding: "12px",
                            textAlign: "center",
                            color: "#999",
                            fontSize: "12px",
                          }}
                        >
                          Cuộn xuống để tải thêm
                        </div>
                      )}
                    </>
                  )}
                </div>
              )}
            </div>
            <div className="group" style={{ minWidth: 180 }}>
              <span>Biến thể</span>
              <select
                className="input"
                value={packageForm.variantId}
                onChange={(e) =>
                  handlePackageFormChange("variantId", e.target.value)
                }
                disabled={
                  supplierInfo?.status !== "Active" ||
                  !packageForm.productId ||
                  loadingVariants
                }
              >
                <option value="">
                  {loadingVariants
                    ? "Đang tải..."
                    : !packageForm.productId
                    ? "Chọn sản phẩm trước"
                    : "Chọn biến thể"}
                </option>
                {variants.map((variant) => (
                  <option key={variant.variantId} value={variant.variantId}>
                    {variant.title}
                  </option>
                ))}
              </select>
            </div>
            <div className="group" style={{ minWidth: 150 }}>
              <span>Số lượng gói</span>
              <input
                className="input"
                type="number"
                placeholder="VD: 100"
                value={packageForm.quantity}
                onChange={(e) =>
                  handlePackageFormChange("quantity", e.target.value)
                }
                disabled={supplierInfo?.status !== "Active"}
              />
            </div>
            <div className="group" style={{ minWidth: 150 }}>
              <span>Giá/gói</span>
              <input
                className="input"
                type="number"
                placeholder="VD: 120000"
                value={packageForm.pricePerUnit}
                onChange={(e) =>
                  handlePackageFormChange("pricePerUnit", e.target.value)
                }
                disabled={supplierInfo?.status !== "Active"}
              />
            </div>
            <button
              className="btn primary"
              onClick={handleAddPackage}
              disabled={supplierInfo?.status !== "Active"}
            >
              Thêm gói
            </button>
          </div>

          {/* Packages Table */}
          <table className="table" style={{ marginTop: 10 }}>
            <thead>
              <tr>
                <th>Sản phẩm</th>
                <th>Số lượng</th>
                <th>Giá/gói</th>
                <th>Đã nhập kho</th>
                <th>Còn lại</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {loadingPackages && (
                <tr>
                  <td colSpan="7" style={{ textAlign: "center", padding: 20 }}>
                    Đang tải...
                  </td>
                </tr>
              )}
              {!loadingPackages && packages.length === 0 && (
                <tr>
                  <td colSpan="7" style={{ textAlign: "center", padding: 20 }}>
                    Chưa có gói license nào
                  </td>
                </tr>
              )}
              {!loadingPackages &&
                packages.map((pkg) => (
                  <tr key={pkg.packageId}>
                    <td>{pkg.productName}</td>
                    <td>{pkg.quantity}</td>
                    <td>{pkg.pricePerUnit.toLocaleString("vi-VN")}</td>
                    <td>{pkg.importedToStock}</td>
                    <td>{pkg.remainingQuantity}</td>
                    <td>
                      <div className="action-buttons">
                        {pkg.remainingQuantity > 0 && (
                          <button
                            className="btn"
                            onClick={() => handleImportToStock(pkg)}
                            disabled={supplierInfo?.status !== "Active"}
                          >
                            Nhập kho
                          </button>
                        )}
                        {pkg.importedToStock > 0 && (
                          <button
                            className="btn"
                            onClick={() => handleViewKeys(pkg)}
                          >
                            Chi tiết
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
          <small className="muted" style={{ display: "block", marginTop: 8 }}>
            "Nhập kho" sẽ tạo lô key mới hoặc bổ sung seat cho pool (nếu là dùng
            chung).
          </small>
        </section>
      )}

      {/* Product Accounts Section - Only shown for existing suppliers */}
      {!isNew && (
        <section className="card " style={{ marginTop: 14 }}>
          <h2 style={{ margin: "0 0 12px" }}>Tài khoản sản phẩm</h2>

          {loadingProductAccounts ? (
            <div style={{ textAlign: "center", padding: 20 }}>Đang tải...</div>
          ) : productAccounts.length === 0 ? (
            <div style={{ textAlign: "center", padding: 20, color: "#6b7280" }}>
              Chưa có tài khoản sản phẩm nào từ nhà cung cấp này
            </div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Sản phẩm</th>
                    <th>Biến thể</th>
                    <th>Email</th>
                    <th>Username</th>
                    <th>Số người dùng</th>
                    <th>Trạng thái</th>
                    <th>Ngày hết hạn</th>
                    <th>Giá nhập</th>
                  </tr>
                </thead>
                <tbody>
                  {productAccounts.map((account) => (
                    <tr key={account.productAccountId}>
                      <td>{account.productName || "-"}</td>
                      <td>{account.variantTitle || "-"}</td>
                      <td>
                        <ChunkedText
                          value={account.accountEmail}
                          fallback="-"
                          chunkSize={24}
                          className="chunk-text--wide"
                        />
                      </td>
                      <td>
                        {account.accountUsername ? (
                          <ChunkedText
                            value={account.accountUsername}
                            fallback="-"
                            chunkSize={20}
                            className="chunk-text--wide"
                          />
                        ) : (
                          "-"
                        )}
                      </td>
                      <td>
                        {account.currentUsers}/{account.maxUsers}
                      </td>
                      <td>
                        <span
                          style={{
                            padding: "2px 8px",
                            borderRadius: "4px",
                            fontSize: "12px",
                            background:
                              account.status === "Active"
                                ? "#d1fae5"
                                : account.status === "Expired"
                                ? "#fee2e2"
                                : "#e5e7eb",
                            color:
                              account.status === "Active"
                                ? "#065f46"
                                : account.status === "Expired"
                                ? "#991b1b"
                                : "#374151",
                          }}
                        >
                          {account.status === "Active"
                            ? "Hoạt động"
                            : account.status === "Expired"
                            ? "Hết hạn"
                            : account.status === "Full"
                            ? "Đầy"
                            : account.status === "Error"
                            ? "Lỗi"
                            : account.status}
                        </span>
                      </td>
                      <td>
                        {account.expiryDate
                          ? new Date(account.expiryDate).toLocaleDateString(
                              "vi-VN"
                            )
                          : "-"}
                      </td>
                      <td>
                        {account.cogsPrice
                          ? account.cogsPrice.toLocaleString("vi-VN") + " đ"
                          : "-"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <small className="muted" style={{ display: "block", marginTop: 8 }}>
            Tổng số: {productAccounts.length} tài khoản
          </small>
        </section>
      )}

      {/* CSV Upload Modal */}
      <CsvUploadModal
        isOpen={showUploadModal}
        onClose={closeUploadModal}
        selectedPackage={selectedPackageForImport}
        csvFile={csvFile}
        uploading={uploading}
        onFileChange={handleCsvFileChange}
        onUpload={handleUploadCsv}
        keyType={csvKeyType}
        onKeyTypeChange={setCsvKeyType}
        expiryDate={csvExpiryDate}
        onExpiryDateChange={setCsvExpiryDate}
      />

      {/* View Keys Modal */}
      <ViewKeysModal
        isOpen={showKeysModal}
        onClose={closeKeysModal}
        packageKeys={selectedPackageKeys}
        loading={loadingKeys}
      />
    </div>
  );
}
