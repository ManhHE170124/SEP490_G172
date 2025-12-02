// src/pages/subcription/SupportPlanSubscriptionPage.jsx
/**
 * Màn khách hàng chọn gói hỗ trợ (Support Plan Subscription)
 * - Lấy danh sách gói từ: GET /api/supportplans/active
 * - Lấy gói hiện tại của user (nếu đăng nhập): GET /api/supportplans/me/current
 * - Khi chọn gói có trả phí:
 *      -> POST /api/payments/payos/create-support-plan
 *      -> Redirect sang PayOS PaymentUrl trả về
 *
 * Path FE: /support/subscription
 */

import React, { useCallback, useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import axiosClient from "../../api/axiosClient";
import Toast from "../../components/Toast/Toast";
import "./SupportPlanSubscriptionPage.css";

// ===== Helpers chung =====

const formatCurrency = (value) => {
  if (value == null) return "0₫";
  try {
    return new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: "VND",
      maximumFractionDigits: 0,
    }).format(value);
  } catch {
    return `${value}₫`;
  }
};

const formatDate = (value) => {
  if (!value) return "";
  try {
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return "";
    return d.toLocaleDateString("vi-VN", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    });
  } catch {
    return "";
  }
};

const readCustomerFromStorage = () => {
  if (typeof window === "undefined") return null;
  try {
    const token = window.localStorage.getItem("access_token");
    const storedUser = window.localStorage.getItem("user");
    if (!token || !storedUser) return null;
    const parsed = JSON.parse(storedUser);
    return parsed?.profile ?? parsed;
  } catch (error) {
    console.error("Failed to parse stored user", error);
    return null;
  }
};

// Chuẩn hoá plan trả về từ API (hỗ trợ cả PascalCase / camelCase)
const normalizePlan = (raw) => {
  if (!raw) return null;
  return {
    supportPlanId: raw.supportPlanId ?? raw.SupportPlanId,
    name: raw.name ?? raw.Name,
    description: raw.description ?? raw.Description,
    priorityLevel: raw.priorityLevel ?? raw.PriorityLevel ?? 0,
    price: raw.price ?? raw.Price ?? 0,
    isActive:
      typeof raw.isActive === "boolean"
        ? raw.isActive
        : raw.IsActive ?? true,
  };
};

// Chuẩn hoá subscription hiện tại
const normalizeCurrentSubscription = (raw) => {
  if (!raw) return null;
  return {
    subscriptionId: raw.subscriptionId ?? raw.SubscriptionId,
    supportPlanId: raw.supportPlanId ?? raw.SupportPlanId,
    planName: raw.planName ?? raw.PlanName,
    planDescription: raw.planDescription ?? raw.PlanDescription,
    priorityLevel: raw.priorityLevel ?? raw.PriorityLevel ?? 0,
    price: raw.price ?? raw.Price ?? 0,
    status: raw.status ?? raw.Status ?? "",
    startedAt: raw.startedAt ?? raw.StartedAt,
    expiresAt: raw.expiresAt ?? raw.ExpiresAt,
  };
};

const getPriorityLabel = (level) => {
  const lv = Number(level) || 0;
  if (lv >= 2) return "Hỗ trợ VIP";
  if (lv === 1) return "Hỗ trợ ưu tiên";
  return "Hỗ trợ tiêu chuẩn";
};

const getPriorityBadgeClass = (level) => {
  const lv = Number(level) || 0;
  if (lv >= 2) return "sp-sub-priority sp-sub-priority--vip";
  if (lv === 1) return "sp-sub-priority sp-sub-priority--priority";
  return "sp-sub-priority sp-sub-priority--standard";
};

const getSubscriptionStatusLabel = (status) => {
  const st = (status || "").toLowerCase();
  if (st === "active") return "Đang hoạt động";
  if (st === "pending") return "Đang chờ kích hoạt";
  if (st === "expired") return "Đã hết hạn";
  if (st === "cancelled" || st === "canceled") return "Đã huỷ";
  return status || "";
};

// ===== Component chính =====

