// src/pages/subcription/SupportPlanSubscriptionPage.jsx
/**
 * Màn khách hàng chọn gói hỗ trợ (Support Plan Subscription)
 * - Lấy danh sách gói từ: GET /api/supportplans/active
 * - Lấy mức ưu tiên/gói hiện tại của user: GET /api/supportplans/me/current
 *   + PriorityLevel trong DTO được tính từ Users.SupportPriorityLevel (BE),
 *     kết hợp với subscription active (nếu có).
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

// Chuẩn hoá subscription / mức ưu tiên hiện tại
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
  if (st === "none") return "Chưa đăng ký gói trả phí";
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

  // PriorityLevel hiện tại luôn lấy từ BE (/supportplans/me/current)
  const currentPriorityLevel = currentSub
    ? Number(
        currentSub.priorityLevel ??
          currentSub.PriorityLevel ??
          0
      ) || 0
    : 0;

  // Status "None" nghĩa là không có subscription active, nhưng vẫn có PriorityLevel từ user
  const isStatusNone =
    currentSub &&
    (currentSub.status || "").toLowerCase() === "none";

  // Toast helpers
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

      // Chỉ gọi /me/current nếu user đang đăng nhập
      const currentPromise = customer
        ? axiosClient
            .get("/supportplans/me/current")
            .catch((err) => {
              console.error("Failed to load current subscription", err);
              return null;
            })
        : Promise.resolve(null);

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
  }, [addToast, customer]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleSelectPlan = useCallback(
    async (plan) => {
      if (!plan || !plan.supportPlanId) return;

      // Gói miễn phí (Standard) -> không gọi PayOS
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
        };

        const resp = await axiosClient.post(
          "/payments/payos/create-support-plan",
          payload
        );
        const data = resp?.data ?? resp;
        const paymentUrl = data.paymentUrl || data.PaymentUrl;

        if (paymentUrl) {
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

  // Tên & mô tả hiển thị ở block "Mức hỗ trợ hiện tại"
  const displayPlanName =
    currentSub &&
    (currentSub.planName && !isStatusNone
      ? currentSub.planName
      : getPriorityLabel(currentPriorityLevel));

  const displayPlanDescription =
    currentSub &&
    (currentSub.planDescription ||
      (isStatusNone
        ? currentPriorityLevel > 0
          ? "Bạn đang được hưởng mức ưu tiên hỗ trợ theo tài khoản hiện tại."
          : "Bạn đang sử dụng gói hỗ trợ mặc định (Standard Support)."
        : null));

  return (
    <main className="sp-sub-page">
      {/* Toast stack */}
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
            Các gói có mức ưu tiên cao hơn sẽ được xử lý nhanh hơn khi bạn cần
            trợ giúp.
          </p>
        </header>

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
                const planLevel = Number(plan.priorityLevel) || 0;
                const isFree = !plan.price || plan.price <= 0;

                // Gói trả phí (1 hoặc 2) và level trùng với Priority hiện tại -> gói hiện tại (không cho click)
                const isCurrentPaidPlan =
                  !isFree &&
                  currentPriorityLevel > 0 &&
                  planLevel === currentPriorityLevel;

                const isHighlight =
                  planLevel >= 2 || (!isFree && planLevel === 1);

                const cardClasses = [
                  "sp-sub-plan-card",
                  isHighlight ? "sp-sub-plan-card--highlight" : "",
                  isCurrentPaidPlan ? "sp-sub-plan-card--current" : "",
                ]
                  .filter(Boolean)
                  .join(" ");

                // Disable rules:
                // - Gói mặc định (free) luôn disable (gói default)
                // - Gói hiện tại (1/2) disable
                // - Trong lúc đang tạo payment cho gói đó cũng disable
                const disabled =
                  isFree ||
                  isCurrentPaidPlan ||
                  creatingPaymentPlanId === plan.supportPlanId;

                let buttonLabel;
                if (isFree) {
                  // Gói mặc định không phụ thuộc current plan, luôn là "Gói mặc định"
                  buttonLabel = "Gói mặc định";
                } else if (
                  creatingPaymentPlanId === plan.supportPlanId
                ) {
                  buttonLabel = "Đang chuyển đến thanh toán...";
                } else if (isCurrentPaidPlan) {
                  // User đang ở level 1 hoặc 2 tương ứng
                  buttonLabel = "Gói hiện tại";
                } else {
                  buttonLabel = "Chọn gói này";
                }

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

                      {isCurrentPaidPlan && (
                        <div className="sp-sub-plan-current-badge">
                          Gói hiện tại của bạn
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
                      disabled={disabled}
                      onClick={() => handleSelectPlan(plan)}
                    >
                      {buttonLabel}
                    </button>

                    {isFree && (
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
