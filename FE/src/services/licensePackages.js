import axiosClient from "../api/axiosClient";

const LICENSE_PACKAGE_ENDPOINTS = {
  ROOT: "/licensepackage",
  IMPORT_TO_STOCK: "/licensepackage/import-to-stock",
  UPLOAD_CSV: "/licensepackage/upload-csv",
  DOWNLOAD_TEMPLATE: "/licensepackage/download-template",
};

export const LicensePackageApi = {
  // ===== CRUD Operations =====
  list: (params = {}) =>
    axiosClient.get(LICENSE_PACKAGE_ENDPOINTS.ROOT, { params }),

  get: (id) =>
    axiosClient.get(`${LICENSE_PACKAGE_ENDPOINTS.ROOT}/${id}`),

  create: (payload) =>
    axiosClient.post(LICENSE_PACKAGE_ENDPOINTS.ROOT, payload),

  update: (id, payload) =>
    axiosClient.put(`${LICENSE_PACKAGE_ENDPOINTS.ROOT}/${id}`, payload),

  delete: (id) =>
    axiosClient.delete(`${LICENSE_PACKAGE_ENDPOINTS.ROOT}/${id}`),

  // ===== Import Operations =====
  importToStock: (payload) =>
    axiosClient.post(LICENSE_PACKAGE_ENDPOINTS.IMPORT_TO_STOCK, payload),

  uploadCsv: (formData) =>
    axiosClient.post(LICENSE_PACKAGE_ENDPOINTS.UPLOAD_CSV, formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    }),

  getKeysByPackage: (packageId, supplierId) =>
    axiosClient.get(`${LICENSE_PACKAGE_ENDPOINTS.ROOT}/${packageId}/keys`, {
      params: { supplierId }
    }),

  downloadCsvTemplate: () =>
    axiosClient.get(LICENSE_PACKAGE_ENDPOINTS.DOWNLOAD_TEMPLATE, {
      responseType: 'blob',
    }),
};

export default LicensePackageApi;
