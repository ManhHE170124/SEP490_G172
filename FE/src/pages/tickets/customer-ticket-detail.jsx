// File: src/pages/tickets/customer-ticket-detail.jsx
import React, { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { ticketsApi } from "../../api/ticketsApi";

function formatDateTime(value) {
  if (!value) return "";
  try {
    return new Date(value).toLocaleString("vi-VN");
  } catch {
    return String(value);
  }
}

function isTicketClosed(status) {
  if (!status) return false;
  const normalized = String(status).toLowerCase();
  return (
    normalized === "closed" ||
    normalized === "completed" ||
    normalized === "resolved"
  );
}

export default function CustomerTicketDetailPage() {
  const { id } = useParams();

  const [ticket, setTicket] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");

  const [replyText, setReplyText] = useState("");
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState("");

  useEffect(() => {
    let mounted = true;

    async function load() {
      setLoading(true);
      setLoadError("");
      try {
        const data = await ticketsApi.detail(id);
        if (mounted) {
          setTicket(data);
        }
      } catch (err) {
        console.error("Failed to load ticket detail", err);
        if (mounted) {
          setLoadError(
            err?.response?.data?.message ||
              "Không tải được thông tin ticket. Vui lòng thử lại."
          );
        }
      } finally {
        if (mounted) setLoading(false);
      }
    }

    load();

    return () => {
      mounted = false;
    };
  }, [id]);

  async function handleSendReply(e) {
    e.preventDefault();
    if (!replyText.trim()) {
      setSendError("Vui lòng nhập nội dung trả lời.");
      return;
    }
    if (!ticket) return;

    setSending(true);
    setSendError("");

    try {
      const payload = {
        message: replyText.trim(),
      };

      const createdReply = await ticketsApi.reply(
        ticket.ticketId || id,
        payload
      );

      setReplyText("");

      // Append reply mới vào danh sách replies hiện tại
      setTicket((prev) =>
        !prev
          ? prev
          : {
              ...prev,
              replies: [...(prev.replies || []), createdReply],
            }
      );
    } catch (err) {
      console.error("Failed to send reply", err);
      setSendError(
        err?.response?.data?.message ||
          "Không gửi được phản hồi. Vui lòng thử lại."
      );
    } finally {
      setSending(false);
    }
  }

  if (loading) {
    return (
      <div className="container my-4">
        <p>Đang tải thông tin ticket...</p>
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="container my-4">
        <div className="alert alert-danger">{loadError}</div>
        <Link to="/tickets" className="btn btn-secondary mt-2">
          Quay lại danh sách ticket
        </Link>
      </div>
    );
  }

  if (!ticket) {
    return (
      <div className="container my-4">
        <div className="alert alert-warning">Không tìm thấy ticket.</div>
        <Link to="/tickets" className="btn btn-secondary mt-2">
          Quay lại danh sách ticket
        </Link>
      </div>
    );
  }

  const closed = isTicketClosed(ticket.status);

  return (
    <div className="container my-4">
      <div className="mb-3">
        <Link to="/tickets" className="btn btn-link p-0">
          &laquo; Quay lại danh sách ticket
        </Link>
      </div>

      {/* Header ticket */}
      <div className="mb-3">
        <h1 className="h4 mb-1">
          Ticket #{ticket.ticketCode} – {ticket.subject}
        </h1>
        <div className="text-muted small">
          Tạo lúc {formatDateTime(ticket.createdAt)}
          {ticket.updatedAt && (
            <> • Cập nhật: {formatDateTime(ticket.updatedAt)}</>
          )}
        </div>
      </div>

      {/* Thông tin tổng quan + SLA */}
      <div className="row g-3 mb-4">
        <div className="col-md-6">
          <div className="card h-100">
            <div className="card-header fw-semibold">Thông tin chung</div>
            <div className="card-body">
              <p className="mb-1">
                <span className="fw-semibold">Trạng thái:</span>{" "}
                {ticket.status || "-"}
              </p>
              <p className="mb-1">
                <span className="fw-semibold">Mức độ:</span>{" "}
                {ticket.severity || "-"}
              </p>
              <p className="mb-1">
                <span className="fw-semibold">Khách hàng:</span>{" "}
                {ticket.customerName || ticket.customerEmail || "-"}
              </p>
              {ticket.assigneeName && (
                <p className="mb-1">
                  <span className="fw-semibold">Nhân viên phụ trách:</span>{" "}
                  {ticket.assigneeName}
                </p>
              )}
              {ticket.assignmentState && (
                <p className="mb-1">
                  <span className="fw-semibold">Phân công:</span>{" "}
                  {ticket.assignmentState}
                </p>
              )}
            </div>
          </div>
        </div>

        {/* SLA */}
        <div className="col-md-6">
          <div className="card h-100">
            <div className="card-header fw-semibold">SLA</div>
            <div className="card-body">
              <p className="mb-1">
                <span className="fw-semibold">Trạng thái SLA:</span>{" "}
                {ticket.slaStatus || "-"}
              </p>
              {ticket.slaSnapshotLabel && (
                <p className="mb-1">
                  <span className="fw-semibold">Chi tiết:</span>{" "}
                  {ticket.slaSnapshotLabel}
                </p>
              )}
              {ticket.firstResponseDueAt && (
                <p className="mb-1">
                  <span className="fw-semibold">
                    Hạn trả lời đầu tiên:
                  </span>{" "}
                  {formatDateTime(ticket.firstResponseDueAt)}
                </p>
              )}
              {ticket.firstResponseAt && (
                <p className="mb-1">
                  <span className="fw-semibold">
                    Thời gian đã trả lời:
                  </span>{" "}
                  {formatDateTime(ticket.firstResponseAt)}
                </p>
              )}
              {ticket.resolutionDueAt && (
                <p className="mb-1">
                  <span className="fw-semibold">Hạn xử lý:</span>{" "}
                  {formatDateTime(ticket.resolutionDueAt)}
                </p>
              )}
              {ticket.resolvedAt && (
                <p className="mb-1">
                  <span className="fw-semibold">Thời điểm hoàn thành:</span>{" "}
                  {formatDateTime(ticket.resolvedAt)}
                </p>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Danh sách trao đổi (replies) */}
      <div className="card mb-4">
        <div className="card-header fw-semibold">Trao đổi</div>
        <div className="card-body">
          {!ticket.replies || ticket.replies.length === 0 ? (
            <p className="mb-0">Chưa có trao đổi nào.</p>
          ) : (
            <div className="vstack gap-3">
              {ticket.replies.map((reply, index) => {
                const isStaff =
                  reply?.isStaffReply ??
                  reply?.isFromStaff ??
                  reply?.isStaff ??
                  reply?.fromStaff ??
                  false;
                const authorName =
                  reply?.authorName ||
                  reply?.createdByName ||
                  reply?.createdByEmail ||
                  (isStaff ? "Nhân viên hỗ trợ" : "Bạn");

                return (
                  <div
                    key={reply.replyId || index}
                    className={`p-2 rounded border ${
                      isStaff ? "bg-light" : ""
                    }`}
                  >
                    <div className="d-flex justify-content-between mb-1 small">
                      <span className="fw-semibold">{authorName}</span>
                      <span className="text-muted">
                        {formatDateTime(reply.createdAt)}
                      </span>
                    </div>
                    <div style={{ whiteSpace: "pre-wrap" }}>
                      {reply.message}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </div>

      {/* Form gửi reply */}
      <div className="card mb-4">
        <div className="card-header fw-semibold">Gửi phản hồi</div>
        <div className="card-body">
          {closed ? (
            <div className="alert alert-info mb-0">
              Ticket đã được đóng, bạn không thể gửi thêm phản hồi. Nếu vẫn còn
              vấn đề, vui lòng tạo ticket mới.
            </div>
          ) : (
            <form onSubmit={handleSendReply}>
              <div className="mb-3">
                <label className="form-label">Nội dung *</label>
                <textarea
                  className="form-control"
                  rows={4}
                  value={replyText}
                  onChange={(e) => setReplyText(e.target.value)}
                  placeholder="Nhập nội dung bạn muốn gửi cho nhân viên hỗ trợ..."
                />
              </div>

              {sendError && (
                <div className="alert alert-danger py-2">{sendError}</div>
              )}

              <button
                type="submit"
                className="btn btn-primary"
                disabled={sending}
              >
                {sending ? "Đang gửi..." : "Gửi phản hồi"}
              </button>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
