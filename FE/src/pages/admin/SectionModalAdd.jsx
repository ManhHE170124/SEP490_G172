import React, { useEffect, useRef, useState } from 'react';
import { layoutSectionsApi } from '../../services/layoutSections';
import { useToast } from '../../contexts/ToastContext';
import '../../styles/LayoutSections.css';

export default function SectionModalAdd({ isOpen, onClose, onCreated, defaultOrder }) {
    const { showToast } = useToast();
    const [name, setName] = useState('');
    const [key, setKey] = useState('');
    const [isActive, setIsActive] = useState(true);
    const [template, setTemplate] = useState('banner');
    const [saving, setSaving] = useState(false);
    const [errors, setErrors] = useState({});
    const [isKeyEdited, setIsKeyEdited] = useState(false);
    const nameRef = useRef(null);

    useEffect(() => {
        if (isOpen) {
            console.log('🔵 SectionModalAdd opened');
            document.body.style.overflow = 'hidden';
            setName('');
            setKey('');
            setIsActive(true);
            setTemplate('banner');
            setErrors({});
            setIsKeyEdited(false);

            setTimeout(() => {
                if (nameRef.current) {
                    nameRef.current.focus();
                }
            }, 100);
        }

        return () => {
            document.body.style.overflow = '';
        };
    }, [isOpen]);

    const generateKey = (n) => {
        if (!n) return '';
        return n.toLowerCase().trim().replace(/\s+/g, '-').replace(/[^a-z0-9-_]/g, '');
    };

    const handleNameChange = (v) => {
        setName(v);
        if (!isKeyEdited) setKey(generateKey(v));
    };

    const handleKeyChange = (v) => {
        setKey(v);
        setIsKeyEdited(true);
    };

    const validate = () => {
        const errs = {};
        const nm = (name || '').trim();
        const k = (key || '').trim();
        if (!nm || nm.length < 2) errs.name = 'Tên section ít nhất 2 ký tự';
        if (!k) errs.key = 'Key không được để trống';
        if (k && !/^[a-z0-9-_]+$/.test(k)) errs.key = 'Key chỉ chứa chữ thường, số, - và _';
        setErrors(errs);
        return Object.keys(errs).length === 0;
    };

    const handleCreate = async (e) => {
        if (e && e.preventDefault) e.preventDefault();

        if (!validate()) return;

        setSaving(true);

        try {
            const payload = {
                sectionName: name.trim(),
                sectionKey: key.trim(),
                isActive: !!isActive,
                displayOrder: typeof defaultOrder === 'number' ? defaultOrder : 9999,
                settings: JSON.stringify({ template })
            };

            console.log('📤 Creating section:', payload);

            const resp = await layoutSectionsApi.create(payload);
            const data = resp && resp.data !== undefined ? resp.data : resp;

            console.log('✅ Section created:', data);

            showToast({ type: 'success', title: 'Tạo thành công', message: 'Section đã được tạo' });

            if (typeof onCreated === 'function') onCreated(data);

            onClose();

        } catch (err) {
            console.error('❌ Create error:', err);
            const serverMsg = err?.response?.data?.message || err?.response?.data?.error;

            if (serverMsg) {
                showToast({ type: 'error', title: 'Lỗi', message: serverMsg });
            } else if (err?.response?.status === 409) {
                showToast({ type: 'error', title: 'Lỗi', message: 'SectionKey đã tồn tại' });
            } else {
                showToast({ type: 'error', title: 'Lỗi', message: 'Tạo thất bại. Vui lòng thử lại.' });
            }
        } finally {
            setSaving(false);
        }
    };

    if (!isOpen) return null;

    return (
        <div className="ls-modal-overlay">
            <button
                className="ls-modal-backdrop"
                onClick={onClose}
                aria-label="Đóng modal"
            />
            <div className="ls-modal-content" onClick={(e) => e.stopPropagation()}>
                <div className="ls-modal-header">
                    <h3>Tạo Section mới</h3>
                    <button className="ls-modal-close" onClick={onClose} aria-label="Đóng">
                        <svg viewBox="0 0 24 24" fill="currentColor">
                            <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" />
                        </svg>
                    </button>
                </div>

                <form onSubmit={handleCreate}>
                    <div className="ls-modal-body">
                        <div className="ls-form-fields">
                            <div className="ls-form-group">
                                <label>Tên section</label>
                                <input
                                    ref={nameRef}
                                    type="text"
                                    value={name}
                                    onChange={(e) => handleNameChange(e.target.value)}
                                    placeholder="Ví dụ: Khuyến mãi"
                                    className={errors.name ? 'error' : ''}
                                />
                                {errors.name && <span className="ls-error-text">{errors.name}</span>}
                            </div>

                            <div className="ls-form-group">
                                <label>Section Key</label>
                                <input
                                    type="text"
                                    value={key}
                                    onChange={(e) => handleKeyChange(e.target.value)}
                                    placeholder="ví dụ: khuyen-mai"
                                    className={errors.key ? 'error' : ''}
                                />
                                {errors.key && <span className="ls-error-text">{errors.key}</span>}
                                <small>Key dùng để ánh xạ tới component frontend.</small>
                            </div>

                            <div className="ls-form-group">
                                <label>Template</label>
                                <select value={template} onChange={(e) => setTemplate(e.target.value)}>
                                    <option value="banner">Banner</option>
                                    <option value="product-grid">Product Grid</option>
                                    <option value="blog-list">Blog Highlights</option>
                                    <option value="custom">Custom</option>
                                </select>
                            </div>

                            <div className="ls-form-group">
                                <label className="ls-checkbox-label">
                                    <input
                                        type="checkbox"
                                        checked={isActive}
                                        onChange={(e) => setIsActive(e.target.checked)}
                                    />
                                    <span>Kích hoạt ngay</span>
                                </label>
                            </div>
                        </div>
                    </div>

                    <div className="ls-modal-footer">
                        <button type="button" className="ls-btn-secondary" onClick={onClose} disabled={saving}>
                            Hủy
                        </button>
                        <button type="submit" className="ls-btn-primary" disabled={saving}>
                            {saving ? 'Đang tạo...' : 'Tạo Section'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}