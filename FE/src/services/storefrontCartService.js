// File: src/services/storefrontCartService.js
import axiosClient from "../api/axiosClient";

export const CART_UPDATED_EVENT = "storefront_cart_updated";

/* ====================== GUEST ANON ID (MUST MATCH BE) ====================== */
/**
 * BE đang dùng AnonymousId để lookup cart khi guest checkout【:contentReference[oaicite:5]{index=5}】
 * FE đã lưu ổn định key này trong axiosClient: "ktk_guest_cart_id"
 * => reuse đúng key để body /orders/checkout có anonymousId.
 */
const GUEST_CART_STORAGE_KEY = "ktk_guest_cart_id";
function getGuestAnonymousId() {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage.getItem(GUEST_CART_STORAGE_KEY);
  } catch {
    return null;
  }
}

/* ====================== NORMALIZE ====================== */

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
    cartItemId: i.cartItemId ?? i.CartItemId ?? null,
    variantId: i.variantId ?? i.VariantId,
    productId: i.productId ?? i.ProductId,

    productName: i.productName ?? i.ProductName ?? "",
    productType: i.productType ?? i.ProductType ?? "",

    variantTitle: i.variantTitle ?? i.VariantTitle ?? "",
    thumbnail: i.thumbnail ?? i.Thumbnail ?? null,
    slug: i.slug ?? i.Slug ?? "",

    quantity,

    listPrice,
    unitPrice,

    lineTotal,
    listLineTotal,

    discountAmount,
    discountPercent,
  };
};

const normalizeCart = (res = {}) => {
  const itemsRaw = res.items ?? res.Items ?? [];
  const items = Array.isArray(itemsRaw) ? itemsRaw.map(normalizeCartItem) : [];

  const cartId = res.cartId ?? res.CartId ?? null;
  const status = res.status ?? res.Status ?? "Active";
  const updatedAt = res.updatedAt ?? res.UpdatedAt ?? null;

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
      : items.reduce((sum, it) => sum + (it.listLineTotal || it.lineTotal || 0), 0);

  const totalDiscount =
    totalDiscountRaw != null
      ? Number(totalDiscountRaw)
      : Math.max(0, totalListAmount - totalAmount);

  const receiverEmail = res.receiverEmail ?? res.ReceiverEmail ?? null;

  const accountEmail = res.accountEmail ?? res.AccountEmail ?? null;
  const accountUserName = res.accountUserName ?? res.AccountUserName ?? null;

  return {
    cartId,
    status,
    updatedAt,
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

const normalizeCheckoutResult = (res = {}) => {
  const paymentId = res.paymentId ?? res.PaymentId ?? null;
  const paymentStatus = res.paymentStatus ?? res.PaymentStatus ?? null;

  const amountRaw = res.amount ?? res.Amount ?? 0;
  const amount = Number(amountRaw);

  const email = res.email ?? res.Email ?? null;
  const createdAt = res.createdAt ?? res.CreatedAt ?? null;

  // BE mới trả CheckoutUrl【:contentReference[oaicite:6]{index=6}】
  const paymentUrl =
    res.paymentUrl ??
    res.PaymentUrl ??
    res.checkoutUrl ??
    res.CheckoutUrl ??
    null;

  const orderId = res.orderId ?? res.OrderId ?? null;
  const orderCode = res.orderCode ?? res.OrderCode ?? null;

  const expiresAtUtc = res.expiresAtUtc ?? res.ExpiresAtUtc ?? null;
  const paymentLinkId = res.paymentLinkId ?? res.PaymentLinkId ?? null;

  return {
    paymentId,
    paymentStatus,
    amount,
    email,
    createdAt,
    paymentUrl,
    orderId,
    orderCode,
    expiresAtUtc,
    paymentLinkId,
  };
};

const notifyCartUpdated = (cart) => {
  if (typeof window === "undefined") return;
  try {
    window.dispatchEvent(new CustomEvent(CART_UPDATED_EVENT, { detail: { cart } }));
  } catch (err) {
    console.error("Dispatch cart updated event failed:", err);
  }
};

const ROOT = "/storefront/cart";

export const StorefrontCartApi = {
  getCart: async () => {
    const axiosRes = await axiosClient.get(`${ROOT}`);
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res ?? {});
    notifyCartUpdated(cart);
    return cart;
  },

  addItem: async ({ variantId, quantity }) => {
    const axiosRes = await axiosClient.post(`${ROOT}/items`, { variantId, quantity });
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res ?? {});
    notifyCartUpdated(cart);
    return cart;
  },

  updateItem: async (variantId, quantity) => {
    const axiosRes = await axiosClient.put(`${ROOT}/items/${variantId}`, { quantity });
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res ?? {});
    notifyCartUpdated(cart);
    return cart;
  },

  removeItem: async (variantId) => {
    const axiosRes = await axiosClient.delete(`${ROOT}/items/${variantId}`);
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res ?? {});
    notifyCartUpdated(cart);
    return cart;
  },

  clearCart: async (options = {}) => {
    const { skipRestoreStock = false } = options;
    const url = skipRestoreStock ? `${ROOT}?skipRestoreStock=true` : `${ROOT}`;
    await axiosClient.delete(url);

    const cart = normalizeCart({ items: [], receiverEmail: null });
    notifyCartUpdated(cart);
    return cart;
  },

  setReceiverEmail: async (receiverEmail) => {
    const axiosRes = await axiosClient.put(`${ROOT}/receiver-email`, { receiverEmail });
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res ?? {});
    notifyCartUpdated(cart);
    return cart;
  },

  /**
   * ✅ NEW: Checkout theo BE mới:
   * POST /api/orders/checkout
   * - guest: cần anonymousId + deliveryEmail【:contentReference[oaicite:7]{index=7}】【:contentReference[oaicite:8]{index=8}】
   * - trả: { OrderId, PaymentId, CheckoutUrl, ExpiresAtUtc }【:contentReference[oaicite:9]{index=9}】
   */
  checkout: async (payload = {}) => {
    const anonymousId = payload.anonymousId ?? getGuestAnonymousId();

    const dto = {
      anonymousId,
      deliveryEmail: payload.deliveryEmail ?? payload.email ?? null,
      buyerName: payload.buyerName ?? null,
      buyerPhone: payload.buyerPhone ?? null,
      // returnUrl/cancelUrl: để BE tự default /checkout/return & /checkout/cancel【:contentReference[oaicite:10]{index=10}】
      returnUrl: payload.returnUrl ?? null,
      cancelUrl: payload.cancelUrl ?? null,
    };

    const axiosRes = await axiosClient.post(`/orders/checkout`, dto);
    const res = axiosRes?.data ?? axiosRes;
    const checkoutResult = normalizeCheckoutResult(res ?? {});

    // FE coi như cart đã “chuyển sang order”, header cart count về 0
    const emptyCart = normalizeCart({ items: [], receiverEmail: null });
    notifyCartUpdated(emptyCart);

    return checkoutResult;
  },
};

export default StorefrontCartApi;