const SupportPlanSubscriptionPage = () => {
  const [plans, setPlans] = useState([]);
  const [currentSub, setCurrentSub] = useState(null);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [creatingPaymentPlanId, setCreatingPaymentPlanId] = useState(null);

  const [toasts, setToasts] = useState([]);
  const [customer] = useState(() => readCustomerFromStorage());
  const navigate = useNavigate();

  // Toast helpers giống Cart
  const removeToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const addToast = useCallback(
    (type, title, message) => {
      const id = Date.now() + Math.random();
      const toast = { id, type, title, message };
      setToasts((prev) => [...prev, toast]);
      setTimeout(() => removeToast(id), 4000);
    },
    [removeToast]
  );

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const activePromise = axiosClient.get("/supportplans/active");
      const currentPromise = axiosClient
        .get("/supportplans/me/current")
        .catch((err) => {
          // Nếu chưa đăng nhập -> 401 -> coi như không có gói hiện tại
          if (err?.response?.status === 401) {
            return null;
          }
          console.error("Failed to load current subscription", err);
          return null;
        });

      const [activeRes, currentRes] = await Promise.all([
        activePromise,
        currentPromise,
      ]);

      const activeData = activeRes?.data ?? activeRes;
      const rawPlans = Array.isArray(activeData)
        ? activeData
        : activeData?.items || [];

      const normalizedPlans = rawPlans
        .map(normalizePlan)
        .filter((p) => p && p.isActive);

      setPlans(normalizedPlans);

      const currentData = currentRes?.data ?? currentRes;
      const rawCurrent = currentData ?? null;
      setCurrentSub(
        rawCurrent ? normalizeCurrentSubscription(rawCurrent) : null
      );
    } catch (err) {
      console.error("Failed to load support plans", err);
      const msg =
        err?.response?.data?.message ||
        err?.message ||
        "Không thể tải danh sách gói hỗ trợ. Vui lòng thử lại.";
      setError(msg);
      addToast("error", "Lỗi tải dữ liệu", msg);
    } finally {
      setLoading(false);
    }
  }, [addToast]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleSelectPlan = useCallback(
    async (plan) => {
      if (!plan || !plan.supportPlanId) return;

      // Gói miễn phí -> không gọi PayOS
      if (!plan.price || plan.price <= 0) {
        addToast(
          "info",
          "Gói miễn phí",
          "Gói hỗ trợ này là gói mặc định, bạn không cần thanh toán."
        );
        return;
      }

      // Yêu cầu đăng nhập trước khi thanh toán
      if (!customer) {
        addToast(
          "info",
          "Cần đăng nhập",
          "Vui lòng đăng nhập để đăng ký gói hỗ trợ."
        );
        const returnUrl = encodeURIComponent("/support/subscription");
        navigate(`/login?returnUrl=${returnUrl}`);
        return;
      }

      setCreatingPaymentPlanId(plan.supportPlanId);
      try {
        const payload = {
          supportPlanId: plan.supportPlanId,
          // note: có thể thêm trường Note sau nếu cần
        };

        const resp = await axiosClient.post(
          "/payments/payos/create-support-plan",
          payload
        );
        const data = resp?.data ?? resp;
        const paymentUrl = data.paymentUrl || data.PaymentUrl;

        if (paymentUrl) {
          // Redirect sang PayOS
          window.location.href = paymentUrl;
        } else {
          throw new Error(
            "Không nhận được đường dẫn thanh toán từ máy chủ."
          );
        }
      } catch (err) {
        console.error("Failed to create support plan payment", err);
        const msg =
          err?.response?.data?.message ||
          err?.message ||
          "Không thể tạo thanh toán cho gói hỗ trợ.";
        addToast("error", "Lỗi thanh toán", msg);
      } finally {
        setCreatingPaymentPlanId(null);
      }
    },
    [addToast, customer, navigate]
  );

  const hasPlans = plans && plans.length > 0;

  return (
    <main className="sp-sub-page">
      {/* Toast stack (giống Cart) */}
      <div className="toast-container">
        {toasts.map((t) => (
          <Toast key={t.id} toast={t} onRemove={removeToast} />
        ))}
      </div>

      <div className="sp-sub-container">
        {/* Breadcrumb */}
        <div className="sp-sub-breadcrumb">
          <Link to="/">Trang chủ</Link>
          <span>/</span>
          <span>Gói hỗ trợ</span>
        </div>

        {/* Header */}
        <header className="sp-sub-header">
          <h1 className="sp-sub-title">Gói hỗ trợ khách hàng</h1>
          <p className="sp-sub-subtitle">
            Chọn gói hỗ trợ phù hợp với nhu cầu sử dụng Keytietkiem của bạn.
            Các gói cao hơn sẽ được ưu tiên xử lý nhanh hơn khi bạn cần trợ
            giúp.
          </p>
        </header>

        {/* Gói hiện tại (nếu có) */}
        {currentSub && (
          <section className="sp-sub-current">
            <div className="sp-sub-current-label">Gói hiện tại của bạn</div>
            <div className="sp-sub-current-main">
              <div className="sp-sub-current-info">
                <div className="sp-sub-current-name-row">
                  <h2 className="sp-sub-current-name">
                    {currentSub.planName}
                  </h2>
                  <span
                    className={getPriorityBadgeClass(
                      currentSub.priorityLevel
                    )}
                  >
                    {getPriorityLabel(currentSub.priorityLevel)}
                  </span>
                </div>
                {currentSub.planDescription && (
                  <p className="sp-sub-current-desc">
                    {currentSub.planDescription}
                  </p>
                )}
                <div className="sp-sub-current-meta">
                  <span className="sp-sub-current-status">
                    Trạng thái:{" "}
                    <strong>
                      {getSubscriptionStatusLabel(currentSub.status)}
                    </strong>
                  </span>
                  <span className="sp-sub-current-dates">
                    Bắt đầu:{" "}
                    <strong>{formatDate(currentSub.startedAt) || "—"}</strong>
                    {currentSub.expiresAt && (
                      <>
                        {"  •  "}
                        Hết hạn:{" "}
                        <strong>
                          {formatDate(currentSub.expiresAt) || "—"}
                        </strong>
                      </>
                    )}
                  </span>
                </div>
              </div>
              <div className="sp-sub-current-price">
                <div className="sp-sub-current-price-label">
                  Giá gói mỗi tháng
                </div>
                <div className="sp-sub-current-price-value">
                  {currentSub.price > 0
                    ? formatCurrency(currentSub.price)
                    : "Miễn phí"}
                </div>
              </div>
            </div>
          </section>
        )}

        {/* Lỗi tổng (nếu có) */}
        {error && !loading && (
          <div className="sp-sub-error">
            <span>{error}</span>
            <button
              type="button"
              className="sf-btn sf-btn-outline sp-sub-error-retry"
              onClick={fetchData}
            >
              Thử lại
            </button>
          </div>
        )}

        {/* Danh sách gói */}
        <section className="sp-sub-plans-section">
          {loading && (
            <div className="sp-sub-loading">
              Đang tải danh sách gói hỗ trợ...
            </div>
          )}

          {!loading && !hasPlans && !error && (
            <div className="sp-sub-empty">
              Hiện tại chưa có gói hỗ trợ nào được mở bán.
            </div>
          )}

          {!loading && hasPlans && (
            <div className="sp-sub-plan-grid">
              {plans.map((plan) => {
                const isCurrent =
                  currentSub &&
                  currentSub.supportPlanId === plan.supportPlanId;
                const isFree = !plan.price || plan.price <= 0;
                const isHighlight =
                  plan.priorityLevel >= 2 ||
                  (!isFree && plan.priorityLevel === 1);

                const cardClasses = [
                  "sp-sub-plan-card",
                  isHighlight ? "sp-sub-plan-card--highlight" : "",
                  isCurrent ? "sp-sub-plan-card--current" : "",
                ]
                  .filter(Boolean)
                  .join(" ");

                return (
                  <article
                    key={plan.supportPlanId}
                    className={cardClasses}
                  >
                    <div className="sp-sub-plan-header">
                      <div className="sp-sub-plan-name-wrap">
                        <h2 className="sp-sub-plan-name">{plan.name}</h2>
                        <span
                          className={getPriorityBadgeClass(
                            plan.priorityLevel
                          )}
                        >
                          {getPriorityLabel(plan.priorityLevel)}
                        </span>
                      </div>

                      {isCurrent && (
                        <div className="sp-sub-plan-current-badge">
                          Gói đang sử dụng
                        </div>
                      )}
                    </div>

                    <div className="sp-sub-plan-price">
                      <span className="sp-sub-plan-price-amount">
                        {isFree
                          ? "Miễn phí"
                          : formatCurrency(plan.price)}
                      </span>
                      {!isFree && (
                        <span className="sp-sub-plan-price-unit">
                          / tháng
                        </span>
                      )}
                    </div>

                    {plan.description && (
                      <p className="sp-sub-plan-desc">
                        {plan.description}
                      </p>
                    )}

                    <ul className="sp-sub-plan-features">
                      <li>
                        Mức ưu tiên:{" "}
                        <strong>{getPriorityLabel(plan.priorityLevel)}</strong>
                      </li>
                      <li>
                        Hỗ trợ qua ticket/email, ưu tiên xử lý theo{" "}
                        <strong>gói đăng ký</strong>.
                      </li>
                      {!isFree && (
                        <li>
                          Phù hợp với người dùng cần{" "}
                          <strong>phản hồi nhanh</strong> và{" "}
                          <strong>ưu tiên cao</strong>.
                        </li>
                      )}
                      {isFree && (
                        <li>
                          Gói mặc định cho tất cả tài khoản Keytietkiem.
                        </li>
                      )}
                    </ul>

                    <button
                      type="button"
                      className="sf-btn sf-btn-primary sp-sub-plan-cta"
                      disabled={
                        creatingPaymentPlanId === plan.supportPlanId ||
                        isCurrent ||
                        isFree
                      }
                      onClick={() => handleSelectPlan(plan)}
                    >
                      {isFree
                        ? isCurrent
                          ? "Đang sử dụng"
                          : "Gói mặc định"
                        : creatingPaymentPlanId === plan.supportPlanId
                        ? "Đang chuyển đến thanh toán..."
                        : isCurrent
                        ? "Đang sử dụng"
                        : "Chọn gói này"}
                    </button>

                    {isFree && !isCurrent && (
                      <div className="sp-sub-plan-note">
                        Nếu bạn không đăng ký gói trả phí, hệ thống sẽ áp dụng{" "}
                        <strong>Standard Support</strong> mặc định.
                      </div>
                    )}
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </div>
    </main>
  );
};

export default SupportPlanSubscriptionPage;
