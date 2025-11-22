// src/pages/storefront/StorefrontProductListPage.jsx
import React, { useEffect, useState, useCallback } from "react";
import { Link } from "react-router-dom";
import StorefrontProductApi from "../../services/storefrontProductService";
import "./StorefrontProductListPage.css";

// Map sort value -> label hiển thị
const SORT_OPTIONS = [
  { value: "default",    label: "Phù hợp nhất" },   // nhiều view nhất
  { value: "updated",    label: "Mới cập nhật" },
  { value: "sold",       label: "Bán chạy" },       // hiện tạm dùng ViewCount, sau map SoldCount
  { value: "price-asc",  label: "Giá tăng dần" },
  { value: "price-desc", label: "Giá giảm dần" },
  { value: "name-asc",   label: "Tên A → Z" },
  { value: "name-desc",  label: "Tên Z → A" },
];

// Helper format tiền VND (nếu sau này Price khác null)
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

const StorefrontProductListPage = () => {
  // ====== State filters từ BE ======
  const [categories, setCategories] = useState([]);
  const [availableTypes, setAvailableTypes] = useState([]);

  // ====== State form filter trên UI ======
  const [form, setForm] = useState({
    categoryId: "",
    productType: "",
    minPrice: "",
    maxPrice: "",
    sort: "default",
  });

  // ====== State query thực tế gửi lên API ======
  const [query, setQuery] = useState({
    q: "",
    categoryId: undefined,
    productType: undefined,
    minPrice: undefined,
    maxPrice: undefined,
    sort: "default",
    page: 1,
    pageSize: 8,
  });

  // ====== Data list & paging ======
  const [items, setItems] = useState([]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(8);
  const [totalItems, setTotalItems] = useState(0);
  const [totalPages, setTotalPages] = useState(1);

  // ====== Loading / error ======
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // ====== Lấy filters (danh mục + loại) ======
  useEffect(() => {
    let isMounted = true;

    (async () => {
      try {
        const res = await StorefrontProductApi.filters();
        if (!isMounted) return;

        setCategories(res.categories || []);

        // Map code -> label (dùng PRODUCT_TYPES + typeLabelOf đã đồng bộ với admin)
        const typeCodes = res.productTypes || [];
        const mappedTypes = typeCodes.map((code) => ({
          value: code,
          label: StorefrontProductApi.typeLabelOf(code),
        }));
        setAvailableTypes(mappedTypes);
      } catch (err) {
        console.error("Load storefront filters failed:", err);
      }
    })();

    return () => {
      isMounted = false;
    };
  }, []);

  // ====== Gửi request lấy danh sách biến thể ======
  const loadVariants = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const res = await StorefrontProductApi.listVariants(query);

      setItems(res.items || []);
      setPage(res.page);
      setPageSize(res.pageSize);
      setTotalItems(res.totalItems);
      setTotalPages(res.totalPages);
    } catch (err) {
      console.error("Load storefront variants failed:", err);
      setError("Không tải được danh sách sản phẩm. Vui lòng thử lại sau.");
    } finally {
      setLoading(false);
    }
  }, [query]);

  useEffect(() => {
    loadVariants();
  }, [loadVariants]);

  // ====== Handlers ======
  const handleChangeField = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const handleApplyFilter = () => {
    setQuery((prev) => ({
      ...prev,
      categoryId: form.categoryId ? Number(form.categoryId) : undefined,
      productType: form.productType || undefined,
      minPrice: form.minPrice ? Number(form.minPrice) : undefined,
      maxPrice: form.maxPrice ? Number(form.maxPrice) : undefined,
      sort: form.sort || "default",
      page: 1, // reset về trang 1
    }));
  };

  const handleResetFilter = () => {
    setForm({
      categoryId: "",
      productType: "",
      minPrice: "",
      maxPrice: "",
      sort: "default",
    });

    setQuery((prev) => ({
      ...prev,
      q: "",
      categoryId: undefined,
      productType: undefined,
      minPrice: undefined,
      maxPrice: undefined,
      sort: "default",
      page: 1,
    }));
  };

  const handleChangePage = (newPage) => {
    if (newPage < 1 || newPage > totalPages || newPage === page) return;
    setQuery((prev) => ({
      ...prev,
      page: newPage,
    }));
  };

  // ====== Tính text "Đang xem ..." ======
  const viewingFrom = totalItems === 0 ? 0 : (page - 1) * pageSize + 1;
  const viewingTo = totalItems === 0 ? 0 : Math.min(totalItems, page * pageSize);

  return (
    <main className="sf-product-page">
      <div className="sf-container">
        {/* Bộ lọc */}
        <section className="sf-section">
          <div className="sf-filters">
            <div className="sf-filters-grid">
              {/* Danh mục */}
              <div className="sf-field">
                <label className="sf-label">Danh mục</label>
                <select
                  className="sf-select"
                  name="categoryId"
                  value={form.categoryId}
                  onChange={handleChangeField}
                >
                  <option value="">Tất cả</option>
                  {categories.map((c) => (
                    <option key={c.categoryId} value={c.categoryId}>
                      {c.categoryName}
                    </option>
                  ))}
                </select>
              </div>

              {/* Loại sản phẩm */}
              <div className="sf-field">
                <label className="sf-label">Loại sản phẩm</label>
                <select
                  className="sf-select"
                  name="productType"
                  value={form.productType}
                  onChange={handleChangeField}
                >
                  <option value="">Tất cả</option>
                  {availableTypes.map((t) => (
                    <option key={t.value} value={t.value}>
                      {t.label}
                    </option>
                  ))}
                </select>
              </div>

              {/* Giá từ */}
              <div className="sf-field">
                <label className="sf-label">Giá từ</label>
                <input
                  type="number"
                  className="sf-input"
                  placeholder="0"
                  name="minPrice"
                  value={form.minPrice}
                  onChange={handleChangeField}
                  min="0"
                />
              </div>

              {/* Giá đến */}
              <div className="sf-field">
                <label className="sf-label">Đến</label>
                <input
                  type="number"
                  className="sf-input"
                  placeholder="0"
                  name="maxPrice"
                  value={form.maxPrice}
                  onChange={handleChangeField}
                  min="0"
                />
              </div>

              {/* Sắp xếp */}
              <div className="sf-field">
                <label className="sf-label">Sắp xếp</label>
                <select
                  className="sf-select"
                  name="sort"
                  value={form.sort}
                  onChange={handleChangeField}
                >
                  {SORT_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="sf-filters-actions">
              <div className="sf-filters-summary">
                {totalItems > 0 ? (
                  <>Đang xem {viewingFrom}-{viewingTo} trong {totalItems} sản phẩm</>
                ) : (
                  <>Không có sản phẩm nào phù hợp</>
                )}
              </div>
              <div className="sf-filters-buttons">
                <button
                  type="button"
                  className="sf-btn sf-btn-primary"
                  onClick={handleApplyFilter}
                >
                  Lọc
                </button>
                <button
                  type="button"
                  className="sf-btn"
                  onClick={handleResetFilter}
                >
                  Khôi phục
                </button>
              </div>
            </div>
          </div>
        </section>

        {/* Danh sách sản phẩm */}
        <section className="sf-section sf-section-cards">
          <div className="sf-section-header">
            <div>
              <h2>Tất cả sản phẩm</h2>
              {/* Có thể hiển thị thêm mô tả, search text... nếu muốn */}
            </div>
          </div>

          {loading && (
            <div className="sf-loading">
              Đang tải sản phẩm...
            </div>
          )}

          {error && !loading && (
            <div className="sf-error">
              {error}
            </div>
          )}

          {!loading && !error && (
            <>
   <div className="sf-grid sf-grid-responsive">
  {items.map((item) => {
    const variantTitle = item.variantTitle || item.title || item.productName;
    const typeLabel = StorefrontProductApi.typeLabelOf(item.productType);
    const displayTitle = typeLabel
      ? `${variantTitle} - ${typeLabel}`
      : variantTitle;

    const samplePriceNow = 295000;
    const samplePriceOld = 1500000;
    const discountPercent = Math.round(
      100 - (samplePriceNow / samplePriceOld) * 100
    );

    const priceNowText = formatCurrency(samplePriceNow);
    const priceOldText = formatCurrency(samplePriceOld);

    const isOutOfStock =
      item.isOutOfStock ?? item.status === "OUT_OF_STOCK";

    return (
      <article
        key={item.variantId}
        className={`sf-card ${isOutOfStock ? "sf-card-out" : ""}`}
      >
        <Link
          className="sf-card-link"
          to={`/products/${item.productId}?variant=${item.variantId}`}
        >
          {/* Ảnh sản phẩm */}
          <div className="sf-media">
            {item.thumbnail ? (
              <img src={item.thumbnail} alt={displayTitle} />
            ) : (
              <div className="sf-media-placeholder">
                {displayTitle?.[0] || "K"}
              </div>
            )}

            {/* Nhãn sản phẩm (badge) đưa lên góc ảnh */}
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

            {/* Dòng "Hết hàng" to ở giữa ảnh */}
            {isOutOfStock && (
              <div className="sf-out-of-stock">Hết hàng</div>
            )}
          </div>

          <div className="sf-body">
            <h3>{displayTitle}</h3>

            {/* Giá sản phẩm */}
            <div className="sf-price">
              <div className="sf-price-now">{priceNowText}</div>
              <div className="sf-price-old">{priceOldText}</div>
              <div className="sf-price-off">-{discountPercent}%</div>
            </div>
          </div>
        </Link>
      </article>
    );
  })}
</div>

              {/* Pagination */}
              {totalPages > 1 && (
                <nav className="sf-pager">
                  <button
                    type="button"
                    className="sf-btn"
                    disabled={page <= 1}
                    onClick={() => handleChangePage(page - 1)}
                  >
                    Trước
                  </button>

                  {/* Chỉ hiển thị vài trang xung quanh current cho gọn */}
                  {Array.from({ length: totalPages }, (_, i) => i + 1)
                    .filter((p) => {
                      // hiển thị luôn trang 1, cuối và các trang gần current
                      if (p === 1 || p === totalPages) return true;
                      return Math.abs(p - page) <= 1;
                    })
                    .map((p, idx, arr) => {
                      const isCurrent = p === page;
                      const showEllipsis =
                        idx > 0 &&
                        p - arr[idx - 1] > 1;

                      return (
                        <React.Fragment key={p}>
                          {showEllipsis && (
                            <span className="sf-pager-ellipsis">…</span>
                          )}
                          <button
                            type="button"
                            className={`sf-btn ${
                              isCurrent ? "sf-btn-current" : ""
                            }`}
                            onClick={() => handleChangePage(p)}
                          >
                            {p}
                          </button>
                        </React.Fragment>
                      );
                    })}

                  <button
                    type="button"
                    className="sf-btn"
                    disabled={page >= totalPages}
                    onClick={() => handleChangePage(page + 1)}
                  >
                    Sau
                  </button>
                </nav>
              )}
            </>
          )}
        </section>
      </div>
    </main>
  );
};

export default StorefrontProductListPage;
