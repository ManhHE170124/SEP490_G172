import React, { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { ticketsApi } from "../../api/ticketsApi";
import "../../styles/admin-ticket-management.css";

/** Badge gọn gàng */
const Badge = ({ text }) => <span className="tk-bad st">{text}</span>;

/** Modal đơn giản để “giả lập” chọn người gán / kỹ thuật */
function AssignModal({ open, title, onClose, onConfirm }) {
    const [q, setQ] = useState("");
    // danh sách giả lập (sau này thay bằng GET /api/users?role=customer-care)
    const staff = useMemo(
        () => [
            { id: "U001", name: "Nguyễn Văn X" },
            { id: "U002", name: "Trần Văn Y" },
            { id: "U003", name: "Lê Văn Z" },
            { id: "U004", name: "Phạm Thị Q" },
        ],
        []
    );
    const list = staff.filter(
        (s) =>
            s.id.toLowerCase().includes(q.toLowerCase()) ||
            s.name.toLowerCase().includes(q.toLowerCase())
    );

    if (!open) return null;
    return (
        <div className="tk-modal-backdrop">
            <div className="tk-modal">
                <div className="tk-modal-hd">
                    <b>{title}</b>
                    <button className="tk-x" onClick={onClose} aria-label="Close">×</button>
                </div>
                <div className="tk-modal-bd">
                    <input
                        placeholder="Tìm theo mã hoặc tên nhân viên…"
                        value={q}
                        onChange={(e) => setQ(e.target.value)}
                        style={{ width: "100%", marginBottom: 8 }}
                    />
                    <div className="tk-modal-list">
                        {list.map((s) => (
                            <button
                                key={s.id}
                                className="tk-modal-row"
                                onClick={() => onConfirm(s)}
                            >
                                <span className="tk-id">{s.id}</span>
                                <span>{s.name}</span>
                            </button>
                        ))}
                        {!list.length && <div style={{ padding: 12 }}>Không có kết quả</div>}
                    </div>
                </div>
                <div className="tk-modal-ft">
                    <button onClick={onClose}>Đóng</button>
                </div>
            </div>
        </div>
    );
}

export default function TicketDetail() {
    const { id } = useParams();
    const nav = useNavigate();
    const [d, setD] = useState(null);
    const [loading, setLoading] = useState(false);
    const [md, setMd] = useState({ open: false, mode: null }); // {open, mode: 'assign'|'transfer'}

    const reload = async () => {
        setLoading(true);
        try {
            const data = await ticketsApi.detail(id);
            setD(data);
        } catch (e) {
            alert(e.message || "Không tải được ticket.");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => { reload(); }, [id]);

    if (!d) return <div className="sb-main">Đang tải…</div>;

    const locked = d.status === "Completed" || d.status === "Closed";
    const issueDesc = (() => {
        const first = (d.replies || []).find((r) => !r.isStaffReply);
        return first?.message || "";
    })();


    const openAssign = () => setMd({ open: true, mode: "assign" });
    const openTransfer = () => setMd({ open: true, mode: "transfer" });
    const closeModal = () => setMd({ open: false, mode: null });

    const onPick = async (staff) => {
        try {
            if (md.mode === "assign") await ticketsApi.assign(d.ticketId);
            if (md.mode === "transfer") await ticketsApi.transferTech(d.ticketId);
            closeModal();
            await reload();
            alert(`${md.mode === "assign" ? "Đã gán" : "Đã chuyển kỹ thuật"}: ${staff.name}`);
        } catch (e) {
            alert(e.message || "Thao tác thất bại.");
        }
    };

    const doComplete = async () => {
        try {
            await ticketsApi.complete(d.ticketId);
            await reload();
            alert("Đã đánh dấu hoàn thành.");
        } catch (e) { alert(e.message); }
    };

    const doClose = async () => {
        try {
            await ticketsApi.close(d.ticketId);
            await reload();
            alert("Đã đóng ticket.");
        } catch (e) { alert(e.message); }
    };

    return (
        <div className="sb-main">
            <button onClick={() => nav(-1)} style={{ marginBottom: 12 }}>← Quay lại</button>

            {/* Header */}
            <div className="tk-card" style={{ marginBottom: 16 }}>
                <h3 style={{ margin: 0 }}>
                    Chi tiết Ticket <small style={{ color: "#64748b" }}>#{d.ticketCode || "—"}</small>
                </h3>
                <p style={{ marginTop: 8, marginBottom: 10 }}><b>Chủ đề:</b> {d.subject}</p>
                <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                    <Badge text={`Trạng thái: ${d.status}`} />
                    <Badge text={`Gán: ${d.assignmentState}`} />
                    <Badge text={`Mức độ: ${d.severity}`} />
                    <Badge text={`SLA: ${d.slaStatus}`} />
                </div>
            </div>

            <div className="tk-2col">
                {/* LEFT: conversation + reply box (display only) */}
                <div className="tk-card">
                    <h4>Lịch sử trao đổi</h4>
                    <div style={{ border: "1px solid var(--line)", borderRadius: 8, maxHeight: 420, overflow: "auto", padding: 12 }}>
                        {(d.replies || []).map((r) => (
                            <div key={r.replyId} style={{ marginBottom: 12 }}>
                                <div style={{ fontWeight: 600 }}>
                                    {r.senderName}
                                    <span style={{ fontWeight: 400, color: "#6b7280" }}>
                                        {" "}({new Date(r.sentAt).toLocaleString("vi-VN")})
                                    </span>
                                </div>
                                <div style={{ whiteSpace: "pre-wrap" }}>{r.message}</div>
                            </div>
                        ))}
                        {!d.replies?.length && <div>Chưa có trao đổi.</div>}
                    </div>

                    {/* Reply form – chỉ hiển thị theo yêu cầu */}
                    <div style={{ marginTop: 12, opacity: .7 }}>
                        <div className="template-buttons">
                            <button className="template-btn" disabled>Mẫu phản hồi nhanh</button>
                            <button className="template-btn" disabled>Gửi email thông báo</button>
                            <button className="template-btn" disabled>Gửi lại key</button>
                        </div>
                        <textarea disabled rows={3} placeholder="Nhập phản hồi… (demo – chưa bật gửi)" style={{ width: "100%" }} />
                        <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
                            <button disabled>Gửi phản hồi</button>
                        </div>
                    </div>
                </div>

                {/* RIGHT: actions + customer + issue description */}
                <div className="tk-card">
                    <h4>Hành động nhanh</h4>
                    <div className="tk-actions" style={{ flexWrap: "wrap", marginBottom: 12 }}>
                        {d.assignmentState === "Unassigned" && !locked && (
                            <button onClick={openAssign} disabled={loading}>Gán</button>
                        )}
                        {d.assignmentState === "Assigned" && !locked && (
                            <button onClick={openTransfer} disabled={loading}>Chuyển kỹ thuật</button>
                        )}
                        {!locked && <button onClick={doComplete} disabled={loading}>Đánh dấu hoàn thành</button>}
                        {!locked && <button onClick={doClose} disabled={loading}>Đóng ticket</button>}
                    </div>

                    <div style={{ height: 8 }} />
                    <h4>Thông tin khách hàng</h4>
                    <div><b>{d.customerName}</b></div>
                    <div>{d.customerEmail}</div>
                    {d.customerPhone && <div>{d.customerPhone}</div>}
                    <div style={{ color: "#6b7280", fontSize: 12, marginTop: 8 }}>
                        Tạo: {new Date(d.createdAt).toLocaleString("vi-VN")}
                        {d.updatedAt && <> · Cập nhật: {new Date(d.updatedAt).toLocaleString("vi-VN")}</>}
                    </div>

                    <div style={{ height: 16 }} />
                    <h4>Mô tả vấn đề</h4>
                    <div style={{ padding: 12, background: "#f9fafb", borderRadius: 8, borderLeft: "4px solid var(--primary)" }}>
                        <div style={{ whiteSpace: "pre-wrap" }}>
                            {issueDesc || "—"}
                        </div>
                    </div>
                </div>
            </div>

            <AssignModal
                open={md.open}
                title={md.mode === "assign" ? "Gán ticket cho nhân viên" : "Chuyển sang kỹ thuật"}
                onClose={closeModal}
                onConfirm={onPick}
            />
        </div>
    );
}
