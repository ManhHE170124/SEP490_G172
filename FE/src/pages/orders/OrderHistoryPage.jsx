import React from "react";
import { useNavigate } from "react-router-dom";
import { orderApi } from "../../services/orderApi";
import "./OrderHistoryPage.css";

const PAGE_SIZE = 25;

const initialFilters = {
  orderCode: "",
  amountFrom: "",
  amountTo: "",
  fromDate: "",
  toDate: "",
  
};

const formatCurrency = (value) =>
  typeof value === "number"
    ? new Intl.NumberFormat("vi-VN", {
        style: "currency",
        currency: "VND",
        maximumFractionDigits: 0,
      }).format(value)
    : "-";

const formatDateTime = (value) => {
  if (!value) {
    return "-";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value ?? "-";
  }
  return date.toLocaleString("vi-VN", {
    hour12: false,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
};

export default function OrderHistoryPage() {
  const navigate = useNavigate();
  const [sort, setSort] = React.useState({
    key: "createdAt",
    direction: "desc",
  });
  const [page, setPage] = React.useState(1);
  const [filters, setFilters] = React.useState(initialFilters);
  const [debouncedFilters, setDebouncedFilters] = React.useState(initialFilters);
  const [errors, setErrors] = React.useState({});
  const [orders, setOrders] = React.useState([]);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState(null);

  // Get user info from localStorage
  const userInfo = React.useMemo(() => {
    try {
      const userStr = localStorage.getItem("user");
      if (!userStr) return null;
      return JSON.parse(userStr);
    } catch (error) {
      console.error("Failed to parse user from localStorage:", error);
      return null;
    }
  }, []);

  // Get userId from localStorage
  const userId = React.useMemo(() => {
    if (!userInfo) return null;
    return userInfo.userId || userInfo.UserId || userInfo.id || null;
  }, [userInfo]);

  // Validate filters
  const validateFilters = React.useCallback((filterValues) => {
    const newErrors = {};

    // Validate amount from
    if (filterValues.amountFrom) {
      const amountFrom = Number.parseFloat(
        filterValues.amountFrom.replaceAll(",", "")
      );
      if (Number.isNaN(amountFrom) || amountFrom < 0) {
        newErrors.amountFrom = "Số tiền phải là số dương";
      }
    }

    // Validate amount to
    if (filterValues.amountTo) {
      const amountTo = Number.parseFloat(
        filterValues.amountTo.replaceAll(",", "")
      );
      if (Number.isNaN(amountTo) || amountTo < 0) {
        newErrors.amountTo = "Số tiền phải là số dương";
      }
    }

    // Validate amount range (to >= from)
    if (
      filterValues.amountFrom &&
      filterValues.amountTo &&
      !newErrors.amountFrom &&
      !newErrors.amountTo
    ) {
      const amountFrom = Number.parseFloat(
        filterValues.amountFrom.replaceAll(",", "")
      );
      const amountTo = Number.parseFloat(
        filterValues.amountTo.replaceAll(",", "")
      );
      if (amountTo < amountFrom) {
        newErrors.amountTo = "Số tiền đến phải lớn hơn hoặc bằng số tiền từ";
      }
    }

    // Validate date range
    const today = new Date();
    today.setHours(23, 59, 59, 999); // End of today

    if (filterValues.fromDate) {
      const fromDate = new Date(filterValues.fromDate);
      fromDate.setHours(0, 0, 0, 0);
      if (fromDate > today) {
        newErrors.fromDate = "Từ ngày không được lớn hơn ngày hiện tại";
      }
    }

    if (filterValues.toDate) {
      const toDate = new Date(filterValues.toDate);
      toDate.setHours(23, 59, 59, 999);
      if (toDate > today) {
        newErrors.toDate = "Đến ngày không được lớn hơn ngày hiện tại";
      }
    }

    // Validate date range (to >= from)
    if (
      filterValues.fromDate &&
      filterValues.toDate &&
      !newErrors.fromDate &&
      !newErrors.toDate
    ) {
      const fromDate = new Date(filterValues.fromDate);
      fromDate.setHours(0, 0, 0, 0);
      const toDate = new Date(filterValues.toDate);
      toDate.setHours(0, 0, 0, 0);
      if (toDate < fromDate) {
        newErrors.toDate = "Đến ngày phải lớn hơn hoặc bằng từ ngày";
      }
    }

    return newErrors;
  }, []);

  // Validate and debounce filters for text inputs
  React.useEffect(() => {
    const timer = setTimeout(() => {
      const validationErrors = validateFilters(filters);
      setErrors(validationErrors);
      // Only apply filters if no errors
      if (Object.keys(validationErrors).length === 0) {
        setDebouncedFilters(filters);
        setPage(1); // Reset to first page when filters change
      }
    }, 300);
    return () => clearTimeout(timer);
  }, [filters.orderCode, filters.amountFrom, filters.amountTo, validateFilters]);

  // Validate and update debounced filters immediately for date changes
  React.useEffect(() => {
    const validationErrors = validateFilters(filters);
    setErrors(validationErrors);
    // Only apply filters if no errors
    if (Object.keys(validationErrors).length === 0) {
      setDebouncedFilters((prev) => ({
        ...prev,
        fromDate: filters.fromDate,
        toDate: filters.toDate,
      }));
      setPage(1);
    }
  }, [filters.fromDate, filters.toDate, filters, validateFilters]);

  // Fetch order history from API
  React.useEffect(() => {
    const fetchOrders = async () => {
      // Don't fetch if no userId
      if (!userId) {
        setLoading(false);
        setOrders([]);
        return;
      }

      try {
        setLoading(true);
        setError(null);
        const response = await orderApi.history(userId);
        // axiosClient interceptor already unwraps response.data, so response is already the data
        const data = Array.isArray(response) ? response : (response?.data || []);
        
        console.log("Order history API response:", response);
        console.log("Current userId:", userId);
        
        // Map API response to component format (already filtered by backend)
        const mappedOrders = data.map((order) => ({
          orderId: order.orderId || order.OrderId,
          orderNumber: order.orderNumber || order.OrderNumber || "",
          totalAmount: order.totalAmount || order.TotalAmount || 0,
          finalAmount: order.finalAmount ?? order.FinalAmount ?? null,
          status: order.status || order.Status || "Pending",
          createdAt: order.createdAt || order.CreatedAt,
          itemCount: order.itemCount ?? order.ItemCount ?? 0,
          productNames: order.productNames || order.ProductNames || [],
          thumbnailUrl: order.thumbnailUrl || order.ThumbnailUrl || null,
          paymentStatus: order.paymentStatus || order.PaymentStatus || "Unpaid",
          // Computed fields
          statusLabel: order.status === "Paid" ? "Đã xử lý" : 
                      order.status === "Processing" ? "Đang xử lý" :
                      order.status === "Pending" ? "Đang chờ" :
                      order.status === "Cancelled" ? "Đã hủy" :
                      order.status === "Refunded" ? "Đã hoàn" : "Đang xử lý",
          statusClass: order.status === "Completed" ? "processed" : 
                      order.status === "Cancelled" ? "cancelled" :
                      order.status === "Refunded" ? "refunded" : "processing",
          sortQuantity: order.itemCount ?? order.ItemCount ?? order.productNames?.length ?? 0,
          sortProduct: order.productNames?.[0] ?? order.ProductNames?.[0] ?? "",
          sortTotal:
            typeof (order.finalAmount ?? order.FinalAmount) === "number"
              ? (order.finalAmount ?? order.FinalAmount)
              : (order.totalAmount || order.TotalAmount || 0),
        }));
        
        setOrders(mappedOrders);
      } catch (err) {
        console.error("Failed to fetch order history:", err);
        setError(err.response?.data?.message || err.message || "Không thể tải lịch sử đơn hàng");
        setOrders([]);
      } finally {
        setLoading(false);
      }
    };

    fetchOrders();
  }, [userId]);

  const filteredOrders = React.useMemo(() => {
    return orders.filter((order) => {
      // Filter by order code
      if (
        debouncedFilters.orderCode &&
        !order.orderNumber
          .toLowerCase()
          .includes(debouncedFilters.orderCode.toLowerCase())
      ) {
        return false;
      }

      // Filter by amount range
      const finalAmount =
        typeof order.finalAmount === "number"
          ? order.finalAmount
          : order.totalAmount;

      if (debouncedFilters.amountFrom) {
        const from = Number.parseFloat(
          debouncedFilters.amountFrom.replaceAll(",", "")
        );
        if (!Number.isNaN(from) && finalAmount < from) {
          return false;
        }
      }

      if (debouncedFilters.amountTo) {
        const to = Number.parseFloat(
          debouncedFilters.amountTo.replaceAll(",", "")
        );
        if (!Number.isNaN(to) && finalAmount > to) {
          return false;
        }
      }

      // Filter by date range
      if (debouncedFilters.fromDate) {
        const fromDate = new Date(debouncedFilters.fromDate);
        fromDate.setHours(0, 0, 0, 0);
        const orderDate = new Date(order.createdAt);
        orderDate.setHours(0, 0, 0, 0);
        if (orderDate < fromDate) {
          return false;
        }
      }

      if (debouncedFilters.toDate) {
        const toDate = new Date(debouncedFilters.toDate);
        toDate.setHours(23, 59, 59, 999);
        const orderDate = new Date(order.createdAt);
        if (orderDate > toDate) {
          return false;
        }
      }

      return true;
    });
  }, [orders, debouncedFilters]);

  const sortedOrders = React.useMemo(() => {
    const copy = [...filteredOrders];
    const dir = sort.direction === "asc" ? 1 : -1;
    copy.sort((a, b) => {
      switch (sort.key) {
        case "orderNumber":
          return a.orderNumber.localeCompare(b.orderNumber) * dir;
        case "product":
          return a.sortProduct.localeCompare(b.sortProduct) * dir;
        case "quantity":
          return (a.sortQuantity - b.sortQuantity) * dir;
        case "totalAmount":
          return (a.sortTotal - b.sortTotal) * dir;
        case "status":
          return a.statusLabel.localeCompare(b.statusLabel) * dir;
        case "createdAt":
        default:
          return (
            (new Date(a.createdAt).getTime() -
              new Date(b.createdAt).getTime()) *
            dir
          );
      }
    });
    return copy;
  }, [filteredOrders, sort]);

  const totalPages = Math.max(1, Math.ceil(sortedOrders.length / PAGE_SIZE));
  const firstIndex = (page - 1) * PAGE_SIZE;
  const displayedOrders = sortedOrders.slice(
    firstIndex,
    firstIndex + PAGE_SIZE
  );

  const changeSort = (key) => {
    setSort((prev) => {
      if (prev.key === key) {
        return {
          key,
          direction: prev.direction === "asc" ? "desc" : "asc",
        };
      }
      setPage(1);
      return { key, direction: "asc" };
    });
  };

  const goToPage = (next) => {
    setPage((current) => Math.min(Math.max(1, next), totalPages));
  };

  const resetFilters = () => {
    setFilters(initialFilters);
    setDebouncedFilters(initialFilters);
    setErrors({});
    setSort({ key: "createdAt", direction: "desc" });
    setPage(1);
  };

  // Get today's date in YYYY-MM-DD format for max attribute
  const todayStr = React.useMemo(() => {
    const today = new Date();
    const year = today.getFullYear();
    const month = String(today.getMonth() + 1).padStart(2, "0");
    const day = String(today.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
  }, []);

  return (
    <div className="oh-wrapper">
      {/* <header className="oh-header">
        <div>
          <h1>Lịch sử đơn hàng</h1>
          <p>Hiển thị thông tin các sản phẩm bạn đã mua tại Keytietkiem.</p>
          {userInfo && (
            <div className="oh-user-info">
              <p className="oh-user-name">Xin chào, {userInfo.fullName || userInfo.username || "Người dùng"}!</p>
              {userInfo.email && <p className="oh-user-email">{userInfo.email}</p>}
              {userInfo.phone && <p className="oh-user-phone">{userInfo.phone}</p>}
            </div>
          )}
        </div>
      </header> */}

      <section className="oh-filters">
        <div className="oh-filter-field">
          <input
            placeholder="Mã đơn hàng"
            value={filters.orderCode}
            onChange={(e) =>
              setFilters((prev) => ({ ...prev, orderCode: e.target.value }))
            }
            className={errors.orderCode ? "oh-error" : ""}
          />
          {errors.orderCode && (
            <span className="oh-error-msg">{errors.orderCode}</span>
          )}
        </div>
        <div className="oh-filter-field">
          <input
            placeholder="Số tiền từ"
            type="number"
            min="0"
            value={filters.amountFrom}
            onChange={(e) =>
              setFilters((prev) => ({ ...prev, amountFrom: e.target.value }))
            }
            className={errors.amountFrom ? "oh-error" : ""}
          />
          {errors.amountFrom && (
            <span className="oh-error-msg">{errors.amountFrom}</span>
          )}
        </div>
        <div className="oh-filter-field">
          <input
            placeholder="Số tiền đến"
            type="number"
            min="0"
            value={filters.amountTo}
            onChange={(e) =>
              setFilters((prev) => ({ ...prev, amountTo: e.target.value }))
            }
            className={errors.amountTo ? "oh-error" : ""}
          />
          {errors.amountTo && (
            <span className="oh-error-msg">{errors.amountTo}</span>
          )}
        </div>
        <div className="oh-filter-group">
          <label htmlFor="oh-from-date">Từ ngày</label>
          <div className="oh-filter-field">
            <input
              id="oh-from-date"
              type="date"
              max={todayStr}
              value={filters.fromDate}
              onChange={(e) =>
                setFilters((prev) => ({ ...prev, fromDate: e.target.value }))
              }
              className={errors.fromDate ? "oh-error" : ""}
            />
            {errors.fromDate && (
              <span className="oh-error-msg">{errors.fromDate}</span>
            )}
          </div>
        </div>
        <div className="oh-filter-group">
          <label htmlFor="oh-to-date">Đến ngày</label>
          <div className="oh-filter-field">
            <input
              id="oh-to-date"
              type="date"
              max={todayStr}
              value={filters.toDate}
              onChange={(e) =>
                setFilters((prev) => ({ ...prev, toDate: e.target.value }))
              }
              className={errors.toDate ? "oh-error" : ""}
            />
            {errors.toDate && (
              <span className="oh-error-msg">{errors.toDate}</span>
            )}
          </div>
        </div>
        <button
          type="button"
          className="oh-reset-btn"
          onClick={resetFilters}
        >
          Đặt lại
        </button>
      </section>

      <section className="oh-table-card">
        {loading ? (
          <div style={{ padding: "2rem", textAlign: "center" }}>
            <p>Đang tải dữ liệu...</p>
          </div>
        ) : error ? (
          <div style={{ padding: "2rem", textAlign: "center" }}>
            <p style={{ color: "red" }}>{error}</p>
            <button
              type="button"
              onClick={() => window.location.reload()}
              style={{ marginTop: "1rem", padding: "0.5rem 1rem" }}
            >
              Thử lại
            </button>
          </div>
        ) : displayedOrders.length === 0 ? (
          <div style={{ padding: "2rem", textAlign: "center" }}>
            <p>Không có đơn hàng nào</p>
          </div>
        ) : (
          <>
            <table className="oh-table">
              <thead>
                <tr>
                  <th>
                    <button
                      type="button"
                      className={`oh-sort ${
                        sort.key === "createdAt" ? `active ${sort.direction}` : ""
                      }`}
                      onClick={() => changeSort("createdAt")}
                    >
                      Thời gian
                    </button>
                  </th>
                  <th>
                    <button
                      type="button"
                      className={`oh-sort ${
                        sort.key === "orderNumber" ? `active ${sort.direction}` : ""
                      }`}
                      onClick={() => changeSort("orderNumber")}
                    >
                      Mã đơn hàng
                    </button>
                  </th>
                  <th>
                    <button
                      type="button"
                      className={`oh-sort ${
                        sort.key === "product" ? `active ${sort.direction}` : ""
                      }`}
                      onClick={() => changeSort("product")}
                    >
                      Sản phẩm
                    </button>
                  </th>
                  <th>
                    <button
                      type="button"
                      className={`oh-sort ${
                        sort.key === "quantity" ? `active ${sort.direction}` : ""
                      }`}
                      onClick={() => changeSort("quantity")}
                    >
                      Số lượng
                    </button>
                  </th>
                  <th className="oh-right">
                    <button
                      type="button"
                      className={`oh-sort ${
                        sort.key === "totalAmount" ? `active ${sort.direction}` : ""
                      }`}
                      onClick={() => changeSort("totalAmount")}
                    >
                      Tổng tiền
                    </button>
                  </th>
                  <th className="oh-center">
                    <button
                      type="button"
                      className={`oh-sort ${
                        sort.key === "status" ? `active ${sort.direction}` : ""
                      }`}
                      onClick={() => changeSort("status")}
                    >
                      Trạng thái
                    </button>
                  </th>
                  <th className="oh-center">Chi tiết</th>
                </tr>
              </thead>
              <tbody>
                {displayedOrders.map((order) => {
                  const quantity = order.sortQuantity;
                  const finalAmount = order.sortTotal;

                  return (
                    <tr key={order.orderId}>
                      <td>{formatDateTime(order.createdAt)}</td>
                      <td>
                        <span className="oh-code">{order.orderNumber}</span>
                      </td>
                      <td>
                        <div className="oh-product">
                          <span>{order.sortProduct || "—"}</span>
                          {quantity > 1 ? (
                            <small>+ {quantity - 1} sản phẩm khác</small>
                          ) : null}
                        </div>
                      </td>
                      <td>x{quantity}</td>
                      <td className="oh-right">{formatCurrency(finalAmount)}</td>
                      <td className="oh-center">
                        <span className={`oh-status ${order.statusClass}`}>
                          {order.statusLabel}
                        </span>
                      </td>
                      <td className="oh-center">
                        <button
                          type="button"
                          className="oh-link"
                          onClick={() => navigate(`/orders/${order.orderId}`)}
                        >
                          Chi tiết
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            <footer className="oh-pagination">
              <button
                type="button"
                onClick={() => goToPage(page - 1)}
                disabled={page === 1}
              >
                Trước
              </button>
              <span>
                Trang {page}/{totalPages}
              </span>
              <button
                type="button"
                onClick={() => goToPage(page + 1)}
                disabled={page === totalPages}
              >
                Tiếp
              </button>
            </footer>
          </>
        )}
      </section>
    </div>
  );
}

