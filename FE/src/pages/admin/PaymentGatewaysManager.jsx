import React, { useEffect, useState, useCallback } from 'react';
import { paymentGatewaysApi } from '../../services/paymentGateways';
import { useToast } from '../../contexts/ToastContext';
import '../../styles/PaymentGateways.css';

export default function PaymentGatewaysManager() {
    const { showToast, showConfirm } = useToast();

    const [loading, setLoading] = useState(false);

    const [clientId, setClientId] = useState('');
    const [apiKey, setApiKey] = useState('');
    const [checksumKey, setChecksumKey] = useState('');
    const [isActive, setIsActive] = useState(true);

    const [hasApiKey, setHasApiKey] = useState(false);
    const [hasChecksumKey, setHasChecksumKey] = useState(false);

    const loadPayOS = useCallback(async () => {
        setLoading(true);
        try {
            const resp = await paymentGatewaysApi.getPayOS();
            const data = resp && resp.data !== undefined ? resp.data : resp;

            setClientId(data?.clientId || '');
            setHasApiKey(!!data?.hasApiKey);
            setHasChecksumKey(!!data?.hasChecksumKey);

            // isActive không nằm trong response view dto của bạn => mặc định true
            // nếu sau này bạn trả isActive về thì set tại đây
            setIsActive(true);
        } catch (err) {
            console.error('❌ Load PayOS error:', err);
            showToast({ type: 'error', title: 'Lỗi', message: 'Không thể tải cấu hình PayOS' });
        } finally {
            setLoading(false);
        }
    }, [showToast]);

    useEffect(() => {
        loadPayOS();
    }, [loadPayOS]);

    const handleSave = async () => {
        showConfirm({
            title: 'Cập nhật PayOS',
            message: 'Bạn có chắc muốn cập nhật cấu hình PayOS không?',
            confirmText: 'Lưu',
            cancelText: 'Hủy',
            onConfirm: async () => {
                setLoading(true);
                try {
                    const payload = {
                        clientId: (clientId || '').trim(),
                        apiKey: apiKey?.trim() ? apiKey.trim() : null,               // để trống => giữ key cũ
                        checksumKey: checksumKey?.trim() ? checksumKey.trim() : null, // để trống => giữ key cũ
                        isActive: !!isActive
                    };

                    const resp = await paymentGatewaysApi.updatePayOS(payload);
                    const data = resp && resp.data !== undefined ? resp.data : resp;

                    setHasApiKey(!!data?.hasApiKey);
                    setHasChecksumKey(!!data?.hasChecksumKey);

                    // Clear importing secrets sau khi lưu để tránh lộ
                    setApiKey('');
                    setChecksumKey('');

                    showToast({ type: 'success', title: 'Đã lưu', message: 'Cập nhật PayOS thành công' });

                    // Reload lại để chắc chắn view đồng bộ
                    await loadPayOS();
                } catch (err) {
                    console.error('❌ Update PayOS error:', err);
                    const serverMsg = err?.response?.data?.message || 'Cập nhật thất bại';
                    showToast({ type: 'error', title: 'Lỗi', message: serverMsg });
                } finally {
                    setLoading(false);
                }
            }
        });
    };

    return (
        <details open className="card">
            <summary>Cấu hình PayOS</summary>

            <div className="content">
                <div className="small" style={{ marginBottom: '12px' }}>
                    Chỉ dùng 1 cổng PayOS. ApiKey/ChecksumKey không hiển thị lại — muốn đổi thì nhập key mới và lưu.
                </div>

                <div className="pg-config">
                    <div className="pg-form-group">
                        <label>ClientId</label>
                        <input
                            type="text"
                            value={clientId}
                            onChange={(e) => setClientId(e.target.value)}
                            placeholder="PayOS ClientId"
                            disabled={loading}
                        />
                    </div>

                    <div className="pg-form-group">
                        <label>
                            ApiKey {hasApiKey ? <span className="pg-badge ok">đã cấu hình</span> : <span className="pg-badge warn">chưa có</span>}
                        </label>
                        <input
                            type="password"
                            value={apiKey}
                            onChange={(e) => setApiKey(e.target.value)}
                            placeholder="Nhập ApiKey mới (để trống để giữ nguyên)"
                            disabled={loading}
                        />
                    </div>

                    <div className="pg-form-group">
                        <label>
                            ChecksumKey {hasChecksumKey ? <span className="pg-badge ok">đã cấu hình</span> : <span className="pg-badge warn">chưa có</span>}
                        </label>
                        <input
                            type="password"
                            value={checksumKey}
                            onChange={(e) => setChecksumKey(e.target.value)}
                            placeholder="Nhập ChecksumKey mới (để trống để giữ nguyên)"
                            disabled={loading}
                        />
                    </div>

                    <div className="pg-form-group">
                        <label className="pg-checkbox-label">
                            <input
                                type="checkbox"
                                checked={isActive}
                                onChange={(e) => setIsActive(e.target.checked)}
                                disabled={loading}
                            />
                            <span>Kích hoạt PayOS</span>
                        </label>
                    </div>

                    <div style={{ marginTop: '10px', display: 'flex', gap: '10px' }}>
                        <button className="btn" type="button" onClick={loadPayOS} disabled={loading}>
                            {loading ? 'Đang tải...' : 'Tải lại'}
                        </button>

                        <button className="pg-btn-primary" type="button" onClick={handleSave} disabled={loading}>
                            {loading ? 'Đang lưu...' : 'Lưu cấu hình'}
                        </button>
                    </div>
                </div>
            </div>
        </details>
    );
}
