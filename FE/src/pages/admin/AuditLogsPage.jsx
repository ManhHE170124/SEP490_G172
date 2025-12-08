// src/pages/admin/AuditLogsPage.jsx
import React from "react";
import { AuditLogsApi } from "../../services/auditLogs";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./AuditLogsPage.css";

/* ============ Helpers: Ellipsis cell ============ */
const EllipsisCell = ({ children, title, maxWidth = 260, mono = false }) => (
  <div
    className={mono ? "mono" : undefined}
    title={title ?? (typeof children === "string" ? children : "")}
    style={{
      maxWidth,
      whiteSpace: "nowrap",
      overflow: "hidden",
      textOverflow: "ellipsis",
    }}
  >
    {children}
  </div>
);

/* ============ Helpers: Sortable header ============ */
const SortableHeader = ({ label, field, sortBy, sortDirection, onSort }) => {
  const isActive = sortBy === field;
  const arrow = !isActive ? "↕" : sortDirection === "asc" ? "↑" : "↓";

  return (
    <button
      type="button"
      className="sortable-header"
      onClick={() => onSort(field)}
    >
      <span>{label}</span>
      <span className="sort-arrow">{arrow}</span>
    </button>
  );
};

/* ============ Modal: Audit Log Detail ============ */
function AuditLogDetailModal({ open, log, detail, loading, onClose }) {
  const [parsedBefore, setParsedBefore] = React.useState(null);
  const [parsedAfter, setParsedAfter] = React.useState(null);

  React.useEffect(() => {
    if (!detail) {
      setParsedBefore(null);
      setParsedAfter(null);
      return;
    }

    const tryParse = (json) => {
      if (!json) return null;
      try {
        return JSON.parse(json);
      } catch {
        return null;
      }
    };

    setParsedBefore(tryParse(detail.beforeDataJson));
    setParsedAfter(tryParse(detail.afterDataJson));
  }, [detail]);

  if (!open || !log) return null;

  const item = detail || log;

  const formatDateTime = (value) => {
    if (!value) return "-";
    try {
      const d = new Date(value);
      if (Number.isNaN(d.getTime())) return value;
      return d.toLocaleString("vi-VN");
    } catch {
      return value;
    }
  };

  return (
    <div className="cat-modal-backdrop">
      <div className="cat-modal-card audit-detail-card">
        <div className="cat-modal-header">
          <div className="audit-detail-title">
            {/* Đổi title, bỏ hiển thị id và mô tả nhỏ */}
            <h3>Chi tiết thao tác hệ thống</h3>
          </div>
          <button type="button" className="btn ghost small" onClick={onClose}>
            Đóng
          </button>
        </div>

        {loading && (
          <div style={{ padding: 8 }}>
            <span className="badge gray">Đang tải chi tiết…</span>
          </div>
        )}

        <div className="cat-modal-body audit-detail-body">
          <div className="grid cols-2 input-group audit-detail-grid">
            <div className="group">
              <span>Thời gian</span>
              <div className="mono strong">{formatDateTime(item.occurredAt)}</div>
            </div>
            <div className="group">
              <span>Hành động</span>
              <div className="mono">{item.action || "-"}</div>
            </div>

            <div className="group">
              <span>Đối tượng</span>
              <div className="mono">
                {item.entityType || "-"}{" "}
                {item.entityId ? `(${item.entityId})` : ""}
              </div>
            </div>
            <div className="group">
              <span>Người thao tác</span>
              <div className="mono">
                {item.actorEmail || "-"}
                {item.actorId ? ` (${item.actorId})` : ""}
              </div>
            </div>

            <div className="group">
              <span>Vai trò</span>
              <div className="mono">{item.actorRole || "-"}</div>
            </div>
            <div className="group">
              <span>Mã phiên</span>
              <div className="mono wrap">{item.sessionId || "-"}</div>
            </div>

            <div className="group">
              <span>Địa chỉ IP</span>
              <div className="mono">{item.ipAddress || "-"}</div>
            </div>

            <div className="group" style={{ gridColumn: "1 / 3" }}>
              <span>Thông tin trình duyệt</span>
              <div className="mono wrap">{item.userAgent || "-"}</div>
            </div>
          </div>

          <div className="audit-json-columns">
            <div className="group">
              <span>Dữ liệu trước</span>
              <pre className="audit-json">
                {parsedBefore
                  ? JSON.stringify(parsedBefore, null, 2)
                  : detail?.beforeDataJson || "(empty)"}
              </pre>
            </div>
            <div className="group">
              <span>Dữ liệu sau</span>
              <pre className="audit-json">
                {parsedAfter
                  ? JSON.stringify(parsedAfter, null, 2)
                  : detail?.afterDataJson || "(empty)"}
              </pre>
            </div>
          </div>
        </div>

        {/* Bỏ nút Đóng ở footer, chỉ dùng nút trên header */}
        {/* <div className="cat-modal-footer" ...> */}
      </div>
    </div>
  );
}

