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
};

export { ProductReportApi };
