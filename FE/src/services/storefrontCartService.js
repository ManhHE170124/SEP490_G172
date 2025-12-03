// src/services/storefrontCartService.js
import axiosClient from "../api/axiosClient";

// Sự kiện global dùng để thông báo cart thay đổi
export const CART_UPDATED_EVENT = "storefront_cart_updated";

// ==== Chuẩn hoá 1 item trong cart (StorefrontCartItemDto) ====
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

// ==== Chuẩn hoá toàn bộ cart (StorefrontCartDto từ BE) ====
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

  // Info tài khoản từ API (user đã đăng nhập)
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

// ==== Chuẩn hoá kết quả checkout (CartCheckoutResultDto – Payment ORDER_CART) ====
const normalizeCheckoutResult = (res = {}) => {
  const paymentId = res.paymentId ?? res.PaymentId ?? null;
  const paymentStatus = res.paymentStatus ?? res.PaymentStatus ?? null;

  const amountRaw = res.amount ?? res.Amount ?? 0;
  const amount = Number(amountRaw);

  const email = res.email ?? res.Email ?? null;
  const createdAt = res.createdAt ?? res.CreatedAt ?? null;
  const paymentUrl = res.paymentUrl ?? res.PaymentUrl ?? null;

  return {
    paymentId,
    paymentStatus,
    amount,
    email,
    createdAt,
    paymentUrl,
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
  // Lấy cart hiện tại của user/guest (server quản lý cả hai)
  getCart: async () => {
    const axiosRes = await axiosClient.get(`${ROOT}`);
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res ?? {});
    notifyCartUpdated(cart);
    return cart;
  },

  // Thêm item vào cart (BE trừ stock + giữ cart trong cache)
  addItem: async ({ variantId, quantity }) => {
    const axiosRes = await axiosClient.post(`${ROOT}/items`, {
      variantId,
      quantity,
    });
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res ?? {});
    notifyCartUpdated(cart);
    return cart;
  },

  // Cập nhật quantity cho 1 variant
  updateItem: async (variantId, quantity) => {
    const axiosRes = await axiosClient.put(`${ROOT}/items/${variantId}`, {
      quantity,
    });
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res ?? {});
    notifyCartUpdated(cart);
    return cart;
  },

  // Xoá 1 item khỏi cart
  removeItem: async (variantId) => {
    const axiosRes = await axiosClient.delete(`${ROOT}/items/${variantId}`);
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res ?? {});
    notifyCartUpdated(cart);
    return cart;
  },

  // Xoá toàn bộ cart
  clearCart: async (options = {}) => {
    const { skipRestoreStock = false } = options;

    const url = skipRestoreStock
      ? `${ROOT}?skipRestoreStock=true`
      : `${ROOT}`;

    await axiosClient.delete(url);

    // BE trả NoContent => tự tạo cart trống ở FE
    const cart = normalizeCart({ items: [], receiverEmail: null });
    notifyCartUpdated(cart);
    return cart;
  },

  // Set email nhận hàng cho cart
  setReceiverEmail: async (receiverEmail) => {
    const axiosRes = await axiosClient.put(`${ROOT}/receiver-email`, {
      receiverEmail,
    });
    const res = axiosRes?.data ?? axiosRes;
    const cart = normalizeCart(res ?? {});
    notifyCartUpdated(cart);
    return cart;
  },

  // Checkout: tạo Payment ORDER_CART + PayOS checkoutUrl.
  // BE:
  //  - tính lại tiền,
  //  - tạo Payment Pending,
  //  - lưu snapshot cart,
  //  - xoá cart hiển thị trên server.
  checkout: async () => {
    const axiosRes = await axiosClient.post(`${ROOT}/checkout`);
    const res = axiosRes?.data ?? axiosRes;
    const checkoutResult = normalizeCheckoutResult(res ?? {});

    // Cart hiện tại trên BE đã bị xoá -> update FE về trạng thái rỗng
    const emptyCart = normalizeCart({ items: [], receiverEmail: null });
    notifyCartUpdated(emptyCart);

    return checkoutResult;
  },
};

export default StorefrontCartApi;
