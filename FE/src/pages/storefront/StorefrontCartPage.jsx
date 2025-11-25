// src/pages/storefront/StorefrontCartPage.jsx
import React, { useEffect, useState, useCallback } from "react";
import { Link, useNavigate } from "react-router-dom";
import StorefrontCartApi from "../../services/storefrontCartService";
import StorefrontProductApi from "../../services/storefrontProductService";
import Toast from "../../components/Toast/Toast";
import ConfirmDialog from "../../components/Toast/ConfirmDialog";
import "./StorefrontCartPage.css";
import GuestCartService from "../../services/guestCartService"; // NEW
import StorefrontOrderApi from "../../services/storefrontOrderService";

const formatCurrency = (value) => {
  if (value == null) return "0₫";
  try {
    return new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: "VND",
      maximumFractionDigits: 0,
    }).format(value);
  } catch {
    return `${value}₫`;
  }
};

const readCustomerFromStorage = () => {
  if (typeof window === "undefined") return null;
  try {
    const token = window.localStorage.getItem("access_token");
    const storedUser = window.localStorage.getItem("user");
    if (!token || !storedUser) return null;
    const parsed = JSON.parse(storedUser);
    return parsed?.profile ?? parsed;
  } catch (error) {
    console.error("Failed to parse stored user", error);
    return null;
  }
};

