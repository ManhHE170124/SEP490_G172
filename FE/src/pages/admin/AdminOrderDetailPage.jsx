// File: src/pages/admin/AdminOrderDetailPage.jsx
import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { orderApi } from "../../services/orderApi";
import "./AdminOrderDetailPage.css";
import formatDatetime from "../../utils/formatDatetime";

const formatMoneyVnd = (n) => {
  const x = Number(n ?? 0);
  try {
    return new Intl.NumberFormat("vi-VN").format(x) + " đ";
  } catch {
    return `${x} đ`;
  }
};

const formatDateTime = (dt) => formatDatetime(dt);

const normalizeStatusKey = (s) => String(s || "").trim().toUpperCase();

/**
 * Parse tiền VN: loại bỏ dấu ngàn (.) và chuyển dấu thập phân (,) thành (.)
 */
const parseMoney = (value) => {
  if (value === null || value === undefined) return { num: null, raw: "" };
  const s = String(value).trim();
  if (!s) return { num: null, raw: "" };
  // Normalize: remove thousand separators (.) then convert decimal comma -> dot
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
 * ✅ Map đúng Order status theo BE:
 * PendingPayment, Paid, Cancelled, CancelledByTimeout, NeedsManualAction, Refunded
 * (vẫn tolerant thêm Timeout/Success/Completed nếu dữ liệu legacy)
 */
const statusVi = (s) => {
  const v = normalizeStatusKey(s);
  if (!v) return "Không rõ";

  if (v === "PAID" || v === "SUCCESS" || v === "COMPLETED") return "Đã thanh toán";
  if (v === "PENDINGPAYMENT" || v === "PENDING") return "Chờ thanh toán";
  if (v === "NEEDSMANUALACTION") return "Cần xử lý thủ công";
  if (v === "CANCELLEDBYTIMEOUT" || v === "TIMEOUT") return "Hủy do quá hạn";
  if (v === "CANCELLED") return "Đã hủy";
  if (v === "REFUNDED") return "Đã hoàn tiền";

  // fallback
  return String(s);
};

const statusPillClass = (s) => {
  const v = normalizeStatusKey(s);

  if (v === "PAID" || v === "SUCCESS" || v === "COMPLETED") return "aod-pill aod-paid";
  if (v === "PENDINGPAYMENT" || v === "PENDING") return "aod-pill aod-pending";
  if (v === "NEEDSMANUALACTION") return "aod-pill aod-pending";
  if (v === "CANCELLEDBYTIMEOUT" || v === "TIMEOUT") return "aod-pill aod-cancelled";
  if (v === "CANCELLED") return "aod-pill aod-cancelled";
  if (v === "REFUNDED") return "aod-pill aod-refunded";

  return "aod-pill aod-unknown";
};

const pickArr = (v) => (Array.isArray(v) ? v : []);

const pickAccounts = (it) => {
  const accounts = pickArr(it?.accounts ?? it?.Accounts);

  // fallback single
  const singleEmail = it?.accountEmail ?? it?.AccountEmail;
  const singleUsername = it?.accountUsername ?? it?.AccountUsername;
  const singlePassword = it?.accountPassword ?? it?.AccountPassword;

  if (accounts.length > 0) return accounts;

  if (singleEmail || singleUsername || singlePassword) {
    return [
      {
        Email: singleEmail,
        Username: singleUsername,
        Password: singlePassword,
        email: singleEmail,
        username: singleUsername,
        password: singlePassword,
      },
    ];
  }

  return [];
};

const pickKeys = (it) => {
  const list = pickArr(it?.keyStrings ?? it?.KeyStrings);
  const single = it?.keyString ?? it?.KeyString;
  if (list.length > 0) return list;
  if (single) return [single];
  return [];
};

const maskSecret = (s) => {
  const str = String(s ?? "");
  if (!str || str === "—") return "—";
  if (str.length <= 8) return "••••••••";
  return `${str.slice(0, 4)}••••••${str.slice(-4)}`;
};

const EyeIcon = ({ open = false }) => {
  if (open) {
    return (
      <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
        <path
          fill="currentColor"
          d="M2.1 3.51 3.51 2.1 21.9 20.49 20.49 21.9l-3.04-3.04c-1.6.75-3.41 1.14-5.45 1.14C6 20 2.73 15.61 1 12c.7-1.45 1.8-3.05 3.3-4.5L2.1 3.51Zm9.9 4.49a4 4 0 0 1 4 4c0 .36-.05.71-.14 1.05l-1.7-1.7c.02-.12.04-.24.04-.35a2 2 0 0 0-2-2c-.12 0-.23.01-.35.03l-1.7-1.7c.33-.08.68-.13 1.05-.13Zm-6.03 6.03c-.53-.64-1-1.32-1.4-2.03c1.35-2.43 4.02-5 7.43-5c.72 0 1.41.11 2.07.3l-1.64 1.64A4 4 0 0 0 8.94 12l-1.97 1.97Zm4.12 4.12c.6.2 1.24.32 1.91.32c3.41 0 6.08-2.57 7.43-5c-.52-.93-1.23-1.91-2.11-2.79l1.42-1.42C20.17 9.33 21.23 10.81 22 12c-1.73 3.61-5 8-10 8c-1.13 0-2.2-.14-3.2-.42l1.32-1.32Z"
        />
      </svg>
    );
  }
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
      <path
        fill="currentColor"
        d="M12 5c5 0 9 5 10 7c-1 2-5 7-10 7S3 14 2 12c1-2 5-7 10-7Zm0 2C8.6 7 5.8 10.1 4.3 12c1.5 1.9 4.3 5 7.7 5s6.2-3.1 7.7-5C18.2 10.1 15.4 7 12 7Zm0 2.25A2.75 2.75 0 1 1 9.25 12A2.75 2.75 0 0 1 12 9.25Zm0 1.5A1.25 1.25 0 1 0 13.25 12A1.25 1.25 0 0 0 12 10.75Z"
      />
    </svg>
  );
};

