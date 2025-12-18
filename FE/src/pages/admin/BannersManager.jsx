import React, { useEffect, useState, useCallback } from "react";
import { bannersApi } from "../../services/banners";
import { useToast } from "../../contexts/ToastContext";
import "../../styles/BannersManager.css";

const AREA = {
    MAIN: "HOME_MAIN",
    SIDE: "HOME_SIDE",
};

const emptyForm = (placement) => ({
    placement,
    title: "",
    mediaUrl: "",
    linkUrl: "",
    linkTarget: "_self",
    sortOrder: 0,
    isActive: true,
    startAt: "",
    endAt: "",
});

const mediaPreview = (url) => {
    if (!url) return null;
    return (
        <div className="bm-preview">
            <img src={url} alt="banner" />
        </div>
    );
};

export default function BannersManager() {
    const { showToast, showConfirm } = useToast();

    const [loading, setLoading] = useState(false);
    const [mainItems, setMainItems] = useState([]);
    const [sideItems, setSideItems] = useState([]);

    const [modalOpen, setModalOpen] = useState(false);
    const [editing, setEditing] = useState(null);
    const [form, setForm] = useState(emptyForm(AREA.MAIN));
    const [saving, setSaving] = useState(false);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const [main, side] = await Promise.all([
                bannersApi.list({ placement: AREA.MAIN }),
                bannersApi.list({ placement: AREA.SIDE }),
            ]);

            const mainList = Array.isArray(main) ? main : main?.items || [];
            const sideList = Array.isArray(side) ? side : side?.items || [];

            setMainItems(
                [...mainList].sort((a, b) => (a?.sortOrder ?? 0) - (b?.sortOrder ?? 0))
            );
            setSideItems(
                [...sideList].sort((a, b) => (a?.sortOrder ?? 0) - (b?.sortOrder ?? 0))
            );
        } catch (e) {
            console.error(e);
            const msg =
                e?.response?.data?.message ||
                e?.message ||
                "Không thể tải danh sách banner";
            showToast({ type: "error", title: "Lỗi", message: msg });
        } finally {
            setLoading(false);
        }
    }, [showToast]);

    useEffect(() => {
        load();
    }, [load]);

    const openCreate = () => {
        setEditing(null);
        setForm(emptyForm(AREA.MAIN)); // mặc định slider
        setModalOpen(true);
    };

    const openEdit = (item) => {
        setEditing(item);
        setForm({
            placement: item.placement,
            title: item.title || "",
            mediaUrl: item.mediaUrl || "",
            linkUrl: item.linkUrl || "",
            linkTarget: item.linkTarget || "_self",
            sortOrder: item.sortOrder ?? 0,
            isActive: item.isActive !== false,
            startAt: item.startAt ? String(item.startAt).slice(0, 16) : "",
            endAt: item.endAt ? String(item.endAt).slice(0, 16) : "",
        });

        // Nếu DB đang có record video cũ, báo nhẹ cho bạn biết
        if (String(item?.mediaType || "").toLowerCase() === "video") {
            showToast({
                type: "warning",
                title: "Lưu ý",
                message:
                    "Banner này đang là video trong DB. Màn này hiện chỉ hỗ trợ ảnh (image). Nếu bạn lưu lại, hệ thống sẽ set MediaType = image.",
            });
        }

        setModalOpen(true);
    };

    const closeModal = () => {
        if (saving) return;
        setModalOpen(false);
    };

    const onSave = async () => {
        if (!form.mediaUrl?.trim()) {
            showToast({
                type: "warning",
                title: "Thiếu dữ liệu",
                message: "Vui lòng nhập MediaUrl (link ảnh Cloudinary) hoặc đường dẫn ảnh",
            });
            return;
        }

        setSaving(true);
        try {
            const payload = {
                placement: form.placement,
                title: form.title?.trim() || null,
                mediaUrl: form.mediaUrl.trim(),
                // ✅ Image-only: luôn gửi image để không phải sửa DB
                mediaType: "image",
                linkUrl: form.linkUrl?.trim() || null,
                linkTarget: form.linkTarget || "_self",
                sortOrder: Number(form.sortOrder || 0),
                isActive: !!form.isActive,
                startAt: form.startAt ? new Date(form.startAt).toISOString() : null,
                endAt: form.endAt ? new Date(form.endAt).toISOString() : null,
            };

            if (editing?.id) {
                await bannersApi.update(editing.id, payload);
                showToast({
                    type: "success",
                    title: "Thành công",
                    message: "Đã cập nhật banner",
                });
            } else {
                await bannersApi.create(payload);
                showToast({
                    type: "success",
                    title: "Thành công",
                    message: "Đã thêm banner",
                });
            }

            setModalOpen(false);
            await load();
        } catch (e) {
            console.error(e);
            const msg =
                e?.response?.data?.message || e?.message || "Không thể lưu banner";
            showToast({ type: "error", title: "Lỗi", message: msg });
        } finally {
            setSaving(false);
        }
    };

    const onDelete = async (item) => {
        const ok = await showConfirm({
            title: "Xóa banner?",
            message: "Thao tác này không thể hoàn tác.",
            confirmText: "Xóa",
            cancelText: "Hủy",
        });
        if (!ok) return;

        try {
            await bannersApi.remove(item.id);
            showToast({ type: "success", title: "Đã xóa", message: "Đã xóa banner" });
            await load();
        } catch (e) {
            console.error(e);
            const msg =
                e?.response?.data?.message || e?.message || "Không thể xóa banner";
            showToast({ type: "error", title: "Lỗi", message: msg });
        }
    };

    const renderTable = (items) => (
        <table className="bm-table">
            <thead>
                <tr>
                    <th style={{ width: 110 }}>Xem trước</th>
                    <th>Đường dẫn</th>
                    <th style={{ width: 70 }}>Thứ tự</th>
                    <th style={{ width: 90 }}>Trạng thái</th>
                    <th style={{ width: 140 }}>Hành động</th>
                </tr>
            </thead>
            <tbody>
                {items.map((x) => (
                    <tr key={x.id}>
                        <td>{mediaPreview(x.mediaUrl)}</td>
                        <td className="bm-link-cell">
                            <div className="bm-link-main">{x.linkUrl || <i>(không có link)</i>}</div>
                            <div className="bm-title-sub">target: {x.linkTarget || "_self"}</div>
                            <div className="bm-title-sub">{x.placement}</div>
                        </td>
                        <td>{x.sortOrder ?? 0}</td>
                        <td>
                            <span className={`bm-pill ${x.isActive ? "on" : "off"}`}>
                                {x.isActive ? "On" : "Off"}
                            </span>
                        </td>
                        <td>
                            <button className="bm-btn" onClick={() => openEdit(x)}>
                                Sửa
                            </button>
                            <button className="bm-btn bm-danger" onClick={() => onDelete(x)}>
                                Xóa
                            </button>
                        </td>
                    </tr>
                ))}

                {items.length === 0 && (
                    <tr>
                        <td colSpan={6} className="bm-empty">
                            Chưa có banner.
                        </td>
                    </tr>
                )}
            </tbody>
        </table>
    );

    return (
        <div className="bm-wrap">
            {/* 1 khung duy nhất */}
            <div className="bm-section">
                <div className="bm-header">
                    <div>
                        <h3 className="bm-h3">Banner trang chủ</h3>
                        <div className="bm-note">
                            • Slider trái: nhiều banner. • Banner phải: hiển thị 2 banner có sortOrder
                            nhỏ nhất (trên/dưới).
                        </div>
                    </div>

                    <div className="bm-header-actions">
                        <button className="bm-btn" onClick={load} disabled={loading}>
                            Tải lại
                        </button>
                        <button className="bm-btn bm-primary" onClick={openCreate}>
                            + Thêm banner
                        </button>
                    </div>
                </div>

                {/* 2 list song song */}
                <div className="bm-cols">
                    <div className="bm-col">
                        <div className="bm-col-title">Slider (trái) — HOME_MAIN</div>
                        {renderTable(mainItems)}
                    </div>

                    <div className="bm-col">
                        <div className="bm-col-title">Banner (phải) — HOME_SIDE</div>
                        {renderTable(sideItems)}
                    </div>
                </div>
            </div>

            {/* Modal */}
            {modalOpen && (
                <div className="bm-modal-overlay" onMouseDown={closeModal}>
                    <div className="bm-modal" onMouseDown={(e) => e.stopPropagation()}>
                        <div className="bm-modal-top">
                            <div className="bm-modal-title">{editing ? "Sửa banner" : "Thêm banner"}</div>
                            <button className="bm-x" onClick={closeModal} disabled={saving}>
                                ✕
                            </button>
                        </div>

                        {/* ✅ Layout gọn hơn khi bỏ MediaType */}
                        <div className="bm-grid">
                            <div className="bm-field">
                                <label>Vị trí</label>
                                <div className="bm-radio-row">
                                    <label>
                                        <input
                                            type="radio"
                                            name="area"
                                            checked={form.placement === AREA.MAIN}
                                            onChange={() => setForm((p) => ({ ...p, placement: AREA.MAIN }))}
                                        />
                                        Slider (trái)
                                    </label>
                                    <label>
                                        <input
                                            type="radio"
                                            name="area"
                                            checked={form.placement === AREA.SIDE}
                                            onChange={() => setForm((p) => ({ ...p, placement: AREA.SIDE }))}
                                        />
                                        Banner (phải)
                                    </label>
                                </div>
                            </div>

                            <div className="bm-field">
                                <label>Thứ tự (SortOrder)</label>
                                <input
                                    type="number"
                                    value={form.sortOrder}
                                    onChange={(e) => setForm((p) => ({ ...p, sortOrder: e.target.value }))}
                                />
                            </div>

                            <div className="bm-field bm-span-2">
                                <label>Title</label>
                                <input
                                    value={form.title}
                                    onChange={(e) => setForm((p) => ({ ...p, title: e.target.value }))}
                                    placeholder="(có thể để trống)"
                                />
                            </div>

                            <div className="bm-field bm-span-2">
                                <label>MediaUrl (ảnh) *</label>
                                <input
                                    value={form.mediaUrl}
                                    onChange={(e) => setForm((p) => ({ ...p, mediaUrl: e.target.value }))}
                                    placeholder="Dán link ảnh Cloudinary (secure_url) hoặc /uploads/..."
                                />
                            </div>

                            <div className="bm-field bm-span-2">
                                <label>LinkUrl</label>
                                <input
                                    value={form.linkUrl}
                                    onChange={(e) => setForm((p) => ({ ...p, linkUrl: e.target.value }))}
                                    placeholder="VD: /products/... hoặc https://..."
                                />
                            </div>

                            <div className="bm-field">
                                <label className="bm-label">Trạng thái</label>

                                <div className="bm-status-row">
                                    <label className="bm-switch">
                                        <input
                                            type="checkbox"
                                            checked={form.isActive}
                                            onChange={(e) => setForm((p) => ({ ...p, isActive: e.target.checked }))}
                                        />
                                        <span className="bm-switch-ui" />
                                    </label>

                                    <span className={`bm-status-text ${form.isActive ? "on" : "off"}`}>
                                        {form.isActive ? "Đang bật" : "Đang tắt"}
                                    </span>
                                </div>
                            </div>


                            <div className="bm-field">
                                <label>LinkTarget</label>
                                <select
                                    value={form.linkTarget}
                                    onChange={(e) => setForm((p) => ({ ...p, linkTarget: e.target.value }))}
                                >
                                    <option value="_self">_self</option>
                                    <option value="_blank">_blank</option>
                                </select>
                            </div>

                            <div className="bm-field">
                                <label>StartAt</label>
                                <input
                                    type="datetime-local"
                                    value={form.startAt}
                                    onChange={(e) => setForm((p) => ({ ...p, startAt: e.target.value }))}
                                />
                            </div>

                            <div className="bm-field">
                                <label>EndAt</label>
                                <input
                                    type="datetime-local"
                                    value={form.endAt}
                                    onChange={(e) => setForm((p) => ({ ...p, endAt: e.target.value }))}
                                />
                            </div>
                        </div>

                        <div className="bm-modal-actions">
                            <button className="bm-btn" onClick={closeModal} disabled={saving}>
                                Hủy
                            </button>
                            <button className="bm-btn bm-primary" onClick={onSave} disabled={saving}>
                                {saving ? "Đang lưu..." : "Lưu"}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
