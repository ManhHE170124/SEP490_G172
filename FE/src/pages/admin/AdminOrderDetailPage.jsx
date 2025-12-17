// File: src/pages/admin/AdminOrderDetailPage.jsx
import React from "react";
import { useNavigate, useParams } from "react-router-dom";
import { orderApi } from "../../services/orderApi";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./OrderPaymentPage.css";

/* ===== Helpers (giữ style cũ) ===== */
const unwrap = (res) => res?.data ?? res;

const formatVnDateTime = (value) => {
  if (!value) return "—";
  const d = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(d.getTime())) return "—";
  return d.toLocaleString("vi-VN");
};

const formatMoney = (n) => {
  if (n === null || n === undefined) return "—";
  const num = Number(n);
  if (Number.isNaN(num)) return "—";
  return `${num.toLocaleString("vi-VN")} đ`;
};

const shortId = (s, head = 6, tail = 4) => {
  const v = String(s || "");
  if (!v) return "—";
  if (v.length <= head + tail + 3) return v;
  return `${v.slice(0, head)}…${v.slice(-tail)}`;
};

const copyText = async (text) => {
  try {
    if (!text) return false;
    await navigator.clipboard.writeText(String(text));
    return true;
  } catch {
    return false;
  }
};

function useDebouncedValue(value, delay = 350) {
  const [debounced, setDebounced] = React.useState(value);
  React.useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}

const toggleSortState = (current, key) =>
  current.sortBy === key
    ? { sortBy: key, sortDir: current.sortDir === "asc" ? "desc" : "asc" }
    : { sortBy: key, sortDir: "asc" };

const renderSortIndicator = (current, key) => {
  if (!current || current.sortBy !== key) return null;
  return current.sortDir === "asc" ? " ▲" : " ▼";
};

const getPaymentStatusClass = (status) => {
  const s = (status || "").toLowerCase();
  if (["paid", "success", "completed", "refunded"].includes(s)) return "status-pill payment-paid";
  if (["pending"].includes(s)) return "status-pill payment-pending";
  if (["cancelled", "timeout", "dupcancelled", "failed", "replaced"].includes(s))
    return "status-pill payment-cancelled";
  return "status-pill payment-unknown";
};

const getPaymentStatusLabel = (status) => {
  const s = (status || "").toLowerCase();
  switch (s) {
    case "pending":
      return "Chờ thanh toán";
    case "paid":
      return "Đã thanh toán";
    case "success":
      return "Success";
    case "completed":
      return "Completed";
    case "cancelled":
      return "Đã hủy";
    case "failed":
      return "Thất bại";
    case "refunded":
      return "Hoàn tiền";
    case "timeout":
      return "Hết hạn";
    case "dupcancelled":
      return "Hủy do tạo phiên mới";
    case "needreview":
      return "Cần kiểm tra";
    case "replaced":
      return "Replaced";
    default:
      return status || "Không xác định";
  }
};

function Icon({ name }) {
  switch (name) {
    case "copy":
      return (
        <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
          <path
            fill="currentColor"
            d="M8 7a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2h-9a2 2 0 0 1-2-2V7zm-3 10V4a2 2 0 0 1 2-2h10v2H7v13H5z"
          />
        </svg>
      );
    default:
      return null;
  }
}

function IconButton({ title, ariaLabel, onClick, disabled, variant = "default", children }) {
  return (
    <button
      type="button"
      className={`op-icon-btn ${variant}`}
      onClick={onClick}
      disabled={disabled}
      title={title}
      aria-label={ariaLabel || title}
    >
      {children}
    </button>
  );
}

