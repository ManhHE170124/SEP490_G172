import React, { useEffect, useState, useCallback } from 'react';
import { paymentGatewaysApi } from '../../services/paymentGateways';
import { useToast } from '../../contexts/ToastContext';
import PaymentGatewayModalAdd from './PaymentGatewayModalAdd';
import PaymentGatewayModalEdit from './PaymentGatewayModalEdit';
import '../../styles/PaymentGateways.css';

export default function PaymentGatewaysManager() {
    const { showToast, showConfirm } = useToast();
    const [gateways, setGateways] = useState([]);
    const [loading, setLoading] = useState(false);
    const [showAddModal, setShowAddModal] = useState(false);
    const [editingGateway, setEditingGateway] = useState(null);

    const loadGateways = useCallback(async () => {
        console.log('🔵 Loading payment gateways...');
        setLoading(true);
        try {
            const resp = await paymentGatewaysApi.getAll();
            const data = resp && resp.data !== undefined ? resp.data : resp;
            console.log('✅ Payment gateways loaded:', data);
            setGateways(Array.isArray(data) ? data : []);
        } catch (err) {
            console.error('❌ Load gateways error:', err);
            console.error('❌ Error details:', err.response);
            showToast({ type: 'error', title: 'Lỗi', message: 'Không thể tải danh sách cổng thanh toán' });
        } finally {
            setLoading(false);
        }
    }, [showToast]);

    useEffect(() => {
        console.log('🔵 PaymentGatewaysManager mounted');
        // Delay 200ms để load sau LayoutSections (tránh conflict)
        const timer = setTimeout(() => {
            loadGateways();
        }, 200);

        return () => clearTimeout(timer);
    }, [loadGateways]);

    const handleCreated = useCallback(async (newGateway) => {
        console.log('✅ Gateway created:', newGateway);
        await loadGateways();
    }, [loadGateways]);

    const handleSaved = useCallback(async (updatedGateway) => {
        console.log('✅ Gateway updated:', updatedGateway);
        await loadGateways();
    }, [loadGateways]);

    const handleEdit = useCallback((gateway) => {
        console.log('✏️ Edit clicked for gateway:', gateway);
        setEditingGateway(gateway);
    }, []);

    const handleOpenAddModal = useCallback(() => {
        console.log('🔵 Opening add modal');
        setShowAddModal(true);
    }, []);

    const handleCloseAddModal = useCallback(() => {
        console.log('❌ Closing add modal');
        setShowAddModal(false);
    }, []);

    const handleCloseEditModal = useCallback(() => {
        console.log('❌ Closing edit modal');
        setEditingGateway(null);
    }, []);

    const handleDelete = useCallback((gateway) => {
        console.log('🗑️ Delete clicked for gateway:', gateway);
        showConfirm({
            title: 'Xác nhận xoá',
            message: `Bạn có chắc muốn xoá cổng thanh toán "${gateway.name}"?`,
            confirmText: 'Xoá',
            cancelText: 'Hủy',
            onConfirm: async () => {
                try {
                    await paymentGatewaysApi.remove(gateway.id);
                    showToast({ type: 'success', title: 'Đã xoá', message: 'Cổng thanh toán đã được xoá' });
                    await loadGateways();
                } catch (err) {
                    console.error('❌ Delete error:', err);
                    showToast({ type: 'error', title: 'Lỗi', message: 'Không thể xoá cổng thanh toán' });
                }
            }
        });
    }, [showConfirm, showToast, loadGateways]);

    const handleToggleActive = useCallback(async (gateway) => {
        try {
            await paymentGatewaysApi.toggle(gateway.id);
            showToast({
                type: 'success',
                title: 'Cập nhật',
                message: `Cổng thanh toán đã ${!gateway.isActive ? 'kích hoạt' : 'ẩn'}`
            });
            await loadGateways();
        } catch (err) {
            console.error('❌ Toggle active error:', err);
            showToast({ type: 'error', title: 'Lỗi', message: 'Không thể cập nhật trạng thái' });
        }
    }, [showToast, loadGateways]);

    const copyCallbackUrl = useCallback(async (gateway) => {
        try {
            await navigator.clipboard.writeText(gateway.callbackUrl);
            showToast({ type: 'success', title: 'Đã sao chép', message: 'URL đã được sao chép vào clipboard' });
        } catch (err) {
            console.error('❌ Copy error:', err);
            showToast({ type: 'error', title: 'Lỗi', message: 'Không thể sao chép URL' });
        }
    }, [showToast]);

    if (loading) {
        return (
            <details open className="card">
                <summary>Cấu hình cổng thanh toán</summary>
                <div className="content" style={{ padding: '20px', textAlign: 'center' }}>
                    <div>Đang tải...</div>
                </div>
            </details>
        );
    }

    return (
        <>
            <details open className="card">
                <summary>Cấu hình cổng thanh toán</summary>
                <div className="content">
                    <div className="small" style={{ marginBottom: '12px' }}>
                        Quản lý các cổng thanh toán tích hợp trên website.
                    </div>
                    <div className="table">
                        <table>
                            <thead>
                                <tr>
                                    <th>Tên cổng</th>
                                    <th>Link/Callback</th>
                                    <th>Trạng thái</th>
                                    <th>Hành động</th>
                                </tr>
                            </thead>
                            <tbody>
                                {gateways && gateways.length > 0 ? (
                                    gateways.map((g) => (
                                        <tr key={g.id}>
                                            <td><strong>{g.name}</strong></td>
                                            <td style={{
                                                maxWidth: '300px',
                                                overflow: 'hidden',
                                                textOverflow: 'ellipsis',
                                                whiteSpace: 'nowrap'
                                            }}>
                                                <code style={{ fontSize: '12px' }}>{g.callbackUrl}</code>
                                            </td>
                                            <td>
                                                <span className={`status ${g.isActive ? 'on' : 'off'}`}>
                                                    {g.isActive ? 'Kích hoạt' : 'Ẩn'}
                                                </span>
                                            </td>
                                            <td className="row-actions">
                                                <button
                                                    className="icon-btn"
                                                    onClick={() => copyCallbackUrl(g)}
                                                    title="Copy URL"
                                                >
                                                    📄
                                                </button>
                                                <button
                                                    className="icon-btn"
                                                    onClick={() => handleToggleActive(g)}
                                                    title={g.isActive ? 'Ẩn' : 'Kích hoạt'}
                                                >
                                                    👁️
                                                </button>
                                                <button
                                                    className="icon-btn"
                                                    onClick={() => handleEdit(g)}
                                                    title="Chỉnh sửa"
                                                >
                                                    ✏️
                                                </button>
                                                <button
                                                    className="icon-btn"
                                                    onClick={() => handleDelete(g)}
                                                    title="Xoá"
                                                >
                                                    🗑️
                                                </button>
                                            </td>
                                        </tr>
                                    ))
                                ) : (
                                    <tr>
                                        <td colSpan="4" style={{ padding: '12px', textAlign: 'center' }}>
                                            Chưa có cổng thanh toán nào
                                        </td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                    <div style={{ marginTop: '10px' }}>
                        <button
                            className="btn"
                            onClick={handleOpenAddModal}
                            type="button"
                        >
                            + Thêm cổng thanh toán
                        </button>
                    </div>
                </div>
            </details>

            {/* Modals */}
            {showAddModal && (
                <PaymentGatewayModalAdd
                    isOpen={showAddModal}
                    onClose={handleCloseAddModal}
                    onCreated={handleCreated}
                />
            )}

            {editingGateway && (
                <PaymentGatewayModalEdit
                    isOpen={!!editingGateway}
                    gateway={editingGateway}
                    onClose={handleCloseEditModal}
                    onSaved={handleSaved}
                />
            )}
        </>
    );
}