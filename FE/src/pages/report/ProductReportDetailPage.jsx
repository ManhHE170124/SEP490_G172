import React, { useState, useEffect, useCallback, useMemo } from "react";
import { useParams, useNavigate, Link, useLocation } from "react-router-dom";
import { ProductReportApi } from "../../services/productReportApi";
import { ProductApi } from "../../services/products";
import { ProductVariantsApi } from "../../services/productVariants";
import { ProductKeyApi } from "../../services/productKeys";
import { ProductAccountApi } from "../../services/productAccounts";
import { usersApi } from "../../api/usersApi";
import ToastContainer from "../../components/Toast/ToastContainer";
import useToast from "../../hooks/useToast";
import "../admin/admin.css";

export default function ProductReportDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const isNew = location.pathname.endsWith("/add") || id === "add";
  const { toasts, showSuccess, showError, removeToast } = useToast();

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [report, setReport] = useState(null);
  const [status, setStatus] = useState("");

  // Creation form state
  const [formData, setFormData] = useState({
    title: "",
    description: "",
    productId: "",
    variantId: "",
    itemId: "", // KeyId or AccountId
    userId: "",
  });

  const [products, setProducts] = useState([]);
  const [variants, setVariants] = useState([]);
  const [loadingVariants, setLoadingVariants] = useState(false);

  // Item search state (Key/Account)
  const [itemSearchTerm, setItemSearchTerm] = useState("");
  const [itemSearchResults, setItemSearchResults] = useState([]);
  const [itemSearchLoading, setItemSearchLoading] = useState(false);

  // User search state
  const [userSearchTerm, setUserSearchTerm] = useState("");
  const [userSearchResults, setUserSearchResults] = useState([]);
  const [userSearchLoading, setUserSearchLoading] = useState(false);
  const [selectedUser, setSelectedUser] = useState(null);

  const [productSearchTerm, setProductSearchTerm] = useState("");

  const filteredProducts = useMemo(() => {
    if (!productSearchTerm) return products;
    return products.filter((p) =>
      p.productName.toLowerCase().includes(productSearchTerm.toLowerCase())
    );
  }, [products, productSearchTerm]);

  const loadReport = useCallback(async () => {
    if (isNew) return;

    setLoading(true);
    try {
      const data = await ProductReportApi.get(id);
      setReport(data);
      setStatus(data.status);
    } catch (err) {
      console.error("Failed to load report:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể tải thông tin báo cáo";
      showError("Lỗi tải dữ liệu", errorMsg);
      navigate("/reports");
    } finally {
      setLoading(false);
    }
  }, [id, isNew, navigate, showError]);

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

  useEffect(() => {
    loadReport();
    if (isNew) {
      loadProducts();
    }
  }, [loadReport, isNew, loadProducts]);

  useEffect(() => {
    if (formData.productId) {
      loadVariantsForProduct(formData.productId);
    }
  }, [formData.productId, loadVariantsForProduct]);

  const handleStatusChange = async (newStatus) => {
    setStatus(newStatus);
  };

  const handleSaveStatus = async () => {
    if (!report || status === report.status) return;

    setSaving(true);
    try {
      await ProductReportApi.updateStatus(id, {
        id: id,
        status: status,
      });
      showSuccess("Thành công", "Trạng thái báo cáo đã được cập nhật");
      loadReport();
    } catch (err) {
      console.error("Failed to update status:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể cập nhật trạng thái";
      showError("Lỗi cập nhật", errorMsg);
    } finally {
      setSaving(false);
    }
  };

  const handleCreate = async (e) => {
    e.preventDefault();

    // Validation
    if (!formData.title || !formData.description || !formData.variantId) {
      showError("Lỗi", "Vui lòng điền đầy đủ thông tin bắt buộc");
      return;
    }

    // Require item (key/account) to be selected
    if (!formData.itemId) {
      showError("Lỗi", "Vui lòng chọn Key/Tài khoản");
      return;
    }

    // Require user (reporter) to be selected
    if (!formData.userId) {
      showError("Lỗi", "Vui lòng chọn Người báo cáo");
      return;
    }

    setSaving(true);
    try {
      
      const payload = {
        name: formData.title,
        description: formData.description,
        productVariantId: formData.variantId,
      };

      // Only add userId if it has a value
      if (formData.userId) {
        payload.userId = formData.userId;
      }

      // Determine if item is key or account based on product type
      const selectedProduct = products.find(
        (p) => p.productId === formData.productId
      );

      if (selectedProduct && formData.itemId) {
        if (selectedProduct.productType === "PERSONAL_KEY") {
          payload.productKeyId = formData.itemId;
        } else if (selectedProduct.productType === "PERSONAL_ACCOUNT" || selectedProduct.productType === "SHARED_ACCOUNT") {
          payload.productAccountId = formData.itemId;
        }
      }

      await ProductReportApi.create(payload);
      showSuccess("Thành công", "Báo cáo đã được tạo thành công");
      navigate("/reports");
    } catch (err) {
      console.error("Failed to create report:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể tạo báo cáo";
      showError("Lỗi tạo báo cáo", errorMsg);
    } finally {
      setSaving(false);
    }
  };

  const handleSearchItems = async () => {
    if (!formData.variantId || !itemSearchTerm.trim()) return;

    setItemSearchLoading(true);
    try {
      const selectedProduct = products.find(
        (p) => p.productId === formData.productId
      );
      let results = [];

      if (selectedProduct?.productType === "PERSONAL_KEY") {
        // Search keys by serial number
        const res = await ProductKeyApi.list({
          pageNumber: 1,
          pageSize: 20,
          productVariantId: formData.variantId,
          searchTerm: itemSearchTerm,
        });
        results = res.items || [];
        // No client-side filtering since we're passing searchTerm to API
      } else if (selectedProduct?.productType === "PERSONAL_ACCOUNT" || selectedProduct?.productType === "SHARED_ACCOUNT") {
        // Search accounts by username or email
        const res = await ProductAccountApi.list({
          pageNumber: 1,
          pageSize: 50,
          productVariantId: formData.variantId,
          searchTerm: itemSearchTerm,
        });
        results = res.items || [];
        // No client-side filtering since we're passing searchTerm to API
      }
      setItemSearchResults(results);
    } catch (err) {
      console.error("Failed to search items:", err);
    } finally {
      setItemSearchLoading(false);
    }
  };

  const handleSearchUsers = async () => {
    if (!userSearchTerm.trim()) return;

    setUserSearchLoading(true);
    try {
      const res = await usersApi.list({
        q: userSearchTerm,
        page: 1,
        pageSize: 10,
      });
      setUserSearchResults(res?.items || res || []);
    } catch (err) {
      console.error("Failed to search users:", err);
    } finally {
      setUserSearchLoading(false);
    }
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

  if (!isNew && !report) return null;

  return (
    <div className="page">
      <ToastContainer toasts={toasts} onRemove={removeToast} />

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
            {isNew ? "Tạo Báo cáo Mới" : "Chi tiết Báo cáo"}
          </h1>
          <Link className="btn" to="/reports">
            ← Quay lại
          </Link>
        </div>

        {isNew ? (
          <form onSubmit={handleCreate}>
            {/* Product and Variant Section */}
            <div style={{ marginBottom: 24 }}>
              <h3 style={{ marginBottom: 16, fontSize: 16, fontWeight: 600 }}>Thông tin sản phẩm</h3>
              <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
              {/* Product Selection */}
              <div className="form-row">
                <label>
                  Sản phẩm <span style={{ color: "red" }}>*</span>
                </label>
                <input
                  className="input"
                  placeholder="Tìm kiếm sản phẩm..."
                  value={productSearchTerm}
                  onChange={(e) => setProductSearchTerm(e.target.value)}
                  style={{ marginBottom: 8 }}
                />
                <select
                  className="input"
                  value={formData.productId}
                  onChange={(e) => {
                    setFormData({
                      ...formData,
                      productId: e.target.value,
                      variantId: "",
                      itemId: "",
                    });
                    setItemSearchResults([]);
                  }}
                  required
                  style={{ minHeight: 120 }}
                  size={5}
                >
                  <option value="">Chọn sản phẩm</option>
                  {filteredProducts.map((p) => (
                    <option key={p.productId} value={p.productId}>
                      {p.productName}
                    </option>
                  ))}
                </select>
              </div>

              {/* Variant Selection */}
              <div className="form-row">
                <label>
                  Biến thể <span style={{ color: "red" }}>*</span>
                </label>
                <select
                  className="input"
                  value={formData.variantId}
                  onChange={(e) =>
                    setFormData({ ...formData, variantId: e.target.value })
                  }
                  disabled={!formData.productId || loadingVariants}
                  required
                >
                  <option value="">
                    {loadingVariants ? "Đang tải..." : "Chọn biến thể"}
                  </option>
                  {variants.map((v) => (
                    <option key={v.variantId} value={v.variantId}>
                      {v.title}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </div>

            {/* Item and User Selection Section */}
            <div style={{ marginBottom: 24 }}>
              <h3 style={{ marginBottom: 16, fontSize: 16, fontWeight: 600 }}>Thông tin liên quan</h3>

              {/* Item Search (Key/Account) */}
              <div className="form-row" style={{ gridColumn: "1 / -1" }}>
                <label>Mã Key / Tài khoản <span style={{ color: "red" }}>*</span></label>
                <div style={{ display: "flex", gap: 8 }}>
                  <input
                    className="input"
                    placeholder="Tìm kiếm..."
                    value={itemSearchTerm}
                    onChange={(e) => setItemSearchTerm(e.target.value)}
                    onKeyDown={(e) => e.key === "Enter" && handleSearchItems()}
                    disabled={!formData.variantId}
                  />
                  <button
                    type="button"
                    className="btn"
                    onClick={handleSearchItems}
                    disabled={!formData.variantId || itemSearchLoading}
                  >
                    {itemSearchLoading ? "Đang tìm..." : "Tìm"}
                  </button>
                </div>
                {formData.itemId && (
                  <div style={{
                    marginTop: 8,
                    padding: "8px 12px",
                    background: "#dcfce7",
                    border: "1px solid #86efac",
                    borderRadius: "6px",
                    fontSize: 14,
                    color: "#166534"
                  }}>
                    ✓ Đã chọn: {(() => {
                      const selectedProduct = products.find(p => p.productId === formData.productId);
                      const isKey = selectedProduct?.productType === 'PERSONAL_KEY';
                      const selectedItem = itemSearchResults.find(item =>
                        (isKey ? item.keyId : item.productAccountId) === formData.itemId
                      );
                      if (!selectedItem) return formData.itemId;
                      return isKey
                        ? `Key: ${selectedItem.keyString}`
                        : `Account: ${selectedItem.accountUsername || selectedItem.accountEmail}`;
                    })()}
                  </div>
                )}
                {itemSearchResults.length > 0 && (
                  <div style={{
                    marginTop: 12,
                    border: "1px solid var(--line)",
                    borderRadius: "8px",
                    maxHeight: "300px",
                    overflowY: "auto",
                    background: "var(--card)"
                  }}>
                    {itemSearchResults.map(item => {
                      const selectedProduct = products.find(p => p.productId === formData.productId);
                      const isKey = selectedProduct?.productType === 'PERSONAL_KEY';

                      const val = isKey ? item.keyId : item.productAccountId;
                      const isSelected = formData.itemId === val;

                      const handleItemClick = () => {
                        setFormData(prev => ({...prev, itemId: val}));
                      };

                      return (
                        <div
                          key={val}
                          onClick={handleItemClick}
                          style={{
                            padding: "12px",
                            cursor: "pointer",
                            borderBottom: "1px solid var(--line)",
                            background: isSelected ? "#eff6ff" : "transparent",
                            transition: "background 0.15s"
                          }}
                          onMouseEnter={(e) => {
                            if (!isSelected) e.currentTarget.style.background = "#f9fafb";
                          }}
                          onMouseLeave={(e) => {
                            if (!isSelected) e.currentTarget.style.background = "transparent";
                          }}
                        >
                          {isKey ? (
                            <div>
                              <div style={{ fontWeight: 600, marginBottom: 4 }}>
                                Key: {item.keyString}
                              </div>
                              <div style={{ fontSize: 13, color: "var(--muted)" }}>
                                Order: <a
                                  href={`http://localhost:3000/orders/${item.assignToOrder}`}
                                  target="_blank"
                                  onClick={(e) => e.stopPropagation()}
                                  rel="noreferrer"
                                >{item.assignToOrder}</a>
                              </div>
                              <div style={{ fontSize: 13, color: "var(--muted)" }}>
                                Trạng thái: {item.status || "—"}
                              </div>
                            </div>
                          ) : (
                            <div>
                              <div style={{ fontWeight: 600, marginBottom: 4 }}>
                                {item.accountUsername || item.accountEmail}
                              </div>
                              <div style={{ fontSize: 13, color: "var(--muted)" }}>
                                Email: {item.accountEmail || "—"}
                              </div>
                              {item.status && (
                                <div style={{ fontSize: 13, color: "var(--muted)", marginTop: 2 }}>
                                  Trạng thái: {item.status}
                                </div>
                              )}
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>

              {/* User Search */}
              <div className="form-row" style={{ gridColumn: "1 / -1" }}>
                <label>Người báo cáo <span style={{ color: "red" }}>*</span></label>
                <div style={{ display: "flex", gap: 8 }}>
                  <input
                    className="input"
                    placeholder="Email hoặc tên..."
                    value={userSearchTerm}
                    onChange={(e) => setUserSearchTerm(e.target.value)}
                    onKeyDown={(e) => e.key === "Enter" && handleSearchUsers()}
                  />
                  <button
                    type="button"
                    className="btn"
                    onClick={handleSearchUsers}
                    disabled={userSearchLoading}
                  >
                    {userSearchLoading ? "Đang tìm..." : "Tìm"}
                  </button>
                </div>
                {userSearchResults.length > 0 && (
                  <div style={{
                    marginTop: 12,
                    border: "1px solid var(--line)",
                    borderRadius: "8px",
                    maxHeight: "300px",
                    overflowY: "auto",
                    background: "var(--card)"
                  }}>
                    {userSearchResults.map(u => {
                      const isSelected = formData.userId === u.userId;
                      return (
                        <div
                          key={u.userId}
                          onClick={() => setFormData({...formData, userId: u.userId})}
                          style={{
                            padding: "12px",
                            cursor: "pointer",
                            borderBottom: "1px solid var(--line)",
                            background: isSelected ? "#eff6ff" : "transparent",
                            transition: "background 0.15s"
                          }}
                          onMouseEnter={(e) => {
                            if (!isSelected) e.currentTarget.style.background = "#f9fafb";
                          }}
                          onMouseLeave={(e) => {
                            if (!isSelected) e.currentTarget.style.background = "transparent";
                          }}
                        >
                          <div style={{ fontWeight: 600, marginBottom: 4 }}>
                            {u.fullName || u.email}
                          </div>
                          <div style={{ fontSize: 13, color: "var(--muted)" }}>
                            {u.email}
                          </div>
                          {u.phoneNumber && (
                            <div style={{ fontSize: 13, color: "var(--muted)", marginTop: 2 }}>
                              SĐT: {u.phoneNumber}
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>

            {/* Report Content Section */}
            <div style={{ marginBottom: 24 }}>
              <h3 style={{ marginBottom: 16, fontSize: 16, fontWeight: 600 }}>Nội dung báo cáo</h3>

              <div className="form-row" style={{ marginBottom: 16 }}>
                <label>
                  Tiêu đề <span style={{ color: "red" }}>*</span>
                </label>
                <input
                  className="input"
                  value={formData.title}
                  onChange={(e) =>
                    setFormData({ ...formData, title: e.target.value })
                  }
                  required
                  placeholder="Nhập tiêu đề báo cáo..."
                />
              </div>

              <div className="form-row" style={{ marginBottom: 16 }}>
                <label>
                  Nội dung báo cáo <span style={{ color: "red" }}>*</span>
                </label>
                <textarea
                  className="input"
                  rows={6}
                  value={formData.description}
                  onChange={(e) =>
                    setFormData({ ...formData, description: e.target.value })
                  }
                  required
                  style={{ resize: "vertical" }}
                  placeholder="Mô tả chi tiết vấn đề..."
                />
              </div>
            </div>

            {/* Submit Section */}
            <div style={{ 
              display: "flex", 
              gap: 12, 
              justifyContent: "flex-end",
              paddingTop: 16,
              borderTop: "1px solid var(--line)"
            }}>
              <Link className="btn" to="/reports">
                Hủy
              </Link>
              <button
                type="submit"
                className="btn primary"
                disabled={saving}
              >
                {saving ? "Đang tạo..." : "Tạo báo cáo"}
              </button>
            </div>
          </form>
        ) : (
          /* Detail View */
          <div
            className="grid"
            style={{
              gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
              gap: 12,
            }}
          >
            <div className="form-row">
              <label>Tiêu đề</label>
              <input
                className="input"
                value={report.name || ""}
                readOnly
                disabled
              />
            </div>

            <div className="form-row">
              <label>Người báo cáo</label>
              <input
                className="input"
                value={report.userEmail || "—"}
                readOnly
                disabled
              />
            </div>

             <div className="form-row">
              <label>Sản phẩm/Biến thể</label>
              <input
                className="input"
                value={`${report.productName || ""} - ${report.productVariantTitle || ""}`}
                readOnly
                disabled
              />
            </div>

             <div className="form-row">
              <label>Item liên quan</label>
              <input
                className="input"
                value={report.productKeyString || report.productAccountUsername || "—"}
                readOnly
                disabled
              />
            </div>

            <div className="form-row">
              <label>Ngày tạo</label>
              <input
                className="input"
                value={
                  report.createdAt
                    ? new Date(report.createdAt).toLocaleString("vi-VN")
                    : "—"
                }
                readOnly
                disabled
              />
            </div>

            <div className="form-row">
              <label>Trạng thái</label>
              <div style={{ display: "flex", gap: 8 }}>
                <select
                  className="input"
                  value={status}
                  onChange={(e) => handleStatusChange(e.target.value)}
                >
                  <option value="Pending">Chờ xử lý</option>
                  <option value="Processing">Đang xử lý</option>
                  <option value="Resolved">Đã giải quyết</option>
                  <option value="Rejected">Từ chối</option>
                </select>
                <button
                  className="btn primary"
                  onClick={handleSaveStatus}
                  disabled={saving || status === report.status}
                >
                  {saving ? "Đang lưu..." : "Cập nhật"}
                </button>
              </div>
            </div>

            <div className="form-row" style={{ gridColumn: "1 / -1" }}>
              <label>Nội dung báo cáo</label>
              <textarea
                className="input"
                rows={6}
                value={report.description || ""}
                readOnly
                disabled
                style={{ resize: "vertical" }}
              />
            </div>

            {report.adminResponse && (
              <div className="form-row" style={{ gridColumn: "1 / -1" }}>
                <label>Phản hồi từ Admin</label>
                <textarea
                  className="input"
                  rows={4}
                  value={report.adminResponse}
                  readOnly
                  disabled
                />
              </div>
            )}
          </div>
        )}
      </section>
    </div>
  );
}
