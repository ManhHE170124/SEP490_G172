// File: src/pages/tickets/customer-ticket-management.jsx
import React, { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import "../../styles/customer-ticket-management.css";
import { ticketsApi } from "../../api/ticketsApi";

const PAGE_SIZE = 10;

function fmtVNDate(dt) {
  try {
    const d =
      typeof dt === "string" || typeof dt === "number" ? new Date(dt) : dt;
    return new Intl.DateTimeFormat("vi-VN", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    }).format(d);
  } catch {
    return "";
  }
}

function normalizeStatus(s) {
  const v = String(s || "").toLowerCase();
  if (v === "open" || v === "new") return "New";
  if (v === "processing" || v === "inprogress" || v === "in_process")
    return "InProgress";
  if (v === "done" || v === "resolved" || v === "completed")
    return "Completed";
  if (v === "closed" || v === "close") return "Closed";
  return "New";
}

function StatusBadge({ value }) {
  const v = normalizeStatus(value);
  const map = {
    New: { cls: "ctm-st ctm-st-new", text: "Mới" },
    InProgress: { cls: "ctm-st ctm-st-processing", text: "Đang xử lý" },
    Completed: { cls: "ctm-st ctm-st-completed", text: "Hoàn thành" },
    Closed: { cls: "ctm-st ctm-st-closed", text: "Đã đóng" },
  };
  const d = map[v] || map.New;
  return <span className={d.cls}>{d.text}</span>;
}

export default function CustomerTicketManagementPage() {
  const nav = useNavigate();

  const [data, setData] = useState({
    items: [],
    totalItems: 0,
    page: 1,
    pageSize: PAGE_SIZE,
  });
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);

  const totalPages = useMemo(
    () =>
      Math.max(
        1,
        Math.ceil((data.totalItems || 0) / (data.pageSize || PAGE_SIZE))
      ),
    [data.totalItems, data.pageSize]
  );

  const normalizePaged = (res, fallbacks) => ({
    items: res?.items ?? res?.Items ?? fallbacks.items,
    totalItems: res?.totalItems ?? res?.TotalItems ?? fallbacks.totalItems,
    page: res?.page ?? res?.Page ?? fallbacks.page,
    pageSize: res?.pageSize ?? res?.PageSize ?? fallbacks.pageSize,
  });

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      try {
        const res = await ticketsApi.customerTicketList({
          page,
          pageSize: PAGE_SIZE,
        });
        if (!cancelled) {
          setData(
            normalizePaged(res, {
              items: [],
              totalItems: 0,
              page,
              pageSize: PAGE_SIZE,
            })
          );
        }
      } catch (e) {
        if (!cancelled) {
          console.error(e);
          alert(
            e?.response?.data?.message ||
              e.message ||
              "Không tải được danh sách ticket."
          );
          setData((prev) => ({ ...prev, items: [] }));
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, [page]);

  const gotoPage = (p) => {
    const next = Math.max(1, Math.min(totalPages, p));
    setPage(next);
  };

  const items = data.items || [];

  return (
    <div className="ctm-page">
      <div className="ctm-header">
        <div>
          <h1 className="ctm-title">Yêu cầu hỗ trợ của tôi</h1>
          <p className="ctm-subtitle">
            Xem và theo dõi các ticket hỗ trợ mà bạn đã tạo.
          </p>
        </div>
        <button
          type="button"
          className="btn primary"
          onClick={() => nav("/tickets/create")}
        >
          + Tạo ticket mới
        </button>
      </div>

      {/* Không còn filter – list đơn giản theo BE MyTickets */}

      <div className="ctm-table-wrap">
        <table className="ctm-table">
          <colgroup>
            <col style={{ width: "110px" }} /> {/* Mã ticket */}
            <col /> {/* Tiêu đề */}
            <col style={{ width: "140px" }} /> {/* Trạng thái */}
            <col style={{ width: "150px" }} /> {/* Ngày tạo */}
            <col style={{ width: "150px" }} /> {/* Cập nhật lúc */}
            <col style={{ width: "90px" }} /> {/* Thao tác */}
          </colgroup>
          <thead>
            <tr>
              <th>Mã ticket</th>
              <th>Tiêu đề</th>
              <th>Trạng thái</th>
              <th>Ngày tạo</th>
              <th>Cập nhật lúc</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {loading && (
              <tr>
                <td colSpan={6} className="ctm-center muted">
                  Đang tải danh sách ticket...
                </td>
              </tr>
            )}

            {!loading && items.length === 0 && (
              <tr>
                <td colSpan={6} className="ctm-center muted">
                  Bạn chưa có ticket nào.{" "}
                </td>
              </tr>
            )}

            {!loading &&
              items.map((t) => (
                <tr key={t.ticketId} className="ctm-row">
                  <td className="ctm-code mono">{t.ticketCode}</td>
                  <td className="ctm-subject">
                    <div className="ctm-subject-main">{t.subject}</div>
                  </td>
                  <td>
                    <StatusBadge value={t.status} />
                  </td>
                  <td>{fmtVNDate(t.createdAt)}</td>
                  <td>{fmtVNDate(t.updatedAt || t.createdAt)}</td>
                  <td className="ctm-actions">
                    <div className="ctm-row-actions">
                      <button
                        type="button"
                        className="btn icon-btn ghost"
                        title="Xem chi tiết"
                        onClick={() => nav(`/tickets/${t.ticketId}`)}
                      >
                        <span aria-hidden="true">🔍</span>
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
          </tbody>
        </table>
      </div>

      <div className="ctm-pager">
        <button
          type="button"
          className="btn ghost sm"
          disabled={data.page <= 1}
          onClick={() => gotoPage(data.page - 1)}
        >
          « Trước
        </button>
        <span className="ctm-page-info">
          Trang {data.page}/{totalPages}
        </span>
        <button
          type="button"
          className="btn ghost sm"
          disabled={data.page >= totalPages}
          onClick={() => gotoPage(data.page + 1)}
        >
          Sau »
        </button>
      </div>
    </div>
  );
}
