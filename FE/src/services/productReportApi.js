import axiosClient from "../api/axiosClient";

const ProductReportApi = {
  list: async (params) => {
    const url = "/productreport";
    return await axiosClient.get(url, { params });
  },

  get: async (id) => {
    const url = `/productreport/${id}`;
    return await axiosClient.get(url);
  },

  create: async (data) => {
    const url = "/productreport";
    return await axiosClient.post(url, data);
  },

  updateStatus: async (id, data) => {
    const url = `/productreport/${id}/status`;
    return await axiosClient.patch(url, data);
  },

  getMyReports: async (params) => {
    const url = "/productreport/my-reports";
    return await axiosClient.get(url, { params });
  },

  // Get key error reports with pagination
  getKeyErrors: async (params) => {
    const url = "/productreport/key-errors";
    return await axiosClient.get(url, { params });
  },

  // Get account error reports with pagination
  getAccountErrors: async (params) => {
    const url = "/productreport/account-errors";
    return await axiosClient.get(url, { params });
  },

  // Count all key errors
  countKeyErrors: async () => {
    const url = "/productreport/key-errors/count";
    return await axiosClient.get(url);
  },

  // Count all account errors
  countAccountErrors: async () => {
    const url = "/productreport/account-errors/count";
    return await axiosClient.get(url);
  },
};

export { ProductReportApi };
