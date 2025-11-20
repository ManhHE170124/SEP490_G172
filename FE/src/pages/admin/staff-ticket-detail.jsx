// File: src/pages/admin/staff-ticket-detail.jsx
import React, { useEffect, useState } from "react";
import "../../styles/admin-ticket-detail.css";
import { useNavigate, useParams } from "react-router-dom";
import { ticketsApi } from "../../api/ticketsApi";

const MAP_STATUS = {
  New: "Mới",
  InProgress: "Đang xử lý",
  Completed: "Đã hoàn thành",
  Closed: "Đã đóng",
};

const MAP_SEV = {
  Low: "Thấp",
  Medium: "Trung bình",
  High: "Cao",
  Critical: "Nghiêm trọng",
};

const MAP_SLA = {
  OK: "Đúng hạn",
  Warning: "Cảnh báo",
  Overdue: "Quá hạn",
};

const MAP_ASN = {
  Unassigned: "Chưa gán",
  Assigned: "Đã gán",
  Technical: "Đã chuyển",
};

function fmtDateTime(v) {
  if (!v) return "";
  try {
    const d = typeof v === "string" || typeof v === "number" ? new Date(v) : v;
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
  if (["processing", "inprogress", "in_process"].includes(v)) return "InProgress";
  if (["done", "resolved", "completed"].includes(v)) return "Completed";
  if (v === "closed" || v === "close") return "Closed";
  return "New";
}

function StatusBadge({ value }) {
  const v = normalizeStatus(value);
  const cls =
    v === "New"
      ? "st st-new"
      : v === "InProgress"
      ? "st st-processing"
      : v === "Completed"
      ? "st st-completed"
      : "st st-closed";
  return <span className={cls}>{MAP_STATUS[v] || v}</span>;
}

function SeverityTag({ value }) {
  const v = String(value);
  const cls =
    v === "Low"
      ? "tag tag-low"
      : v === "Medium"
      ? "tag tag-medium"
      : v === "High"
      ? "tag tag-high"
      : "tag tag-critical";
  return <span className={cls}>{MAP_SEV[v] || v}</span>;
}

function SlaPill({ value }) {
  const v = String(value);
  const cls =
    v === "OK"
      ? "sla sla-ok"
      : v === "Overdue"
      ? "sla sla-breached"
      : "sla sla-warning";
  return <span className={cls}>{MAP_SLA[v] || v}</span>;
}

export default function StaffTicketDetail() {
  const { id } = useParams();
  const nav = useNavigate();

  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");

  const [replyText, setReplyText] = useState("");
  const [sending, setSending] = useState(false);

  useEffect(() => {
    let mounted = true;

    async function load() {
      setLoading(true);
      setErr("");
      try {
        const res = await ticketsApi.detail(id);
        const d = res?.data || res;
        if (!mounted) return;
        setData({
          ...d,
          replies: d?.replies || d?.Replies || [],
        });
      } catch (e) {
        if (!mounted) return;
        setErr(
          e?.response?.data?.message ||
            e.message ||
            "Không tải được chi tiết ticket."
        );
      } finally {
        if (mounted) setLoading(false);
      }
    }

    load();
    return () => {
      mounted = false;
    };
  }, [id]);

  const replies = data?.replies || [];

  const handleSendReply = async (e) => {
    e.preventDefault();
    const msg = replyText.trim();
    if (!msg) return;

    setSending(true);
    try {
      const newReply = await ticketsApi.reply(id, { message: msg });
      const r = newReply?.data || newReply;
      setReplyText("");
      setData((prev) => ({
        ...(prev || {}),
        replies: [...(prev?.replies || []), r],
      }));
    } catch (e) {
      alert(
        e?.response?.data?.message ||
          e.message ||
          "Gửi phản hồi thất bại. Vui lòng thử lại."
      );
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="tkd-page">
      <div className="tkd-header">
        <button
          type="button"
          className="btn ghost"
          onClick={() => nav("/staff/tickets")}
        >
          ← Quay lại danh sách
        </button>
        <h1 className="tkd-title">
          Ticket hỗ trợ{" "}
          {data?.ticketCode ? `#${data.ticketCode}` : ""}
        </h1>
      </div>

      {loading && <div style={{ padding: 16 }}>Đang tải...</div>}
      {!loading && err && (
        <div style={{ padding: 16, color: "red" }}>{err}</div>
      )}

      {!loading && !err && data && (
        <div className="tkd-layout">
          <main className="tkd-main">
            {/* Thông tin chính */}
            <section className="tkd-section">
              <h2 className="tkd-section-title">Thông tin ticket</h2>
              <div className="tkd-meta-grid">
                <div>
                  <div className="tkd-label">Mã ticket</div>
                  <div className="mono">{data.ticketCode}</div>
                </div>
                <div>
                  <div className="tkd-label">Trạng thái</div>
                  <StatusBadge value={data.status} />
                </div>
                <div>
                  <div className="tkd-label">Mức độ</div>
                  <SeverityTag value={data.severity} />
                </div>
                <div>
                  <div className="tkd-label">SLA</div>
                  <SlaPill value={data.slaStatus} />
                </div>
                <div>
                  <div className="tkd-label">Ngày tạo</div>
                  <div>{fmtDateTime(data.createdAt)}</div>
                </div>
                {data.slaDueAt && (
                  <div>
                    <div className="tkd-label">Hạn SLA</div>
                    <div>{fmtDateTime(data.slaDueAt)}</div>
                  </div>
                )}
              </div>

              <div className="tkd-block">
                <div className="tkd-label">Tiêu đề</div>
                <div className="tkd-subject">{data.subject}</div>
              </div>
              <div className="tkd-block">
                <div className="tkd-label">Mô tả</div>
                <div className="tkd-description">
                  {data.description || "(Không có mô tả chi tiết)"}
                </div>
              </div>
            </section>

            {/* Thread trao đổi */}
            <section className="tkd-section">
              <h2 className="tkd-section-title">Trao đổi</h2>
              <div className="tkd-thread">
                {replies.length === 0 && (
                  <div className="tkd-empty-thread">
                    Chưa có phản hồi nào.
                  </div>
                )}
                {replies.map((r) => (
                  <div
                    key={r.ticketReplyId || r.id}
                    className="tkd-msg"
                  >
                    <div className="tkd-msg-head">
                      <span className="tkd-msg-sender">
                        {r.senderName || r.senderEmail || "Người dùng"}
                      </span>
                      <span className="tkd-msg-time">
                        {fmtDateTime(r.createdAt || r.sentAt)}
                      </span>
                    </div>
                    <div className="tkd-msg-body">
                      {r.message}
                    </div>
                  </div>
                ))}
              </div>

              <form className="tkd-reply-box" onSubmit={handleSendReply}>
                <label className="tkd-label">Phản hồi</label>
                <textarea
                  className="ip tkd-reply-textarea"
                  rows={3}
                  placeholder="Nhập nội dung phản hồi..."
                  value={replyText}
                  onChange={(e) => setReplyText(e.target.value)}
                />
                <div className="tkd-reply-actions">
                  <button
                    type="submit"
                    className="btn primary"
                    disabled={sending || !replyText.trim()}
                  >
                    {sending ? "Đang gửi..." : "Gửi phản hồi"}
                  </button>
                </div>
              </form>
            </section>
          </main>

          {/* Sidebar: khách hàng + phân công */}
          <aside className="tkd-side">
            <section className="tkd-section">
              <h3 className="tkd-section-title">Khách hàng</h3>
              <div className="tkd-block">
                <div className="tkd-label">Tên</div>
                <div>{data.userName || "(Không rõ)"}</div>
              </div>
              <div className="tkd-block">
                <div className="tkd-label">Email</div>
                <div>{data.userEmail}</div>
              </div>
            </section>

            <section className="tkd-section">
              <h3 className="tkd-section-title">Phân công</h3>
              <div className="tkd-block">
                <div className="tkd-label">Trạng thái phân công</div>
                <div>
                  {MAP_ASN[data.assignmentState] ||
                    MAP_ASN.Unassigned}
                </div>
              </div>
              {data.assigneeName && (
                <>
                  <div className="tkd-block">
                    <div className="tkd-label">Nhân viên phụ trách</div>
                    <div>{data.assigneeName}</div>
                  </div>
                  <div className="tkd-block">
                    <div className="tkd-label">Email</div>
                    <div>{data.assigneeEmail}</div>
                  </div>
                </>
              )}
            </section>
          </aside>
        </div>
      )}
    </div>
  );
}
