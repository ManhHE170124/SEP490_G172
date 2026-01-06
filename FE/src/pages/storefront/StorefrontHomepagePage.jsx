// src/pages/storefront/StorefrontHomepagePage.jsx
import React, { useEffect, useState, useCallback, useMemo } from "react";
import { Link, useNavigate } from "react-router-dom";
import StorefrontHomepageApi from "../../services/storefrontHomepageService";
import StorefrontProductApi from "../../services/storefrontProductService";
import { CART_UPDATED_EVENT } from "../../services/storefrontCartService";
import storefrontBannerService from "../../services/storefrontBannerService";

import "./StorefrontProductListPage.css";
import "./StorefrontHomepagePage.css";

const DEFAULT_MAIN_SLIDES = [
  {
    id: "main-1",
    title: "Key chính hãng — Kích hoạt trong 1 phút",
    subtitle:
      "Windows, Office, Adobe, tài khoản AI… bảo hành rõ ràng & hỗ trợ từ xa.",
    badge: "Giảm đến 70%",
    params: { q: "Windows", sort: "default" },
  },
  {
    id: "main-2",
    title: "Flash Sale hôm nay",
    subtitle: "Săn deal nóng, số lượng có hạn. Hết là thôi!",
    badge: "Flash Sale",
    params: { sort: "updated" },
  },
  {
    id: "main-3",
    title: "Combo Office + Windows tiết kiệm",
    subtitle:
      "Mua combo kích hoạt vĩnh viễn, tối ưu chi phí cho học tập & làm việc.",
    badge: "Combo tiết kiệm",
    params: { q: "Office", sort: "price-asc" },
  },
];

const DEFAULT_SIDE_SLIDES = [
  {
    id: "side-1",
    title: "Dùng thử AI, ChatGPT, Copilot",
    subtitle: "Gói theo tháng, theo team, thanh toán linh hoạt.",
    params: { q: "ChatGPT", sort: "default" },
  },
  {
    id: "side-2",
    title: "Tài khoản giải trí / Steam",
    subtitle: "Game bản quyền, Netflix, Spotify, YouTube Premium…",
    params: { q: "Steam", sort: "default" },
  },
];

const PRICE_FILTERS = [
  // NOTE: Đây là "giá tối đa" (<=), không phải giá tối thiểu.
  { label: "20.000đ", maxPrice: 20000 },
  { label: "50.000đ", maxPrice: 50000 },
  { label: "100.000đ", maxPrice: 100000 },
  { label: "200.000đ", maxPrice: 200000 },
  { label: "500.000đ", maxPrice: 500000 },
  { label: "1.000.000đ", maxPrice: 1000000 },
];

const formatCurrency = (value) => {
  if (value == null) return "Đang cập nhật";
  return value.toLocaleString("vi-VN") + "đ";
};

const isExternalUrl = (url) => /^https?:\/\//i.test(url || "");
const hasText = (b) => !!(b?.title?.trim() || b?.subtitle?.trim());

const normalizeInternalUrl = (url) => {
  if (!url) return url;
  try {
    if (isExternalUrl(url)) {
      const u = new URL(url);
      if (typeof window !== "undefined" && u.origin === window.location.origin) {
        return `${u.pathname}${u.search}${u.hash}`;
      }
    }
  } catch {}
  return url;
};

