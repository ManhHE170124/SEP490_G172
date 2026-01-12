import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import ImageUploader from "../../components/ImageUploader/ImageUploader";
import profileService from "../../services/profile";
import { orderApi } from "../../services/orderApi";
import { paymentApi } from "../../services/paymentApi";
import "./UserProfilePage.css";

const SECTION_ITEMS = [
  { id: "profile-overview", label: "Tổng quan" },
  { id: "profile-orders", label: "Lịch sử đơn hàng" },
  { id: "profile-transactions", label: "Lịch sử giao dịch" },
  { id: "profile-security", label: "Mật khẩu & Bảo mật" },
  { id: "profile-details", label: "Cập nhật thông tin" },
];

const INITIAL_ORDER_FILTERS = {
  keyword: "",
  fromDate: "",
  toDate: "",
  minAmount: "",
  maxAmount: "",
  status: "all",
};
const INITIAL_TRANSACTION_FILTERS = {
  keyword: "",
  fromDate: "",
  toDate: "",
  status: "",
  type: "",
  minAmount: "",
  maxAmount: "",
};

const currencyFormatter = new Intl.NumberFormat("vi-VN", {
  style: "currency",
  currency: "VND",
  maximumFractionDigits: 0,
});
const numberFormatter = new Intl.NumberFormat("vi-VN");

const unwrapData = (payload) =>
  payload?.data !== undefined ? payload.data : payload;

const extractList = (payload) => {
  const unwrapped = unwrapData(payload);
  if (!unwrapped) return [];
  if (Array.isArray(unwrapped)) return unwrapped;
  if (Array.isArray(unwrapped.items)) return unwrapped.items;
  if (Array.isArray(unwrapped.results)) return unwrapped.results;
  if (Array.isArray(unwrapped.data)) return unwrapped.data;
  return [];
};

const sanitizeFilters = (filters = {}) =>
  Object.fromEntries(
    Object.entries(filters).filter(
      ([, value]) => value !== null && value !== undefined && value !== ""
    )
  );

const maskEmail = (email = "") => {
  if (!email.includes("@")) return email;
  const [alias, domain] = email.split("@");
  if (alias.length <= 2) return `***@${domain}`;
  return `${alias.slice(0, 2)}***@${domain}`;
};

const getInitials = (name = "") => {
  const parts = name.trim().split(/\s+/).filter(Boolean).slice(-2);
  if (!parts.length) return "KV";
  return parts.map((part) => part[0]?.toUpperCase()).join("");
};

const formatDate = (value, fallback = "—") => {
  if (!value) return fallback;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return fallback;
  return date.toLocaleDateString("vi-VN");
};

const formatDateTime = (value, fallback = "—") => {
  if (!value) return fallback;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return fallback;
  return date.toLocaleString("vi-VN");
};

const formatMoney = (value) => {
  if (value === null || value === undefined || value === "") return "—";
  const numeric = Number(value);
  if (Number.isNaN(numeric)) return "—";
  return currencyFormatter.format(numeric).replace(/\s?₫/, "đ");
};

const getStatusTone = (status = "") => {
  const text = status.toString().toLowerCase().trim();
  // Chỉ có 2 trạng thái: Paid (success) và Cancelled (danger)
  if (text === "paid") return "success";
  if (text === "cancelled") return "danger";
  // Mặc định là danger (Đã hủy)
  return "danger";
};

const getStatusLabel = (status = "") => {
  const normalized = status.toString().toLowerCase().trim();
  // Chỉ hiển thị 2 trạng thái: Đã thanh toán và Đã hủy
  if (normalized === "paid") return "Đã thanh toán";
  if (normalized === "cancelled") return "Đã hủy";
  // Mặc định là "Đã hủy" nếu không khớp
  return "Đã hủy";
};

// Helper functions for transaction/payment history
const formatVnd = (n) => {
  const num = Number(n || 0);
  return new Intl.NumberFormat("vi-VN").format(num) + " đ";
};

const normalizeText = (v) => String(v ?? "").trim();

const getPaymentId = (p) => p?.paymentId ?? p?.PaymentId ?? p?.id ?? p?.Id ?? "";
const getPaymentCreatedAt = (p) =>
  p?.createdAt ?? p?.CreatedAt ?? p?.createdTime ?? p?.createdDate ?? p?.createdOn ?? null;
const getPaymentAmount = (p) => p?.amount ?? p?.Amount ?? p?.totalAmount ?? p?.paidAmount ?? 0;
const getPaymentStatus = (p) => p?.status ?? p?.Status ?? p?.paymentStatus ?? p?.PaymentStatus ?? "Unknown";

const getTargetType = (p) => {
  const t =
    p?.transactionType ??
    p?.TransactionType ??
    p?.targetType ??
    p?.TargetType ??
    p?.paymentTarget ??
    p?.target ??
    p?.purpose ??
    p?.type ??
    "";
  const tt = normalizeText(t);
  if (tt) return tt;

  if (p?.orderId || p?.OrderId) return "Order";
  if (p?.supportPlanId || p?.supportSubscriptionId || p?.subscriptionId) return "SupportPlan";
  return "Unknown";
};

const mapTargetToUi = (t) => {
  const v = normalizeText(t).toLowerCase();
  if (v === "order" || v === "donhang" || v === "đơn hàng") {
    return { label: "Đơn hàng", cls: "type-order", value: "Order" };
  }
  if (
    v === "supportplan" ||
    v === "plan" ||
    v === "subscription" ||
    v === "support" ||
    v === "goiho tro" ||
    v === "gói hỗ trợ"
  ) {
    return { label: "Gói hỗ trợ", cls: "type-support", value: "SupportPlan" };
  }
  return { label: "Không rõ", cls: "payment-unknown", value: "Unknown" };
};

const mapPaymentStatusToUi = (s) => {
  const v = normalizeText(s).toLowerCase();

  if (v === "pending") return { label: "Chờ thanh toán", cls: "payment-pending", value: "Pending" };
  if (v === "paid" || v === "success" || v === "completed")
    return { label: "Đã thanh toán", cls: "payment-paid", value: "Paid" };

  if (v === "cancelledbytimeout" || v === "timeout")
    return { label: "Hủy do quá hạn", cls: "payment-timeout", value: "CancelledByTimeout" };

  if (v === "cancelled") return { label: "Đã hủy", cls: "payment-cancelled", value: "Cancelled" };
  if (v === "failed") return { label: "Thất bại", cls: "payment-failed", value: "Failed" };
  if (v === "refunded") return { label: "Đã hoàn tiền", cls: "payment-refunded", value: "Refunded" };

  return { label: "Không rõ", cls: "payment-unknown", value: "Unknown" };
};

