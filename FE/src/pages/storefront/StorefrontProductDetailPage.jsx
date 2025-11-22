// src/pages/storefront/StorefrontProductDetailPage.jsx
import React, { useEffect, useState, useCallback, useMemo } from "react";
import { useParams, useLocation, useNavigate, Link } from "react-router-dom";
import StorefrontProductApi from "../../services/storefrontProductService";
import "./StorefrontProductDetailPage.css";

// Format VND
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

// Parse query string
const useQueryParams = () => {
  const { search } = useLocation();
  return useMemo(() => new URLSearchParams(search), [search]);
};

// Map sectionType -> nhãn tiếng Việt
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

const StorefrontProductDetailPage = () => {
  const { productId } = useParams();
  const queryParams = useQueryParams();
  const navigate = useNavigate();

  const initialVariantId = queryParams.get("variant") || "";
  const [currentVariantId, setCurrentVariantId] = useState(initialVariantId);

  const [detail, setDetail] = useState(null);
  const [relatedItems, setRelatedItems] = useState([]);

  const [loadingDetail, setLoadingDetail] = useState(false);
  const [loadingRelated, setLoadingRelated] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    const v = queryParams.get("variant") || "";
    setCurrentVariantId(v);
  }, [queryParams]);

  const loadDetail = useCallback(async (pid, vid) => {
    if (!pid || !vid) return;
    setLoadingDetail(true);
    setError("");
    try {
      const res = await StorefrontProductApi.variantDetail(pid, vid);
      setDetail(res);
    } catch (err) {
      console.error("Load variant detail failed:", err);
      setError("Không tải được thông tin sản phẩm. Vui lòng thử lại sau.");
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

  useEffect(() => {
    if (!productId || !currentVariantId) return;
    loadDetail(productId, currentVariantId);
    loadRelated(productId, currentVariantId);
  }, [productId, currentVariantId, loadDetail, loadRelated]);

  const isOutOfStock = detail?.isOutOfStock;

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

  // Giá tạm thời (demo)
  const priceNow = 295000;
  const priceOld = 1500000;
  const discountPercent = Math.round(100 - (priceNow / priceOld) * 100);

  const handleChangeVariant = (variantId) => {
    if (!variantId || variantId === currentVariantId) return;
    navigate(`/products/${productId}?variant=${variantId}`, { replace: false });
    setCurrentVariantId(variantId);
  };

  const handleBuyNow = () => {
    alert("Tính năng mua ngay sẽ được bổ sung sau.");
  };

  return (
    <main className="sf-detail-page">
      <div className="sf-detail-container">
        {detail && (
          <nav className="sf-breadcrumb">
            <Link to="/">Trang chủ</Link>
            <span>/</span>
            {/* Link về trang danh sách sản phẩm */}
            <Link to="/products">Sản phẩm</Link>

            {/* Link danh mục: về list filter theo categoryId */}
            {primaryCategory && (
              <>
                <span>/</span>
                <span className="sf-breadcrumb-current">
                  {primaryCategory.categoryName}
                  </span>
              </>
            )}

            <span>/</span>
            {/* Tên sản phẩm hiện tại (không cần link) */}
            <span className="sf-breadcrumb-current">
              {detail.productName || "Chi tiết"}
            </span>
          </nav>
        )}

        {/* HERO: ảnh + info */}
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

              {/* Nhãn sản phẩm ở góc trái ảnh (giống list) */}
              {detail?.badges && detail.badges.length > 0 && (
                <div className="sf-media-badges">
                  {detail.badges.map((b) => (
                    <span
                      key={b.badgeCode}
                      className="sf-tag"
                      style={
                        b.colorHex
                          ? { backgroundColor: b.colorHex, color: "#fff" }
                          : undefined
                      }
                    >
                      {b.displayName || b.badgeCode}
                    </span>
                  ))}
                </div>
              )}

              {/* HẾT HÀNG to ở giữa hình */}
              {isOutOfStock && (
                <div className="sf-detail-out-of-stock">Hết hàng</div>
              )}
            </div>
          </div>

          <div className="sf-detail-hero-right">
            {loadingDetail && (
              <div className="sf-detail-loading">Đang tải thông tin...</div>
            )}

            {error && !loadingDetail && (
              <div className="sf-detail-error">{error}</div>
            )}

            {detail && !loadingDetail && !error && (
              <>
                <header className="sf-detail-header">
                  <h1 className="sf-detail-title">{displayTitle}</h1>
                </header>

                {/* Tình trạng + kho còn (label đậm giống meta khác) */}
                <div className="sf-detail-meta-row">
                  <div className="sf-detail-status">
                    <span className="sf-detail-meta-label">Tình trạng:</span>{" "}
                    {isOutOfStock ? (
                      <strong className="sf-text-danger">Hết hàng</strong>
                    ) : (
                      <strong className="sf-text-success">Còn hàng</strong>
                    )}
                  </div>
                  {!isOutOfStock && (
                    <div className="sf-detail-stock">
                      <span className="sf-detail-meta-label">Kho còn:</span>{" "}
                      <span className="sf-detail-meta-value">
                        <strong>{detail.stockQty ?? 0}</strong> sản phẩm
                      </span>
                    </div>
                  )}
                </div>

                {/* Danh mục, loại sản phẩm */}
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

                {/* Giá */}
                <div className="sf-detail-price-block">
                  <div className="sf-detail-price-main">
                    <div className="sf-detail-price-now">
                      {formatCurrency(priceNow)}
                    </div>
                    <div className="sf-detail-price-old">
                      {formatCurrency(priceOld)}
                    </div>
                    <div className="sf-detail-price-off">
                      -{discountPercent}%
                    </div>
                  </div>
                </div>

                {/* Tuỳ chọn gói */}
                {detail.siblingVariants && detail.siblingVariants.length > 1 && (
                  <div className="sf-detail-variants">
                    <div className="sf-detail-variants-label">
                      Tuỳ chọn gói sản phẩm:
                    </div>
                    <div className="sf-detail-variants-list">
                      {detail.siblingVariants.map((sv) => {
                        const active = sv.variantId === currentVariantId;
                        const svOut = sv.isOutOfStock;
                        return (
                          <button
                            key={sv.variantId}
                            type="button"
                            className={`sf-variant-chip ${
                              active ? "sf-variant-chip-active" : ""
                            } ${svOut ? "sf-variant-chip-out" : ""}`}
                            onClick={() => handleChangeVariant(sv.variantId)}
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

                {/* Nút mua */}
                <div className="sf-detail-actions">
                  <button
                    type="button"
                    className="sf-btn sf-btn-primary sf-btn-lg"
                    onClick={handleBuyNow}
                    disabled={isOutOfStock}
                  >
                    Mua ngay
                  </button>
                  <button
                    type="button"
                    className="sf-btn sf-btn-outline sf-btn-lg"
                    onClick={() =>
                      alert("Tính năng thêm vào giỏ sẽ được bổ sung sau.")
                    }
                    disabled={isOutOfStock}
                  >
                    Thêm vào giỏ
                  </button>
                </div>
              </>
            )}
          </div>
        </section>

        {/* SECTIONS + FAQ */}
        <section className="sf-detail-body">
          <div className="sf-detail-main">
            {detail?.sections && detail.sections.length > 0 && (
              <div className="sf-detail-block">
                <h2 className="sf-detail-block-title">
                  Thông tin chi tiết sản phẩm
                </h2>
                <div className="sf-detail-sections">
                  {detail.sections.map((s) => (
                    <article
                      key={s.sectionId}
                      className="sf-detail-section-row"
                    >
                      <div className="sf-detail-section-type">
                        {getSectionTypeLabel(s.sectionType)}
                      </div>
                      <div
                        className="sf-detail-section-content"
                        dangerouslySetInnerHTML={{ __html: s.content }}
                      />
                    </article>
                  ))}
                </div>
              </div>
            )}

           {detail?.faqs && detail.faqs.length > 0 && (
  <div className="sf-detail-block">
    <div className="sf-detail-section-row sf-detail-faq-row">
      {/* Cột trái: tiêu đề khối */}
      <div className="sf-detail-section-type">
        Câu hỏi thường gặp (FAQ)
      </div>

      {/* Cột phải: danh sách câu hỏi & trả lời */}
      <div className="sf-detail-faq-list">
        {detail.faqs.map((f) => (
          <div key={f.faqId} className="sf-detail-faq-item">
            <div className="sf-detail-faq-question">{f.question}</div>
            <div
              className="sf-detail-faq-answer"
              dangerouslySetInnerHTML={{ __html: f.answer }}
            />
          </div>
        ))}
      </div>
    </div>
  </div>
)}
          </div>
        </section>

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
            <div className="sf-detail-empty">Chưa có sản phẩm liên quan.</div>
          )}

          {!loadingRelated && relatedItems.length > 0 && (
            <div className="sf-grid sf-grid-responsive sf-related-grid">
              {relatedItems.map((item) => {
                const typeLabelCard = StorefrontProductApi.typeLabelOf(
                  item.productType
                );
                const variantTitle = item.variantTitle || item.title;
                const displayTitleCard = typeLabelCard
                  ? `${variantTitle} - ${typeLabelCard}`
                  : variantTitle;

                const priceNowText = formatCurrency(priceNow);
                const priceOldText = formatCurrency(priceOld);

                const isCardOut =
                  item.isOutOfStock ?? item.status === "OUT_OF_STOCK";

                return (
                  <article
                    key={item.variantId}
                    className={`sf-card ${isCardOut ? "sf-card-out" : ""}`}
                  >
                    <Link
                      className="sf-card-link"
                      to={`/products/${item.productId}?variant=${item.variantId}`}
                    >
                      <div className="sf-media">
                        {item.thumbnail ? (
                          <img src={item.thumbnail} alt={displayTitleCard} />
                        ) : (
                          <div className="sf-media-placeholder">
                            {displayTitleCard?.[0] || "K"}
                          </div>
                        )}

                        {/* Badge biến thể ở góc ảnh */}
                        {item.badges && item.badges.length > 0 && (
                          <div className="sf-media-badges">
                            {item.badges.map((b) => (
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

                        {/* HẾT HÀNG ở giữa hình */}
                        {isCardOut && (
                          <div className="sf-out-of-stock">Hết hàng</div>
                        )}
                      </div>

                      <div className="sf-body">
                        <h3>{displayTitleCard}</h3>

                        <div className="sf-price">
                          <div className="sf-price-now">{priceNowText}</div>
                          <div className="sf-price-old">{priceOldText}</div>
                          <div className="sf-price-off">
                            -{discountPercent}%
                          </div>
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