const StorefrontHomepagePage = () => {
  const navigate = useNavigate();

  const [mainSlides, setMainSlides] = useState(DEFAULT_MAIN_SLIDES);
  const [sideSlides, setSideSlides] = useState(DEFAULT_SIDE_SLIDES);
  const [loadingBanners, setLoadingBanners] = useState(false);

  const [mainSlideIndex, setMainSlideIndex] = useState(0);
  const [isHoverMain, setIsHoverMain] = useState(false);

  const [products, setProducts] = useState({
    todayBestDeals: [],
    bestSellers: [],
    weeklyTrends: [],
    newlyUpdated: [],
    lowStock: [],
  });

  const [loadingProducts, setLoadingProducts] = useState(false);
  const [errorProducts, setErrorProducts] = useState("");

  useEffect(() => {
    if ((mainSlides?.length || 0) <= 1) return;
    if (isHoverMain) return;

    const timer = setInterval(() => {
      setMainSlideIndex((prev) => (prev + 1) % mainSlides.length);
    }, 6000);

    return () => clearInterval(timer);
  }, [mainSlides, isHoverMain]);

  useEffect(() => {
    if (mainSlideIndex >= (mainSlides?.length || 0)) setMainSlideIndex(0);
  }, [mainSlides, mainSlideIndex]);

  const loadHomepageBanners = useCallback(async () => {
    setLoadingBanners(true);
    try {
      const [main, side] = await Promise.all([
        storefrontBannerService.getPublicByPlacement("HOME_MAIN"),
        storefrontBannerService.getPublicByPlacement("HOME_SIDE"),
      ]);

      const mainList = Array.isArray(main) ? main : main?.items || [];
      const mappedMain = mainList
        .filter((x) => x?.isActive !== false)
        .sort((a, b) => (a?.sortOrder ?? 0) - (b?.sortOrder ?? 0))
        .map((x) => ({
          id: `main-${x.id}`,
          title: x.title ?? "",
          subtitle: x.subtitle ?? "",
          badge: x.badge ?? "",
          mediaUrl: x.mediaUrl,
          mediaType: x.mediaType,
          linkUrl: x.linkUrl,
          linkTarget: x.linkTarget,
        }));

      const sideList = Array.isArray(side) ? side : side?.items || [];
      const actives = sideList
        .filter((x) => x?.isActive !== false)
        .sort((a, b) => (a?.sortOrder ?? 0) - (b?.sortOrder ?? 0));

      const pickSide = (idx, fallback) => {
        const one = actives[idx];
        if (!one) return fallback;
        return {
          id: fallback.id,
          title: one.title ?? "",
          subtitle: one.subtitle ?? "",
          mediaUrl: one.mediaUrl,
          mediaType: one.mediaType,
          linkUrl: one.linkUrl,
          linkTarget: one.linkTarget,
        };
      };

      const mappedSide = [
        pickSide(0, DEFAULT_SIDE_SLIDES[0]),
        pickSide(1, DEFAULT_SIDE_SLIDES[1]),
      ];

      if (mappedMain.length > 0) setMainSlides(mappedMain);
      setSideSlides(mappedSide);
    } catch (err) {
      console.error("Load homepage banners failed:", err);
    } finally {
      setLoadingBanners(false);
    }
  }, []);

  useEffect(() => {
    loadHomepageBanners();
  }, [loadHomepageBanners]);

  const loadHomepageProducts = useCallback(async () => {
    setLoadingProducts(true);
    setErrorProducts("");
    try {
      const res = await StorefrontHomepageApi.products();
      setProducts(res);
    } catch (err) {
      console.error("Load homepage products failed:", err);
      setErrorProducts("Không tải được danh sách sản phẩm. Vui lòng thử lại sau.");
    } finally {
      setLoadingProducts(false);
    }
  }, []);

  useEffect(() => {
    loadHomepageProducts();
  }, [loadHomepageProducts]);

  useEffect(() => {
    if (typeof window === "undefined") return;
    const handleCartUpdated = () => loadHomepageProducts();
    window.addEventListener(CART_UPDATED_EVENT, handleCartUpdated);
    return () =>
      window.removeEventListener(CART_UPDATED_EVENT, handleCartUpdated);
  }, [loadHomepageProducts]);

  const goToProductList = (params = {}) => {
    const sp = new URLSearchParams();
    if (params.q) sp.set("q", params.q);
    if (params.categoryId) sp.set("categoryId", String(params.categoryId));
    if (params.productType) sp.set("productType", params.productType);
    if (params.minPrice != null) sp.set("minPrice", String(params.minPrice));
    if (params.maxPrice != null) sp.set("maxPrice", String(params.maxPrice));
    if (params.sort && params.sort !== "default") sp.set("sort", params.sort);
    const search = sp.toString();
    navigate(`/products${search ? `?${search}` : ""}`);
  };

  // NOTE: Price filter trên homepage là "giá tối đa" (<=)
  const handlePriceFilterClick = (maxPrice) =>
    goToProductList({ maxPrice, sort: "price-asc" });

  const openBannerLink = (banner) => {
    let url = banner?.linkUrl;
    const target = banner?.linkTarget || "_self";
    if (!url) return;

    url = normalizeInternalUrl(url);

    if (target === "_blank") {
      window.open(url, "_blank", "noopener,noreferrer");
      return;
    }

    if (isExternalUrl(url)) {
      window.location.href = url;
      return;
    }

    navigate(url);
  };

  const handleClickMainSlide = (slide) => {
    if (slide?.linkUrl) return openBannerLink(slide);
    if (slide?.params) return goToProductList(slide.params);
    goToProductList({});
  };

  const handleClickSideSlide = (slide) => {
    if (slide?.linkUrl) return openBannerLink(slide);
    if (slide?.params) return goToProductList(slide.params);
    goToProductList({});
  };

  const activeMainSlide = useMemo(
    () => mainSlides?.[mainSlideIndex] || {},
    [mainSlides, mainSlideIndex]
  );

  const canSlide = (mainSlides?.length || 0) > 1;

  const goPrev = (e) => {
    e?.stopPropagation?.();
    if (!canSlide) return;
    setMainSlideIndex(
      (prev) => (prev - 1 + mainSlides.length) % mainSlides.length
    );
  };

  const goNext = (e) => {
    e?.stopPropagation?.();
    if (!canSlide) return;
    setMainSlideIndex((prev) => (prev + 1) % mainSlides.length);
  };

  // NOTE: sort keys dựa trên StorefrontProductsController hiện tại.
  // - sold: (đang là best-seller; BE list sẽ được bạn chỉnh lại sold theo đơn thành công sau)
  // - views: sort fallback theo ViewCount desc
  // - updated: UpdatedAt/CreatedAt desc
const handleViewAllTodayDeals = () => goToProductList({ sort: "deals" });
  const handleViewAllBestSellers = () => goToProductList({ sort: "sold" });
  const handleViewAllTrending = () => goToProductList({ sort: "views" });
  const handleViewAllNewArrivals = () => goToProductList({ sort: "updated" });
const handleViewAllLowStock = () => goToProductList({ sort: "low-stock" });

  const renderProductCard = (item) => {
    const variantTitle = item.variantTitle || item.title || item.productName;
    const typeLabel = StorefrontProductApi.typeLabelOf(item.productType);
    const displayTitle = typeLabel
      ? `${variantTitle} - ${typeLabel}`
      : variantTitle;

    const sellPrice = item.sellPrice ?? item.SellPrice ?? null;
    const listPrice = item.listPrice ?? item.ListPrice ?? null;

    let hasDiscount = false;
    let discountPercent = 0;

    if (
      sellPrice != null &&
      listPrice != null &&
      sellPrice > 0 &&
      listPrice > 0 &&
      sellPrice < listPrice
    ) {
      hasDiscount = true;
      discountPercent = Math.round(100 - (sellPrice / listPrice) * 100);
    }

    const priceNowText = formatCurrency(sellPrice ?? listPrice);
    const priceOldText = hasDiscount ? formatCurrency(listPrice) : null;

    const isOutOfStock = item.isOutOfStock ?? item.status === "OUT_OF_STOCK";

    return (
      <article
        key={item.variantId}
        className={`sf-card ${isOutOfStock ? "sf-card-out" : ""}`}
      >
        <Link
          className="sf-card-link"
          to={`/products/${item.productId}?variant=${item.variantId}`}
        >
          <div className="sf-media">
            {item.thumbnail ? (
              <img src={item.thumbnail} alt={displayTitle} />
            ) : (
              <div className="sf-media-placeholder">
                {displayTitle?.[0] || "K"}
              </div>
            )}

            {item.badges && item.badges.length > 0 && (
              <div className="sf-media-badges">
                {item.badges.map((b) => (
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

            {isOutOfStock && <div className="sf-out-of-stock">Hết hàng</div>}
          </div>

          <div className="sf-body">
            <h3>{displayTitle}</h3>

            <div className="sf-price">
              <div className="sf-price-now">{priceNowText}</div>
              {hasDiscount && (
                <>
                  <div className="sf-price-old">{priceOldText}</div>
                  <div className="sf-price-off">-{discountPercent}%</div>
                </>
              )}
            </div>
          </div>
        </Link>
      </article>
    );
  };

  const renderProductBlock = (title, subtitle, items, onViewAll) => (
    <section className="sf-home-section" key={title}>
      <div className="sf-section-header">
        <div>
          <h2>{title}</h2>
          {subtitle && <p>{subtitle}</p>}
        </div>
        {onViewAll && (
          <button
            type="button"
            className="sf-btn sf-btn-primary sf-home-view-all"
            onClick={onViewAll}
          >
            Xem tất cả
          </button>
        )}
      </div>

      {loadingProducts && items.length === 0 && (
        <div className="sf-loading">Đang tải sản phẩm...</div>
      )}

      {errorProducts && items.length === 0 && !loadingProducts && (
        <div className="sf-error">{errorProducts}</div>
      )}

      {!loadingProducts && !errorProducts && items.length > 0 && (
        <div className="sf-grid sf-grid-responsive">
          {items.map(renderProductCard)}
        </div>
      )}

      {!loadingProducts && !errorProducts && items.length === 0 && (
        <div className="sf-home-empty">Chưa có sản phẩm phù hợp.</div>
      )}
    </section>
  );

  return (
    <main className="sf-home sf-product-page">
      <div className="sf-container">
        {/* HERO */}
        <section className="sf-home-hero">
          <div className="sf-home-hero-inner">
            <div
              className={`sf-home-main-slider ${canSlide ? "sf-can-slide" : ""}`}
              onMouseEnter={() => setIsHoverMain(true)}
              onMouseLeave={() => setIsHoverMain(false)}
              aria-label="Homepage main banner"
            >
              <div
                className="sf-home-main-track"
                style={{
                  transform: `translateX(-${mainSlideIndex * 100}%)`,
                }}
              >
                {(mainSlides || []).map((slide, idx) => {
                  const showText =
                    hasText(slide) || !!slide?.badge || loadingBanners;
                  const bg = slide?.mediaUrl
                    ? showText
                      ? `linear-gradient(135deg, rgba(15,23,42,.45), rgba(15,23,42,.15)), url("${slide.mediaUrl}")`
                      : `url("${slide.mediaUrl}")`
                    : undefined;

                  return (
                    <div
                      key={slide.id || `main-${idx}`}
                      className="sf-home-main-slide-item"
                      style={bg ? { backgroundImage: bg } : undefined}
                      role="button"
                      tabIndex={0}
                      onClick={() => handleClickMainSlide(slide)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          handleClickMainSlide(slide);
                        }
                      }}
                    >
                      {(showText || !slide?.mediaUrl) && (
                        <div className="sf-home-main-slide">
                          {!!slide?.badge && (
                            <div className="sf-home-main-badge">
                              {slide.badge}
                            </div>
                          )}

                          {!!slide?.title && (
                            <h1 className="sf-home-main-title">{slide.title}</h1>
                          )}

                          {!!slide?.subtitle && (
                            <p className="sf-home-main-subtitle">
                              {slide.subtitle}
                            </p>
                          )}

                          {loadingBanners && idx === mainSlideIndex && (
                            <div className="sf-home-banner-loading">
                              Đang tải banner…
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>

              {canSlide && (
                <>
                  <button
                    type="button"
                    className="sf-home-main-arrow sf-home-main-arrow-left"
                    onClick={goPrev}
                    aria-label="Previous slide"
                  >
                    ‹
                  </button>
                  <button
                    type="button"
                    className="sf-home-main-arrow sf-home-main-arrow-right"
                    onClick={goNext}
                    aria-label="Next slide"
                  >
                    ›
                  </button>
                </>
              )}
            </div>

            <div className="sf-home-side-sliders">
              {sideSlides.map((s) => {
                const showText = hasText(s);
                const bg = s?.mediaUrl
                  ? showText
                    ? `linear-gradient(135deg, rgba(15,23,42,.35), rgba(15,23,42,.10)), url("${s.mediaUrl}")`
                    : `url("${s.mediaUrl}")`
                  : undefined;

                return (
                  <div
                    key={s.id}
                    className={`sf-home-side-card ${
                      showText ? "sf-banner-has-text" : "sf-banner-no-text"
                    }`}
                    style={bg ? { backgroundImage: bg } : undefined}
                    role="button"
                    tabIndex={0}
                    onClick={() => handleClickSideSlide(s)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        handleClickSideSlide(s);
                      }
                    }}
                    aria-label={`Homepage side banner ${s.id}`}
                  >
                    {showText && (
                      <div className="sf-home-side-content">
                        <h3>{s.title}</h3>
                        {!!s.subtitle && <p>{s.subtitle}</p>}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        </section>

        {/* Ưu đãi hôm nay */}
        {renderProductBlock(
          "Ưu đãi hôm nay",
          "Giảm sâu trong thời gian có hạn.",
          products.todayBestDeals,
          handleViewAllTodayDeals
        )}

        {/* Giá phù hợp */}
        <section className="sf-home-price-section">
          <div className="sf-home-price-header">
            <h3>Giá phù hợp</h3>
            <p>
              Chọn mức giá tối đa để lọc nhanh (tất cả sản phẩm có giá ≤ mức bạn
              chọn).
            </p>
          </div>
          <div className="sf-home-price-pills">
            {PRICE_FILTERS.map((p) => (
              <button
                key={p.maxPrice}
                type="button"
                className="sf-home-price-pill"
                onClick={() => handlePriceFilterClick(p.maxPrice)}
              >
                {p.label}
              </button>
            ))}
          </div>
        </section>

        {/* Bán chạy nhất */}
        {renderProductBlock(
          "Bán chạy nhất",
          "Được mua nhiều nhất (ưu tiên 30 ngày gần đây).",
          products.bestSellers,
          handleViewAllBestSellers
        )}

        {/* Đang thịnh hành */}
        {renderProductBlock(
          "Đang thịnh hành",
          "Nổi bật theo lượt xem.",
          products.weeklyTrends,
          handleViewAllTrending
        )}

        {/* Mới ra mắt */}
        {renderProductBlock(
          "Mới ra mắt",
          "Sản phẩm mới đăng gần đây.",
          products.newlyUpdated,
          handleViewAllNewArrivals
        )}

        {/* Sắp hết hàng */}
        {renderProductBlock(
          "Sắp hết hàng",
          "Số lượng có hạn — tranh thủ trước khi hết.",
          products.lowStock,
          handleViewAllLowStock
        )}
      </div>
    </main>
  );
};

export default StorefrontHomepagePage;
