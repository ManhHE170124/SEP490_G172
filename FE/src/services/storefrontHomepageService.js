// src/services/storefrontHomepageService.js
import axiosClient from "../api/axiosClient";

// dùng chung mapping loại sản phẩm nếu cần
import { PRODUCT_TYPES, typeLabelOf } from "./products";

const ROOT = "storefront/homepage";

// ==== Chuẩn hoá Badge mini (giống storefrontProductService) ====
const normalizeBadgeMini = (b = {}) => ({
  badgeCode:   b.badgeCode   ?? b.BadgeCode,
  displayName: b.displayName ?? b.DisplayName,
  colorHex:    b.colorHex    ?? b.ColorHex ?? null,
  icon:        b.icon        ?? b.Icon ?? null,
});

// ==== Chuẩn hoá item biến thể (giống storefrontProductService) ====
const normalizeVariantItem = (v = {}) => {
  const variantTitle =
    v.variantTitle ?? v.VariantTitle ??
    v.title        ?? v.Title        ??
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
    isOutOfStock: rawStatus === "OUT_OF_STOCK",

    badges: (v.badges ?? v.Badges ?? []).map(normalizeBadgeMini),
  };
};

// ==== Chuẩn hoá DTO trả về từ /api/storefront/homepage/products ====
const normalizeHomepageProducts = (res = {}) => ({
  todayBestDeals: (res.todayBestDeals ?? res.TodayBestDeals ?? []).map(
    normalizeVariantItem
  ),
  bestSellers: (res.bestSellers ?? res.BestSellers ?? []).map(
    normalizeVariantItem
  ),
  weeklyTrends: (res.weeklyTrends ?? res.WeeklyTrends ?? []).map(
    normalizeVariantItem
  ),
  newlyUpdated: (res.newlyUpdated ?? res.NewlyUpdated ?? []).map(
    normalizeVariantItem
  ),
});

export const StorefrontHomepageApi = {
  products: async () => {
    const axiosRes = await axiosClient.get(`${ROOT}/products`);
    const res = axiosRes?.data ?? axiosRes;
    return normalizeHomepageProducts(res);
  },

  // re-export để page có thể dùng cho label nếu cần
  types: PRODUCT_TYPES,
  typeLabelOf,
};

export default StorefrontHomepageApi;
