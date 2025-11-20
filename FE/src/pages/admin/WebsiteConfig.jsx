import React, { useEffect, useState, useRef } from 'react';
import '../../styles/WebsiteConfig.css';
import { settingsApi } from '../../services/settings';
import { useToast } from '../../contexts/ToastContext';
import LayoutSectionsManager from './LayoutSectionsManager';
//import PaymentGatewaysManager from './PaymentGatewaysManager';

const WebsiteConfig = () => {
    // Toast
    const { showToast } = useToast();

    // State management
    const [config, setConfig] = useState({
        name: '',
        slogan: '',
        logoUrl: '',
        primaryColor: '#2563EB',
        secondaryColor: '#111827',
        font: 'Inter (khuyên dùng)',
        contact: { address: '', phone: '', email: '' },
        smtp: { server: '', port: 587, user: '', password: '', tls: false, dkim: false },
        media: { uploadLimitMB: 10, formats: ['jpg', 'png', 'webp'] },
        social: { facebook: '', instagram: '', zalo: '', tiktok: '' },
        payments: []
    });

    const [loading, setLoading] = useState(false);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");

    // Form field errors
    const [formErrors, setFormErrors] = useState({});

    const logoFileRef = useRef(null);
    const [logoPreviewUrl, setLogoPreviewUrl] = useState(null);

    // Load data on mount
    useEffect(() => {
        loadData();
        // Cleanup created object URL on unmount if created from file
        return () => {
            if (logoPreviewUrl && logoPreviewUrl.startsWith('blob:')) {
                URL.revokeObjectURL(logoPreviewUrl);
            }
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const loadData = async () => {
        setLoading(true);
        setError("");
        try {
            const resp = await settingsApi.getSettings();
            const data = resp && resp.data !== undefined ? resp.data : resp;

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
            const msg = err?.response?.data?.message || err.message || "Không thể tải cấu hình";
            setError(msg);
            showToast({ type: 'error', title: 'Lỗi tải cấu hình', message: msg });
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

    // Simple URL validation using native URL parser
    const isValidUrl = (value) => {
        if (!value) return true; // empty allowed — validate required separately if needed
        try {
            const u = new URL(value);
            return u.protocol === "http:" || u.protocol === "https:";
        } catch {
            return false;
        }
    };

    const validateAll = () => {
        const errs = {};
        // Site name required
        if (!config.name || config.name.trim().length < 2) {
            errs.name = "Tên trang là bắt buộc (ít nhất 2 ký tự)";
        }
        // Website optional but if provided must be a valid URL
        if (config.website && !isValidUrl(config.website)) {
            errs.website = "URL không hợp lệ. Ví dụ: https://example.com";
        }
        // Email validation (optional)
        if (config.contact?.email && !/^\S+@\S+\.\S+$/.test(config.contact.email)) {
            errs.contactEmail = "Email không hợp lệ";
        }

        setFormErrors(errs);
        return Object.keys(errs).length === 0;
    };

    // Logo handling
    const onLogoChange = (e) => {
        const f = e.target.files?.[0];
        if (!f) return;

        console.log("📷 Logo selected:", f.name);

        // revoke previous blob if we created it
        if (logoPreviewUrl && logoPreviewUrl.startsWith('blob:')) {
            URL.revokeObjectURL(logoPreviewUrl);
        }

        const url = URL.createObjectURL(f);
        setLogoPreviewUrl(url);
        logoFileRef.current = f;
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
            payments: config.payments,
        };
    };

    // Save settings
    const onSave = async () => {
        // Validate form fields
        if (!validateAll()) {
            const firstErr = Object.values(formErrors)[0] || 'Vui lòng kiểm tra các trường';
            showToast({ type: 'error', title: 'Lỗi nhập liệu', message: firstErr });
            return;
        }

        setSaving(true);
        try {
            const payload = collectPayload();
            console.log("💾 Saving settings:", payload);
            console.log("💾 Has logo file:", !!logoFileRef.current);

            const result = await settingsApi.saveSettings(payload, logoFileRef.current);
            const data = result && result.data !== undefined ? result.data : result;

            console.log("✅ Save result:", data);
            showToast({ type: 'success', title: 'Lưu thành công', message: 'Cấu hình đã được lưu' });

            // Update logoUrl if returned
            if (data?.logoUrl) {
                update({ logoUrl: data.logoUrl });
                setLogoPreviewUrl(data.logoUrl);
            }

            logoFileRef.current = null;

            // Reload to sync with backend
            await loadData();

        } catch (err) {
            console.error("❌ Save error full:", err);
            console.error("❌ Error response:", err.response?.data);
            console.error("❌ Error status:", err.response?.status);

            const errorMsg = err.response?.data?.message
                || err.response?.data?.error
                || err.message
                || 'Lỗi không xác định';

            showToast({ type: 'error', title: 'Lưu thất bại', message: errorMsg });
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
        showToast({ type: 'success', title: 'Xuất thành công', message: 'Đã xuất file cấu hình' });
    };

    // Test SMTP
    const onSendTestEmail = async () => {
        try {
            console.log("📧 Testing SMTP...");
            const resp = await settingsApi.testSmtp(config.smtp);
            const result = resp && resp.data !== undefined ? resp.data : resp;

            console.log("✅ SMTP test result:", result);

            if (result?.success || result?.ok) {
                showToast({ type: 'success', title: 'Gửi thành công', message: 'Email thử đã được gửi thành công' });
            } else {
                showToast({ type: 'error', title: 'Gửi thất bại', message: result?.message || 'Gửi email thất bại' });
            }
        } catch (err) {
            console.error("❌ SMTP test error:", err);
            const msg = err?.response?.data?.message || err.message || 'Lỗi gửi email thử';
            showToast({ type: 'error', title: 'Lỗi SMTP', message: msg });
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
                                    className={formErrors.name ? 'error' : ''}
                                />
                            </div>
                            {formErrors.name && <div className="field-error">{formErrors.name}</div>}
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

            {/* Layout Sections - Component mới */}
            <LayoutSectionsManager />

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
                                    className={formErrors.contactEmail ? 'error' : ''}
                                />
                            </div>
                            {formErrors.contactEmail && <div className="field-error">{formErrors.contactEmail}</div>}
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

            {/* Payment Gateways - Component mới */}
            {/*  <PaymentGatewaysManager /> */}

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