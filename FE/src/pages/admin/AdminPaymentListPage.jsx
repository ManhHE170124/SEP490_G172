// File: src/pages/admin/AdminPaymentListPage.jsx
import React from "react";
import ToastContainer from "../../components/Toast/ToastContainer";
import { paymentApi } from "../../services/paymentApi";
import "./OrderPaymentPage.css";

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

const toUtcIsoFromDateOnly = (dateStr, endOfDay = false) => {
  if (!dateStr) return "";
  const time = endOfDay ? "23:59:59" : "00:00:00";
  const d = new Date(`${dateStr}T${time}+07:00`);
  if (Number.isNaN(d.getTime())) return "";
  return d.toISOString();
};

function useDebouncedValue(value, delay = 350) {
  const [debounced, setDebounced] = React.useState(value);
  React.useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}

function Modal({ open, title, onClose, children, width = 980 }) {
  if (!open) return null;
  return (
    <div className="cat-modal">
      <div className="cat-modal-overlay" onMouseDown={onClose} />
      <div className="cat-modal-card" style={{ maxWidth: width }}>
        <div className="cat-modal-header">
          <h3 style={{ margin: 0 }}>{title}</h3>
        </div>
        <div className="cat-modal-body">{children}</div>
        <div className="cat-modal-footer">
          <button type="button" className="btn ghost" onClick={onClose}>
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
}

const toggleSortState = (current, key) =>
  current.sortBy === key
    ? { sortBy: key, sortDir: current.sortDir === "asc" ? "desc" : "asc" }
    : { sortBy: key, sortDir: "asc" };

const renderSortIndicator = (current, key) => {
  if (!current || current.sortBy !== key) return null;
  return current.sortDir === "asc" ? " ▲" : " ▼";
};

const pick = (obj, ...keys) => {
  for (const k of keys) {
    const v = obj?.[k];
    if (v !== undefined && v !== null) return v;
  }
  return undefined;
};

function Icon({ name }) {
  if (name === "eye") {
    return (
      <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">
        <path
          fill="currentColor"
          d="M12 5c5.5 0 9.5 4.5 10.5 6-1 1.5-5 6-10.5 6S2.5 12.5 1.5 11C2.5 9.5 6.5 5 12 5zm0 10a4 4 0 1 0 0-8 4 4 0 0 0 0 8zm0-2.2a1.8 1.8 0 1 1 0-3.6 1.8 1.8 0 0 1 0 3.6z"
        />
      </svg>
    );
  }
  return null;
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

export default function AdminPaymentListPage() {
  // Toast
  const [toasts, setToasts] = React.useState([]);
  const addToast = React.useCallback((type, message, title) => {
    const tid = `${Date.now()}_${Math.random().toString(16).slice(2)}`;
    setToasts((prev) => [...prev, { id: tid, type, message, title }]);
    return tid;
  }, []);
  const removeToast = React.useCallback((tid) => setToasts((prev) => prev.filter((t) => t.id !== tid)), []);

  const [loading, setLoading] = React.useState(false);
  const [paged, setPaged] = React.useState({ pageIndex: 1, pageSize: 10, totalItems: 0, items: [] });
  const [sort, setSort] = React.useState({ sortBy: "createdAt", sortDir: "desc" });

  const [filter, setFilter] = React.useState({
    search: "",
    createdFrom: "",
    createdTo: "",
    paymentStatus: "",
    transactionType: "", // Order | SupportPlan
    amountFrom: "",
    amountTo: "",
  });

  const debouncedSearch = useDebouncedValue(filter.search, 350);

  const [detailModal, setDetailModal] = React.useState({ open: false, id: null, loading: false, data: null });

  const openDetail = async (paymentId) => {
    if (!paymentId) return;
    setDetailModal({ open: true, id: paymentId, loading: true, data: null });
    try {
      const res = await paymentApi.get(paymentId);
      setDetailModal((m) => ({ ...m, loading: false, data: unwrap(res) }));
    } catch (err) {
      console.error(err);
      addToast("error", err?.response?.data?.message || "Không tải được payment detail.", "Lỗi");
      setDetailModal((m) => ({ ...m, loading: false }));
    }
  };

  const closeDetail = () => setDetailModal({ open: false, id: null, loading: false, data: null });

  const goToPage = (pageIndex) => setPaged((p) => ({ ...p, pageIndex: Math.max(1, Number(pageIndex) || 1) }));
  const totalPages = Math.max(1, Math.ceil((paged.totalItems || 0) / (paged.pageSize || 10)));

  React.useEffect(() => {
    setLoading(true);

    const params = {
      search: (debouncedSearch || "").trim() || undefined,
      createdFrom: toUtcIsoFromDateOnly(filter.createdFrom, false) || undefined,
      createdTo: toUtcIsoFromDateOnly(filter.createdTo, true) || undefined,
      paymentStatus: filter.paymentStatus || undefined,
      transactionType: filter.transactionType || undefined,
      amountFrom: filter.amountFrom !== "" ? Number(filter.amountFrom) : undefined,
      amountTo: filter.amountTo !== "" ? Number(filter.amountTo) : undefined,
      sortBy: sort.sortBy,
      sortDir: sort.sortDir,
      pageIndex: paged.pageIndex,
      pageSize: paged.pageSize,
    };

    paymentApi
      .listPaged(params)
      .then((x) => setPaged((p) => ({ ...p, ...x })))
      .catch((err) => {
        console.error(err);
        addToast("error", err?.response?.data?.message || "Không tải được danh sách payments.", "Lỗi");
      })
      .finally(() => setLoading(false));
  }, [
    debouncedSearch,
    filter.createdFrom,
    filter.createdTo,
    filter.paymentStatus,
    filter.transactionType,
    filter.amountFrom,
    filter.amountTo,
    sort.sortBy,
    sort.sortDir,
    paged.pageIndex,
    paged.pageSize,
    addToast,
  ]);

  return (
    <div className="op-page">
      <ToastContainer toasts={toasts} onClose={removeToast} />

      <div className="order-payment-header">
        <h2>Giao dịch (Admin)</h2>
      </div>

      <div className="op-toolbar">
        <div className="op-filters">
          <div className="op-group">
            <span>Search (PaymentId / OrderId / UserId)</span>
            <input
              value={filter.search}
              onChange={(e) => {
                setFilter((f) => ({ ...f, search: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
              placeholder="PaymentId / OrderId / UserId"
            />
          </div>

          <div className="op-group">
            <span>Từ ngày</span>
            <input
              type="date"
              value={filter.createdFrom}
              onChange={(e) => {
                setFilter((f) => ({ ...f, createdFrom: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
            />
          </div>

          <div className="op-group">
            <span>Đến ngày</span>
            <input
              type="date"
              value={filter.createdTo}
              onChange={(e) => {
                setFilter((f) => ({ ...f, createdTo: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
            />
          </div>

          <div className="op-group">
            <span>Trạng thái</span>
            <input
              value={filter.paymentStatus}
              onChange={(e) => {
                setFilter((f) => ({ ...f, paymentStatus: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
              placeholder="Pending / Paid / Cancelled / ..."
            />
          </div>

          <div className="op-group">
            <span>Loại giao dịch</span>
            <select
              value={filter.transactionType}
              onChange={(e) => {
                setFilter((f) => ({ ...f, transactionType: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
            >
              <option value="">Tất cả</option>
              <option value="Order">Order</option>
              <option value="SupportPlan">SupportPlan</option>
            </select>
          </div>

          <div className="op-group">
            <span>Số tiền từ</span>
            <input
              inputMode="numeric"
              value={filter.amountFrom}
              onChange={(e) => {
                setFilter((f) => ({ ...f, amountFrom: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
              placeholder="0"
            />
          </div>

          <div className="op-group">
            <span>Đến</span>
            <input
              inputMode="numeric"
              value={filter.amountTo}
              onChange={(e) => {
                setFilter((f) => ({ ...f, amountTo: e.target.value }));
                setPaged((p) => ({ ...p, pageIndex: 1 }));
              }}
              placeholder="1000000"
            />
          </div>

          <div className="op-group">
            <span>Page size</span>
            <select
              value={paged.pageSize}
              onChange={(e) => setPaged((p) => ({ ...p, pageSize: Number(e.target.value) || 10, pageIndex: 1 }))}
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

      <div className="cat-card" style={{ marginTop: 12 }}>
        <div className="cat-card-title" style={{ display: "flex", justifyContent: "space-between", gap: 10 }}>
          <div>Danh sách giao dịch</div>
          <div className="badge gray">
            {paged.totalItems || 0} items • page {paged.pageIndex}/{totalPages}
          </div>
        </div>

        {loading ? <div className="op-empty">Đang tải…</div> : null}

        {!loading && (
          <div style={{ overflowX: "auto" }}>
            <table className="op-table">
              <thead>
                <tr>
                  <th>
                    <button
                      className="table-sort-header"
                      onClick={() => {
                        setSort((s) => toggleSortState(s, "paymentId"));
                        setPaged((p) => ({ ...p, pageIndex: 1 }));
                      }}
                    >
                      PaymentId{renderSortIndicator(sort, "paymentId")}
                    </button>
                  </th>
                  <th>
                    <button
                      className="table-sort-header"
                      onClick={() => {
                        setSort((s) => toggleSortState(s, "transactionType"));
                        setPaged((p) => ({ ...p, pageIndex: 1 }));
                      }}
                    >
                      Loại{renderSortIndicator(sort, "transactionType")}
                    </button>
                  </th>
                  <th>Mục tiêu</th>
                  <th className="text-right">
                    <button
                      className="table-sort-header"
                      onClick={() => {
                        setSort((s) => toggleSortState(s, "amount"));
                        setPaged((p) => ({ ...p, pageIndex: 1 }));
                      }}
                    >
                      Số tiền{renderSortIndicator(sort, "amount")}
                    </button>
                  </th>
                  <th>
                    <button
                      className="table-sort-header"
                      onClick={() => {
                        setSort((s) => toggleSortState(s, "status"));
                        setPaged((p) => ({ ...p, pageIndex: 1 }));
                      }}
                    >
                      Trạng thái{renderSortIndicator(sort, "status")}
                    </button>
                  </th>
                  <th>
                    <button
                      className="table-sort-header"
                      onClick={() => {
                        setSort((s) => toggleSortState(s, "createdAt"));
                        setPaged((p) => ({ ...p, pageIndex: 1 }));
                      }}
                    >
                      Ngày tạo{renderSortIndicator(sort, "createdAt")}
                    </button>
                  </th>
                  <th className="op-th-actions">Actions</th>
                </tr>
              </thead>
              <tbody>
                {paged.items.map((p) => {
                  const paymentId = pick(p, "paymentId", "PaymentId");
                  const targetType = pick(p, "targetType", "TargetType");
                  const targetDisplayId = pick(p, "targetDisplayId", "TargetDisplayId") || pick(p, "targetId", "TargetId");
                  const amount = pick(p, "amount", "Amount");
                  const status = pick(p, "status", "Status");
                  const createdAt = pick(p, "createdAt", "CreatedAt");

                  return (
                    <tr key={paymentId}>
                      <td className="mono">{paymentId}</td>
                      <td>{targetType || "—"}</td>
                      <td className="mono">{targetDisplayId || "—"}</td>
                      <td className="text-right mono">{formatMoney(amount)}</td>
                      <td>{status ? <span className="badge gray">{status}</span> : "—"}</td>
                      <td>{formatVnDateTime(createdAt)}</td>
                      <td className="op-td-actions">
                        <div className="op-actions">
                          <IconButton title="Chi tiết" onClick={() => openDetail(paymentId)} variant="primary">
                            <Icon name="eye" />
                          </IconButton>
                        </div>
                      </td>
                    </tr>
                  );
                })}

                {paged.items.length === 0 ? (
                  <tr>
                    <td colSpan={7}>
                      <div className="op-empty">Không có dữ liệu.</div>
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "space-between", gap: 10, marginTop: 12, flexWrap: "wrap" }}>
          <button type="button" className="btn ghost" disabled={paged.pageIndex <= 1} onClick={() => goToPage(paged.pageIndex - 1)}>
            Trang trước
          </button>

          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <span className="badge gray">Page</span>
            <input style={{ width: 90, height: 36 }} value={paged.pageIndex} onChange={(e) => goToPage(e.target.value)} />
            <span className="badge gray">/ {totalPages}</span>
          </div>

          <button type="button" className="btn ghost" disabled={paged.pageIndex >= totalPages} onClick={() => goToPage(paged.pageIndex + 1)}>
            Trang sau
          </button>
        </div>
      </div>

      <Modal open={detailModal.open} title={`Payment detail: ${detailModal.id || "—"}`} onClose={closeDetail} width={1020}>
        {detailModal.loading ? <div className="op-empty">Đang tải…</div> : null}

        {!detailModal.loading && (
          <div className="op-2col">
            <div>
              <div className="op-subtext">PaymentId</div>
              <div className="mono">{pick(detailModal.data, "paymentId", "PaymentId") || "—"}</div>
            </div>
            <div>
              <div className="op-subtext">Số tiền</div>
              <div className="mono">{formatMoney(pick(detailModal.data, "amount", "Amount"))}</div>
            </div>

            <div>
              <div className="op-subtext">Trạng thái</div>
              <div>{pick(detailModal.data, "status", "Status") ? <span className="badge gray">{pick(detailModal.data, "status", "Status")}</span> : "—"}</div>
            </div>
            <div>
              <div className="op-subtext">Ngày tạo</div>
              <div>{formatVnDateTime(pick(detailModal.data, "createdAt", "CreatedAt"))}</div>
            </div>

            <div>
              <div className="op-subtext">Provider</div>
              <div className="mono">{pick(detailModal.data, "provider", "Provider") || "—"}</div>
            </div>
            <div>
              <div className="op-subtext">ProviderOrderCode</div>
              <div className="mono">{pick(detailModal.data, "providerOrderCode", "ProviderOrderCode") ?? "—"}</div>
            </div>

            <div>
              <div className="op-subtext">PaymentLinkId</div>
              <div className="mono">{pick(detailModal.data, "paymentLinkId", "PaymentLinkId") || "—"}</div>
            </div>
            <div>
              <div className="op-subtext">Email</div>
              <div className="mono">{pick(detailModal.data, "email", "Email") || "—"}</div>
            </div>

            <div>
              <div className="op-subtext">TargetType</div>
              <div className="mono">{pick(detailModal.data, "targetType", "TargetType") || "—"}</div>
            </div>
            <div>
              <div className="op-subtext">TargetDisplayId</div>
              <div className="mono">{pick(detailModal.data, "targetDisplayId", "TargetDisplayId") || pick(detailModal.data, "targetId", "TargetId") || "—"}</div>
            </div>

            <div>
              <div className="op-subtext">ExpiresAtUtc</div>
              <div className="mono">{String(pick(detailModal.data, "expiresAtUtc", "ExpiresAtUtc") || "—")}</div>
            </div>
            <div>
              <div className="op-subtext">IsExpired</div>
              <div className="mono">{String(pick(detailModal.data, "isExpired", "IsExpired") ?? "—")}</div>
            </div>

            <div style={{ gridColumn: "1 / -1" }}>
              <div className="op-subtext">CheckoutUrl</div>
              <div className="mono" style={{ wordBreak: "break-all" }}>
                {pick(detailModal.data, "checkoutUrl", "CheckoutUrl") || "—"}
              </div>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
