import axiosClient from "../api/axiosClient";

const PRODUCT_ENDPOINTS = {
  ROOT: "products",
  LIST: "products/list",
};

export const ProductApi = {
  // ===== CRUD & list =====
  list: (params = {}) => axiosClient.get(PRODUCT_ENDPOINTS.LIST, { params }),
  get: (id) => axiosClient.get(`${PRODUCT_ENDPOINTS.ROOT}/${id}`),
  create: (payload) => axiosClient.post(PRODUCT_ENDPOINTS.ROOT, payload),
  update: (id, payload) => axiosClient.put(`${PRODUCT_ENDPOINTS.ROOT}/${id}`, payload),
  remove: (id) => axiosClient.delete(`${PRODUCT_ENDPOINTS.ROOT}/${id}`),

  // ===== Status / toggle =====
  // Giống RBAC: PUT với body JSON để tránh 415
  changeStatus: (id, status) =>
    axiosClient.put(`${PRODUCT_ENDPOINTS.ROOT}/${id}/status`, { status }),
  toggle: (id) => axiosClient.patch(`${PRODUCT_ENDPOINTS.ROOT}/${id}/toggle`),

  // ===== Bulk price & CSV =====
  bulkPrice: (payload) =>
    axiosClient.post(`${PRODUCT_ENDPOINTS.ROOT}/bulk-price`, payload),

  exportCsv: () =>
    axiosClient.get(`${PRODUCT_ENDPOINTS.ROOT}/export-csv`, { responseType: "blob" }),

  importPriceCsv: (file) => {
    const form = new FormData();
    form.append("file", file);
    return axiosClient.post(`${PRODUCT_ENDPOINTS.ROOT}/import-price-csv`, form);
  },

  // ===== Images =====
  uploadImage: async (id, file) => {
    const form = new FormData();
    form.append("file", file);
    const d = await axiosClient.post(`${PRODUCT_ENDPOINTS.ROOT}/${id}/images/upload`, form);
    // axiosClient thường trả data trực tiếp
    return {
      imageId: d.imageId ?? d.ImageId,
      url: d.url ?? d.Url,
      sortOrder: d.sortOrder ?? d.SortOrder,
      isPrimary: d.isPrimary ?? d.IsPrimary,
    };
  },

  setThumbnail: (id, url) =>
    axiosClient.post(`${PRODUCT_ENDPOINTS.ROOT}/${id}/thumbnail`, url, {
      headers: { "Content-Type": "application/json" },
    }),

  deleteImage: (id, imageId) =>
    axiosClient.delete(`${PRODUCT_ENDPOINTS.ROOT}/${id}/images/${imageId}`),

  reorderImages: (id, imageIds) =>
    axiosClient.post(`${PRODUCT_ENDPOINTS.ROOT}/${id}/images/reorder`, { imageIds }),

  setPrimaryImage: (id, imageId) =>
    axiosClient.post(`${PRODUCT_ENDPOINTS.ROOT}/${id}/images/${imageId}/primary`),

  // ===== Create/Update multipart =====
  createWithImages: (payload, files = [], primaryIndex = 0) => {
    const form = new FormData();
    if (payload.productCode !== undefined) form.append("ProductCode", payload.productCode);
    if (payload.productName !== undefined) form.append("ProductName", payload.productName);
    if (payload.supplierId !== undefined) form.append("SupplierId", String(payload.supplierId));
    if (payload.productType !== undefined) form.append("ProductType", payload.productType);
    if (payload.costPrice !== undefined && payload.costPrice !== null) form.append("CostPrice", String(payload.costPrice));
    if (payload.salePrice !== undefined) form.append("SalePrice", String(payload.salePrice));
    if (payload.stockQty !== undefined) form.append("StockQty", String(payload.stockQty));
    if (payload.warrantyDays !== undefined) form.append("WarrantyDays", String(payload.warrantyDays));
    if (payload.expiryDate !== undefined && payload.expiryDate !== null) form.append("ExpiryDate", payload.expiryDate);
    if (payload.autoDelivery !== undefined) form.append("AutoDelivery", String(payload.autoDelivery));
    if (payload.status !== undefined && payload.status !== null) form.append("Status", payload.status);
    if (payload.description !== undefined && payload.description !== null) form.append("Description", payload.description);
    if (payload.thumbnailUrl !== undefined && payload.thumbnailUrl !== null) form.append("ThumbnailUrl", payload.thumbnailUrl);
    if (payload.categoryIds?.length) payload.categoryIds.forEach((id) => form.append("CategoryIds", String(id)));
    if (payload.badgeCodes?.length) payload.badgeCodes.forEach((c) => form.append("BadgeCodes", c));
    if (files?.length) {
      for (const f of files) form.append("Images", f);
      form.append("PrimaryIndex", String(primaryIndex ?? 0));
    }
    return axiosClient.post(`${PRODUCT_ENDPOINTS.ROOT}/with-images`, form);
  },

  updateWithImages: (id, payload, newFiles = [], primaryIndex = null, deleteImageIds = []) => {
    const form = new FormData();
    if (payload.productName !== undefined) form.append("ProductName", payload.productName);
    if (payload.supplierId !== undefined) form.append("SupplierId", String(payload.supplierId));
    if (payload.productType !== undefined) form.append("ProductType", payload.productType);
    if (payload.costPrice !== undefined && payload.costPrice !== null) form.append("CostPrice", String(payload.costPrice));
    if (payload.salePrice !== undefined) form.append("SalePrice", String(payload.salePrice));
    if (payload.stockQty !== undefined) form.append("StockQty", String(payload.stockQty));
    if (payload.warrantyDays !== undefined) form.append("WarrantyDays", String(payload.warrantyDays));
    if (payload.expiryDate !== undefined && payload.expiryDate !== null) form.append("ExpiryDate", payload.expiryDate);
    if (payload.autoDelivery !== undefined) form.append("AutoDelivery", String(payload.autoDelivery));
    if (payload.status !== undefined && payload.status !== null) form.append("Status", payload.status);
    if (payload.description !== undefined && payload.description !== null) form.append("Description", payload.description);
    if (payload.thumbnailUrl !== undefined && payload.thumbnailUrl !== null) form.append("ThumbnailUrl", payload.thumbnailUrl);
    if (payload.categoryIds?.length) payload.categoryIds.forEach((cid) => form.append("CategoryIds", String(cid)));
    if (payload.badgeCodes?.length) payload.badgeCodes.forEach((b) => form.append("BadgeCodes", b));
    if (deleteImageIds?.length) deleteImageIds.forEach((did) => form.append("DeleteImageIds", String(did)));
    if (newFiles?.length) {
      for (const f of newFiles) form.append("NewImages", f);
      if (primaryIndex !== null && primaryIndex !== undefined) {
        form.append("PrimaryIndex", String(primaryIndex));
      }
    }
    return axiosClient.put(`${PRODUCT_ENDPOINTS.ROOT}/${id}/with-images`, form);
  },
};

export default ProductApi;