const fmtDateTime = (d) => {
  if (!d) return "—";
  const dt = new Date(d);
  if (Number.isNaN(dt.getTime())) return "—";
  return new Intl.DateTimeFormat("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(dt);
};

// Icon components for transaction history
const Ico = {
  Filter: (p) => (
    <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" {...p}>
      <path
        fill="currentColor"
        d="M3 5a1 1 0 0 1 1-1h16a1 1 0 0 1 .8 1.6L14 13.5V20a1 1 0 0 1-1.447.894l-3-1.5A1 1 0 0 1 9 18.5v-5L3.2 5.6A1 1 0 0 1 3 5z"
      />
    </svg>
  ),
  Refresh: (p) => (
    <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" {...p}>
      <path
        fill="currentColor"
        d="M20 12a8 8 0 0 1-14.314 4.906l-1.43 1.43A1 1 0 0 1 2.5 17.5V13a1 1 0 0 1 1-1H8a1 1 0 0 1 .707 1.707L7.19 15.224A6 6 0 1 0 6 12a1 1 0 1 1-2 0 8 8 0 1 1 16 0z"
      />
    </svg>
  ),
  Eye: (p) => (
    <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" {...p}>
      <path
        fill="currentColor"
        d="M12 5c5.5 0 9.5 4.5 10.8 6.2a1.5 1.5 0 0 1 0 1.6C21.5 14.5 17.5 19 12 19S2.5 14.5 1.2 12.8a1.5 1.5 0 0 1 0-1.6C2.5 9.5 6.5 5 12 5zm0 2c-4.2 0-7.6 3.4-8.7 5 1.1 1.6 4.5 5 8.7 5s7.6-3.4 8.7-5c-1.1-1.6-4.5-5-8.7-5zm0 2.5A2.5 2.5 0 1 1 9.5 12 2.5 2.5 0 0 1 12 9.5z"
      />
    </svg>
  ),
  X: (p) => (
    <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" {...p}>
      <path
        fill="currentColor"
        d="M18.3 5.7a1 1 0 0 1 0 1.4L13.4 12l4.9 4.9a1 1 0 1 1-1.4 1.4L12 13.4l-4.9 4.9a1 1 0 0 1-1.4-1.4l4.9-4.9-4.9-4.9a1 1 0 0 1 1.4-1.4l4.9 4.9 4.9-4.9a1 1 0 0 1 1.4 0z"
      />
    </svg>
  ),
  Up: (p) => (
    <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" {...p}>
      <path fill="currentColor" d="M12 8l6 6H6l6-6z" />
    </svg>
  ),
  Down: (p) => (
    <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" {...p}>
      <path fill="currentColor" d="M12 16l-6-6h12l-6 6z" />
    </svg>
  ),
};

const InlineNotice = ({ notice }) => {
  if (!notice?.message) return null;
  return (
    <div className={`profile-inline-alert ${notice.type ?? "success"}`}>
      {notice.message}
    </div>
  );
};

const UserProfilePage = () => {
  const navigate = useNavigate();
  
  const storedUser = useMemo(() => {
    try {
      const raw = localStorage.getItem("user");
      if (!raw) return null;
      const parsed = JSON.parse(raw);
      return parsed?.profile ?? parsed;
    } catch {
      return null;
    }
  }, []);

  const [profile, setProfile] = useState(storedUser);
  const [allOrders, setAllOrders] = useState([]); // Tất cả orders từ BE
  const [orders, setOrders] = useState([]); // Orders đã filter/sort/paginate
  const [transactions, setTransactions] = useState([]);

  const [loadingProfile, setLoadingProfile] = useState(true);
  const [pageError, setPageError] = useState("");

  const [orderLoading, setOrderLoading] = useState(true);
  const [transactionLoading, setTransactionLoading] = useState(true);
  const [orderError, setOrderError] = useState("");
  const [transactionError, setTransactionError] = useState("");

  const [savingProfile, setSavingProfile] = useState(false);
  const [changingPassword, setChangingPassword] = useState(false);
  const [twoFactorUpdating, setTwoFactorUpdating] = useState(false);

  const [profileNotice, setProfileNotice] = useState(null);
  const [passwordNotice, setPasswordNotice] = useState(null);
  const [securityNotice, setSecurityNotice] = useState(null);
  const [avatarUploading, setAvatarUploading] = useState(false);

  const [profileForm, setProfileForm] = useState({
    fullName: storedUser?.fullName ?? storedUser?.displayName ?? "",
    phone: storedUser?.phone ?? "",
    address: storedUser?.address ?? "",
    avatarUrl:
      storedUser?.avatarUrl ??
      storedUser?.avatar ??
      storedUser?.avatarURL ??
      "",
  });

  const [passwordForm, setPasswordForm] = useState({
    currentPassword: "",
    newPassword: "",
    confirmPassword: "",
  });

  const [orderFilters, setOrderFilters] = useState(INITIAL_ORDER_FILTERS);
  const [orderKeywordInput, setOrderKeywordInput] = useState(""); // Input value (chưa debounce)
  const [orderPage, setOrderPage] = useState(1);
  const [orderTotalPages, setOrderTotalPages] = useState(1);
  const [orderSortBy, setOrderSortBy] = useState("createdAt"); // Mặc định sort theo ngày tạo
  const [orderSortDir, setOrderSortDir] = useState("desc"); // Mặc định giảm dần (mới nhất trước)

  // Transaction history state
  const [transactionFilters, setTransactionFilters] = useState(INITIAL_TRANSACTION_FILTERS);
  const [transactionKeywordInput, setTransactionKeywordInput] = useState(""); // Input value (chưa debounce)
  const [transactionSortBy, setTransactionSortBy] = useState("createdAt"); // null = không sort (TH1)
  const [transactionSortDir, setTransactionSortDir] = useState("desc");
  const [transactionPage, setTransactionPage] = useState(1);
  const [transactionItems, setTransactionItems] = useState([]);
  const [transactionTotal, setTransactionTotal] = useState(0);
  const [allTransactionsTotal, setAllTransactionsTotal] = useState(0);

  const [activeSection, setActiveSection] = useState(SECTION_ITEMS[0].id);

  useEffect(() => {
    if (!profile) return;
    setProfileForm({
      fullName:
        profile.fullName || profile.displayName || profile.username || "",
      phone: profile.phone || profile.phoneNumber || "",
      address: profile.address || "",
      avatarUrl:
        profile.avatarUrl ||
        profile.avatar ||
        profile.avatarURL ||
        profile.avatarUrlProfile ||
        "",
    });
  }, [profile]);

  const loadProfile = useCallback(async () => {
    setLoadingProfile(true);
    setPageError("");
    try {
      const response = await profileService.getProfile();
      setProfile(unwrapData(response));
    } catch (error) {
      const message =
        error?.response?.data?.message ||
        error?.message ||
        "Không thể tải thông tin tài khoản.";
      setPageError(message);
    } finally {
      setLoadingProfile(false);
    }
  }, []);

  const syncProfileCache = useCallback((updatedProfile) => {
    try {
      const cached = localStorage.getItem("user");
      if (!cached) return;
      const parsed = JSON.parse(cached);
      const nextProfile = {
        ...(parsed?.profile || {}),
        ...updatedProfile,
      };
      const nextStored = { ...parsed, ...nextProfile, profile: nextProfile };
      localStorage.setItem("user", JSON.stringify(nextStored));
    } catch (error) {
      console.warn("Không thể đồng bộ cache người dùng", error);
    }
  }, []);

  // Tạo userId một lần dựa trên profile/storedUser
  const userId = useMemo(() => {
    return (
      profile?.userId ||
      profile?.id ||
      storedUser?.userId ||
      storedUser?.id ||
      null
    );
  }, [profile, storedUser]);

  // Lấy tất cả orders từ BE một lần
  const fetchAllOrders = useCallback(async (currentUserId) => {
    if (!currentUserId) {
      setOrderError("Không tìm thấy thông tin người dùng.");
      setOrderLoading(false);
      return;
    }

    setOrderLoading(true);
    setOrderError("");
    try {
      const response = await orderApi.history(currentUserId);
      const list = extractList(response);
      setAllOrders(list);
    } catch (error) {
      const message =
        error?.response?.data?.message ||
        error?.message ||
        "Không thể tải lịch sử đơn hàng.";
      setOrderError(message);
    } finally {
      setOrderLoading(false);
    }
  }, []);

  // Filter, sort, paginate orders ở client-side
  const filteredAndPaginatedOrders = useMemo(() => {
    let filtered = [...allOrders];

    // Filter theo keyword (search trong OrderNumber)
    if (orderFilters.keyword?.trim()) {
      const keyword = orderFilters.keyword.trim().toLowerCase();
      filtered = filtered.filter((order) => {
        const orderNumber = (order.orderNumber || "").toLowerCase();
        return orderNumber.includes(keyword);
      });
    }

    // Filter theo minAmount
    if (orderFilters.minAmount) {
      const minAmount = Number(orderFilters.minAmount);
      if (!Number.isNaN(minAmount)) {
        filtered = filtered.filter((order) => {
          const finalAmount = order.finalAmount ?? order.totalAmount ?? 0;
          return finalAmount >= minAmount;
        });
      }
    }

    // Filter theo maxAmount
    if (orderFilters.maxAmount) {
      const maxAmount = Number(orderFilters.maxAmount);
      if (!Number.isNaN(maxAmount)) {
        filtered = filtered.filter((order) => {
          const finalAmount = order.finalAmount ?? order.totalAmount ?? 0;
          return finalAmount <= maxAmount;
        });
      }
    }

    // Filter theo fromDate
    if (orderFilters.fromDate) {
      const fromDate = new Date(orderFilters.fromDate);
      fromDate.setHours(0, 0, 0, 0);
      if (!Number.isNaN(fromDate.getTime())) {
        filtered = filtered.filter((order) => {
          const orderDate = new Date(order.createdAt);
          orderDate.setHours(0, 0, 0, 0);
          return orderDate >= fromDate;
        });
      }
    }

    // Filter theo toDate
    if (orderFilters.toDate) {
      const toDate = new Date(orderFilters.toDate);
      toDate.setHours(23, 59, 59, 999);
      if (!Number.isNaN(toDate.getTime())) {
        filtered = filtered.filter((order) => {
          const orderDate = new Date(order.createdAt);
          return orderDate <= toDate;
        });
      }
    }

    // Filter theo status
    if (orderFilters.status && orderFilters.status !== "all") {
      filtered = filtered.filter(
        (order) => order.status?.toLowerCase() === orderFilters.status.toLowerCase()
      );
    }

    // Sort - chỉ sort khi có orderSortBy (không null)
    if (orderSortBy && orderSortDir) {
      const sortBy = orderSortBy;
      const asc = orderSortDir === "asc";

      filtered.sort((a, b) => {
        let aVal, bVal;

        switch (sortBy.toLowerCase()) {
          case "ordernumber":
            aVal = a.orderNumber || "";
            bVal = b.orderNumber || "";
            return asc
              ? aVal.localeCompare(bVal)
              : bVal.localeCompare(aVal);

          case "totalamount":
            aVal = a.finalAmount ?? a.totalAmount ?? 0;
            bVal = b.finalAmount ?? b.totalAmount ?? 0;
            return asc ? aVal - bVal : bVal - aVal;

          case "status":
            aVal = a.status || "";
            bVal = b.status || "";
            return asc
              ? aVal.localeCompare(bVal)
              : bVal.localeCompare(aVal);

          default: // createdAt
            aVal = new Date(a.createdAt).getTime();
            bVal = new Date(b.createdAt).getTime();
            return asc ? aVal - bVal : bVal - aVal;
        }
      });
    }

    // Tính total pages
    const pageSize = 5;
    const total = filtered.length;
    const totalPages = pageSize > 0 ? Math.max(1, Math.ceil(total / pageSize)) : 1;
    setOrderTotalPages(totalPages);

    // Paginate
    const page = orderPage || 1;
    const startIndex = (page - 1) * pageSize;
    const endIndex = startIndex + pageSize;
    const paginated = filtered.slice(startIndex, endIndex);

    return { items: paginated, total, totalPages };
  }, [
    allOrders,
    orderFilters,
    orderSortBy,
    orderSortDir,
    orderPage,
  ]);

  const fetchTransactions = useCallback(async () => {
      setTransactionError("");
    setTransactionLoading(true);
    try {
      const params = {
        search: transactionFilters.keyword || undefined,
        createdFrom: transactionFilters.fromDate || undefined,
        createdTo: transactionFilters.toDate || undefined,
        paymentStatus: transactionFilters.status || undefined,
        transactionType: transactionFilters.type || undefined,
        amountFrom: transactionFilters.minAmount || undefined,
        amountTo: transactionFilters.maxAmount || undefined,
        sortBy: transactionSortBy || "createdAt",
        sortDir: transactionSortDir || "desc",
        pageIndex: transactionPage,
        pageSize: 10,
      };

      const paged = await paymentApi.listCustomerPaged(params);
      setTransactionItems(Array.isArray(paged.items) ? paged.items : []);
      setTransactionTotal(Number(paged.totalItems || 0));
    } catch (e) {
      setTransactionItems([]);
      setTransactionTotal(0);
      setTransactionError(e?.message || "Không tải được danh sách giao dịch.");
      } finally {
        setTransactionLoading(false);
      }
  }, [transactionFilters, transactionSortBy, transactionSortDir, transactionPage]);

  const fetchAllTransactionsCount = useCallback(async () => {
    try {
      const params = {
        pageIndex: 1,
        pageSize: 1, // Only need totalItems, not the actual items
      };
      const paged = await paymentApi.listCustomerPaged(params);
      setAllTransactionsTotal(Number(paged.totalItems || 0));
    } catch (e) {
      // Silently fail - we'll just show 0 or use other fallback
      setAllTransactionsTotal(0);
    }
  }, []);

  useEffect(() => {
    loadProfile();
  }, [loadProfile]);

  // Load tất cả orders một lần khi userId thay đổi
  useEffect(() => {
    if (userId) {
      fetchAllOrders(userId);
    }
  }, [fetchAllOrders, userId]);

  // Cập nhật orders từ filteredAndPaginatedOrders
  useEffect(() => {
    setOrders(filteredAndPaginatedOrders.items || []);
  }, [filteredAndPaginatedOrders]);

  // Debounce keyword input (300ms)
  useEffect(() => {
    const timer = setTimeout(() => {
      setOrderFilters((prev) => ({ ...prev, keyword: orderKeywordInput }));
      setOrderPage(1); // Reset về trang 1 khi search
    }, 300);

    return () => clearTimeout(timer);
  }, [orderKeywordInput]);

  // Load transactions when filters/sort/page change
  useEffect(() => {
    fetchTransactions();
  }, [fetchTransactions]);

  // Fetch total transaction count once on mount
  useEffect(() => {
    fetchAllTransactionsCount();
  }, [fetchAllTransactionsCount]);

  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            setActiveSection(entry.target.id);
          }
        });
      },
      { threshold: 0.3, rootMargin: "-35% 0px -35% 0px" }
    );

    const elements = SECTION_ITEMS.map((item) =>
      document.getElementById(item.id)
    ).filter(Boolean);

    elements.forEach((element) => observer.observe(element));
    return () => observer.disconnect();
  }, []);

  const handleMenuClick = (event, id) => {
    event.preventDefault();
    const element = document.getElementById(id);
    if (element) {
      element.scrollIntoView({ behavior: "smooth", block: "start" });
      setActiveSection(id);
    }
  };

  const handleProfileInput = (event) => {
    const { name, value } = event.target;
    setProfileForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleAvatarChange = (url) => {
    setProfileForm((prev) => ({ ...prev, avatarUrl: url || "" }));
  };

  const handleAvatarError = (message) => {
    setProfileNotice({
      type: "error",
      message: message || "Không thể tải ảnh đại diện. Vui lòng thử lại.",
    });
  };

  const handleAvatarUploading = (state) => setAvatarUploading(state);

  const handlePasswordInput = (event) => {
    const { name, value } = event.target;
    setPasswordForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleOrderFilterChange = (event) => {
    const { name, value } = event.target;
    if (name === "keyword") {
      // Keyword được xử lý riêng với debounce
      setOrderKeywordInput(value);
    } else {
      setOrderFilters((prev) => ({ ...prev, [name]: value }));
      // Reset về trang 1 khi thay đổi filter
      setOrderPage(1);
    }
  };


  const handleProfileSubmit = async (event) => {
    event.preventDefault();
    setProfileNotice(null);
    setSavingProfile(true);
    try {
      const payload = {
        fullName: profileForm.fullName?.trim() || "",
        phone: profileForm.phone?.trim() || null,
        address: profileForm.address?.trim() || null,
        avatarUrl: profileForm.avatarUrl || null,
      };

      if (payload.phone && !/^0(3|5|7|8|9)[0-9]{8}$/.test(payload.phone)) {
         setProfileNotice({
          type: "error",
          message: "Số điện thoại không hợp lệ (phải là số VN 10 chữ số, bắt đầu bằng 03, 05, 07, 08, 09)",
        });
        setSavingProfile(false);
        return;
      }

      const response = await profileService.updateProfile(payload);
      const updated = unwrapData(response) ?? payload;
      const mergedProfile = {
        ...(profile || {}),
        ...payload,
        ...updated,
      };
      setProfile(mergedProfile);
      syncProfileCache(mergedProfile);
      window.dispatchEvent(new Event("profile-updated"));
      setProfileNotice({
        type: "success",
        message: "Cập nhật thông tin cá nhân thành công.",
      });
    } catch (error) {
      setProfileNotice({
        type: "error",
        message:
          error?.response?.data?.message ||
          error?.message ||
          "Không thể cập nhật thông tin.",
      });
    } finally {
      setSavingProfile(false);
    }
  };

  const handlePasswordSubmit = async (event) => {
    event.preventDefault();
    setPasswordNotice(null);
    if (!passwordForm.currentPassword || !passwordForm.newPassword) {
      setPasswordNotice({
        type: "error",
        message: "Vui lòng nhập đầy đủ thông tin.",
      });
      return;
    }
    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      setPasswordNotice({
        type: "error",
        message: "Mật khẩu xác nhận chưa khớp.",
      });
      return;
    }
    setChangingPassword(true);
    try {
      await profileService.changePassword({
        currentPassword: passwordForm.currentPassword,
        newPassword: passwordForm.newPassword,
        confirmPassword: passwordForm.confirmPassword,
      });
      setPasswordForm({
        currentPassword: "",
        newPassword: "",
        confirmPassword: "",
      });
      setPasswordNotice({
        type: "success",
        message: "Đổi mật khẩu thành công.",
      });
    } catch (error) {
      setPasswordNotice({
        type: "error",
        message:
          error?.response?.data?.message ||
          error?.message ||
          "Không thể đổi mật khẩu.",
      });
    } finally {
      setChangingPassword(false);
    }
  };

  const handleTwoFactorToggle = async () => {
    if (!profile) return;
    const enabled =
      profile?.security?.twoFactorEnabled ?? profile?.twoFactorEnabled ?? false;
    setSecurityNotice(null);
    setTwoFactorUpdating(true);
    try {
      await profileService.updateTwoFactor({ enabled: !enabled });
      setProfile((prev) => {
        if (!prev) return prev;
        return {
          ...prev,
          security: {
            ...(prev.security || {}),
            twoFactorEnabled: !enabled,
          },
        };
      });
      setSecurityNotice({
        type: "success",
        message: !enabled
          ? "Đã bật xác thực hai bước."
          : "Đã tắt xác thực hai bước.",
      });
    } catch (error) {
      setSecurityNotice({
        type: "error",
        message:
          error?.response?.data?.message ||
          error?.message ||
          "Không thể cập nhật xác thực hai bước.",
      });
    } finally {
      setTwoFactorUpdating(false);
    }
  };

  const handleScrollToSecurity = () => {
    const element = document.getElementById("profile-security");
    if (element) {
      element.scrollIntoView({ behavior: "smooth", block: "start" });
      setActiveSection("profile-security");
    }
  };

  // Không cần handleApplyOrderFilters vì useEffect đã tự động xử lý
  // Transaction filter handlers
  const handleTransactionFilterChange = (event) => {
    const { name, value } = event.target;
    if (name === "keyword") {
      // Keyword được xử lý riêng với debounce
      setTransactionKeywordInput(value);
    } else {
      setTransactionFilters((prev) => ({ ...prev, [name]: value }));
      // Reset về trang 1 khi thay đổi filter
      setTransactionPage(1);
    }
  };

  const handleTransactionSort = (columnKey) => {
    if (transactionSortBy !== columnKey) {
      // Click vào cột khác: bắt đầu với DESC (TH2)
      setTransactionSortBy(columnKey);
      setTransactionSortDir("desc");
    } else {
      // Click vào cột đang sort: chuyển trạng thái
      if (transactionSortDir === "desc") {
        // TH2 -> TH3 (ASC)
        setTransactionSortDir("asc");
      } else if (transactionSortDir === "asc") {
        // TH3 -> TH1 (không sort)
        setTransactionSortBy(null);
        setTransactionSortDir(null);
      } else {
        // TH1 -> TH2 (DESC) - trường hợp này không nên xảy ra nhưng để an toàn
        setTransactionSortBy(columnKey);
        setTransactionSortDir("desc");
      }
    }
    // Reset về trang 1 khi thay đổi sort
    setTransactionPage(1);
  };

  const handleTransactionPageChange = useCallback((nextPage) => {
    const totalPages = Math.max(1, Math.ceil((transactionTotal || 0) / 10));
    if (nextPage < 1 || nextPage > totalPages) return;
    setTransactionPage(nextPage);
  }, [transactionTotal]);

  const transactionTotalPages = useMemo(
    () => Math.max(1, Math.ceil((transactionTotal || 0) / 10)),
    [transactionTotal]
  );

  const TRANSACTION_STATUS_OPTIONS = useMemo(
    () => [
      { value: "", label: "Tất cả trạng thái" },
      { value: "Paid", label: "Đã thanh toán" },
      { value: "Cancelled", label: "Đã hủy" },
      { value: "Timeout", label: "Hủy do quá hạn" },
      { value: "Refunded", label: "Đã hoàn tiền" },
      { value: "Failed", label: "Thất bại" },
    ],
    []
  );

  const TRANSACTION_TYPE_OPTIONS = useMemo(
    () => [
      { value: "", label: "Tất cả loại" },
      { value: "Order", label: "Đơn hàng" },
      { value: "SupportPlan", label: "Gói hỗ trợ" },
    ],
    []
  );

  const orderCount =
    profile?.stats?.orderCount ?? profile?.ordersCount ?? (allOrders?.length || 0);
  const transactionCount =
    profile?.stats?.transactionCount ??
    profile?.transactionsCount ??
    (allTransactionsTotal || 0);

  const profileName =
    profileForm.fullName ||
    profile?.fullName ||
    profile?.displayName ||
    profile?.username ||
    "Người dùng";
  const avatarUrl =
    profileForm.avatarUrl ||
    profile?.avatarUrl ||
    profile?.avatar ||
    profile?.avatarURL ||
    storedUser?.avatarUrl ||
    storedUser?.avatar ||
    "";
  const email =
    profile?.email ||
    profile?.emailAddress ||
    profile?.mail ||
    storedUser?.email ||
    "";
  const membershipLabel =
    profile?.membership?.label ||
    profile?.membershipLabel ||
    profile?.role?.name ||
    "Thành viên";
  const memberSinceDate =
    profile?.memberSince || profile?.createdAt || profile?.joinDate || null;
  const memberSinceYear = memberSinceDate
    ? (() => {
        const date = new Date(memberSinceDate);
        return Number.isNaN(date.getTime()) ? null : date.getFullYear();
      })()
    : profile?.joinYear || null;
  const twoFactorEnabled =
    profile?.security?.twoFactorEnabled ?? profile?.twoFactorEnabled ?? false;
  const passwordStatus = profile?.security?.passwordUpdatedAt
    ? `Đã đổi ${formatDate(profile.security.passwordUpdatedAt)}`
    : "Đã đặt";

  const maskedEmail = email ? maskEmail(email) : "—";

  const handleViewOrderDetail = (orderId) => {
    // Chuyển hướng đến trang chi tiết đơn hàng
    navigate(`/orderhistory/${orderId}`);
  };

  const orderRows = orders?.map((order, index) => {
    const createdAt = order?.createdAt;
    const orderId = order?.orderId || order?.id;
    const code =
      order?.orderNumber ||
      order?.orderCode ||
      order?.code ||
      orderId ||
      `#${index}`;
    const productNames = Array.isArray(order?.productNames)
      ? order.productNames
      : [];
    let product = "—";
    if (productNames.length === 0) {
      product = "—";
    } else if (productNames.length === 1) {
      product = productNames[0];
    } else {
      const remainingCount = productNames.length - 1;
      product = `${productNames[0]}, +${remainingCount} sản phẩm khác`;
    }
    const total = order?.finalAmount ?? order?.totalAmount ?? order?.total;
    const status = order?.status || "";
    return (
      <tr key={`${code}-${index}`}>
        <td>{formatDate(createdAt)}</td>
        <td>{code}</td>
        <td>{product}</td>
        <td>{formatMoney(total)}</td>
        <td>
          <span className={`profile-pill ${getStatusTone(status)}`}>
            {getStatusLabel(status)}
          </span>
        </td>
        <td>
          <a
            href={`/orderhistory/${orderId}`}
            className="profile-link"
            onClick={(e) => {
              e.preventDefault();
              handleViewOrderDetail(orderId);
            }}
          >
           Chi tiết
          </a>
        </td>
      </tr>
    );
  });

  const handleOrderPageChange = (nextPage) => {
    if (nextPage < 1 || nextPage > orderTotalPages) return;
    setOrderPage(nextPage); // useEffect sẽ tự động fetch
  };

  const handleOrderSort = (columnKey) => {
    if (orderSortBy !== columnKey) {
      // Click vào cột khác: bắt đầu với DESC (TH2)
      setOrderSortBy(columnKey);
      setOrderSortDir("desc");
    } else {
      // Click vào cột đang sort: chuyển trạng thái
      if (orderSortDir === "desc") {
        // TH2 -> TH3 (ASC)
        setOrderSortDir("asc");
      } else if (orderSortDir === "asc") {
        // TH3 -> TH1 (không sort)
        setOrderSortBy(null);
        setOrderSortDir(null);
      } else {
        // TH1 -> TH2 (DESC) - trường hợp này không nên xảy ra nhưng để an toàn
        setOrderSortBy(columnKey);
        setOrderSortDir("desc");
      }
    }
    // Reset về trang 1 khi thay đổi sort
    setOrderPage(1);
  };

  const transactionRows = transactions?.map((transaction, index) => {
    const createdAt =
      transaction?.createdAt ||
      transaction?.createdDate ||
      transaction?.transactionDate;
    const description =
      transaction?.description ||
      transaction?.note ||
      transaction?.title ||
      "—";
    const amount = transaction?.amount || transaction?.value || 0;
    return (
      <tr key={`${description}-${index}`}>
        <td>{formatDateTime(createdAt)}</td>
        <td>{description}</td>
        <td>{formatMoney(amount)}</td>
      </tr>
    );
  });

  return (
    <div className="profile-page">
      <div className="profile-page__container">
        {loadingProfile && (
          <div className="profile-status">
            <span>Đang tải dữ liệu tài khoản...</span>
          </div>
        )}

        {pageError && (
          <div className="profile-status error">
            <span>{pageError}</span>
            <button type="button" className="profile-btn" onClick={loadProfile}>
              Thử lại
            </button>
          </div>
        )}

        <div className="profile-layout">
          <aside className="profile-sidebar">
            <div className="profile-card">
              <div
                style={{
                  display: "flex",
                  gap: "12px",
                  alignItems: "center",
                }}
              >
                <div className="profile-avatar" aria-hidden="true">
                  {avatarUrl ? (
                    <img src={avatarUrl} alt="Ảnh đại diện" />
                  ) : (
                    getInitials(profileName)
                  )}
                </div>
                <div>
                  <div style={{ fontWeight: 700 }}>{profileName}</div>
                  <div className="profile-hint">{maskedEmail}</div>
                </div>
              </div>
              <div
                className="profile-kv"
                style={{ marginTop: "12px", fontWeight: 600 }}
              >
                Tài khoản: {membershipLabel}
              </div>
            </div>

            <nav
              className="profile-menu"
              aria-label="Menu tài khoản"
              role="navigation"
            >
              {SECTION_ITEMS.map((item) => (
                <a
                  key={item.id}
                  href={`#${item.id}`}
                  className={activeSection === item.id ? "active" : ""}
                  onClick={(event) => handleMenuClick(event, item.id)}
                >
                  {item.label}
                </a>
              ))}
            </nav>
          </aside>

          <main className="profile-main">
            <div className="profile-breadcrumb">
              Tài khoản / <strong>Tổng quan</strong>
            </div>

            <section id="profile-overview" className="profile-card">
              <div
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  marginBottom: 12,
                }}
              >
                <h2 style={{ margin: 0 }}>Tổng quan</h2>
                <div className="profile-hint">
                  Vì sự an toàn, hãy sử dụng mật khẩu mạnh.
                </div>
              </div>

              <div className="profile-overview-grid">
                <div className="profile-card" style={{ padding: 16 }}>
                  <div className="profile-overview-meta">
                    <div
                      className="profile-avatar"
                      style={{ width: 80, height: 80, borderRadius: 10 }}
                    >
                      {avatarUrl ? (
                        <img src={avatarUrl} alt="Ảnh đại diện" />
                      ) : (
                        getInitials(profileName)
                      )}
                    </div>
                    <div>
                      <div
                        style={{
                          fontWeight: 700,
                          fontSize: 18,
                          marginBottom: 4,
                        }}
                      >
                        {profileName}
                      </div>
                      <div className="profile-hint">{maskedEmail}</div>
                      <div className="profile-meta-row">
                        <div>{membershipLabel}</div>
                        <div>•</div>
                        <div>
                          Đã tham gia: {memberSinceYear ? memberSinceYear : "—"}
                        </div>
                      </div>
                    </div>
                  </div>

                  <hr
                    style={{
                      border: "none",
                      borderTop: "1px solid var(--profile-line)",
                      margin: "16px 0",
                    }}
                  />

                  <div>
                    <div className="profile-kv">Số liệu</div>
                    <div className="profile-stat-grid">
                      <div className="profile-stat-card">
                        <strong>{numberFormatter.format(orderCount)}</strong>
                        <span className="profile-hint">Đơn hàng</span>
                      </div>
                      <div className="profile-stat-card">
                        <strong>
                          {numberFormatter.format(transactionCount)}
                        </strong>
                        <span className="profile-hint">Giao dịch</span>
                      </div>
                    </div>
                  </div>
                </div>

                {/* <div className="profile-card" style={{ padding: 16 }}>
                  <h4 style={{ margin: "0 0 8px 0" }}>Bảo mật tài khoản</h4>
                  <div className="profile-kv">
                    Mật khẩu: <strong>{passwordStatus}</strong>
                  </div>
                  <div className="profile-kv" style={{ marginBottom: 8 }}>
                    Xác thực 2 bước:{" "}
                    <strong>{twoFactorEnabled ? "Đã bật" : "Chưa bật"}</strong>
                  </div>
                  <div className="profile-actions">
                    <button
                      type="button"
                      className="profile-btn primary"
                      onClick={handleTwoFactorToggle}
                      disabled={twoFactorUpdating}
                    >
                      {twoFactorEnabled ? "Tắt 2 bước" : "Bật 2 bước"}
                    </button>
                    <button
                      type="button"
                      className="profile-btn"
                      onClick={handleScrollToSecurity}
                    >
                      Đổi mật khẩu
                    </button>
                  </div>
                  <InlineNotice notice={securityNotice} />
                </div> */}
              </div>
            </section>

            <section id="profile-orders" className="profile-card">
              <h3 style={{ margin: "0 0 8px 0" }}>Lịch sử đơn hàng</h3>
              <div className="profile-hint" style={{ marginBottom: 12 }}>
                Hiển thị các đơn hàng sản phẩm bạn đã mua.
              </div>

              <div className="profile-orders-filter-container">
                {/* Hàng 1: Tìm kiếm theo mã đơn hàng, Từ ngày, Đến ngày */}
                <div className="profile-orders-filter-row">
                  <div className="profile-filter-group profile-filter-group--wide">
                    <label className="profile-label" htmlFor="orderKeyword">
                      Tìm kiếm theo mã đơn hàng
                    </label>
                    <input
                      id="orderKeyword"
                      className="profile-input"
                      name="keyword"
                      placeholder="VD: ORD-20250101-ABCD"
                      value={orderKeywordInput}
                      onChange={handleOrderFilterChange}
                    />
                  </div>

                  <div className="profile-filter-group">
                    <label className="profile-label" htmlFor="fromDate">
                      Từ ngày
                    </label>
                    <input
                      id="fromDate"
                      className="profile-input"
                      type="date"
                      name="fromDate"
                      value={orderFilters.fromDate}
                      onChange={handleOrderFilterChange}
                    />
                  </div>

                  <div className="profile-filter-group">
                    <label className="profile-label" htmlFor="toDate">
                      Đến ngày
                    </label>
                    <input
                      id="toDate"
                      className="profile-input"
                      type="date"
                      name="toDate"
                      value={orderFilters.toDate}
                      onChange={handleOrderFilterChange}
                    />
                  </div>
                </div>

                {/* Hàng 2: Số tiền từ, Số tiền đến, Trạng thái, nút Đặt lại */}
                <div className="profile-orders-filter-row">
                  <div className="profile-filter-group">
                    <label className="profile-label" htmlFor="minAmount">
                      Số tiền từ
                    </label>
                    <input
                      id="minAmount"
                      className="profile-input"
                      type="number"
                      name="minAmount"
                      placeholder="Tối thiểu"
                      value={orderFilters.minAmount}
                      onChange={handleOrderFilterChange}
                    />
                  </div>

                  <div className="profile-filter-group">
                    <label className="profile-label" htmlFor="maxAmount">
                      Số tiền đến
                    </label>
                    <input
                      id="maxAmount"
                      className="profile-input"
                      type="number"
                      name="maxAmount"
                      placeholder="Tối đa"
                      value={orderFilters.maxAmount}
                      onChange={handleOrderFilterChange}
                    />
                  </div>

                  <div className="profile-filter-group">
                    <label className="profile-label" htmlFor="status">
                      Trạng thái
                    </label>
                    <select
                      id="status"
                      className="profile-input"
                      name="status"
                      value={orderFilters.status}
                      onChange={handleOrderFilterChange}
                    >
                      <option value="all">Tất cả</option>
                      <option value="Paid">Đã thanh toán</option>
                      <option value="Cancelled">Đã hủy</option>
                    </select>
                  </div>

                  <div className="profile-filter-actions">
                    <button
                      type="button"
                      className="profile-btn"
                      disabled={orderLoading}
                      onClick={() => {
                        setOrderFilters(INITIAL_ORDER_FILTERS);
                        setOrderKeywordInput("");
                        setOrderPage(1);
                        setOrderSortBy(null); // TH1: không sort
                        setOrderSortDir(null);
                      }}
                    >
                      Đặt lại
                    </button>
                  </div>
                </div>
              </div>

              <div className="profile-table-wrapper">
                <table className="profile-table" aria-label="Lịch sử đơn hàng">
                  <thead>
                    <tr>
                      <th>
                        <div
                          className="profile-table-sorter"
                          onClick={() => handleOrderSort("createdAt")}
                          onKeyDown={(e) => e.key === "Enter" && handleOrderSort("createdAt")}
                          role="button"
                          tabIndex={0}
                        >
                          Thời gian
                          {orderSortBy === "createdAt" && orderSortDir &&
                            (orderSortDir === "asc" ? " ↑" : " ↓")}
                        </div>
                      </th>
                      <th>
                        <div
                          className="profile-table-sorter"
                          onClick={() => handleOrderSort("orderNumber")}
                          onKeyDown={(e) => e.key === "Enter" && handleOrderSort("orderNumber")}
                          role="button"
                          tabIndex={0}
                        >
                          Mã đơn
                          {orderSortBy === "orderNumber" && orderSortDir &&
                            (orderSortDir === "asc" ? " ↑" : " ↓")}
                        </div>
                      </th>
                      <th>Sản phẩm</th>
                      <th>
                        <div
                          className="profile-table-sorter"
                          onClick={() => handleOrderSort("totalAmount")}
                          onKeyDown={(e) => e.key === "Enter" && handleOrderSort("totalAmount")}
                          role="button"
                          tabIndex={0}
                        >
                          Tổng tiền
                          {orderSortBy === "totalAmount" && orderSortDir &&
                            (orderSortDir === "asc" ? " ↑" : " ↓")}
                        </div>
                      </th>
                      <th>
                        <div
                          className="profile-table-sorter"
                          onClick={() => handleOrderSort("status")}
                          onKeyDown={(e) => e.key === "Enter" && handleOrderSort("status")}
                          role="button"
                          tabIndex={0}
                        >
                          Trạng thái
                          {orderSortBy === "status" && orderSortDir &&
                            (orderSortDir === "asc" ? " ↑" : " ↓")}
                        </div>
                      </th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {orderLoading ? (
                      <tr>
                        <td colSpan={6} className="profile-empty">
                          Đang tải đơn hàng...
                        </td>
                      </tr>
                    ) : orderError ? (
                      <tr>
                        <td colSpan={6} className="profile-empty">
                          {orderError}{" "}
                          <button
                            type="button"
                            className="profile-btn"
                            onClick={() => fetchAllOrders(userId)}
                          >
                            Thử lại
                          </button>
                        </td>
                      </tr>
                    ) : orders?.length ? (
                      orderRows
                    ) : (
                      <tr>
                        <td colSpan={6} className="profile-empty">
                          Bạn chưa có đơn hàng nào.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              <div className="profile-pagination">
                <button
                  type="button"
                  className="profile-btn"
                  disabled={orderPage <= 1 || orderLoading}
                  onClick={() => handleOrderPageChange(orderPage - 1)}
                >
                  Trang trước
                </button>
                <span className="profile-hint">
                  Trang {orderPage} / {orderTotalPages}
                </span>
                <button
                  type="button"
                  className="profile-btn"
                  disabled={orderPage >= orderTotalPages || orderLoading}
                  onClick={() => handleOrderPageChange(orderPage + 1)}
                >
                  Trang sau
                </button>
              </div>
            </section>

            <section id="profile-transactions" className="profile-card">
              <h3 style={{ margin: "0 0 8px 0" }}>Lịch sử giao dịch</h3>
              <div className="profile-hint" style={{ marginBottom: 12 }}>
                Hiển thị tất cả giao dịch thanh toán bạn đã thực hiện.
              </div>

              {/* Transaction Filters */}
              <div className="profile-orders-filter-container">
                {/* Hàng 1: Tìm kiếm theo mã thanh toán, Từ ngày, Đến ngày */}
                <div className="profile-orders-filter-row">
                  <div className="profile-filter-group profile-filter-group--wide">
                    <label className="profile-label" htmlFor="transactionKeyword">
                      Tìm kiếm theo mã thanh toán
                    </label>
                    <input
                      id="transactionKeyword"
                      className="profile-input"
                      name="keyword"
                      placeholder="VD: mã thanh toán..."
                      value={transactionKeywordInput}
                      onChange={handleTransactionFilterChange}
                    />
                  </div>

                  <div className="profile-filter-group">
                    <label className="profile-label" htmlFor="transactionFromDate">
                      Từ ngày
                    </label>
                    <input
                      id="transactionFromDate"
                      className="profile-input"
                      type="date"
                      name="fromDate"
                      value={transactionFilters.fromDate}
                      onChange={handleTransactionFilterChange}
                    />
                  </div>

                  <div className="profile-filter-group">
                    <label className="profile-label" htmlFor="transactionToDate">
                      Đến ngày
                    </label>
                    <input
                      id="transactionToDate"
                      className="profile-input"
                      type="date"
                      name="toDate"
                      value={transactionFilters.toDate}
                      onChange={handleTransactionFilterChange}
                    />
                  </div>
                </div>

                {/* Hàng 2: Số tiền từ, Số tiền đến, Trạng thái, Loại thanh toán, nút Đặt lại */}
                <div className="profile-orders-filter-row">
                  <div className="profile-filter-group" style={{ flex: "0 0 140px" }}>
                    <label className="profile-label" htmlFor="transactionMinAmount">
                      Số tiền từ
                    </label>
                    <input
                      id="transactionMinAmount"
                      className="profile-input"
                      type="number"
                      name="minAmount"
                      placeholder="Tối thiểu"
                      value={transactionFilters.minAmount}
                      onChange={handleTransactionFilterChange}
                    />
                  </div>

                  <div className="profile-filter-group" style={{ flex: "0 0 140px" }}>
                    <label className="profile-label" htmlFor="transactionMaxAmount">
                      Số tiền đến
                    </label>
                    <input
                      id="transactionMaxAmount"
                      className="profile-input"
                      type="number"
                      name="maxAmount"
                      placeholder="Tối đa"
                      value={transactionFilters.maxAmount}
                      onChange={handleTransactionFilterChange}
                    />
                  </div>

                  <div className="profile-filter-group" style={{ flex: "0 0 160px" }}>
                    <label className="profile-label" htmlFor="transactionStatus">
                      Trạng thái
                    </label>
                    <select
                      id="transactionStatus"
                      className="profile-input"
                      name="status"
                      value={transactionFilters.status}
                      onChange={handleTransactionFilterChange}
                    >
                      {TRANSACTION_STATUS_OPTIONS.map((o) => (
                        <option key={o.value || "all"} value={o.value}>
                          {o.label}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="profile-filter-group" style={{ flex: "0 0 160px" }}>
                    <label className="profile-label" htmlFor="transactionType">
                      Loại thanh toán
                    </label>
                    <select
                      id="transactionType"
                      className="profile-input"
                      name="type"
                      value={transactionFilters.type}
                      onChange={handleTransactionFilterChange}
                    >
                      {TRANSACTION_TYPE_OPTIONS.map((o) => (
                        <option key={o.value || "all"} value={o.value}>
                          {o.label}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="profile-filter-actions">
                    <button
                      type="button"
                      className="profile-btn"
                      disabled={transactionLoading}
                      onClick={() => {
                        setTransactionFilters(INITIAL_TRANSACTION_FILTERS);
                        setTransactionKeywordInput("");
                        setTransactionPage(1);
                        setTransactionSortBy(null); // TH1: không sort
                        setTransactionSortDir(null);
                      }}
                    >
                      Đặt lại
                    </button>
                  </div>
                </div>
              </div>

              {/* Transaction Table */}
              <div className="profile-table-wrapper">
                <table className="profile-table" aria-label="Lịch sử giao dịch">
                  <thead>
                    <tr>
                      <th>
                        <div
                          className="profile-table-sorter"
                          onClick={() => handleTransactionSort("createdAt")}
                          onKeyDown={(e) => e.key === "Enter" && handleTransactionSort("createdAt")}
                          role="button"
                          tabIndex={0}
                        >
                          Ngày tạo
                          {transactionSortBy === "createdAt" && transactionSortDir &&
                            (transactionSortDir === "asc" ? " ↑" : " ↓")}
                        </div>
                      </th>
                      <th>
                        <div
                          className="profile-table-sorter"
                          onClick={() => handleTransactionSort("paymentId")}
                          onKeyDown={(e) => e.key === "Enter" && handleTransactionSort("paymentId")}
                          role="button"
                          tabIndex={0}
                        >
                          Mã thanh toán
                          {transactionSortBy === "paymentId" && transactionSortDir &&
                            (transactionSortDir === "asc" ? " ↑" : " ↓")}
                        </div>
                      </th>
                      <th>
                        <div
                          className="profile-table-sorter"
                          onClick={() => handleTransactionSort("transactionType")}
                          onKeyDown={(e) => e.key === "Enter" && handleTransactionSort("transactionType")}
                          role="button"
                          tabIndex={0}
                        >
                          Loại thanh toán
                          {transactionSortBy === "transactionType" && transactionSortDir &&
                            (transactionSortDir === "asc" ? " ↑" : " ↓")}
                        </div>
                      </th>
                      <th>
                        <div
                          className="profile-table-sorter"
                          onClick={() => handleTransactionSort("amount")}
                          onKeyDown={(e) => e.key === "Enter" && handleTransactionSort("amount")}
                          role="button"
                          tabIndex={0}
                        >
                          Số tiền
                          {transactionSortBy === "amount" && transactionSortDir &&
                            (transactionSortDir === "asc" ? " ↑" : " ↓")}
                        </div>
                      </th>
                      <th>
                        <div
                          className="profile-table-sorter"
                          onClick={() => handleTransactionSort("status")}
                          onKeyDown={(e) => e.key === "Enter" && handleTransactionSort("status")}
                          role="button"
                          tabIndex={0}
                        >
                          Trạng thái
                          {transactionSortBy === "status" && transactionSortDir &&
                            (transactionSortDir === "asc" ? " ↑" : " ↓")}
                        </div>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {transactionLoading ? (
                      <tr>
                        <td colSpan={5} className="profile-empty">
                          Đang tải giao dịch...
                        </td>
                      </tr>
                    ) : transactionError ? (
                      <tr>
                        <td colSpan={5} className="profile-empty">
                          {transactionError}{" "}
                          <button
                            type="button"
                            className="profile-btn"
                            onClick={() => fetchTransactions()}
                          >
                            Thử lại
                          </button>
                        </td>
                      </tr>
                    ) : transactionItems?.length ? (
                      transactionItems.map((p) => {
                        const pid = getPaymentId(p);
                        const created = getPaymentCreatedAt(p);
                        const amount = getPaymentAmount(p);
                        const statusUi = mapPaymentStatusToUi(getPaymentStatus(p));
                        const typeUi = mapTargetToUi(getTargetType(p));

                        return (
                          <tr key={pid || JSON.stringify(p)}>
                            <td style={{ fontWeight: 600 }}>{fmtDateTime(created)}</td>
                            <td>
                              <span title={pid}>{pid || "—"}</span>
                            </td>
                            <td>
                              <span className={`profile-pill ${typeUi.cls}`}>{typeUi.label}</span>
                            </td>
                            <td style={{ fontWeight: 700 }}>{formatVnd(amount)}</td>
                            <td>
                              <span className={`profile-pill ${statusUi.cls}`}>{statusUi.label}</span>
                            </td>
                          </tr>
                        );
                      })
                    ) : (
                      <tr>
                        <td colSpan={5} className="profile-empty">
                          Bạn chưa có giao dịch nào.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              {/* Pagination */}
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "12px" }}>
                <div style={{ color: "#6b7280", fontSize: "14px" }}>
                  {transactionLoading
                    ? "Đang tải..."
                    : `Tổng: ${transactionTotal} • Trang ${transactionPage}/${transactionTotalPages}`}
                </div>
                <div className="profile-pagination" style={{ margin: 0 }}>
                  <button
                    type="button"
                    className="profile-btn"
                    disabled={transactionPage <= 1 || transactionLoading}
                    onClick={() => handleTransactionPageChange(transactionPage - 1)}
                  >
                    Trang trước
                  </button>
                  <span className="profile-hint">
                    Trang {transactionPage} / {transactionTotalPages}
                  </span>
                  <button
                    type="button"
                    className="profile-btn"
                    disabled={transactionPage >= transactionTotalPages || transactionLoading}
                    onClick={() => handleTransactionPageChange(transactionPage + 1)}
                  >
                    Trang sau
                  </button>
                </div>
              </div>
            </section>

            <section id="profile-security" className="profile-card">
              <h3 style={{ margin: "0 0 8px 0" }}>Mật khẩu & Bảo mật</h3>
              <div className="profile-hint" style={{ marginBottom: 12 }}>
                Để bảo mật, hãy dùng mật khẩu mạnh và bật xác thực hai bước.
              </div>

              <div className="profile-form-grid" style={{ marginTop: 12 }}>
                <form onSubmit={handlePasswordSubmit}>
                  <div className="profile-field-group">
                    <label className="profile-label" htmlFor="currentPassword">
                      Mật khẩu hiện tại
                    </label>
                    <input
                      id="currentPassword"
                      type="password"
                      className="profile-input"
                      name="currentPassword"
                      placeholder="Nhập mật khẩu hiện tại"
                      value={passwordForm.currentPassword}
                      onChange={handlePasswordInput}
                    />
                  </div>
                  <div className="profile-field-group">
                    <label className="profile-label" htmlFor="newPassword">
                      Mật khẩu mới
                    </label>
                    <input
                      id="newPassword"
                      type="password"
                      className="profile-input"
                      name="newPassword"
                      placeholder="Ít nhất 8 ký tự, chứa chữ cái và số"
                      value={passwordForm.newPassword}
                      onChange={handlePasswordInput}
                    />
                  </div>
                  <div className="profile-field-group">
                    <label className="profile-label" htmlFor="confirmPassword">
                      Nhập lại mật khẩu mới
                    </label>
                    <input
                      id="confirmPassword"
                      type="password"
                      className="profile-input"
                      name="confirmPassword"
                      placeholder="Xác nhận mật khẩu"
                      value={passwordForm.confirmPassword}
                      onChange={handlePasswordInput}
                    />
                  </div>
                  <div style={{ marginTop: 12 }}>
                    <button
                      type="submit"
                      className="profile-btn primary"
                      disabled={changingPassword}
                    >
                      Lưu thay đổi
                    </button>
                  </div>
                  <InlineNotice notice={passwordNotice} />
                </form>

                {/* <div className="profile-card profile-security-card">
                  <h4 style={{ marginTop: 0 }}>Mẹo bảo mật</h4>
                  <ul>
                    <li>Dùng mật khẩu mạnh, khác nhau cho từng dịch vụ.</li>
                    <li>Bật xác thực 2 yếu tố (2FA) để bảo vệ tài khoản.</li>
                    <li>Không chia sẻ mã OTP hoặc mật khẩu cho người khác.</li>
                  </ul>
                  <hr
                    style={{
                      border: "none",
                      borderTop: "1px solid var(--profile-line)",
                      margin: "12px 0",
                    }}
                  />
                  <div className="profile-kv">Xác thực 2 bước</div>
                  <div
                    className="profile-security-actions"
                    style={{ marginTop: 8 }}
                  >
                    <button
                      type="button"
                      className="profile-btn primary"
                      onClick={handleTwoFactorToggle}
                      disabled={twoFactorUpdating}
                    >
                      {twoFactorEnabled ? "Tắt" : "Kích hoạt"}
                    </button>
                    <button
                      type="button"
                      className="profile-btn"
                      onClick={handleScrollToSecurity}
                    >
                      Cài đặt
                    </button>
                  </div>
                </div> */}
              </div>
            </section>

            <section id="profile-details" className="profile-card">
              <h3 style={{ margin: "0 0 8px 0" }}>
                Cập nhật thông tin cá nhân
              </h3>
              <div className="profile-hint" style={{ marginBottom: 12 }}>
                Cập nhật tên, điện thoại, địa chỉ để nhận hỗ trợ và hoá đơn.
              </div>

              <form onSubmit={handleProfileSubmit}>
                <div style={{ marginBottom: 16 }}>
                  <ImageUploader
                    label="Ảnh đại diện"
                    value={profileForm.avatarUrl}
                    onChange={handleAvatarChange}
                    onError={handleAvatarError}
                    onUploadingChange={handleAvatarUploading}
                    helperText="Ảnh sẽ hiển thị ở menu và trang hồ sơ của bạn."
                    height={180}
                  />
                </div>
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "1fr 1fr",
                    gap: 12,
                  }}
                >
                  <div>
                    <label className="profile-label" htmlFor="fullName">
                      Họ và tên
                    </label>
                    <input
                      id="fullName"
                      className="profile-input"
                      name="fullName"
                      placeholder="Nhập họ và tên"
                      value={profileForm.fullName}
                      onChange={handleProfileInput}
                    />
                  </div>
                  <div>
                    <label className="profile-label" htmlFor="phone">
                      Số điện thoại
                    </label>
                    <input
                      id="phone"
                      className="profile-input"
                      name="phone"
                      placeholder="09xx xxx xxx"
                      value={profileForm.phone}
                      onChange={handleProfileInput}
                    />
                  </div>
                  <div style={{ gridColumn: "1 / -1" }}>
                    <label className="profile-label" htmlFor="address">
                      Địa chỉ
                    </label>
                    <input
                      id="address"
                      className="profile-input"
                      name="address"
                      placeholder="Số nhà, đường, quận, thành phố"
                      value={profileForm.address}
                      onChange={handleProfileInput}
                    />
                  </div>
                </div>

                <div style={{ marginTop: 12 }}>
                  <button
                    type="submit"
                    className="profile-btn primary"
                    disabled={savingProfile || avatarUploading}
                  >
                    {savingProfile
                      ? "Đang lưu..."
                      : avatarUploading
                      ? "Đang tải ảnh..."
                      : "Lưu thay đổi"}
                  </button>
                </div>
                <InlineNotice notice={profileNotice} />
              </form>
            </section>
          </main>
        </div>
      </div>
    </div>
  );
};

export default UserProfilePage;