export default function AdminOrderDetailPage() {
  const nav = useNavigate();
  const { id } = useParams(); // /admin/orders/:id

  // Toast
  const [toasts, setToasts] = React.useState([]);
  const addToast = React.useCallback((type, message, title) => {
    const tid = `${Date.now()}_${Math.random().toString(16).slice(2)}`;
    setToasts((prev) => [...prev, { id: tid, type, message, title }]);
    return tid;
  }, []);
  const removeToast = React.useCallback((tid) => {
    setToasts((prev) => prev.filter((t) => t.id !== tid));
  }, []);

  const [loading, setLoading] = React.useState(false);
  const [detail, setDetail] = React.useState(null); // { order, orderItems, pageIndex, pageSize, totalItems }

  // filters for order items
  const [itemsFilter, setItemsFilter] = React.useState({
    search: "",
    minPrice: "",
    maxPrice: "",
    pageIndex: 1,
    pageSize: 10,
  });

  const [itemsSort, setItemsSort] = React.useState({ sortBy: "orderdetailid", sortDir: "asc" });
  const debouncedItemSearch = useDebouncedValue(itemsFilter.search, 350);

  React.useEffect(() => {
    if (!id) return;

    setLoading(true);

    orderApi
      .get(id, {
        includePaymentAttempts: true,
        includeCheckoutUrl: false,
        search: (debouncedItemSearch || "").trim() || undefined,
        minPrice: itemsFilter.minPrice !== "" ? Number(itemsFilter.minPrice) : undefined,
        maxPrice: itemsFilter.maxPrice !== "" ? Number(itemsFilter.maxPrice) : undefined,
        sortBy: itemsSort.sortBy,
        sortDir: itemsSort.sortDir,
        pageIndex: itemsFilter.pageIndex,
        pageSize: itemsFilter.pageSize,
      })
      .then((res) => setDetail(unwrap(res)))
      .catch((err) => {
        console.error(err);
        addToast("error", err?.response?.data?.message || "Không tải được chi tiết đơn hàng.", "Lỗi");
      })
      .finally(() => setLoading(false));
  }, [
    id,
    debouncedItemSearch,
    itemsFilter.minPrice,
    itemsFilter.maxPrice,
    itemsFilter.pageIndex,
    itemsFilter.pageSize,
    itemsSort.sortBy,
    itemsSort.sortDir,
    addToast,
  ]);

  const order = detail?.order || detail?.Order || null;
  const items = Array.isArray(detail?.orderItems)
    ? detail.orderItems
    : Array.isArray(detail?.OrderItems)
    ? detail.OrderItems
    : [];

  const totalItems = detail?.totalItems ?? detail?.TotalItems ?? items.length;
  const pageIndex = detail?.pageIndex ?? detail?.PageIndex ?? itemsFilter.pageIndex;
  const pageSize = detail?.pageSize ?? detail?.PageSize ?? itemsFilter.pageSize;
  const totalPages = Math.max(1, Math.ceil((totalItems || 0) / (pageSize || 10)));

  const effectiveFinal =
    order != null
      ? order.finalAmount ??
        (order.totalAmount != null && order.discountAmount != null
          ? order.totalAmount - order.discountAmount
          : order.totalAmount)
      : 0;

  const payment = order?.payment || null;
  const attempts = Array.isArray(order?.paymentAttempts) ? order.paymentAttempts : [];

  const onCopy = async (text) => {
    const ok = await copyText(text);
    addToast(ok ? "success" : "error", ok ? "Đã copy" : "Copy thất bại", ok ? "OK" : "Lỗi");
  };

  return (
    <div className="op-page">
      <ToastContainer toasts={toasts} onClose={removeToast} />

      <div className="order-payment-header">
        <h2>Chi tiết đơn hàng</h2>
        <div className="op-inline-actions">
          <button type="button" className="btn ghost" onClick={() => nav("/admin/orders")}>
            ← Quay lại danh sách
          </button>
          <button type="button" className="btn" onClick={() => nav(`/admin/payments?search=${encodeURIComponent(id || "")}`)}>
            Xem Payments theo OrderId
          </button>
        </div>
      </div>

      {loading ? <div className="op-empty">Đang tải…</div> : null}

      {!loading && order && (
        <>
          <div className="cat-card" style={{ marginTop: 12 }}>
            <div className="cat-card-title" style={{ display: "flex", justifyContent: "space-between", gap: 10 }}>
              <div>Tổng quan</div>
              {order?.status ? <span className="badge gray">{order.status}</span> : null}
            </div>

            <div className="detail-section-box op-2col">
              <div>
                <div className="detail-label">OrderNumber</div>
                <div className="detail-value mono">{order.orderNumber || shortId(order.orderId)}</div>
              </div>
              <div>
                <div className="detail-label">OrderId</div>
                <div className="detail-value mono">{order.orderId}</div>
              </div>
              <div>
                <div className="detail-label">Ngày tạo</div>
                <div className="detail-value">{formatVnDateTime(order.createdAt)}</div>
              </div>
              <div>
                <div className="detail-label">Email đơn hàng</div>
                <div className="detail-value">{order.email || "—"}</div>
              </div>

              <div>
                <div className="detail-label">UserEmail</div>
                <div className="detail-value">{order.userEmail || "—"}</div>
              </div>
              <div>
                <div className="detail-label">UserPhone</div>
                <div className="detail-value">{order.userPhone || "—"}</div>
              </div>
            </div>

            <div className="detail-section-title" style={{ marginTop: 12 }}>
              Số tiền
            </div>
            <div className="detail-section-box op-3col">
              <div>
                <div className="detail-label">Total</div>
                <div className="detail-value text-mono">{formatMoney(order.totalAmount)}</div>
              </div>
              <div>
                <div className="detail-label">Discount</div>
                <div className="detail-value text-mono">{formatMoney(order.discountAmount)}</div>
              </div>
              <div>
                <div className="detail-label">Final</div>
                <div className="detail-value text-mono">{formatMoney(effectiveFinal)}</div>
              </div>
            </div>

            <div className="detail-section-title" style={{ marginTop: 12 }}>
              Thanh toán (best/latest)
            </div>
            <div className="detail-section-box">
              {payment ? (
                <div className="op-2col">
                  <div>
                    <div className="detail-label">PaymentId</div>
                    <div className="detail-value mono">{payment.paymentId}</div>
                  </div>
                  <div>
                    <div className="detail-label">Trạng thái</div>
                    <div className="detail-value">
                      <span className={getPaymentStatusClass(payment.status)}>{getPaymentStatusLabel(payment.status)}</span>
                      {payment.isExpired ? <span className="badge gray" style={{ marginLeft: 8 }}>Expired</span> : null}
                    </div>
                  </div>
                  <div>
                    <div className="detail-label">Số tiền</div>
                    <div className="detail-value text-mono">{formatMoney(payment.amount)}</div>
                  </div>
                  <div>
                    <div className="detail-label">ProviderOrderCode</div>
                    <div className="detail-value mono">{payment.providerOrderCode != null ? payment.providerOrderCode : "—"}</div>
                  </div>

                  <div style={{ gridColumn: "1 / -1", display: "flex", gap: 8, flexWrap: "wrap", marginTop: 6 }}>
                    <button
                      type="button"
                      className="btn"
                      onClick={() => nav(`/admin/payments?search=${encodeURIComponent(payment.paymentId)}`)}
                    >
                      Mở chi tiết payment
                    </button>
                  </div>
                </div>
              ) : (
                <div className="op-empty">Đơn này chưa có payment.</div>
              )}
            </div>
          </div>

          {/* Filters for items */}
          <div className="cat-card" style={{ marginTop: 12 }}>
            <div className="cat-card-title" style={{ display: "flex", justifyContent: "space-between", gap: 10 }}>
              <div>Order items</div>
              <div className="badge gray">
                {totalItems || 0} items • page {pageIndex}/{totalPages}
              </div>
            </div>

            <div className="op-toolbar">
              <div className="op-filters">
                <div className="op-group">
                  <span>Search (OrderDetailId / Variant / Key / Account)</span>
                  <input
                    value={itemsFilter.search}
                    onChange={(e) => setItemsFilter((f) => ({ ...f, search: e.target.value, pageIndex: 1 }))}
                    placeholder="VD: variant, key, account..."
                  />
                </div>

                <div className="op-group">
                  <span>Giá từ</span>
                  <input
                    inputMode="numeric"
                    value={itemsFilter.minPrice}
                    onChange={(e) => setItemsFilter((f) => ({ ...f, minPrice: e.target.value, pageIndex: 1 }))}
                    placeholder="0"
                  />
                </div>

                <div className="op-group">
                  <span>Đến</span>
                  <input
                    inputMode="numeric"
                    value={itemsFilter.maxPrice}
                    onChange={(e) => setItemsFilter((f) => ({ ...f, maxPrice: e.target.value, pageIndex: 1 }))}
                    placeholder="1000000"
                  />
                </div>

                <div className="op-group">
                  <span>Page size</span>
                  <select
                    value={itemsFilter.pageSize}
                    onChange={(e) => setItemsFilter((f) => ({ ...f, pageSize: Number(e.target.value) || 10, pageIndex: 1 }))}
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

            <div style={{ overflowX: "auto" }}>
              <table className="order-items-table">
                <thead>
                  <tr>
                    <th>
                      <button
                        className="table-sort-header"
                        onClick={() => {
                          setItemsSort((s) => toggleSortState(s, "orderdetailid"));
                          setItemsFilter((f) => ({ ...f, pageIndex: 1 }));
                        }}
                      >
                        OrderDetailId{renderSortIndicator(itemsSort, "orderdetailid")}
                      </button>
                    </th>
                    <th>Sản phẩm</th>
                    <th>
                      <button
                        className="table-sort-header"
                        onClick={() => {
                          setItemsSort((s) => toggleSortState(s, "varianttitle"));
                          setItemsFilter((f) => ({ ...f, pageIndex: 1 }));
                        }}
                      >
                        Biến thể{renderSortIndicator(itemsSort, "varianttitle")}
                      </button>
                    </th>
                    <th className="text-right">
                      <button
                        className="table-sort-header"
                        onClick={() => {
                          setItemsSort((s) => toggleSortState(s, "quantity"));
                          setItemsFilter((f) => ({ ...f, pageIndex: 1 }));
                        }}
                      >
                        SL{renderSortIndicator(itemsSort, "quantity")}
                      </button>
                    </th>
                    <th className="text-right">
                      <button
                        className="table-sort-header"
                        onClick={() => {
                          setItemsSort((s) => toggleSortState(s, "unitprice"));
                          setItemsFilter((f) => ({ ...f, pageIndex: 1 }));
                        }}
                      >
                        Đơn giá{renderSortIndicator(itemsSort, "unitprice")}
                      </button>
                    </th>
                    <th className="text-right">Thành tiền</th>
                    <th>Key / Account</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((it) => {
                    const keyStrings = it.keyStrings || it.KeyStrings || [];
                    const keyStringSingle = it.keyString || it.KeyString;
                    const keyPreview = (Array.isArray(keyStrings) && keyStrings.length > 0 ? keyStrings[0] : keyStringSingle) || null;

                    const accountEmail = it.accountEmail || it.AccountEmail;
                    const accountPassword = it.accountPassword || it.AccountPassword;
                    const accounts = it.accounts || it.Accounts;

                    let accountPreview = null;
                    if (accountEmail || accountPassword) {
                      accountPreview = { email: accountEmail || "—", password: accountPassword || "—" };
                    } else if (Array.isArray(accounts) && accounts.length > 0) {
                      const a0 = accounts[0];
                      accountPreview = {
                        email: a0?.email || a0?.Email || "—",
                        password: a0?.password || a0?.Password || "—",
                      };
                    }

                    return (
                      <tr key={it.orderDetailId}>
                        <td className="mono">{shortId(it.orderDetailId, 10, 6)}</td>
                        <td>{it.productName || "—"}</td>
                        <td>{it.variantTitle || "—"}</td>
                        <td className="text-right">{it.quantity}</td>
                        <td className="text-right">{formatMoney(it.unitPrice)}</td>
                        <td className="text-right">{formatMoney(it.subTotal)}</td>
                        <td>
                          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                            <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                              <span className="badge gray">Key</span>
                              <span className="mono" title={keyPreview || ""}>
                                {keyPreview ? shortId(keyPreview, 10, 10) : "—"}
                              </span>
                              {keyPreview ? (
                                <IconButton title="Copy key" variant="ghost" onClick={() => onCopy(keyPreview)}>
                                  <Icon name="copy" />
                                </IconButton>
                              ) : null}
                              {Array.isArray(keyStrings) && keyStrings.length > 1 ? (
                                <span className="badge gray">{keyStrings.length} keys</span>
                              ) : null}
                            </div>

                            <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                              <span className="badge gray">Account</span>
                              {accountPreview ? (
                                <>
                                  <span className="mono" title={accountPreview.email}>
                                    {shortId(accountPreview.email, 10, 10)}
                                  </span>
                                  <IconButton title="Copy email" variant="ghost" onClick={() => onCopy(accountPreview.email)}>
                                    <Icon name="copy" />
                                  </IconButton>
                                  <span className="mono" title={accountPreview.password}>
                                    {shortId(accountPreview.password, 10, 10)}
                                  </span>
                                  <IconButton
                                    title="Copy password"
                                    variant="ghost"
                                    onClick={() => onCopy(accountPreview.password)}
                                  >
                                    <Icon name="copy" />
                                  </IconButton>
                                </>
                              ) : (
                                <span className="op-subtext">—</span>
                              )}
                            </div>
                          </div>
                        </td>
                      </tr>
                    );
                  })}

                  {items.length === 0 ? (
                    <tr>
                      <td colSpan={7}>
                        <div className="op-empty">Không có items.</div>
                      </td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>

            <div style={{ display: "flex", justifyContent: "space-between", gap: 10, marginTop: 12, flexWrap: "wrap" }}>
              <button
                type="button"
                className="btn ghost"
                disabled={pageIndex <= 1}
                onClick={() => setItemsFilter((f) => ({ ...f, pageIndex: Math.max(1, f.pageIndex - 1) }))}
              >
                Trang trước
              </button>

              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <span className="badge gray">Page</span>
                <input
                  style={{ width: 90, height: 36 }}
                  value={pageIndex}
                  onChange={(e) =>
                    setItemsFilter((f) => ({ ...f, pageIndex: Math.max(1, Number(e.target.value) || 1) }))
                  }
                />
                <span className="badge gray">/ {totalPages}</span>
              </div>

              <button
                type="button"
                className="btn ghost"
                disabled={pageIndex >= totalPages}
                onClick={() => setItemsFilter((f) => ({ ...f, pageIndex: Math.min(totalPages, f.pageIndex + 1) }))}
              >
                Trang sau
              </button>
            </div>
          </div>

          {attempts.length > 0 ? (
            <div className="cat-card" style={{ marginTop: 12 }}>
              <div className="cat-card-title">Payment attempts (cùng target)</div>
              <div className="detail-section-box" style={{ overflowX: "auto" }}>
                <table className="order-items-table">
                  <thead>
                    <tr>
                      <th>PaymentId</th>
                      <th>Trạng thái</th>
                      <th>ProviderOrderCode</th>
                      <th>Ngày tạo</th>
                      <th>Hết hạn</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {attempts.map((a) => (
                      <tr key={a.paymentId}>
                        <td className="mono">{shortId(a.paymentId, 10, 6)}</td>
                        <td>
                          <span className={getPaymentStatusClass(a.status)}>{getPaymentStatusLabel(a.status)}</span>
                          {a.isExpired ? <span className="badge gray" style={{ marginLeft: 8 }}>Expired</span> : null}
                        </td>
                        <td className="mono">{a.providerOrderCode || "—"}</td>
                        <td>{formatVnDateTime(a.createdAt)}</td>
                        <td>{formatVnDateTime(a.expiresAtUtc)}</td>
                        <td style={{ whiteSpace: "nowrap", textAlign: "right" }}>
                          <button
                            type="button"
                            className="btn ghost"
                            onClick={() => nav(`/admin/payments?search=${encodeURIComponent(a.paymentId)}`)}
                          >
                            Mở
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ) : null}
        </>
      )}

      {!loading && !order ? <div className="op-empty">Không tìm thấy đơn hàng.</div> : null}
    </div>
  );
}
