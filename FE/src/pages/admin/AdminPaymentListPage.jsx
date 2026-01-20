// File: src/pages/admin/AdminPaymentListPage.jsx
import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";
import { paymentApi } from "../../services/paymentApi";
import "./AdminPaymentListPage.css";
import formatDatetime from "../../utils/formatDatetime";
import ToastContainer from "../../components/Toast/ToastContainer";

/** ===== Icons (SVG inline) - tránh phụ thuộc react-icons ===== */
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
  Caret: (p) => (
    <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" {...p}>
      <path fill="currentColor" d="M7 10l5 5 5-5H7z" />
    </svg>
  ),
};

const formatVnd = (n) => {
  const num = Number(n || 0);
  return new Intl.NumberFormat("vi-VN").format(num) + " đ";
};

const normalizeText = (v) => String(v ?? "").trim();
const normalizeStatusKey = (s) => String(s || "").trim().toUpperCase();

const getPaymentId = (p) => p?.paymentId ?? p?.PaymentId ?? p?.id ?? p?.Id ?? "";
const getCreatedAt = (p) =>
  p?.createdAt ?? p?.CreatedAt ?? p?.createdTime ?? p?.createdDate ?? p?.createdOn ?? null;
const getAmount = (p) => p?.amount ?? p?.Amount ?? p?.totalAmount ?? p?.paidAmount ?? 0;
const getStatus = (p) => p?.status ?? p?.Status ?? p?.paymentStatus ?? p?.PaymentStatus ?? "Unknown";

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

/**
 * ✅ Map đúng Payment Status theo BE hiện tại:
 * Pending, Paid, Cancelled, Timeout, NeedReview, DupCancelled, Replaced, Refunded
 */
const mapStatusToUi = (s) => {
  const v = normalizeStatusKey(s);

  if (v === "PENDING") return { label: "Chờ thanh toán", cls: "payment-pending", value: "Pending" };

  if (v === "PAID" || v === "SUCCESS" || v === "COMPLETED")
    return { label: "Đã thanh toán", cls: "payment-paid", value: "Paid" };

  if (v === "TIMEOUT" || v === "CANCELLEDBYTIMEOUT")
    return { label: "Hủy do quá hạn", cls: "payment-timeout", value: "Timeout" };

  if (v === "CANCELLED") return { label: "Đã hủy", cls: "payment-cancelled", value: "Cancelled" };

  if (v === "NEEDREVIEW")
    return { label: "Cần kiểm tra", cls: "payment-pending", value: "NeedReview" };

  if (v === "DUPCANCELLED")
    return { label: "Hủy do trùng", cls: "payment-cancelled", value: "DupCancelled" };

  if (v === "REPLACED")
    return { label: "Đã thay thế", cls: "payment-cancelled", value: "Replaced" };

  if (v === "REFUNDED")
    return { label: "Đã hoàn tiền", cls: "payment-refunded", value: "Refunded" };

  return { label: s ? String(s) : "Không rõ", cls: "payment-unknown", value: s || "Unknown" };
};

const fmtDateTime = (d) => formatDatetime(d);