const StorefrontCartPage = () => {
  const [cart, setCart] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [updatingItemId, setUpdatingItemId] = useState(null);
  const [updatingEmail, setUpdatingEmail] = useState(false);
  const [localEmail, setLocalEmail] = useState("");

  // Toast state
  const [toasts, setToasts] = useState([]);
  const [customer] = useState(() => readCustomerFromStorage());

  // Confirm dialog state
  const [confirmDialog, setConfirmDialog] = useState({
    isOpen: false,
    title: "",
    message: "",
    action: null,
  });

  const navigate = useNavigate();

  // ===== Toast helpers =====
  const removeToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const addToast = useCallback(
    (type, title, message) => {
      const id = Date.now() + Math.random();
      const toast = { id, type, title, message };
      setToasts((prev) => [...prev, toast]);
      // auto-hide sau 4s
      setTimeout(() => removeToast(id), 4000);
    },
    [removeToast]
  );

  // ===== Confirm dialog helpers =====
  const closeConfirmDialog = () => {
    setConfirmDialog((prev) => ({
      ...prev,
      isOpen: false,
      action: null,
    }));
  };

  const handleConfirmDialogConfirm = () => {
    if (confirmDialog.action) {
      confirmDialog.action();
    }
    closeConfirmDialog();
  };

  // ===== Load cart =====
  const loadCart = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      let res;

      if (customer) {
        // Đã đăng nhập -> cart server-side
        res = await StorefrontCartApi.getCart();

        const emailFromApi =
          res.accountEmail || res.receiverEmail || "";
        setLocalEmail(emailFromApi);
      } else {
        // Guest -> cart frontend
        res = GuestCartService.getCart();

        const emailFromApi = res.receiverEmail || "";
        setLocalEmail(emailFromApi);
      }

      setCart(res);
    } catch (err) {
      console.error("Load cart failed:", err);
      if (err?.response?.status === 401) {
        setError("Bạn cần đăng nhập để sử dụng giỏ hàng.");
      } else {
        setError("Không tải được giỏ hàng. Vui lòng thử lại sau.");
      }
    } finally {
      setLoading(false);
    }
  }, [customer]);

  useEffect(() => {
    loadCart();
  }, [loadCart]);

  // Thực hiện xoá 1 item (sau khi đã confirm)
  const actuallyRemoveItem = async (item) => {
    setUpdatingItemId(item.variantId);
    try {
      const res = customer
        ? await StorefrontCartApi.removeItem(item.variantId)
        : GuestCartService.removeItem(item.variantId);

      setCart(res);
      addToast(
        "success",
        "Đã xoá sản phẩm",
        `Đã xoá "${
          item.variantTitle ||
          item.title ||
          item.productName ||
          "sản phẩm"
        }" khỏi giỏ hàng.`
      );
    } catch (err) {
      console.error("Remove item failed:", err);
      addToast(
        "error",
        "Xoá sản phẩm thất bại",
        "Không thể xoá sản phẩm khỏi giỏ. Vui lòng thử lại."
      );
    } finally {
      setUpdatingItemId(null);
    }
  };

  // Mở confirm dialog để xoá 1 item
  const handleRemoveItem = (item) => {
    const itemName =
      item.variantTitle || item.title || item.productName || "sản phẩm này";
    setConfirmDialog({
      isOpen: true,
      title: "Xoá sản phẩm khỏi giỏ hàng?",
      message: `Bạn có chắc muốn xoá "${itemName}" khỏi giỏ hàng?`,
      action: () => actuallyRemoveItem(item),
    });
  };

  // Thay đổi quantity trong cart
  const handleChangeQuantity = async (item, delta) => {
    if (!cart) return;
    const newQty = (item.quantity || 0) + delta;
    if (newQty < 0) return;

    if (newQty === 0) {
      const itemName =
        item.variantTitle || item.title || item.productName || "sản phẩm này";
      setConfirmDialog({
        isOpen: true,
        title: "Xoá sản phẩm khỏi giỏ hàng?",
        message: `Bạn có chắc muốn xoá "${itemName}" khỏi giỏ hàng?`,
        action: () => actuallyRemoveItem(item),
      });
      return;
    }

    setUpdatingItemId(item.variantId);
    try {
      const res = customer
        ? await StorefrontCartApi.updateItem(item.variantId, newQty)
        : GuestCartService.updateItem(item.variantId, newQty);

      setCart(res);
    } catch (err) {
      console.error("Update quantity failed:", err);
      const serverMsg = err?.response?.data?.message;
      const type = err?.response?.status === 400 ? "warning" : "error";
      addToast(
        type,
        "Cập nhật số lượng thất bại",
        serverMsg || "Không thể cập nhật số lượng. Vui lòng thử lại."
      );
    } finally {
      setUpdatingItemId(null);
    }
  };

  // Thực hiện xoá toàn bộ giỏ
  const actuallyClearCart = async () => {
    if (!cart || !cart.items || cart.items.length === 0) return;

    try {
      const res = customer
        ? await StorefrontCartApi.clearCart()
        : GuestCartService.clearCart();

      setCart(res);
      addToast("success", "Đã xoá giỏ hàng", "Toàn bộ sản phẩm đã được xoá.");
    } catch (err) {
      console.error("Clear cart failed:", err);
      addToast(
        "error",
        "Xoá giỏ hàng thất bại",
        "Không thể xoá giỏ hàng. Vui lòng thử lại."
      );
    }
  };

  // Bấm nút "Xoá toàn bộ giỏ hàng" -> mở confirm
  const handleClearCart = () => {
    if (!cart || !cart.items || cart.items.length === 0) return;
    setConfirmDialog({
      isOpen: true,
      title: "Xoá toàn bộ giỏ hàng?",
      message:
        "Tất cả sản phẩm sẽ bị xoá khỏi giỏ hàng của bạn và không thể hoàn tác.",
      action: () => actuallyClearCart(),
    });
  };

  const handleSaveEmail = async () => {
    if (!localEmail.trim()) {
      addToast(
        "warning",
        "Thiếu email nhận hàng",
        "Vui lòng nhập email nhận hàng."
      );
      return;
    }

    setUpdatingEmail(true);
    try {
      const trimmed = localEmail.trim();
      const res = customer
        ? await StorefrontCartApi.setReceiverEmail(trimmed)
        : GuestCartService.setReceiverEmail(trimmed);

      setCart(res);
      addToast(
        "success",
        "Đã lưu email nhận hàng",
        "Email nhận hàng đã được cập nhật cho đơn hàng."
      );
    } catch (err) {
      console.error("Update receiver email failed:", err);
      addToast(
        "error",
        "Lưu email nhận hàng thất bại",
        "Không thể lưu email nhận hàng. Vui lòng thử lại."
      );
    } finally {
      setUpdatingEmail(false);
    }
  };

    const handleProceedCheckout = async () => {
    if (!cart || !cart.items || cart.items.length === 0) return;

    // email ưu tiên:
    // - user login: cart.accountEmail (do server gửi về)
    // - guest: localEmail nhập tay
    const effectiveEmail = (
      (customer ? cart.accountEmail : null) ||
      localEmail ||
      ""
    ).trim();

    if (!effectiveEmail) {
      addToast(
        "warning",
        "Thiếu email nhận hàng",
        "Vui lòng nhập email nhận hàng trước khi thanh toán."
      );
      return;
    }

    try {
      let checkoutCart = cart;

      if (customer) {
        // User login: sync email nhận hàng lên server cart (cho đẹp)
        await StorefrontCartApi.setReceiverEmail(effectiveEmail);
        // Có thể reload lại cart từ API, nhưng ở đây cart state đã chuẩn nên dùng luôn
      } else {
        // Guest: lưu email vào guest cart local
        GuestCartService.setReceiverEmail(effectiveEmail);
        checkoutCart = GuestCartService.getCart();
        setCart(checkoutCart);
      }

      // Lấy userId nếu có (tùy cấu trúc user khi login)
      const userId =
        customer?.userId ??
        customer?.userID ??
        customer?.id ??
        null;

      // Gọi /api/orders/checkout để:
      //  - tạo Order Pending
      //  - tạo Payment Pending
      //  - nhận lại paymentUrl của PayOS
      const { orderId, paymentUrl } =
        await StorefrontOrderApi.checkoutFromCart({
          userId,
          email: effectiveEmail,
          cart: checkoutCart,
        });

      // Sau khi tạo Order thành công:
      //  - clear cart
      //  - KHÔNG trả stock về kho (vì OrdersController đã trừ kho)
      if (customer) {
        await StorefrontCartApi.clearCart({
          skipRestoreStock: true,
        });
      } else {
        GuestCartService.clearCart();
      }

      setCart(null);
      setLocalEmail(effectiveEmail);

      // Redirect sang trang thanh toán PayOS
      window.location.href = paymentUrl;
    } catch (err) {
      console.error("Checkout failed:", err);
      const serverMsg = err?.response?.data?.message;
      addToast(
        "error",
        "Thanh toán thất bại",
        serverMsg || "Không thể tạo đơn hàng. Vui lòng thử lại."
      );
    }
  };


  const handleContinueShopping = () => {
    navigate("/products");
  };

  const hasItems = cart && cart.items && cart.items.length > 0;

  return (
    <main className="sf-cart-page">
      {/* Toast stack */}
      <div className="toast-container">
        {toasts.map((t) => (
          <Toast key={t.id} toast={t} onRemove={removeToast} />
        ))}
      </div>

      {/* Confirm dialog */}
      <ConfirmDialog
        isOpen={confirmDialog.isOpen}
        title={confirmDialog.title}
        message={confirmDialog.message}
        onConfirm={handleConfirmDialogConfirm}
        onCancel={closeConfirmDialog}
        confirmText="Đồng ý"
        cancelText="Hủy"
      />

      <div className="sf-cart-container">
        <div className="sf-cart-breadcrumb">
          <Link to="/">Trang chủ</Link>
          <span>/</span>
          <span>Giỏ hàng</span>
        </div>

        <header className="sf-cart-header">
          <h1 className="sf-cart-title">Giỏ hàng của bạn</h1>
          <p className="sf-cart-subtitle">
            Kiểm tra lại sản phẩm trước khi tiến hành thanh toán.
          </p>
        </header>

        {error && <div className="sf-cart-error">{error}</div>}

        {loading && (
          <div className="sf-cart-loading">Đang tải giỏ hàng...</div>
        )}

        {!loading && !error && (
          <div className="sf-cart-layout">
            {/* Cột trái: danh sách item */}
            <section className="sf-cart-main">
              {!hasItems && (
                <div className="sf-cart-empty">
                  <p>Giỏ hàng của bạn đang trống.</p>
                  <div className="sf-cart-empty-actions">
                    <button
                      type="button"
                      className="sf-btn sf-btn-primary"
                      onClick={handleContinueShopping}
                    >
                      Tiếp tục mua sắm
                    </button>
                  </div>
                </div>
              )}

              {hasItems && (
                <>
                  <ul className="sf-cart-items">
                    {cart.items.map((item) => {
                      const isUpdating =
                        updatingItemId === item.variantId;

                      // Giống bên product list: variantTitle + typeLabel
                      const variantTitle =
                        item.variantTitle ||
                        item.title ||
                        item.productName ||
                        "Sản phẩm không xác định";

                      let typeLabel = "";
                      try {
                        typeLabel =
                          StorefrontProductApi.typeLabelOf(
                            item.productType
                          );
                      } catch {
                        typeLabel = "";
                      }

                      const displayTitle = typeLabel
                        ? `${variantTitle} - ${typeLabel}`
                        : variantTitle;

                      const unitPrice = item.unitPrice ?? 0;
                      const listPrice = item.listPrice;
                      const showOldPrice =
                        listPrice != null && listPrice > unitPrice;

                      // ưu tiên discountPercent từ backend, nếu không có thì tự tính
                      let discountPercent = item.discountPercent ?? 0;
                      if (
                        (!discountPercent || discountPercent <= 0) &&
                        showOldPrice &&
                        unitPrice > 0
                      ) {
                        discountPercent = Math.round(
                          100 - (unitPrice / listPrice) * 100
                        );
                      }

                      return (
                        <li
                          key={item.variantId}
                          className="sf-cart-item"
                        >
                          <div className="sf-cart-item-media">
                            {item.thumbnail ? (
                              <img
                                src={item.thumbnail}
                                alt={displayTitle}
                              />
                            ) : (
                              <div className="sf-cart-item-media-placeholder">
                                {displayTitle?.[0] || "K"}
                              </div>
                            )}
                          </div>

                          <div className="sf-cart-item-body">
                            <div className="sf-cart-item-title">
                              {displayTitle}
                            </div>

                            <div className="sf-cart-item-actions">
                              <button
                                type="button"
                                className="sf-btn sf-btn-outline"
                                onClick={() =>
                                  handleChangeQuantity(item, -1)
                                }
                                disabled={isUpdating}
                              >
                                -
                              </button>
                              <span className="sf-cart-qty-value">
                                {item.quantity}
                              </span>
                              <button
                                type="button"
                                className="sf-btn sf-btn-outline"
                                onClick={() =>
                                  handleChangeQuantity(item, 1)
                                }
                                disabled={isUpdating}
                              >
                                +
                              </button>

                              <button
                                type="button"
                                className="sf-btn sf-btn-outline"
                                onClick={() => handleRemoveItem(item)}
                                disabled={isUpdating}
                              >
                                Xoá
                              </button>
                            </div>
                          </div>

                          <div className="sf-cart-item-right">
                            {/* Giá bán hiện tại */}
                            <div className="sf-cart-item-price-now">
                              {formatCurrency(unitPrice)}
                            </div>

                            {/* Giá niêm yết (nếu cao hơn giá bán) */}
                            {showOldPrice && (
                              <div className="sf-cart-item-price-old">
                                {formatCurrency(listPrice)}
                              </div>
                            )}

                            {/* % giảm giá */}
                            {discountPercent > 0 && (
                              <div className="sf-cart-item-discount-tag">
                                Giảm {discountPercent}%
                              </div>
                            )}
                          </div>
                        </li>
                      );
                    })}
                  </ul>

                  <div className="sf-cart-actions-secondary">
                    <button
                      type="button"
                      className="sf-btn sf-btn-outline"
                      onClick={handleContinueShopping}
                    >
                      &larr; Tiếp tục mua sắm
                    </button>

                    <button
                      type="button"
                      className="sf-btn sf-btn-outline"
                      onClick={handleClearCart}
                    >
                      Xoá toàn bộ giỏ hàng
                    </button>
                  </div>
                </>
              )}
            </section>

            {/* Cột phải: tóm tắt & email */}
            <aside className="sf-cart-summary">
              <h2 className="sf-cart-summary-title">
                Thông tin đơn hàng
              </h2>

              {hasItems && (
                <>
                  <div className="sf-cart-summary-row">
                    <span className="sf-cart-summary-label">
                      Tổng tiền theo giá gốc
                    </span>
                    <span className="sf-cart-summary-value">
                      {formatCurrency(cart.totalListAmount)}
                    </span>
                  </div>

                  <div className="sf-cart-summary-row sf-cart-summary-discount">
                    <span className="sf-cart-summary-label">
                      Bạn tiết kiệm được
                    </span>
                    <span className="sf-cart-summary-value">
                      -{formatCurrency(cart.totalDiscount)}
                    </span>
                  </div>

                  <div className="sf-cart-summary-row sf-cart-summary-total">
                    <span className="sf-cart-summary-label">
                      Tổng thanh toán
                    </span>
                    <span className="sf-cart-summary-value">
                      {formatCurrency(cart.totalAmount)}
                    </span>
                  </div>

                  <div className="sf-cart-email-block">
                    <div className="sf-cart-email-label">
                      Email nhận hàng (gửi key / tài khoản):
                    </div>

                    {cart.accountEmail ? (
                      // user đã đăng nhập -> show username + email, không cho sửa
                      <div className="sf-cart-email-readonly">
                        {cart.accountUserName && (
                          <div className="sf-cart-account-name">
                            {cart.accountUserName}
                          </div>
                        )}
                        <div className="sf-cart-account-email">
                          {cart.accountEmail}
                        </div>
                        <div className="sf-cart-email-note">
                          Email nhận hàng sẽ dùng email của tài khoản
                          này.
                        </div>
                      </div>
                    ) : (
                      // Guest (không login) -> input + nút Lưu
                      <div className="sf-cart-email-input-row">
                        <input
                          type="email"
                          className="sf-cart-email-input"
                          placeholder="nhapemail@domain.com"
                          value={localEmail}
                          onChange={(e) =>
                            setLocalEmail(e.target.value)
                          }
                        />
                        <button
                          type="button"
                          className="sf-btn sf-btn-outline"
                          onClick={handleSaveEmail}
                          disabled={updatingEmail}
                        >
                          Lưu
                        </button>
                      </div>
                    )}
                  </div>

                  <div className="sf-cart-actions-main">
                    <button
                      type="button"
                      className="sf-btn sf-btn-primary sf-btn-lg"
                      onClick={handleProceedCheckout}
                    >
                      Tiến hành thanh toán
                    </button>
                  </div>
                </>
              )}

              {!hasItems && (
                <p style={{ fontSize: 13, color: "#6b7280" }}>
                  Hãy thêm sản phẩm vào giỏ để bắt đầu mua hàng.
                </p>
              )}
            </aside>
          </div>
        )}
      </div>
    </main>
  );
};

export default StorefrontCartPage;
