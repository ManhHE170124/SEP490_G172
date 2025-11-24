import React, { useCallback, useEffect, useMemo, useState } from "react";
import ImageUploader from "../../components/ImageUploader/ImageUploader";
import profileService from "../../services/profile";
import "./UserProfilePage.css";

const SECTION_ITEMS = [
  { id: "profile-overview", label: "Tổng quan" },
  { id: "profile-orders", label: "Lịch sử đơn hàng" },
  { id: "profile-transactions", label: "Lịch sử giao dịch" },
  { id: "profile-security", label: "Mật khẩu & Bảo mật" },
  { id: "profile-details", label: "Cập nhật thông tin" },
];

const INITIAL_ORDER_FILTERS = { keyword: "", fromDate: "", toDate: "" };
const INITIAL_TRANSACTION_FILTERS = { keyword: "", fromDate: "", toDate: "" };

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
  const text = status.toString().toLowerCase();
  const successTokens = ["hoàn", "success", "complete", "done"];
  const warningTokens = ["pending", "đang", "processing"];
  const dangerTokens = ["hủy", "cancel", "failed", "lỗi"];
  if (successTokens.some((token) => text.includes(token))) return "success";
  if (warningTokens.some((token) => text.includes(token))) return "warning";
  if (dangerTokens.some((token) => text.includes(token))) return "danger";
  return "muted";
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
  const [orders, setOrders] = useState([]);
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
  const [transactionFilters, setTransactionFilters] = useState(
    INITIAL_TRANSACTION_FILTERS
  );

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

  const fetchOrders = useCallback(async (filters = INITIAL_ORDER_FILTERS) => {
    setOrderLoading(true);
    setOrderError("");
    try {
      const response = await profileService.getOrders({
        ...sanitizeFilters(filters),
        page: 1,
        pageSize: 5,
      });
      setOrders(extractList(response));
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

  const fetchTransactions = useCallback(
    async (filters = INITIAL_TRANSACTION_FILTERS) => {
      setTransactionLoading(true);
      setTransactionError("");
      try {
        const response = await profileService.getTransactions({
          ...sanitizeFilters(filters),
          page: 1,
          pageSize: 5,
        });
        setTransactions(extractList(response));
      } catch (error) {
        const message =
          error?.response?.data?.message ||
          error?.message ||
          "Không thể tải lịch sử giao dịch.";
        setTransactionError(message);
      } finally {
        setTransactionLoading(false);
      }
    },
    []
  );

  useEffect(() => {
    loadProfile();
  }, [loadProfile]);

  // useEffect(() => {
  //   fetchOrders(INITIAL_ORDER_FILTERS);
  // }, [fetchOrders]);

  // useEffect(() => {
  //   fetchTransactions(INITIAL_TRANSACTION_FILTERS);
  // }, [fetchTransactions]);

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
    setOrderFilters((prev) => ({ ...prev, [name]: value }));
  };

  const handleTransactionFilterChange = (event) => {
    const { name, value } = event.target;
    setTransactionFilters((prev) => ({ ...prev, [name]: value }));
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

  const handleApplyOrderFilters = () => fetchOrders(orderFilters);
  const handleApplyTransactionFilters = () =>
    fetchTransactions(transactionFilters);

  const orderCount =
    profile?.stats?.orderCount ?? profile?.ordersCount ?? (orders?.length || 0);
  const transactionCount =
    profile?.stats?.transactionCount ??
    profile?.transactionsCount ??
    (transactions?.length || 0);

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

  const orderRows = orders?.map((order, index) => {
    const createdAt =
      order?.createdAt || order?.createdDate || order?.orderDate;
    const code = order?.code || order?.orderCode || order?.id || `#${index}`;
    const product =
      order?.productName ||
      order?.productTitle ||
      order?.items?.[0]?.name ||
      "—";
    const total =
      order?.total ||
      order?.totalPrice ||
      order?.totalAmount ||
      order?.amount ||
      order?.grandTotal;
    const status = order?.statusLabel || order?.status || "";
    return (
      <tr key={`${code}-${index}`}>
        <td>{formatDate(createdAt)}</td>
        <td>{code}</td>
        <td>{product}</td>
        <td>{formatMoney(total)}</td>
        <td>
          <span className={`profile-pill ${getStatusTone(status)}`}>
            {status || "—"}
          </span>
        </td>
      </tr>
    );
  });

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

              <div className="profile-orders-filter">
                <input
                  className="profile-input"
                  name="keyword"
                  placeholder="Mã đơn hàng"
                  value={orderFilters.keyword}
                  onChange={handleOrderFilterChange}
                />
                <input
                  className="profile-input"
                  type="date"
                  name="fromDate"
                  placeholder="Từ ngày"
                  value={orderFilters.fromDate}
                  onChange={handleOrderFilterChange}
                />
                <input
                  className="profile-input"
                  type="date"
                  name="toDate"
                  placeholder="Đến ngày"
                  value={orderFilters.toDate}
                  onChange={handleOrderFilterChange}
                />
                <button
                  type="button"
                  className="profile-btn"
                  onClick={handleApplyOrderFilters}
                >
                  Lọc
                </button>
              </div>

              <div className="profile-table-wrapper">
                <table className="profile-table" aria-label="Lịch sử đơn hàng">
                  <thead>
                    <tr>
                      <th>Thời gian</th>
                      <th>Mã đơn</th>
                      <th>Sản phẩm</th>
                      <th>Tổng tiền</th>
                      <th>Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {orderLoading ? (
                      <tr>
                        <td colSpan={5} className="profile-empty">
                          Đang tải đơn hàng...
                        </td>
                      </tr>
                    ) : orderError ? (
                      <tr>
                        <td colSpan={5} className="profile-empty">
                          {orderError}{" "}
                          <button
                            type="button"
                            className="profile-btn"
                            onClick={handleApplyOrderFilters}
                          >
                            Thử lại
                          </button>
                        </td>
                      </tr>
                    ) : orders?.length ? (
                      orderRows
                    ) : (
                      <tr>
                        <td colSpan={5} className="profile-empty">
                          Bạn chưa có đơn hàng nào.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </section>

            <section id="profile-transactions" className="profile-card">
              <h3 style={{ margin: "0 0 8px 0" }}>Lịch sử giao dịch</h3>
              <div className="profile-hint" style={{ marginBottom: 12 }}>
                Hiển thị tất cả giao dịch bạn đã thực hiện.
              </div>

              <div className="profile-transactions-filter">
                <input
                  className="profile-input"
                  name="keyword"
                  placeholder="Mô tả"
                  value={transactionFilters.keyword}
                  onChange={handleTransactionFilterChange}
                />
                <input
                  className="profile-input"
                  type="date"
                  name="fromDate"
                  placeholder="Từ ngày"
                  value={transactionFilters.fromDate}
                  onChange={handleTransactionFilterChange}
                />
                <input
                  className="profile-input"
                  type="date"
                  name="toDate"
                  placeholder="Đến ngày"
                  value={transactionFilters.toDate}
                  onChange={handleTransactionFilterChange}
                />
                <button
                  type="button"
                  className="profile-btn"
                  onClick={handleApplyTransactionFilters}
                >
                  Lọc
                </button>
              </div>

              <div className="profile-table-wrapper">
                <table className="profile-table" aria-label="Lịch sử giao dịch">
                  <thead>
                    <tr>
                      <th>Thời gian</th>
                      <th>Mô tả</th>
                      <th>Số tiền</th>
                    </tr>
                  </thead>
                  <tbody>
                    {transactionLoading ? (
                      <tr>
                        <td colSpan={3} className="profile-empty">
                          Đang tải giao dịch...
                        </td>
                      </tr>
                    ) : transactionError ? (
                      <tr>
                        <td colSpan={3} className="profile-empty">
                          {transactionError}{" "}
                          <button
                            type="button"
                            className="profile-btn"
                            onClick={handleApplyTransactionFilters}
                          >
                            Thử lại
                          </button>
                        </td>
                      </tr>
                    ) : transactions?.length ? (
                      transactionRows
                    ) : (
                      <tr>
                        <td colSpan={3} className="profile-empty">
                          Bạn chưa có giao dịch nào.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
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
                      placeholder="Ít nhất 6 ký tự"
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