export default function AdminPaymentListPage() {
  // ===== Draft filters (UI) =====
  const [qDraft, setQDraft] = useState("");
  const [fromDraft, setFromDraft] = useState(null);
  const [toDraft, setToDraft] = useState(null);
  const [statusDraft, setStatusDraft] = useState("");
  const [typeDraft, setTypeDraft] = useState("");
  const [minDraft, setMinDraft] = useState("");
  const [maxDraft, setMaxDraft] = useState("");

  // ===== Applied filters (fetch) =====
  const [q, setQ] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [status, setStatus] = useState("");
  const [type, setType] = useState("");
  const [minAmount, setMinAmount] = useState("");
  const [maxAmount, setMaxAmount] = useState("");

  // ===== Sort / paging =====
  const [sortField, setSortField] = useState("createdAt"); // paymentId/amount/status/createdAt/transactionType
  const [sortDir, setSortDir] = useState("desc"); // asc|desc
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  // ===== Data =====
  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const totalPages = useMemo(
    () => Math.max(1, Math.ceil((total || 0) / (pageSize || 10))),
    [total, pageSize]
  );
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // ===== Inline status update (Admin/CustomerCare) =====
  const [updatingPaymentId, setUpdatingPaymentId] = useState("");
  const [updateStatusErr, setUpdateStatusErr] = useState("");

  // ===== Status dropdown-pill menu =====
  const [openStatusMenuId, setOpenStatusMenuId] = useState("");
  const statusMenuRef = useRef(null);

  // ===== Modal =====
  const [open, setOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detail, setDetail] = useState(null);

  // ====== Toast & ConfirmDialog (giống ProductsPage) ======
  const [toasts, setToasts] = useState([]);
  const [confirmDialog, setConfirmDialog] = useState(null);
  const toastIdRef = useRef(1);

  const removeToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const addToast = useCallback(
    (type, title, message) => {
      const id = toastIdRef.current++;
      setToasts((prev) => [...prev, { id, type, message, title: title || undefined }]);

      setTimeout(() => {
        removeToast(id);
      }, 5000);

      return id;
    },
    [removeToast]
  );

  const openConfirm = useCallback(({ title, message, onConfirm }) => {
    setConfirmDialog({
      title,
      message,
      onConfirm: async () => {
        setConfirmDialog(null);
        await onConfirm?.();
      },
      onCancel: () => setConfirmDialog(null),
    });
  }, []);

  const STATUS_OPTIONS = useMemo(
    () => [
      { value: "", label: "Tất cả trạng thái" },
      { value: "Pending", label: "Chờ thanh toán" },
      { value: "Paid", label: "Đã thanh toán" },
      { value: "NeedReview", label: "Cần kiểm tra" },
      { value: "Timeout", label: "Hủy do quá hạn" },
      { value: "Cancelled", label: "Đã hủy" },
      { value: "DupCancelled", label: "Hủy do trùng" },
      { value: "Replaced", label: "Đã thay thế" },
      { value: "Refunded", label: "Đã hoàn tiền" },
    ],
    []
  );

  const TYPE_OPTIONS = useMemo(
    () => [
      { value: "", label: "Tất cả loại" },
      { value: "Order", label: "Đơn hàng" },
      { value: "SupportPlan", label: "Gói hỗ trợ" },
    ],
    []
  );

  const fetchPayments = useCallback(async () => {
    setError("");
    setUpdateStatusErr("");
    setLoading(true);
    try {
      const params = {
        search: q || undefined,
        createdFrom: from || undefined,
        createdTo: to || undefined,
        paymentStatus: status || undefined,
        transactionType: type || undefined,
        amountFrom: minAmount || undefined,
        amountTo: maxAmount || undefined,
        sortBy: sortField,
        sortDir,
        pageIndex: page,
        pageSize,
      };

      const paged = await paymentApi.listPaged(params);
      setItems(Array.isArray(paged.items) ? paged.items : []);
      setTotal(Number(paged.totalItems || 0));
    } catch (e) {
      setItems([]);
      setTotal(0);
      setError(e?.message || "Không tải được danh sách giao dịch.");
    } finally {
      setLoading(false);
    }
  }, [q, from, to, status, type, minAmount, maxAmount, sortField, sortDir, page, pageSize]);

  useEffect(() => {
    fetchPayments();
  }, [fetchPayments]);

  const applyFilters = useCallback(() => {
    const fmtYmd = (d) => {
      if (!d) return "";
      const yy = d.getFullYear();
      const mm = String(d.getMonth() + 1).padStart(2, "0");
      const dd = String(d.getDate()).padStart(2, "0");
      return `${yy}-${mm}-${dd}`;
    };

    setQ(qDraft.trim());
    setFrom(fmtYmd(fromDraft));
    setTo(fmtYmd(toDraft));
    setStatus(statusDraft);
    setType(typeDraft);
    setMinAmount(minDraft);
    setMaxAmount(maxDraft);
    setPage(1);
  }, [qDraft, fromDraft, toDraft, statusDraft, typeDraft, minDraft, maxDraft]);

  const resetFilters = useCallback(() => {
    setQDraft("");
    setFromDraft(null);
    setToDraft(null);
    setStatusDraft("");
    setTypeDraft("");
    setMinDraft("");
    setMaxDraft("");

    setQ("");
    setFrom("");
    setTo("");
    setStatus("");
    setType("");
    setMinAmount("");
    setMaxAmount("");

    setSortField("createdAt");
    setSortDir("desc");
    setPage(1);
    setPageSize(10);
  }, []);

  const toggleSort = useCallback((field) => {
    setPage(1);
    setSortField((prev) => {
      if (prev !== field) {
        setSortDir("asc");
        return field;
      }
      setSortDir((d) => (d === "asc" ? "desc" : "asc"));
      return prev;
    });
  }, []);

  const sortIcon = (field) => {
    if (sortField !== field) return null;
    return sortDir === "asc" ? <Ico.Up /> : <Ico.Down />;
  };

  const openDetail = useCallback(async (payment) => {
    const paymentId = getPaymentId(payment);
    if (!paymentId) return;

    setOpen(true);
    setDetail(null);
    setDetailLoading(true);

    try {
      const res = await paymentApi.get(paymentId);
      setDetail(res?.data ?? res ?? payment);
    } catch {
      setDetail(payment);
    } finally {
      setDetailLoading(false);
    }
  }, []);

  const closeModal = useCallback(() => {
    setOpen(false);
    setDetail(null);
    setDetailLoading(false);
  }, []);

  useEffect(() => {
    const onKey = (e) => {
      if (e.key === "Escape") {
        closeModal();
        setOpenStatusMenuId("");
        // (optional) đóng confirm dialog nếu muốn
        // setConfirmDialog(null);
      }
    };
    if (open) window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, closeModal]);

  // ✅ Close status menu when clicking outside
  useEffect(() => {
    if (!openStatusMenuId) return;
    const onDown = (e) => {
      if (!statusMenuRef.current) return;
      if (!statusMenuRef.current.contains(e.target)) {
        setOpenStatusMenuId("");
      }
    };
    window.addEventListener("mousedown", onDown);
    return () => window.removeEventListener("mousedown", onDown);
  }, [openStatusMenuId]);

  const getAllowedNextStatuses = useCallback((currentStatus) => {
    const cur = mapStatusToUi(currentStatus).value;
    const key = normalizeStatusKey(cur);

    // Refunded là trạng thái cuối
    if (key === "REFUNDED") return [];

    // NeedReview: chỉ có thể resolve sang Paid/Cancelled
    if (key === "NEEDREVIEW") return ["Paid", "Cancelled"];

    // Các trạng thái khác: chỉ cho chuyển sang Refunded
    return ["Refunded"];
  }, []);

  const buildMenuOptions = useCallback(
    (currentStatus) => {
      const cur = mapStatusToUi(currentStatus).value;
      const allow = getAllowedNextStatuses(cur);

      // chỉ show các trạng thái khác current
      return allow
        .filter((x) => x && x !== cur)
        .map((v) => ({ value: v, label: mapStatusToUi(v).label, ui: mapStatusToUi(v) }));
    },
    [getAllowedNextStatuses]
  );

  const handleAdminChangeStatus = useCallback(
    async (payment, desiredStatus) => {
      const paymentId = getPaymentId(payment);
      if (!paymentId) return;

      const current = mapStatusToUi(getStatus(payment)).value;
      if (!desiredStatus || desiredStatus === current) return;

      setUpdateStatusErr("");
      setUpdatingPaymentId(paymentId);

      try {
        await paymentApi.adminUpdateStatus(paymentId, desiredStatus, "");

        // update nhanh UI, rồi refetch để đồng bộ
        setItems((prev) =>
          prev.map((x) => {
            const id = getPaymentId(x);
            if (id !== paymentId) return x;
            return { ...x, status: desiredStatus, Status: desiredStatus };
          })
        );

        await fetchPayments();

        // ✅ Toast thành công (theo yêu cầu)
        const desiredUi = mapStatusToUi(desiredStatus);
        addToast(
          "success",
          "Thành công",
          `Đã đổi trạng thái thanh toán "${paymentId}" sang "${desiredUi.label}".`
        );
      } catch (e) {
        const msg =
          e?.response?.data?.message ||
          e?.response?.data?.Message ||
          e?.message ||
          "Không thể cập nhật trạng thái thanh toán.";
        setUpdateStatusErr(msg);
      } finally {
        setUpdatingPaymentId("");
      }
    },
    [fetchPayments, addToast]
  );

  // ✅ Confirm dialog giống ProductsPage (không dùng window.confirm)
  const confirmAndChange = useCallback(
    (payment, desiredStatus) => {
      const currentUi = mapStatusToUi(getStatus(payment));
      const desiredUi = mapStatusToUi(desiredStatus);

      setOpenStatusMenuId("");

      const isRefund = normalizeStatusKey(desiredUi.value) === "REFUNDED";
      const title = "Xác nhận đổi trạng thái?";
      const message = isRefund
        ? `Đổi trạng thái từ "${currentUi.label}" sang "${desiredUi.label}"?\nLưu ý: thao tác này không thể hoàn tác.`
        : `Đổi trạng thái từ "${currentUi.label}" sang "${desiredUi.label}"?`;

      openConfirm({
        title,
        message,
        onConfirm: async () => {
          await handleAdminChangeStatus(payment, desiredStatus);
        },
      });
    },
    [openConfirm, handleAdminChangeStatus]
  );

  const renderPages = () => {
    const pages = [];
    const maxButtons = 5;
    let start = Math.max(1, page - 2);
    let end = Math.min(totalPages, start + maxButtons - 1);
    start = Math.max(1, end - maxButtons + 1);

    if (start > 1) {
      pages.push(
        <button key="p1" className={`apl-pageBtn ${page === 1 ? "active" : ""}`} onClick={() => setPage(1)}>
          1
        </button>
      );
      if (start > 2) pages.push(<span key="d1" className="apl-dots">…</span>);
    }

    for (let i = start; i <= end; i++) {
      pages.push(
        <button key={i} className={`apl-pageBtn ${page === i ? "active" : ""}`} onClick={() => setPage(i)}>
          {i}
        </button>
      );
    }

    if (end < totalPages) {
      if (end < totalPages - 1) pages.push(<span key="d2" className="apl-dots">…</span>);
      pages.push(
        <button
          key={`p${totalPages}`}
          className={`apl-pageBtn ${page === totalPages ? "active" : ""}`}
          onClick={() => setPage(totalPages)}
        >
          {totalPages}
        </button>
      );
    }

    return pages;
  };

  return (
    <>
      <div className="apl-page">
        <div className="order-payment-header">
          <h2>Danh sách giao dịch</h2>
        </div>

        <div className="apl-card">
          {/* ===== Toolbar (2 rows) ===== */}
          <div className="apl-toolbar">
            <div className="apl-toolbarRows">
              <div className="apl-row1">
                <div className="apl-group">
                  <span>Tìm kiếm</span>
                  <input
                    value={qDraft}
                    onChange={(e) => setQDraft(e.target.value)}
                    placeholder="VD: mã thanh toán... hoặc email@example.com"
                    onKeyDown={(e) => {
                      if (e.key === "Enter") applyFilters();
                    }}
                  />
                </div>

                <div className="apl-group">
                  <span>Từ ngày</span>
                  <DatePicker
                    selected={fromDraft}
                    onChange={(d) => setFromDraft(d)}
                    className="apl-dateInput"
                    dateFormat="dd/MM/yyyy"
                    placeholderText="Chọn ngày"
                  />
                </div>

                <div className="apl-group">
                  <span>Đến ngày</span>
                  <DatePicker
                    selected={toDraft}
                    onChange={(d) => setToDraft(d)}
                    className="apl-dateInput"
                    dateFormat="dd/MM/yyyy"
                    placeholderText="Chọn ngày"
                  />
                </div>
              </div>

              <div className="apl-row2">
                <div className="apl-group">
                  <span>Trạng thái</span>
                  <select value={statusDraft} onChange={(e) => setStatusDraft(e.target.value)}>
                    {STATUS_OPTIONS.map((o) => (
                      <option key={o.value || "all"} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="apl-group">
                  <span>Loại thanh toán</span>
                  <select value={typeDraft} onChange={(e) => setTypeDraft(e.target.value)}>
                    {TYPE_OPTIONS.map((o) => (
                      <option key={o.value || "all"} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="apl-group">
                  <span>Số tiền</span>
                  <div className="apl-amountRange">
                    <input value={minDraft} onChange={(e) => setMinDraft(e.target.value)} placeholder="Từ" inputMode="numeric" />
                    <input value={maxDraft} onChange={(e) => setMaxDraft(e.target.value)} placeholder="Đến" inputMode="numeric" />
                  </div>
                </div>

                <div className="apl-filterActions">
                  <button className="apl-iconActionBtn primary" onClick={applyFilters} title="Lọc">
                    <Ico.Filter />
                  </button>
                  <button className="apl-iconActionBtn" onClick={resetFilters} title="Đặt lại">
                    <Ico.Refresh />
                  </button>
                </div>
              </div>
            </div>

            <div className="apl-topInfo">
              {loading ? "Đang tải..." : `Tổng: ${total} • Trang ${page}/${totalPages}`}
            </div>

            {updateStatusErr ? <div className="apl-inlineError">{updateStatusErr}</div> : null}
          </div>

          {/* ===== Table ===== */}
          <div className="apl-tableWrap">
            <table className="apl-table">
              <thead>
                <tr>
                  <th>
                    <button className="apl-sortBtn" onClick={() => toggleSort("paymentId")}>
                      Mã thanh toán {sortIcon("paymentId")}
                    </button>
                  </th>
                  <th>
                    <button className="apl-sortBtn" onClick={() => toggleSort("transactionType")}>
                      Loại thanh toán {sortIcon("transactionType")}
                    </button>
                  </th>
                  <th>
                    <button className="apl-sortBtn" onClick={() => toggleSort("amount")}>
                      Số tiền {sortIcon("amount")}
                    </button>
                  </th>
                  <th>
                    <button className="apl-sortBtn" onClick={() => toggleSort("status")}>
                      Trạng thái {sortIcon("status")}
                    </button>
                  </th>
                  <th>
                    <button className="apl-sortBtn" onClick={() => toggleSort("createdAt")}>
                      Ngày tạo {sortIcon("createdAt")}
                    </button>
                  </th>
                  <th className="apl-th-actions">Chi tiết</th>
                </tr>
              </thead>

              <tbody>
                {!loading && error ? (
                  <tr>
                    <td colSpan={6} style={{ padding: 14, color: "#b91c1c", fontWeight: 800 }}>
                      {error}
                    </td>
                  </tr>
                ) : null}

                {!loading && !error && items.length === 0 ? (
                  <tr>
                    <td colSpan={6} style={{ padding: 14, color: "#6b7280", fontWeight: 800 }}>
                      Không có giao dịch phù hợp.
                    </td>
                  </tr>
                ) : null}

                {items.map((p) => {
                  const pid = getPaymentId(p);
                  const created = getCreatedAt(p);
                  const amount = getAmount(p);
                  const statusUi = mapStatusToUi(getStatus(p));
                  const typeUi = mapTargetToUi(getTargetType(p));

                  const menuOptions = buildMenuOptions(statusUi.value);
                  const isFinal = normalizeStatusKey(statusUi.value) === "REFUNDED";
                  const isUpdating = updatingPaymentId === pid;
                  const canOpen = !!pid && !isUpdating && !isFinal && menuOptions.length > 0;

                  const isMenuOpen = openStatusMenuId === pid;

                  return (
                    <tr key={pid || JSON.stringify(p)}>
                      <td>
                        <span className="apl-paymentId" title={pid}>
                          {pid || "—"}
                        </span>
                      </td>

                      <td>
                        <span className={`apl-pill ${typeUi.cls}`}>{typeUi.label}</span>
                      </td>

                      <td style={{ fontWeight: 900 }}>{formatVnd(amount)}</td>

                      <td>
                        <div className="apl-statusMenuWrap" ref={isMenuOpen ? statusMenuRef : null}>
                          <button
                            type="button"
                            className={`apl-pill apl-pillDropdown ${statusUi.cls} ${
                              canOpen ? "" : "disabled"
                            } ${isMenuOpen ? "open" : ""}`}
                            title={
                              canOpen
                                ? "Đổi trạng thái"
                                : isFinal
                                  ? "Trạng thái cuối (không thể đổi)"
                                  : "Không có trạng thái khả thi để đổi"
                            }
                            disabled={!canOpen}
                            onClick={() => {
                              if (!canOpen) return;
                              setOpenStatusMenuId((prev) => (prev === pid ? "" : pid));
                            }}
                          >
                            <span className="apl-pillText">{statusUi.label}</span>
                            <span className="apl-pillCaret" aria-hidden="true">
                              <Ico.Caret />
                            </span>
                          </button>

                          {isMenuOpen ? (
                            <div className="apl-statusMenu" role="menu" aria-label="Chọn trạng thái">
                              {menuOptions.map((opt) => (
                                <button
                                  key={opt.value}
                                  type="button"
                                  role="menuitem"
                                  className={`apl-statusMenuItem ${opt.ui.cls}`}
                                  onClick={() => confirmAndChange(p, opt.value)}
                                >
                                  {opt.label}
                                </button>
                              ))}
                            </div>
                          ) : null}
                        </div>
                      </td>

                      <td style={{ fontWeight: 800 }}>{fmtDateTime(created)}</td>

                      <td className="apl-td-actions">
                        <button className="apl-icon-btn" title="Xem chi tiết" onClick={() => openDetail(p)}>
                          <Ico.Eye />
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {/* ===== Pager ===== */}
          <div className="apl-pager">
            <div className="apl-pager-center">
              <button className="apl-navBtn" onClick={() => setPage((x) => Math.max(1, x - 1))} disabled={page <= 1}>
                ← Trước
              </button>

              {renderPages()}

              <button className="apl-navBtn" onClick={() => setPage((x) => Math.min(totalPages, x + 1))} disabled={page >= totalPages}>
                Sau →
              </button>
            </div>

            <div className="apl-pager-right">
              <select
                className="apl-pageSizeSelect"
                value={pageSize}
                onChange={(e) => {
                  setPageSize(Number(e.target.value));
                  setPage(1);
                }}
                title="Kích thước trang"
              >
                {[10, 20, 50].map((n) => (
                  <option key={n} value={n}>
                    {n}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>

        {/* ===== Modal centered ===== */}
        {open ? (
          <div
            className="apl-modal-backdrop"
            onMouseDown={(e) => {
              if (e.target === e.currentTarget) closeModal();
            }}
          >
            <div className="apl-modal">
              <div className="apl-modal-header">
                <h3 className="apl-modal-title">Chi tiết thanh toán</h3>
                <button className="apl-modal-close" onClick={closeModal} title="Đóng">
                  <Ico.X />
                </button>
              </div>

              <div className="apl-modal-body">
                {detailLoading ? (
                  <div style={{ padding: 8, fontWeight: 800, color: "#6b7280" }}>Đang tải chi tiết...</div>
                ) : (
                  (() => {
                    const d = detail || {};
                    const pid = getPaymentId(d);
                    const created = getCreatedAt(d);
                    const amount = getAmount(d);
                    const statusUi = mapStatusToUi(getStatus(d));
                    const typeUi = mapTargetToUi(getTargetType(d));

                    return (
                      <div className="apl-modal-grid">
                        <div className="apl-field">
                          <div className="apl-field-label">Mã thanh toán</div>
                          <div className="apl-field-value">{pid || "—"}</div>
                        </div>

                        <div className="apl-field">
                          <div className="apl-field-label">Trạng thái</div>
                          <div className="apl-field-value">
                            <span className={`apl-pill ${statusUi.cls}`}>{statusUi.label}</span>
                          </div>
                        </div>

                        <div className="apl-field">
                          <div className="apl-field-label">Loại thanh toán</div>
                          <div className="apl-field-value">
                            <span className={`apl-pill ${typeUi.cls}`}>{typeUi.label}</span>
                          </div>
                        </div>

                        <div className="apl-field">
                          <div className="apl-field-label">Số tiền</div>
                          <div className="apl-field-value">{formatVnd(amount)}</div>
                        </div>

                        <div className="apl-field">
                          <div className="apl-field-label">Ngày tạo</div>
                          <div className="apl-field-value">{fmtDateTime(created)}</div>
                        </div>
                      </div>
                    );
                  })()
                )}
              </div>
            </div>
          </div>
        ) : null}
      </div>

      {/* ✅ Toast + Confirm Dialog (giống ProductsPage) */}
      <ToastContainer toasts={toasts} onRemove={removeToast} confirmDialog={confirmDialog} />
    </>
  );
}
