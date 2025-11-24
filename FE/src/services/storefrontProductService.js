// src/services/storefrontProductService.js
import axiosClient from "../api/axiosClient";
import {
  PRODUCT_TYPES,
  typeLabelOf,
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

// ==== Chuẩn hoá item biến thể (dùng cho list & related) ====
const normalizeVariantItem = (v = {}) => {
  const variantTitle =
    v.variantTitle ?? v.VariantTitle ??
    v.title        ?? v.Title        ??
    "";

  const rawStatus = (v.status ?? v.Status ?? "INACTIVE")
    .toString()
    .toUpperCase();

  const sellPrice = v.sellPrice ?? v.SellPrice ?? null;
  const cogsPrice = v.cogsPrice ?? v.CogsPrice ?? null;

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
    isOutOfStock: rawStatus === "OUT_OF_STOCK",

    sellPrice,
    cogsPrice,

    badges: (v.badges ?? v.Badges ?? []).map(normalizeBadgeMini),
  };
};

// ==== Chuẩn hoá sibling variant trong detail ====
const normalizeSiblingVariant = (v = {}) => {
  const title =
    v.variantTitle ?? v.VariantTitle ??
    v.title        ?? v.Title        ??
    "";

  const rawStatus = (v.status ?? v.Status ?? "INACTIVE")
    .toString()
    .toUpperCase();

  return {
    variantId: v.variantId ?? v.VariantId,
    title,
    status: rawStatus,
    isOutOfStock: rawStatus === "OUT_OF_STOCK",
  };
};

// ==== Chuẩn hoá section trong detail ====
const normalizeSection = (s = {}) => ({
  sectionId:   s.sectionId   ?? s.SectionId,
  sectionType: s.sectionType ?? s.SectionType,
  title:       s.title       ?? s.Title,
  content:     s.content     ?? s.Content ?? "",
});

// ==== Chuẩn hoá FAQ trong detail ====
const normalizeFaq = (f = {}) => ({
  faqId:    f.faqId    ?? f.FaqId,
  question: f.question ?? f.Question,
  answer:   f.answer   ?? f.Answer,
  source:   f.source   ?? f.Source,
});

// ==== Chuẩn hoá detail variant ====
const normalizeVariantDetail = (res = {}) => {
  const main = normalizeVariantItem(res);
  const stockQty = res.stockQty ?? res.StockQty ?? 0;

  return {
    ...main,
    stockQty,

    categories: (res.categories ?? res.Categories ?? []).map(
      normalizeCategoryMini
    ),

    siblingVariants: (res.siblingVariants ?? res.SiblingVariants ?? []).map(
      normalizeSiblingVariant
    ),

    sections: (res.sections ?? res.Sections ?? []).map(normalizeSection),

    faqs: (res.faqs ?? res.Faqs ?? []).map(normalizeFaq),
  };
};

// ==== Chuẩn hoá filters (StorefrontFiltersDto) ====
const normalizeFilters = (res = {}) => ({
  categories: (res.categories ?? res.Categories ?? []).map((c) => ({
    categoryId:   c.categoryId   ?? c.CategoryId,
    categoryCode: c.categoryCode ?? c.CategoryCode,
    categoryName: c.categoryName ?? c.CategoryName,
  })),
  productTypes: res.productTypes ?? res.ProductTypes ?? [],
});

export const StorefrontProductApi = {
  filters: async () => {
    const axiosRes = await axiosClient.get(`${ROOT}/filters`);
    const res = axiosRes?.data ?? axiosRes;
    return normalizeFilters(res);
  },

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

  variantDetail: async (productId, variantId) => {
    const axiosRes = await axiosClient.get(
      `${ROOT}/${productId}/variants/${variantId}/detail`
    );
    const res = axiosRes?.data ?? axiosRes;
    return normalizeVariantDetail(res);
  },

  relatedVariants: async (productId, variantId) => {
    const axiosRes = await axiosClient.get(
      `${ROOT}/${productId}/variants/${variantId}/related`
    );
    const res = axiosRes?.data ?? axiosRes;

    const items = Array.isArray(res)
      ? res
      : res?.items ?? res?.Items ?? [];

    return items.map(normalizeVariantItem);
  },

  types: PRODUCT_TYPES,
  typeLabelOf,
};

export default StorefrontProductApi;