const FunnelIcon = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
    <path
      fill="currentColor"
      d="M3 5h18l-7 8v5a1 1 0 0 1-.55.89l-3 1.5A1 1 0 0 1 9 19.5V13L3 5z"
    />
  </svg>
);

const ResetIcon = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
    <path
      fill="currentColor"
      d="M12 6V3L8 7l4 4V8a4 4 0 1 1-4 4H6a6 6 0 1 0 6-6Z"
    />
  </svg>
);

const CloseIcon = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
    <path
      fill="currentColor"
      d="M18.3 5.71 12 12l6.3 6.29-1.41 1.42L10.59 13.4 4.29 19.71 2.88 18.29 9.17 12 2.88 5.71 4.29 4.29l6.3 6.3 6.29-6.3 1.42 1.42Z"
    />
  </svg>
);

const SortIcon = ({ active, dir }) => {
  if (!active) return <span className="aod-sortGhost">↕</span>;
  return <span className="aod-sortActive">{dir === "asc" ? "↑" : "↓"}</span>;
};

export default function AdminOrderDetailPage() {
  const nav = useNavigate();
  const { id } = useParams();

  const [loading, setLoading] = useState(false);
  const [order, setOrder] = useState(null);

  // input state (chỉ gọi API khi bấm lọc / enter)
  const [filters, setFilters] = useState({
    search: "",
    minPrice: "",
    maxPrice: "",
  });

  // query state (dùng để gọi API)
  const [query, setQuery] = useState({
    search: "",
    minPrice: "",
    maxPrice: "",
    sortBy: "orderdetailid",
    sortDir: "desc",
    pageIndex: 1,
    pageSize: 10,
  });

  const [paged, setPaged] = useState({
    pageIndex: 1,
    pageSize: 10,
    totalItems: 0,
    items: [],
  });

  // Modal thông tin sản phẩm
  const [modalOpen, setModalOpen] = useState(false);
  const [modalItem, setModalItem] = useState(null);
  const [modalReveal, setModalReveal] = useState(false);

  const totalPages = useMemo(() => {
    const t = Math.max(0, Number(paged.totalItems ?? 0));
    const s = Math.max(1, Number(paged.pageSize ?? 10));
    return Math.max(1, Math.ceil(t / s));
  }, [paged.totalItems, paged.pageSize]);

  const oId = order?.orderId ?? order?.OrderId;
  const email = order?.email ?? order?.Email ?? order?.userEmail ?? order?.UserEmail;
  const status = order?.status ?? order?.Status ?? "";
  const totalAmount = order?.totalAmount ?? order?.TotalAmount ?? 0;
  const finalAmount = order?.finalAmount ?? order?.FinalAmount ?? 0;
  const phone = order?.userPhone ?? order?.UserPhone;
  const createdAt = order?.createdAt ?? order?.CreatedAt;
  const buyerName = order?.userName ?? order?.UserName;

  const hasDiscount = Math.max(0, Number(totalAmount) - Number(finalAmount)) > 0.0001;

  // ✅ dùng /orders/{id} để lấy order + orderItems (có key/account)
  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      const data = await orderApi.get(id, {
        search: query.search,
        minPrice: query.minPrice,
        maxPrice: query.maxPrice,
        sortBy: query.sortBy,
        sortDir: query.sortDir,
        pageIndex: query.pageIndex,
        pageSize: query.pageSize,
      });

      const o = data?.order ?? data?.Order ?? null;
      const items = data?.orderItems ?? data?.OrderItems ?? data?.items ?? data?.Items ?? [];

      setOrder(o);
      setPaged({
        pageIndex: data?.pageIndex ?? data?.PageIndex ?? query.pageIndex,
        pageSize: data?.pageSize ?? data?.PageSize ?? query.pageSize,
        totalItems: data?.totalItems ?? data?.TotalItems ?? items.length,
        items: Array.isArray(items) ? items : [],
      });
    } catch (e) {
      console.error(e);
      setOrder(null);
      setPaged((p) => ({ ...p, items: [], totalItems: 0 }));
    } finally {
      setLoading(false);
    }
  }, [id, query]);

  useEffect(() => {
    load();
  }, [load]);

  const setQ = (patch) => setQuery((q) => ({ ...q, ...patch }));
  const setF = (patch) => setFilters((f) => ({ ...f, ...patch }));

  const onApply = () => {
    setModalOpen(false);
    setModalItem(null);
    setModalReveal(false);
    setQuery((q) => ({
      ...q,
      ...filters,
      pageIndex: 1,
    }));
  };

  const onReset = () => {
    setModalOpen(false);
    setModalItem(null);
    setModalReveal(false);
    setFilters({ search: "", minPrice: "", maxPrice: "" });
    setQuery({
      search: "",
      minPrice: "",
      maxPrice: "",
      sortBy: "orderdetailid",
      sortDir: "desc",
      pageIndex: 1,
      pageSize: 10,
    });
  };

  const openProductModal = (it) => {
    setModalItem(it);
    setModalReveal(false);
    setModalOpen(true);
  };

  const closeProductModal = () => {
    setModalOpen(false);
    setModalItem(null);
    setModalReveal(false);
  };

  const getInfoKind = (it) => {
    const pType = String(it?.productType ?? it?.ProductType ?? "").trim().toLowerCase();
    const keys = pickKeys(it);
    const accounts = pickAccounts(it);

    // ✅ ưu tiên KEY nếu có keyStrings
    if (keys.length > 0) return { kind: "key", keys, accounts: [] };

    // ✅ chỉ coi là account khi không phải key
    if (accounts.length > 0) return { kind: "account", keys: [], accounts };

    // fallback theo ProductType
    if (pType.includes("key")) return { kind: "key", keys: [], accounts: [] };
    if (pType.includes("account") || pType.includes("tài khoản") || pType.includes("shared"))
      return { kind: "account", keys: [], accounts: [] };

    return { kind: "none", keys: [], accounts: [] };
  };

  const onSort = (sortBy) => {
    setQuery((q) => {
      const same = String(q.sortBy) === String(sortBy);
      const nextDir = same ? (q.sortDir === "asc" ? "desc" : "asc") : "asc";
      return {
        ...q,
        ...filters, // nếu user đang nhập filter mà chưa bấm lọc, bấm tiêu đề cột sẽ áp dụng luôn
        sortBy,
        sortDir: nextDir,
        pageIndex: 1,
      };
    });
  };

  const buildPages = (cur, total) => {
    const c = Number(cur || 1);
    const t = Number(total || 1);
    if (t <= 7) return Array.from({ length: t }, (_, i) => i + 1);

    const pages = new Set([1, t, c, c - 1, c + 1, c - 2, c + 2]);
    const arr = [...pages].filter((x) => x >= 1 && x <= t).sort((a, b) => a - b);

    const out = [];
    for (let i = 0; i < arr.length; i++) {
      out.push(arr[i]);
      if (i < arr.length - 1 && arr[i + 1] - arr[i] > 1) out.push("...");
    }
    return out;
  };

  const renderModalBody = () => {
    if (!modalItem) return null;

    const detailId = modalItem?.orderDetailId ?? modalItem?.OrderDetailId;
    const variantTitle = modalItem?.variantTitle ?? modalItem?.VariantTitle ?? "—";

    const { kind, keys, accounts } = getInfoKind(modalItem);

    if (kind === "none") {
      return (
        <div className="aod-modalEmpty">
          Sản phẩm này không có thông tin Mã kích hoạt/Tài khoản để hiển thị.
        </div>
      );
    }

    if (kind === "key") {
      const list = keys.length > 0 ? keys : [];
      return (
        <div className="aod-modalBlock">
          <div className="aod-modalHint">Mã kích hoạt sản phẩm</div>
          <div className="aod-modalMono">
            {list.length === 0 ? (
              <div className="aod-modalEmpty">Không có mã kích hoạt.</div>
            ) : (
              list.map((k, idx) => (
                <div key={idx} className="aod-modalLine">
                  <b>Key {idx + 1}:</b> {modalReveal ? String(k) : maskSecret(k)}
                </div>
              ))
            )}
          </div>
        </div>
      );
    }

    // account
    const list = accounts.length > 0 ? accounts : [];
    return (
      <div className="aod-modalBlock">
        <div className="aod-modalHint">Tài khoản sản phẩm</div>

        {list.length === 0 ? (
          <div className="aod-modalEmpty">Không có tài khoản.</div>
        ) : (
          <div className="aod-modalAccList">
            {list.map((a, idx) => {
              const emailA = a.email ?? a.Email ?? "—";
              const userA = a.username ?? a.Username ?? "—";
              const passA = a.password ?? a.Password ?? "—";

              return (
                <div key={`${detailId}_${idx}`} className="aod-modalAccCard">
                  <div className="aod-modalAccTitle">Tài khoản #{idx + 1}</div>

                  <div className="aod-modalAccRow">
                    <div className="aod-modalAccK">Email</div>
                    <div className="aod-modalAccV aod-mono">{String(emailA)}</div>
                  </div>

                  <div className="aod-modalAccRow">
                    <div className="aod-modalAccK">Tên người dùng</div>
                    <div className="aod-modalAccV aod-mono">{String(userA)}</div>
                  </div>

                  <div className="aod-modalAccRow">
                    <div className="aod-modalAccK">Mật khẩu</div>
                    <div className="aod-modalAccV aod-mono">
                      {modalReveal ? String(passA) : maskSecret(passA)}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    );
  };

  return (
    <div className="aod-page">
      {/* ===== KHỐI TRÊN: THÔNG TIN ĐƠN HÀNG ===== */}
      <div className="aod-orderCard">
        <div className="aod-orderHead">
          <div>
            <div className="aod-orderMeta">Chi tiết đơn hàng</div>
            <div className="aod-orderTitle aod-mono">{oId || "—"}</div>
          </div>

          <div className="aod-orderActions">
            <span className={statusPillClass(status)}>{statusVi(status)}</span>

            <button className="aod-iconBtn" onClick={() => nav(-1)} title="Quay lại">
              ←
            </button>
          </div>
        </div>

        <div className="aod-orderGrid">
          <div className="aod-kv">
            <div className="aod-k">Khách hàng</div>
            <div className="aod-v">{buyerName || "—"}</div>

            <div className="aod-k">Email</div>
            <div className="aod-v aod-mono">{email || "—"}</div>

            <div className="aod-k">Điện thoại</div>
            <div className="aod-v">{phone || "—"}</div>
          </div>

          <div className="aod-kv">
            <div className="aod-k">Ngày tạo</div>
            <div className="aod-v aod-mono">{formatDateTime(createdAt)}</div>

            <div className="aod-k">Tổng tiền</div>
            <div className="aod-v">
              <div className="aod-moneyInline">
                <span className="aod-moneyMain">
                  {formatMoneyVnd(hasDiscount ? finalAmount : totalAmount)}
                </span>

                {hasDiscount ? (
                  <span className="aod-moneyOld">{formatMoneyVnd(totalAmount)}</span>
                ) : null}
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* ===== KHỐI DƯỚI: DANH SÁCH SẢN PHẨM (ORDER DETAIL) ===== */}
      <div className="aod-itemsCard">
        <div className="aod-itemsHead">
          <div className="aod-itemsTitle">Sản phẩm trong đơn</div>
          <div className="aod-itemsHint">
            {loading ? "Đang tải..." : `Tổng: ${paged.totalItems || 0} mục`}
          </div>
        </div>

        <div className="aod-filters">
          <div className="aod-field">
            <div className="aod-label">Tìm kiếm</div>
            <input
              value={filters.search}
              onChange={(e) => setF({ search: e.target.value })}
              onKeyDown={(e) => e.key === "Enter" && onApply()}
              placeholder="Tìm theo mã đơn chi tiết / tên / mã kích hoạt / tài khoản..."
            />
          </div>

          <div className="aod-field">
            <div className="aod-label">Giá từ</div>
            <input
              type="text"
              inputMode="decimal"
              value={formatForInput(filters.minPrice)}
              onChange={(e) => {
                const raw = e.target.value;
                if (/^[0-9.,]*$/.test(raw) && isValidDecimal18_2(raw)) {
                  setF({ minPrice: raw });
                } else if (raw === "") {
                  setF({ minPrice: "" });
                }
              }}
              placeholder="0"
            />
          </div>

          <div className="aod-field">
            <div className="aod-label">Giá đến</div>
            <input
              type="text"
              inputMode="decimal"
              value={formatForInput(filters.maxPrice)}
              onChange={(e) => {
                const raw = e.target.value;
                if (/^[0-9.,]*$/.test(raw) && isValidDecimal18_2(raw)) {
                  setF({ maxPrice: raw });
                } else if (raw === "") {
                  setF({ maxPrice: "" });
                }
              }}
              placeholder="0"
            />
          </div>

          <div className="aod-field aod-fieldActions">
            <div className="aod-label">&nbsp;</div>
            <div className="aod-filterBtns">
              <button className="aod-iconBtn" onClick={onApply} title="Lọc">
                <FunnelIcon />
              </button>
              <button className="aod-iconBtn" onClick={onReset} title="Đặt lại">
                <ResetIcon />
              </button>
            </div>
          </div>
        </div>

        <div className="aod-tableWrap">
          <table className="aod-table">
            <thead>
              <tr>
                <th style={{ width: 110 }}>
                  <button
                    className="aod-thBtn"
                    type="button"
                    onClick={() => onSort("orderdetailid")}
                  >
                    Mã đơn chi tiết{" "}
                    <SortIcon active={query.sortBy === "orderdetailid"} dir={query.sortDir} />
                  </button>
                </th>

                <th>
                  <button className="aod-thBtn" type="button" onClick={() => onSort("varianttitle")}>
                    Sản phẩm{" "}
                    <SortIcon active={query.sortBy === "varianttitle"} dir={query.sortDir} />
                  </button>
                </th>

                <th style={{ width: 90 }}>
                  <button className="aod-thBtn" type="button" onClick={() => onSort("quantity")}>
                    SL <SortIcon active={query.sortBy === "quantity"} dir={query.sortDir} />
                  </button>
                </th>

                <th style={{ width: 150 }}>
                  <button className="aod-thBtn" type="button" onClick={() => onSort("unitprice")}>
                    Đơn giá <SortIcon active={query.sortBy === "unitprice"} dir={query.sortDir} />
                  </button>
                </th>

                <th style={{ width: 160 }}>Thành tiền</th>
                <th style={{ width: 160, textAlign: "center" }}>Thông tin sản phẩm</th>
              </tr>
            </thead>

            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={6} className="aod-empty">
                    Đang tải...
                  </td>
                </tr>
              ) : (paged.items || []).length === 0 ? (
                <tr>
                  <td colSpan={6} className="aod-empty">
                    Không có dữ liệu
                  </td>
                </tr>
              ) : (
                paged.items.map((it) => {
                  const detailId = it.orderDetailId ?? it.OrderDetailId;

                  const variantTitle = it.variantTitle ?? it.VariantTitle ?? "—";
                  const qty = it.quantity ?? it.Quantity ?? 0;
                  const unitPrice = it.unitPrice ?? it.UnitPrice ?? 0;
                  const subTotal = it.subTotal ?? it.SubTotal ?? qty * unitPrice;

                  const info = getInfoKind(it);
                  const canShowInfo =
                    info.kind === "key"
                      ? info.keys.length > 0
                      : info.kind === "account"
                      ? info.accounts.length > 0
                      : false;

                  return (
                    <tr key={String(detailId)}>
                      <td className="aod-mono">{detailId}</td>

                      <td>
                        <div className="aod-itemMain">
                          <div className="aod-itemTitle">{variantTitle}</div>
                        </div>
                      </td>

                      <td>{qty}</td>
                      <td>{formatMoneyVnd(unitPrice)}</td>
                      <td>{formatMoneyVnd(subTotal)}</td>

                      <td style={{ textAlign: "center" }}>
                        {canShowInfo ? (
                          <button
                            className="aod-eyeBtn"
                            type="button"
                            onClick={() => openProductModal(it)}
                            title="Xem thông tin"
                            aria-label="Xem thông tin"
                          >
                            <EyeIcon open={false} />
                          </button>
                        ) : (
                          <span className="aod-muted">—</span>
                        )}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        <div className="aod-pager">
          <div className="aod-pagerInfo">
            Trang <b>{paged.pageIndex}</b>/<b>{totalPages}</b>
          </div>

          <div className="aod-pagerBtns">
            <button
              className="aod-pageNav"
              disabled={paged.pageIndex <= 1}
              onClick={() => setQ({ pageIndex: Math.max(1, paged.pageIndex - 1) })}
              title="Trang trước"
            >
              ←
            </button>

            {buildPages(paged.pageIndex, totalPages).map((p, idx) =>
             p === "..." ? (
                <span key={`dots_${idx}`} className="aod-dots">
                  …
                </span>
              ) : (
                <button
                  key={p}
                  className={`aod-pageBtn ${Number(p) === Number(paged.pageIndex) ? "active" : ""}`}
                  onClick={() => setQ({ pageIndex: Number(p) })}
                >
                  {p}
                </button>
              )
            )}

            <button
              className="aod-pageNav"
              disabled={paged.pageIndex >= totalPages}
              onClick={() => setQ({ pageIndex: Math.min(totalPages, paged.pageIndex + 1) })}
              title="Trang sau"
            >
              →
            </button>

            <select
              className="aod-pageSize"
              value={query.pageSize}
              onChange={(e) => setQ({ pageIndex: 1, pageSize: Number(e.target.value || 10) })}
              title="Kích thước trang"
            >
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
            </select>
          </div>
        </div>
      </div>

      {/* ===== MODAL: THÔNG TIN SẢN PHẨM ===== */}
      {modalOpen ? (
        <div className="aod-modalBackdrop" role="dialog" aria-modal="true" onMouseDown={closeProductModal}>
          <div className="aod-modal" onMouseDown={(e) => e.stopPropagation()}>
            <div className="aod-modalHead">
              <div className="aod-modalTitle">
                {modalItem?.variantTitle ?? modalItem?.VariantTitle ?? "Thông tin sản phẩm"}
              </div>

              <div className="aod-modalActions">
                <button
                  className="aod-iconBtn"
                  onClick={() => setModalReveal((v) => !v)}
                  title={modalReveal ? "Ẩn thông tin nhạy cảm" : "Hiện thông tin nhạy cảm"}
                >
                  <EyeIcon open={modalReveal} />
                </button>
                <button className="aod-iconBtn" onClick={closeProductModal} title="Đóng">
                  <CloseIcon />
                </button>
              </div>
            </div>

            <div className="aod-modalBody">{renderModalBody()}</div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
