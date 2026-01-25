// src/pages/admin/ProductsPage.jsx
import React, { useRef } from "react";
import { Link } from "react-router-dom";
import ProductApi from "../../services/products";
import { CategoryApi } from "../../services/categories";
import { BadgesApi } from "../../services/badges";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./admin.css";

export default function ProductsPage() {
  // Permission checks removed - now role-based on backend
  const canViewList = true;
  const permissionLoading = false;
  const canViewDetail = true;
  const canCreate = true;
  const canEdit = true;
  const canDelete = true;

  // Global network error handler
  const networkErrorShownRef = useRef(false);
  // Global permission error handler - only show one toast for permission errors
  const permissionErrorShownRef = useRef(false);
  React.useEffect(() => {
    networkErrorShownRef.current = false;
    permissionErrorShownRef.current = false;
  }, []);

  // ====== Toast & ConfirmDialog ======
  const [toasts, setToasts] = React.useState([]);
  const [confirmDialog, setConfirmDialog] = React.useState(null);
  const toastIdRef = React.useRef(1);

  const removeToast = (id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  const addToast = (type, title, message) => {
    const id = toastIdRef.current++;
    setToasts((prev) => [
      ...prev,
      { id, type, message, title: title || undefined },
    ]);

    // Auto close sau 5 giây
    setTimeout(() => {
      removeToast(id);
    }, 5000);

    return id;
  };

  const openConfirm = ({ title, message, onConfirm }) => {
    setConfirmDialog({
      title,
      message,
      onConfirm: async () => {
        setConfirmDialog(null);
        await onConfirm?.();
      },
      onCancel: () => setConfirmDialog(null),
    });
  };

  // Lấy toast cross-page từ sessionStorage (sau khi Add/Detail lưu xong)
  React.useEffect(() => {
    try {
      const raw = window.sessionStorage.getItem("products:toast");
      if (raw) {
        const t = JSON.parse(raw);
        if (t && t.message) {
          addToast(t.type || "success", t.title, t.message);
        }
        window.sessionStorage.removeItem("products:toast");
      }
    } catch {
      // ignore
    }
    // chỉ chạy 1 lần khi mount
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ====== Query + paging ======
  const [query, setQuery] = React.useState({
    keyword: "",
    categoryId: "",
    type: "",
    status: "",
    badge: "", // Lọc theo 1 badge
    sort: "name",
    direction: "asc",
  });
  const [page, setPage] = React.useState(1);
  const [pageSize] = React.useState(10);
  const [total, setTotal] = React.useState(0);
  const [loading, setLoading] = React.useState(false);
  const [items, setItems] = React.useState([]);

  const [categories, setCategories] = React.useState([]);
  const [categoriesDict, setCategoriesDict] = React.useState({});
  const [badges, setBadges] = React.useState([]);
  const [badgesDict, setBadgesDict] = React.useState({});

  React.useEffect(() => {
    // Danh mục (để render tên)
    CategoryApi.listPaged({ active: true, page: 1, pageSize: 1000 }).then(
      (res) => {
        const list = res?.items ?? [];
        setCategories(list);
        const dict = {};
        for (const c of list)
          dict[c.categoryId] = c.categoryName || `#${c.categoryId}`;
        setCategoriesDict(dict);
      }
    );

    // Nhãn (để hiển thị chip + màu)
    BadgesApi.listPaged({ active: true, page: 1, pageSize: 1000 }).then(
      (res) => {
        const items = res?.items ?? [];
        setBadges(items);
        const dict = {};
        for (const b of items) {
          dict[b.badgeCode] = {
            name: b.displayName || b.badgeCode,
            color: b.colorHex || "#1e40af",
          };
        }
        setBadgesDict(dict);
      }
    );
  }, []);

  const load = React.useCallback(async () => {
    setLoading(true);
    const params = {
      keyword: query.keyword || undefined,
      categoryId: query.categoryId || undefined,
      type: query.type || undefined, // map sang [FromQuery(Name="type")] productType
      status: query.status || undefined,
      badge: query.badge || undefined,
      sort: query.sort || "name",
      direction: query.direction || "asc",
      page,
      pageSize,
    };
    try {
      const res = await ProductApi.list(params);
      const arr = res?.items ?? [];
      setItems(arr);

      // Ưu tiên res.totalItems, fallback arr.length
      const t =
        typeof res?.totalItems === "number" ? res.totalItems : arr.length;
      setTotal(t);
    } catch (err) {
      console.error(err);
      const errorMsg = err?.response?.data?.message || err.message || "Không tải được sản phẩm.";
      
      // Handle network errors globally - only show one toast
      if (err.isNetworkError || err.message === 'Lỗi kết nối đến máy chủ') {
        if (!networkErrorShownRef.current) {
          networkErrorShownRef.current = true;
          addToast("error", "Lỗi kết nối", "Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.");
        }
      } else {
        // Check if error message contains permission denied - only show once
        const isPermissionError = err.message?.includes('không có quyền') || 
                                  err.message?.includes('quyền truy cập') ||
                                  err.response?.status === 403;
        if (isPermissionError && !permissionErrorShownRef.current) {
          permissionErrorShownRef.current = true;
          const errorMsgFinal = err?.response?.data?.message || err.message || "Bạn không có quyền truy cập chức năng này.";
          addToast("error", "Lỗi tải dữ liệu", errorMsgFinal);
        } else if (!isPermissionError) {
          addToast("error", "Lỗi", errorMsg);
        }
      }
    } finally {
      setLoading(false);
    }
  }, [query, page, pageSize]);

  React.useEffect(() => {
    const t = setTimeout(load, 250);
    return () => clearTimeout(t);
  }, [load]);

  React.useEffect(() => {
    setPage(1);
  }, [
    query.keyword,
    query.categoryId,
    query.type,
    query.status,
    query.badge,
    query.sort,
    query.direction,
  ]);

  const TYPES = ProductApi?.types ?? [];
  const STATUSES = ProductApi?.statuses ?? [];

  const fmtType = (t) => ProductApi.typeLabelOf?.(t) || t;
  const fmtStatus = (s) => ProductApi.statusLabelOf?.(s) || s;

  const statusBadge = (s) =>
    s === "ACTIVE"
      ? "badge green"
      : s === "OUT_OF_STOCK"
      ? "badge red"
      : "badge gray";

  const headerSort = (key) => {
    setQuery((q) => ({
      ...q,
      sort: key,
      direction: q.sort === key && q.direction === "asc" ? "desc" : "asc",
    }));
  };

  // Đổi trạng thái từ list, logic theo controller mới (ACTIVE / INACTIVE / OUT_OF_STOCK)
  const toggleStatus = async (p) => {
    if (!canEdit) {
      addToast("error", "Không có quyền", "Bạn không có quyền thay đổi trạng thái sản phẩm.");
      return;
    }
    try {
      const res = await ProductApi.toggle(p.productId);
      const nextRaw = res?.status ?? res?.Status;
      const next = (nextRaw || "").toUpperCase();

      await load();

      if (!next) {
        addToast(
          "success",
          "Thành công",
          "Đã cập nhật trạng thái sản phẩm."
        );
        return;
      }

      if (next === "ACTIVE") {
        addToast(
          "success",
          "Trạng thái hiển thị",
          `Sản phẩm "${p.productName}" đã được bật hiển thị.`
        );
      } else if (next === "INACTIVE") {
        addToast(
          "info",
          "Trạng thái hiển thị",
          `Sản phẩm "${p.productName}" đã được ẩn khỏi trang bán.`
        );
      } else if (next === "OUT_OF_STOCK") {
        const totalStock = p.totalStock ?? 0;
        const msg =
          totalStock <= 0
            ? `Sản phẩm "${p.productName}" hiện đang hết hàng nên sẽ hiển thị với trạng thái "Hết hàng". Khách vẫn xem được nhưng không thể mua cho đến khi bạn nhập thêm tồn kho.`
            : `Trạng thái sản phẩm "${p.productName}" đang là "Hết hàng".`;
        addToast("info", "Trạng thái hết hàng", msg);
      } else {
        addToast(
          "success",
          "Thành công",
          "Đã cập nhật trạng thái sản phẩm."
        );
      }
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        "Lỗi",
        e?.response?.data?.message || "Đổi trạng thái thất bại."
      );
    }
  };

  // Xoá sản phẩm: để BE quyết định chặn (409) nếu còn variant/đơn hàng
  const deleteProduct = (p) => {
    openConfirm({
      title: "Xoá sản phẩm?",
      message: `Xoá sản phẩm "${p.productName}"? Hành động này không thể hoàn tác!`,
      onConfirm: async () => {
        try {
          await ProductApi.remove(p.productId);
          addToast("success", "Thành công", "Đã xoá sản phẩm.");
          await load();
        } catch (e) {
          console.error(e);

          const status = e?.response?.status;
          const data = e?.response?.data || {};
          const msg =
            data.message || e.message || "Xoá sản phẩm thất bại.";

          if (status === 409) {
            // Bị chặn vì đã có biến thể / đơn hàng... => hiển thị message từ server
            addToast("warning", "Không thể xoá sản phẩm", msg);
          } else {
            addToast("error", "Lỗi", msg);
          }
        }
      },
    });
  };

  // Show loading while checking permission
  if (permissionLoading) {
    return (
      <div className="page">
        <div className="card">
          <h2>Danh sách sản phẩm</h2>
          <div style={{ padding: "20px", textAlign: "center" }}>
            Đang kiểm tra quyền truy cập...
          </div>
        </div>
      </div>
    );
  }

  // Show access denied message if no VIEW_LIST permission
  if (!canViewList) {
    return (
      <div className="page">
        <div className="card">
          <h2>Danh sách sản phẩm</h2>
          <div style={{ padding: "20px" }}>
            <h2>Không có quyền truy cập</h2>
            <p>Bạn không có quyền xem danh sách sản phẩm. Vui lòng liên hệ quản trị viên để được cấp quyền.</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <>
      <div className="page">
        <div className="card">
          <div
            className="row"
            style={{
              justifyContent: "space-between",
              alignItems: "center",
            }}
          >
            <h2>Danh sách sản phẩm</h2>
            <Link 
              className="btn primary" 
              to="/admin/products/add"
              onClick={(e) => {
                if (!canCreate) {
                  e.preventDefault();
                  addToast("error", "Không có quyền", "Bạn không có quyền tạo sản phẩm mới.");
                }
              }}
            >
              + Thêm sản phẩm
            </Link>
          </div>

          {/* Filters */}
          <div className="filter-inline input-group" style={{ marginTop: 12 }}>
            <div className="group">
              <span>Tìm kiếm</span>
              <input
                value={query.keyword}
                onChange={(e) =>
                  setQuery((s) => ({
                    ...s,
                    keyword: e.target.value,
                  }))
                }
                placeholder="Tìm theo tên hoặc mã…"
              />
            </div>

            <div className="group w-180">
              <span>Danh mục sản phẩm</span>
              <select
                value={query.categoryId}
                onChange={(e) =>
                  setQuery((s) => ({
                    ...s,
                    categoryId: e.target.value,
                  }))
                }
              >
                <option value="">Tất cả</option>
                {categories.map((c) => (
                  <option key={c.categoryId} value={c.categoryId}>
                    {c.categoryName}
                  </option>
                ))}
              </select>
            </div>

            <div className="group w-180">
              <span>Loại sản phẩm</span>
              <select
                value={query.type}
                onChange={(e) =>
                  setQuery((s) => ({
                    ...s,
                    type: e.target.value,
                  }))
                }
              >
                <option value="">Tất cả</option>
                {TYPES.map((t) => (
                  <option key={t.value} value={t.value}>
                    {t.label}
                  </option>
                ))}
              </select>
            </div>

            <div className="group w-160">
              <span>Trạng thái</span>
              <select
                value={query.status}
                onChange={(e) =>
                  setQuery((s) => ({
                    ...s,
                    status: e.target.value,
                  }))
                }
              >
                <option value="">Tất cả</option>
                {STATUSES.map((s) => (
                  <option key={s.value} value={s.value}>
                    {s.label}
                  </option>
                ))}
              </select>
            </div>

            {/* Lọc theo nhãn */}
            <div className="group w-180">
              <span>Nhãn sản phẩm</span>
              <select
                value={query.badge}
                onChange={(e) =>
                  setQuery((s) => ({
                    ...s,
                    badge: e.target.value,
                  }))
                }
              >
                <option value="">Tất cả</option>
                {badges.map((b) => (
                  <option key={b.badgeCode} value={b.badgeCode}>
                    {b.displayName || b.badgeCode}
                  </option>
                ))}
              </select>
            </div>
            {loading && <span className="badge gray">Đang tải…</span>}
            <button
              className="btn"
              onClick={() =>
                setQuery({
                  keyword: "",
                  categoryId: "",
                  type: "",
                  status: "",
                  badge: "",
                  sort: "name",
                  direction: "asc",
                })
              }
              title="Xoá bộ lọc"
            >
              Đặt lại
            </button>
          </div>

          {/* Table */}
          <table className="table" style={{ marginTop: 10 }}>
            <thead>
              <tr>
                <th
                  onClick={() => headerSort("name")}
                  style={{ cursor: "pointer" }}
                >
                  Tên{" "}
                  {query.sort === "name"
                    ? query.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th
                  onClick={() => headerSort("type")}
                  style={{ cursor: "pointer" }}
                >
                  Loại{" "}
                  {query.sort === "type"
                    ? query.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th
                  onClick={() => headerSort("stock")}
                  style={{ cursor: "pointer" }}
                >
                  Tồn kho (tổng){" "}
                  {query.sort === "stock"
                    ? query.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th>Danh mục</th>
                <th>Nhãn</th>
                <th
                  className="col-status"
                  onClick={() => headerSort("status")}
                  style={{ cursor: "pointer" }}
                >
                  Trạng thái{" "}
                  {query.sort === "status"
                    ? query.direction === "asc"
                      ? " ▲"
                      : " ▼"
                    : ""}
                </th>
                <th>Thao tác</th>
              </tr>
            </thead>

            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={7}>
                    <div className="table-loading">
                      Đang tải danh sách sản phẩm…
                    </div>
                  </td>
                </tr>
              ) : (items ?? []).length === 0 ? (
                <tr>
                  <td colSpan={7}>
                    <div className="empty-state-center">
                      <h3>Không có sản phẩm phù hợp</h3>
                      <p style={{ marginTop: 4, marginBottom: 12 }}>
                        Hãy thử nới lỏng bộ lọc hoặc thêm sản phẩm mới.
                      </p>
                    </div>
                  </td>
                </tr>
              ) : (
                (items ?? []).map((p) => (
                  <tr key={p.productId}>
                    <td>{p.productName}</td>
                    <td>{fmtType(p.productType)}</td>

                    {/* Tổng tồn kho */}
                    <td className="mono">{p.totalStock ?? 0}</td>

                    {/* Danh mục */}
                    <td style={{ maxWidth: 360 }}>
                      {(p.categoryIds ?? []).length === 0
                        ? "—"
                        : (p.categoryIds ?? []).map((cid, idx, arr) => {
                            const name =
                              categoriesDict[cid] ?? `#${cid}`;
                            return (
                              <React.Fragment key={cid}>
                                <span className="chip">{name}</span>
                                {idx < arr.length - 1 ? (
                                  <span>,&nbsp;</span>
                                ) : null}
                              </React.Fragment>
                            );
                          })}
                    </td>

                    {/* Nhãn */}
                    <td style={{ maxWidth: 360 }}>
                      {(p.badges ?? []).map((code) => {
                        const meta = badgesDict[code];

                        // Nếu badge không có trong dict (tức đã bị ẩn / không tồn tại) -> không hiển thị
                        if (!meta) return null;

                        return (
                          <span
                            key={code}
                            className="label-chip"
                            style={{
                              background: meta.color,
                              color: "#fff",
                              marginRight: 6,
                              marginBottom: 4,
                            }}
                            title={meta.name}
                          >
                            {meta.name}
                          </span>
                        );
                      })}
                    </td>
                    <td className="col-status">
                      <span
                        className={statusBadge(p.status)}
                        style={{ textTransform: "none" }}
                      >
                        {fmtStatus(p.status)}
                      </span>
                    </td>

                    {/* Thao tác */}
                    <td
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 8,
                      }}
                    >
                      <div className="action-buttons">
                        <Link
                          className="action-btn edit-btn"
                          to={`/admin/products/${p.productId}`}
                          title="Chi tiết / Gói sản phẩm"
                          onClick={(e) => {
                            if (!canViewDetail) {
                              e.preventDefault();
                              addToast("error", "Không có quyền", "Bạn không có quyền xem chi tiết sản phẩm.");
                            }
                          }}
                        >
                          <svg
                            viewBox="0 0 24 24"
                            fill="currentColor"
                            aria-hidden="true"
                          >
                            <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                            <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                          </svg>
                        </Link>
                        <button
                          className="action-btn delete-btn"
                          title="Xoá sản phẩm"
                          onClick={() => deleteProduct(p)}
                        >
                          <svg
                            viewBox="0 0 24 24"
                            fill="currentColor"
                            aria-hidden="true"
                          >
                            <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                          </svg>
                        </button>
                      </div>

                      <label
                        className="switch"
                        title="Bật/Tắt hiển thị"
                      >
                        <input
                          type="checkbox"
                          checked={p.status === "ACTIVE"}
                          onChange={() => toggleStatus(p)}
                        />
                        <span className="slider" />
                      </label>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          {/* Pager */}
          <div className="pager">
            <button
              disabled={page <= 1}
              onClick={() => setPage((x) => Math.max(1, x - 1))}
            >
              Trước
            </button>
            <span style={{ padding: "0 8px" }}>Trang {page}</span>
            <button
              disabled={page * pageSize >= total}
              onClick={() => setPage((x) => x + 1)}
            >
              Tiếp
            </button>
          </div>
        </div>
      </div>

      {/* Toast + Confirm Dialog */}
      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </>
  );
}
