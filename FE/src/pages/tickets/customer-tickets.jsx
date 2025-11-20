// File: src/pages/tickets/customer-tickets.jsx
import React, { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ticketsApi } from "../../api/ticketsApi";

function formatDateTime(value) {
  if (!value) return "";
  try {
    return new Date(value).toLocaleString("vi-VN");
  } catch {
    return String(value);
  }
}

const SEVERITY_OPTIONS = [
  { value: "Low", label: "Low" },
  { value: "Medium", label: "Medium" },
  { value: "High", label: "High" },
  { value: "Critical", label: "Critical" },
];

export default function CustomerTicketsPage() {
  const navigate = useNavigate();

  // List tickets
  const [tickets, setTickets] = useState([]);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [totalItems, setTotalItems] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(false);
  const [listError, setListError] = useState("");

  // Search
  const [keyword, setKeyword] = useState("");

  // Create ticket
  const [showCreate, setShowCreate] = useState(true);
  const [createSubject, setCreateSubject] = useState("");
  const [createSeverity, setCreateSeverity] = useState("Medium");
  const [createMessage, setCreateMessage] = useState("");
  const [createError, setCreateError] = useState("");
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    fetchTickets();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  async function fetchTickets(paramsOverride) {
    setLoading(true);
    setListError("");

    try {
      const res = await ticketsApi.list({
        page,
        pageSize,
        q: keyword?.trim() || undefined,
        ...paramsOverride,
      });

      // Giả sử backend trả { items, totalItems, totalPages, page, pageSize }
      setTickets(res.items || []);
      setTotalItems(res.totalItems ?? (res.items ? res.items.length : 0));
      setTotalPages(res.totalPages ?? 1);
      if (typeof res.page === "number") setPage(res.page);
    } catch (err) {
      console.error("Failed to load tickets", err);
      setListError(
        err?.response?.data?.message ||
          "Không tải được danh sách ticket. Vui lòng thử lại."
      );
    } finally {
      setLoading(false);
    }
  }

  async function handleSearchSubmit(e) {
    e.preventDefault();
    setPage(1);
    await fetchTickets({ page: 1 });
  }

  async function handleClearSearch() {
    setKeyword("");
    setPage(1);
    await fetchTickets({ page: 1, q: undefined });
  }

  function validateCreateForm() {
    if (!createSubject.trim()) {
      setCreateError("Vui lòng nhập tiêu đề ticket.");
      return false;
    }
    if (!createMessage.trim()) {
      setCreateError("Vui lòng mô tả vấn đề của bạn.");
      return false;
    }
    if (!createSeverity) {
      setCreateError("Vui lòng chọn mức độ ưu tiên.");
      return false;
    }
    setCreateError("");
    return true;
  }

  async function handleCreateSubmit(e) {
    e.preventDefault();
    if (!validateCreateForm()) return;

    setCreating(true);
    setCreateError("");

    try {
      const payload = {
        subject: createSubject.trim(),
        severity: createSeverity,
        initialMessage: createMessage.trim(),
      };

      const created = await ticketsApi.create(payload);

      // Reset form
      setCreateSubject("");
      setCreateMessage("");
      setCreateSeverity("Medium");

      // Nếu backend trả TicketDetailDto có ticketId → redirect thẳng
      if (created && created.ticketId) {
        navigate(`/tickets/${created.ticketId}`);
      } else {
        // fallback: reload list
        await fetchTickets({ page: 1 });
        setPage(1);
      }
    } catch (err) {
      console.error("Failed to create ticket", err);
      setCreateError(
        err?.response?.data?.message ||
          "Không tạo được ticket. Vui lòng thử lại."
      );
    } finally {
      setCreating(false);
    }
  }

  const canGoPrev = page > 1;
  const canGoNext = page < totalPages;

  return (
    <div className="container my-4">
      <h1 className="mb-3">Ticket hỗ trợ của tôi</h1>

      {/* --- Form tạo ticket mới --- */}
      <div className="card mb-4">
        <div
          className="card-header d-flex justify-content-between align-items-center"
          style={{ cursor: "pointer" }}
          onClick={() => setShowCreate((v) => !v)}
        >
          <span className="fw-semibold">Tạo ticket mới</span>
          <span>{showCreate ? "−" : "+"}</span>
        </div>
        {showCreate && (
          <div className="card-body">
            <form onSubmit={handleCreateSubmit}>
              <div className="mb-3">
                <label className="form-label">Tiêu đề *</label>
                <input
                  type="text"
                  className="form-control"
                  value={createSubject}
                  onChange={(e) => setCreateSubject(e.target.value)}
                  placeholder="Ví dụ: Không nhận được key sau khi thanh toán"
                />
              </div>

              <div className="mb-3">
                <label className="form-label">Mức độ *</label>
                <select
                  className="form-select"
                  value={createSeverity}
                  onChange={(e) => setCreateSeverity(e.target.value)}
                >
                  {SEVERITY_OPTIONS.map((opt) => (
                    <option value={opt.value} key={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="mb-3">
                <label className="form-label">Mô tả vấn đề *</label>
                <textarea
                  className="form-control"
                  rows={4}
                  value={createMessage}
                  onChange={(e) => setCreateMessage(e.target.value)}
                  placeholder="Mô tả chi tiết vấn đề bạn gặp phải để nhân viên hỗ trợ xử lý nhanh hơn."
                />
              </div>

              {createError && (
                <div className="alert alert-danger py-2">{createError}</div>
              )}

              <button
                type="submit"
                className="btn btn-primary"
                disabled={creating}
              >
                {creating ? "Đang tạo..." : "Gửi ticket"}
              </button>
            </form>
          </div>
        )}
      </div>

      {/* --- Bộ lọc đơn giản (search theo mã hoặc tiêu đề) --- */}
      <form
        className="row g-2 align-items-end mb-3"
        onSubmit={handleSearchSubmit}
      >
        <div className="col-md-4">
          <label className="form-label">Tìm kiếm</label>
          <input
            type="text"
            className="form-control"
            placeholder="Nhập mã ticket hoặc tiêu đề..."
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
          />
        </div>
        <div className="col-md-4 d-flex gap-2">
          <button
            type="submit"
            className="btn btn-outline-primary"
            disabled={loading}
          >
            Tìm kiếm
          </button>
          <button
            type="button"
            className="btn btn-outline-secondary"
            onClick={handleClearSearch}
            disabled={loading && !keyword}
          >
            Xóa lọc
          </button>
        </div>
      </form>

      {/* --- Danh sách ticket --- */}
      <div className="card">
        <div className="card-body p-0">
          {listError && (
            <div className="alert alert-danger m-3">{listError}</div>
          )}

          {loading && !tickets.length ? (
            <div className="p-3">Đang tải danh sách ticket...</div>
          ) : tickets.length === 0 ? (
            <div className="p-3">
              Bạn chưa có ticket hỗ trợ nào. Hãy tạo ticket mới ở phía trên.
            </div>
          ) : (
            <div className="table-responsive">
              <table className="table table-hover mb-0">
                <thead>
                  <tr>
                    <th style={{ width: "120px" }}>Mã</th>
                    <th>Tiêu đề</th>
                    <th style={{ width: "120px" }}>Trạng thái</th>
                    <th style={{ width: "120px" }}>Mức độ</th>
                    <th style={{ width: "150px" }}>SLA</th>
                    <th style={{ width: "180px" }}>Ngày tạo</th>
                    <th style={{ width: "100px" }}></th>
                  </tr>
                </thead>
                <tbody>
                  {tickets.map((t) => (
                    <tr key={t.ticketId}>
                      <td>{t.ticketCode}</td>
                      <td>{t.subject}</td>
                      <td>{t.status}</td>
                      <td>{t.severity}</td>
                      <td>{t.slaStatus}</td>
                      <td>{formatDateTime(t.createdAt)}</td>
                      <td className="text-end">
                        <Link
                          to={`/tickets/${t.ticketId}`}
                          className="btn btn-sm btn-outline-primary"
                        >
                          Chi tiết
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Pagination đơn giản */}
        {tickets.length > 0 && (
          <div className="card-footer d-flex justify-content-between align-items-center">
            <div>
              Trang {page} / {totalPages} – Tổng {totalItems} ticket
            </div>
            <div className="btn-group">
              <button
                type="button"
                className="btn btn-outline-secondary btn-sm"
                disabled={!canGoPrev}
                onClick={() => canGoPrev && setPage((p) => p - 1)}
              >
                « Trước
              </button>
              <button
                type="button"
                className="btn btn-outline-secondary btn-sm"
                disabled={!canGoNext}
                onClick={() => canGoNext && setPage((p) => p + 1)}
              >
                Sau »
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
