// src/pages/storefront/StorefrontHomepagePage.jsx
import React, { useEffect, useState, useCallback } from "react";
import { Link, useNavigate } from "react-router-dom";
import StorefrontHomepageApi from "../../services/storefrontHomepageService";
import StorefrontProductApi from "../../services/storefrontProductService";
import { CART_UPDATED_EVENT } from "../../services/storefrontCartService";

import "./StorefrontProductListPage.css"; // dùng lại css card/grid/nút
import "./StorefrontHomepagePage.css";

// Slider lớn tự trượt
const MAIN_SLIDES = [
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

// 2 slider nhỏ bên cạnh
const SIDE_SLIDES = [
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

// 6 từ khoá tìm kiếm cố định
const TOP_KEYWORDS = [
  "Windows 11 Pro",
  "Office 365",
  "ChatGPT Plus",
  "Midjourney",
  "Steam Wallet",
  "Canva Pro",
];

// Giá phù hợp – giá từ (minPrice) cố định
const PRICE_FILTERS = [
  { label: "20.000đ", minPrice: 20000 },
  { label: "50.000đ", minPrice: 50000 },
  { label: "100.000đ", minPrice: 100000 },
  { label: "200.000đ", minPrice: 200000 },
  { label: "500.000đ", minPrice: 500000 },
  { label: "1.000.000đ", minPrice: 1000000 },
];

// Format tiền VND
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

const StorefrontHomepagePage = () => {
  const navigate = useNavigate();

  const [mainSlideIndex, setMainSlideIndex] = useState(0);

  const [products, setProducts] = useState({
    todayBestDeals: [],
    bestSellers: [],
    weeklyTrends: [],
    newlyUpdated: [],
  });

  const [loadingProducts, setLoadingProducts] = useState(false);
  const [errorProducts, setErrorProducts] = useState("");

  // Auto slide
  useEffect(() => {
    if (MAIN_SLIDES.length <= 1) return;
    const timer = setInterval(() => {
      setMainSlideIndex((prev) => (prev + 1) % MAIN_SLIDES.length);
    }, 6000);
    return () => clearInterval(timer);
  }, []);

  // GỌI API homepage/products
  const loadHomepageProducts = useCallback(async () => {
    setLoadingProducts(true);
    setErrorProducts("");
    try {
      const res = await StorefrontHomepageApi.products();
      setProducts(res);
    } catch (err) {
      console.error("Load homepage products failed:", err);
      setErrorProducts(
        "Không tải được danh sách sản phẩm. Vui lòng thử lại sau."
      );
    } finally {
      setLoadingProducts(false);
    }
  }, []);

  useEffect(() => {
    loadHomepageProducts();
  }, [loadHomepageProducts]);

  // Khi cart thay đổi (Add/Update/Remove/Clear) -> reload block sản phẩm
  useEffect(() => {
    if (typeof window === "undefined") return;

    const handleCartUpdated = () => {
      loadHomepageProducts();
    };

    window.addEventListener(CART_UPDATED_EVENT, handleCartUpdated);
    return () => {
      window.removeEventListener(CART_UPDATED_EVENT, handleCartUpdated);
    };
  }, [loadHomepageProducts]);

  // Helper: chuyển sang trang danh sách sản phẩm
  const goToProductList = (params = {}) => {
    const sp = new URLSearchParams();

    if (params.q) sp.set("q", params.q);
    if (params.categoryId) sp.set("categoryId", String(params.categoryId));
    if (params.productType) sp.set("productType", params.productType);
    if (params.minPrice != null) sp.set("minPrice", String(params.minPrice));
    if (params.maxPrice != null) sp.set("maxPrice", String(params.maxPrice));
    if (params.sort && params.sort !== "default") {
      sp.set("sort", params.sort);
    }

    const search = sp.toString();
    navigate(`/products${search ? `?${search}` : ""}`);
  };

  const handleKeywordClick = (keyword) => {
    goToProductList({ q: keyword });
  };

  const handlePriceFilterClick = (minPrice) => {
    goToProductList({ minPrice, sort: "price-asc" });
  };

  // Click cả banner slider chính
  const handleClickMainSlider = () => {
    const current = MAIN_SLIDES[mainSlideIndex];
    if (current?.linkTo) {
      navigate(current.linkTo);
      return;
    }
    if (current?.params) {
      goToProductList(current.params);
      return;
    }
    goToProductList({});
  };

  const handleClickSideSlide = (slide) => {
    if (slide?.linkTo) {
      navigate(slide.linkTo);
      return;
    }
    if (slide?.params) {
      goToProductList(slide.params);
      return;
    }
    goToProductList({});
  };

  // “Xem tất cả” cho từng block
  const handleViewAllTodayDeals = () => {
    goToProductList({ sort: "default" });
  };

  const handleViewAllBestSellers = () => {
    goToProductList({ sort: "sold" });
  };

  const handleViewAllWeeklyTrends = () => {
    goToProductList({ sort: "default" });
  };

  const handleViewAllNewlyUpdated = () => {
    goToProductList({ sort: "updated" });
  };

  // Render 1 card sản phẩm (dùng chung cho mọi block)
  const renderProductCard = (item) => {
    const variantTitle =
      item.variantTitle || item.title || item.productName;
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
      discountPercent = Math.round(
        100 - (sellPrice / listPrice) * 100
      );
    }

    // Nếu chưa có sellPrice (trường hợp hiếm) thì hiển thị listPrice
    const priceNowText = formatCurrency(sellPrice ?? listPrice);
    const priceOldText = hasDiscount
      ? formatCurrency(listPrice)
      : null;

    const isOutOfStock =
      item.isOutOfStock ??
      item.status === "OUT_OF_STOCK";

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

            {isOutOfStock && (
              <div className="sf-out-of-stock">Hết hàng</div>
            )}
          </div>

          <div className="sf-body">
            <h3>{displayTitle}</h3>

            <div className="sf-price">
              <div className="sf-price-now">{priceNowText}</div>
              {hasDiscount && (
                <>
                  <div className="sf-price-old">{priceOldText}</div>
                  <div className="sf-price-off">
                    -{discountPercent}%
                  </div>
                </>
              )}
            </div>
          </div>
        </Link>
      </article>
    );
  };

  // Render block sản phẩm
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

  const activeMainSlide = MAIN_SLIDES[mainSlideIndex];

  return (
    <main className="sf-home sf-product-page">
      <div className="sf-container">
        {/* HERO: slider + 2 slider nhỏ bên cạnh */}
        <section className="sf-home-hero">
          <div className="sf-home-hero-inner">
            {/* Slider chính – cả khối là link */}
            <div
              className="sf-home-main-slider"
              role="button"
              tabIndex={0}
              onClick={handleClickMainSlider}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                  e.preventDefault();
                  handleClickMainSlider();
                }
              }}
            >
              <div className="sf-home-main-slide">
                <div className="sf-home-main-badge">
                  {activeMainSlide.badge}
                </div>
                <h1 className="sf-home-main-title">
                  {activeMainSlide.title}
                </h1>
                <p className="sf-home-main-subtitle">
                  {activeMainSlide.subtitle}
                </p>
              </div>
            </div>

            {/* 2 slider nhỏ bên phải */}
            <div className="sf-home-side-sliders">
              {SIDE_SLIDES.map((s) => (
                <div
                  key={s.id}
                  className="sf-home-side-card"
                  role="button"
                  tabIndex={0}
                  onClick={() => handleClickSideSlide(s)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      handleClickSideSlide(s);
                    }
                  }}
                >
                  <div className="sf-home-side-content">
                    <h3>{s.title}</h3>
                    <p>{s.subtitle}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* Thanh tìm kiếm hàng đầu – layout giống “Giá phù hợp” */}
        <section className="sf-home-top-search">
          <div className="sf-home-top-search-inner">
            <div className="sf-home-top-search-header">
              <h3>Tìm kiếm hàng đầu</h3>
              <p>
                Một số từ khóa được khách chọn nhiều. Click để lọc
                nhanh danh sách sản phẩm.
              </p>
            </div>
            <div className="sf-home-top-search-keywords">
              {TOP_KEYWORDS.map((kw) => (
                <button
                  key={kw}
                  type="button"
                  className="sf-home-chip"
                  onClick={() => handleKeywordClick(kw)}
                >
                  {kw}
                </button>
              ))}
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
            <p>Chọn khoảng giá bạn thấy hợp lý để lọc nhanh.</p>
          </div>
          <div className="sf-home-price-pills">
            {PRICE_FILTERS.map((p) => (
              <button
                key={p.minPrice}
                type="button"
                className="sf-home-price-pill"
                onClick={() => handlePriceFilterClick(p.minPrice)}
              >
                {p.label}
              </button>
            ))}
          </div>
        </section>

        {/* Sản phẩm bán chạy */}
        {renderProductBlock(
          "Sản phẩm bán chạy",
          "Được mua nhiều nhất tuần qua.",
          products.bestSellers,
          handleViewAllBestSellers
        )}

        {/* Xu hướng tuần này */}
        {renderProductBlock(
          "Xu hướng tuần này",
          "Sản phẩm nổi bật theo lượt xem và tương tác.",
          products.weeklyTrends,
          handleViewAllWeeklyTrends
        )}

        {/* Mới cập nhật */}
        {renderProductBlock(
          "Mới cập nhật",
          "Sản phẩm mới thêm hoặc vừa cập nhật nội dung.",
          products.newlyUpdated,
          handleViewAllNewlyUpdated
        )}

        {/* Dịch vụ hỗ trợ – click cả card để qua trang chi tiết */}
        <section className="sf-home-services">
          <div className="sf-section-header">
            <div>
              <h2>Dịch vụ hỗ trợ</h2>
              <p>Click vào dịch vụ để xem chi tiết hoặc đặt lịch hỗ trợ.</p>
            </div>
          </div>

          <div className="sf-home-services-grid">
            <div
              className="sf-home-service-card"
              role="button"
              tabIndex={0}
              onClick={() => navigate("/support-service")}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                  e.preventDefault();
                  navigate("/support-service");
                }
              }}
            >
              <div className="sf-home-service-icon">🖥️</div>
              <h3>Cài đặt từ xa</h3>
              <p>
                Hỗ trợ cài Windows / Office, phần mềm qua TeamViewer /
                AnyDesk.
              </p>
            </div>

            <div
              className="sf-home-service-card"
              role="button"
              tabIndex={0}
              onClick={() => navigate("/support-service")}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                  e.preventDefault();
                  navigate("/support-service");
                }
              }}
            >
              <div className="sf-home-service-icon">📘</div>
              <h3>Hướng dẫn sử dụng</h3>
              <p>
                Video + bài viết hướng dẫn, giải đáp thắc mắc trong quá
                trình sử dụng.
              </p>
            </div>

            <div
              className="sf-home-service-card"
              role="button"
              tabIndex={0}
              onClick={() => navigate("/support-service")}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                  e.preventDefault();
                  navigate("/support-service");
                }
              }}
            >
              <div className="sf-home-service-icon">🛠️</div>
              <h3>Fix lỗi phần mềm đã mua</h3>
              <p>
                Xử lý lỗi kích hoạt, lỗi bản quyền, tư vấn nâng cấp cấu
                hình phù hợp.
              </p>
            </div>
          </div>
        </section>
      </div>
    </main>
  );
};

export default StorefrontHomepagePage;
