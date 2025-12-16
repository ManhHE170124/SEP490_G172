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
// Lưu ý logic mới BE:
// - ACTIVE       : Hiển thị & còn hàng
// - OUT_OF_STOCK : Hết hàng nhưng vẫn hiển thị
// - INACTIVE     : Ẩn hoàn toàn (chỉ khi admin set)
export const PRODUCT_STATUSES = [
  { value: "ACTIVE",       label: "Hiển thị" },
  { value: "OUT_OF_STOCK", label: "Hết hàng" },
  { value: "INACTIVE",     label: "Ẩn" },
];

// Helper: map code -> label (VI)
export const typeLabelOf   = (v) => PRODUCT_TYPES.find((x) => x.value === v)?.label || v;
export const statusLabelOf = (v) => PRODUCT_STATUSES.find((x) => x.value === v)?.label || v;

// ==== Chuẩn hoá phân trang (paged result) ====
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

// Helper normalize status code theo BE mới
const normalizeStatusCode = (status, fallback = "OUT_OF_STOCK") => {
  const raw = (status ?? "").toString().trim().toUpperCase();
  return raw || fallback;
};

// ==== Chuẩn hoá item list (ProductListItemDto) ====
// DTO BE: (… , int TotalStockQty, IEnumerable<int> CategoryIds, IEnumerable<string> BadgeCodes)
const normalizeListItem = (p = {}) => ({
  productId:   p.productId   ?? p.ProductId,
  productCode: p.productCode ?? p.ProductCode,
  productName: p.productName ?? p.ProductName,
  productType: p.productType ?? p.ProductType,
  totalStock:  p.totalStockQty ?? p.TotalStockQty ?? 0, // tổng stock của các biến thể
  status:      normalizeStatusCode(p.status ?? p.Status),
  categoryIds: p.categoryIds ?? p.CategoryIds ?? [],
  badges:      p.badgeCodes  ?? p.BadgeCodes  ?? [],     // mảng code badge
});

// ==== Chuẩn hoá chi tiết (ProductDetailDto) ====
// DTO BE: ProductDetailDto(..., Status, IEnumerable<int> CategoryIds, IEnumerable<string> BadgeCodes, IEnumerable<ProductVariantMiniDto> Variants)
const normalizeDetail = (p = {}) => ({
  productId:   p.productId   ?? p.ProductId,
  productCode: p.productCode ?? p.ProductCode,
  productName: p.productName ?? p.ProductName,
  productType: p.productType ?? p.ProductType,
  status:      normalizeStatusCode(p.status ?? p.Status),
  categoryIds: p.categoryIds ?? p.CategoryIds ?? [],
  badges:      p.badgeCodes  ?? p.BadgeCodes  ?? [],

  // Hiện tại BE chưa trả FAQ trong ProductDetailDto => fallback []
  faqs: (p.faqs ?? p.Faqs ?? []).map((f) => ({
    faqId:     f.faqId ?? f.FaqId,
    question:  f.question ?? f.Question,
    answer:    f.answer ?? f.Answer,
    sortOrder: f.sortOrder ?? f.SortOrder ?? 0,
    isActive:  !!(f.isActive ?? f.IsActive ?? true),
  })),

  // Variants mini (không kèm giá)
  variants: (p.variants ?? p.Variants ?? []).map((v) => ({
    variantId:    v.variantId ?? v.VariantId,
    variantCode:  v.variantCode ?? v.VariantCode ?? "",
    title:        v.title ?? v.Title ?? "",
    durationDays: v.durationDays ?? v.DurationDays ?? null,
    stockQty:     v.stockQty ?? v.StockQty ?? 0,
    status:       normalizeStatusCode(v.status ?? v.Status),
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
  // BE trả { productId, status }
  toggle: async (id) => {
    const axiosRes = await axiosClient.patch(`${ROOT}/${id}/toggle`);
    return axiosRes?.data ?? axiosRes;
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
