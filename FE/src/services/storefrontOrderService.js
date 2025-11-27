// src/services/storefrontOrderService.js
import axiosClient from "../api/axiosClient";

/**
 * Đóng gói logic tạo DTO CreateOrderDTO từ cart (guest hoặc user)
 * và gọi API /api/orders/checkout
 */
const StorefrontOrderApi = {
  checkoutFromCart: async ({ userId, email, cart }) => {
    if (!cart || !Array.isArray(cart.items) || cart.items.length === 0) {
      throw new Error("Cart is empty");
    }

    const orderDetails = cart.items
      .filter((it) => it.variantId)
      .map((it) => ({
        variantId: it.variantId,
        quantity: it.quantity || 0,
        unitPrice: Number(it.unitPrice ?? 0), // đảm bảo là number
        keyId: null,
      }));

    if (!orderDetails.length) {
      throw new Error("Cart is empty");
    }

    // Thành tiền thực tế (sau giảm) = Σ quantity * unitPrice
    const finalAmount = orderDetails.reduce(
      (sum, d) => sum + d.quantity * d.unitPrice,
      0
    );

    // Tổng tiền gốc (trước giảm) = Σ listPrice * quantity
    const totalListAmount = (cart.items || []).reduce((sum, it) => {
      const qty = it.quantity || 0;
      const listPrice =
        it.listPrice != null
          ? Number(it.listPrice)
          : Number(it.unitPrice ?? 0);
      return sum + listPrice * qty;
    }, 0);

    // TotalAmount: tổng tiền gốc, nếu không có thì fallback dùng finalAmount
    const totalAmount = totalListAmount || finalAmount;

    // DiscountAmount: số tiền được giảm
    const discountAmount = Math.max(0, totalAmount - finalAmount);

    const payload = {
      userId: userId || null,
      email,
      totalAmount,     // BE sẽ dùng totalAmount & discountAmount để set Order
      discountAmount,  // GIỜ đã map đúng discount của cart
      status: "Pending", // backend vẫn ép Pending
      orderDetails,
    };

    // Debug nếu cần
    // console.log("Checkout payload:", payload);

    const axiosRes = await axiosClient.post("/orders/checkout", payload);
    const data = axiosRes?.data ?? axiosRes;

    // data: { orderId }
    return data;
  },

  // Hàm cũ, nếu nơi khác còn dùng thì giữ lại, nhưng sửa cho đồng bộ cách tính
  createFromGuestCart: async ({ email, cart }) => {
    const orderDetails = (cart.items || [])
      .filter((it) => it.variantId)
      .map((it) => ({
        variantId: it.variantId,
        quantity: it.quantity || 0,
        unitPrice: Number(it.unitPrice ?? 0),
        keyId: null,
      }));

    if (!orderDetails.length) {
      throw new Error("Cart is empty");
    }

    const finalAmount = orderDetails.reduce(
      (sum, d) => sum + d.quantity * d.unitPrice,
      0
    );

    const totalListAmount = (cart.items || []).reduce((sum, it) => {
      const qty = it.quantity || 0;
      const listPrice =
        it.listPrice != null
          ? Number(it.listPrice)
          : Number(it.unitPrice ?? 0);
      return sum + listPrice * qty;
    }, 0);

    const totalAmount = totalListAmount || finalAmount;
    const discountAmount = Math.max(0, totalAmount - finalAmount);

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
