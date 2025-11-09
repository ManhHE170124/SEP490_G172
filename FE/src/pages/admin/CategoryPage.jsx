import React from "react";
import { CategoryApi, CategoryCsv } from "../../services/categories";
import { Link } from "react-router-dom";
import { BadgesApi } from "../../services/badges";
import "./admin.css";

export default function CategoryPage() {
  // ====== Danh mục ======
  const [catQuery, setCatQuery] = React.useState({
    keyword: "",
    active: "",
    sort: "displayorder",
    direction: "asc",
  });
  const [categories, setCategories] = React.useState([]);
  const [catLoading, setCatLoading] = React.useState(false);
  const [catPage, setCatPage] = React.useState(1);
  const [catPageSize, setCatPageSize] = React.useState(10);
  const [catTotal, setCatTotal] = React.useState(0);

  const loadCategories = React.useCallback(() => {
    setCatLoading(true);
    const params = { ...catQuery, page: catPage, pageSize: catPageSize };
    if (params.active === "") delete params.active;
    if (!params.sort) params.sort = "displayorder";
    if (!params.direction) params.direction = "asc";

    // Dùng listPaged để lấy {items,total,...}; fallback mảng nếu BE cũ
    CategoryApi.listPaged(params)
      .then((res) => {
        const items = res?.items ?? res ?? [];
        setCategories(items);
        setCatTotal(typeof res?.total === "number" ? res.total : items.length);
      })
      .finally(() => setCatLoading(false));
  }, [catQuery, catPage, catPageSize]);

  // Debounce + load
  React.useEffect(() => {
    const t = setTimeout(loadCategories, 300);
    return () => clearTimeout(t);
  }, [loadCategories]);

  // Reset về trang 1 khi đổi filter/sort
  React.useEffect(() => {
    setCatPage(1);
  }, [catQuery.keyword, catQuery.active, catQuery.sort, catQuery.direction]);

  const catToggle = async (id) => {
    try {
      await CategoryApi.toggle(id);
    } catch (err) {
      console.error(err);
    }
    loadCategories();
  };

  const deleteCategory = async (c) => {
    const ok = window.confirm(
      `Xoá danh mục "${c.categoryName}"? Hành động này không thể hoàn tác!`
    );
    if (!ok) return;
    try {
      await CategoryApi.remove(c.categoryId);
      // reload tổng và trang hiện tại để đồng bộ
      loadCategories();
    } catch (e) {
      alert(e?.response?.data?.message || e.message || "Xoá thất bại");
    }
  };

  // ====== CSV danh mục ======
  const catExportCsv = async () => {
    const blob = await CategoryCsv.exportCsv();
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "categories.csv";
    a.click();
    URL.revokeObjectURL(url);
  };

  const catImportCsv = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const res = await CategoryCsv.importCsv(file);
    alert(
      `Import xong: total=${res.total}, created=${res.created}, updated=${res.updated}`
    );
    e.target.value = "";
    loadCategories();
  };

  // ====== Badges ======
  const [badges, setBadges] = React.useState([]);
  const [badgesLoading, setBadgesLoading] = React.useState(false);
  const [badgeQuery, setBadgeQuery] = React.useState({ keyword: "", active: "" });
  const [badgeSort, setBadgeSort] = React.useState("name");
  const [badgeDirection, setBadgeDirection] = React.useState("asc");
  const [badgePage, setBadgePage] = React.useState(1);
  const [badgePageSize, setBadgePageSize] = React.useState(10);
  const [badgeTotal, setBadgeTotal] = React.useState(0);

  const getBadgeName = (b) =>
    b?.displayName ||
    b?.badgeName ||
    b?.name ||
    b?.BadgeDisplayName ||
    b?.BadgeName ||
    b?.badgeCode ||
    "";

  const getBadgeColor = (b) =>
    b?.colorHex || b?.color || b?.colorhex || b?.ColorHex || "#1e40af";

  const loadBadges = React.useCallback(() => {
    setBadgesLoading(true);
    const params = {
      keyword: badgeQuery.keyword || undefined,
      active: badgeQuery.active || undefined,
      sort: badgeSort,
      direction: badgeDirection,
      page: badgePage,
      pageSize: badgePageSize,
    };
    BadgesApi.listPaged(params)
      .then((res) => {
        const items = res?.items ?? res ?? [];
        setBadges(items);
        setBadgeTotal(typeof res?.total === "number" ? res.total : items.length);
      })
      .finally(() => setBadgesLoading(false));
  }, [badgeQuery, badgeSort, badgeDirection, badgePage, badgePageSize]);

  React.useEffect(() => {
    loadBadges();
  }, [loadBadges]);

  // Reset về trang 1 khi đổi filter/sort
  React.useEffect(() => {
    setBadgePage(1);
  }, [badgeQuery.keyword, badgeQuery.active, badgeSort, badgeDirection]);

  const toggleBadge = async (code) => {
    try {
      await BadgesApi.toggle(code);
      loadBadges();
    } catch (e) {
      console.error(e);
    }
  };

  const deleteBadge = async (b) => {
    const ok = window.confirm(
      `Xoá nhãn "${getBadgeName(b)}" (${b.badgeCode})? Hành động này không thể hoàn tác!`
    );
    if (!ok) return;
    try {
      await BadgesApi.remove(b.badgeCode);
      loadBadges();
    } catch (e) {
      alert(e?.response?.data?.message || e.message || "Xoá thất bại");
    }
  };

  // ====== Helpers: Pagination UI ======
  const Pager = ({
    page,
    pageSize,
    total,
    onPrev,
    onNext,
    onPageSize,
  }) => {
    const pages = Math.max(1, Math.ceil(total / Math.max(1, pageSize)));
    const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
    const to = Math.min(total, page * pageSize);
    return (
      <div className="pager row" style={{ gap: 8, alignItems: "center", marginTop: 8 }}>
        <span className="mono">
          Hiển thị {from}-{to} / {total}
        </span>
        <button className="btn" disabled={page <= 1} onClick={onPrev}>
          ◀ Trang trước
        </button>
        <button className="btn" disabled={page >= pages} onClick={onNext}>
          Trang sau ▶
        </button>
        <span style={{ marginLeft: "auto" }}>Mỗi trang</span>
        <select
          value={pageSize}
          onChange={(e) => onPageSize(parseInt(e.target.value, 10))}
        >
          {[5, 10, 20, 50, 100].map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </select>
      </div>
    );
  };

  return (
    <div className="page">
      {/* ===== Khối: Danh mục ===== */}
      <div className="card">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <h2>Danh mục sản phẩm</h2>
          <div className="row" style={{ gap: 8 }}>
            <label className="btn">
              ⬆ Nhập CSV
              <input type="file" accept=".csv,text/csv" style={{ display: "none" }} onChange={catImportCsv} />
            </label>
            <button className="btn" onClick={catExportCsv}>⬇ Xuất CSV</button>
            <Link className="btn primary" to="/admin/categories/add">+ Thêm danh mục</Link>
          </div>
        </div>

        {/* Bộ lọc giống sản phẩm */}
        <div className="row input-group" style={{ gap: 10, marginTop: 12, flexWrap: "nowrap", alignItems: "end", overflowX: "auto" }}>
          <div className="group" style={{ minWidth: 320, maxWidth: 520 }}>
            <span>Tìm kiếm</span>
            <input
              value={catQuery.keyword}
              onChange={(e) => setCatQuery((s) => ({ ...s, keyword: e.target.value }))}
              placeholder="Tìm theo mã, tên hoặc mô tả…"
            />
          </div>
          <div className="group" style={{ minWidth: 160 }}>
            <span>Trạng thái</span>
            <select
              value={catQuery.active}
              onChange={(e) => setCatQuery((s) => ({ ...s, active: e.target.value }))}
            >
              <option value="">Tất cả</option>
              <option value="true">Hiển thị</option>
              <option value="false">Ẩn</option>
            </select>
          </div>

          {catLoading && <span className="badge gray">Đang tải…</span>}

          <button
            className="btn"
            onClick={() => setCatQuery((s) => ({ ...s, keyword: "", active: "" }))}
            title="Xoá bộ lọc"
          >
            Đặt lại
          </button>
        </div>

        <table className="table" style={{ marginTop: 10 }}>
          <thead>
            <tr>
              <th
                onClick={() =>
                  setCatQuery((s) => ({
                    ...s,
                    sort: "name",
                    direction: s.sort === "name" && s.direction === "asc" ? "desc" : "asc",
                  }))
                }
                style={{ cursor: "pointer" }}
              >
                Tên {catQuery.sort === "name" ? (catQuery.direction === "asc" ? " ▲" : " ▼") : ""}
              </th>
              <th
                onClick={() =>
                  setCatQuery((s) => ({
                    ...s,
                    sort: "code",
                    direction: s.sort === "code" && s.direction === "asc" ? "desc" : "asc",
                  }))
                }
                style={{ cursor: "pointer" }}
              >
                Mã danh mục {catQuery.sort === "code" ? (catQuery.direction === "asc" ? " ▲" : " ▼") : ""}
              </th>
              <th
                onClick={() =>
                  setCatQuery((s) => ({
                    ...s,
                    sort: "displayorder",
                    direction: s.sort === "displayorder" && s.direction === "asc" ? "desc" : "asc",
                  }))
                }
                style={{ cursor: "pointer" }}
              >
                Thứ tự {catQuery.sort === "displayorder" ? (catQuery.direction === "asc" ? " ▲" : " ▼") : ""}
              </th>
              <th>Số sản phẩm</th>
              <th
                onClick={() =>
                  setCatQuery((s) => ({
                    ...s,
                    sort: "active",
                    direction: s.sort === "active" && s.direction === "asc" ? "desc" : "asc",
                  }))
                }
                style={{ cursor: "pointer" }}
              >
                Trạng thái {catQuery.sort === "active" ? (catQuery.direction === "asc" ? " ▲" : " ▼") : ""}
              </th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {categories.map((c) => (
              <tr key={c.categoryId}>
                <td>{c.categoryName}</td>
                <td className="mono">{c.categoryCode}</td>
                <td>{c.displayOrder ?? 0}</td>
                <td>{c.productsCount ?? c.productCount ?? c.products ?? 0}</td>
                <td>
                  <span className={c.isActive ? "badge green" : "badge gray"}>
                    {c.isActive ? "Hiển thị" : "Ẩn"}
                  </span>
                </td>
                <td style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <div className="action-buttons">
                    <Link
                      className="action-btn edit-btn"
                      to={`/admin/categories/${c.categoryId}`}
                      title="Xem chi tiết / chỉnh sửa"
                    >
                      <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor" aria-hidden="true">
                        <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                        <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                      </svg>
                    </Link>
                    <button className="action-btn delete-btn" title="Xoá danh mục" onClick={() => deleteCategory(c)}>
                      <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor" aria-hidden="true">
                        <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                      </svg>
                    </button>
                  </div>
                  <label className="switch" title="Bật/Tắt hiển thị">
                    <input type="checkbox" checked={!!c.isActive} onChange={() => catToggle(c.categoryId)} />
                    <span className="slider" />
                  </label>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {/* Pagination: Categories */}
  <div className="pager">
  <button
    disabled={catPage <= 1}
    onClick={() => setCatPage((p) => Math.max(1, p - 1))}
  >
    Trước
  </button>
  <span style={{ padding: "0 8px" }}>Trang {catPage}</span>
  <button
    disabled={catPage * catPageSize >= catTotal}
    onClick={() => setCatPage((p) => p + 1)}
  >
    Tiếp
  </button>
</div>

      </div>

      {/* ===== Khối: Nhãn sản phẩm ===== */}
      <div className="card" style={{ marginTop: 14 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <h2>Nhãn sản phẩm</h2>
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <Link className="btn primary" to="/admin/badges/add">+ Thêm nhãn</Link>
          </div>
        </div>

        {/* Filter ngay dưới tiêu đề */}
        <div className="row input-group" style={{ gap: 10, marginTop: 12, flexWrap: "nowrap", alignItems: "end", overflowX: "auto" }}>
          <div className="group" style={{ minWidth: 320 }}>
            <span>Tìm kiếm</span>
            <input
              value={badgeQuery.keyword}
              onChange={(e) => setBadgeQuery((s) => ({ ...s, keyword: e.target.value }))}
              placeholder="Tìm theo mã, tên, màu…"
            />
          </div>
          <div className="group" style={{ minWidth: 160 }}>
            <span>Trạng thái</span>
            <select
              value={badgeQuery.active}
              onChange={(e) => setBadgeQuery((s) => ({ ...s, active: e.target.value }))}
            >
              <option value="">Tất cả</option>
              <option value="true">Hiển thị</option>
              <option value="false">Ẩn</option>
            </select>
          </div>

          {badgesLoading && <span className="badge gray">Đang tải…</span>}

          <button className="btn" onClick={() => setBadgeQuery({ keyword: "", active: "" })}>
            Đặt lại
          </button>
        </div>

        <table className="table" style={{ marginTop: 10 }}>
          <thead>
            <tr>
              <th
                onClick={() => {
                  const key = "code";
                  setBadgeSort((prev) => {
                    setBadgeDirection((d) => (prev === key && d === "asc" ? "desc" : "asc"));
                    return key;
                  });
                }}
                style={{ cursor: "pointer" }}
              >
                Mã {badgeSort === "code" ? (badgeDirection === "asc" ? " ▲" : " ▼") : ""}
              </th>
              <th
                onClick={() => {
                  const key = "name";
                  setBadgeSort((prev) => {
                    setBadgeDirection((d) => (prev === key && d === "asc" ? "desc" : "asc"));
                    return key;
                  });
                }}
                style={{ cursor: "pointer" }}
              >
                Tên {badgeSort === "name" ? (badgeDirection === "asc" ? " ▲" : " ▼") : ""}
              </th>

              {/* NEW: Nhãn hiển thị */}
              <th>Nhãn hiển thị</th>

              {/* NEW: Số sản phẩm dùng nhãn */}
              <th
                title="Số sản phẩm đang gắn nhãn này"
              >
                Số SP
              </th>

              <th
                onClick={() => {
                  const key = "color";
                  setBadgeSort((prev) => {
                    setBadgeDirection((d) => (prev === key && d === "asc" ? "desc" : "asc"));
                    return key;
                  });
                }}
                style={{ cursor: "pointer" }}
              >
                Màu {badgeSort === "color" ? (badgeDirection === "asc" ? " ▲" : " ▼") : ""}
              </th>
              <th
                onClick={() => {
                  const key = "active";
                  setBadgeSort((prev) => {
                    setBadgeDirection((d) => (prev === key && d === "asc" ? "desc" : "asc"));
                    return key;
                  });
                }}
                style={{ cursor: "pointer" }}
              >
                Trạng thái {badgeSort === "active" ? (badgeDirection === "asc" ? " ▲" : " ▼") : ""}
              </th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {badges.map((b) => {
              const name = getBadgeName(b);
              const color = getBadgeColor(b);
              const count =
                b.productsCount ??
                b.productCount ??
                b.ProductsCount ??
                0;

              return (
                <tr key={b.badgeCode}>
                  <td className="mono">{b.badgeCode}</td>
                  <td>{name}</td>

                  {/* Nhãn hiển thị: giống chip trong ProductAdd/Detail */}
                  <td>
                    <span
                      className="label-chip"
                      style={{
                        backgroundColor: color,
                        color: "#fff",
                        padding: "4px 10px",
                        borderRadius: 8,
                        fontSize: 12,
                        display: "inline-block",
                      }}
                      title={name}
                    >
                      {name}
                    </span>
                  </td>

                  {/* Số sản phẩm dùng nhãn */}
                  <td className="mono">{count}</td>

                  <td className="mono">{color}</td>
                  <td>
                    <span className={b.isActive ? "badge green" : "badge gray"}>
                      {b.isActive ? "Hiển thị" : "Ẩn"}
                    </span>
                  </td>
                  <td style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <div className="action-buttons">
                      {/* Xem chi tiết (icon bút chì) */}
                      <Link
                        className="action-btn edit-btn"
                        to={`/admin/badges/${encodeURIComponent(b.badgeCode)}`}
                        title="Xem chi tiết / chỉnh sửa"
                      >
                        <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor" aria-hidden="true">
                          <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                          <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                        </svg>
                      </Link>
                      {/* Xoá (icon thùng rác) */}
                      <button
                        className="action-btn delete-btn"
                        title="Xoá nhãn"
                        onClick={() => deleteBadge(b)}
                      >
                        <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor" aria-hidden="true">
                          <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                        </svg>
                      </button>
                    </div>

                    {/* Công tắc bật/tắt nhãn */}
                    <label className="switch" title="Bật/Tắt nhãn">
                      <input type="checkbox" checked={!!b.isActive} onChange={() => toggleBadge(b.badgeCode)} />
                      <span className="slider" />
                    </label>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>

        {/* Pagination: Badges */}
        <div className="pager">
  <button
    disabled={badgePage <= 1}
    onClick={() => setBadgePage((p) => Math.max(1, p - 1))}
  >
    Trước
  </button>
  <span style={{ padding: "0 8px" }}>Trang {badgePage}</span>
  <button
    disabled={badgePage * badgePageSize >= badgeTotal}
    onClick={() => setBadgePage((p) => p + 1)}
  >
    Tiếp
  </button>
</div>

      </div>
    </div>
  );
}
