import React, { useEffect, useRef, useState } from 'react';
import { paymentGatewaysApi } from '../../services/paymentGateways';
import { useToast } from '../../contexts/ToastContext';
import '../../styles/PaymentGateways.css';

export default function PaymentGatewayModalAdd({ isOpen, onClose, onCreated }) {
    const { showToast } = useToast();
    const [name, setName] = useState('');
    const [callbackUrl, setCallbackUrl] = useState('');
    const [isActive, setIsActive] = useState(true);
    const [saving, setSaving] = useState(false);
    const [errors, setErrors] = useState({});
    const nameRef = useRef(null);

    useEffect(() => {
        if (isOpen) {
            console.log('🔵 PaymentGatewayModalAdd opened');
            document.body.style.overflow = 'hidden';
            setName('');
            setCallbackUrl('');
            setIsActive(true);
            setErrors({});

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

    const validate = () => {
        const errs = {};
        const nm = (name || '').trim();
        const url = (callbackUrl || '').trim();

        if (!nm || nm.length < 2) errs.name = 'Tên cổng thanh toán ít nhất 2 ký tự';
        if (!url) errs.callbackUrl = 'Callback URL không được để trống';
        if (url && !isValidUrl(url)) errs.callbackUrl = 'URL không hợp lệ (phải bắt đầu bằng http:// hoặc https://)';

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

    const handleCreate = async (e) => {
        if (e && e.preventDefault) e.preventDefault();

        if (!validate()) return;

        setSaving(true);

        try {
            const payload = {
                name: name.trim(),
                callbackUrl: callbackUrl.trim(),
                isActive: !!isActive
            };

            console.log('📤 Creating payment gateway:', payload);

            const resp = await paymentGatewaysApi.create(payload);
            const data = resp && resp.data !== undefined ? resp.data : resp;

            console.log('✅ Payment gateway created:', data);

            showToast({ type: 'success', title: 'Tạo thành công', message: 'Cổng thanh toán đã được tạo' });

            if (typeof onCreated === 'function') onCreated(data);

            onClose();

        } catch (err) {
            console.error('❌ Create error:', err);
            const serverMsg = err?.response?.data?.message || err?.response?.data?.error;

            if (serverMsg) {
                showToast({ type: 'error', title: 'Lỗi', message: serverMsg });
            } else {
                showToast({ type: 'error', title: 'Lỗi', message: 'Tạo thất bại. Vui lòng thử lại.' });
            }
        } finally {
            setSaving(false);
        }
    };

    if (!isOpen) return null;

    return (
        <div className="pg-modal-overlay">
            <button
                className="pg-modal-backdrop"
                onClick={onClose}
                aria-label="Đóng modal"
            />
            <div className="pg-modal-content" onClick={(e) => e.stopPropagation()}>
                <div className="pg-modal-header">
                    <h3>Thêm cổng thanh toán</h3>
                    <button className="pg-modal-close" onClick={onClose} aria-label="Đóng">
                        <svg viewBox="0 0 24 24" fill="currentColor">
                            <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z" />
                        </svg>
                    </button>
                </div>

                <form onSubmit={handleCreate}>
                    <div className="pg-modal-body">
                        <div className="pg-form-fields">
                            <div className="pg-form-group">
                                <label>Tên cổng thanh toán</label>
                                <input
                                    ref={nameRef}
                                    type="text"
                                    value={name}
                                    onChange={(e) => setName(e.target.value)}
                                    placeholder="Ví dụ: VNPay, MoMo, ZaloPay"
                                    className={errors.name ? 'error' : ''}
                                />
                                {errors.name && <span className="pg-error-text">{errors.name}</span>}
                            </div>

                            <div className="pg-form-group">
                                <label>Callback URL</label>
                                <input
                                    type="text"
                                    value={callbackUrl}
                                    onChange={(e) => setCallbackUrl(e.target.value)}
                                    placeholder="https://example.com/payment/callback"
                                    className={errors.callbackUrl ? 'error' : ''}
                                />
                                {errors.callbackUrl && <span className="pg-error-text">{errors.callbackUrl}</span>}
                                <small>URL nhận thông báo kết quả thanh toán từ cổng thanh toán.</small>
                            </div>

                            <div className="pg-form-group">
                                <label className="pg-checkbox-label">
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

                    <div className="pg-modal-footer">
                        <button type="button" className="pg-btn-secondary" onClick={onClose} disabled={saving}>
                            Hủy
                        </button>
                        <button type="submit" className="pg-btn-primary" disabled={saving}>
                            {saving ? 'Đang tạo...' : 'Tạo'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}