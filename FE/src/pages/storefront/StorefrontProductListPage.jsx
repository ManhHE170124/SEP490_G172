// src/pages/storefront/StorefrontProductListPage.jsx
import React, { useEffect, useState, useCallback } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import StorefrontProductApi from "../../services/storefrontProductService";
import { CART_UPDATED_EVENT } from "../../services/storefrontCartService";
import "./StorefrontProductListPage.css";

const SORT_OPTIONS = [
  { value: "default",    label: "Phù hợp nhất" },
  { value: "deals",      label: "Ưu đãi hôm nay" },   // ✅ NEW
  { value: "low-stock",  label: "Sắp hết hàng" },     // ✅ NEW
  { value: "updated",    label: "Mới cập nhật" },
  { value: "sold",       label: "Bán chạy" },
  { value: "views",      label: "Xem nhiều" },        // (optional)
  { value: "price-asc",  label: "Giá tăng dần" },
  { value: "price-desc", label: "Giá giảm dần" },
  { value: "name-asc",   label: "Tên A → Z" },
  { value: "name-desc",  label: "Tên Z → A" },
];

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

const PAGE_SIZE = 8;

// Helper: build query object từ URL (KHÔNG đẩy page / pageSize lên URL nữa)
const buildQueryFromSearch = (search) => {
  const params = new URLSearchParams(search);

  const q = params.get("q") || "";
  const categoryIdStr = params.get("categoryId");
  const productType = params.get("productType") || "";
  const minPriceStr = params.get("minPrice");
  const maxPriceStr = params.get("maxPrice");
  const sort = params.get("sort") || "default";

  const page = 1;
  const pageSize = PAGE_SIZE;

  return {
    q,
    categoryId: categoryIdStr ? Number(categoryIdStr) : undefined,
    productType: productType || undefined,
    minPrice: minPriceStr ? Number(minPriceStr) : undefined,
    maxPrice: maxPriceStr ? Number(maxPriceStr) : undefined,
    sort,
    page,
    pageSize,
  };
};

