// File: src/services/paymentApi.js
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

export const paymentApi = {
  // params: search, createdFrom, createdTo, paymentStatus, transactionType, amountFrom, amountTo, sortBy, sortDir, pageIndex, pageSize
  listPaged: (params = {}) => axiosClient.get(END.PAYMENTS, { params }).then(normalizePaged),

  // detail: full payment fields (không dùng attempts)
  get: (paymentId, params) => {
    const cfg = params ? { params } : undefined;
    return axiosClient.get(`${END.PAYMENTS}/${paymentId}`, cfg);
  },
};
