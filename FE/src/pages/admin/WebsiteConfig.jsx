import React, { useEffect, useState, useRef } from 'react';
import '../../styles/WebsiteConfig.css';
import settingsService from '../../services/settings';

// Reuse any project Toast or modal if available; here we use simple alert for demo.
const WebsiteConfig = () => {
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [config, setConfig] = useState({
        name: '',
        slogan: '',
        primaryColor: '#2563EB',
        secondaryColor: '#111827',
        font: 'Inter (khuyên dùng)',
        sections: [],
        contact: { address: '', phone: '', email: '' },
        smtp: { server: '', port: 587, user: '', tls: false, dkim: false },
        media: { uploadLimitMB: 10, formats: ['jpg', 'png', 'webp'] },
        social: { facebook: '', instagram: '', zalo: '', tiktok: '' },
        payments: []
    });

    const logoFileRef = useRef(null);
    const [logoPreviewUrl, setLogoPreviewUrl] = useState(null);

    useEffect(() => {
        let mounted = true;
        async function load() {
            try {
                const data = await settingsService.getSettings();
                if (!mounted) return;
                if (data) {
                    // merge safely
                    setConfig(prev => ({ ...prev, ...data }));
                    if (data.logoUrl) setLogoPreviewUrl(data.logoUrl);
                }
            } catch (err) {
                console.error('Load settings error', err);
                // optionally show toast
            } finally {
                if (mounted) setLoading(false);
            }
        }
        load();
        return () => { mounted = false; };
    }, []);

    // Helpers to update nested config
    const update = (patch) => setConfig(prev => ({ ...prev, ...patch }));
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

    // Logo file change
    const onLogoChange = (e) => {
        const f = e.target.files?.[0];
        if (!f) return;
        const url = URL.createObjectURL(f);
        setLogoPreviewUrl(url);
        // store file object for upload on save
        logoFileRef.current = f;
    };

    // Sections actions
    const toggleSectionVisibility = (id) => {
        updateNested(`sections.${config.sections.findIndex(s => s.id === id)}.visible`,
            !config.sections.find(s => s.id === id)?.visible);
    };
    const deleteSection = (id) => {
        if (!window.confirm('Xoá section này?')) return;
        update({ sections: config.sections.filter(s => s.id !== id) });
    };
    const addSection = () => {
        const id = 'custom.' + Date.now();
        update({ sections: [...config.sections, { id, title: 'New Section', order: config.sections.length + 1, visible: true }] });
        // scroll into view: after DOM render, not implemented here (ok).
    };

    // Payments actions: for demo we keep simple
    const copyPaymentLink = async (index) => {
        const link = config.payments?.[index]?.callback || '';
        if (navigator.clipboard) {
            try { await navigator.clipboard.writeText(link); alert('Đã sao chép URL'); }
            catch { alert('Không thể sao chép'); }
        }
    };

    const collectPayload = () => {
        // prepare payload for backend (strip any preview-only fields)
        const payload = { ...config };
        // If logo file present, backend should accept multipart/form-data. We'll handle in service.
        return payload;
    };

    const onSave = async () => {
        setSaving(true);
        try {
            const payload = collectPayload();
            await settingsService.saveSettings(payload, logoFileRef.current);
            alert('Lưu thành công');
        } catch (err) {
            console.error(err);
            alert('Lưu thất bại');
        } finally { setSaving(false); }
    };

    const onExport = () => {
        const a = document.createElement('a');
        const blob = new Blob([JSON.stringify(collectPayload(), null, 2)], { type: 'application/json' });
        a.href = URL.createObjectURL(blob);
        a.download = 'site-config.json';
        document.body.appendChild(a);
        a.click();
        a.remove();
    };

    const onSendTestEmail = async () => {
        try {
            const resp = await settingsService.testSmtp(config.smtp);
            if (resp?.ok) alert('Yêu cầu gửi email thử đã được gửi.');
            else alert('Gửi email thử thất bại.');
        } catch (err) { console.error(err); alert('Lỗi gửi email thử'); }
    };

    if (loading) return <div className="card" style={{ padding: 20 }}>Đang tải...</div>;

    return (
        <main className="main" id="site-config-main">
            {/* Thông tin chung */}
            <details open className="card">
                <summary>Thông tin chung</summary>
                <div className="content">
                    <div className="field">
                        <label htmlFor="sitename">Tên website</label>
                        <div className="control">
                            <div className="input"><input id="sitename" value={config.name || ''} onChange={e => update({ name: e.target.value })} placeholder="Tên website..." /></div>
                            <div className="small">Hiển thị ở tiêu đề, email và SEO.</div>
                        </div>
                    </div>

                    <div className="field">
                        <label>Logo</label>
                        <div className="control">
                            <div className="file"><input id="logo-file" type="file" accept="image/*" onChange={onLogoChange} /></div>
                            <div className="small">Khuyến nghị PNG/SVG nền trong suốt, chiều cao ~48px.</div>
                            <div style={{ marginTop: 8 }}>
                                {logoPreviewUrl ? <img src={logoPreviewUrl} alt="logo" style={{ height: 40, borderRadius: 6, boxShadow: '0 2px 6px rgba(0,0,0,0.08)' }} /> : <span className="small">Chưa có logo</span>}
                            </div>
                        </div>
                    </div>

                    <div className="field">
                        <label htmlFor="slogan">Slogan</label>
                        <div className="control">
                            <div className="textarea"><textarea id="slogan" value={config.slogan || ''} onChange={e => update({ slogan: e.target.value })} placeholder="Thông điệp ngắn gọn..."></textarea></div>
                            <div className="small">Tin dùng cho hero/banner & thẻ meta description.</div>
                        </div>
                    </div>
                </div>
            </details>

            {/* Màu sắc & Giao diện */}
            <details open className="card">
                <summary>Màu sắc & Giao diện</summary>
                <div className="content">
                    <div className="grid-3">
                        <div className="field">
                            <label>Màu chủ đạo</label>
                            <div className="control">
                                <div className="color">
                                    <input type="color" value={config.primaryColor} onChange={e => update({ primaryColor: e.target.value })} />
                                    <input type="text" value={config.primaryColor} onChange={e => update({ primaryColor: e.target.value })} />
                                </div>
                                <div className="small">Dùng cho CTA, link, badge chính.</div>
                            </div>
                        </div>
                        <div className="field">
                            <label>Màu thứ cấp</label>
                            <div className="control">
                                <div className="color">
                                    <input type="color" value={config.secondaryColor} onChange={e => update({ secondaryColor: e.target.value })} />
                                    <input type="text" value={config.secondaryColor} onChange={e => update({ secondaryColor: e.target.value })} />
                                </div>
                                <div className="small">Dùng cho tiêu đề, icon đậm.</div>
                            </div>
                        </div>
                        <div className="field">
                            <label>Font chữ</label>
                            <div className="control">
                                <div className="select">
                                    <select value={config.font} onChange={e => update({ font: e.target.value })}>
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
                        <button className="cta" style={{ background: config.primaryColor }}>Nút hành động</button>
                    </div>
                </div>
            </details>

            {/* Layout */}
            <details className="card">
                <summary>Layout</summary>
                <div className="content">
                    <div className="small" style={{ margin: '8px 0 12px' }}>Sắp xếp thứ tự section trên trang chủ. Trạng thái “Ẩn/Hiện” chỉ ảnh hưởng frontend.</div>
                    <div className="table">
                        <table id="sections-table">
                            <thead><tr><th>SectionID</th><th>Tên section</th><th>Thứ tự</th><th>Trạng thái</th><th>Tùy chọn</th></tr></thead>
                            <tbody>
                                {config.sections && config.sections.length ? config.sections.map((s, i) => (
                                    <tr key={s.id} data-id={s.id}>
                                        <td>{s.id}</td>
                                        <td>{s.title}</td>
                                        <td><input className="number" type="number" value={s.order} min="1" onChange={e => {
                                            const v = parseInt(e.target.value || 0, 10);
                                            update({ sections: config.sections.map(x => x.id === s.id ? { ...x, order: v } : x) });
                                        }} style={{ width: 70, padding: 6, borderRadius: 8 }} /></td>
                                        <td><span className={`status ${s.visible ? 'on' : 'off'}`}>{s.visible ? 'Hiện' : 'Ẩn'}</span></td>
                                        <td className="row-actions">
                                            <button className="icon-btn" onClick={() => alert('Edit modal - implement if needed')}>✏️</button>
                                            <button className="icon-btn" onClick={() => update({ sections: config.sections.map(x => x.id === s.id ? { ...x, visible: !x.visible } : x) })}>👁️</button>
                                            <button className="icon-btn" onClick={() => deleteSection(s.id)}>🗑️</button>
                                        </td>
                                    </tr>
                                )) : (
                                    <tr><td colSpan="5" style={{ padding: 12 }}>Chưa có section</td></tr>
                                )}
                            </tbody>
                        </table>
                    </div>
                    <div style={{ marginTop: 10 }}><button className="btn" onClick={addSection}>+ Thêm Section</button></div>
                </div>
            </details>

            {/* Contact */}
            <details className="card">
                <summary>Thông tin liên hệ</summary>
                <div className="content">
                    <div className="field"><label>Địa chỉ công ty</label><div className="control"><div className="input"><input type="text" value={config.contact.address || ''} onChange={e => updateNested('contact.address', e.target.value)} placeholder="Số nhà, đường, quận/huyện, tỉnh/thành" /></div></div></div>
                    <div className="field"><label>Số điện thoại</label><div className="control"><div className="input"><input type="tel" value={config.contact.phone || ''} onChange={e => updateNested('contact.phone', e.target.value)} placeholder="+84 9xx xxx xxx" /></div></div></div>
                    <div className="field"><label>Email</label><div className="control"><div className="input"><input type="email" value={config.contact.email || ''} onChange={e => updateNested('contact.email', e.target.value)} placeholder="support@example.com" /></div></div></div>
                </div>
            </details>

            {/* SMTP */}
            <details className="card">
                <summary>Cấu hình Server (SMTP)</summary>
                <div className="content">
                    <div className="grid-2">
                        <div className="field"><label>SMTP Server</label><div className="control"><div className="input"><input type="text" value={config.smtp.server || ''} onChange={e => updateNested('smtp.server', e.target.value)} placeholder="smtp.example.com" /></div></div></div>
                        <div className="field"><label>Port</label><div className="control"><div className="number"><input type="number" value={config.smtp.port || ''} onChange={e => updateNested('smtp.port', e.target.value)} placeholder="587" /></div></div></div>
                        <div className="field"><label>SMTP Username</label><div className="control"><div className="input"><input type="text" value={config.smtp.user || ''} onChange={e => updateNested('smtp.user', e.target.value)} placeholder="no-reply@example.com" /></div></div></div>
                        <div className="field"><label>SMTP Password</label><div className="control"><div className="input"><input type="password" value={config.smtp.password || ''} onChange={e => updateNested('smtp.password', e.target.value)} placeholder="••••••••" /></div><div className="small">Khuyên dùng ENV cho production.</div></div></div>
                    </div>

                    <div className="checkbox-row" style={{ marginTop: 8 }}>
                        <label><input type="checkbox" checked={!!config.smtp.tls} onChange={e => updateNested('smtp.tls', e.target.checked)} /> Sử dụng TLS</label>
                        <label><input type="checkbox" checked={!!config.smtp.dkim} onChange={e => updateNested('smtp.dkim', e.target.checked)} /> Bật DKIM/DMARC (đã cấu hình DNS)</label>
                    </div>

                    <div style={{ marginTop: 10, display: 'flex', gap: 8 }}>
                        <button className="btn" onClick={onSendTestEmail}>Gửi email thử</button>
                        <button className="btn ghost" onClick={() => {
                            // download example .env
                            const content = `# Example .env\nSMTP_HOST=${config.smtp.server || ''}\nSMTP_PORT=${config.smtp.port || ''}\nSMTP_USER=${config.smtp.user || ''}\n`;
                            const blob = new Blob([content], { type: 'text/plain' });
                            const url = URL.createObjectURL(blob);
                            const a = document.createElement('a'); a.href = url; a.download = '.env.example'; document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);
                        }}>Tải cấu hình .env.example</button>
                    </div>
                </div>
            </details>

            {/* Media */}
            <details className="card">
                <summary>Cấu hình hình ảnh</summary>
                <div className="content">
                    <div className="field"><label>Giới hạn upload (MB)</label><div className="control"><div className="number"><input type="number" min="1" value={config.media.uploadLimitMB || 10} onChange={e => updateNested('media.uploadLimitMB', parseInt(e.target.value || 1, 10))} /></div></div></div>
                    <div className="field">
                        <label>Định dạng cho phép</label>
                        <div className="control checkbox-row">
                            {['jpg', 'png', 'webp', 'svg'].map(fmt => (
                                <label key={fmt}><input type="checkbox" checked={config.media.formats?.includes(fmt)} onChange={e => {
                                    const set = new Set(config.media.formats || []);
                                    if (e.target.checked) set.add(fmt); else set.delete(fmt);
                                    updateNested('media.formats', Array.from(set));
                                }} /> {fmt}</label>
                            ))}
                        </div>
                    </div>
                    <div className="small">Khuyên dùng WebP cho ảnh sản phẩm; cân nhắc CDN nếu lưu lượng lớn.</div>
                </div>
            </details>

            {/* Social */}
            <details className="card">
                <summary>Cấu hình mạng xã hội</summary>
                <div className="content">
                    <div className="grid-2">
                        <div className="field"><label>Facebook</label><div className="control"><div className="input"><input type="url" value={config.social.facebook || ''} onChange={e => updateNested('social.facebook', e.target.value)} placeholder="https://facebook.com/..." /></div></div></div>
                        <div className="field"><label>Instagram</label><div className="control"><div className="input"><input type="url" value={config.social.instagram || ''} onChange={e => updateNested('social.instagram', e.target.value)} placeholder="https://instagram.com/..." /></div></div></div>
                        <div className="field"><label>Zalo</label><div className="control"><div className="input"><input type="url" value={config.social.zalo || ''} onChange={e => updateNested('social.zalo', e.target.value)} placeholder="https://zalo.me/..." /></div></div></div>
                        <div className="field"><label>TikTok</label><div className="control"><div className="input"><input type="url" value={config.social.tiktok || ''} onChange={e => updateNested('social.tiktok', e.target.value)} placeholder="https://tiktok.com/@..." /></div></div></div>
                    </div>
                </div>
            </details>

            {/* Payment gateways */}
            <details className="card">
                <summary>Cấu hình cổng thanh toán</summary>
                <div className="content">
                    <div className="table">
                        <table id="payments-table">
                            <thead><tr><th>Tên cổng</th><th>Link/Callback</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
                            <tbody>
                                {config.payments && config.payments.length ? config.payments.map((p, idx) => (
                                    <tr key={p.name || idx}>
                                        <td>{p.name}</td>
                                        <td>{p.callback}</td>
                                        <td><span className={`status ${p.enabled ? 'on' : 'off'}`}>{p.enabled ? 'Bật' : 'Tắt'}</span></td>
                                        <td className="row-actions">
                                            <button className="icon-btn" onClick={() => copyPaymentLink(idx)}>📄</button>
                                            <button className="icon-btn" onClick={() => alert('Chỉnh sửa cổng - modal')}>✏️</button>
                                            <button className="icon-btn" onClick={() => alert('Bật/Tắt cổng - implement')}>⚙️</button>
                                        </td>
                                    </tr>
                                )) : <tr><td colSpan="4" style={{ padding: 12 }}>Chưa có cổng thanh toán</td></tr>}
                            </tbody>
                        </table>
                    </div>
                    <div style={{ marginTop: 10 }}><button className="btn" onClick={() => alert('Form thêm cổng')}>+ Thêm cổng thanh toán</button></div>
                </div>
            </details>

            {/* Save bar */}
            <div className="savebar" role="toolbar" aria-label="Lưu cấu hình" style={{ marginTop: 12 }}>
                <button className="btn ghost" onClick={() => window.location.reload()}>Hoàn tác</button>
                <button className="btn" onClick={onExport}>Xuất cấu hình</button>
                <button className="btn primary" onClick={onSave} disabled={saving}>{saving ? 'Đang lưu...' : '💾 Lưu thay đổi'}</button>
            </div>
        </main>
    );
};

export default WebsiteConfig;