// 📝 src/layout/ClientLayout/PublicFooter.jsx

import { useSettings } from "../../contexts/SettingContext"; // ✅ Import

export default function PublicFooter() {
  const currentYear = new Date().getFullYear();
  const { settings, loading } = useSettings(); // ✅ Use hook

  return (
    <footer className="footer">
      <div className="container section">
        <div className="grid">
          <div>
            <h5>{loading ? 'Keytietkiem' : settings.name}</h5>
            <a href="#about">Giới thiệu</a>
            <a href="#warranty">Chính sách bảo hành</a>
            <a href="#refund">Hoàn tiền</a>
          </div>

          <div>
            <h5>Hỗ trợ</h5>
            <a href="#activation-guide">Hướng dẫn kích hoạt</a>
            <a href="#help-center">Trung tâm trợ giúp</a>
            {settings.contact.email && (
              <a href={`mailto:${settings.contact.email}`}>✉️ {settings.contact.email}</a>
            )}
            {settings.contact.phone && (
              <a href={`tel:${settings.contact.phone.replace(/\s/g, '')}`}>📞 {settings.contact.phone}</a>
            )}
          </div>

          <div>
            <h5>Tài khoản</h5>
            <a href="#orders">Đơn hàng</a>
            <a href="#rewards">Điểm thưởng</a>
            <a href="#warranty-check">Bảo hành</a>
          </div>

          <div>
            <h5>Kết nối</h5>
            {settings.social.facebook ? (
              <a href={settings.social.facebook} target="_blank" rel="noopener noreferrer">📘 Facebook</a>
            ) : <a href="#facebook">Facebook</a>}
            {settings.social.instagram ? (
              <a href={settings.social.instagram} target="_blank" rel="noopener noreferrer">📷 Instagram</a>
            ) : <a href="#instagram">Instagram</a>}
            {settings.social.zalo ? (
              <a href={settings.social.zalo} target="_blank" rel="noopener noreferrer">💬 Zalo OA</a>
            ) : <a href="#zalo">Zalo OA</a>}
            {settings.social.tiktok && (
              <a href={settings.social.tiktok} target="_blank" rel="noopener noreferrer">🎵 TikTok</a>
            )}
          </div>

          <div className="legal">
            <div>© {currentYear} {loading ? 'Keytietkiem' : settings.name}. Các nhãn hiệu thuộc chủ sở hữu tương ứng.</div>
            {settings.contact.address && (
              <div style={{ marginTop: '8px', fontSize: '14px' }}>📍 {settings.contact.address}</div>
            )}
          </div>
        </div>
      </div>
    </footer>
  );
}