/* ============ MAIN PAGE ============ */
export default function AuditLogsPage() {
  // ===== Toast & Confirm =====
  const [toasts, setToasts] = React.useState([]);
  const toastIdRef = React.useRef(1);
  const [confirmDialog, setConfirmDialog] = React.useState(null);

  const removeToast = (id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  const addToast = (type, message, title) => {
    const id = toastIdRef.current++;
    setToasts((prev) => [...prev, { id, type, message, title }]);
    setTimeout(() => removeToast(id), 5000);
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

  // ===== Query / Pagination =====
  const [query, setQuery] = React.useState({
    actorEmail: "", // dùng như ô tìm kiếm chung
    actorRole: "",
    action: "",
    entityType: "",
    from: "",
    to: "",
  });

  const [logs, setLogs] = React.useState([]);
  const [loading, setLoading] = React.useState(false);
  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(20);
  const [total, setTotal] = React.useState(0);

  const [selectedLog, setSelectedLog] = React.useState(null);
  const [detail, setDetail] = React.useState(null);
  const [detailLoading, setDetailLoading] = React.useState(false);

  // ===== Options cho dropdown (lấy từ /api/auditlogs/options) =====
  const [options, setOptions] = React.useState({
    actions: [],
    entityTypes: [],
    actorRoles: [],
  });

  // ===== Sort state =====
  const [sortBy, setSortBy] = React.useState("OccurredAt");
  const [sortDirection, setSortDirection] = React.useState("desc");

  React.useEffect(() => {
    AuditLogsApi.getFilterOptions()
      .then((res) => {
        setOptions({
          actions: res.actions || [],
          entityTypes: res.entityTypes || [],
          actorRoles: res.actorRoles || [],
        });
      })
      .catch((err) => {
        console.error(err);
        addToast(
          "error",
          "Không tải được danh sách bộ lọc (hành động, đối tượng, vai trò).",
          "Lỗi"
        );
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadLogs = React.useCallback(() => {
    setLoading(true);

    const params = {
      page,
      pageSize,
    };

    // actorEmail = ô tìm kiếm chung (email, vai trò, hành động, đối tượng, EntityId)
    if (query.actorEmail) params.actorEmail = query.actorEmail.trim();
    if (query.actorRole) params.actorRole = query.actorRole.trim();
    if (query.action) params.action = query.action.trim();
    if (query.entityType) params.entityType = query.entityType.trim();
    if (query.from) params.from = query.from;
    if (query.to) params.to = query.to;

    // sort
    if (sortBy) params.sortBy = sortBy;
    if (sortDirection) params.sortDirection = sortDirection;

    AuditLogsApi.listPaged(params)
      .then((res) => {
        const items = res?.items ?? [];
        setLogs(items);
        setTotal(typeof res?.total === "number" ? res.total : items.length);
        setPage(res.pageNumber ?? page);
        setPageSize(res.pageSize ?? pageSize);
      })
      .catch((err) => {
        console.error(err);
        addToast("error", "Không tải được lịch sử thao tác.", "Lỗi");
      })
      .finally(() => setLoading(false));
  }, [page, pageSize, query, sortBy, sortDirection]);

  React.useEffect(() => {
    const t = setTimeout(loadLogs, 300);
    return () => clearTimeout(t);
  }, [loadLogs]);

  // Reset page khi filter thay đổi
  React.useEffect(() => {
    setPage(1);
  }, [query.actorEmail, query.actorRole, query.action, query.entityType, query.from, query.to]);

  const formatDateTime = (value) => {
    if (!value) return "";
    try {
      const d = new Date(value);
      if (Number.isNaN(d.getTime())) return value;
      return d.toLocaleString("vi-VN");
    } catch {
      return value;
    }
  };

  const openDetail = (log) => {
    setSelectedLog(log);
    setDetail(null);
    setDetailLoading(true);
    AuditLogsApi.getDetail(log.auditId)
      .then((res) => setDetail(res))
      .catch((err) => {
        console.error(err);
        addToast("error", "Không tải được chi tiết lịch sử thao tác.", "Lỗi");
      })
      .finally(() => setDetailLoading(false));
  };

  const closeDetail = () => {
    setSelectedLog(null);
    setDetail(null);
    setDetailLoading(false);
  };

  const resetFilters = () => {
    setQuery({
      actorEmail: "",
      actorRole: "",
      action: "",
      entityType: "",
      from: "",
      to: "",
    });
    setSortBy("OccurredAt");
    setSortDirection("desc");
  };

  const totalPages =
    pageSize > 0 ? Math.max(1, Math.ceil(total / pageSize)) : 1;

  const handleSort = (field) => {
    setSortBy((prevField) => {
      if (prevField === field) {
        setSortDirection((prevDir) => (prevDir === "asc" ? "desc" : "asc"));
        return prevField;
      }
      setSortDirection(field === "OccurredAt" ? "desc" : "asc");
      return field;
    });
  };

  return (
    <>
      <div className="page audit-page">
        <div className="card">
          <div className="audit-header-row">
            <div className="audit-header-left">
              <h2>Lịch sử thao tác hệ thống</h2>
            </div>
            <div className="row" style={{ gap: 8, alignItems: "center" }}>
              <span className="muted">
                Tổng: <b>{total}</b> bản ghi
              </span>
            </div>
          </div>

          {/* Hàng 1: Tìm kiếm + khoảng thời gian */}
          <div className="row input-group audit-filters">
            <div className="group filter-col filter-col-wide">
              <span>Tìm kiếm</span>
              <input
                value={query.actorEmail}
                onChange={(e) =>
                  setQuery((s) => ({ ...s, actorEmail: e.target.value }))
                }
              />
            </div>

            <div className="group filter-col">
              <span>Từ ngày</span>
              <input
                type="date"
                value={query.from}
                onChange={(e) =>
                  setQuery((s) => ({ ...s, from: e.target.value }))
                }
              />
            </div>
            <div className="group filter-col">
              <span>Đến ngày</span>
              <input
                type="date"
                value={query.to}
                onChange={(e) =>
                  setQuery((s) => ({ ...s, to: e.target.value }))
                }
              />
            </div>
          </div>

          {/* Hàng 2: 3 dropdown + nút Đặt lại */}
          <div className="row input-group audit-filters">
            <div className="group filter-col">
              <span>Hành động</span>
              <select
                value={query.action}
                onChange={(e) =>
                  setQuery((s) => ({ ...s, action: e.target.value }))
                }
              >
                <option value="">Tất cả</option>
                {options.actions.map((action) => (
                  <option key={action} value={action}>
                    {action}
                  </option>
                ))}
              </select>
            </div>

            <div className="group filter-col">
              <span>Loại đối tượng</span>
              <select
                value={query.entityType}
                onChange={(e) =>
                  setQuery((s) => ({ ...s, entityType: e.target.value }))
                }
              >
                <option value="">Tất cả</option>
                {options.entityTypes.map((type) => (
                  <option key={type} value={type}>
                    {type}
                  </option>
                ))}
              </select>
            </div>

            <div className="group filter-col">
              <span>Vai trò</span>
              <select
                value={query.actorRole}
                onChange={(e) =>
                  setQuery((s) => ({ ...s, actorRole: e.target.value }))
                }
              >
                <option value="">Tất cả</option>
                {options.actorRoles.map((role) => (
                  <option key={role} value={role}>
                    {role}
                  </option>
                ))}
              </select>
            </div>

            {loading && <span className="badge gray">Đang tải…</span>}

            <button
              type="button"
              className="btn ghost filter-reset-btn"
              onClick={resetFilters}
              title="Xóa bộ lọc"
            >
              Đặt lại
            </button>
          </div>

          {/* Bảng logs */}
          <table className="table audit-table">
            <thead>
              <tr>
                <th>
                  <SortableHeader
                    label="Thời gian"
                    field="OccurredAt"
                    sortBy={sortBy}
                    sortDirection={sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th>
                  <SortableHeader
                    label="Người thao tác"
                    field="ActorEmail"
                    sortBy={sortBy}
                    sortDirection={sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th>
                  <SortableHeader
                    label="Vai trò"
                    field="ActorRole"
                    sortBy={sortBy}
                    sortDirection={sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th>
                  <SortableHeader
                    label="Hành động"
                    field="Action"
                    sortBy={sortBy}
                    sortDirection={sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th>
                  <SortableHeader
                    label="Loại đối tượng"
                    field="EntityType"
                    sortBy={sortBy}
                    sortDirection={sortDirection}
                    onSort={handleSort}
                  />
                </th>
                <th>
                  <SortableHeader
                    label="Mã đối tượng"
                    field="EntityId"
                    sortBy={sortBy}
                    sortDirection={sortDirection}
                    onSort={handleSort}
                  />
                </th>
                {/* Bỏ cột Địa chỉ IP ở danh sách */}
                <th style={{ textAlign: "center" }}>Chi tiết</th>
              </tr>
            </thead>
            <tbody>
              {logs.map((log) => (
                <tr key={log.auditId} className="audit-row">
                  <td className="mono">{formatDateTime(log.occurredAt)}</td>
                  <td>
                    <EllipsisCell mono maxWidth={220} title={log.actorEmail}>
                      {log.actorEmail || "-"}
                    </EllipsisCell>
                  </td>
                  <td>
                    <EllipsisCell mono maxWidth={200} title={log.actorRole}>
                      {log.actorRole || "-"}
                    </EllipsisCell>
                  </td>
                  <td>
                    <EllipsisCell mono maxWidth={200} title={log.action}>
                      {log.action || "-"}
                    </EllipsisCell>
                  </td>
                  <td>
                    <EllipsisCell mono maxWidth={200} title={log.entityType}>
                      {log.entityType || "-"}
                    </EllipsisCell>
                  </td>
                  <td>
                    <EllipsisCell mono maxWidth={140} title={log.entityId}>
                      {log.entityId || "-"}
                    </EllipsisCell>
                  </td>
                  <td className="audit-actions-cell">
                    <button
                      type="button"
                      className="btn icon-btn"
                      title="Xem chi tiết lịch sử thao tác"
                      onClick={(e) => {
                        e.stopPropagation();
                        openDetail(log);
                      }}
                    >
                      <svg
                        viewBox="0 0 24 24"
                        width="18"
                        height="18"
                        aria-hidden="true"
                      >
                        <path
                          d="M1.5 12S4.5 5 12 5s10.5 7 10.5 7-3 7-10.5 7S1.5 12 1.5 12Z"
                          stroke="currentColor"
                          strokeWidth="1.7"
                          fill="none"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                        />
                        <circle
                          cx="12"
                          cy="12"
                          r="3"
                          stroke="currentColor"
                          strokeWidth="1.7"
                          fill="none"
                        />
                      </svg>
                    </button>
                  </td>
                </tr>
              ))}
              {logs.length === 0 && !loading && (
                <tr>
                  <td colSpan={7} style={{ textAlign: "center", padding: 16 }}>
                    Không có dữ liệu.
                  </td>
                </tr>
              )}
            </tbody>
          </table>

          {/* Pagination */}
          <div className="pager">
            <div className="row" style={{ gap: 8, alignItems: "center" }}>
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                Trước
              </button>
              <span>
                Trang {page}/{totalPages}
              </span>
              <button
                type="button"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              >
                Tiếp
              </button>
            </div>

            <div className="row" style={{ gap: 8, alignItems: "center" }}>
              <span>Hiển thị</span>
              <select
                value={pageSize}
                onChange={(e) => setPageSize(Number(e.target.value) || 20)}
              >
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
                <option value={100}>100</option>
              </select>
              <span>bản ghi mỗi trang</span>
            </div>
          </div>
        </div>
      </div>

      <AuditLogDetailModal
        open={!!selectedLog}
        log={selectedLog}
        detail={detail}
        loading={detailLoading}
        onClose={closeDetail}
      />

      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </>
  );
}
