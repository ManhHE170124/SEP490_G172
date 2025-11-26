// src/services/guestCartService.js
import { CART_UPDATED_EVENT } from "./storefrontCartService";

const STORAGE_KEY = "ktk_guest_cart_v1";

const readRawCart = () => {
  if (typeof window === "undefined") return { items: [] };
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return { items: [] };
    const parsed = JSON.parse(raw);
    if (!parsed || typeof parsed !== "object") return { items: [] };
    if (!Array.isArray(parsed.items)) parsed.items = [];
    return parsed;
  } catch (err) {
    console.error("Failed to read guest cart from storage", err);
    return { items: [] };
  }
};

const writeRawCart = (raw) => {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(raw));
  } catch (err) {
    console.error("Failed to write guest cart to storage", err);
  }
};

const normalizeItem = (i = {}) => {
  const quantity = Number(i.quantity ?? 0);
  const unitPrice =
    i.unitPrice != null ? Number(i.unitPrice) : 0;
  const listPrice =
    i.listPrice != null ? Number(i.listPrice) : unitPrice;

  const lineTotal = unitPrice * quantity;
  const listLineTotal = listPrice * quantity;

  const discountAmount = Math.max(0, listLineTotal - lineTotal);
  const discountPercent =
    listLineTotal > 0 && discountAmount > 0
      ? Math.round(100 - (lineTotal / listLineTotal) * 100)
      : 0;

  return {
    variantId: i.variantId,
    productId: i.productId,
    productName: i.productName || "",
    productType: i.productType || "",
    variantTitle: i.variantTitle || "",
    thumbnail: i.thumbnail || null,

    quantity,
    listPrice,
    unitPrice,

    lineTotal,
    listLineTotal,
    discountAmount,
    discountPercent,
  };
};

const normalizeCart = (raw = {}) => {
  const itemsRaw = Array.isArray(raw.items) ? raw.items : [];
  const items = itemsRaw.map(normalizeItem);

  const totalQuantity = items.reduce(
    (sum, it) => sum + (it.quantity || 0),
    0
  );
  const totalAmount = items.reduce(
    (sum, it) => sum + (it.lineTotal || 0),
    0
  );
  const totalListAmount = items.reduce(
    (sum, it) => sum + (it.listLineTotal ?? it.lineTotal ?? 0),
    0
  );
  const totalDiscount = Math.max(0, totalListAmount - totalAmount);

  const receiverEmail = raw.receiverEmail || null;

  return {
    receiverEmail,
    // guest không có account
    accountEmail: null,
    accountUserName: null,
    items,
    totalQuantity,
    totalAmount,
    totalListAmount,
    totalDiscount,
  };
};

const notifyCartUpdated = (cart) => {
  if (typeof window === "undefined") return;
  try {
    window.dispatchEvent(
      new CustomEvent(CART_UPDATED_EVENT, {
        detail: { cart },
      })
    );
  } catch (err) {
    console.error("Dispatch guest cart updated event failed:", err);
  }
};

const GuestCartService = {
  // Lấy cart guest
  getCart: () => {
    const raw = readRawCart();
    const cart = normalizeCart(raw);
    notifyCartUpdated(cart);
    return cart;
  },

  // Thêm item vào cart guest
  addItem: ({
    variantId,
    productId,
    productName,
    productType,
    variantTitle,
    thumbnail,
    listPrice,
    unitPrice,
    quantity,
  }) => {
    if (!variantId) return GuestCartService.getCart();

    const raw = readRawCart();
    const items = Array.isArray(raw.items) ? [...raw.items] : [];

    const idx = items.findIndex(
      (x) => x.variantId === variantId
    );

    if (idx >= 0) {
      const currentQty = Number(items[idx].quantity ?? 0);
      items[idx] = {
        ...items[idx],
        quantity: currentQty + Number(quantity ?? 0),
      };
    } else {
      items.push({
        variantId,
        productId,
        productName,
        productType,
        variantTitle,
        thumbnail,
        quantity: Number(quantity ?? 0),
        listPrice,
        unitPrice,
      });
    }

    const nextRaw = { ...raw, items };
    writeRawCart(nextRaw);
    const cart = normalizeCart(nextRaw);
    notifyCartUpdated(cart);
    return cart;
  },

  // Cập nhật quantity
  updateItem: (variantId, quantity) => {
    const raw = readRawCart();
    const items = Array.isArray(raw.items) ? [...raw.items] : [];
    const idx = items.findIndex((x) => x.variantId === variantId);
    if (idx < 0) {
      return normalizeCart(raw);
    }

    if (quantity <= 0) {
      items.splice(idx, 1);
    } else {
      items[idx] = {
        ...items[idx],
        quantity: Number(quantity ?? 0),
      };
    }

    const nextRaw = { ...raw, items };
    writeRawCart(nextRaw);
    const cart = normalizeCart(nextRaw);
    notifyCartUpdated(cart);
    return cart;
  },

  // Xoá 1 item
// Xoá 1 item
removeItem: (variantId) => {
  const raw = readRawCart();
  const items = Array.isArray(raw.items)
    ? raw.items.filter((x) => x.variantId !== variantId)
    : [];

  const nextRaw = { ...raw, items };
  writeRawCart(nextRaw);
  const cart = normalizeCart(nextRaw);
  notifyCartUpdated(cart);
  return cart;
},

  // Xoá toàn bộ cart
  clearCart: () => {
    const nextRaw = { items: [], receiverEmail: null };
    writeRawCart(nextRaw);
    const cart = normalizeCart(nextRaw);
    notifyCartUpdated(cart);
    return cart;
  },

  // Lưu email guest
  setReceiverEmail: (receiverEmail) => {
    const raw = readRawCart();
    const nextRaw = {
      ...raw,
      receiverEmail: receiverEmail || "",
    };
    writeRawCart(nextRaw);
    const cart = normalizeCart(nextRaw);
    notifyCartUpdated(cart);
    return cart;
  },
};

export default GuestCartService;
