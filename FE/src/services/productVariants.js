// src/services/productVariants.js
// Product Variants service – theo ProductVariantsController (/api/products/{productId}/variants)
import axiosClient from "../api/axiosClient";

const normalizePaged = (res, fallbackPageSize = 10) => ({
  items: res?.items ?? res?.Items ?? [],
  totalItems: res?.totalItems ?? res?.TotalItems ?? (res?.items?.length ?? 0),
  totalPages: res?.totalPages ?? res?.TotalPages ?? 1,
  page: res?.page ?? res?.Page ?? 1,
  pageSize: res?.pageSize ?? res?.PageSize ?? fallbackPageSize,
});

// Chuẩn hoá 1 item trong list (BE đã có đủ trường, nhưng map phòng trường hợp PascalCase)
const normalizeListItem = (v = {}) => ({
  variantId: v.variantId ?? v.VariantId,
  variantCode: v.variantCode ?? v.VariantCode ?? "",
  title: v.title ?? v.Title ?? "",
  durationDays: v.durationDays ?? v.DurationDays ?? null,
  stockQty: v.stockQty ?? v.StockQty ?? 0,
  status: (v.status ?? v.Status ?? "INACTIVE").toString().toUpperCase(),
  thumbnail: v.thumbnail ?? v.Thumbnail ?? null,
  viewCount: v.viewCount ?? v.ViewCount ?? 0,
});

// Chuẩn hoá detail
const normalizeDetail = (v = {}) => ({
  variantId: v.variantId ?? v.VariantId,
  productId: v.productId ?? v.ProductId,
  variantCode: v.variantCode ?? v.VariantCode ?? "",
  title: v.title ?? v.Title ?? "",
  durationDays: v.durationDays ?? v.DurationDays ?? null,
  stockQty: v.stockQty ?? v.StockQty ?? 0,
  warrantyDays: v.warrantyDays ?? v.WarrantyDays ?? null,
  status: (v.status ?? v.Status ?? "INACTIVE").toString().toUpperCase(),
  thumbnail: v.thumbnail ?? v.Thumbnail ?? null,
  metaTitle: v.metaTitle ?? v.MetaTitle ?? null,
  metaDescription: v.metaDescription ?? v.MetaDescription ?? null,
  viewCount: v.viewCount ?? v.ViewCount ?? 0,
});

export const ProductVariantsApi = {
  /**
   * List + filter + sort (sort: created|title|duration|stock|status|views)
   * params: { q, status, dur, sort, dir, page, pageSize }
   */
  list: async (productId, params = {}) => {
    const axiosRes = await axiosClient.get(`products/${productId}/variants`, { params });
    const res = axiosRes?.data ?? axiosRes;
    const paged = normalizePaged(res, params.pageSize ?? 10);
    return {
      ...paged,
      items: (paged.items || []).map(normalizeListItem),
    };
  },

  /**
   * Get detail
   */
  get: async (productId, variantId) => {
    const axiosRes = await axiosClient.get(`products/${productId}/variants/${variantId}`);
    const res = axiosRes?.data ?? axiosRes;
    return normalizeDetail(res);
  },

  /**
   * Create
   * dto: { variantCode, title, durationDays, stockQty, warrantyDays, status?, thumbnail?, metaTitle?, metaDescription? }
   */
  create: async (productId, dto) => {
    const axiosRes = await axiosClient.post(`products/${productId}/variants`, dto);
    const res = axiosRes?.data ?? axiosRes;
    return normalizeDetail(res);
  },

  /**
   * Update
   * dto: { title, durationDays, stockQty, warrantyDays, status?, thumbnail?, metaTitle?, metaDescription? }
   */
  update: (productId, variantId, dto) =>
    axiosClient.put(`products/${productId}/variants/${variantId}`, dto),

  /**
   * Delete
   */
  remove: (productId, variantId) =>
    axiosClient.delete(`products/${productId}/variants/${variantId}`),

  /**
   * Toggle status (PATCH) -> { VariantId, Status }
   */
  toggle: async (productId, variantId) => {
    const axiosRes = await axiosClient.patch(`products/${productId}/variants/${variantId}/toggle`);
    return axiosRes?.data ?? axiosRes;
  },

  // ===== Image helper (Cloudinary via ProductVariantImagesController) =====

  /**
   * Upload thumbnail image (multipart/form-data)
   * return: { path: 'https://res.cloudinary.com/.../image.png' }
   */
  uploadImage: async (file) => {
    const form = new FormData();
    form.append("file", file);
    const axiosRes = await axiosClient.post(`productvariantimages/uploadImage`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
    return axiosRes?.data ?? axiosRes; // { path }
  },

  /**
   * Delete image from Cloudinary by publicId
   * body: { publicId: 'folder/xxx' }
   */
  deleteImage: async (publicId) => {
    const axiosRes = await axiosClient.delete(`productvariantimages/deleteImage`, {
      data: { publicId },
    });
    return axiosRes?.data ?? axiosRes;
  },
};

export default ProductVariantsApi;
