// File: src/api/userDashboardAdminApi.js
import axiosClient from "./axiosClient";

export const userDashboardAdminApi = {
  /**
   * BE: GET /api/user-dashboard-admin/overview-growth?month=yyyy-MM&groupBy=day|week
   */
  getGrowthOverview: ({ month, groupBy } = {}) => {
    const params = {};
    if (month) params.month = month; // yyyy-MM
    if (groupBy) params.groupBy = groupBy; // day|week
    return axiosClient.get("/user-dashboard-admin/overview-growth", { params });
  },
};
