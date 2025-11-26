// src/services/storefrontCartService.js
import axiosClient from "../api/axiosClient";

// Sự kiện global dùng để thông báo cart thay đổi
export const CART_UPDATED_EVENT = "storefront_cart_updated";

// ==== Chuẩn hoá 1 item trong cart ====
const normalizeCartItem = (i = {}) => {
  const quantity = i.quantity ?? i.Quantity ?? 0;

  const unitPriceRaw = i.unitPrice ?? i.UnitPrice ?? null;
  const listPriceRaw = i.listPrice ?? i.ListPrice ?? unitPriceRaw;

  const unitPrice = unitPriceRaw != null ? Number(unitPriceRaw) : null;
  const listPrice = listPriceRaw != null ? Number(listPriceRaw) : null;

  const lineTotalRaw = i.lineTotal ?? i.LineTotal;
  const listLineTotalRaw = i.listLineTotal ?? i.ListLineTotal;

  const lineTotal =
    lineTotalRaw != null
      ? Number(lineTotalRaw)
      : unitPrice != null
      ? unitPrice * quantity
      : 0;

  const listLineTotal =
    listLineTotalRaw != null
      ? Number(listLineTotalRaw)
      : listPrice != null
      ? listPrice * quantity
      : lineTotal;

  const discountAmount = Math.max(0, listLineTotal - lineTotal);
  const discountPercent =
    listLineTotal > 0 && discountAmount > 0
      ? Math.round(100 - (lineTotal / listLineTotal) * 100)
      : 0;

  return {
    variantId: i.variantId ?? i.VariantId,
    productId: i.productId ?? i.ProductId,

    productName: i.productName ?? i.ProductName ?? "",
    productType: i.productType ?? i.ProductType ?? "",

    variantTitle: i.variantTitle ?? i.VariantTitle ?? "",
    thumbnail: i.thumbnail ?? i.Thumbnail ?? null,

    quantity,

    listPrice,
    unitPrice,

    lineTotal,
    listLineTotal,

    discountAmount,
    discountPercent,
  };
};

// ==== Chuẩn hoá toàn bộ cart ====
const normalizeCart = (res = {}) => {
  const itemsRaw = res.items ?? res.Items ?? [];
  const items = Array.isArray(itemsRaw)
    ? itemsRaw.map(normalizeCartItem)
    : [];

  const totalQuantityRaw = res.totalQuantity ?? res.TotalQuantity;
  const totalAmountRaw = res.totalAmount ?? res.TotalAmount;
  const totalListAmountRaw = res.totalListAmount ?? res.TotalListAmount;
  const totalDiscountRaw = res.totalDiscount ?? res.TotalDiscount;

  const totalQuantity =
    totalQuantityRaw != null
      ? Number(totalQuantityRaw)
      : items.reduce((sum, it) => sum + (it.quantity || 0), 0);

  const totalAmount =
    totalAmountRaw != null
      ? Number(totalAmountRaw)
      : items.reduce((sum, it) => sum + (it.lineTotal || 0), 0);

  const totalListAmount =
    totalListAmountRaw != null
      ? Number(totalListAmountRaw)
      : items.reduce(
          (sum, it) => sum + (it.listLineTotal || it.lineTotal || 0),
          0
        );

  const totalDiscount =
    totalDiscountRaw != null
      ? Number(totalDiscountRaw)
      : Math.max(0, totalListAmount - totalAmount);

  const receiverEmail = res.receiverEmail ?? res.ReceiverEmail ?? null;

  // NEW: thông tin tài khoản từ API
  const accountEmail = res.accountEmail ?? res.AccountEmail ?? null;
  const accountUserName =
    res.accountUserName ?? res.AccountUserName ?? null;

  return {
    receiverEmail,
    accountEmail,
    accountUserName,
    items,
    totalQuantity,
    totalAmount,
    totalListAmount,
    totalDiscount,
  };
};

// Bắn event ra window để header (và chỗ khác) nghe được
const notifyCartUpdated = (cart) => {
  if (typeof window === "undefined") return;
  try {
    window.dispatchEvent(
      new CustomEvent(CART_UPDATED_EVENT, {
        detail: { cart },
      })
    );
  } catch (err) {
    console.error("Dispatch cart updated event failed:", err);
  }
};

const ROOT = "storefront/cart";

export const StorefrontCartApi = {
  // Lấy cart hiện tại của user
  getCart: async () => {
    const axiosRes = await axiosClient.get(`${ROOT}`);
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res);
    notifyCartUpdated(cart);
    return cart;
  },

  // Thêm item vào cart (nếu đã có thì BE sẽ cộng dồn quantity)
  addItem: async ({ variantId, quantity }) => {
    const axiosRes = await axiosClient.post(`${ROOT}/items`, {
      variantId,
      quantity,
    });
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res);
    notifyCartUpdated(cart);
    return cart;
  },

  // Cập nhật quantity cho 1 variant
  updateItem: async (variantId, quantity) => {
    const axiosRes = await axiosClient.put(`${ROOT}/items/${variantId}`, {
      quantity,
    });
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res);
    notifyCartUpdated(cart);
    return cart;
  },

  // Xoá 1 item khỏi cart
  removeItem: async (variantId) => {
    const axiosRes = await axiosClient.delete(`${ROOT}/items/${variantId}`);
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res);
    notifyCartUpdated(cart);
    return cart;
  },

  // Xoá toàn bộ cart
  clearCart: async (options = {}) => {
    const { skipRestoreStock = false } = options;

    const url = skipRestoreStock
      ? `${ROOT}?skipRestoreStock=true`
      : `${ROOT}`;

    const axiosRes = await axiosClient.delete(url);
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res ?? { items: [] });
    notifyCartUpdated(cart);
    return cart;
  },

  // Set email nhận hàng cho cart
  setReceiverEmail: async (receiverEmail) => {
    const axiosRes = await axiosClient.put(`${ROOT}/receiver-email`, {
      receiverEmail,
    });
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res);
    notifyCartUpdated(cart);
    return cart;
  },
};

export default StorefrontCartApi;
