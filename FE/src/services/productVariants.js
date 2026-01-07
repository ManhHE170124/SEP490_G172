// src/services/productVariantsService.js
// Service cho Product Variants – khớp với Controllers/ProductVariantsController.cs
import axiosClient from "../api/axiosClient";

// ==== Chuẩn hoá phân trang (PagedResult<T>) ====
const normalizePaged = (res, fallbackPageSize = 10) => {
  const items = res?.items ?? res?.Items ?? [];
  const pageSize = res?.pageSize ?? res?.PageSize ?? fallbackPageSize;
  const total =
    res?.totalItems ?? res?.TotalItems ??
    res?.total      ?? res?.Total      ??
    items.length;

  const page =
    res?.page ?? res?.Page ?? 1;

  const totalPages =
    res?.totalPages ?? res?.TotalPages ??
    Math.max(1, Math.ceil(total / pageSize));

  return {
    items,
    totalItems: total,
    totalPages,
    page,
    pageSize,
  };
};

// Helper convert số
const toNumber = (val, fallback = 0) => {
  const num = Number(val);
  return Number.isFinite(num) ? num : fallback;
};

// Helper normalize status theo BE mới
// - ACTIVE       : còn hàng & hiển thị
// - OUT_OF_STOCK : hết hàng nhưng vẫn hiển thị
// - INACTIVE     : ẩn (chỉ khi admin set)
const normalizeStatusCode = (status, fallback = "OUT_OF_STOCK") => {
  const raw = (status ?? "").toString().trim().toUpperCase();
  return raw || fallback;
};

// ==== List item (ProductVariantListItemDto) ====
const normalizeListItem = (v = {}) => ({
  variantId:    v.variantId ?? v.VariantId,
  variantCode:  v.variantCode ?? v.VariantCode ?? "",
  title:        v.title ?? v.Title ?? "",
  durationDays: v.durationDays ?? v.DurationDays ?? null,
  stockQty:     v.stockQty ?? v.StockQty ?? 0,
  status:       normalizeStatusCode(v.status ?? v.Status),
  thumbnail:    v.thumbnail ?? v.Thumbnail ?? null,
  viewCount:    v.viewCount ?? v.ViewCount ?? 0,
  // NEW: map thêm giá từ API list
  sellPrice: toNumber(v.sellPrice ?? v.SellPrice ?? 0, 0),
  listPrice: toNumber(
    v.listPrice ?? v.ListPrice ?? v.cogsPrice ?? v.CogsPrice ?? 0,
    0
  ), // fallback từ cogsPrice nếu data cũ
});

// ==== Detail (GET /api/products/{pid}/variants/{vid}) ====
// BE trả anonymous object:
// {
//   VariantId, ProductId, VariantCode, Title, DurationDays, StockQty,
//   WarrantyDays, Thumbnail, MetaTitle, MetaDescription,
//   ViewCount, Status, SellPrice, ListPrice, CogsPrice, HasSections
// }
const normalizeDetail = (v = {}) => ({
  variantId:    v.variantId ?? v.VariantId,
  productId:    v.productId ?? v.ProductId,
  variantCode:  v.variantCode ?? v.VariantCode ?? "",
  title:        v.title ?? v.Title ?? "",
  durationDays: v.durationDays ?? v.DurationDays ?? null,
  stockQty:     v.stockQty ?? v.StockQty ?? 0,
  warrantyDays: v.warrantyDays ?? v.WarrantyDays ?? null,
  status:       normalizeStatusCode(v.status ?? v.Status),
  thumbnail:    v.thumbnail ?? v.Thumbnail ?? null,
  metaTitle:    v.metaTitle ?? v.MetaTitle ?? null,
  metaDescription: v.metaDescription ?? v.MetaDescription ?? null,
  viewCount:       v.viewCount ?? v.ViewCount ?? 0,
  hasSections:     v.hasSections ?? v.HasSections ?? false,
  // 3 trường giá khi xem chi tiết
  sellPrice: toNumber(v.sellPrice ?? v.SellPrice ?? 0, 0),
  listPrice: toNumber(
    v.listPrice ?? v.ListPrice ?? v.cogsPrice ?? v.CogsPrice ?? 0,
    0
  ),
  cogsPrice: toNumber(v.cogsPrice ?? v.CogsPrice ?? 0, 0), // giá vốn – chỉ hiển thị
});

export const ProductVariantsApi = {
  // GET /api/products/{productId}/variants
  // params: { q, status, dur, minPrice, maxPrice, sort, dir, page, pageSize }
  // sort: created|title|duration|stock|status|views|price
  list: async (productId, params = {}) => {
    const axiosRes = await axiosClient.get(`products/${productId}/variants`, {
      params,
    });
    const res = axiosRes?.data ?? axiosRes;
    const paged = normalizePaged(res, params.pageSize ?? 10);
    return { ...paged, items: (paged.items || []).map(normalizeListItem) };
  },

  // GET /api/products/{pid}/variants/{vid}
  get: async (productId, variantId) => {
    const axiosRes = await axiosClient.get(
      `products/${productId}/variants/${variantId}`
    );
    const res = axiosRes?.data ?? axiosRes;
    return normalizeDetail(res);
  },

  // POST /api/products/{pid}/variants
  // dto: {
  //   variantCode,
  //   title,
  //   durationDays?,
  //   stockQty,
  //   warrantyDays?,
  //   thumbnail?,
  //   metaTitle?,
  //   metaDescription?,
  //   sellPrice,   // bắt buộc phía BE
  //   listPrice,   // bắt buộc phía BE
  //   status?
  // }
  create: async (productId, dto) => {
    const axiosRes = await axiosClient.post(
      `products/${productId}/variants`,
      dto
    );
    const res = axiosRes?.data ?? axiosRes;
    return normalizeDetail(res);
  },

  // PUT /api/products/{pid}/variants/{vid}
  // dto: {
  //   title,
  //   variantCode?,
  //   durationDays?,
  //   stockQty,
  //   warrantyDays?,
  //   thumbnail?,
  //   metaTitle?,
  //   metaDescription?,
  //   status?,
  //   sellPrice?,   // nếu null -> giữ nguyên
  //   listPrice?    // nếu null -> giữ nguyên
  // }
  update: (productId, variantId, dto) =>
    axiosClient.put(`products/${productId}/variants/${variantId}`, dto),

  // DELETE /api/products/{pid}/variants/{vid}
  remove: (productId, variantId) =>
    axiosClient.delete(`products/${productId}/variants/${variantId}`),

  // PATCH /api/products/{pid}/variants/{vid}/toggle  -> { VariantId, Status }
  toggle: async (productId, variantId) => {
    const axiosRes = await axiosClient.patch(
      `products/${productId}/variants/${variantId}/toggle`
    );
    return axiosRes?.data ?? axiosRes;
  },

  // ===== Image helper (Cloudinary via ProductVariantImagesController) =====
  uploadImage: async (file) => {
    const form = new FormData();
    form.append("file", file);
    const axiosRes = await axiosClient.post(
      `productvariantimages/uploadImage`,
      form,
      {
        headers: { "Content-Type": "multipart/form-data" },
      }
    );
    return axiosRes?.data ?? axiosRes; // { path }
  },

  deleteImage: async (publicId) => {
    const axiosRes = await axiosClient.delete(
      `productvariantimages/deleteImage`,
      {
        data: { publicId },
      }
    );
    return axiosRes?.data ?? axiosRes;
  },
};

export default ProductVariantsApi;
