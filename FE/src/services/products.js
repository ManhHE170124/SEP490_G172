// src/services/products.js
import axiosClient from "../api/axiosClient";

const ROOT = "products";

// VIỆT HOÁ loại sản phẩm
export const PRODUCT_TYPES = [
  { value: "SHARED_KEY",       label: "Key dùng chung" },
  { value: "PERSONAL_KEY",     label: "Key cá nhân" },
  { value: "SHARED_ACCOUNT",   label: "Tài khoản dùng chung" },
  { value: "PERSONAL_ACCOUNT", label: "Tài khoản cá nhân" },
];

// VIỆT HOÁ trạng thái
export const PRODUCT_STATUSES = [
  { value: "ACTIVE",       label: "Hiển thị" },
  { value: "INACTIVE",     label: "Ẩn" },
  { value: "OUT_OF_STOCK", label: "Hết hàng" },
];

// Helper: map code -> label (VI)
export const typeLabelOf   = (v) => PRODUCT_TYPES.find((x) => x.value === v)?.label || v;
export const statusLabelOf = (v) => PRODUCT_STATUSES.find((x) => x.value === v)?.label || v;

// ==== Chuẩn hoá phân trang (paged result) ====
const normalizePaged = (res, fallbackPageSize = 10) => ({
  items:      res?.items      ?? res?.Items      ?? [],
  totalItems: res?.totalItems ?? res?.TotalItems ?? (res?.items?.length ?? 0),
  totalPages: res?.totalPages ?? res?.TotalPages ?? 1,
  page:       res?.page       ?? res?.Page       ?? 1,
  pageSize:   res?.pageSize   ?? res?.PageSize   ?? fallbackPageSize,
});

// ==== Chuẩn hoá item list (ProductListItemDto) ====
// DTO BE: (… , int TotalStockQty, IEnumerable<int> CategoryIds, IEnumerable<string> BadgeCodes)
const normalizeListItem = (p = {}) => ({
  productId:   p.productId   ?? p.ProductId,
  productCode: p.productCode ?? p.ProductCode,
  productName: p.productName ?? p.ProductName,
  productType: p.productType ?? p.ProductType,
  totalStock:  p.totalStockQty ?? p.TotalStockQty ?? 0, // <-- ĐÚNG FIELD
  status:      (p.status ?? p.Status ?? "INACTIVE").toString().toUpperCase(),
  categoryIds: p.categoryIds ?? p.CategoryIds ?? [],
  badges:      p.badgeCodes  ?? p.BadgeCodes  ?? [],     // <-- ĐÚNG FIELD (mảng code)
});

// ==== Chuẩn hoá chi tiết (ProductDetailDto) ====
// DTO BE: … IEnumerable<string> BadgeCodes
const normalizeDetail = (p = {}) => ({
  productId:   p.productId   ?? p.ProductId,
  productCode: p.productCode ?? p.ProductCode,
  productName: p.productName ?? p.ProductName,
  productType: p.productType ?? p.ProductType,
  status:      (p.status ?? p.Status ?? "INACTIVE").toString().toUpperCase(),
  categoryIds: p.categoryIds ?? p.CategoryIds ?? [],
  badges:      p.badgeCodes  ?? p.BadgeCodes  ?? [],     // <-- ĐÚNG FIELD (mảng code)
  faqs: (p.faqs ?? p.Faqs ?? []).map((f) => ({
    faqId:     f.faqId ?? f.FaqId,
    question:  f.question ?? f.Question,
    answer:    f.answer ?? f.Answer,
    sortOrder: f.sortOrder ?? f.SortOrder ?? 0,
    isActive:  !!(f.isActive ?? f.IsActive ?? true),
  })),
  variants: (p.variants ?? p.Variants ?? []).map((v) => ({
    variantId:    v.variantId ?? v.VariantId,
    variantCode:  v.variantCode ?? v.VariantCode ?? "",
    title:        v.title ?? v.Title ?? "",
    durationDays: v.durationDays ?? v.DurationDays ?? null,
    stockQty:     v.stockQty ?? v.StockQty ?? 0,
    status:       (v.status ?? v.Status ?? "INACTIVE").toString().toUpperCase(),
  })),
});

export const ProductApi = {
  // ===== LIST (GET /api/products/list) =====
  // params: { keyword, categoryId, type, status, badge, badges, productTypes[], sort, direction, page, pageSize }
  list: async (params = {}) => {
    const axiosRes = await axiosClient.get(`${ROOT}/list`, { params });
    const res = axiosRes?.data ?? axiosRes;
    const paged = normalizePaged(res, params.pageSize ?? 10);
    return {
      ...paged,
      items: (paged.items || []).map(normalizeListItem),
    };
  },

  // ===== DETAIL (GET /api/products/{id}) =====
  get: async (id) => {
    const axiosRes = await axiosClient.get(`${ROOT}/${id}`);
    const res = axiosRes?.data ?? axiosRes;
    return normalizeDetail(res);
  },

  // ===== CREATE (POST /api/products) =====
  // payload view có thể giữ 'badges' (mảng code); map sang BadgeCodes đúng với BE
  create: async (payload = {}) => {
    const { badges, ...rest } = payload;
    const fixed = { ...rest, badgeCodes: badges ?? [] };
    const axiosRes = await axiosClient.post(ROOT, fixed);
    const res = axiosRes?.data ?? axiosRes;
    return normalizeDetail(res);
  },

  // ===== UPDATE (PUT /api/products/{id}) =====
  update: (id, payload = {}) => {
    const { badges, ...rest } = payload;
    const fixed = { ...rest, badgeCodes: badges ?? [] };
    return axiosClient.put(`${ROOT}/${id}`, fixed);
  },

  // ===== TOGGLE (PATCH /api/products/{id}/toggle) =====
  toggle: async (id) => {
    const axiosRes = await axiosClient.patch(`${ROOT}/${id}/toggle`);
    return axiosRes?.data ?? axiosRes; // { productId, status }
  },

  // ===== DELETE =====
  remove: (id) => axiosClient.delete(`${ROOT}/${id}`),

  // ===== Hằng số + helper =====
  types: PRODUCT_TYPES,
  statuses: PRODUCT_STATUSES,
  typeLabelOf,
  statusLabelOf,
};

export default ProductApi;
