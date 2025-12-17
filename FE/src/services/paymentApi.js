// services/paymentApi.js
import axiosClient from "../api/axiosClient";

const END = { PAYMENTS: "payments" };

const unwrap = (res) => res?.data ?? res;

const normalizePaged = (res) => {
  const data = unwrap(res) || {};
  return {
    pageIndex: data.pageIndex ?? data.PageIndex ?? 1,
    pageSize: data.pageSize ?? data.PageSize ?? 20,
    totalItems: data.totalItems ?? data.TotalItems ?? 0,
    items: Array.isArray(data.items ?? data.Items) ? (data.items ?? data.Items) : [],
  };
};

const listPaged = (params = {}) => {
  const p = { ...(params || {}) };

  // compat: FE cũ dùng q -> map sang search
  if (!p.search && p.q) {
    p.search = p.q;
    delete p.q;
  }

  // compat: FE cũ dùng status -> map sang paymentStatus
  if (!p.paymentStatus && p.status) {
    p.paymentStatus = p.status;
    delete p.status;
  }

  return axiosClient.get(END.PAYMENTS, { params: p }).then(normalizePaged);
};

export const paymentApi = {
  /**
   * ✅ Admin list (paged)
   * params (BE): search, createdFrom, createdTo, paymentStatus, sortBy, sortDir, pageIndex, pageSize
   * response: { pageIndex, pageSize, totalItems, items }
   */
  listPaged,

  /**
   * (compat) trả items[] thôi
   */
  list: (params = {}) => listPaged(params).then((x) => x.items),

  /**
   * ✅ Admin detail
   * query (BE): { includeCheckoutUrl, includeAttempts }
   */
  get: (id, params) => {
    const cfg = params ? { params } : undefined;
    return axiosClient.get(`${END.PAYMENTS}/${id}`, cfg);
  },
};
