/**
 * Màn khách hàng chọn gói hỗ trợ (Support Plan Subscription)
 */

import React, {
  useCallback,
  useEffect,
  useMemo,
  useState,
  useRef, // ✅ thêm useRef
} from "react";
import { Link, useNavigate, useLocation } from "react-router-dom";
import axiosClient from "../../api/axiosClient";
import Toast from "../../components/Toast/Toast";
import "./SupportPlanSubscriptionPage.css";
import supportPlanPaymentApi from "../../api/supportPlanPaymentApi";

// ===== Helpers chung =====
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const isPaidStatus = (s) => {
  const st = String(s || "").trim().toLowerCase();
  return st === "paid" || st === "success" || st === "completed";
};

const isTerminalFailStatus = (s) => {
  const st = String(s || "").trim().toLowerCase();
  return (
    st === "cancelled" ||
    st === "canceled" ||
    st === "timeout" ||
    st === "failed" ||
    st === "dupcancelled" ||
    st === "needreview"
  );
};

const waitForSupportPlanPaymentPaid = async (paymentId, maxWaitMs = 25000, intervalMs = 1200) => {
  const deadline = Date.now() + maxWaitMs;
  let lastStatus = "";

  while (Date.now() < deadline) {
    try {
      const res = await supportPlanPaymentApi.getSupportPlanPaymentStatus(paymentId);
      const data = res?.data ?? res;
      lastStatus = data?.status ?? "";

      if (isPaidStatus(lastStatus)) return { ok: true, status: lastStatus };
      if (isTerminalFailStatus(lastStatus)) return { ok: false, status: lastStatus };
    } catch {
      // ignore transient errors and keep polling
    }

    await sleep(intervalMs);
  }

  return { ok: false, status: lastStatus || "Pending", timeout: true };
};

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

