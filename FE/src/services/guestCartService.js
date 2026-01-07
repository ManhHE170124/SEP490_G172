// File: src/services/guestCartService.js
import StorefrontCartApi from "./storefrontCartService";

const GuestCartService = {
  getCart: () => StorefrontCartApi.getCart(),

  addItem: ({ variantId, quantity }) => {
    if (!variantId) return StorefrontCartApi.getCart();
    const qty = quantity != null && !Number.isNaN(Number(quantity)) ? Number(quantity) : 1;
    return StorefrontCartApi.addItem({ variantId, quantity: qty });
  },

  updateItem: (variantId, quantity) => {
    const qty = quantity != null && !Number.isNaN(Number(quantity)) ? Number(quantity) : 0;
    return StorefrontCartApi.updateItem(variantId, qty);
  },

  removeItem: (variantId) => StorefrontCartApi.removeItem(variantId),

  clearCart: () => StorefrontCartApi.clearCart(),

  setReceiverEmail: (receiverEmail) => StorefrontCartApi.setReceiverEmail(receiverEmail),
};

export default GuestCartService;
