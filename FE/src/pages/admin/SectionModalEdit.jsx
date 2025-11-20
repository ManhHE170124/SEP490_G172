import React, { useEffect, useState } from 'react';
import { layoutSectionsApi } from '../../services/layoutSections';
import { useToast } from '../../contexts/ToastContext';
import '../../styles/LayoutSections.css';

export default function SectionModalEdit({ isOpen, section, onClose, onSaved }) {
    const { showToast } = useToast();
    const [form, setForm] = useState(null);
    const [saving, setSaving] = useState(false);

    useEffect(() => {
        if (section) {
            console.log('🔵 SectionModalEdit opened with section:', section);
            let s = {
                id: section.id,
                sectionKey: section.sectionKey ?? section.SectionKey,
                sectionName: section.sectionName ?? section.SectionName,
                displayOrder: section.displayOrder ?? section.DisplayOrder,
                isActive: section.isActive ?? section.IsActive,
                settingsObj: {}
            };
            try {
                const raw = section.settings ?? section.Settings;
                if (raw) s.settingsObj = typeof raw === 'string' ? JSON.parse(raw) : raw;
            } catch {
                s.settingsObj = {};
            }
            setForm(s);
        } else {
            setForm(null);
        }
    }, [section]);

    useEffect(() => {
        if (isOpen) {
            document.body.style.overflow = 'hidden';
        }
        return () => {
            document.body.style.overflow = '';
        };
    }, [isOpen]);

    if (!isOpen || !form) return null;

    const updateField = (k, v) => setForm(prev => ({ ...prev, [k]: v }));
    const updateSetting = (k, v) => setForm(prev => ({ ...prev, settingsObj: { ...(prev.settingsObj || {}), [k]: v } }));

    const handleSave = async () => {
        setSaving(true);
        try {
            const payload = {
                sectionName: form.sectionName,
                sectionKey: form.sectionKey,
                displayOrder: form.displayOrder,
                isActive: form.isActive,
                settings: JSON.stringify(form.settingsObj || {})
            };

            console.log('📤 Updating section:', payload);

            const resp = await layoutSectionsApi.update(form.id, payload);
            const data = resp && resp.data !== undefined ? resp.data : resp;

            console.log('✅ Section updated:', data);

            showToast({ type: 'success', title: 'Đã lưu', message: 'Cập nhật section thành công' });
            if (typeof onSaved === 'function') onSaved(data);
            onClose();
        } catch (err) {
            console.error('❌ Update error:', err);
            showToast({ type: 'error', title: 'Lỗi', message: err?.response?.data?.message || 'Cập nhật thất bại' });
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="ls-modal-overlay">
            <button
                className="ls-modal-backdrop"
                onClick={onClose}
                aria-label="Đóng modal"
            />
            <div className="ls-modal-content" onClick={e => e.stopPropagation()}>
                <div className="ls-modal-header">
                    <h3>Chỉnh sửa Section</h3>
                    <button className="ls-modal-close" onClick={onClose} aria-label="Đóng">
                        <svg viewBox="0 0 24 24" fill="currentColor">
                            <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" />
                        </svg>
                    </button>
                </div>

                <div className="ls-modal-body">
                    <div className="ls-form-fields">
                        <div className="ls-form-group">
                            <label>Section Name</label>
                            <input
                                type="text"
                                value={form.sectionName}
                                onChange={e => updateField('sectionName', e.target.value)}
                            />
                        </div>

                        <div className="ls-form-group">
                            <label>Section Key</label>
                            <input
                                type="text"
                                value={form.sectionKey}
                                onChange={e => updateField('sectionKey', e.target.value)}
                            />
                        </div>

                        <div className="ls-form-group">
                            <label>Display Order</label>
                            <input
                                type="number"
                                value={form.displayOrder || 0}
                                onChange={e => updateField('displayOrder', parseInt(e.target.value || '0'))}
                            />
                        </div>

                        <div className="ls-form-group">
                            <label>Template</label>
                            <input
                                type="text"
                                value={form.settingsObj?.template || ''}
                                onChange={e => updateSetting('template', e.target.value)}
                            />
                        </div>

                        <div className="ls-form-group">
                            <label className="ls-checkbox-label">
                                <input
                                    type="checkbox"
                                    checked={form.isActive}
                                    onChange={e => updateField('isActive', e.target.checked)}
                                />
                                <span>Kích hoạt</span>
                            </label>
                        </div>
                    </div>
                </div>

                <div className="ls-modal-footer">
                    <button className="ls-btn-secondary" onClick={onClose} disabled={saving}>
                        Hủy
                    </button>
                    <button className="ls-btn-primary" onClick={handleSave} disabled={saving}>
                        {saving ? 'Đang lưu...' : 'Lưu thay đổi'}
                    </button>
                </div>
            </div>
        </div>
    );
}