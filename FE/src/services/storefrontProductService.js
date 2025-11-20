// src/services/storefrontProductService.js
// Service gọi API sản phẩm phía người dùng (storefront)

import axiosClient from "../api/axiosClient";
import {
  PRODUCT_TYPES,   // 4 loại sản phẩm dùng chung với admin
  typeLabelOf,     // helper map code -> nhãn tiếng Việt
} from "./products";

const ROOT = "storefront/products";

// ==== Chuẩn hoá phân trang (paged result) ====
const normalizePaged = (res, fallbackPageSize = 8) => {
  const items = res?.items ?? res?.Items ?? [];
  const pageSize = res?.pageSize ?? res?.PageSize ?? fallbackPageSize;
  const total =
    res?.totalItems ?? res?.TotalItems ??
    res?.total      ?? res?.Total      ??
    items.length;

  const page = res?.page ?? res?.Page ?? 1;

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

// ==== Chuẩn hoá Category mini trong storefront ====
const normalizeCategoryMini = (c = {}) => ({
  categoryId:   c.categoryId   ?? c.CategoryId,
  categoryCode: c.categoryCode ?? c.CategoryCode,
  categoryName: c.categoryName ?? c.CategoryName,
});

// ==== Chuẩn hoá Badge mini trong storefront ====
const normalizeBadgeMini = (b = {}) => ({
  badgeCode:   b.badgeCode   ?? b.BadgeCode,
  displayName: b.displayName ?? b.DisplayName,
  colorHex:    b.colorHex    ?? b.ColorHex ?? null,
  icon:        b.icon        ?? b.Icon ?? null,
});

// src/services/storefrontProductService.js

const normalizeVariantItem = (v = {}) => {
  const variantTitle =
    v.variantTitle ??
    v.VariantTitle ??
    v.title ??
    v.Title ??
    "";

  const rawStatus = (v.status ?? v.Status ?? "INACTIVE")
    .toString()
    .toUpperCase();

  return {
    variantId:   v.variantId   ?? v.VariantId,
    productId:   v.productId   ?? v.ProductId,
    productCode: v.productCode ?? v.ProductCode,
    productName: v.productName ?? v.ProductName,
    productType: v.productType ?? v.ProductType,

    title: variantTitle,
    variantTitle,

    thumbnail: v.thumbnail ?? v.Thumbnail ?? null,
    status: rawStatus,
    isOutOfStock: rawStatus === "OUT_OF_STOCK",   // <== thêm

    badges: (v.badges ?? v.Badges ?? []).map(normalizeBadgeMini),
  };
};



// ==== Chuẩn hoá filters (StorefrontFiltersDto) ====
const normalizeFilters = (res = {}) => ({
  categories: (res.categories ?? res.Categories ?? []).map((c) => ({
    categoryId:   c.categoryId   ?? c.CategoryId,
    categoryCode: c.categoryCode ?? c.CategoryCode,
    categoryName: c.categoryName ?? c.CategoryName,
  })),
  // productTypes bên BE chỉ là array string, FE có thể map sang label bằng PRODUCT_TYPES + typeLabelOf
  productTypes: res.productTypes ?? res.ProductTypes ?? [],
});

export const StorefrontProductApi = {
  // ===== FILTERS (GET /api/storefront/products/filters) =====
  filters: async () => {
    const axiosRes = await axiosClient.get(`${ROOT}/filters`);
    const res = axiosRes?.data ?? axiosRes;
    return normalizeFilters(res);
  },

  // ===== LIST VARIANTS (GET /api/storefront/products/variants) =====
  // params: { q, categoryId, productType, minPrice, maxPrice, sort, page, pageSize }
  listVariants: async (params = {}) => {
    const {
      q,
      categoryId,
      productType,
      minPrice,
      maxPrice,
      sort,
      page = 1,
      pageSize = 8,
    } = params;

    const query = {
      q: q || undefined,
      categoryId: categoryId ?? undefined,
      productType: productType || undefined,
      minPrice: minPrice ?? undefined,
      maxPrice: maxPrice ?? undefined,
      sort: sort || undefined,
      page,
      pageSize,
    };

    const axiosRes = await axiosClient.get(`${ROOT}/variants`, { params: query });
    const res = axiosRes?.data ?? axiosRes;

    const paged = normalizePaged(res, pageSize);
    return {
      ...paged,
      items: (paged.items || []).map(normalizeVariantItem),
    };
  },

  // Re-export mapping loại sản phẩm dùng chung với admin
  types: PRODUCT_TYPES,
  typeLabelOf,
};

export default StorefrontProductApi;