// Tính số ngày còn lại (ước tính) giữa hôm nay và expiresAt
// Căn theo BE: (ExpiresAt.Date - Today.Date) trong khoảng 0..30
const calcRemainingDays = (value) => {
  if (!value) return null;
  try {
    const exp = new Date(value);
    if (Number.isNaN(exp.getTime())) return null;

    const now = new Date();

    const expDate = new Date(
      exp.getFullYear(),
      exp.getMonth(),
      exp.getDate()
    );
    const nowDate = new Date(
      now.getFullYear(),
      now.getMonth(),
      now.getDate()
    );

    const diffMs = expDate.getTime() - nowDate.getTime();
    let diffDays = Math.round(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays < 0) diffDays = 0;
    if (diffDays > 30) diffDays = 30;

    return diffDays;
  } catch {
    return null;
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

  // popup preview state
  const [previewPlan, setPreviewPlan] = useState(null);
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);

  const [toasts, setToasts] = useState([]);
  const [customer] = useState(() => readCustomerFromStorage());
  const navigate = useNavigate();
  const location = useLocation();

  // ✅ Cờ chống chạy handleRedirect 2 lần cho cùng 1 lượt redirect
  const redirectHandledRef = useRef(false);

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

  const currentSubRemainingDays =
    currentSub && currentSub.expiresAt
      ? calcRemainingDays(currentSub.expiresAt)
      : null;

  // ===== Base info để tính tiền theo note (bao gồm case Loyalty level 1) =====
  const paymentBaseInfo = useMemo(() => {
    const daysInMonth = 30;

    if (!previewPlan) {
      return {
        currentPrice: null,
        remainingDays: null,
        source: "none", // subscription | loyalty_base | none
      };
    }

    const previewLevel =
      Number(previewPlan.priorityLevel ?? previewPlan.PriorityLevel ?? 0) || 0;

    // Case 1: đang có gói hỗ trợ trả phí ACTIVE → dùng số ngày còn lại thực tế
    if (
      currentSub &&
      (currentSub.status || "").toLowerCase() === "active" &&
      currentSubRemainingDays != null &&
      currentSubRemainingDays > 0 &&
      Number(currentSub.price ?? currentSub.Price ?? 0) > 0 &&
      previewLevel > (Number(currentSub.priorityLevel ?? currentSub.PriorityLevel ?? 0) || 0)
    ) {
      return {
        currentPrice:
          Number(currentSub.price ?? currentSub.Price ?? 0) || null,
        remainingDays: currentSubRemainingDays,
        source: "subscription",
      };
    }

    // Case 2: Base priority = 1 (loyalty), không có gói trả phí đang active,
    // nâng lên gói level 2:
    // → luôn coi số ngày còn lại = full kỳ (30 ngày) của gói level 1.
    const statusLower = (currentSub?.status || "").toLowerCase();
    const hasPaidPlanActive =
      currentSub &&
      statusLower === "active" &&
      Number(currentSub.price ?? currentSub.Price ?? 0) > 0;

    if (
      !hasPaidPlanActive &&
      currentPriorityLevel === 1 &&
      previewLevel > 1
    ) {
      // tìm giá gói level 1 trong danh sách plans
      const lv1Plan = (plans || []).find(
        (p) =>
          Number(p.priorityLevel ?? p.PriorityLevel ?? 0) === 1 &&
          Number(p.price ?? p.Price ?? 0) > 0
      );

      if (lv1Plan) {
        return {
          currentPrice:
            Number(lv1Plan.price ?? lv1Plan.Price ?? 0) || null,
          remainingDays: daysInMonth, // full 30 ngày như trong note
          source: "loyalty_base",
        };
      }
    }

    // Mặc định: không có "gói cũ" để trừ
    return {
      currentPrice: null,
      remainingDays: null,
      source: "none",
    };
  }, [
    previewPlan,
    currentSub,
    currentSubRemainingDays,
    currentPriorityLevel,
    plans,
  ]);

  // Số tiền đã điều chỉnh để hiển thị trong popup (dùng cho gói trả phí)
  // Công thức mới:
  // Số tiền phải thanh toán = Giá gói được chọn - Giá gói ban đầu * (Số ngày còn lại / 30)
  const adjustedAmount = useMemo(() => {
    if (!previewPlan) return null;
    const newPrice = Number(
      previewPlan.price ?? previewPlan.Price ?? 0
    );
    if (!newPrice || newPrice <= 0) return 0;

    const { currentPrice, remainingDays } = paymentBaseInfo;
    const daysInMonth = 30;

    if (
      currentPrice &&
      remainingDays &&
      Number(remainingDays) > 0
    ) {
      const discount = (currentPrice * remainingDays) / daysInMonth;
      let adjusted = newPrice - discount;
      if (adjusted < 0) adjusted = 0;
      return Math.round(adjusted);
    }

    // Không có gói cũ hợp lệ → trả full giá
    return newPrice;
  }, [previewPlan, paymentBaseInfo]);

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

  // ===== Xử lý kết quả redirect từ PayOS sau khi thanh toán xong =====
  useEffect(() => {
    // Không có query -> reset cờ, để lần sau redirect còn chạy
    if (!location?.search) {
      redirectHandledRef.current = false;
      return;
    }

    // Đã xử lý rồi thì bỏ qua (chống chạy 2 lần)
    if (redirectHandledRef.current) {
      return;
    }
    redirectHandledRef.current = true;

    const searchParams = new URLSearchParams(location.search);
    const code = searchParams.get("code");
    const status = searchParams.get("status");
    const cancelParam = searchParams.get("cancel");
    const paymentId = searchParams.get("paymentId");
    const supportPlanIdParam = searchParams.get("supportPlanId");

    if (
      !code &&
      !status &&
      !cancelParam &&
      !paymentId &&
      !supportPlanIdParam
    ) {
      return;
    }

    const cancel =
      (cancelParam || "").toLowerCase() === "true" ||
      (status || "").toUpperCase() === "CANCELLED";

    const handleRedirect = async () => {
      // Trường hợp user huỷ thanh toán trên PayOS
      if (cancel) {
        addToast(
          "info",
          "Thanh toán đã huỷ",
          "Bạn đã huỷ giao dịch thanh toán gói hỗ trợ."
        );
        // Xoá query trên URL cho sạch
        navigate("/support/subscription", { replace: true });
        return;
      }

      const okCode = code === "00";
      const okStatus = (status || "").toUpperCase() === "PAID";

      // Thanh toán thành công → đợi webhook update DB rồi mới confirm
      if (okCode && okStatus && paymentId && supportPlanIdParam) {
        try {
          const wait = await waitForSupportPlanPaymentPaid(paymentId);

          if (!wait.ok) {
            // Pending quá lâu hoặc trạng thái fail
            const st = String(wait.status || "").trim();
            if (wait.timeout || st.toLowerCase() === "pending") {
              addToast(
                "info",
                "Đang xử lý thanh toán",
                "Hệ thống đang cập nhật trạng thái thanh toán. Vui lòng đợi vài giây và tải lại trang nếu chưa thấy gói được kích hoạt."
              );
            } else {
              addToast(
                "error",
                "Thanh toán chưa được ghi nhận",
                `Trạng thái thanh toán hiện tại: ${st || "Unknown"}. Nếu bạn đã bị trừ tiền, vui lòng liên hệ CSKH.`
              );
            }

            // reload data để nếu webhook vừa kịp apply subscription thì UI lên luôn
            await fetchData();
            navigate("/support/subscription", { replace: true });
            return;
          }

          const payload = {
            paymentId,
            supportPlanId: Number(supportPlanIdParam),
          };

          const resp = await supportPlanPaymentApi.confirmSupportPlanPayment(payload);
          const data = resp?.data ?? resp;

          if (data) setCurrentSub(normalizeCurrentSubscription(data));

          addToast(
            "success",
            "Đăng ký gói hỗ trợ thành công",
            "Gói hỗ trợ của bạn đã được kích hoạt."
          );
        } catch (err) {
          console.error("Failed to confirm support plan payment", err);
          const msg =
            err?.response?.data?.message ||
            err?.message ||
            "Không thể xác nhận thanh toán gói hỗ trợ. Vui lòng liên hệ chăm sóc khách hàng nếu tiền đã bị trừ.";
          addToast("error", "Lỗi xác nhận thanh toán", msg);
        } finally {
          await fetchData();
          navigate("/support/subscription", { replace: true });
        }
        return;
      }

      // Các trường hợp code != 00 hoặc status khác PAID
      if (code && code !== "00") {
        addToast(
          "error",
          "Thanh toán thất bại",
          "Giao dịch thanh toán với PayOS không thành công."
        );
      }

      // Dọn query URL
      navigate("/support/subscription", { replace: true });
    };

    handleRedirect();
  }, [location.search, addToast, fetchData, navigate]);

  // Gọi BE tạo Payment + redirect sang PayOS
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
        const returnUrl = encodeURIComponent(
          "/support/subscription"
        );
        navigate(`/login?returnUrl=${returnUrl}`);
        return;
      }

      setCreatingPaymentPlanId(plan.supportPlanId);
      try {
        const resp =
          await supportPlanPaymentApi.createPayOSPayment(
            plan.supportPlanId
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

  // mở popup preview khi chọn gói (thay vì gọi BE ngay)
  const handleOpenPreview = useCallback(
    (plan) => {
      if (!plan || !plan.supportPlanId) return;

      const planLevel =
        Number(
          plan.priorityLevel ??
          plan.PriorityLevel ??
          0
        ) || 0;

      // Nếu đang có priority > 0 và gói này có level <= currentPriorityLevel => không cho chọn
      if (
        currentPriorityLevel > 0 &&
        planLevel > 0 &&
        planLevel <= currentPriorityLevel
      ) {
        addToast(
          "info",
          "Không thể chọn gói thấp hơn",
          "Bạn đang ở gói có mức ưu tiên cao hơn. Chỉ có thể nâng cấp lên gói cao hơn hiện tại."
        );
        return;
      }

      // Gói miễn phí vẫn giữ behavior cũ: báo note, không mở popup
      if (!plan.price || plan.price <= 0) {
        addToast(
          "info",
          "Gói miễn phí",
          "Gói hỗ trợ này là gói mặc định, bạn không cần thanh toán."
        );
        return;
      }

      setPreviewPlan(plan);
      setIsPreviewOpen(true);
    },
    [addToast, currentPriorityLevel]
  );

  const handleClosePreview = useCallback(() => {
    setIsPreviewOpen(false);
    setPreviewPlan(null);
  }, []);

  // user confirm trong popup -> lúc này mới tạo Payment & redirect
  const handleConfirmPreview = useCallback(async () => {
    if (!previewPlan) return;
    const planToConfirm = previewPlan;
    setIsPreviewOpen(false);
    setPreviewPlan(null);
    await handleSelectPlan(planToConfirm);
  }, [previewPlan, handleSelectPlan]);

  const hasPlans = plans && plans.length > 0;

  // Tên hiển thị hiện tại (nếu sau này cần) — ưu tiên tên từ BE, fallback mức ưu tiên
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
          : "Bạn đang sử dụng gói hỗ trợ mặc định (Hỗ trợ tiêu chuẩn)."
        : null));

  const {
    currentPrice: paymentCurrentPrice,
    remainingDays: paymentRemainingDays,
    source: paymentSource,
  } = paymentBaseInfo;

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

                // Gói thấp hơn gói hiện tại (ví dụ đang VIP=2 mà gói này=1) -> không cho chọn
                const isDowngradePlan =
                  !isFree &&
                  currentPriorityLevel > 0 &&
                  planLevel > 0 &&
                  planLevel < currentPriorityLevel;

                const isHighlight =
                  planLevel >= 2 || (!isFree && planLevel === 1);

                const cardClasses = [
                  "sp-sub-plan-card",
                  isHighlight ? "sp-sub-plan-card--highlight" : "",
                  isCurrentPaidPlan ? "sp-sub-plan-card--current" : "",
                  isDowngradePlan ? "sp-sub-plan-card--downgrade" : "",
                ]
                  .filter(Boolean)
                  .join(" ");

                // Disable rules:
                // - Gói mặc định (free) luôn disable (gói default)
                // - Gói hiện tại (1/2) disable
                // - Gói thấp hơn gói hiện tại disable
                // - Trong lúc đang tạo payment cho gói đó cũng disable
                const disabled =
                  isFree ||
                  isCurrentPaidPlan ||
                  isDowngradePlan ||
                  creatingPaymentPlanId === plan.supportPlanId;

                let buttonLabel;
                if (isFree) {
                  buttonLabel = "Gói mặc định";
                } else if (
                  creatingPaymentPlanId === plan.supportPlanId
                ) {
                  buttonLabel = "Đang chuyển đến thanh toán...";
                } else if (isCurrentPaidPlan) {
                  buttonLabel = "Gói hiện tại";
                } else if (isDowngradePlan) {
                  buttonLabel = "Không thể chọn gói thấp hơn";
                } else {
                  buttonLabel = "Chọn gói này";
                }

                // Hiển thị số ngày còn lại trên card CHỈ khi thật sự có gói trả phí active
                const showRemainingOnCard =
                  isCurrentPaidPlan &&
                  currentSub &&
                  (currentSub.status || "").toLowerCase() === "active" &&
                  currentSubRemainingDays != null &&
                  currentSubRemainingDays > 0;

                return (
                  <article
                    key={plan.supportPlanId}
                    className={cardClasses}
                  >
                    <div className="sp-sub-plan-header">
                      <div className="sp-sub-plan-name-wrap">
                        {/* Tên gói lấy từ database */}
                        <h2 className="sp-sub-plan-name">
                          {plan.name}
                        </h2>
                        <span
                          className={getPriorityBadgeClass(
                            plan.priorityLevel
                          )}
                        >
                          {getPriorityLabel(plan.priorityLevel)}
                        </span>
                      </div>

                      {isCurrentPaidPlan && (
                        <div className="sp-sub-plan-current-inline">
                          <span className="sp-sub-plan-current-badge">
                            Gói hiện tại của bạn
                          </span>
                          {showRemainingOnCard && (
                            <span className="sp-sub-plan-current-days">
                              Còn{" "}
                              <strong>
                                {currentSubRemainingDays} ngày
                              </strong>
                            </span>
                          )}
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
                        <strong>
                          {getPriorityLabel(plan.priorityLevel)}
                        </strong>
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
                          Gói mặc định cho tất cả tài khoản
                          Keytietkiem.
                        </li>
                      )}
                    </ul>

                    <button
                      type="button"
                      className="sf-btn sf-btn-primary sp-sub-plan-cta"
                      disabled={disabled}
                      onClick={() => handleOpenPreview(plan)}
                    >
                      {buttonLabel}
                    </button>

                    {isFree && (
                      <div className="sp-sub-plan-note">
                        Nếu bạn không đăng ký gói trả phí, hệ thống sẽ
                        áp dụng{" "}
                        <strong>Hỗ trợ tiêu chuẩn</strong> mặc định.
                      </div>
                    )}
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </div>

      {/* Popup xác nhận gói hỗ trợ trước khi chuyển sang màn thanh toán */}
      {isPreviewOpen && previewPlan && (
        <div className="sp-sub-modal-backdrop">
          <div className="sp-sub-modal">
            <div className="sp-sub-modal-header">
              <h2 className="sp-sub-modal-title">
                Xác nhận đăng ký gói hỗ trợ
              </h2>
              <button
                type="button"
                className="sp-sub-modal-close"
                onClick={handleClosePreview}
              >
                ×
              </button>
            </div>

            <div className="sp-sub-modal-body">
              <p className="sp-sub-modal-intro">
                Hệ thống sẽ sử dụng các thông tin dưới đây để tạo{" "}
                <strong>đơn thanh toán</strong> cho gói hỗ trợ của bạn.
                Vui lòng kiểm tra kỹ trước khi tiếp tục.
              </p>

              {/* 1. Gói bạn chọn */}
              <section className="sp-sub-modal-section">
                <h3 className="sp-sub-modal-section-title">
                  1. Gói bạn chọn
                </h3>
                <div className="sp-sub-modal-plan">
                  <div className="sp-sub-modal-plan-main">
                    <div>
                      <div className="sp-sub-modal-plan-name">
                        {/* Tên gói lấy từ database */}
                        {previewPlan.name}
                      </div>
                      <div className="sp-sub-modal-plan-priority">
                        <span
                          className={getPriorityBadgeClass(
                            previewPlan.priorityLevel
                          )}
                        >
                          {getPriorityLabel(previewPlan.priorityLevel)}
                        </span>
                      </div>
                    </div>
                    <div className="sp-sub-modal-plan-price">
                      <span className="sp-sub-modal-plan-price-amount">
                        {formatCurrency(previewPlan.price)}
                      </span>
                      <span className="sp-sub-modal-plan-price-unit">
                        / tháng
                      </span>
                    </div>
                  </div>

                  {previewPlan.description && (
                    <p className="sp-sub-modal-plan-desc">
                      {previewPlan.description}
                    </p>
                  )}
                </div>
              </section>

              {/* 2. Mức ưu tiên & thời hạn */}
              <section className="sp-sub-modal-section">
                <h3 className="sp-sub-modal-section-title">
                  2. Mức ưu tiên & thời hạn
                </h3>
                <div className="sp-sub-modal-grid">
                  <div className="sp-sub-modal-col">
                    <div className="sp-sub-modal-label">
                      Trước khi đăng ký
                    </div>
                    <p>
                      Mức ưu tiên hiện tại:{" "}
                      <strong>
                        {getPriorityLabel(currentPriorityLevel)}
                      </strong>
                    </p>
                    {currentSub ? (
                      <>
                        {!isStatusNone && (
                          <p>
                            Gói hỗ trợ hiện tại:{" "}
                            <strong>
                              {currentSub.planName ||
                                getPriorityLabel(
                                  currentSub.priorityLevel
                                )}
                            </strong>
                          </p>
                        )}
                        <p>
                          Trạng thái gói hiện tại:{" "}
                          <strong>
                            {getSubscriptionStatusLabel(
                              currentSub.status
                            )}
                          </strong>
                        </p>
                        {currentSubRemainingDays != null && (
                          <p>
                            Số ngày còn lại (ước tính):{" "}
                            <strong>{currentSubRemainingDays}</strong>{" "}
                            ngày
                          </p>
                        )}
                      </>
                    ) : (
                      <p>
                        Hiện tại bạn chưa đăng ký gói hỗ trợ trả phí
                        nào.
                      </p>
                    )}
                  </div>

                  <div className="sp-sub-modal-col">
                    <div className="sp-sub-modal-label">
                      Sau khi đăng ký
                    </div>
                    <p>
                      Mức ưu tiên dự kiến:{" "}
                      <strong>
                        {getPriorityLabel(previewPlan.priorityLevel)}
                      </strong>
                    </p>
                    <p>
                      Thời hạn gói:{" "}
                      <strong>1 tháng</strong> kể từ khi thanh toán
                      thành công.
                    </p>
                  </div>
                </div>
              </section>

              {/* 3. Số tiền đã điều chỉnh (thực sự phải thanh toán) */}
              {adjustedAmount != null && (
                <section className="sp-sub-modal-section sp-sub-modal-section-summary">
                  <div className="sp-sub-modal-summary-row">
                    {/* Bên trái: thông tin gói cũ & số ngày còn lại */}
                    <div className="sp-sub-modal-summary-left">
                      <div className="sp-sub-modal-summary-label">
                        Gói hiện tại
                      </div>
                      {paymentCurrentPrice &&
                        paymentRemainingDays != null &&
                        paymentRemainingDays > 0 ? (
                        <>
                          <div className="sp-sub-modal-summary-text">
                            <strong>
                              {currentSub &&
                                Number(
                                  currentSub.price ??
                                  currentSub.Price ??
                                  0
                                ) > 0 &&
                                (currentSub.status || "")
                                  .toLowerCase() === "active"
                                ? currentSub.planName ||
                                getPriorityLabel(
                                  currentSub.priorityLevel
                                )
                                : getPriorityLabel(1)}
                            </strong>
                          </div>
                          <div className="sp-sub-modal-summary-meta">
                            Còn khoảng{" "}
                            <strong>
                              {paymentRemainingDays} ngày
                            </strong>{" "}
                            trong chu kỳ.
                            {paymentSource === "loyalty_base" && (
                              <>
                                {" "}
                                (tính theo quyền lợi{" "}
                                <strong>Hỗ trợ ưu tiên</strong>{" "}
                                vĩnh viễn)
                              </>
                            )}
                          </div>
                        </>
                      ) : (
                        <div className="sp-sub-modal-summary-text">
                          Không có gói cũ còn thời hạn.
                        </div>
                      )}
                    </div>

                    {/* Bên phải: số tiền phải thanh toán (highlight) */}
                    <div className="sp-sub-modal-summary-right">
                      <div className="sp-sub-modal-summary-label">
                        Số tiền phải thanh toán
                      </div>
                      <div className="sp-sub-modal-summary-amount">
                        {formatCurrency(adjustedAmount)}
                      </div>
                      <div className="sp-sub-modal-summary-note">
                        Số tiền sau khi đã điều chỉnh theo gói hiện tại
                        (nếu có).
                      </div>
                    </div>
                  </div>
                </section>
              )}
            </div>

            <div className="sp-sub-modal-footer">
              <button
                type="button"
                className="sf-btn sf-btn-outline sp-sub-modal-btn"
                onClick={handleClosePreview}
              >
                Hủy
              </button>
              <button
                type="button"
                className="sf-btn sf-btn-primary sp-sub-modal-btn"
                disabled={
                  creatingPaymentPlanId ===
                  (previewPlan && previewPlan.supportPlanId)
                }
                onClick={handleConfirmPreview}
              >
                Xác nhận & chuyển đến thanh toán
              </button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
};

export default SupportPlanSubscriptionPage;
