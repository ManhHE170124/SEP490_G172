// File: src/services/paymentsDashboardAdminApi.js
import axiosClient from "../api/axiosClient";

const END = { ROOT: "payments-dashboard-admin" };

const unwrap = (res) => res?.data ?? res;

// normalize keys (camelCase/PascalCase safe)
const pick = (obj, ...keys) => {
  for (const k of keys) {
    if (obj && obj[k] !== undefined && obj[k] !== null) return obj[k];
  }
  return undefined;
};

export const paymentsDashboardAdminApi = {
  // GET /api/payments-dashboard-admin/summary
  // params: fromUtc, toUtc, provider, targetType, pendingOverdueMinutes
  summary: (params = {}) =>
    axiosClient.get(`${END.ROOT}/summary`, { params }).then(unwrap),

  // GET /api/payments-dashboard-admin/trends/daily
  // params: days, provider, targetType, timezoneOffsetMinutes
  dailyTrends: (params = {}) =>
    axiosClient.get(`${END.ROOT}/trends/daily`, { params }).then(unwrap),

  // GET /api/payments-dashboard-admin/time-to-pay
  // params: fromUtc, toUtc, provider, targetType
  timeToPay: (params = {}) =>
    axiosClient.get(`${END.ROOT}/time-to-pay`, { params }).then(unwrap),

  // GET /api/payments-dashboard-admin/attempts
  // params: days, provider
  attempts: (params = {}) =>
    axiosClient.get(`${END.ROOT}/attempts`, { params }).then(unwrap),

  // GET /api/payments-dashboard-admin/heatmap
  // params: days, provider, metric, timezoneOffsetMinutes
  heatmap: (params = {}) =>
    axiosClient.get(`${END.ROOT}/heatmap`, { params }).then(unwrap),

  // GET /api/payments-dashboard-admin/failure-reasons
  // params: days, provider, targetType, top
  failureReasons: (params = {}) =>
    axiosClient.get(`${END.ROOT}/failure-reasons`, { params }).then(unwrap),

  // small helpers for UI formatting if you want
  _getRangeFromSummary: (summary) => {
    const from = pick(summary, "rangeFromUtc", "RangeFromUtc");
    const to = pick(summary, "rangeToUtc", "RangeToUtc");
    return { from, to };
  },
};
