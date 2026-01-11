import React, { useState, useEffect, useCallback, useMemo } from "react";
import { usersApi } from "../../api/usersApi";
import { useParams, useNavigate, useLocation, Link } from "react-router-dom";
import { ProductAccountApi } from "../../services/productAccounts";
import { ProductApi } from "../../services/products";
import { ProductVariantsApi } from "../../services/productVariants";
import { SupplierApi } from "../../services/suppliers";
import ToastContainer from "../../components/Toast/ToastContainer";
import ConfirmDialog from "../../components/ConfirmDialog/ConfirmDialog";
import useToast from "../../hooks/useToast";
import formatDateTime from "../../utils/formatDatetime";
import { formatVietnameseDate } from "../../utils/formatDate";
import { getAccountStatusLabel } from "../../utils/productAccountHelper";
import "../admin/admin.css";

export default function AccountDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const isNew = location.pathname.endsWith("/add") || !id || id === "add";
  const { toasts, showSuccess, showError, showWarning, removeToast } =
    useToast();

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [accountInfo, setAccountInfo] = useState(null);
  const [suppliers, setSuppliers] = useState([]);
  const [products, setProducts] = useState([]);
  const [variants, setVariants] = useState([]);
  const [loadingVariants, setLoadingVariants] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [actualPassword, setActualPassword] = useState("");

  const [formData, setFormData] = useState({
    supplierId: "",
    productId: "",
    variantId: "",
    accountEmail: "",
    accountUsername: "",
    accountPassword: "",
    maxUsers: "5",
    status: "Active",
    cogsPrice: "",
    startDate: "",
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

  const [extendDialog, setExtendDialog] = useState({
    isOpen: false,
    newExpiryDate: "",
  });

  // History state (for in-page paging and sort)
  const [history, setHistory] = useState([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historySort, setHistorySort] = useState("desc");
  const [historyPage, setHistoryPage] = useState(1);
  const [historyPageSize, setHistoryPageSize] = useState(10);

  const totalHistory = history.length;
  const sortedHistory = useMemo(() => {
    const items = [...history];
    items.sort((a, b) => {
      const da = new Date(a.actionAt || a.ActionAt);
      const db = new Date(b.actionAt || b.ActionAt);
      return historySort === "desc" ? db - da : da - db;
    });
    return items;
  }, [history, historySort]);

  const pagedHistory = useMemo(() => {
    const start = (historyPage - 1) * historyPageSize;
    return sortedHistory.slice(start, start + historyPageSize);
  }, [sortedHistory, historyPage, historyPageSize]);

  // User search/add state
  const [userSearchTerm, setUserSearchTerm] = useState("");
  const [userSearchLoading, setUserSearchLoading] = useState(false);
  const [userSearchResults, setUserSearchResults] = useState([]);
  const [selectedUserId, setSelectedUserId] = useState("");

  const loadProductAccount = useCallback(async () => {
    if (!id || id === "add") return;

    setLoading(true);
    try {
      const data = await ProductAccountApi.get(id);
      setAccountInfo(data);

      // Ensure the account's supplier is in the suppliers list
      if (data.supplierId && data.supplierName) {
        setSuppliers(prev => {
          const exists = prev.some(s => s.supplierId === data.supplierId);
          if (!exists) {
            return [...prev, { supplierId: data.supplierId, name: data.supplierName }];
          }
          return prev;
        });
      }

      const variantId =
        data.variantId || data.productVariantId || data.productVariantId?.value;
      setFormData({
        supplierId: data.supplierId ? data.supplierId.toString() : "",
        productId: data.productId,
        variantId: variantId ? variantId.toString() : "",
        accountEmail: data.accountEmail,
        accountUsername: data.accountUsername || "",
        accountPassword: "", // Don't populate password
        maxUsers: data.maxUsers.toString(),
        status: data.status,
        cogsPrice: (data.cogsPrice ?? "").toString(),
        expiryDate: data.expiryDate
          ? (() => {
              const d = new Date(data.expiryDate);
              return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
            })()
          : "",
        notes: data.notes || "",
      });
    } catch (err) {
      console.error("Failed to load account:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể tải thông tin tài khoản";
      showError("Lỗi tải dữ liệu", errorMsg);
      navigate("/accounts");
    } finally {
      setLoading(false);
    }
  }, [id, navigate, showError]);

  const loadSuppliers = useCallback(async (loadAll = false) => {
    try {
      const params = {
        pageNumber: 1,
        pageSize: 100,
      };
      // Only filter by Active status when creating new account
      if (!loadAll) {
        params.status = "Active";
      }
      const data = await SupplierApi.list(params);
      setSuppliers(data.items || data.data || []);
    } catch (err) {
      console.error("Failed to load suppliers:", err);
    }
  }, []);

  const loadProducts = useCallback(async () => {
    try {
      const data = await ProductApi.list({
        pageNumber: 1,
        pageSize: 100,
        // Load both shared and personal account products
        productTypes: ["SHARED_ACCOUNT", "PERSONAL_ACCOUNT"],
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

  const loadHistory = useCallback(
    async (resetPage = false) => {
      if (!id || id === "add") return;
      setHistoryLoading(true);
      try {
        const data = await ProductAccountApi.getHistory(id);
        const list =
          data?.history || data?.History || data?.data?.history || [];
        setHistory(Array.isArray(list) ? list : []);
        if (resetPage) setHistoryPage(1);
      } catch (err) {
        console.error("Failed to load history:", err);
      } finally {
        setHistoryLoading(false);
      }
    },
    [id]
  );

  const searchUsers = useCallback(async () => {
    if (!userSearchTerm.trim()) {
      setUserSearchResults([]);
      return;
    }
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
      showError("Loi", err.message || "Khong tim duoc nguoi dung");
    } finally {
      setUserSearchLoading(false);
    }
  }, [userSearchTerm, showError]);

  const handleAddUserToAccount = useCallback(async () => {
    if (!selectedUserId) {
      showWarning("Du lieu khong hop le", "Hay chon nguoi dung");
      return;
    }
    try {
      await ProductAccountApi.addCustomer(id, {
        productAccountId: id,
        userId: selectedUserId,
      });
      showSuccess("Thành công", "Đã thêm người dùng vào tài khoản");
      setSelectedUserId("");
      setUserSearchTerm("");
      setUserSearchResults([]);
      await loadProductAccount();
      await loadHistory(true);
    } catch (err) {
      console.error("Add user failed:", err);
      const msg =
        err.response?.data?.message || err.message || "Không thể thêm";
      showError("Lỗi", msg);
    }
  }, [
    id,
    selectedUserId,
    loadProductAccount,
    loadHistory,
    showSuccess,
    showError,
    showWarning,
  ]);

  const handleRemoveUserFromAccount = useCallback(
    async (userId) => {
      setConfirmDialog({
        isOpen: true,
        title: "Xác nhận xóa người dùng",
        message: "Bạn có chắc muốn xóa người dùng này khỏi tài khoản?",
        type: "danger",
        onConfirm: async () => {
          setConfirmDialog((prev) => ({ ...prev, isOpen: false }));
          try {
            await ProductAccountApi.removeCustomer(id, {
              productAccountId: id,
              userId,
            });
            showSuccess("Thành công", "Đã xóa người dùng khỏi tài khoản");
            await loadProductAccount();
            await loadHistory(true);
          } catch (err) {
            console.error("Remove user failed:", err);
            const msg =
              err.response?.data?.message || err.message || "Không thể xóa";
            showError("Lỗi", msg);
          }
        },
      });
    },
    [id, loadProductAccount, loadHistory, showSuccess, showError]
  );

  const handleExtendExpiry = useCallback(async () => {
    if (!extendDialog.newExpiryDate) {
      showWarning("Dữ liệu không hợp lệ", "Vui lòng chọn ngày hết hạn mới");
      return;
    }

    if (formData.expiryDate && extendDialog.newExpiryDate < formData.expiryDate) {
      showWarning("Dữ liệu không hợp lệ", "Ngày hết hạn mới không được nhỏ hơn ngày hết hạn hiện tại");
      return;
    }

    try {
      await ProductAccountApi.extendExpiry(id, {
        productAccountId: id,
        newExpiryDate: extendDialog.newExpiryDate,
      });
      showSuccess("Thành công", "Đã gia hạn tài khoản thành công");
      setExtendDialog({ isOpen: false, newExpiryDate: "" });
      await loadProductAccount();
      await loadHistory(true);
    } catch (err) {
      console.error("Extend expiry failed:", err);
      const msg =
        err.response?.data?.message ||
        err.message ||
        "Không thể gia hạn tài khoản";
      showError("Lỗi", msg);
    }
  }, [
    id,
    extendDialog.newExpiryDate,
    formData.expiryDate,
    loadProductAccount,
    loadHistory,
    showSuccess,
    showError,
    showWarning,
  ]);

  useEffect(() => {
    // Load all suppliers when viewing existing account, only active when creating new
    loadSuppliers(!isNew);
    loadProducts();
    if (!isNew) {
      loadProductAccount();
      loadHistory(true);
    } else {
        // Set default start date to today for new accounts
        setFormData(prev => ({ ...prev, startDate: new Date().toISOString().split('T')[0] }));
    }
  }, [isNew, loadProductAccount, loadSuppliers, loadProducts, loadHistory]);

  // Load variants when product changes
  useEffect(() => {
    if (formData.productId) {
      loadVariantsForProduct(formData.productId);
    }
  }, [formData.productId, loadVariantsForProduct]);

  // Reload history from API when history filters change
  useEffect(() => {
    if (!isNew) {
      loadHistory(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [historySort, historyPage, historyPageSize, isNew]);

  const handleShowPassword = async () => {
    if (showPassword) {
      setShowPassword(false);
      setActualPassword("");
      return;
    }

    try {
      const response = await ProductAccountApi.getPassword(id);
      setActualPassword(response.password);
      setShowPassword(true);
    } catch (err) {
      console.error("Failed to get password:", err);
      const errorMsg =
        err.response?.data?.message ||
        err.message ||
        "Không thể lấy mật khẩu. Bạn có thể không có quyền truy cập.";
      showError("Lỗi", errorMsg);
    }
  };

  // Pre-select product if passed via query param (e.g. from KeyMonitor)
  useEffect(() => {
    if (isNew) {
      const params = new URLSearchParams(location.search);
      const pid = params.get("productId");
      if (pid) {
        setFormData((prev) => ({ ...prev, productId: pid }));
      }
    }
  }, [location.search, isNew]);

  const validateForm = () => {
    const newErrors = {};

    if (!formData.supplierId) {
      newErrors.supplierId = "Nhà cung cấp là bắt buộc";
    }

    if (!formData.productId) {
      newErrors.productId = "Sản phẩm là bắt buộc";
    }

    if (!formData.variantId) {
      newErrors.variantId = "Biến thể sản phẩm là bắt buộc";
    }

    if (!formData.accountEmail.trim()) {
      newErrors.accountEmail = "Email tài khoản là bắt buộc";
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.accountEmail)) {
      newErrors.accountEmail = "Email không hợp lệ";
    }

    if (isNew && !formData.accountPassword.trim()) {
      newErrors.accountPassword = "Mật khẩu là bắt buộc khi tạo tài khoản mới";
    }

    const maxUsers = parseInt(formData.maxUsers);
    if (isNaN(maxUsers) || maxUsers < 1 || maxUsers > 100) {
      newErrors.maxUsers = "Số người dùng tối đa phải từ 1 đến 100";
    }

    if (formData.notes && formData.notes.length > 1000) {
      newErrors.notes = "Ghi chú không được vượt quá 1000 ký tự";
    }

    // Validate COGS price and expiry date
    const cogs = parseFloat(formData.cogsPrice);
    if (isNew) {
      if (isNaN(cogs)) {
        newErrors.cogsPrice = "Giá vốn (COGS) là bắt buộc";
      } else if (cogs < 0) {
        newErrors.cogsPrice = "Giá vốn không được âm";
      }
    } else if (formData.cogsPrice && !isNaN(cogs) && cogs < 0) {
      newErrors.cogsPrice = "Giá vốn không được âm";
    }

    if (isNew && !formData.startDate) {
      // Should be set automatically, but just in case
      newErrors.startDate = "Ngày bắt đầu là bắt buộc";
    } else if (formData.startDate && formData.expiryDate) {
       // Validate expiry > start + duration is handled by UI min attribute, but good to keep check
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
      const isPersonal = selectedProduct?.productType === "PERSONAL_ACCOUNT";
      const payload = {
        supplierId: parseInt(formData.supplierId),
        variantId: formData.variantId,
        accountEmail: formData.accountEmail,
        accountUsername: formData.accountUsername || null,
        maxUsers: isPersonal ? 1 : parseInt(formData.maxUsers),
        cogsPrice:
          formData.cogsPrice === "" ? null : parseFloat(formData.cogsPrice),
        startDate: formData.startDate || null,
        notes: formData.notes || null,
      };

      if (isNew) {
        payload.accountPassword = formData.accountPassword;
        await ProductAccountApi.create(payload);
        showSuccess("Thành công", "Tài khoản đã được tạo thành công");
        navigate("/accounts");
      } else {
        payload.productAccountId = id;
        payload.variantId = formData.variantId;
        payload.status = formData.status;
        // Only include password if it was changed
        if (formData.accountPassword.trim()) {
          payload.accountPassword = formData.accountPassword;
        }
        await ProductAccountApi.update(id, payload);
        showSuccess("Thành công", "Tài khoản đã được cập nhật thành công");
        await loadProductAccount();
        await loadHistory(true);
      }
    } catch (err) {
      console.error("Failed to save account:", err);
      const errorMsg =
        err.response?.data?.message || err.message || "Không thể lưu tài khoản";
      showError("Lỗi lưu dữ liệu", errorMsg);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = () => {
    setConfirmDialog({
      isOpen: true,
      title: "Xác nhận xóa tài khoản",
      message:
        "Bạn có chắc muốn xóa tài khoản này? Hành động này không thể hoàn tác.",
      type: "danger",
      onConfirm: async () => {
        setConfirmDialog({ ...confirmDialog, isOpen: false });
        try {
          await ProductAccountApi.delete(id);
          showSuccess("Thành công", "Tài khoản đã được xóa thành công");
          navigate("/accounts");
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

  const handleChange = (field, value) => {
    setFormData((prev) => {
      // Reset product and variant when supplier changes
      if (field === "supplierId" && value !== prev.supplierId) {
        // Only reset if changing from one supplier to another (not on initial set)
        if (prev.supplierId) {
           return { ...prev, [field]: value, productId: "", variantId: "" };
        }
        return { ...prev, [field]: value };
      }
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

  // Determine selected product and enforce rules for PERSONAL_ACCOUNT
  const selectedProduct = useMemo(() => {
    const pid = formData.productId?.toString?.() ?? formData.productId;
    return products.find(
      (p) => (p.productId?.toString?.() ?? p.productId) === pid
    );
  }, [products, formData.productId]);

  const selectedVariant = useMemo(() => {
    const vid = formData.variantId?.toString?.() ?? formData.variantId;
    return variants.find(
      (v) => (v.variantId?.toString?.() ?? v.variantId) === vid
    );
  }, [variants, formData.variantId]);

  // If product type is PERSONAL_ACCOUNT, force maxUsers to 1
  useEffect(() => {
    if (selectedProduct?.productType === "PERSONAL_ACCOUNT") {
      setFormData((prev) =>
        prev.maxUsers !== "1" ? { ...prev, maxUsers: "1" } : prev
      );
    }
  }, [selectedProduct]);

  useEffect(() => {
    if (!isNew) return;
    if (!formData.startDate) return;
    const duration = parseInt(selectedVariant?.durationDays ?? 0, 10);
    if (!Number.isFinite(duration) || duration <= 0) return;
    const [yyyy, mm, dd] = formData.startDate.split("-").map(Number);
    if (!yyyy || !mm || !dd) return;
    const expiryDate = new Date(yyyy, mm - 1, dd + duration);
    const iso = `${expiryDate.getFullYear()}-${String(
      expiryDate.getMonth() + 1
    ).padStart(2, "0")}-${String(expiryDate.getDate()).padStart(2, "0")}`;
    setFormData((prev) =>
      prev.expiryDate === iso ? prev : { ...prev, expiryDate: iso }
    );
  }, [formData.startDate, selectedVariant, isNew]);

  // Today (local) for min date constraint
  const todayStr = useMemo(() => {
    const t = new Date();
    const yyyy = t.getFullYear();
    const mm = String(t.getMonth() + 1).padStart(2, "0");
    const dd = String(t.getDate()).padStart(2, "0");
    return `${yyyy}-${mm}-${dd}`;
  }, []);

  // Calculate min expiry date
  const minExpiryDate = useMemo(() => {
      if (!selectedVariant?.durationDays) return todayStr;
      const duration = parseInt(selectedVariant.durationDays, 10);
      if (isNaN(duration) || duration <= 0) return todayStr;
      
      const t = new Date();
      t.setDate(t.getDate() + duration);
      const yyyy = t.getFullYear();
      const mm = String(t.getMonth() + 1).padStart(2, "0");
      const dd = String(t.getDate()).padStart(2, "0");
      return `${yyyy}-${mm}-${dd}`;
  }, [selectedVariant, todayStr]);

  const closeConfirmDialog = () => {
    setConfirmDialog({ ...confirmDialog, isOpen: false });
  };

  if (loading) {
    return (
      <div className="page">
        <div className="card ">
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

      {/* Extend Expiry Dialog */}
      {extendDialog.isOpen && (
        (() => {
          const calculateMinDate = () => {
             if (!formData.expiryDate) return todayStr;
             const parts = formData.expiryDate.split('-');
             if (parts.length !== 3) return todayStr;
             const duration = parseInt(selectedVariant?.durationDays ?? 0);
             if (duration <= 0) return todayStr;
             const d = new Date(parseInt(parts[0]), parseInt(parts[1]) - 1, parseInt(parts[2]));
             // Set min date to current expiry + duration (next possible expiry)
             d.setDate(d.getDate() + 1 + duration);
             return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
          };
          const minDateVal = calculateMinDate();
          return (
        <div
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            backgroundColor: "rgba(0, 0, 0, 0.5)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 1000,
          }}
          onClick={() => setExtendDialog({ isOpen: false, newExpiryDate: "" })}
        >
          <div
            className="card"
            style={{
              minWidth: 400,
              maxWidth: 500,
              padding: 24,
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <h2 style={{ marginTop: 0 }}>Gia hạn tài khoản</h2>
            <p style={{ color: "#6b7280", marginBottom: 16 }}>
              Chọn ngày hết hạn mới cho tài khoản này
            </p>
            <div className="group">
              <span>
                Ngày hết hạn hiện tại
              </span>
              <input
                className="input"
                type="text"
                value={formatVietnameseDate(formData.expiryDate)}
                disabled
              />
            </div>
            <div className="group">
              <span style={{ whiteSpace: "nowrap" }}>
                Ngày hết hạn mới <span style={{ color: "red" }}>*</span>
              </span>
              <input
                className="input"
                type="date"
                value={extendDialog.newExpiryDate}
                onChange={(e) =>
                  setExtendDialog((prev) => ({
                    ...prev,
                    newExpiryDate: e.target.value,
                  }))
                }
                min={minDateVal}
              />
              <small style={{ color: "#6b7280", marginTop: 4 }}>
                Ngày mới phải sau ngày hết hạn hiện tại (Min: {minDateVal})
              </small>
            </div>
            <div style={{ display: "flex", gap: 8, marginTop: 16 }}>
              <button
                type="button"
                className="btn primary"
                onClick={handleExtendExpiry}
              >
                Xác nhận
              </button>
              <button
                type="button"
                className="btn"
                onClick={() =>
                  setExtendDialog({ isOpen: false, newExpiryDate: "" })
                }
              >
                Hủy
              </button>
            </div>
          </div>
        </div>
          );
        })()
      )}

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
            {isNew ? "Thêm tài khoản mới" : "Chi tiết Tài khoản"}
          </h1>
          <Link className="btn" to="/accounts">
            ← Quay lại
          </Link>
        </div>

        <form onSubmit={handleSubmit}>
          {!isNew && accountInfo?.status === "Expired" && (
            <div
              style={{
                marginBottom: 16,
                padding: 12,
                background: "#fee2e2",
                borderRadius: 8,
                color: "#991b1b",
              }}
            >
              Tài khoản đã hết hạn. Chỉ có thể cập nhật ghi chú và trạng thái.
            </div>
          )}

          <div
            style={{
              display: "flex",
              flexWrap: "wrap",
              gap: 12,
            }}
          >
            <div className="group" style={{ flex: "1 1 300px" }}>
              <span style={{ whiteSpace: "nowrap" }}>
                Nhà cung cấp <span style={{ color: "red" }}>*</span>
              </span>
              <div>
                <select
                  className="input"
                  value={formData.supplierId}
                  onChange={(e) => handleChange("supplierId", e.target.value)}
                  disabled={!isNew}
                >
                  <option value="">Chọn nhà cung cấp</option>
                  {suppliers.map((supplier) => (
                    <option key={supplier.supplierId} value={supplier.supplierId.toString()}>
                      {supplier.name}
                    </option>
                  ))}
                </select>
                {errors.supplierId && (
                  <small style={{ color: "red" }}>{errors.supplierId}</small>
                )}
              </div>
            </div>

            <div className="group" style={{ flex: "1 1 300px" }}>
              <span style={{ whiteSpace: "nowrap" }}>
                Sản phẩm <span style={{ color: "red" }}>*</span>
              </span>
              <div>
                <select
                  className="input"
                  value={formData.productId}
                  onChange={(e) => handleChange("productId", e.target.value)}
                  disabled={!isNew}
                >
                  <option value="">
                    {!formData.supplierId && !formData.productId
                      ? "Chọn nhà cung cấp trước"
                      : "Chọn sản phẩm"}
                  </option>
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

            <div className="group" style={{ flex: "1 1 300px" }}>
              <span style={{ whiteSpace: "nowrap" }}>
                Biến thể <span style={{ color: "red" }}>*</span>
              </span>
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

            <div className="group" style={{ flex: "1 1 300px" }}>
              <span style={{ whiteSpace: "nowrap" }}>
                Email tài khoản <span style={{ color: "red" }}>*</span>
              </span>
              <div>
                <input
                  className="input"
                  type="email"
                  value={formData.accountEmail}
                  onChange={(e) => handleChange("accountEmail", e.target.value)}
                  placeholder="email@example.com"
                />
                {errors.accountEmail && (
                  <small style={{ color: "red" }}>{errors.accountEmail}</small>
                )}
              </div>
            </div>

            <div className="group" style={{ flex: "1 1 300px" }}>
              <span>Username (tùy chọn)</span>
              <input
                className="input"
                type="text"
                value={formData.accountUsername}
                onChange={(e) =>
                  handleChange("accountUsername", e.target.value)
                }
                placeholder="username (nếu có)"
              />
            </div>

            <div className="group" style={{ flex: "1 1 300px" }}>
              <span style={{ whiteSpace: "nowrap" }}>
                Mật khẩu {isNew && <span style={{ color: "red" }}>*</span>}
              </span>
              <div>
                <div style={{ position: "relative" }}>
                  <input
                    className="input"
                    style={{ paddingRight: 40 }}
                    type={showPassword ? "text" : "password"}
                    value={
                      showPassword
                        ? actualPassword || formData.accountPassword
                        : formData.accountPassword
                    }
                    onChange={(e) =>
                      handleChange("accountPassword", e.target.value)
                    }
                    placeholder={isNew ? "Mật khẩu" : "Để trống nếu không đổi"}
                    readOnly={showPassword && !isNew}
                  />
                  {!isNew && (
                    <button
                      type="button"
                      onClick={handleShowPassword}
                      aria-label={
                        showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"
                      }
                      title={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                      style={{
                        position: "absolute",
                        right: 8,
                        top: "50%",
                        transform: "translateY(-50%)",
                        width: 28,
                        height: 28,
                        padding: 0,
                        display: "grid",
                        placeItems: "center",
                        background: "transparent",
                        border: "none",
                        color: "#6b7280",
                        cursor: "pointer",
                      }}
                    >
                      {showPassword ? (
                        <svg
                          width="20"
                          height="20"
                          viewBox="0 0 24 24"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="2"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          aria-hidden="true"
                        >
                          <path d="M17.94 17.94A10.94 10.94 0 0 1 12 20c-5 0-9.27-3.11-11-8 1.02-2.9 3.05-5.16 5.59-6.53" />
                          <path d="M1 1l22 22" />
                          <path d="M10.58 10.58a2 2 0 0 0 2.84 2.84" />
                          <path d="M9.88 4.24A10.94 10.94 0 0 1 12 4c5 0 9.27 3.11 11 8a11.44 11.44 0 0 1-4.26 5.18" />
                        </svg>
                      ) : (
                        <svg
                          width="20"
                          height="20"
                          viewBox="0 0 24 24"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="2"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          aria-hidden="true"
                        >
                          <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                          <circle cx="12" cy="12" r="3" />
                        </svg>
                      )}
                    </button>
                  )}
                </div>
                {errors.accountPassword && (
                  <small style={{ color: "red" }}>
                    {errors.accountPassword}
                  </small>
                )}
              </div>
            </div>

            <div className="group" style={{ flex: "1 1 300px" }}>
              <span style={{ whiteSpace: "nowrap" }}>
                Số người dùng tối đa <span style={{ color: "red" }}>*</span>
              </span>
              <div>
                <input
                  className="input"
                  type="number"
                  min="1"
                  max="100"
                  value={formData.maxUsers}
                  onChange={(e) => handleChange("maxUsers", e.target.value)}
                  disabled={selectedProduct?.productType === "PERSONAL_ACCOUNT"}
                />
                {errors.maxUsers && (
                  <small style={{ color: "red" }}>{errors.maxUsers}</small>
                )}
              </div>
            </div>

            {!isNew && (
              <div className="group" style={{ flex: "1 1 300px" }}>
                <span>Trạng thái</span>
                <input
                  className="input"
                  type="text"
                  value={formData.status}
                  readOnly
                  disabled
                />
                <select
                  style={{ display: "none" }}
                  className="input"
                  value={formData.status}
                  disabled={true}
                >
                  <option value="Active">Hoạt động</option>
                  <option value="Full">Đầy</option>
                  <option value="Expired">Hết hạn</option>
                  <option value="Error">Lỗi</option>
                  <option value="Inactive">Không hoạt động</option>
                </select>
              </div>
            )}



            <div className="group" style={{ flex: "1 1 300px" }}>
              <span style={{ whiteSpace: "nowrap" }}>
                Ngày hết hạn <span style={{ color: "red" }}>*</span>
              </span>
              <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                <input
                  className="input"
                  type={!isNew ? "text" : "date"}
                  value={
                    !isNew
                      ? formatVietnameseDate(formData.expiryDate)
                      : formData.expiryDate
                  }
                  onChange={(e) => handleChange("expiryDate", e.target.value)}
                  min={minExpiryDate}
                  disabled={!isNew}
                  style={{ flex: 1 }}
                />
                {!isNew && (
                  <button
                    type="button"
                    className="btn"
                    onClick={() => {
                        const duration = parseInt(selectedVariant?.durationDays ?? 0);
                        let nextDate = "";
                        if (formData.expiryDate && duration > 0) {
                            // Robust local date parsing
                            const parts = formData.expiryDate.split('-');
                            if (parts.length === 3) {
                                const d = new Date(parseInt(parts[0]), parseInt(parts[1]) - 1, parseInt(parts[2]));
                                // Start from visual expiry date (stored + 1)
                                d.setDate(d.getDate() + 1 + duration);
                                nextDate = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
                            }
                        }
                        setExtendDialog({
                            isOpen: true,
                            newExpiryDate: nextDate || formData.expiryDate || "",
                        });
                    }}
                    title="Gia hạn tài khoản"
                  >
                    Gia hạn
                  </button>
                )}
              </div>
              {errors.expiryDate && (
                <small style={{ color: "red" }}>{errors.expiryDate}</small>
              )}
            </div>

            <div className="group" style={{ flex: "1 1 300px" }}>
              <span style={{ whiteSpace: "nowrap" }}>
                Giá nhập <span style={{ color: "red" }}>*</span>
              </span>
              <div>
                <input
                  className="input"
                  type="text"
                  min="0"
                  value={formData.cogsPrice ? Number(formData.cogsPrice).toLocaleString('vi-VN') : ''}
                  onChange={(e) => {
                      const val = e.target.value.replace(/\./g, "").replace(/[^0-9]/g, "");
                      handleChange("cogsPrice", val);
                  }}
                  placeholder="Nhập giá vốn (COGS)"
                  disabled={!isNew}
                  required={isNew}
                />
                {errors.cogsPrice && (
                  <small style={{ color: "red" }}>{errors.cogsPrice}</small>
                )}
              </div>
            </div>

            <div className="group" style={{ flex: "1 1 100%" }}>
              <span>Ghi chú</span>
              <div>
                <textarea
                  className="textarea"
                  value={formData.notes}
                  onChange={(e) => handleChange("notes", e.target.value)}
                  placeholder="Ghi chú thêm về tài khoản..."
                  rows={4}
                />
                {errors.notes && (
                  <small style={{ color: "red" }}>{errors.notes}</small>
                )}
              </div>
            </div>
          </div>

          {!isNew && accountInfo && (
            <div
              style={{
                marginTop: 16,
                padding: 12,
                background: "#f8fafc",
                borderRadius: 8,
              }}
            >
              <h3 style={{ marginTop: 0 }}>Thông tin bổ sung</h3>
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
                  gap: 12,
                }}
              >
                <div>
                  <small className="muted">Trạng thái hiện tại:</small>
                  <div>{getAccountStatusLabel(accountInfo.status)}</div>
                </div>
                <div>
                  <small className="muted">Slot sử dụng:</small>
                  <div>
                    {accountInfo.currentUsers}/{accountInfo.maxUsers}
                  </div>
                </div>
                <div>
                  <small className="muted">Ngày tạo:</small>
                  <div>{formatDateTime(accountInfo.createdAt)}</div>
                </div>
                {accountInfo.updatedAt && (
                  <div>
                    <small className="muted">Cập nhật lần cuối:</small>
                    <div>{formatDateTime(accountInfo.updatedAt)}</div>
                  </div>
                )}
              </div>

              {accountInfo.customers && accountInfo.customers.length > 0 && (
                <div style={{ marginTop: 16 }}>
                  <h4>Khách hàng đang sử dụng</h4>
                  <div style={{ overflowX: "auto" }}>
                    <table className="table">
                      <thead>
                        <tr>
                          <th>Tên</th>
                          <th>Email</th>
                          <th>Ngày thêm</th>
                          <th>Trạng thái</th>
                        </tr>
                      </thead>
                      <tbody>
                        {accountInfo.customers
                          .filter((c) => c.isActive)
                          .map((customer) => (
                            <tr key={customer.productAccountCustomerId}>
                              <td>{customer.userFullName || "—"}</td>
                              <td>{customer.userEmail}</td>
                              <td>
                                {new Date(customer.addedAt).toLocaleDateString(
                                  "vi-VN"
                                )}
                              </td>
                              <td>
                                <span
                                  style={{
                                    padding: "2px 8px",
                                    borderRadius: "4px",
                                    fontSize: "12px",
                                    background: "#d1fae5",
                                    color: "#065f46",
                                  }}
                                >
                                  Đang dùng
                                </span>
                              </td>
                            </tr>
                          ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Full users list in product account */}
          {!isNew && accountInfo && (
            <section
              className="card"
              style={{ marginTop: 16, padding: "10px" }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <h3 style={{ margin: 0, flex: 1 }}>
                  Người dùng trong tài khoản
                </h3>
                {/* <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                  <input
                    className="input"
                    placeholder="Nhập email/tên để tìm kiếm"
                    value={userSearchTerm}
                    onChange={(e) => setUserSearchTerm(e.target.value)}
                    style={{ width: 220 }}
                  />
                  <button
                    type="button"
                    className="btn"
                    onClick={searchUsers}
                    disabled={userSearchLoading}
                  >
                    {userSearchLoading ? "Đang tìm..." : "Tìm"}
                  </button>
                  <select
                    className="input"
                    value={selectedUserId}
                    onChange={(e) => setSelectedUserId(e.target.value)}
                    style={{ width: 220 }}
                  >
                    <option value="">Chọn người dùng</option>
                    {userSearchResults.map((u) => (
                      <option key={u.userId} value={u.userId}>
                        {(u.fullName || u.username || "").trim() || u.email} -{" "}
                        {u.email}
                      </option>
                    ))}
                  </select>
                  <button
                    type="button"
                    className="btn primary"
                    onClick={handleAddUserToAccount}
                  >
                    Thêm
                  </button>
                </div> */}
              </div>
              <div style={{ overflowX: "auto" }}>
                <table className="table">
                  <thead>
                    <tr>
                      <th>Tên</th>
                      <th>Email</th>
                      <th>Ngày thêm</th>
                      <th>Ngày gỡ</th>
                      <th>Trạng thái</th>
                      <th>Ghi chú</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {(accountInfo.customers || []).map((c) => (
                      <tr key={c.productAccountCustomerId}>
                        <td>{c.userFullName || "-"}</td>
                        <td>{c.userEmail}</td>
                        <td>{c.addedAt ? formatDateTime(c.addedAt) : "-"}</td>
                        <td>
                          {c.removedAt ? formatDateTime(c.removedAt) : "-"}
                        </td>
                        <td>
                          {c.isActive ? (
                            <span
                              style={{
                                padding: "2px 8px",
                                borderRadius: 4,
                                background: "#d1fae5",
                                color: "#065f46",
                                fontSize: 12,
                              }}
                            >
                              Đang dùng
                            </span>
                          ) : (
                            <span
                              style={{
                                padding: "2px 8px",
                                borderRadius: 4,
                                background: "#e5e7eb",
                                color: "#374151",
                                fontSize: 12,
                              }}
                            >
                              Đã gỡ
                            </span>
                          )}
                        </td>
                        <td>{c.notes || "-"}</td>
                        <td>
                          {c.isActive ? (
                            <button
                              type="button"
                              className="btn"
                              onClick={() =>
                                handleRemoveUserFromAccount(c.userId)
                              }
                            >
                              Xóa
                            </button>
                          ) : (
                            <span className="muted">-</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}

          {/* History with paging and sort by created date */}
          {!isNew && (
            <section
              className="card "
              style={{ marginTop: 16, padding: "10px" }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <h3 style={{ margin: 0, flex: 1 }}>Lịch sử</h3>
            <div className="group" style={{ display: "flex", alignItems: "center", gap: 8 }}>
              Kích thước trang:
                  <select
                    className="input"
                    value={historyPageSize}
                    onChange={(e) => {
                      setHistoryPageSize(parseInt(e.target.value, 10));
                      setHistoryPage(1);
                    }}
                    style={{ width: 80 }}
                  >
                    <option value={10}>10</option>
                    <option value={20}>20</option>
                    <option value={50}>50</option>
                  </select>
              </div>
                <button
                  type="button"
                  className="btn"
                  onClick={() =>
                    setHistorySort((s) => (s === "desc" ? "asc" : "desc"))
                  }
                  title={historySort === "desc" ? "Mới nhất" : "Cũ nhất"}
                >
                  Sắp xếp: {historySort === "desc" ? "Mới nhất" : "Cũ nhất"}
                </button>
              </div>
              <div style={{ marginTop: 12 }}>
                {historyLoading ? (
                  <div className="muted">Đang tải lịch sử...</div>
                ) : totalHistory === 0 ? (
                  <div className="muted">Chưa có lịch sử</div>
                ) : (
                  <div style={{ overflowX: "auto" }}>
                    <table className="table">
                      <thead>
                        <tr>
                          <th>Thời gian</th>
                          <th>Người dùng</th>
                          <th>Email</th>
                          <th>Ghi chú</th>
                        </tr>
                      </thead>
                      <tbody>
                        {pagedHistory.map((h, idx) => (
                          <tr key={h.historyId || idx}>
                            <td>{formatDateTime(h.actionAt || h.ActionAt)}</td>
                            <td>{h.userFullName || h.UserFullName || "-"}</td>
                            <td>{h.userEmail || h.UserEmail || "-"}</td>
                            <td>{h.notes || h.Notes || "-"}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
              {totalHistory > 0 && (
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 12,
                    marginTop: 8,
                  }}
                >
                  <button
                    type="button"
                    className="btn"
                    onClick={() => setHistoryPage((p) => Math.max(1, p - 1))}
                    disabled={historyPage === 1}
                  >
                    Trước
                  </button>
                  <span className="muted">
                    Trang {historyPage} /{" "}
                    {Math.max(1, Math.ceil(totalHistory / historyPageSize))}
                  </span>
                  <button
                    type="button"
                    className="btn"
                    onClick={() =>
                      setHistoryPage((p) =>
                        Math.min(
                          Math.ceil(totalHistory / historyPageSize),
                          p + 1
                        )
                      )
                    }
                    disabled={
                      historyPage >= Math.ceil(totalHistory / historyPageSize)
                    }
                  >
                    Tiếp
                  </button>
                </div>
              )}
            </section>
          )}

          <div style={{ display: "flex", gap: 8, marginTop: 16 }}>
            <button type="submit" className="btn primary" disabled={saving}>
              {saving ? "Đang lưu..." : "Lưu"}
            </button>
            <button
              type="button"
              className="btn"
              onClick={() => navigate("/accounts")}
            >
              Hủy
            </button>
            {!isNew && accountInfo?.currentUsers === 0 && (
              <button
                type="button"
                className="btn"
                onClick={handleDelete}
                style={{ marginLeft: "auto", color: "#dc2626" }}
              >
                Xóa tài khoản
              </button>
            )}
          </div>
        </form>
      </section>
    </div>
  );
}
