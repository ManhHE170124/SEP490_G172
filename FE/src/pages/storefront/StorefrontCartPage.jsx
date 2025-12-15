// File: src/pages/storefront/StorefrontCartPage.jsx
import React, { useEffect, useState, useCallback, useMemo } from "react";
import { Link, useNavigate } from "react-router-dom";
import StorefrontCartApi from "../../services/storefrontCartService";
import StorefrontProductApi from "../../services/storefrontProductService";
import Toast from "../../components/Toast/Toast";
import ConfirmDialog from "../../components/Toast/ConfirmDialog";
import "./StorefrontCartPage.css";
import GuestCartService from "../../services/guestCartService";

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

const CHECKOUT_LOCK_KEY = "ktk_checkout_lock";

const readCheckoutLock = () => {
  if (typeof window === "undefined") return null;
  try {
    const raw = window.localStorage.getItem(CHECKOUT_LOCK_KEY);
    if (!raw) return null;

    const lock = JSON.parse(raw);
    const expMs = Date.parse(lock?.expiresAtUtc || "");
    if (!lock?.paymentUrl || !Number.isFinite(expMs)) return null;

    if (Date.now() >= expMs) {
      window.localStorage.removeItem(CHECKOUT_LOCK_KEY);
      return null;
    }

    return lock; // { paymentId, orderId, paymentUrl, expiresAtUtc }
  } catch {
    try {
      window.localStorage.removeItem(CHECKOUT_LOCK_KEY);
    } catch {}
    return null;
  }
};

const saveCheckoutLock = ({ paymentId, orderId, paymentUrl, expiresAtUtc }) => {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(
      CHECKOUT_LOCK_KEY,
      JSON.stringify({ paymentId, orderId, paymentUrl, expiresAtUtc })
    );
  } catch {}
};

