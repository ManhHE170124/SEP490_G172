import React, { useEffect, useMemo, useRef, useState } from "react";
import "../../styles/BannersManager.css";
import { bannersApi } from "../../services/banners";
import { postsApi } from "../../services/postsApi";
import { useToast } from "../../contexts/ToastContext";

const AREA = {
    MAIN: "HOME_MAIN",
    SIDE: "HOME_SIDE",
};

const emptyForm = () => ({
    placement: AREA.MAIN,
    sortOrder: 0,
    title: "",
    mediaUrl: "",
    linkUrl: "",
    linkTarget: "_self",
    isActive: true,
    startAt: "",
    endAt: "",
});

const toLocalInput = (iso) => {
    if (!iso) return "";
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return "";
    const pad = (n) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(
        d.getHours()
    )}:${pad(d.getMinutes())}`;
};

export default function BannersManager() {
    const { showToast } = useToast();

    const [loading, setLoading] = useState(false);
    const [items, setItems] = useState([]);

    const [modalOpen, setModalOpen] = useState(false);
    const [editing, setEditing] = useState(null);
    const [form, setForm] = useState(emptyForm());
    const [saving, setSaving] = useState(false);

    // Upload image
    const fileRef = useRef(null);
    const [uploading, setUploading] = useState(false);

    const load = async () => {
        try {
            setLoading(true);
            const res = await bannersApi.list();
            // axiosClient thường unwrap res.data -> res có thể là data trực tiếp
            const data = res?.data ?? res;
            setItems(Array.isArray(data) ? data : data?.items ?? []);
        } catch (e) {
            console.error(e);
            showToast({
                type: "error",
                title: "Lỗi",
                message: e?.response?.data?.message || e?.message || "Không thể tải banners",
            });
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        load();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const grouped = useMemo(() => {
        const main = items.filter((x) => x.placement === AREA.MAIN);
        const side = items.filter((x) => x.placement === AREA.SIDE);
        return { main, side };
    }, [items]);

    const openAdd = () => {
        setEditing(null);
        setForm(emptyForm());
        setModalOpen(true);
    };

    const openEdit = (item) => {
        setEditing(item);
        setForm({
            placement: item.placement || AREA.MAIN,
            sortOrder: item.sortOrder ?? 0,
            title: item.title || "",
            mediaUrl: item.mediaUrl || "",
            linkUrl: item.linkUrl || "",
            linkTarget: item.linkTarget || "_self",
            isActive: !!item.isActive,
            startAt: toLocalInput(item.startAt),
            endAt: toLocalInput(item.endAt),
        });
        setModalOpen(true);
    };

    const closeModal = () => {
        if (saving || uploading) return;
        setModalOpen(false);
    };

    const mediaPreview = (url) => {
        if (!url) return <div className="bm-preview" />;
        return (
            <div className="bm-preview">
                <img src={url} alt="preview" />
            </div>
        );
    };

    const pickFile = () => fileRef.current?.click();

    const onFileChange = async (e) => {
        const file = e.target.files?.[0];
        e.target.value = ""; // để chọn lại cùng file vẫn trigger
        if (!file) return;

        try {
            setUploading(true);

            const resp = await postsApi.uploadImage(file);
            const data = resp?.data ?? resp;

            // backend của bạn đang trả { path, publicId? } ở PostImages upload
            const url = data?.path || data?.secure_url || data?.url;

            if (!url) throw new Error("Upload không trả về URL ảnh.");

            setForm((p) => ({ ...p, mediaUrl: url }));
            showToast({
                type: "success",
                title: "Thành công",
                message: "Đã upload ảnh và gán MediaUrl",
            });
        } catch (err) {
            console.error(err);
            showToast({
                type: "error",
                title: "Upload lỗi",
                message: err?.response?.data?.message || err?.message || "Không thể upload ảnh",
            });
        } finally {
            setUploading(false);
        }
    };

    const onSave = async () => {
        if (!form.mediaUrl?.trim()) {
            showToast({ type: "error", title: "Thiếu dữ liệu", message: "MediaUrl là bắt buộc." });
            return;
        }

        try {
            setSaving(true);

            const payload = {
                placement: form.placement,
                sortOrder: Number(form.sortOrder) || 0,
                title: form.title?.trim() || null,
                mediaUrl: form.mediaUrl?.trim(),
                linkUrl: form.linkUrl?.trim() || null,
                linkTarget: form.linkTarget || "_self",
                isActive: !!form.isActive,
                startAt: form.startAt ? new Date(form.startAt).toISOString() : null,
                endAt: form.endAt ? new Date(form.endAt).toISOString() : null,
            };

            if (editing?.id) {
                await bannersApi.update(editing.id, payload);
                showToast({ type: "success", title: "Thành công", message: "Đã cập nhật banner" });
            } else {
                await bannersApi.create(payload);
                showToast({ type: "success", title: "Thành công", message: "Đã thêm banner" });
            }

            setModalOpen(false);
            await load();
        } catch (e) {
            console.error(e);
            const msg = e?.response?.data?.message || e?.message || "Không thể lưu banner";
            showToast({ type: "error", title: "Lỗi", message: msg });
        } finally {
            setSaving(false);
        }
    };

    const onDelete = async (item) => {
        try {
            await bannersApi.remove(item.id);
            showToast({ type: "success", title: "Đã xóa", message: "Đã xóa banner" });
            await load();
        } catch (e) {
            console.error(e);
            const msg = e?.response?.data?.message || e?.message || "Không thể xóa banner";
            showToast({ type: "error", title: "Lỗi", message: msg });
        }
    };

    const renderTable = (list) => (
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
                {list.map((x) => (
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

                {list.length === 0 && (
                    <tr>
                        <td colSpan={5} className="bm-empty">
                            Chưa có banner.
                        </td>
                    </tr>
                )}
            </tbody>
        </table>
    );

    return (
        <div className="bm-wrap">
            <div className="bm-section">
                <div className="bm-header">
                    <div>
                        <div className="bm-note">Quản lý Slider (trái) và Banner (phải) trên trang chủ.</div>
                    </div>

                    <div className="bm-header-actions">
                        <button className="bm-btn" onClick={load} disabled={loading}>
                            {loading ? "Đang tải..." : "Tải lại"}
                        </button>
                        <button className="bm-btn bm-primary" onClick={openAdd}>
                            + Thêm banner
                        </button>
                    </div>
                </div>

                <div className="bm-cols">
                    <div className="bm-col">
                        <div className="bm-col-title">Slider - HOME_MAIN</div>
                        <div className="bm-table-wrap">{renderTable(grouped.main)}</div>
                    </div>

                    <div className="bm-col">
                        <div className="bm-col-title">Banner - HOME_SIDE</div>
                        <div className="bm-table-wrap">{renderTable(grouped.side)}</div>
                    </div>
                </div>
            </div>

            {modalOpen && (
                <div className="bm-modal-overlay" onMouseDown={closeModal}>
                    <div className="bm-modal" onMouseDown={(e) => e.stopPropagation()}>
                        <div className="bm-modal-top">
                            <div className="bm-modal-title">{editing?.id ? "Sửa banner" : "Thêm banner"}</div>
                            <button className="bm-x" onClick={closeModal} disabled={saving || uploading}>
                                ✕
                            </button>
                        </div>

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

                                <div className="bm-media-row">
                                    <input
                                        className="bm-media-input"
                                        value={form.mediaUrl}
                                        onChange={(e) => setForm((p) => ({ ...p, mediaUrl: e.target.value }))}
                                        placeholder="Dán link ảnh Cloudinary (secure_url) hoặc bấm 'Chọn ảnh' để upload"
                                    />

                                    <input
                                        ref={fileRef}
                                        type="file"
                                        accept="image/*"
                                        onChange={onFileChange}
                                        style={{ display: "none" }}
                                    />

                                    <button
                                        type="button"
                                        className="bm-btn"
                                        onClick={pickFile}
                                        disabled={saving || uploading}
                                        title="Chọn ảnh từ máy để upload"
                                    >
                                        {uploading ? "Đang tải..." : "Chọn ảnh"}
                                    </button>
                                </div>

                                {form.mediaUrl ? <div className="bm-thumbline">{mediaPreview(form.mediaUrl)}</div> : null}
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
                            <button className="bm-btn" onClick={closeModal} disabled={saving || uploading}>
                                Hủy
                            </button>
                            <button className="bm-btn bm-primary" onClick={onSave} disabled={saving || uploading}>
                                {saving ? "Đang lưu..." : uploading ? "Đang upload..." : "Lưu"}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
