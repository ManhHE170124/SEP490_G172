// File: src/services/ordersDashboardAdminApi.js
import axiosClient from "../api/axiosClient";

const OrdersDashboardAdminApi = {
  getDashboard: (params) => {
    // params: { fromUtc, toUtc, bucket }
    return axiosClient.get("/admin/orders-dashboard", { params });
  },
};

export default OrdersDashboardAdminApi;
