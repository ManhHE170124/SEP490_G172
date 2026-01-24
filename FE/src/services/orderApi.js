// File: src/services/orderApi.js
import axiosClient from "../api/axiosClient";

const END = { ORDERS: "orders" };

const unwrap = (res) => res?.data ?? res;

const pickPagedItems = (data) => {
  const raw = data?.items ?? data?.Items ?? data?.orderItems ?? data?.OrderItems ?? [];
  return Array.isArray(raw) ? raw : [];
};

const normalizePaged = (res) => {
  const data = unwrap(res) || {};
  const items = pickPagedItems(data);

  return {
    pageIndex: data.pageIndex ?? data.PageIndex ?? 1,
    pageSize: data.pageSize ?? data.PageSize ?? 20,
    totalItems: data.totalItems ?? data.TotalItems ?? data.total ?? data.Total ?? items.length,
    items,
  };
};

const normalizeListSortBy = (v) => {
  const x = String(v || "").trim().toLowerCase();
  if (x === "orderid" || x === "order_id") return "orderid";
  if (x === "amount" || x === "finalamount" || x === "total") return "amount";
  if (x === "status") return "status";
  if (x === "createdat" || x === "created_at" || x === "created") return "createdat";
  return "createdat";
};

const normalizeDetailSortBy = (v) => {
  const x = String(v || "").trim().toLowerCase();
  if (x === "orderdetailid") return "orderdetailid";
  if (x === "varianttitle") return "varianttitle";
  if (x === "quantity") return "quantity";
  if (x === "unitprice") return "unitprice";
  return "orderdetailid";
};

const addIf = (obj, key, val) => {
  if (val === undefined || val === null) return;
  if (typeof val === "string" && val.trim() === "") return;
  obj[key] = val;
};

const mapListParams = (params = {}) => {
  const p = {};
  addIf(p, "search", params.search);

  addIf(p, "createdFrom", params.createdFrom);
  addIf(p, "createdTo", params.createdTo);
  addIf(p, "orderStatus", params.orderStatus);

  // ✅ GetOrders() dùng minTotal/maxTotal
  const minTotal = params.minTotal ?? params.amountFrom;
  const maxTotal = params.maxTotal ?? params.amountTo;
  addIf(p, "minTotal", minTotal);
  addIf(p, "maxTotal", maxTotal);

  addIf(p, "sortBy", normalizeListSortBy(params.sortBy));
  addIf(p, "sortDir", params.sortDir);

  addIf(p, "pageIndex", params.pageIndex);
  addIf(p, "pageSize", params.pageSize);

  return p;
};

const mapDetailParams = (params = {}) => {
  const p = {};
  addIf(p, "search", params.search);

  // ✅ GetOrderDetailItems() dùng minPrice/maxPrice
  const minPrice = params.minPrice ?? params.amountFrom;
  const maxPrice = params.maxPrice ?? params.amountTo;
  addIf(p, "minPrice", minPrice);
  addIf(p, "maxPrice", maxPrice);

  addIf(p, "sortBy", normalizeDetailSortBy(params.sortBy));
  addIf(p, "sortDir", params.sortDir);

  addIf(p, "pageIndex", params.pageIndex);
  addIf(p, "pageSize", params.pageSize);

  return p;
};

const listPaged = (params = {}) =>
  axiosClient.get(END.ORDERS, { params: mapListParams(params) }).then(normalizePaged);

export const orderApi = {
  // ✅ Admin list
  listPaged,
  list: (params = {}) => listPaged(params).then((x) => x.items),

  history: (userId, params = {}) => {
    if (!userId) return Promise.reject(new Error("UserId is required"));
    return axiosClient
      .get(`${END.ORDERS}/history`, { params: { userId, ...params } })
      .then(unwrap);
  },

  /**
   * ✅ Admin order detail
   * IMPORTANT: Trang AdminOrderDetailPage sẽ dùng API này (GET /orders/{id})
   * vì BE trả: { order, orderItems, pageIndex, pageSize, totalItems }
   */
  get: (id, params) => {
    const cfg = params ? { params: mapDetailParams(params) } : undefined;
    return axiosClient.get(`${END.ORDERS}/${id}`, cfg).then(unwrap);
  },

  // ✅ Paged order details (compat chỗ khác nếu còn dùng GET /orders/{id}/details)
  getDetails: (id, params) => {
    const cfg = params ? { params: mapDetailParams(params) } : undefined;
    return axiosClient.get(`${END.ORDERS}/${id}/details`, cfg).then(normalizePaged);
  },

  // ✅ tránh 404 (compat)
  getDetailCredentials: (orderId, orderDetailId, params) => {
    const cfg = params ? { params } : undefined;
    return axiosClient
      .get(`${END.ORDERS}/${orderId}/details/${orderDetailId}/credentials`, cfg)
      .then(unwrap);
  },
   manualUpdateStatus: (orderId, payload) => {
    if (!orderId) return Promise.reject(new Error("OrderId is required"));
    return axiosClient.patch(`${END.ORDERS}/${orderId}/status`, payload).then(unwrap);
  },
};
