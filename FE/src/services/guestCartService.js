// src/services/guestCartService.js
import StorefrontCartApi, {
  CART_UPDATED_EVENT,
} from "./storefrontCartService";

// GuestCartService giữ nguyên interface cũ nhưng bên trong
// dùng API server (StorefrontCartApi) cho đúng luồng mới.
const GuestCartService = {
  // Lấy cart guest (thực ra là cart trên server theo anon cookie)
  getCart: () => {
    return StorefrontCartApi.getCart();
  },

  // Thêm item cho guest
  addItem: ({
    variantId,
    quantity,
    // các field khác (productName, listPrice, ...) không cần nữa,
    // BE tự lấy từ ProductVariant, nên mình bỏ qua.
  }) => {
    if (!variantId) {
      return StorefrontCartApi.getCart();
    }

    const qty =
      quantity != null && !Number.isNaN(Number(quantity))
        ? Number(quantity)
        : 1;

    return StorefrontCartApi.addItem({ variantId, quantity: qty });
  },

  // Cập nhật quantity
  updateItem: (variantId, quantity) => {
    const qty =
      quantity != null && !Number.isNaN(Number(quantity))
        ? Number(quantity)
        : 0;

    return StorefrontCartApi.updateItem(variantId, qty);
  },

  // Xoá 1 item
  removeItem: (variantId) => {
    return StorefrontCartApi.removeItem(variantId);
  },

  // Xoá toàn bộ cart guest
  clearCart: () => {
    return StorefrontCartApi.clearCart();
  },

  // Lưu email guest
  setReceiverEmail: (receiverEmail) => {
    return StorefrontCartApi.setReceiverEmail(receiverEmail);
  },
};

export default GuestCartService;
