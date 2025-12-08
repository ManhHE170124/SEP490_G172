import React, { useState, useEffect, useCallback, useMemo } from "react";
import { useParams, useNavigate, useLocation, Link } from "react-router-dom";
import { ProductKeyApi } from "../../services/productKeys";
import { ProductApi } from "../../services/products";
import { ProductVariantsApi } from "../../services/productVariants";
import { SupplierApi } from "../../services/suppliers";
import ToastContainer from "../../components/Toast/ToastContainer";
import ConfirmDialog from "../../components/ConfirmDialog/ConfirmDialog";
import { orderApi } from "../../services/orderApi";
import useToast from "../../hooks/useToast";
import { usePermission } from "../../hooks/usePermission";
import PermissionGuard from "../../components/PermissionGuard";
import formatDateTime from "../../utils/formatDatetime";
import { getStatusLabel } from "../../utils/productKeyHepler";
import "../admin/admin.css";

export default function KeyDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const isNew = location.pathname.endsWith("/add") || !id || id === "add";
  const { toasts, showSuccess, showError, showWarning, removeToast } =
    useToast();
  const { hasPermission: hasCreatePermission } = usePermission("WAREHOUSE_MANAGER", "CREATE");
  const { hasPermission: hasEditPermission } = usePermission("WAREHOUSE_MANAGER", "EDIT");

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [keyInfo, setKeyInfo] = useState(null);
  const [buyerInfo, setBuyerInfo] = useState(null);
  const [products, setProducts] = useState([]);
  const [variants, setVariants] = useState([]);
  const [loadingVariants, setLoadingVariants] = useState(false);
  const [suppliers, setSuppliers] = useState([]);
  const [formData, setFormData] = useState({
    productId: "",
    variantId: "",
    supplierId: "",
    keyString: "",
    type: "Individual",
    status: "Available",
    cogsPrice: "",
    expiryDate: "",
    notes: "",
  });
  const [errors, setErrors] = useState({});
  const [confirmDialog, setConfirmDialog] = useState({
    isOpen: false,
    title: "",
    message: "",
    onConfirm: null,
    type: "warning",
  });

  const minExpiryDate = useMemo(
    () => new Date().toISOString().split("T")[0],
    []
  );

  const loadProductKey = useCallback(async () => {
    if (!id || id === "add") return;

    setLoading(true);
    try {
      const data = await ProductKeyApi.get(id);
      setKeyInfo(data);
      setFormData({
        productId: data.productId,
        variantId: data.variantId || "",
        supplierId: data.supplierId,
        keyString: data.keyString,
        type: data.type,
        status: data.status,
        cogsPrice:
          data.cogsPrice !== undefined && data.cogsPrice !== null
            ? data.cogsPrice.toString()
            : "",
        expiryDate: data.expiryDate
          ? new Date(data.expiryDate).toISOString().split("T")[0]
          : "",
        notes: data.notes || "",
      });
    } catch (err) {
      console.error("Failed to load key:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể tải thông tin key";
      showError("Lỗi tải dữ liệu", errorMsg);
      navigate("/keys");
    } finally {
      setLoading(false);
    }
  }, [id, navigate, showError]);

  const loadProducts = useCallback(async () => {
    try {
      const data = await ProductApi.list({
        pageNumber: 1,
        pageSize: 100,
        type: ["PERSONAL_KEY", "SHARED_KEY"], // Filter for key types only
      });
      setProducts(data.items || data.data || []);
    } catch (err) {
      console.error("Failed to load products:", err);
    }
  }, []);

  const loadSuppliers = useCallback(async () => {
    try {
      const data = await SupplierApi.list({
        pageNumber: 1,
        pageSize: 100,
        status: "Active",
      });
      setSuppliers(data.items || data.data || []);
    } catch (err) {
      console.error("Failed to load suppliers:", err);
    }
  }, []);

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
      setVariants(data.items || []);
    } catch (err) {
      console.error("Failed to load variants:", err);
      setVariants([]);
    } finally {
      setLoadingVariants(false);
    }
  }, []);

  const loadBuyerInfo = useCallback(async (orderId) => {
    if (!orderId) return;
    try {
      const order = await orderApi.get(orderId);
      if (order) {
        setBuyerInfo({
          name: order.userFullName || order.userName || "Unknown",
          email: order.userEmail || "Unknown",
          orderId: order.orderId,
        });
      }
    } catch (err) {
      console.error("Failed to load buyer info:", err);
    }
  }, []);

  useEffect(() => {
    loadProducts();
    loadSuppliers();
    if (!isNew) {
      loadProductKey();
    }
  }, [isNew, loadProductKey, loadProducts, loadSuppliers]);

  useEffect(() => {
    if (keyInfo?.assignedToOrderId) {
      loadBuyerInfo(keyInfo.assignedToOrderId);
    }
  }, [keyInfo, loadBuyerInfo]);

  // Load variants when product changes
  useEffect(() => {
    if (formData.productId) {
      loadVariantsForProduct(formData.productId);
    }
  }, [formData.productId, loadVariantsForProduct]);

  const validateForm = () => {
    const newErrors = {};

    if (!formData.productId) {
      newErrors.productId = "Sản phẩm là bắt buộc";
    }

    if (!formData.variantId) {
      newErrors.variantId = "Biến thể sản phẩm là bắt buộc";
    }

    if (!formData.supplierId) {
      newErrors.supplierId = "Nhà cung cấp là bắt buộc";
    }

    if (!formData.keyString.trim()) {
      newErrors.keyString = "License key là bắt buộc";
    } else if (formData.keyString.length > 500) {
      newErrors.keyString = "License key không được vượt quá 500 ký tự";
    }

    if (formData.notes && formData.notes.length > 1000) {
      newErrors.notes = "Ghi chú không được vượt quá 1000 ký tự";
    }

    const parsedCogs = parseFloat(formData.cogsPrice);
    if (isNew) {
      if (formData.cogsPrice === "" || Number.isNaN(parsedCogs)) {
        newErrors.cogsPrice = "Giá vốn (COGS) là bắt buộc";
      } else if (parsedCogs < 0) {
        newErrors.cogsPrice = "Giá vốn không được âm";
      }
    } else if (
      formData.cogsPrice &&
      (Number.isNaN(parsedCogs) || parsedCogs < 0)
    ) {
      newErrors.cogsPrice = "Giá vốn không được âm";
    }

    if (isNew && formData.expiryDate) {
      const [year, month, day] = formData.expiryDate
        .split("-")
        .map((value) => parseInt(value, 10));
      const selectedDate = new Date(year, month - 1, day);
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      if (selectedDate < today) {
        newErrors.expiryDate = "Ngày hết hạn không được trong quá khứ";
      }
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };
  const handleSubmit = async (e) => {
    e.preventDefault();

    if (isNew && !hasCreatePermission) {
      showError("Không có quyền", "Bạn không có quyền tạo key");
      return;
    }
    if (!isNew && !hasEditPermission) {
      showError("Không có quyền", "Bạn không có quyền sửa key");
      return;
    }

    if (!validateForm()) {
      showWarning("Dữ liệu không hợp lệ", "Vui lòng kiểm tra lại thông tin");
      return;
    }

    setSaving(true);
    try {
      if (isNew) {
        const cogsPriceValue = parseFloat(formData.cogsPrice);
        await ProductKeyApi.create({
          ...formData,
          expiryDate: formData.expiryDate || null,
          cogsPrice: Number.isNaN(cogsPriceValue) ? null : cogsPriceValue,
        });
        showSuccess("Thành công", "Key đã được tạo thành công");
        navigate("/keys");
      } else {
        // Only allow updating notes for existing keys
        await ProductKeyApi.update(id, {
          keyId: id,
          notes: formData.notes,
        });
        showSuccess("Thành công", "Ghi chú đã được cập nhật thành công");
        loadProductKey();
      }
    } catch (err) {
      console.error("Failed to save key:", err);
      const errorMsg =
        err.response?.data?.message || err.message || "Không thể lưu key";
      showError("Lỗi lưu dữ liệu", errorMsg);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = () => {
    setConfirmDialog({
      isOpen: true,
      title: "Xác nhận xóa key",
      message:
        "Bạn có chắc muốn xóa key này? Hành động này không thể hoàn tác.",
      type: "danger",
      onConfirm: async () => {
        setConfirmDialog({ ...confirmDialog, isOpen: false });
        try {
          await ProductKeyApi.delete(id);
          showSuccess("Thành công", "Key đã được xóa thành công");
          navigate("/keys");
        } catch (err) {
          console.error("Failed to delete key:", err);
          const errorMsg =
            err.response?.data?.message || err.message || "Không thể xóa key";
          showError("Lỗi xóa key", errorMsg);
        }
      },
    });
  };

  const handleChange = (field, value) => {
    setFormData((prev) => {
      // Reset variant when product changes
      if (field === "productId" && value !== prev.productId) {
        return { ...prev, [field]: value, variantId: "" };
      }
      return { ...prev, [field]: value };
    });
    if (errors[field]) {
      setErrors((prev) => ({ ...prev, [field]: undefined }));
    }
  };

  const closeConfirmDialog = () => {
    setConfirmDialog({ ...confirmDialog, isOpen: false });
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

      <section className="card">
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 16,
          }}
        >
          <h1 style={{ margin: 0 }}>
            {isNew ? "Tạo key mới" : "Chi tiết Product Key"}
          </h1>
          <Link className="btn" to="/keys">
            ← Quay lại
          </Link>
        </div>

        <form onSubmit={handleSubmit}>
          {!isNew && (
            <div
              style={{
                marginBottom: 16,
                padding: 12,
                background:
                  formData.status === "Expired" ? "#fee2e2" : "#fef3c7",
                borderRadius: 8,
                color: formData.status === "Expired" ? "#991b1b" : "#92400e",
              }}
            >
              {formData.status === "Expired"
                ? "Key đã hết hạn. Chỉ có thể cập nhật ghi chú."
                : "Chỉ có thể cập nhật ghi chú cho key đã tồn tại."}
            </div>
          )}

          <div
            className="grid"
            style={{
              gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
              gap: 12,
            }}
          >
            <div className="form-row">
              <label>
                Sản phẩm <span style={{ color: "red" }}>*</span>
              </label>
              <div>
                <select
                  className="input"
                  value={formData.productId}
                  onChange={(e) => handleChange("productId", e.target.value)}
                  disabled={!isNew}
                >
                  <option value="">Chọn sản phẩm</option>
                  {products.map((product) => (
                    <option key={product.productId} value={product.productId}>
                      {product.productName}
                    </option>
                  ))}
                </select>
                {errors.productId && (
                  <small style={{ color: "red" }}>{errors.productId}</small>
                )}
              </div>
            </div>

            <div className="form-row">
              <label>
                Biến thể <span style={{ color: "red" }}>*</span>
              </label>
              <div>
                <select
                  className="input"
                  value={formData.variantId}
                  onChange={(e) => handleChange("variantId", e.target.value)}
                  disabled={!isNew || !formData.productId || loadingVariants}
                >
                  <option value="">
                    {loadingVariants
                      ? "Đang tải..."
                      : !formData.productId
                      ? "Chọn sản phẩm trước"
                      : "Chọn biến thể"}
                  </option>
                  {variants.map((variant) => (
                    <option key={variant.variantId} value={variant.variantId}>
                      {variant.title}
                    </option>
                  ))}
                </select>
                {errors.variantId && (
                  <small style={{ color: "red" }}>{errors.variantId}</small>
                )}
              </div>
            </div>

            <div className="form-row">
              <label>
                Nhà cung cấp <span style={{ color: "red" }}>*</span>
              </label>
              <div>
                <select
                  className="input"
                  value={formData.supplierId}
                  onChange={(e) => handleChange("supplierId", e.target.value)}
                  disabled={!isNew}
                >
                  <option value="">Chọn nhà cung cấp</option>
                  {suppliers.map((supplier) => (
                    <option
                      key={supplier.supplierId}
                      value={supplier.supplierId}
                    >
                      {supplier.name}
                    </option>
                  ))}
                </select>
                {errors.supplierId && (
                  <small style={{ color: "red" }}>{errors.supplierId}</small>
                )}
              </div>
            </div>

            <div className="form-row" style={{ gridColumn: "1 / -1" }}>
              <label>
                License Key <span style={{ color: "red" }}>*</span>
              </label>
              <div>
                <textarea
                  className="textarea"
                  value={formData.keyString}
                  onChange={(e) => handleChange("keyString", e.target.value)}
                  placeholder="Nhập license key..."
                  rows={3}
                  disabled={!isNew}
                  style={{ fontFamily: "monospace" }}
                />
                {errors.keyString && (
                  <small style={{ color: "red" }}>{errors.keyString}</small>
                )}
              </div>
            </div>

            <div className="form-row">
              <label>Loại key</label>
              <select
                className="input"
                value={formData.type}
                onChange={(e) => handleChange("type", e.target.value)}
                disabled={!isNew}
              >
                <option value="Individual">Cá nhân</option>
                <option value="Pool">Dùng chung (Pool)</option>
              </select>
            </div>

            {!isNew && (
              <div className="form-row">
                <label>Trạng thái</label>
                <select
                  className="input"
                  value={formData.status}
                  onChange={(e) => handleChange("status", e.target.value)}
                  disabled
                >
                  <option value="Available">Còn</option>
                  <option value="Sold">Đã bán</option>
                  <option value="Error">Lỗi</option>
                  <option value="Recalled">Thu hồi</option>
                  <option value="Expired">Hết hạn</option>
                </select>
              </div>
            )}

            {isNew && (
              <div className="form-row">
                <label>Giá vốn (COGS)</label>
                <div>
                  <input
                    className="input"
                    type="number"
                    min="0"
                    step="0.01"
                    value={formData.cogsPrice}
                    onChange={(e) => handleChange("cogsPrice", e.target.value)}
                    placeholder="Nhập giá vốn (COGS)"
                  />
                  {errors.cogsPrice && (
                    <small style={{ color: "red" }}>{errors.cogsPrice}</small>
                  )}
                </div>
              </div>
            )}

            <div className="form-row">
              <label>Ngày hết hạn</label>
              <div>
                <input
                  className="input"
                  type="date"
                  min={minExpiryDate}
                  value={formData.expiryDate}
                  onChange={(e) => handleChange("expiryDate", e.target.value)}
                  disabled={!isNew}
                />
                {errors.expiryDate && (
                  <small style={{ color: "red" }}>{errors.expiryDate}</small>
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
                  placeholder="Ghi chú thêm về key..."
                  rows={4}
                />
                {errors.notes && (
                  <small style={{ color: "red" }}>{errors.notes}</small>
                )}
              </div>
            </div>
          </div>

          {!isNew && keyInfo && (
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
                  <small className="muted">Trạng thái hiện tại:</small>
                  <div>{getStatusLabel(keyInfo.status)}</div>
                </div>
                <div>
                  <small className="muted">Ngày nhập kho:</small>
                  <div>{formatDateTime(keyInfo.importedAt)}</div>
                </div>
                <div>
                  <small className="muted">Người nhập:</small>
                  <div>{keyInfo.importedByEmail || "-"}</div>
                </div>
                {keyInfo.updatedAt && (
                  <div>
                    <small className="muted">Cập nhật lần cuối:</small>
                    <div>{formatDateTime(keyInfo.updatedAt)}</div>
                  </div>
                )}
                {keyInfo.assignedToOrderId && (
                  <>
                    <div>
                      <small className="muted">Người mua:</small>
                      <div>{buyerInfo?.name || "-"}</div>
                    </div>
                    <div>
                      <small className="muted">Email người mua:</small>
                      <div>{buyerInfo?.email || "-"}</div>
                    </div>
                    <div>
                      <small className="muted">Mã đơn hàng:</small>
                      <div>
                        {buyerInfo?.orderId ? (
                          <Link to={`/orders/${buyerInfo.orderId}`}>
                            {buyerInfo.orderId}
                          </Link>
                        ) : (
                          "-"
                        )}
                      </div>
                    </div>
                  </>
                )}
                
              </div>
            </div>
          )}

          <div style={{ display: "flex", gap: 8, marginTop: 16 }}>
            <PermissionGuard moduleCode="WAREHOUSE_MANAGER" permissionCode={isNew ? "CREATE" : "EDIT"} fallback={
              <button type="button" className="btn primary disabled" disabled title={isNew ? "Bạn không có quyền tạo key" : "Bạn không có quyền sửa key"}>
                {saving ? "Đang lưu..." : "Lưu"}
              </button>
            }>
              <button type="submit" className="btn primary" disabled={saving}>
                {saving ? "Đang lưu..." : "Lưu"}
              </button>
            </PermissionGuard>
            <button
              type="button"
              className="btn"
              onClick={() => navigate("/keys")}
            >
              Hủy
            </button>
            {!isNew && !keyInfo?.orderCode && (
              <button
                type="button"
                className="btn"
                onClick={handleDelete}
                style={{ marginLeft: "auto", color: "#dc2626" }}
              >
                Xóa key
              </button>
            )}
          </div>
        </form>
      </section>
    </div>
  );
}