const StorefrontCartPage = () => {
  const [cart, setCart] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [updatingItemId, setUpdatingItemId] = useState(null);
  const [updatingEmail, setUpdatingEmail] = useState(false);
  const [localEmail, setLocalEmail] = useState("");
  const [checkingOut, setCheckingOut] = useState(false);

  const [toasts, setToasts] = useState([]);

  const [customer, setCustomer] = useState(() => readCustomerFromStorage());

  useEffect(() => {
    if (typeof window === "undefined") return;
    const syncCustomer = () => setCustomer(readCustomerFromStorage());
    window.addEventListener("storage", syncCustomer);
    return () => window.removeEventListener("storage", syncCustomer);
  }, []);

  const [confirmDialog, setConfirmDialog] = useState({
    isOpen: false,
    title: "",
    message: "",
    action: null,
  });

  const navigate = useNavigate();

  const removeToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const addToast = useCallback(
    (type, title, message) => {
      const id = Date.now() + Math.random();
      setToasts((prev) => [...prev, { id, type, title, message }]);
      setTimeout(() => removeToast(id), 4000);
    },
    [removeToast]
  );

  const closeConfirmDialog = () => {
    setConfirmDialog((prev) => ({ ...prev, isOpen: false, action: null }));
  };

  const handleConfirmDialogConfirm = () => {
    if (confirmDialog.action) confirmDialog.action();
    closeConfirmDialog();
  };

  const loadCart = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      let res;

      if (customer) {
        res = await StorefrontCartApi.getCart();
        const emailFromApi = res.accountEmail || res.receiverEmail || "";
        setLocalEmail(emailFromApi);
      } else {
        res = await GuestCartService.getCart();
        const emailFromApi = res.receiverEmail || "";
        setLocalEmail(emailFromApi);
      }

      setCart(res);

      if (String(res?.status || "").toLowerCase() === "converting") {
        addToast(
          "warning",
          "Giỏ hàng đang được checkout",
          "Giỏ hàng đang được checkout ở tab/phiên khác. Vui lòng đợi vài giây rồi tải lại."
        );
      }
    } catch (err) {
      console.error("Load cart failed:", err);

      if (err?.response?.status === 401 && customer) {
        setError("Bạn cần đăng nhập để sử dụng giỏ hàng.");
      } else {
        setError(err?.message || "Không tải được giỏ hàng. Vui lòng thử lại sau.");
      }
    } finally {
      setLoading(false);
    }
  }, [customer, addToast]);

  useEffect(() => {
    loadCart();
  }, [loadCart]);

  const isCartLocked = useMemo(() => {
    const s = String(cart?.status || "Active").toLowerCase();
    return s !== "active";
  }, [cart]);

  const tryAutoReloadAfterConflict = useCallback(() => {
    setTimeout(() => loadCart(), 800);
  }, [loadCart]);

  const actuallyRemoveItem = async (item) => {
    setUpdatingItemId(item.variantId);
    try {
      const res = customer
        ? await StorefrontCartApi.removeItem(item.variantId)
        : await GuestCartService.removeItem(item.variantId);

      setCart(res);
      addToast(
        "success",
        "Đã xoá sản phẩm",
        `Đã xoá "${item.variantTitle || item.title || item.productName || "sản phẩm"}" khỏi giỏ hàng.`
      );
    } catch (err) {
      console.error("Remove item failed:", err);
      if (err?.response?.status === 409) {
        addToast("warning", "Giỏ hàng đang checkout", err?.message || "Vui lòng thử lại sau.");
        tryAutoReloadAfterConflict();
      } else {
        addToast("error", "Xoá sản phẩm thất bại", err?.message || "Không thể xoá sản phẩm khỏi giỏ.");
      }
    } finally {
      setUpdatingItemId(null);
    }
  };

  const handleRemoveItem = (item) => {
    const itemName = item.variantTitle || item.title || item.productName || "sản phẩm này";
    setConfirmDialog({
      isOpen: true,
      title: "Xoá sản phẩm khỏi giỏ hàng?",
      message: `Bạn có chắc muốn xoá "${itemName}" khỏi giỏ hàng?`,
      action: () => actuallyRemoveItem(item),
    });
  };

  const handleChangeQuantity = async (item, delta) => {
    if (!cart) return;
    const newQty = (item.quantity || 0) + delta;
    if (newQty < 0) return;

    if (newQty === 0) {
      const itemName = item.variantTitle || item.title || item.productName || "sản phẩm này";
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
        : await GuestCartService.updateItem(item.variantId, newQty);

      setCart(res);
    } catch (err) {
      console.error("Update quantity failed:", err);

      if (err?.response?.status === 409) {
        addToast("warning", "Giỏ hàng đang checkout", err?.message || "Vui lòng thử lại sau.");
        tryAutoReloadAfterConflict();
      } else {
        const serverMsg = err?.response?.data?.message;
        const type = err?.response?.status === 400 ? "warning" : "error";
        addToast(type, "Cập nhật số lượng thất bại", serverMsg || err?.message || "Không thể cập nhật số lượng.");
      }
    } finally {
      setUpdatingItemId(null);
    }
  };

  const actuallyClearCart = async () => {
    if (!cart?.items?.length) return;
    try {
      const res = customer ? await StorefrontCartApi.clearCart() : await GuestCartService.clearCart();
      setCart(res);
      addToast("success", "Đã xoá giỏ hàng", "Toàn bộ sản phẩm đã được xoá.");
    } catch (err) {
      console.error("Clear cart failed:", err);

      if (err?.response?.status === 409) {
        addToast("warning", "Giỏ hàng đang checkout", err?.message || "Vui lòng thử lại sau.");
        tryAutoReloadAfterConflict();
      } else {
        addToast("error", "Xoá giỏ hàng thất bại", err?.message || "Không thể xoá giỏ hàng.");
      }
    }
  };

  const handleClearCart = () => {
    if (!cart?.items?.length) return;
    setConfirmDialog({
      isOpen: true,
      title: "Xoá toàn bộ giỏ hàng?",
      message: "Tất cả sản phẩm sẽ bị xoá khỏi giỏ hàng của bạn và không thể hoàn tác.",
      action: () => actuallyClearCart(),
    });
  };

  const handleSaveEmail = async () => {
    if (!localEmail.trim()) {
      addToast("warning", "Thiếu email nhận hàng", "Vui lòng nhập email nhận hàng.");
      return;
    }

    setUpdatingEmail(true);
    try {
      const trimmed = localEmail.trim();
      const res = customer
        ? await StorefrontCartApi.setReceiverEmail(trimmed)
        : await GuestCartService.setReceiverEmail(trimmed);

      setCart(res);
      addToast("success", "Đã lưu email nhận hàng", "Email nhận hàng đã được cập nhật.");
    } catch (err) {
      console.error("Update receiver email failed:", err);
      if (err?.response?.status === 409) {
        addToast("warning", "Giỏ hàng đang checkout", err?.message || "Vui lòng thử lại sau.");
        tryAutoReloadAfterConflict();
      } else {
        addToast("error", "Lưu email nhận hàng thất bại", err?.message || "Không thể lưu email nhận hàng.");
      }
    } finally {
      setUpdatingEmail(false);
    }
  };

  const handleProceedCheckout = async () => {
    if (checkingOut) return;
    if (!cart?.items?.length) return;

    // ✅ Multi-tab lock: nếu tab khác đã checkout và còn hạn => redirect luôn, KHÔNG gọi checkout API
    const existingLock = readCheckoutLock();
    if (existingLock?.paymentUrl) {
      window.location.href = existingLock.paymentUrl;
      return;
    }

    const effectiveEmail = ((customer ? cart.accountEmail : null) || localEmail || "").trim();
    if (!effectiveEmail) {
      addToast("warning", "Thiếu email nhận hàng", "Vui lòng nhập email nhận hàng trước khi thanh toán.");
      return;
    }

    setCheckingOut(true);
    try {
      const checkoutResult = await StorefrontCartApi.checkout({
        deliveryEmail: effectiveEmail,
      });

      setCart(null);
      setLocalEmail(effectiveEmail);

      // ✅ lưu lock cho multi-tab (localStorage)
      if (checkoutResult?.paymentUrl && checkoutResult?.expiresAtUtc) {
        saveCheckoutLock({
          paymentId: checkoutResult.paymentId,
          orderId: checkoutResult.orderId,
          paymentUrl: checkoutResult.paymentUrl,
          expiresAtUtc: checkoutResult.expiresAtUtc,
        });
      }

      // lưu lại để trang return/cancel có thể hiển thị tốt hơn (cùng tab)
      try {
        window.sessionStorage.setItem(
          "ktk_last_checkout",
          JSON.stringify({
            paymentId: checkoutResult.paymentId,
            orderId: checkoutResult.orderId,
            paymentUrl: checkoutResult.paymentUrl,
            expiresAtUtc: checkoutResult.expiresAtUtc,
            at: new Date().toISOString(),
          })
        );
      } catch {}

      if (checkoutResult.paymentUrl) {
        window.location.href = checkoutResult.paymentUrl;
        return;
      }

      addToast(
        "warning",
        "Không nhận được link thanh toán",
        "Phiên thanh toán đã được tạo nhưng thiếu URL. Vui lòng thử lại."
      );
    } catch (err) {
      console.error("Checkout failed:", err);
      const serverMsg = err?.response?.data?.message;

      if (err?.response?.status === 409) {
        addToast("warning", "Giỏ hàng đang checkout", serverMsg || err?.message || "Vui lòng thử lại sau.");
        tryAutoReloadAfterConflict();
      } else {
        addToast("error", "Thanh toán thất bại", serverMsg || err?.message || "Không thể tạo phiên thanh toán.");
      }
    } finally {
      setCheckingOut(false);
    }
  };

  const handleContinueShopping = () => navigate("/products");

  const hasItems = !!cart?.items?.length;

  return (
    <main className="sf-cart-page">
      <div className="toast-container">
        {toasts.map((t) => (
          <Toast key={t.id} toast={t} onRemove={removeToast} />
        ))}
      </div>

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
          <p className="sf-cart-subtitle">Kiểm tra lại sản phẩm trước khi tiến hành thanh toán.</p>
        </header>

        {isCartLocked && (
          <div className="sf-cart-error" style={{ background: "#fff7ed", borderColor: "#fdba74", color: "#9a3412" }}>
            Giỏ hàng đang được checkout ở phiên khác. Bạn chỉ có thể xem, vui lòng đợi rồi{" "}
            <button className="sf-btn sf-btn-outline" type="button" onClick={loadCart} style={{ marginLeft: 8 }}>
              Tải lại
            </button>
          </div>
        )}

        {error && <div className="sf-cart-error">{error}</div>}
        {loading && <div className="sf-cart-loading">Đang tải giỏ hàng...</div>}

        {!loading && !error && (
          <div className="sf-cart-layout">
            <section className="sf-cart-main">
              {!hasItems && (
                <div className="sf-cart-empty">
                  <p>Giỏ hàng của bạn đang trống.</p>
                  <div className="sf-cart-empty-actions">
                    <button type="button" className="sf-btn sf-btn-primary" onClick={handleContinueShopping}>
                      Tiếp tục mua sắm
                    </button>
                  </div>
                </div>
              )}

              {hasItems && (
                <>
                  <ul className="sf-cart-items">
                    {cart.items.map((item) => {
                      const isUpdating = updatingItemId === item.variantId;
                      const disabled = isUpdating || isCartLocked || checkingOut;

                      const variantTitle =
                        item.variantTitle || item.title || item.productName || "Sản phẩm không xác định";

                      let typeLabel = "";
                      try {
                        typeLabel = StorefrontProductApi.typeLabelOf(item.productType);
                      } catch {
                        typeLabel = "";
                      }

                      const displayTitle = typeLabel ? `${variantTitle} - ${typeLabel}` : variantTitle;

                      const unitPrice = item.unitPrice ?? 0;
                      const listPrice = item.listPrice;
                      const showOldPrice = listPrice != null && listPrice > unitPrice;

                      let discountPercent = item.discountPercent ?? 0;
                      if ((!discountPercent || discountPercent <= 0) && showOldPrice && unitPrice > 0) {
                        discountPercent = Math.round(100 - (unitPrice / listPrice) * 100);
                      }

                      return (
                        <li key={item.variantId} className="sf-cart-item">
                          <div className="sf-cart-item-media">
                            {item.thumbnail ? (
                              <img src={item.thumbnail} alt={displayTitle} />
                            ) : (
                              <div className="sf-cart-item-media-placeholder">{displayTitle?.[0] || "K"}</div>
                            )}
                          </div>

                          <div className="sf-cart-item-body">
                            <div className="sf-cart-item-title">{displayTitle}</div>

                            <div className="sf-cart-item-actions">
                              <button
                                type="button"
                                className="sf-btn sf-btn-outline"
                                onClick={() => handleChangeQuantity(item, -1)}
                                disabled={disabled}
                              >
                                -
                              </button>
                              <span className="sf-cart-qty-value">{item.quantity}</span>
                              <button
                                type="button"
                                className="sf-btn sf-btn-outline"
                                onClick={() => handleChangeQuantity(item, 1)}
                                disabled={disabled}
                              >
                                +
                              </button>

                              <button
                                type="button"
                                className="sf-btn sf-btn-outline"
                                onClick={() => handleRemoveItem(item)}
                                disabled={disabled}
                              >
                                Xoá
                              </button>
                            </div>
                          </div>

                          <div className="sf-cart-item-right">
                            <div className="sf-cart-item-price-now">{formatCurrency(unitPrice)}</div>
                            {showOldPrice && <div className="sf-cart-item-price-old">{formatCurrency(listPrice)}</div>}
                            {discountPercent > 0 && (
                              <div className="sf-cart-item-discount-tag">Giảm {discountPercent}%</div>
                            )}
                          </div>
                        </li>
                      );
                    })}
                  </ul>

                  <div className="sf-cart-actions-secondary">
                    <button type="button" className="sf-btn sf-btn-outline" onClick={handleContinueShopping}>
                      &larr; Tiếp tục mua sắm
                    </button>

                    <button
                      type="button"
                      className="sf-btn sf-btn-outline"
                      onClick={handleClearCart}
                      disabled={isCartLocked || checkingOut}
                    >
                      Xoá toàn bộ giỏ hàng
                    </button>
                  </div>
                </>
              )}
            </section>

            <aside className="sf-cart-summary">
              <h2 className="sf-cart-summary-title">Thông tin đơn hàng</h2>

              {hasItems && (
                <>
                  <div className="sf-cart-summary-row">
                    <span className="sf-cart-summary-label">Tổng tiền theo giá gốc</span>
                    <span className="sf-cart-summary-value">{formatCurrency(cart.totalListAmount)}</span>
                  </div>

                  <div className="sf-cart-summary-row sf-cart-summary-discount">
                    <span className="sf-cart-summary-label">Bạn tiết kiệm được</span>
                    <span className="sf-cart-summary-value">-{formatCurrency(cart.totalDiscount)}</span>
                  </div>

                  <div className="sf-cart-summary-row sf-cart-summary-total">
                    <span className="sf-cart-summary-label">Tổng thanh toán</span>
                    <span className="sf-cart-summary-value">{formatCurrency(cart.totalAmount)}</span>
                  </div>

                  <div className="sf-cart-email-block">
                    <div className="sf-cart-email-label">Email nhận hàng (gửi key / tài khoản):</div>

                    {cart.accountEmail ? (
                      <div className="sf-cart-email-readonly">
                        {cart.accountUserName && <div className="sf-cart-account-name">{cart.accountUserName}</div>}
                        <div className="sf-cart-account-email">{cart.accountEmail}</div>
                        <div className="sf-cart-email-note">Email nhận hàng sẽ dùng email của tài khoản này.</div>
                      </div>
                    ) : (
                      <div className="sf-cart-email-input-row">
                        <input
                          type="email"
                          className="sf-cart-email-input"
                          placeholder="nhapemail@domain.com"
                          value={localEmail}
                          onChange={(e) => setLocalEmail(e.target.value)}
                          disabled={isCartLocked || checkingOut}
                        />
                        <button
                          type="button"
                          className="sf-btn sf-btn-outline"
                          onClick={handleSaveEmail}
                          disabled={updatingEmail || isCartLocked || checkingOut}
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
                      disabled={checkingOut || isCartLocked}
                    >
                      {checkingOut ? "Đang chuyển đến cổng thanh toán..." : "Tiến hành thanh toán"}
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
