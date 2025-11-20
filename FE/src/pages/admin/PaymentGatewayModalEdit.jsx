import React, { useEffect, useState } from 'react';
import { paymentGatewaysApi } from '../../services/paymentGateways';
import { useToast } from '../../contexts/ToastContext';
import '../../styles/PaymentGateways.css';

export default function PaymentGatewayModalEdit({ isOpen, gateway, onClose, onSaved }) {
    const { showToast } = useToast();
    const [form, setForm] = useState(null);
    const [saving, setSaving] = useState(false);
    const [errors, setErrors] = useState({});

    useEffect(() => {
        if (gateway) {
            console.log('🔵 PaymentGatewayModalEdit opened with gateway:', gateway);
            setForm({
                id: gateway.id,
                name: gateway.name || '',
                callbackUrl: gateway.callbackUrl || '',
                isActive: gateway.isActive ?? true
            });
        } else {
            setForm(null);
        }
    }, [gateway]);

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

    const validate = () => {
        const errs = {};
        const nm = (form.name || '').trim();
        const url = (form.callbackUrl || '').trim();

        if (!nm || nm.length < 2) errs.name = 'Tên cổng thanh toán ít nhất 2 ký tự';
        if (!url) errs.callbackUrl = 'Callback URL không được để trống';
        if (url && !isValidUrl(url)) errs.callbackUrl = 'URL không hợp lệ';

        setErrors(errs);
        return Object.keys(errs).length === 0;
    };

    const isValidUrl = (string) => {
        try {
            const url = new URL(string);
            return url.protocol === 'http:' || url.protocol === 'https:';
        } catch {
            return false;
        }
    };

    const handleSave = async () => {
        if (!validate()) return;

        setSaving(true);
        try {
            const payload = {
                name: form.name.trim(),
                callbackUrl: form.callbackUrl.trim(),
                isActive: form.isActive
            };

            console.log('📤 Updating payment gateway:', payload);

            const resp = await paymentGatewaysApi.update(form.id, payload);
            const data = resp && resp.data !== undefined ? resp.data : resp;

            console.log('✅ Payment gateway updated:', data);

            showToast({ type: 'success', title: 'Đã lưu', message: 'Cập nhật cổng thanh toán thành công' });
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
        <div className="pg-modal-overlay">
            <button
                className="pg-modal-backdrop"
                onClick={onClose}
                aria-label="Đóng modal"
            />
            <div className="pg-modal-content" onClick={e => e.stopPropagation()}>
                <div className="pg-modal-header">
                    <h3>Chỉnh sửa cổng thanh toán</h3>
                    <button className="pg-modal-close" onClick={onClose} aria-label="Đóng">
                        <svg viewBox="0 0 24 24" fill="currentColor">
                            <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" />
                        </svg>
                    </button>
                </div>

                <div className="pg-modal-body">
                    <div className="pg-form-fields">
                        <div className="pg-form-group">
                            <label>Tên cổng thanh toán</label>
                            <input
                                type="text"
                                value={form.name}
                                onChange={e => updateField('name', e.target.value)}
                                className={errors.name ? 'error' : ''}
                            />
                            {errors.name && <span className="pg-error-text">{errors.name}</span>}
                        </div>

                        <div className="pg-form-group">
                            <label>Callback URL</label>
                            <input
                                type="text"
                                value={form.callbackUrl}
                                onChange={e => updateField('callbackUrl', e.target.value)}
                                className={errors.callbackUrl ? 'error' : ''}
                            />
                            {errors.callbackUrl && <span className="pg-error-text">{errors.callbackUrl}</span>}
                        </div>

                        <div className="pg-form-group">
                            <label className="pg-checkbox-label">
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

                <div className="pg-modal-footer">
                    <button className="pg-btn-secondary" onClick={onClose} disabled={saving}>
                        Hủy
                    </button>
                    <button className="pg-btn-primary" onClick={handleSave} disabled={saving}>
                        {saving ? 'Đang lưu...' : 'Lưu thay đổi'}
                    </button>
                </div>
            </div>
        </div>
    );
}