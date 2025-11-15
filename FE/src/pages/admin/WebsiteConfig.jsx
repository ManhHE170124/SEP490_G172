// 📝 WebsiteConfig.jsx - FULL VERSION

import React, { useEffect, useState, useRef } from 'react';
import '../../styles/WebsiteConfig.css';
import { settingsApi } from '../../services/settings';

const WebsiteConfig = () => {
    // State management
    const [config, setConfig] = useState({
        name: '',
        slogan: '',
        logoUrl: '',
        primaryColor: '#2563EB',
        secondaryColor: '#111827',
        font: 'Inter (khuyên dùng)',
        sections: [],
        contact: { address: '', phone: '', email: '' },
        smtp: { server: '', port: 587, user: '', password: '', tls: false, dkim: false },
        media: { uploadLimitMB: 10, formats: ['jpg', 'png', 'webp'] },
        social: { facebook: '', instagram: '', zalo: '', tiktok: '' },
        payments: []
    });

    const [loading, setLoading] = useState(false);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");

    const logoFileRef = useRef(null);
    const [logoPreviewUrl, setLogoPreviewUrl] = useState(null);

    // Load data on mount
    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        setLoading(true);
        setError("");
        try {
            const data = await settingsApi.getSettings();

            console.log("✅ Settings loaded:", data);

            if (data && typeof data === 'object') {
                setConfig(prev => ({
                    ...prev,
                    name: data.name || prev.name,
                    slogan: data.slogan || prev.slogan,
                    logoUrl: data.logoUrl || prev.logoUrl,
                    primaryColor: data.primaryColor || prev.primaryColor,
                    secondaryColor: data.secondaryColor || prev.secondaryColor,
                    font: data.font || prev.font,
                    sections: Array.isArray(data.sections) ? data.sections : prev.sections,
                    contact: {
                        address: data.contact?.address || prev.contact.address,
                        phone: data.contact?.phone || prev.contact.phone,
                        email: data.contact?.email || prev.contact.email,
                    },
                    smtp: {
                        server: data.smtp?.server || prev.smtp.server,
                        port: data.smtp?.port || prev.smtp.port,
                        user: data.smtp?.user || prev.smtp.user,
                        password: data.smtp?.password || prev.smtp.password,
                        tls: data.smtp?.tls ?? prev.smtp.tls,
                        dkim: data.smtp?.dkim ?? prev.smtp.dkim,
                    },
                    media: {
                        uploadLimitMB: data.media?.uploadLimitMB || prev.media.uploadLimitMB,
                        formats: Array.isArray(data.media?.formats) ? data.media.formats : prev.media.formats,
                    },
                    social: {
                        facebook: data.social?.facebook || prev.social.facebook,
                        instagram: data.social?.instagram || prev.social.instagram,
                        zalo: data.social?.zalo || prev.social.zalo,
                        tiktok: data.social?.tiktok || prev.social.tiktok,
                    },
                    payments: Array.isArray(data.payments) ? data.payments : prev.payments,
                }));

                if (data.logoUrl) {
                    setLogoPreviewUrl(data.logoUrl);
                }
            }
        } catch (err) {
            console.error("❌ Load settings error:", err);
            setError(err.message || "Không thể tải cấu hình");
        } finally {
            setLoading(false);
        }
    };

    // Update helpers
    const update = (patch) => {
        setConfig(prev => ({ ...prev, ...patch }));
    };

    const updateNested = (path, value) => {
        setConfig(prev => {
            const copy = JSON.parse(JSON.stringify(prev));
            const keys = path.split('.');
            let cur = copy;
            for (let i = 0; i < keys.length - 1; i++) {
                cur = cur[keys[i]];
            }
            cur[keys[keys.length - 1]] = value;
            return copy;
        });
    };

    // Logo handling
    const onLogoChange = (e) => {
        const f = e.target.files?.[0];
        if (!f) return;

        console.log("📷 Logo selected:", f.name);
        const url = URL.createObjectURL(f);
        setLogoPreviewUrl(url);
        logoFileRef.current = f;
    };

    // Section management
    const addSection = () => {
        const id = 'custom.' + Date.now();
        update({
            sections: [
                ...config.sections,
                { id, title: 'New Section', order: config.sections.length + 1, visible: true }
            ]
        });
    };

    const deleteSection = (id) => {
        if (!window.confirm('Xoá section này?')) return;
        update({ sections: config.sections.filter(s => s.id !== id) });
    };

    const toggleSectionVisibility = (id) => {
        update({
            sections: config.sections.map(s =>
                s.id === id ? { ...s, visible: !s.visible } : s
            )
        });
    };

    // Payment actions
    const copyPaymentLink = async (index) => {
        const link = config.payments?.[index]?.callback || '';
        if (!navigator.clipboard) return;

        try {
            await navigator.clipboard.writeText(link);
            alert('Đã sao chép URL');
        } catch {
            alert('Không thể sao chép');
        }
    };

    // Prepare payload for save
    const collectPayload = () => {
        return {
            name: config.name,
            slogan: config.slogan,
            logoUrl: config.logoUrl,
            primaryColor: config.primaryColor,
            secondaryColor: config.secondaryColor,
            font: config.font,
            contact: config.contact,
            smtp: config.smtp,
            media: config.media,
            social: config.social,
            sections: config.sections,
            payments: config.payments,
        };
    };

    // Save settings
    // 📝 WebsiteConfig.jsx - Sửa onSave()

    const onSave = async () => {
        setSaving(true);
        try {
            const payload = collectPayload();
            console.log("💾 Saving settings:", payload);
            console.log("💾 Has logo file:", !!logoFileRef.current);

            const result = await settingsApi.saveSettings(payload, logoFileRef.current);

            console.log("✅ Save result:", result);
            alert('Lưu thành công!');

            // ✅ Update logoUrl if returned
            if (result?.logoUrl) {
                update({ logoUrl: result.logoUrl });
                setLogoPreviewUrl(result.logoUrl);
            }

            logoFileRef.current = null;

            // ✅ Reload để sync với backend
            await loadData();

        } catch (err) {
            console.error("❌ Save error full:", err);
            console.error("❌ Error response:", err.response?.data);
            console.error("❌ Error status:", err.response?.status);

            // ✅ Better error message
            const errorMsg = err.response?.data?.message
                || err.response?.data?.error
                || err.message
                || 'Lỗi không xác định';

            alert(`Lưu thất bại: ${errorMsg}`);
        } finally {
            setSaving(false);
        }
    };

    // Export config
    const onExport = () => {
        const payload = collectPayload();
        const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `site-config-${new Date().toISOString().split('T')[0]}.json`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    };

    // Test SMTP
    const onSendTestEmail = async () => {
        try {
            console.log("📧 Testing SMTP...");
            const result = await settingsApi.testSmtp(config.smtp);
            console.log("✅ SMTP test result:", result);

            if (result?.success || result?.ok) {
                alert('Email thử đã được gửi thành công!');
            } else {
                alert('Gửi email thất bại: ' + (result?.message || 'Unknown error'));
            }
        } catch (err) {
            console.error("❌ SMTP test error:", err);
            alert('Lỗi gửi email thử: ' + err.message);
        }
    };

    // Loading state
    if (loading) {
        return (
            <div className="card" style={{ padding: '40px', textAlign: 'center' }}>
                <div style={{
                    width: '40px',
                    height: '40px',
                    border: '4px solid #f3f3f3',
                    borderTop: '4px solid #3498db',
                    borderRadius: '50%',
                    animation: 'spin 1s linear infinite',
                    margin: '0 auto 12px'
                }} />
                <div>Đang tải cấu hình...</div>
            </div>
        );
    }

    // Error state
    if (error) {
        return (
            <div className="card" style={{ padding: '40px', textAlign: 'center' }}>
                <div style={{ color: '#dc3545', marginBottom: '12px' }}>❌ Lỗi: {error}</div>
                <button className="btn" onClick={loadData}>
                    Thử lại
                </button>
            </div>
        );
    }

    // Main render
    return (
        <main className="main">
            {/* Thông tin chung */}
            <details open className="card">
                <summary>Thông tin chung</summary>
                <div className="content">
                    <div className="field">
                        <label htmlFor="sitename">Tên website</label>
                        <div className="control">
                            <div className="input">
                                <input
                                    id="sitename"
                                    value={config.name || ''}
                                    onChange={e => update({ name: e.target.value })}
                                    placeholder="Tên website..."
                                />
                            </div>
                            <div className="small">Hiển thị ở tiêu đề, email và SEO.</div>
                        </div>
                    </div>

                    <div className="field">
                        <label>Logo</label>
                        <div className="control">
                            <div className="file">
                                <input
                                    id="logo-file"
                                    type="file"
                                    accept="image/*"
                                    onChange={onLogoChange}
                                />
                            </div>
                            <div className="small">Khuyến nghị PNG/SVG nền trong suốt, chiều cao ~48px.</div>
                            <div style={{ marginTop: '8px' }}>
                                {logoPreviewUrl ? (
                                    <img
                                        src={logoPreviewUrl}
                                        alt="logo preview"
                                        style={{
                                            height: '40px',
                                            borderRadius: '6px',
                                            boxShadow: '0 2px 6px rgba(0,0,0,0.08)'
                                        }}
                                    />
                                ) : (
                                    <span className="small">Chưa có logo</span>
                                )}
                            </div>
                        </div>
                    </div>

                    <div className="field">
                        <label htmlFor="slogan">Slogan</label>
                        <div className="control">
                            <div className="textarea">
                                <textarea
                                    id="slogan"
                                    value={config.slogan || ''}
                                    onChange={e => update({ slogan: e.target.value })}
                                    placeholder="Thông điệp ngắn gọn..."
                                    rows={3}
                                />
                            </div>
                            <div className="small">Dùng cho hero/banner & thẻ meta description.</div>
                        </div>
                    </div>
                </div>
            </details>

            {/* Màu sắc & Giao diện */}
            <details open className="card">
                <summary>Màu sắc & Giao diện</summary>
                <div className="content">
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px' }}>
                        <div className="field" style={{ display: 'block' }}>
                            <label>Màu chủ đạo</label>
                            <div className="control">
                                <div className="color">
                                    <input
                                        type="color"
                                        value={config.primaryColor}
                                        onChange={e => update({ primaryColor: e.target.value })}
                                    />
                                    <input
                                        type="text"
                                        value={config.primaryColor}
                                        onChange={e => update({ primaryColor: e.target.value })}
                                    />
                                </div>
                                <div className="small">Dùng cho CTA, link, badge chính.</div>
                            </div>
                        </div>

                        <div className="field" style={{ display: 'block' }}>
                            <label>Màu thứ cấp</label>
                            <div className="control">
                                <div className="color">
                                    <input
                                        type="color"
                                        value={config.secondaryColor}
                                        onChange={e => update({ secondaryColor: e.target.value })}
                                    />
                                    <input
                                        type="text"
                                        value={config.secondaryColor}
                                        onChange={e => update({ secondaryColor: e.target.value })}
                                    />
                                </div>
                                <div className="small">Dùng cho tiêu đề, icon đậm.</div>
                            </div>
                        </div>

                        <div className="field" style={{ display: 'block' }}>
                            <label>Font chữ</label>
                            <div className="control">
                                <div className="select">
                                    <select
                                        value={config.font}
                                        onChange={e => update({ font: e.target.value })}
                                    >
                                        <option>Inter (khuyên dùng)</option>
                                        <option>Roboto</option>
                                        <option>Nunito</option>
                                        <option>Open Sans</option>
                                    </select>
                                </div>
                                <div className="small">Áp dụng toàn site; hỗ trợ font Việt hoá.</div>
                            </div>
                        </div>
                    </div>

                    <div className="theme-demo" style={{ '--primary': config.primaryColor }}>
                        <div className="h">Xem trước chủ đề</div>
                        <div className="p">Tiêu đề và nút sử dụng màu chủ đạo để kiểm tra độ tương phản.</div>
                        <button className="cta" style={{ background: config.primaryColor }}>
                            Nút hành động
                        </button>
                    </div>
                </div>
            </details>

            {/* Layout */}
            <details className="card">
                <summary>Layout</summary>
                <div className="content">
                    <div className="small" style={{ marginBottom: '12px' }}>
                        Sắp xếp thứ tự section trên trang chủ. Trạng thái "Ẩn/Hiện" chỉ ảnh hưởng frontend.
                    </div>
                    <div className="table">
                        <table>
                            <thead>
                                <tr>
                                    <th>Section ID</th>
                                    <th>Tên section</th>
                                    <th>Thứ tự</th>
                                    <th>Trạng thái</th>
                                    <th>Tùy chọn</th>
                                </tr>
                            </thead>
                            <tbody>
                                {config.sections && config.sections.length > 0 ? (
                                    config.sections.map((s) => (
                                        <tr key={s.id}>
                                            <td>{s.id}</td>
                                            <td>{s.title}</td>
                                            <td>
                                                <input
                                                    type="number"
                                                    value={s.order}
                                                    min="1"
                                                    onChange={e => {
                                                        const v = parseInt(e.target.value || 1, 10);
                                                        update({
                                                            sections: config.sections.map(x =>
                                                                x.id === s.id ? { ...x, order: v } : x
                                                            )
                                                        });
                                                    }}
                                                    style={{ width: '70px', padding: '6px', borderRadius: '8px' }}
                                                />
                                            </td>
                                            <td>
                                                <span className={`status ${s.visible ? 'on' : 'off'}`}>
                                                    {s.visible ? 'Hiện' : 'Ẩn'}
                                                </span>
                                            </td>
                                            <td className="row-actions">
                                                <button
                                                    className="icon-btn"
                                                    onClick={() => toggleSectionVisibility(s.id)}
                                                    title={s.visible ? 'Ẩn' : 'Hiện'}
                                                >
                                                    👁️
                                                </button>
                                                <button
                                                    className="icon-btn"
                                                    onClick={() => deleteSection(s.id)}
                                                    title="Xoá"
                                                >
                                                    🗑️
                                                </button>
                                            </td>
                                        </tr>
                                    ))
                                ) : (
                                    <tr>
                                        <td colSpan="5" style={{ padding: '12px', textAlign: 'center' }}>
                                            Chưa có section
                                        </td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                    <div style={{ marginTop: '10px' }}>
                        <button className="btn" onClick={addSection}>
                            + Thêm Section
                        </button>
                    </div>
                </div>
            </details>

            {/* Thông tin liên hệ */}
            <details className="card">
                <summary>Thông tin liên hệ</summary>
                <div className="content">
                    <div className="field">
                        <label>Địa chỉ công ty</label>
                        <div className="control">
                            <div className="input">
                                <input
                                    type="text"
                                    value={config.contact.address || ''}
                                    onChange={e => updateNested('contact.address', e.target.value)}
                                    placeholder="Số nhà, đường, quận/huyện, tỉnh/thành"
                                />
                            </div>
                        </div>
                    </div>

                    <div className="field">
                        <label>Số điện thoại</label>
                        <div className="control">
                            <div className="input">
                                <input
                                    type="tel"
                                    value={config.contact.phone || ''}
                                    onChange={e => updateNested('contact.phone', e.target.value)}
                                    placeholder="+84 9xx xxx xxx"
                                />
                            </div>
                        </div>
                    </div>

                    <div className="field">
                        <label>Email</label>
                        <div className="control">
                            <div className="input">
                                <input
                                    type="email"
                                    value={config.contact.email || ''}
                                    onChange={e => updateNested('contact.email', e.target.value)}
                                    placeholder="support@example.com"
                                />
                            </div>
                        </div>
                    </div>
                </div>
            </details>

            {/* SMTP */}
            <details className="card">
                <summary>Cấu hình SMTP</summary>
                <div className="content">
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '16px' }}>
                        <div className="field" style={{ display: 'block' }}>
                            <label>SMTP Server</label>
                            <div className="control">
                                <div className="input">
                                    <input
                                        type="text"
                                        value={config.smtp.server || ''}
                                        onChange={e => updateNested('smtp.server', e.target.value)}
                                        placeholder="smtp.gmail.com"
                                    />
                                </div>
                            </div>
                        </div>

                        <div className="field" style={{ display: 'block' }}>
                            <label>Port</label>
                            <div className="control">
                                <div className="number">
                                    <input
                                        type="number"
                                        value={config.smtp.port || ''}
                                        onChange={e => updateNested('smtp.port', parseInt(e.target.value) || 587)}
                                        placeholder="587"
                                    />
                                </div>
                            </div>
                        </div>

                        <div className="field" style={{ display: 'block' }}>
                            <label>Username</label>
                            <div className="control">
                                <div className="input">
                                    <input
                                        type="text"
                                        value={config.smtp.user || ''}
                                        onChange={e => updateNested('smtp.user', e.target.value)}
                                        placeholder="your-email@gmail.com"
                                    />
                                </div>
                            </div>
                        </div>

                        <div className="field" style={{ display: 'block' }}>
                            <label>Password</label>
                            <div className="control">
                                <div className="input">
                                    <input
                                        type="password"
                                        value={config.smtp.password || ''}
                                        onChange={e => updateNested('smtp.password', e.target.value)}
                                        placeholder="••••••••"
                                    />
                                </div>
                                <div className="small">Khuyên dùng ENV cho production.</div>
                            </div>
                        </div>
                    </div>

                    <div className="checkbox-row" style={{ marginTop: '12px' }}>
                        <label>
                            <input
                                type="checkbox"
                                checked={!!config.smtp.tls}
                                onChange={e => updateNested('smtp.tls', e.target.checked)}
                            />
                            {' '}Sử dụng TLS
                        </label>
                        <label>
                            <input
                                type="checkbox"
                                checked={!!config.smtp.dkim}
                                onChange={e => updateNested('smtp.dkim', e.target.checked)}
                            />
                            {' '}Bật DKIM/DMARC
                        </label>
                    </div>

                    <div style={{ marginTop: '16px' }}>
                        <button className="btn" onClick={onSendTestEmail}>
                            📧 Gửi email thử
                        </button>
                    </div>
                </div>
            </details>

            {/* Media */}
            <details className="card">
                <summary>Cấu hình hình ảnh</summary>
                <div className="content">
                    <div className="field">
                        <label>Giới hạn upload (MB)</label>
                        <div className="control">
                            <div className="number">
                                <input
                                    type="number"
                                    min="1"
                                    value={config.media.uploadLimitMB || 10}
                                    onChange={e => updateNested('media.uploadLimitMB', parseInt(e.target.value) || 10)}
                                />
                            </div>
                        </div>
                    </div>

                    <div className="field">
                        <label>Định dạng cho phép</label>
                        <div className="control checkbox-row">
                            {['jpg', 'png', 'webp', 'svg'].map(fmt => (
                                <label key={fmt}>
                                    <input
                                        type="checkbox"
                                        checked={config.media.formats?.includes(fmt)}
                                        onChange={e => {
                                            const formats = config.media.formats || [];
                                            if (e.target.checked) {
                                                updateNested('media.formats', [...formats, fmt]);
                                            } else {
                                                updateNested('media.formats', formats.filter(f => f !== fmt));
                                            }
                                        }}
                                    />
                                    {' '}{fmt}
                                </label>
                            ))}
                        </div>
                    </div>
                    <div className="small">Khuyên dùng WebP cho ảnh sản phẩm; cân nhắc CDN nếu lưu lượng lớn.</div>
                </div>
            </details>

            {/* Social Media */}
            <details className="card">
                <summary>Mạng xã hội</summary>
                <div className="content">
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '16px' }}>
                        <div className="field" style={{ display: 'block' }}>
                            <label>Facebook</label>
                            <div className="control">
                                <div className="input">
                                    <input
                                        type="url"
                                        value={config.social.facebook || ''}
                                        onChange={e => updateNested('social.facebook', e.target.value)}
                                        placeholder="https://facebook.com/..."
                                    />
                                </div>
                            </div>
                        </div>

                        <div className="field" style={{ display: 'block' }}>
                            <label>Instagram</label>
                            <div className="control">
                                <div className="input">
                                    <input
                                        type="url"
                                        value={config.social.instagram || ''}
                                        onChange={e => updateNested('social.instagram', e.target.value)}
                                        placeholder="https://instagram.com/..."
                                    />
                                </div>
                            </div>
                        </div>

                        <div className="field" style={{ display: 'block' }}>
                            <label>Zalo</label>
                            <div className="control">
                                <div className="input">
                                    <input
                                        type="url"
                                        value={config.social.zalo || ''}
                                        onChange={e => updateNested('social.zalo', e.target.value)}
                                        placeholder="https://zalo.me/..."
                                    />
                                </div>
                            </div>
                        </div>

                        <div className="field" style={{ display: 'block' }}>
                            <label>TikTok</label>
                            <div className="control">
                                <div className="input">
                                    <input
                                        type="url"
                                        value={config.social.tiktok || ''}
                                        onChange={e => updateNested('social.tiktok', e.target.value)}
                                        placeholder="https://tiktok.com/@..."
                                    />
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </details>

 // 📝 WebsiteConfig.jsx - Line ~748

            {/* Payment Gateways */}
            <details className="card">
                <summary>Cấu hình cổng thanh toán</summary>
                <div className="content">
                    <div className="table">
                        <table>
                            <thead>
                                <tr>
                                    <th>Tên cổng</th>
                                    <th>Link/Callback</th>
                                    <th>Trạng thái</th>
                                    <th>Thao tác</th>
                                </tr>
                            </thead>
                            <tbody>
                                {config.payments && config.payments.length > 0 ? (
                                    config.payments.map((p, idx) => (
                                        // ✅ FIX: Dùng index thay vì p.name để tránh duplicate keys
                                        <tr key={`payment-${idx}`}>
                                            <td>{p.name}</td>
                                            <td style={{
                                                maxWidth: '300px',
                                                overflow: 'hidden',
                                                textOverflow: 'ellipsis',
                                                whiteSpace: 'nowrap'
                                            }}>
                                                {p.callback}
                                            </td>
                                            <td>
                                                <span className={`status ${p.enabled ? 'on' : 'off'}`}>
                                                    {p.enabled ? 'Bật' : 'Tắt'}
                                                </span>
                                            </td>
                                            <td className="row-actions">
                                                <button
                                                    className="icon-btn"
                                                    onClick={() => copyPaymentLink(idx)}
                                                    title="Copy link"
                                                >
                                                    📄
                                                </button>
                                            </td>
                                        </tr>
                                    ))
                                ) : (
                                    <tr>
                                        <td colSpan="4" style={{ padding: '12px', textAlign: 'center' }}>
                                            Chưa có cổng thanh toán
                                        </td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                </div>
            </details>

            {/* Save Bar */}
            <div className="savebar">
                <button className="btn ghost" onClick={() => window.location.reload()}>
                    Hoàn tác
                </button>
                <button className="btn" onClick={onExport}>
                    Xuất cấu hình
                </button>
                <button
                    className="btn primary"
                    onClick={onSave}
                    disabled={saving}
                >
                    {saving ? 'Đang lưu...' : '💾 Lưu thay đổi'}
                </button>
            </div>
        </main>
    );
};

export default WebsiteConfig;