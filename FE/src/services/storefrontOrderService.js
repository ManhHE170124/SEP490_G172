// src/services/storefrontOrderService.js
import axiosClient from "../api/axiosClient";

/**
 * Đóng gói logic tạo DTO CreateOrderDTO từ cart (guest hoặc user)
 * và gọi API /api/orders/checkout
 */
const StorefrontOrderApi = {
  /**
   * Checkout từ cart (dùng cho cả user đã login và guest)
   * @param {object} options
   *  - userId: Guid? (null nếu guest)
   *  - email: string (email nhận hàng)
   *  - cart: object (cart đã normalize: items, totalAmount, totalListAmount, totalDiscount)
   */
  checkoutFromCart: async ({ userId, email, cart }) => {
    if (!cart || !Array.isArray(cart.items) || cart.items.length === 0) {
      throw new Error("Cart is empty");
    }

    // Build OrderDetails từ cart
    const orderDetails = cart.items
      .filter((it) => it.variantId)
      .map((it) => ({
        variantId: it.variantId,
        quantity: it.quantity || 0,
        unitPrice: it.unitPrice ?? 0,
        // Checkout online: chưa gắn Key, xử lý sau khi Paid
        keyId: null,
      }));

    const totalAmount =
      cart.totalAmount ??
      orderDetails.reduce(
        (sum, d) => sum + d.quantity * d.unitPrice,
        0
      );

    const totalListAmount = cart.totalListAmount ?? totalAmount;
    const discountAmount = Math.max(0, totalListAmount - totalAmount);

    const payload = {
      userId: userId || null,
      email,
      totalAmount,
      discountAmount,
      status: "Pending", // backend vẫn ép Pending
      orderDetails,
    };

    const axiosRes = await axiosClient.post("/orders/checkout", payload);
    const data = axiosRes?.data ?? axiosRes;

    // data: { orderId, paymentUrl }
    return data;
  },

  // Hàm cũ, nếu nơi khác còn dùng thì giữ lại, còn luồng mới nên dùng checkoutFromCart
  createFromGuestCart: async ({ email, cart }) => {
    const orderDetails = (cart.items || [])
      .filter((it) => it.variantId)
      .map((it) => ({
        variantId: it.variantId,
        quantity: it.quantity || 0,
        unitPrice: it.unitPrice ?? 0,
        keyId: null,
      }));

    if (!orderDetails.length) {
      throw new Error("Cart is empty");
    }

    const totalAmount = orderDetails.reduce(
      (sum, d) => sum + d.quantity * d.unitPrice,
      0
    );
    const totalListAmount = cart.totalListAmount ?? totalAmount;
    const discountAmount = Math.max(0, totalListAmount - totalAmount);

    const payload = {
      userId: null,
      email,
      totalAmount,
      discountAmount,
      status: "Pending",
      orderDetails,
    };

    const axiosRes = await axiosClient.post("/orders", payload);
    return axiosRes?.data ?? axiosRes;
  },
};

export default StorefrontOrderApi;
