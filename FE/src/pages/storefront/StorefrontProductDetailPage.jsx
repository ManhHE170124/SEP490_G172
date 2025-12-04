// src/pages/storefront/StorefrontProductDetailPage.jsx
import React, {
  useEffect,
  useState,
  useCallback,
  useMemo,
} from "react";
import {
  useParams,
  useLocation,
  useNavigate,
  Link,
} from "react-router-dom";
import StorefrontProductApi from "../../services/storefrontProductService";
import StorefrontCartApi, {
  CART_UPDATED_EVENT,
} from "../../services/storefrontCartService";
import Toast from "../../components/Toast/Toast";
import "./StorefrontProductDetailPage.css";
import GuestCartService from "../../services/guestCartService";

const formatCurrency = (value) => {
  if (value == null) return "Đang cập nhật";
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

const useQueryParams = () => {
  const { search } = useLocation();
  return useMemo(() => new URLSearchParams(search), [search]);
};

const getSectionTypeLabel = (sectionType) => {
  const key = (sectionType || "").toString().toUpperCase();

  switch (key) {
    case "NOTE":
    case "NOTICE":
      return "Lưu ý khi mua sản phẩm";
    case "DETAIL":
    case "DESCRIPTION":
      return "Chi tiết sản phẩm";
    case "GUIDE":
    case "HOWTO":
      return "Hướng dẫn sử dụng";
    case "WARRANTY":
      return "Chính sách bảo hành";
    default:
      return "Thông tin";
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

const StorefrontProductDetailPage = () => {
  const { productId } = useParams();
  const queryParams = useQueryParams();
  const navigate = useNavigate();

  const currentVariantId = queryParams.get("variant") || "";

  const [detail, setDetail] = useState(null);
  const [relatedItems, setRelatedItems] = useState([]);
  const [quantity, setQuantity] = useState(1);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [loadingRelated, setLoadingRelated] = useState(false);
  const [error, setError] = useState("");
  const [addingToCart, setAddingToCart] = useState(false);

  // Toast state
  const [toasts, setToasts] = useState([]);

  const removeToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const addToast = useCallback(
    (type, title, message) => {
      const id = Date.now() + Math.random();
      const toast = { id, type, title, message };
      setToasts((prev) => [...prev, toast]);
      setTimeout(() => removeToast(id), 4000);
    },
    [removeToast]
  );

  const [customer, setCustomer] = useState(() =>
    readCustomerFromStorage()
  );

  useEffect(() => {
    if (typeof window === "undefined") return;

    const syncCustomer = () => {
      setCustomer(readCustomerFromStorage());
    };

    window.addEventListener("storage", syncCustomer);
    return () => window.removeEventListener("storage", syncCustomer);
  }, []);

  // ====== Load detail & related ======
  const loadDetail = useCallback(async (pid, vid) => {
    if (!pid || !vid) return;
    setLoadingDetail(true);
    setError("");
    try {
      const res = await StorefrontProductApi.variantDetail(pid, vid);
      setDetail(res);
      // Reset quantity về 1 khi đổi biến thể hoặc khi reload detail
      setQuantity(1);
    } catch (err) {
      console.error("Load variant detail failed:", err);
      setError(
        "Không tải được thông tin sản phẩm. Vui lòng thử lại sau."
      );
    } finally {
      setLoadingDetail(false);
    }
  }, []);

  const loadRelated = useCallback(async (pid, vid) => {
    if (!pid || !vid) {
      setRelatedItems([]);
      return;
    }
    setLoadingRelated(true);
    try {
      const res = await StorefrontProductApi.relatedVariants(pid, vid);
      setRelatedItems(res || []);
    } catch (err) {
      console.error("Load related variants failed:", err);
      setRelatedItems([]);
    } finally {
      setLoadingRelated(false);
    }
  }, []);

  // Lần đầu & khi đổi variant
  useEffect(() => {
    if (!productId || !currentVariantId) return;
    loadDetail(productId, currentVariantId);
    loadRelated(productId, currentVariantId);
  }, [productId, currentVariantId, loadDetail, loadRelated]);

  // Khi giỏ hàng thay đổi (Add/Update/Remove/Clear) -> reload lại detail
  useEffect(() => {
    if (typeof window === "undefined") return;

    const handleCartUpdated = () => {
      if (productId && currentVariantId) {
        loadDetail(productId, currentVariantId);
      }
    };

    window.addEventListener(CART_UPDATED_EVENT, handleCartUpdated);
    return () => {
      window.removeEventListener(
        CART_UPDATED_EVENT,
        handleCartUpdated
      );
    };
  }, [productId, currentVariantId, loadDetail]);

  // ====== Quantity control ======
  const handleIncreaseQuantity = () => {
    if (!detail) {
      setQuantity((prevQuantity) => prevQuantity + 1);
      return;
    }

    const max = detail.stockQty ?? 0;

    setQuantity((prevQuantity) => {
      const next = prevQuantity + 1;

      if (max > 0 && next > max) {
        addToast(
          "warning",
          "Vượt số lượng tồn kho",
          `Chỉ còn ${max} sản phẩm trong kho.`
        );
        return prevQuantity;
      }

      return next;
    });
  };

  const handleDecreaseQuantity = () => {
    if (quantity > 1) {
      setQuantity((prevQuantity) => prevQuantity - 1);
    }
  };

  // ==== Tính trạng thái hết hàng (kết hợp status + stockQty) ====
const isOutOfStock = detail
  ? !!(
      detail.isOutOfStock ||
      (detail.status || "").toString().toUpperCase() === "OUT_OF_STOCK"
    )
  : false;

  const typeLabel = detail
    ? StorefrontProductApi.typeLabelOf(detail.productType)
    : "";

  const displayTitle = detail
    ? (detail.variantTitle || detail.title || detail.productName) +
      (typeLabel ? ` - ${typeLabel}` : "")
    : "";

  const categoryNames = detail?.categories
    ? detail.categories.map((c) => c.categoryName).join(", ")
    : "";

  const primaryCategory =
    detail?.categories && detail.categories.length > 0
      ? detail.categories[0]
      : null;

  const sellPrice = detail?.sellPrice;
  const listPrice = detail?.listPrice;

  const priceNowText = formatCurrency(sellPrice);
  const hasOldPrice = listPrice != null && listPrice > sellPrice;
  const priceOldText = hasOldPrice ? formatCurrency(listPrice) : null;
  const discountPercent = hasOldPrice
    ? Math.round(100 - (sellPrice / listPrice) * 100)
    : null;

  const handleChangeVariant = (variantId) => {
    if (!variantId || variantId === currentVariantId) return;
    navigate(`/products/${productId}?variant=${variantId}`, {
      replace: false,
    });
  };

   // ====== Add to cart / Buy now ======
  const handleAddToCart = async () => {
    if (!detail || !detail.variantId) return;
    if (quantity <= 0) return;

    // Nếu đã hết hàng -> chặn luôn
    if (isOutOfStock) {
      addToast(
        "warning",
        "Sản phẩm đã hết hàng",
        "Hiện tại sản phẩm này đã hết hàng. Vui lòng chọn sản phẩm khác hoặc quay lại sau."
      );
      return;
    }

    setAddingToCart(true);
    try {
      if (customer) {
        // Đã đăng nhập -> dùng cart server-side (userId)
        await StorefrontCartApi.addItem({
          variantId: detail.variantId,
          quantity,
        });
      } else {
        // Guest -> dùng cart server-side qua cookie ẩn ktk_anon_cart
        await GuestCartService.addItem({
          variantId: detail.variantId,
          quantity,
        });
      }

      addToast(
        "success",
        "Đã thêm vào giỏ hàng",
        "Sản phẩm đã được thêm vào giỏ hàng của bạn."
      );
    } catch (err) {
      console.error("Thêm vào giỏ thất bại:", err);
      const status = err?.response?.status;
      const serverMsg = err?.response?.data?.message;
      const msg =
        status === 401
          ? "Vui lòng đăng nhập để sử dụng giỏ hàng."
          : serverMsg || "Không thể thêm vào giỏ. Vui lòng thử lại.";
      const type = status === 400 ? "warning" : "error";
      addToast(type, "Thêm vào giỏ thất bại", msg);
    } finally {
      setAddingToCart(false);
    }
  };

   const handleBuyNow = async () => {
    if (!detail || !detail.variantId) return;
    if (quantity <= 0) return;

    // Nếu đã hết hàng -> chặn luôn
    if (isOutOfStock) {
      addToast(
        "warning",
        "Sản phẩm đã hết hàng",
        "Hiện tại sản phẩm này đã hết hàng. Vui lòng chọn sản phẩm khác hoặc quay lại sau."
      );
      return;
    }

    setAddingToCart(true);
    try {
      if (customer) {
        await StorefrontCartApi.addItem({
          variantId: detail.variantId,
          quantity,
        });
      } else {
        await GuestCartService.addItem({
          variantId: detail.variantId,
          quantity,
        });
      }

      // Cart đã được cập nhật & bắn event, detail sẽ tự reload.
      navigate("/cart");
    } catch (err) {
      console.error("Mua ngay thất bại:", err);
      const status = err?.response?.status;
      const serverMsg = err?.response?.data?.message;
      const msg =
        status === 401
          ? "Vui lòng đăng nhập để sử dụng giỏ hàng."
          : serverMsg ||
            "Không thể xử lý mua ngay. Vui lòng thử lại.";
      const type = status === 400 ? "warning" : "error";
      addToast(type, "Mua ngay thất bại", msg);
    } finally {
      setAddingToCart(false);
    }
  };


  // Chuẩn hoá dữ liệu sections & FAQ cho an toàn
  const sections = detail?.sections || detail?.detailSections || [];
  const faqs =
    detail?.faqs || detail?.faqItems || detail?.faqList || [];

  return (
    <main className="sf-detail-page">
      {/* Toast stack */}
      <div className="toast-container">
        {toasts.map((t) => (
          <Toast key={t.id} toast={t} onRemove={removeToast} />
        ))}
      </div>

      <div className="sf-detail-container">
        {detail && (
          <nav className="sf-breadcrumb">
            <Link to="/">Trang chủ</Link>
            <span>/</span>
            <Link to="/products">Sản phẩm</Link>

            {primaryCategory && (
              <>
                <span>/</span>
                <span className="sf-breadcrumb-current">
                  {primaryCategory.categoryName}
                </span>
              </>
            )}

            <span>/</span>
            <span className="sf-breadcrumb-current">
              {detail.productName || "Chi tiết"}
            </span>
          </nav>
        )}

        {/* HERO: ảnh + thông tin chính + nút mua */}
        <section className="sf-detail-hero">
          <div className="sf-detail-hero-left">
            <div
              className={`sf-detail-media ${
                isOutOfStock ? "sf-detail-media-out" : ""
              }`}
            >
              {detail?.thumbnail ? (
                <img src={detail.thumbnail} alt={displayTitle} />
              ) : (
                <div className="sf-detail-media-placeholder">
                  {displayTitle?.[0] || "K"}
                </div>
              )}

              {detail?.badges && detail.badges.length > 0 && (
                <div className="sf-media-badges">
                  {detail.badges.map((b) => (
                    <span
                      key={b.badgeCode}
                      className="sf-tag"
                      style={
                        b.colorHex
                          ? {
                              backgroundColor: b.colorHex,
                              color: "#fff",
                            }
                          : undefined
                      }
                    >
                      {b.displayName || b.badgeCode}
                    </span>
                  ))}
                </div>
              )}

              {isOutOfStock && (
                <div className="sf-detail-out-of-stock">Hết hàng</div>
              )}
            </div>
          </div>

          <div className="sf-detail-hero-right">
            {loadingDetail && (
              <div className="sf-detail-loading">
                Đang tải thông tin...
              </div>
            )}

            {error && !loadingDetail && (
              <div className="sf-detail-error">{error}</div>
            )}

            {detail && !loadingDetail && !error && (
              <>
                <header className="sf-detail-header">
                  <h1 className="sf-detail-title">{displayTitle}</h1>
                </header>

                <div className="sf-detail-meta-row">
                  <div className="sf-detail-status">
                    <span className="sf-detail-meta-label">
                      Tình trạng:
                    </span>{" "}
                    {isOutOfStock ? (
                      <strong className="sf-text-danger">
                        Hết hàng
                      </strong>
                    ) : (
                      <strong className="sf-text-success">
                        Còn hàng
                      </strong>
                    )}
                  </div>
                  {!isOutOfStock && (
                    <div className="sf-detail-stock">
                      <span className="sf-detail-meta-label">
                        Kho còn:
                      </span>{" "}
                      <span className="sf-detail-meta-value">
                        <strong>{detail.stockQty ?? 0}</strong> sản
                        phẩm
                      </span>
                    </div>
                  )}
                </div>

                <div className="sf-detail-meta-list">
                  {categoryNames && (
                    <div className="sf-detail-meta-item">
                      <span className="sf-detail-meta-label">
                        Danh mục sản phẩm:
                      </span>
                      <span className="sf-detail-meta-value">
                        {categoryNames}
                      </span>
                    </div>
                  )}

                  <div className="sf-detail-meta-item">
                    <span className="sf-detail-meta-label">
                      Loại sản phẩm:
                    </span>
                    <span className="sf-detail-meta-value">
                      {typeLabel || detail.productType || "—"}
                    </span>
                  </div>
                </div>

                <div className="sf-detail-price-block">
                  <div className="sf-detail-price-main">
                    <div className="sf-detail-price-now">
                      {priceNowText}
                    </div>
                    {hasOldPrice && (
                      <>
                        <div className="sf-detail-price-old">
                          {priceOldText}
                        </div>
                        {discountPercent > 0 && (
                          <div className="sf-detail-price-off">
                            -{discountPercent}%
                          </div>
                        )}
                      </>
                    )}
                  </div>
                </div>

                {detail.siblingVariants &&
                  detail.siblingVariants.length > 1 && (
                    <div className="sf-detail-variants">
                      <div className="sf-detail-variants-label">
                        Tuỳ chọn gói sản phẩm:
                      </div>
                      <div className="sf-detail-variants-list">
                        {detail.siblingVariants.map((sv) => {
                          const active =
                            sv.variantId === currentVariantId;
                          // Nếu là biến thể hiện tại và detail đang out-of-stock
                          // thì ép chip này cũng "Hết hàng"
                          const svOut =
                            sv.isOutOfStock ||
                            (sv.variantId === detail.variantId &&
                              isOutOfStock);
                          return (
                            <button
                              key={sv.variantId}
                              type="button"
                              className={`sf-variant-chip ${
                                active ? "sf-variant-chip-active" : ""
                              } ${
                                svOut ? "sf-variant-chip-out" : ""
                              }`}
                              onClick={() =>
                                handleChangeVariant(sv.variantId)
                              }
                              disabled={active}
                            >
                              <span className="sf-variant-chip-title">
                                {sv.title}
                              </span>
                              {svOut && (
                                <span className="sf-variant-chip-status">
                                  Hết hàng
                                </span>
                              )}
                            </button>
                          );
                        })}
                      </div>
                    </div>
                  )}

                <div className="sf-quantity-control">
                  <span className="sf-detail-meta-label">
                    Số lượng:
                  </span>
                  <button
                    type="button"
                    className="sf-btn sf-btn-outline sf-btn-lg"
                    onClick={handleDecreaseQuantity}
                  >
                    -
                  </button>
                  <span className="sf-quantity">{quantity}</span>
                  <button
                    type="button"
                    className="sf-btn sf-btn-outline sf-btn-lg"
                    onClick={handleIncreaseQuantity}
                  >
                    +
                  </button>
                </div>

                <div className="sf-detail-actions">
                  <button
                    type="button"
                    className="sf-btn sf-btn-primary sf-btn-lg"
                    onClick={handleBuyNow}
                    disabled={isOutOfStock || addingToCart}
                  >
                    Mua ngay
                  </button>

                  <button
                    type="button"
                    className="sf-btn sf-btn-outline sf-btn-lg"
                    onClick={handleAddToCart}
                    disabled={isOutOfStock || addingToCart}
                  >
                    Thêm vào giỏ
                  </button>
                </div>
              </>
            )}
          </div>
        </section>

        {/* BODY: Sections mô tả / hướng dẫn / bảo hành + FAQ */}
        {detail && (
          <section className="sf-detail-body">
            <div className="sf-detail-main">
              {/* Các section nội dung */}
              {sections && sections.length > 0 && (
                <article className="sf-detail-block">
                  <h2 className="sf-detail-block-title">
                    Thông tin sản phẩm
                  </h2>
                  <div className="sf-detail-sections">
                    {sections.map((s, idx) => {
                      const sectionType =
                        s.sectionType || s.type || s.kind || "";
                      const title =
                        getSectionTypeLabel(sectionType);
                      const html =
                        s.contentHtml ||
                        s.htmlContent ||
                        s.content ||
                        "";

                      return (
                        <div
                          key={s.sectionId || s.id || idx}
                          className="sf-detail-section-row"
                        >
                          <div className="sf-detail-section-type">
                            {title}
                          </div>
                          <div
                            className="sf-detail-section-content"
                            dangerouslySetInnerHTML={{ __html: html }}
                          />
                        </div>
                      );
                    })}
                  </div>
                </article>
              )}

              {/* FAQ */}
              {faqs && faqs.length > 0 && (
                <article className="sf-detail-block">
                  <h2 className="sf-detail-block-title">
                    Câu hỏi thường gặp
                  </h2>
                  <div className="sf-detail-faq-list">
                    {faqs.map((f, idx) => {
                      const question =
                        f.question || f.title || "";
                      const answerHtml =
                        f.answerHtml ||
                        f.htmlAnswer ||
                        f.answer ||
                        "";

                      return (
                        <details
                          key={f.faqId || f.id || idx}
                          className="sf-detail-faq-item"
                        >
                          <summary className="sf-detail-faq-question">
                            {question}
                          </summary>
                          <div
                            className="sf-detail-faq-answer"
                            dangerouslySetInnerHTML={{
                              __html: answerHtml,
                            }}
                          />
                        </details>
                      );
                    })}
                  </div>
                </article>
              )}
            </div>
          </section>
        )}

        {/* Sản phẩm liên quan */}
        <section className="sf-detail-related">
          <div className="sf-section-header">
            <h2>Sản phẩm liên quan</h2>
            <p>Có thể bạn cũng quan tâm</p>
          </div>

          {loadingRelated && (
            <div className="sf-detail-loading">
              Đang tải sản phẩm liên quan…
            </div>
          )}

          {!loadingRelated && relatedItems.length === 0 && (
            <div className="sf-detail-empty">
              Chưa có sản phẩm liên quan.
            </div>
          )}

          {!loadingRelated && relatedItems.length > 0 && (
            <div className="sf-grid sf-grid-responsive sf-related-grid">
              {relatedItems.map((item) => {
                const typeLabelCard =
                  StorefrontProductApi.typeLabelOf(
                    item.productType
                  );
                const variantTitle =
                  item.variantTitle || item.title;
                const displayTitleCard = typeLabelCard
                  ? `${variantTitle} - ${typeLabelCard}`
                  : variantTitle;

                const sellPriceCard = item.sellPrice;
                const listPriceCard = item.listPrice;

                const priceNowTextCard =
                  formatCurrency(sellPriceCard);
                const hasOldPriceCard =
                  listPriceCard != null &&
                  listPriceCard > sellPriceCard;

                const priceOldTextCard = hasOldPriceCard
                  ? formatCurrency(listPriceCard)
                  : null;

                const discountPercentCard = hasOldPriceCard
                  ? Math.round(
                      100 - (sellPriceCard / listPriceCard) * 100
                    )
                  : null;

                const isCardOut =
                  item.isOutOfStock ??
                  item.status === "OUT_OF_STOCK";

                return (
                  <article
                    key={item.variantId}
                    className={`sf-card ${
                      isCardOut ? "sf-card-out" : ""
                    }`}
                  >
                    <Link
                      className="sf-card-link"
                      to={`/products/${item.productId}?variant=${item.variantId}`}
                    >
                      <div className="sf-media">
                        {item.thumbnail ? (
                          <img
                            src={item.thumbnail}
                            alt={displayTitleCard}
                          />
                        ) : (
                          <div className="sf-media-placeholder">
                            {displayTitleCard?.[0] || "K"}
                          </div>
                        )}

                        {item.badges &&
                          item.badges.length > 0 && (
                            <div className="sf-media-badges">
                              {item.badges.map((b) => (
                                <span
                                  key={b.badgeCode}
                                  className="sf-tag"
                                  style={
                                    b.colorHex
                                      ? {
                                          backgroundColor:
                                            b.colorHex,
                                          color: "#fff",
                                        }
                                      : undefined
                                  }
                                >
                                  {b.displayName || b.badgeCode}
                                </span>
                              ))}
                            </div>
                          )}

                        {isCardOut && (
                          <div className="sf-out-of-stock">
                            Hết hàng
                          </div>
                        )}
                      </div>

                      <div className="sf-body">
                        <h3>{displayTitleCard}</h3>

                        <div className="sf-price">
                          <div className="sf-price-now">
                            {priceNowTextCard}
                          </div>
                          {hasOldPriceCard && (
                            <>
                              <div className="sf-price-old">
                                {priceOldTextCard}
                              </div>
                              {discountPercentCard > 0 && (
                                <div className="sf-price-off">
                                  -{discountPercentCard}%
                                </div>
                              )}
                            </>
                          )}
                        </div>
                      </div>
                    </Link>
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </div>
    </main>
  );
};

export default StorefrontProductDetailPage;