const StorefrontProductListPage = () => {
  const location = useLocation();
  const navigate = useNavigate();

  const [categories, setCategories] = useState([]);
  const [availableTypes, setAvailableTypes] = useState([]);

  // Form filter hiển thị trên UI
  const [form, setForm] = useState({
    categoryId: "",
    productType: "",
    minPrice: "",
    maxPrice: "",
    sort: "default",
  });

  // Data list & paging
  const [items, setItems] = useState([]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(PAGE_SIZE);
  const [totalItems, setTotalItems] = useState(0);
  const [totalPages, setTotalPages] = useState(1);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // ==== Lấy filters (danh mục + loại) ====
  useEffect(() => {
    let isMounted = true;

    (async () => {
      try {
        const res = await StorefrontProductApi.filters();
        if (!isMounted) return;

        setCategories(res.categories || []);

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

  // ==== Đồng bộ FORM từ URL (mỗi lần location.search thay đổi) ====
  useEffect(() => {
    const q = buildQueryFromSearch(location.search);

    setForm({
      categoryId: q.categoryId != null ? String(q.categoryId) : "",
      productType: q.productType ?? "",
      minPrice: q.minPrice != null ? String(q.minPrice) : "",
      maxPrice: q.maxPrice != null ? String(q.maxPrice) : "",
      sort: q.sort || "default",
    });
  }, [location.search]);

  // ==== Gửi request lấy danh sách biến thể theo URL (page mặc định = 1) ====
  const loadVariants = useCallback(async (query) => {
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
  }, []);

  useEffect(() => {
    const q = buildQueryFromSearch(location.search);
    loadVariants(q);
  }, [location.search, loadVariants]);

  // Khi cart thay đổi -> reload list hiện tại
  useEffect(() => {
    if (typeof window === "undefined") return;

    const handleCartUpdated = () => {
      const baseQuery = buildQueryFromSearch(location.search);
      const queryWithPage = { ...baseQuery, page, pageSize: PAGE_SIZE };
      loadVariants(queryWithPage);
    };

    window.addEventListener(CART_UPDATED_EVENT, handleCartUpdated);
    return () => window.removeEventListener(CART_UPDATED_EVENT, handleCartUpdated);
  }, [location.search, page, loadVariants]);

  // ====== Handlers ======
  const handleChangeField = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  // Bấm nút "Lọc" -> cập nhật URL
  const handleApplyFilter = () => {
    const params = new URLSearchParams(location.search);

    params.delete("page");
    params.delete("pageSize");

    if (form.categoryId) params.set("categoryId", form.categoryId);
    else params.delete("categoryId");

    if (form.productType) params.set("productType", form.productType);
    else params.delete("productType");

    if (form.minPrice) params.set("minPrice", form.minPrice);
    else params.delete("minPrice");

    if (form.maxPrice) params.set("maxPrice", form.maxPrice);
    else params.delete("maxPrice");

    if (form.sort && form.sort !== "default") params.set("sort", form.sort);
    else params.delete("sort");

    const search = params.toString();
    setPage(1);
    navigate(`${location.pathname}${search ? `?${search}` : ""}`);
  };

  const handleResetFilter = () => {
    setForm({
      categoryId: "",
      productType: "",
      minPrice: "",
      maxPrice: "",
      sort: "default",
    });

    setPage(1);
    navigate(location.pathname);
  };

  const handleChangePage = (newPage) => {
    if (newPage < 1 || newPage > totalPages || newPage === page) return;

    const baseQuery = buildQueryFromSearch(location.search);
    const queryWithPage = { ...baseQuery, page: newPage, pageSize: PAGE_SIZE };

    setPage(newPage);
    loadVariants(queryWithPage);
  };

  const viewingFrom = totalItems === 0 ? 0 : (page - 1) * pageSize + 1;
  const viewingTo = totalItems === 0 ? 0 : Math.min(totalItems, page * pageSize);

  return (
    <main className="sf-product-page">
      <div className="sf-container">
        {/* Bộ lọc */}
        <section className="sf-section">
          <div className="sf-filters">
            <div className="sf-filters-grid">
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
                <button type="button" className="sf-btn sf-btn-primary" onClick={handleApplyFilter}>
                  Lọc
                </button>
                <button type="button" className="sf-btn" onClick={handleResetFilter}>
                  Khôi phục
                </button>
              </div>
            </div>
          </div>
        </section>

        {/* Danh sách sản phẩm */}
        <section className="sf-section sf-section-cards">
          <div className="sf-section-header">
            <div><h2>Tất cả sản phẩm</h2></div>
          </div>

          {loading && <div className="sf-loading">Đang tải sản phẩm...</div>}
          {error && !loading && <div className="sf-error">{error}</div>}

          {!loading && !error && (
            <>
              <div className="sf-grid sf-grid-responsive">
                {items.map((item) => {
                  const variantTitle = item.variantTitle || item.title || item.productName;
                  const typeLabel = StorefrontProductApi.typeLabelOf(item.productType);
                  const displayTitle = typeLabel ? `${variantTitle} - ${typeLabel}` : variantTitle;

                  const sellPrice = item.sellPrice;
                  const listPrice = item.listPrice;

                  const priceNowText = formatCurrency(sellPrice);

                  const hasOldPrice = listPrice != null && listPrice > sellPrice;
                  const priceOldText = hasOldPrice ? formatCurrency(listPrice) : null;
                  const discountPercent = hasOldPrice
                    ? Math.round(100 - (sellPrice / listPrice) * 100)
                    : null;

                  const isOutOfStock = item.isOutOfStock ?? item.status === "OUT_OF_STOCK";

                  return (
                    <article key={item.variantId} className={`sf-card ${isOutOfStock ? "sf-card-out" : ""}`}>
                      <Link className="sf-card-link" to={`/products/${item.productId}?variant=${item.variantId}`}>
                        <div className="sf-media">
                          {item.thumbnail ? (
                            <img src={item.thumbnail} alt={displayTitle} />
                          ) : (
                            <div className="sf-media-placeholder">{displayTitle?.[0] || "K"}</div>
                          )}

                          {item.badges && item.badges.length > 0 && (
                            <div className="sf-media-badges">
                              {item.badges.map((b) => (
                                <span
                                  key={b.badgeCode}
                                  className="sf-tag"
                                  style={b.colorHex ? { backgroundColor: b.colorHex, color: "#fff" } : undefined}
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
                            {hasOldPrice && (
                              <>
                                <div className="sf-price-old">{priceOldText}</div>
                                {discountPercent > 0 && (
                                  <div className="sf-price-off">-{discountPercent}%</div>
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

                  {Array.from({ length: totalPages }, (_, i) => i + 1)
                    .filter((p) => p === 1 || p === totalPages || Math.abs(p - page) <= 1)
                    .map((p, idx, arr) => {
                      const isCurrent = p === page;
                      const showEllipsis = idx > 0 && p - arr[idx - 1] > 1;

                      return (
                        <React.Fragment key={p}>
                          {showEllipsis && <span className="sf-pager-ellipsis">…</span>}
                          <button
                            type="button"
                            className={`sf-btn ${isCurrent ? "sf-btn-current" : ""}`}
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
