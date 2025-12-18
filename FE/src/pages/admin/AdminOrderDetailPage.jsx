// File: src/pages/admin/AdminOrderDetailPage.jsx
import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { orderApi } from "../../services/orderApi";
import "./OrderPaymentPage.css";

const formatMoneyVnd = (n) => {
  const x = Number(n ?? 0);
  try {
    return new Intl.NumberFormat("vi-VN").format(x) + " đ";
  } catch {
    return `${x} đ`;
  }
};

const formatDateTime = (dt) => {
  if (!dt) return "—";
  const d = new Date(dt);
  if (Number.isNaN(d.getTime())) return String(dt);
  const pad = (v) => String(v).padStart(2, "0");
  return `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())} ${pad(
    d.getDate()
  )}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;
};

const statusPillClass = (s) => {
  const x = String(s || "").trim().toLowerCase();
  if (x.includes("paid") || x === "success" || x === "completed") return "status-pill payment-paid";
  if (x.includes("pending")) return "status-pill payment-pending";
  if (x.includes("cancel")) return "status-pill payment-cancelled";
  if (x.includes("fail")) return "status-pill payment-failed";
  if (x.includes("refund")) return "status-pill payment-refunded";
  return "status-pill payment-unknown";
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

export default function AdminOrderDetailPage() {
  const nav = useNavigate();
  const { id } = useParams(); // /admin/orders/:id

  const [loading, setLoading] = useState(false);
  const [order, setOrder] = useState(null);

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

  const totalPages = useMemo(() => {
    const t = Math.max(0, Number(paged.totalItems ?? 0));
    const s = Math.max(1, Number(paged.pageSize ?? 10));
    return Math.max(1, Math.ceil(t / s));
  }, [paged.totalItems, paged.pageSize]);

  // ✅ dùng /orders/{id} để lấy luôn order + orderItems (có key/account)
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
      const items =
        data?.orderItems ?? data?.OrderItems ?? data?.items ?? data?.Items ?? [];

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

  const setQ = (patch) => {
    setQuery((q) => ({ ...q, ...patch }));
  };

  const onApply = () => {
    // nếu đang ở page 1 thì vẫn cần load lại => gọi thẳng load()
    setQ({ pageIndex: 1 });
    setTimeout(() => load(), 0);
  };

  const oId = order?.orderId ?? order?.OrderId;
  const orderNumber = order?.orderNumber ?? order?.OrderNumber;
  const email = order?.email ?? order?.Email ?? order?.userEmail ?? order?.UserEmail;
  const status = order?.status ?? order?.Status ?? "";
  const totalAmount = order?.totalAmount ?? order?.TotalAmount ?? 0;
  const finalAmount = order?.finalAmount ?? order?.FinalAmount ?? 0;
  const phone = order?.userPhone ?? order?.UserPhone;
  const createdAt = order?.createdAt ?? order?.CreatedAt;

  return (
    <div className="op-page">
      <div className="order-payment-header">
        <div style={{ display: "flex", gap: 10, alignItems: "center" }}>
          <button className="op-icon-btn" onClick={() => nav(-1)}>
            ← Quay lại
          </button>
          <h2 style={{ margin: 0 }}>Chi tiết đơn hàng</h2>
        </div>
      </div>

      <div style={{ marginTop: 12 }} className="card">
        <div className="card-body">
          <div className="op-2col">
            <div>
              <div className="op-subtext">OrderId</div>
              <div className="mono" style={{ fontWeight: 700 }}>
                {oId || "—"}
              </div>

              <div className="op-subtext" style={{ marginTop: 10 }}>
                Email
              </div>
              <div style={{ fontWeight: 600 }}>{email || "—"}</div>

              <div className="op-subtext" style={{ marginTop: 10 }}>
                Tổng tiền
              </div>
              <div style={{ fontWeight: 700 }}>{formatMoneyVnd(totalAmount)}</div>

              <div className="op-subtext" style={{ marginTop: 10 }}>
                Ngày tạo
              </div>
              <div className="mono">{formatDateTime(createdAt)}</div>
            </div>

            <div>
              <div className="op-subtext">OrderNumber</div>
              <div style={{ fontWeight: 800 }}>{orderNumber || "—"}</div>

              <div className="op-subtext" style={{ marginTop: 10 }}>
                Trạng thái
              </div>
              <span className={statusPillClass(status)}>{status || "—"}</span>

              <div className="op-subtext" style={{ marginTop: 10 }}>
                Thành tiền
              </div>
              <div style={{ fontWeight: 700 }}>{formatMoneyVnd(finalAmount)}</div>

              <div className="op-subtext" style={{ marginTop: 10 }}>
                SĐT
              </div>
              <div style={{ fontWeight: 600 }}>{phone || "—"}</div>
            </div>
          </div>
        </div>
      </div>

      {/* Filters */}
      <div className="op-toolbar">
        <div className="op-filters">
          <div className="op-group">
            <span>Tìm (detailId / tên / code / key / email account)</span>
            <input
              value={query.search}
              onChange={(e) => setQ({ search: e.target.value })}
              placeholder="Nhập từ khoá..."
            />
          </div>

          <div className="op-group">
            <span>Giá từ (bỏ trống = tất cả)</span>
            <input value={query.minPrice} onChange={(e) => setQ({ minPrice: e.target.value })} />
          </div>

          <div className="op-group">
            <span>Giá đến (bỏ trống = tất cả)</span>
            <input value={query.maxPrice} onChange={(e) => setQ({ maxPrice: e.target.value })} />
          </div>

          <div className="op-group">
            <span>Sort</span>
            <select value={query.sortBy} onChange={(e) => setQ({ sortBy: e.target.value })}>
              <option value="orderdetailid">OrderDetailId</option>
              <option value="varianttitle">VariantTitle</option>
              <option value="quantity">Quantity</option>
              <option value="unitprice">UnitPrice</option>
            </select>
          </div>

          <div className="op-group">
            <span>Dir</span>
            <select value={query.sortDir} onChange={(e) => setQ({ sortDir: e.target.value })}>
              <option value="desc">Desc</option>
              <option value="asc">Asc</option>
            </select>
          </div>

          <div className="op-group">
            <span>Page size</span>
            <select
              value={query.pageSize}
              onChange={(e) => setQ({ pageIndex: 1, pageSize: Number(e.target.value || 10) })}
            >
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
            </select>
          </div>

          <div className="op-group">
            <span>&nbsp;</span>
            <button className="op-icon-btn primary" onClick={onApply}>
              Lọc
            </button>
          </div>
        </div>
      </div>

      {/* Table */}
      <div style={{ marginTop: 12 }}>
        <table className="op-table table">
          <thead>
            <tr>
              <th>DetailId</th>
              <th>Sản phẩm / Biến thể</th>
              <th>SL</th>
              <th>Giá bán</th>
              <th>Tạm tính</th>
              <th>Key</th>
              <th>Account</th>
            </tr>
          </thead>

          <tbody>
            {loading ? (
              <tr>
                <td colSpan={7}>Đang tải...</td>
              </tr>
            ) : (paged.items || []).length === 0 ? (
              <tr>
                <td colSpan={7}>Không có dữ liệu</td>
              </tr>
            ) : (
              paged.items.map((it) => {
                const detailId = it.orderDetailId ?? it.OrderDetailId;
                const productName = it.productName ?? it.ProductName ?? "—";
                const variantTitle = it.variantTitle ?? it.VariantTitle ?? "—";
                const qty = it.quantity ?? it.Quantity ?? 0;
                const unitPrice = it.unitPrice ?? it.UnitPrice ?? 0;
                const subTotal = it.subTotal ?? it.SubTotal ?? qty * unitPrice;

                const keys = pickKeys(it);
                const accounts = pickAccounts(it);

                return (
                  <tr key={String(detailId)}>
                    <td className="mono">{detailId}</td>
                    <td>
                      <div className="op-cell-main">
                        <div className="op-cell-title">{productName}</div>
                        <div className="op-cell-sub">{variantTitle}</div>
                      </div>
                    </td>
                    <td>{qty}</td>
                    <td>{formatMoneyVnd(unitPrice)}</td>
                    <td>{formatMoneyVnd(subTotal)}</td>

                    {/* Key */}
                    <td>
                      {keys.length === 0 ? (
                        "—"
                      ) : (
                        <div className="mono" style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                          {keys.map((k, idx) => (
                            <span key={idx}>{k}</span>
                          ))}
                        </div>
                      )}
                    </td>

                    {/* Account */}
                    <td>
                      {accounts.length === 0 ? (
                        "—"
                      ) : (
                        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                          {accounts.map((a, idx) => {
                            const emailA = a.email ?? a.Email ?? "—";
                            const userA = a.username ?? a.Username ?? "—";
                            const passA = a.password ?? a.Password ?? "—";
                            return (
                              <div key={idx} className="mono">
                                <div>Email: {emailA}</div>
                                <div>User: {userA}</div>
                                <div>Pass: {passA}</div>
                              </div>
                            );
                          })}
                        </div>
                      )}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>

        <div style={{ display: "flex", justifyContent: "space-between", marginTop: 12 }}>
          <div className="op-cell-sub">
            Tổng: {paged.totalItems} | Trang {paged.pageIndex}/{totalPages}
          </div>

          <div style={{ display: "flex", gap: 8 }}>
            <button
              className="op-icon-btn"
              disabled={paged.pageIndex <= 1}
              onClick={() => {
                const next = Math.max(1, paged.pageIndex - 1);
                setPaged((p) => ({ ...p, pageIndex: next }));
                setQ({ pageIndex: next });
              }}
            >
              ← Trước
            </button>
            <button
              className="op-icon-btn"
              disabled={paged.pageIndex >= totalPages}
              onClick={() => {
                const next = Math.min(totalPages, paged.pageIndex + 1);
                setPaged((p) => ({ ...p, pageIndex: next }));
                setQ({ pageIndex: next });
              }}
            >
              Sau →
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
