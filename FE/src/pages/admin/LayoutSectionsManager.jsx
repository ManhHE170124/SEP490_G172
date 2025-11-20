import React, { useEffect, useState, useCallback } from 'react';
import { layoutSectionsApi } from '../../services/layoutSections';
import { useToast } from '../../contexts/ToastContext';
import SectionModalAdd from './SectionModalAdd';
import SectionModalEdit from './SectionModalEdit';

export default function LayoutSectionsManager() {
    const { showToast, showConfirm } = useToast();
    const [sections, setSections] = useState([]);
    const [loading, setLoading] = useState(false);
    const [showAddModal, setShowAddModal] = useState(false);
    const [editingSection, setEditingSection] = useState(null);

    const loadSections = useCallback(async () => {
        console.log('🔵 Loading sections...');
        setLoading(true);
        try {
            const resp = await layoutSectionsApi.getAll();
            const data = resp && resp.data !== undefined ? resp.data : resp;
            console.log('✅ Sections loaded:', data);
            setSections(Array.isArray(data) ? data : []);
        } catch (err) {
            console.error('❌ Load sections error:', err);
            console.error('❌ Error details:', err.response);
            showToast({ type: 'error', title: 'Lỗi', message: 'Không thể tải danh sách sections' });
        } finally {
            setLoading(false);
        }
    }, [showToast]);

    useEffect(() => {
        console.log('🔵 LayoutSectionsManager mounted');
        loadSections();
    }, [loadSections]);

    const handleCreated = useCallback(async (newSection) => {
        console.log('✅ Section created:', newSection);
        await loadSections();
    }, [loadSections]);

    const handleSaved = useCallback(async (updatedSection) => {
        console.log('✅ Section updated:', updatedSection);
        await loadSections();
    }, [loadSections]);

    const handleEdit = useCallback((section) => {
        console.log('✏️ Edit clicked for section:', section);
        setEditingSection(section);
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
        setEditingSection(null);
    }, []);

    const handleDelete = useCallback((section) => {
        console.log('🗑️ Delete clicked for section:', section);
        showConfirm({
            title: 'Xác nhận xoá',
            message: `Bạn có chắc muốn xoá section "${section.sectionName || section.SectionName}"?`,
            confirmText: 'Xoá',
            cancelText: 'Hủy',
            onConfirm: async () => {
                try {
                    await layoutSectionsApi.remove(section.id);
                    showToast({ type: 'success', title: 'Đã xoá', message: 'Section đã được xoá' });
                    await loadSections();
                } catch (err) {
                    console.error('❌ Delete error:', err);
                    showToast({ type: 'error', title: 'Lỗi', message: 'Không thể xoá section' });
                }
            }
        });
    }, [showConfirm, showToast, loadSections]);

    const handleToggleActive = useCallback(async (section) => {
        try {
            const currentActive = section.isActive ?? section.IsActive;
            const payload = {
                sectionKey: section.sectionKey ?? section.SectionKey,
                sectionName: section.sectionName ?? section.SectionName,
                displayOrder: section.displayOrder ?? section.DisplayOrder,
                isActive: !currentActive,
                settings: section.settings ?? section.Settings
            };
            await layoutSectionsApi.update(section.id, payload);
            showToast({
                type: 'success',
                title: 'Cập nhật',
                message: `Section đã ${!currentActive ? 'kích hoạt' : 'ẩn'}`
            });
            await loadSections();
        } catch (err) {
            console.error('❌ Toggle active error:', err);
            showToast({ type: 'error', title: 'Lỗi', message: 'Không thể cập nhật trạng thái' });
        }
    }, [showToast, loadSections]);

    const moveSection = useCallback(async (section, direction) => {
        const currentIndex = sections.findIndex(s => s.id === section.id);
        if (currentIndex === -1) return;

        const newIndex = direction === 'up' ? currentIndex - 1 : currentIndex + 1;
        if (newIndex < 0 || newIndex >= sections.length) return;

        const reordered = [...sections];
        const temp = reordered[currentIndex];
        reordered[currentIndex] = reordered[newIndex];
        reordered[newIndex] = temp;

        const updates = reordered.map((s, idx) => ({
            id: s.id,
            displayOrder: idx + 1
        }));

        try {
            await layoutSectionsApi.reorder(updates);
            showToast({ type: 'success', title: 'Đã lưu', message: 'Thứ tự đã được cập nhật' });
            await loadSections();
        } catch (err) {
            console.error('❌ Reorder error:', err);
            showToast({ type: 'error', title: 'Lỗi', message: 'Không thể thay đổi thứ tự' });
        }
    }, [sections, showToast, loadSections]);

    const getNextDisplayOrder = useCallback(() => {
        if (sections.length === 0) return 1;
        const maxOrder = Math.max(...sections.map(s => s.displayOrder ?? s.DisplayOrder ?? 0));
        return maxOrder + 1;
    }, [sections]);

    if (loading) {
        return (
            <details open className="card">
                <summary>Layout Sections</summary>
                <div className="content" style={{ padding: '20px', textAlign: 'center' }}>
                    <div>Đang tải...</div>
                </div>
            </details>
        );
    }

    return (
        <>
            <details open className="card">
                <summary>Layout Sections</summary>
                <div className="content">
                    <div className="small" style={{ marginBottom: '12px' }}>
                        Quản lý các section hiển thị trên trang. Sử dụng SectionKey để ánh xạ với component frontend.
                    </div>
                    <div className="table">
                        <table>
                            <thead>
                                <tr>
                                    <th>Section Key</th>
                                    <th>Tên Section</th>
                                    <th>Thứ tự</th>
                                    <th>Trạng thái</th>
                                    <th>Hành động</th>
                                </tr>
                            </thead>
                            <tbody>
                                {sections && sections.length > 0 ? (
                                    sections.map((s, index) => {
                                        const sectionKey = s.sectionKey ?? s.SectionKey;
                                        const sectionName = s.sectionName ?? s.SectionName;
                                        const displayOrder = s.displayOrder ?? s.DisplayOrder;
                                        const isActive = s.isActive ?? s.IsActive;

                                        return (
                                            <tr key={s.id}>
                                                <td><code>{sectionKey}</code></td>
                                                <td>{sectionName}</td>
                                                <td>
                                                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                        <span>{displayOrder}</span>
                                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
                                                            <button
                                                                className="icon-btn"
                                                                onClick={() => moveSection(s, 'up')}
                                                                disabled={index === 0}
                                                                title="Lên"
                                                                style={{ padding: '2px 6px', fontSize: '10px' }}
                                                            >
                                                                ▲
                                                            </button>
                                                            <button
                                                                className="icon-btn"
                                                                onClick={() => moveSection(s, 'down')}
                                                                disabled={index === sections.length - 1}
                                                                title="Xuống"
                                                                style={{ padding: '2px 6px', fontSize: '10px' }}
                                                            >
                                                                ▼
                                                            </button>
                                                        </div>
                                                    </div>
                                                </td>
                                                <td>
                                                    <span className={`status ${isActive ? 'on' : 'off'}`}>
                                                        {isActive ? 'Hiện' : 'Ẩn'}
                                                    </span>
                                                </td>
                                                <td className="row-actions">
                                                    <button
                                                        className="icon-btn"
                                                        onClick={() => handleToggleActive(s)}
                                                        title={isActive ? 'Ẩn' : 'Hiện'}
                                                    >
                                                        👁️
                                                    </button>
                                                    <button
                                                        className="icon-btn"
                                                        onClick={() => handleEdit(s)}
                                                        title="Chỉnh sửa"
                                                    >
                                                        ✏️
                                                    </button>
                                                    <button
                                                        className="icon-btn"
                                                        onClick={() => handleDelete(s)}
                                                        title="Xoá"
                                                    >
                                                        🗑️
                                                    </button>
                                                </td>
                                            </tr>
                                        );
                                    })
                                ) : (
                                    <tr>
                                        <td colSpan="5" style={{ padding: '12px', textAlign: 'center' }}>
                                            Chưa có section nào
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
                            + Thêm Section
                        </button>
                    </div>
                </div>
            </details>

            {/* Modals */}
            {showAddModal && (
                <SectionModalAdd
                    isOpen={showAddModal}
                    onClose={handleCloseAddModal}
                    onCreated={handleCreated}
                    defaultOrder={getNextDisplayOrder()}
                />
            )}

            {editingSection && (
                <SectionModalEdit
                    isOpen={!!editingSection}
                    section={editingSection}
                    onClose={handleCloseEditModal}
                    onSaved={handleSaved}
                />
            )}
        </>
    );
}