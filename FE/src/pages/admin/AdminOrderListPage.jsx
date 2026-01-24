// File: src/pages/admin/AdminOrderListPage.jsx
import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import DatePicker from "react-datepicker";
import "react-datepicker/dist/react-datepicker.css";
import { orderApi } from "../../services/orderApi";
import axiosClient from "../../api/axiosClient";
import "./AdminOrderListPage.css";
import formatDatetime from "../../utils/formatDatetime";
import ToastContainer from "../../components/Toast/ToastContainer";

/** ===== Icons (SVG inline) ===== */
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
  Caret: (p) => (
    <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" {...p}>
      <path fill="currentColor" d="M7 10l5 5 5-5H7z" />
    </svg>
  ),
};

const fmtDateTime = (d) => formatDatetime(d);

const formatMoneyVnd = (v) => {
  const n = Number(v ?? 0);
  return n.toLocaleString("vi-VN") + " đ";
};

const normalizeText = (v) => String(v ?? "").trim();
const normalizeStatusKey = (s) => String(s || "").trim().toUpperCase();

/**
 * Parse tiền VN: loại bỏ dấu ngàn (.) và chuyển dấu thập phân (,) thành (.)
 */
const parseMoney = (value) => {
  if (value === null || value === undefined) return { num: null, raw: "" };
  const s = String(value).trim();
  if (!s) return { num: null, raw: "" };
  const normalized = s.replace(/\./g, "").replace(/,/g, ".");
  const num = Number(normalized);
  if (!Number.isFinite(num)) return { num: null, raw: s };
  return { num, raw: s };
};

/**
 * Format số để hiển thị trong input: dùng định dạng VN (ngàn dùng ., thập phân dùng ,)
 */
const formatForInput = (value) => {
  if (value === null || value === undefined || value === "") return "";
  const s = String(value).trim();
  const normalized = s.replace(/\./g, "").replace(/,/g, ".");
  const num = Number(normalized);
  if (!Number.isFinite(num)) return s;
  return num.toLocaleString("vi-VN", {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  });
};

/**
 * Validate format tiền: tối đa 18 chữ số phần nguyên, 2 chữ số phần thập phân
 */
const isValidDecimal18_2 = (raw) => {
  if (!raw) return false;
  const normalized = String(raw).trim().replace(/\./g, "").replace(/,/g, ".");
  if (!normalized) return false;
  const neg = normalized[0] === "-";
  const unsigned = neg ? normalized.slice(1) : normalized;
  const parts = unsigned.split(".");
  const intPart = parts[0] || "0";
  const fracPart = parts[1] || "";
  if (intPart.replace(/^0+/, "").length > 16) return false;
  if (fracPart.length > 2) return false;
  return true;
};

/**
 * ✅ Order statuses đúng theo BE:
 * PendingPayment, Paid, Cancelled, CancelledByTimeout, NeedsManualAction, Refunded
 */
const ORDER_STATUS_OPTIONS = [
  { value: "", label: "Tất cả trạng thái" },
  { value: "PendingPayment", label: "Chờ thanh toán" },
  { value: "Paid", label: "Đã thanh toán" },
  { value: "Refunded", label: "Đã hoàn tiền" },
  { value: "NeedsManualAction", label: "Cần xử lý thủ công" },
  { value: "CancelledByTimeout", label: "Đã hủy do quá hạn" },
  { value: "Cancelled", label: "Đã hủy" },
];

const mapOrderStatusToUi = (s) => {
  const v = normalizeStatusKey(s);

  if (v === "PENDINGPAYMENT" || v === "PENDING")
    return { label: "Chờ thanh toán", cls: "pending", value: "PendingPayment" };

  if (v === "PAID" || v === "SUCCESS" || v === "COMPLETED")
    return { label: "Đã thanh toán", cls: "paid", value: "Paid" };

  if (v === "REFUNDED") return { label: "Đã hoàn tiền", cls: "refunded", value: "Refunded" };

  if (v === "NEEDSMANUALACTION")
    return { label: "Cần xử lý thủ công", cls: "manual", value: "NeedsManualAction" };

  if (v === "CANCELLEDBYTIMEOUT" || v === "TIMEOUT")
    return { label: "Đã hủy do quá hạn", cls: "cancelled", value: "CancelledByTimeout" };

  if (v === "CANCELLED") return { label: "Đã hủy", cls: "cancelled", value: "Cancelled" };

  return { label: s ? String(s) : "Không rõ", cls: "other", value: s || "Unknown" };
};

