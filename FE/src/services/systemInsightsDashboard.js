// File: src/services/systemInsightsDashboard.js
import axiosClient from "../api/axiosClient";

// ✅ FIX: bỏ "/" đầu để không làm mất "/api" khi axiosClient baseURL đã là "/api"
const ROOT = "system-insights-dashboard"; // maps to /api/system-insights-dashboard

export async function getSystemInsightsOverview(params) {
  const res = await axiosClient.get(`${ROOT}/overview`, { params });
  return res?.data ?? res;
}
