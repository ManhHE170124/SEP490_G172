// File: src/api/supportDashboardAdminApi.js
import axiosClient from "./axiosClient";

export const supportDashboardAdminApi = {
  // 1. Overview
  getOverview: ({ days, yearMonth } = {}) => {
    const params = {};
    if (days != null) params.days = days;
    if (yearMonth) params.yearMonth = yearMonth;
    return axiosClient.get("/support-dashboard-admin/overview", { params });
  },

  // 2. Ticket & SLA
  getTicketDailyKpi: ({ days } = {}) =>
    axiosClient.get("/support-dashboard-admin/tickets/daily", {
      params: days != null ? { days } : undefined,
    }),

  getTicketSeverityPriorityWeekly: ({ weeks } = {}) =>
    axiosClient.get(
      "/support-dashboard-admin/tickets/weekly-severity-priority",
      {
        params: weeks != null ? { weeks } : undefined,
      }
    ),

  getTicketPriorityDistribution: ({ days } = {}) =>
    axiosClient.get("/support-dashboard-admin/tickets/priority-distribution", {
      params: days != null ? { days } : undefined,
    }),

  // 3. Live Chat
  getChatDailyKpi: ({ days } = {}) =>
    axiosClient.get("/support-dashboard-admin/chat/daily", {
      params: days != null ? { days } : undefined,
    }),

  getChatPriorityWeekly: ({ weeks } = {}) =>
    axiosClient.get("/support-dashboard-admin/chat/weekly-priority", {
      params: weeks != null ? { weeks } : undefined,
    }),

  // 4. Staff performance
  getStaffPerformance: ({ days } = {}) =>
    axiosClient.get("/support-dashboard-admin/staff/performance", {
      params: days != null ? { days } : undefined,
    }),

  // 5. Support plan & loyalty
  getActiveSupportPlanDistribution: () =>
    axiosClient.get("/support-dashboard-admin/plans/active-distribution"),

  getSupportPlanMonthlyStats: ({ months } = {}) =>
    axiosClient.get("/support-dashboard-admin/plans/monthly-stats", {
      params: months != null ? { months } : undefined,
    }),

  getPriorityDistribution: () =>
    axiosClient.get(
      "/support-dashboard-admin/segments/priority-distribution"
    ),

  getPrioritySupportVolume: ({ weeks } = {}) =>
    axiosClient.get(
      "/support-dashboard-admin/segments/priority-support-volume",
      {
        params: weeks != null ? { weeks } : undefined,
      }
    ),
};