export default function AdminOrderListPage() {
  const nav = useNavigate();

  const DEFAULT_FILTERS = useMemo(
    () => ({
      search: "",
      createdFrom: null,
      createdTo: null,
      orderStatus: "",
      minTotal: "",
      maxTotal: "",
    }),
    []
  );

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // draft filters
  const [draft, setDraft] = useState(DEFAULT_FILTERS);
  const [filters, setFilters] = useState(DEFAULT_FILTERS);

  // sort/paged
  const [sort, setSort] = useState({ sortBy: "createdat", sortDir: "desc" });
  const [paged, setPaged] = useState({
    pageIndex: 1,
    pageSize: 10,
    totalItems: 0,
    items: [],
  });

  const totalPages = useMemo(() => {
    const ps = Math.max(1, Number(paged.pageSize || 10));
    return Math.max(1, Math.ceil((paged.totalItems || 0) / ps));
  }, [paged.pageSize, paged.totalItems]);

  /** ====== Toast & ConfirmDialog ====== */
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
      setTimeout(() => removeToast(id), 5000);
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

  /** ====== Manual change gate (ẩn khỏi UI) ====== */
  // manualGate[orderId] = { state: 'checking'|'ok'|'blocked'|'unknown', checkedAt }
  const [manualGate, setManualGate] = useState({});
  const [updatingOrderId, setUpdatingOrderId] = useState("");

  // dropdown status menu
  const [openStatusMenuId, setOpenStatusMenuId] = useState("");
  const statusMenuRef = useRef(null);

  // close menu when clicking outside
  useEffect(() => {
    if (!openStatusMenuId) return;
    const onDown = (e) => {
      if (!statusMenuRef.current) return;
      if (!statusMenuRef.current.contains(e.target)) setOpenStatusMenuId("");
    };
    window.addEventListener("mousedown", onDown);
    return () => window.removeEventListener("mousedown", onDown);
  }, [openStatusMenuId]);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const res = await orderApi.listPaged({
        ...filters,
        sortBy: sort.sortBy,
        sortDir: sort.sortDir,
        pageIndex: paged.pageIndex,
        pageSize: paged.pageSize,
      });

      setPaged((p) => ({
        ...p,
        totalItems: res.totalItems ?? 0,
        items: res.items ?? [],
      }));
    } catch (e) {
      setError(e?.message || "Không thể tải danh sách đơn hàng");
      setPaged((p) => ({ ...p, totalItems: 0, items: [] }));
    } finally {
      setLoading(false);
    }
  }, [filters, sort.sortBy, sort.sortDir, paged.pageIndex, paged.pageSize]);

  useEffect(() => {
    load();
  }, [load]);

  const fmtYmd = (d) => {
    if (!d) return "";
    const dt = new Date(d);
    if (Number.isNaN(dt.getTime())) return "";
    const yy = dt.getFullYear();
    const mm = String(dt.getMonth() + 1).padStart(2, "0");
    const dd = String(dt.getDate()).padStart(2, "0");
    return `${yy}-${mm}-${dd}`;
  };

  const applyFilters = () => {
    setPaged((p) => ({ ...p, pageIndex: 1 }));
    setFilters({
      ...draft,
      search: String(draft.search || "").trim(),
      createdFrom: fmtYmd(draft.createdFrom),
      createdTo: fmtYmd(draft.createdTo),
    });
  };

  const resetFilters = () => {
    setSort({ sortBy: "createdat", sortDir: "desc" });
    setPaged((p) => ({ ...p, pageIndex: 1, pageSize: 10 }));
    setDraft(DEFAULT_FILTERS);
    setFilters(DEFAULT_FILTERS);
    setOpenStatusMenuId("");
  };

  const onDraftChange = (key, val) => setDraft((f) => ({ ...f, [key]: val }));

  const toggleSort = (sortBy) => {
    setPaged((p) => ({ ...p, pageIndex: 1 }));
    setSort((s) => {
      const nextDir = s.sortBy === sortBy ? (s.sortDir === "asc" ? "desc" : "asc") : "desc";
      return { sortBy, sortDir: nextDir };
    });
  };

  const onEnterApply = (e) => {
    if (e.key === "Enter") applyFilters();
  };

  /** ✅ Gate check: nếu Order NeedsManualAction thì check /payments NeedReview (không hiển thị lên UI) */
  useEffect(() => {
    const items = Array.isArray(paged.items) ? paged.items : [];

    const needCheck = items
      .map((o) => {
        const orderId = normalizeText(o.orderId ?? o.OrderId);
        const st = normalizeText(o.status ?? o.Status);
        return { orderId, st };
      })
      .filter((x) => x.orderId && normalizeStatusKey(x.st) === "NEEDSMANUALACTION")
      .map((x) => x.orderId);

    if (needCheck.length === 0) {
      setManualGate({});
      return;
    }

    let cancelled = false;

    // set checking (chỉ cho các id cần check)
    setManualGate((prev) => {
      const next = { ...(prev || {}) };
      for (const id of needCheck) {
        const p = prev?.[id];
        if (p?.state === "ok" || p?.state === "blocked") continue;
        next[id] = { state: "checking" };
      }
      return next;
    });

    const parseTotalItems = (data) => {
      const t = data?.totalItems ?? data?.TotalItems;
      if (typeof t === "number") return t;
      const it = data?.items ?? data?.Items;
      return Array.isArray(it) ? it.length : 0;
    };

    (async () => {
      const results = await Promise.all(
        needCheck.map(async (orderId) => {
          try {
            const res = await axiosClient.get("payments", {
              params: {
                search: orderId,
                transactionType: "Order",
                paymentStatus: "NeedReview",
                pageIndex: 1,
                pageSize: 1,
              },
            });

            const data = res?.data ?? res;
            const total = parseTotalItems(data);
            return { orderId, state: total > 0 ? "blocked" : "ok" };
          } catch {
            return { orderId, state: "unknown" };
          }
        })
      );

      if (cancelled) return;

      setManualGate((prev) => {
        const next = { ...(prev || {}) };
        for (const r of results) next[r.orderId] = { state: r.state, checkedAt: Date.now() };
        return next;
      });
    })();

    return () => {
      cancelled = true;
    };
  }, [paged.items]);

  /** ✅ Allowed next statuses for Order */
  const getAllowedNextOrderStatuses = useCallback((orderStatusRaw, gateState) => {
    const cur = mapOrderStatusToUi(orderStatusRaw).value;
    const k = normalizeStatusKey(cur);

    // chỉ đổi khi NeedsManualAction
    if (k !== "NEEDSMANUALACTION") return [];

    // Payment NeedReview => bị chặn
    if (gateState === "blocked") return [];

    // ok hoặc unknown => cho phép đổi (unknown: BE sẽ chặn nếu thật sự cần)
    return ["Paid", "Cancelled"];
  }, []);

  const buildMenuOptions = useCallback(
    (orderStatusRaw, gateState) => {
      const allow = getAllowedNextOrderStatuses(orderStatusRaw, gateState);
      const cur = mapOrderStatusToUi(orderStatusRaw).value;

      return allow
        .filter((x) => x && normalizeStatusKey(x) !== normalizeStatusKey(cur))
        .map((v) => {
          const ui = mapOrderStatusToUi(v);
          return { value: v, label: ui.label, ui };
        });
    },
    [getAllowedNextOrderStatuses]
  );

  const doManualUpdateOrderStatus = useCallback(
    async (orderId, desiredStatus) => {
      if (!orderId || !desiredStatus) return;

      setUpdatingOrderId(orderId);
      try {
        // OrdersController: PATCH /orders/{orderId}/status, body: { Status, Note }
        await axiosClient.patch(`orders/${orderId}/status`, {
          Status: desiredStatus,
          Note: "",
        });

        // update nhanh UI
        setPaged((p) => ({
          ...p,
          items: (p.items || []).map((x) => {
            const id = normalizeText(x.orderId ?? x.OrderId);
            if (id !== orderId) return x;
            return { ...x, status: desiredStatus, Status: desiredStatus };
          }),
        }));

        await load();

        const desiredUi = mapOrderStatusToUi(desiredStatus);
        addToast("success", "Thành công", `Đã đổi trạng thái đơn "${orderId}" sang "${desiredUi.label}".`);
      } catch (e) {
        const msg =
          e?.response?.data?.message ||
          e?.response?.data?.Message ||
          e?.message ||
          "Không thể cập nhật trạng thái đơn.";
        addToast("error", "Thất bại", msg);
      } finally {
        setUpdatingOrderId("");
      }
    },
    [load, addToast]
  );

  const confirmAndChange = useCallback(
    (orderId, currentStatusRaw, desiredStatus, gateState) => {
      setOpenStatusMenuId("");

      const currentUi = mapOrderStatusToUi(currentStatusRaw);
      const desiredUi = mapOrderStatusToUi(desiredStatus);

      const warn =
        gateState === "unknown"
          ? "\n(Lưu ý: không kiểm tra được Payment NeedReview — nếu BE chặn thì thao tác sẽ thất bại.)"
          : "";

      openConfirm({
        title: "Xác nhận đổi trạng thái?",
        message: `Đổi trạng thái từ "${currentUi.label}" sang "${desiredUi.label}"?${warn}`,
        onConfirm: async () => {
          await doManualUpdateOrderStatus(orderId, desiredStatus);
        },
      });
    },
    [openConfirm, doManualUpdateOrderStatus]
  );

  const buildPageButtons = () => {
    const current = paged.pageIndex;
    const total = totalPages;
    const btns = [];

    const pushBtn = (n) => {
      btns.push(
        <button
          key={`p-${n}`}
          className={`aol-pageBtn ${n === current ? "active" : ""}`}
          onClick={() => setPaged((p) => ({ ...p, pageIndex: n }))}
          type="button"
        >
          {n}
        </button>
      );
    };

    const pushDots = (key) => btns.push(<span key={key} className="aol-dots">…</span>);

    if (total <= 7) {
      for (let i = 1; i <= total; i++) pushBtn(i);
      return btns;
    }

    pushBtn(1);

    if (current > 3) pushDots("d1");

    const start = Math.max(2, current - 1);
    const end = Math.min(total - 1, current + 1);
    for (let i = start; i <= end; i++) pushBtn(i);

    if (current < total - 2) pushDots("d2");

    pushBtn(total);
    return btns;
  };

  return (
    <>
      <div className="aol-page">
        <div className="aol-top">
          <div>
            <div className="aol-title">Danh sách đơn hàng</div>
            <div className="aol-sub">Quản lý, lọc và xem chi tiết đơn hàng</div>
          </div>
        </div>

        <div className="aol-card">
          {/* ===== Filters (style cũ aol-*) ===== */}
          <div style={{ padding: "12px 14px 0" }}>
            <div className="aol-toolbar aol-toolbar-row1">
              <div className="aol-field">
                <label>Tìm kiếm (mã đơn / email)</label>
                <input
                  className="aol-input"
                  value={draft.search}
                  onChange={(e) => onDraftChange("search", e.target.value)}
                  onKeyDown={onEnterApply}
                  placeholder="VD: 1ffa... hoặc mail@example.com"
                />
              </div>

              <div className="aol-field">
                <label>Trạng thái</label>
                <select
                  className="aol-select"
                  value={draft.orderStatus}
                  onChange={(e) => onDraftChange("orderStatus", e.target.value)}
                >
                  {ORDER_STATUS_OPTIONS.map((o) => (
                    <option key={o.value || "all"} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="aol-field">
                <label>Từ ngày</label>
                <DatePicker
                  selected={draft.createdFrom}
                  onChange={(d) => onDraftChange("createdFrom", d)}
                  className="aol-dateInput"
                  dateFormat="dd/MM/yyyy"
                  placeholderText="Chọn ngày"
                />
              </div>

              <div className="aol-field">
                <label>Đến ngày</label>
                <DatePicker
                  selected={draft.createdTo}
                  onChange={(d) => onDraftChange("createdTo", d)}
                  className="aol-dateInput"
                  dateFormat="dd/MM/yyyy"
                  placeholderText="Chọn ngày"
                />
              </div>

              <div className="aol-field aol-field-actions">
                <button
                  type="button"
                  className="aol-btn icon primary"
                  onClick={applyFilters}
                  title="Lọc"
                  aria-label="Lọc"
                >
                  <Ico.Filter />
                </button>
                <button
                  type="button"
                  className="aol-btn icon"
                  onClick={resetFilters}
                  title="Đặt lại"
                  aria-label="Đặt lại"
                >
                  <Ico.Refresh />
                </button>
              </div>
            </div>

            <div className="aol-toolbar aol-toolbar-row2">
              <div className="aol-field">
                <label>Tổng tiền</label>
                <div className="aol-amountRange">
                  <input
                    className="aol-input"
                    type="text"
                    inputMode="decimal"
                    placeholder="Từ"
                    value={formatForInput(draft.minTotal)}
                    onChange={(e) => {
                      const raw = e.target.value;
                      if (/^[0-9.,]*$/.test(raw) && isValidDecimal18_2(raw)) {
                        onDraftChange("minTotal", raw);
                      } else if (raw === "") {
                        onDraftChange("minTotal", "");
                      }
                    }}
                    onKeyDown={onEnterApply}
                  />
                  <input
                    className="aol-input"
                    type="text"
                    inputMode="decimal"
                    placeholder="Đến"
                    value={formatForInput(draft.maxTotal)}
                    onChange={(e) => {
                      const raw = e.target.value;
                      if (/^[0-9.,]*$/.test(raw) && isValidDecimal18_2(raw)) {
                        onDraftChange("maxTotal", raw);
                      } else if (raw === "") {
                        onDraftChange("maxTotal", "");
                      }
                    }}
                    onKeyDown={onEnterApply}
                  />
                </div>
              </div>

              <div />
            </div>
          </div>

          <div className="aol-cardTop" style={{ marginTop: 10 }}>
            <div>
              <div className="aol-cardTitle">Kết quả</div>
              <div className="aol-meta">
                {loading
                  ? "Đang tải..."
                  : `Tổng: ${paged.totalItems} • Trang ${paged.pageIndex}/${totalPages}`}
              </div>
            </div>
          </div>

          {error ? <div className="aol-inlineError">{error}</div> : null}

          {/* ===== Table (style cũ) ===== */}
          <div className="aol-tableWrap">
            <table className="aol-table">
              <thead>
                <tr>
                  <th>Mã đơn</th>
                  <th>Người mua</th>
                  <th>Tổng tiền</th>
                  <th>Trạng thái</th>
                  <th>
                    <button
                      className="aol-sortBtn"
                      type="button"
                      onClick={() => toggleSort("createdat")}
                      title="Sắp xếp theo ngày tạo"
                    >
                      Ngày tạo {sort.sortBy === "createdat" ? (sort.sortDir === "asc" ? "▲" : "▼") : ""}
                    </button>
                  </th>
                  <th style={{ textAlign: "right" }}>Chi tiết</th>
                </tr>
              </thead>

              <tbody>
                {(paged.items || []).map((o) => {
                  const orderId = normalizeText(o.orderId ?? o.OrderId);
                  const buyerName = o.userName ?? o.UserName ?? "—";
                  const buyerEmail = o.userEmail ?? o.UserEmail ?? o.email ?? o.Email ?? "";

                  const totalAmount = Number(o.totalAmount ?? o.TotalAmount ?? 0);
                  const finalAmount = Number(o.finalAmount ?? o.FinalAmount ?? totalAmount);

                  const statusRaw = o.status ?? o.Status ?? "";
                  const statusUi = mapOrderStatusToUi(statusRaw);
                  const createdAt = o.createdAt ?? o.CreatedAt;

                  const gateState = manualGate?.[orderId]?.state || "";
                  const menuOptions = buildMenuOptions(statusRaw, gateState);

                  const isUpdating = updatingOrderId === orderId;
                  const isMenuOpen = openStatusMenuId === orderId;

                  const canOpen =
                    !!orderId &&
                    !isUpdating &&
                    menuOptions.length > 0 &&
                    gateState !== "checking" &&
                    gateState !== "blocked";

                  const titleHint =
                    gateState === "checking"
                      ? "Đang kiểm tra Payment NeedReview..."
                      : gateState === "blocked"
                      ? "Payment đang NeedReview → không được đổi thủ công Order"
                      : canOpen
                      ? "Đổi trạng thái"
                      : "Không thể đổi trạng thái";

                  return (
                    <tr key={orderId || JSON.stringify(o)}>
                      <td>
                        <span className="aol-orderId mono">{orderId || "—"}</span>
                      </td>

                      <td>
                        <div className="aol-buyerName">{buyerName}</div>
                        <div className="aol-buyerEmail">{buyerEmail || "—"}</div>
                      </td>

                      <td className="aol-amount">
                        <div>{formatMoneyVnd(finalAmount)}</div>
                        {totalAmount && totalAmount !== finalAmount ? (
                          <div className="aol-oldPrice">
                            {formatMoneyVnd(totalAmount)}
                          </div>
                        ) : null}
                      </td>

                      {/* ✅ Status pill dropdown if can change */}
                      <td>
                        {menuOptions.length > 0 ? (
                          <div className="aol-statusMenuWrap" ref={isMenuOpen ? statusMenuRef : null}>
                            <button
                              type="button"
                              className={`status-pill aol-pillDropdown ${statusUi.cls} ${
                                canOpen ? "" : "disabled"
                              }`}
                              title={titleHint}
                              disabled={!canOpen}
                              onClick={() => {
                                if (!canOpen) return;
                                setOpenStatusMenuId((prev) => (prev === orderId ? "" : orderId));
                              }}
                            >
                              <span className="aol-pillText">{statusUi.label}</span>
                              <span className="aol-pillCaret" aria-hidden="true">
                                <Ico.Caret />
                              </span>
                            </button>

                            {isMenuOpen ? (
                              <div className="aol-statusMenu" role="menu" aria-label="Chọn trạng thái đơn">
                                {menuOptions.map((opt) => (
                                  <button
                                    key={opt.value}
                                    type="button"
                                    role="menuitem"
                                    className={`aol-statusMenuItem ${opt.ui.cls}`}
                                    onClick={() => confirmAndChange(orderId, statusRaw, opt.value, gateState)}
                                  >
                                    {opt.label}
                                  </button>
                                ))}
                              </div>
                            ) : null}
                          </div>
                        ) : (
                          <span className={`status-pill ${statusUi.cls}`} title={titleHint}>
                            {statusUi.label}
                          </span>
                        )}
                      </td>

                      <td className="mono">{fmtDateTime(createdAt)}</td>

                      <td style={{ textAlign: "right" }}>
                        <div className="aol-actions">
                          <button
                            className="aol-miniActionBtn"
                            type="button"
                            title="Xem chi tiết"
                            onClick={() => nav(`/admin/orders/${orderId}`)}
                            disabled={!orderId}
                          >
                            <Ico.Eye />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}

                {!loading && (!paged.items || paged.items.length === 0) ? (
                  <tr>
                    <td colSpan={6} className="aol-empty">
                      Không có dữ liệu
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>

          {/* ===== Pager (style cũ) ===== */}
          <div className="aol-pager">
            <div className="aol-pager-center">
              <button
                className="aol-pageBtn"
                type="button"
                onClick={() => setPaged((p) => ({ ...p, pageIndex: Math.max(1, p.pageIndex - 1) }))}
                disabled={paged.pageIndex <= 1}
                title="Trang trước"
              >
                ← Trước
              </button>

              {buildPageButtons()}

              <button
                className="aol-pageBtn"
                type="button"
                onClick={() => setPaged((p) => ({ ...p, pageIndex: Math.min(totalPages, p.pageIndex + 1) }))}
                disabled={paged.pageIndex >= totalPages}
                title="Trang sau"
              >
                Sau →
              </button>
            </div>

            <div className="aol-pager-right">
              <select
                className="aol-select"
                value={paged.pageSize}
                onChange={(e) =>
                  setPaged((p) => ({
                    ...p,
                    pageIndex: 1,
                    pageSize: Number(e.target.value || 10),
                  }))
                }
                title="Số dòng mỗi trang"
              >
                {[10, 20, 30, 50].map((n) => (
                  <option key={n} value={n}>
                    {n}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>
      </div>

      {/* ✅ Toast + Confirm Dialog */}
      <ToastContainer toasts={toasts} onRemove={removeToast} confirmDialog={confirmDialog} />
    </>
  );
}
