import api from "../apiClient";

export const ProductApi = {
  list: (params = {}) =>
    api.get("/products/list", { params }).then((r) => r.data),

  get: (id) => api.get(`/products/${id}`).then((r) => r.data),

  create: (payload) => api.post("/products", payload).then((r) => r.data),

  update: (id, payload) =>
    api.put(`/products/${id}`, payload).then((r) => r.data),

  remove: (id) => api.delete(`/products/${id}`).then((r) => r.data),

  changeStatus: (id, status) =>
    api
      .patch(`/products/${id}/status`, JSON.stringify(status), {
        headers: { "Content-Type": "application/json" },
      })
      .then((r) => r.data),
  toggle: (id) => api.patch(`/products/${id}/toggle`).then((r) => r.data),

  bulkPrice: (payload) =>
    api.post("/products/bulk-price", payload).then((r) => r.data),

  exportCsv: () =>
    api.get("/products/export-csv", { responseType: "blob" }).then((r) => r.data),

  importPriceCsv: (file) => {
    const form = new FormData();
    form.append("file", file);
    return api
      .post("/products/import-price-csv", form)
      .then((r) => r.data);
  },
  // Images
  uploadImage: (id, file) => {
    const form = new FormData();
    form.append("file", file);
    return api.post(`/products/${id}/images/upload`, form).then((r) => {
      const d = r.data || {};
      return {
        imageId: d.imageId ?? d.ImageId,
        url: d.url ?? d.Url,
        sortOrder: d.sortOrder ?? d.SortOrder,
        isPrimary: d.isPrimary ?? d.IsPrimary,
      };
    });
  },
  setThumbnail: (id, url) =>
    api
      .post(`/products/${id}/thumbnail`, JSON.stringify(url), {
        headers: { "Content-Type": "application/json" },
      })
      .then((r) => r.data),
  deleteImage: (id, imageId) => api.delete(`/products/${id}/images/${imageId}`).then((r) => r.data),
  reorderImages: (id, imageIds) => api.post(`/products/${id}/images/reorder`, { imageIds }).then((r) => r.data),
  setPrimaryImage: (id, imageId) => api.post(`/products/${id}/images/${imageId}/primary`).then((r) => r.data),
  // Create product + images (multipart) - payload is same fields as ProductCreateDto
  createWithImages: (payload, files = [], primaryIndex = 0) => {
    const form = new FormData();
    // scalar fields
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

    // arrays: CategoryIds, BadgeCodes
    if (payload.categoryIds && Array.isArray(payload.categoryIds)) {
      payload.categoryIds.forEach((id) => form.append("CategoryIds", String(id)));
    }
    if (payload.badgeCodes && Array.isArray(payload.badgeCodes)) {
      payload.badgeCodes.forEach((c) => form.append("BadgeCodes", c));
    }

    // files
    if (files && files.length) {
      for (const f of files) form.append("Images", f);
      form.append("PrimaryIndex", String(primaryIndex ?? 0));
    }

    return api.post("/products/with-images", form).then((r) => r.data);
  },

  // Update product with images (multipart). Payload fields map to ProductUpdateWithImagesForm
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

    if (payload.categoryIds && Array.isArray(payload.categoryIds)) payload.categoryIds.forEach((cid) => form.append("CategoryIds", String(cid)));
    if (payload.badgeCodes && Array.isArray(payload.badgeCodes)) payload.badgeCodes.forEach((b) => form.append("BadgeCodes", b));

    if (deleteImageIds && Array.isArray(deleteImageIds)) deleteImageIds.forEach((did) => form.append("DeleteImageIds", String(did)));

    if (newFiles && newFiles.length) {
      for (const f of newFiles) form.append("NewImages", f);
      if (primaryIndex !== null && primaryIndex !== undefined) form.append("PrimaryIndex", String(primaryIndex));
    }

    return api.put(`/products/${id}/with-images`, form).then((r) => r.data);
  },
};
