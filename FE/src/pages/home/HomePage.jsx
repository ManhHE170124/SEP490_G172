import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import homepageApi from "../../services/homepage";
import "./HomePage.css";

const currencyFormat = new Intl.NumberFormat("vi-VN");
const compactFormat = new Intl.NumberFormat("vi-VN", {
  notation: "compact",
  maximumFractionDigits: 1,
});

const formatMoney = (value) => {
  if (value === null || value === undefined) return "Liên hệ";
  return `${currencyFormat.format(value)}đ`;
};

const formatCompact = (value) => {
  if (!value) return "0";
  return compactFormat.format(value);
};

const formatWarranty = (days) => {
  if (!days) return "";
  if (days >= 365) {
    return `BH ${Math.round(days / 365)} năm`;
  }
  if (days >= 30) {
    return `BH ${Math.round(days / 30)} tháng`;
  }
  return `BH ${days} ngày`;
};

const buildTags = (item) => {
  const tags = [];
  if (item.discountPercent) {
    tags.push({
      label: `-${Math.round(item.discountPercent)}%`,
      tone: "primary",
    });
  }
  if (item.productTypeLabel) {
    tags.push({
      label: item.productTypeLabel,
      tone: "secondary",
    });
  }
  const badgeLabel =
    formatWarranty(item.warrantyDays) ||
    (item.autoDelivery ? "Kích hoạt 1 phút" : "") ||
    item.badges?.[0]?.label ||
    "";
  if (badgeLabel) {
    tags.push({ label: badgeLabel, tone: "success" });
  }
  return tags.slice(0, 3);
};

const ProductCard = ({ item, onNavigate }) => {
  const tags = buildTags(item);
  const ratingText = item.rating
    ? `⭐ ${item.rating.toFixed(1)}`
    : "Chưa có đánh giá";
  const reviewsText = item.reviewCount
    ? `${formatCompact(item.reviewCount)} đánh giá`
    : "";
  const soldText = item.soldCount
    ? `Đã bán ${formatCompact(item.soldCount)}`
    : "Mới mở bán";

  return (
    <a
      className="hp-card"
      href={`/product-list?highlight=${encodeURIComponent(item.slug)}`}
      onClick={(event) => onNavigate(event, item.slug)}
    >
      <div className="media">
        <div className="badges">
          {tags.map((tag) => (
            <span
              key={`${item.productId}-${tag.label}`}
              className={`tag ${tag.tone}`}
            >
              {tag.label}
            </span>
          ))}
        </div>
      </div>
      <div className="body">
        <h3>{item.name}</h3>
        <div className="meta">
          {ratingText}
          {reviewsText && <span className="dot" />}
          {reviewsText}
          <span className="dot" />
          {soldText}
        </div>
        <div className="price">
          <div className="now">{formatMoney(item.price)}</div>
          {item.originalPrice && item.originalPrice > item.price && (
            <div className="old">{formatMoney(item.originalPrice)}</div>
          )}
        </div>
      </div>
    </a>
  );
};

const FilterBlock = ({ title, chips, onNavigate }) => {
  if (!chips?.length) return null;
  return (
    <section className="hp-section">
      <div className="container">
        <div className="hp-filter-card">
          <div className="hp-filter-title">{title}</div>
          <div className="hp-chip-grid">
            {chips.map((chip) => (
              <button
                type="button"
                key={`${title}-${chip.label}`}
                className={`hp-chip-btn ${chip.tone ?? "solid"}`}
                onClick={() => onNavigate(chip.href)}
              >
                {chip.label}
              </button>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
};

const ProductShelf = ({ shelf, accent, onNavigate }) => {
  if (!shelf?.items?.length) return null;
  return (
    <section className={`hp-section ${accent ? "hp-section-alt" : ""}`}>
      <div className="container">
        <div className="hp-section-header">
          <div>
            <h2>{shelf.title}</h2>
            <p>{shelf.subtitle}</p>
          </div>
          <button
            type="button"
            className="hp-btn"
            onClick={() => onNavigate("/product-list")}
          >
            Xem tất cả
          </button>
        </div>
        <div className="hp-grid hp-grid-products">
          {shelf.items.map((item) => (
            <ProductCard
              key={item.variantId}
              item={item}
              onNavigate={(event, slug) =>
                onNavigate(`/product-list?highlight=${slug}`, event)
              }
            />
          ))}
        </div>
      </div>
    </section>
  );
};

const Services = ({ services, onNavigate }) => {
  if (!services?.length) return null;
  return (
    <section className="hp-section">
      <div className="container">
        <div className="hp-section-header">
          <div>
            <h2>Dịch vụ hỗ trợ</h2>
            <p>Click để xem chi tiết hoặc đặt lịch.</p>
          </div>
          <button
            type="button"
            className="hp-btn"
            onClick={() => onNavigate("/support-service")}
          >
            Đặt lịch
          </button>
        </div>
        <div className="hp-services-grid">
          {services.map((svc) => (
            <article
              key={svc.title}
              className="hp-service-card"
              onClick={() => onNavigate(svc.actionUrl)}
              role="button"
              tabIndex={0}
              onKeyDown={(event) => {
                if (event.key === "Enter") onNavigate(svc.actionUrl);
              }}
            >
              <p className="svc-title">{svc.title}</p>
              <p className="svc-desc">{svc.description}</p>
              <span className="svc-action">{svc.actionText}</span>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
};

const HomePage = () => {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const navigate = useNavigate();

  useEffect(() => {
    let mounted = true;
    const load = async () => {
      try {
        setLoading(true);
        const payload = await homepageApi.getSummary();
        if (mounted) {
          setData(payload);
          setError(null);
        }
      } catch (err) {
        if (mounted) {
          const message =
            err?.response?.data?.message ||
            err?.message ||
            "Không thể tải dữ liệu trang chủ.";
          setError(message);
        }
      } finally {
        if (mounted) {
          setLoading(false);
        }
      }
    };
    load();
    return () => {
      mounted = false;
    };
  }, []);

  const handleNavigate = (href, event) => {
    if (event) {
      event.preventDefault();
    }
    if (!href) return;
    navigate(href);
  };

  const topSearches = data?.topSearches ?? [];

  return (
    <div className="hp-page">
      {loading && (
        <div className="hp-status">
          <div className="container">Đang tải trang chủ...</div>
        </div>
      )}

      {error && !loading && (
        <div className="hp-status error">
          <div className="container">
            <p>{error}</p>
            <button
              type="button"
              className="hp-btn"
              onClick={() => window.location.reload()}
            >
              Thử lại
            </button>
          </div>
        </div>
      )}

      {!loading && !error && (
        <>
          <FilterBlock
            title="Tìm kiếm hàng đầu"
            chips={topSearches}
            onNavigate={(href) => handleNavigate(href)}
          />

          <Services
            services={data?.services}
            onNavigate={(href) => handleNavigate(href)}
          />
        </>
      )}
    </div>
  );
};

export default HomePage;
