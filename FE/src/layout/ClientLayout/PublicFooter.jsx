// 📝 src/layout/ClientLayout/PublicFooter.jsx
import { Link } from "react-router-dom";
import { useSettings } from "../../contexts/SettingContext";

export default function PublicFooter() {
  const currentYear = new Date().getFullYear();
  const { settings, loading } = useSettings();

  // ✅ dự án bạn lưu JWT ở key: access_token
  const isLoggedIn = !!localStorage.getItem("access_token");

  const footer = settings?.footer;

  const aboutPath = "/tai-lieu/ve-chung-toi";

  const helpCenterPath = footer?.support?.helpCenterPath || "/tickets/create";

  const cartPath = footer?.account?.cartPath || "/cart";
  const orderHistoryPath = footer?.account?.ordersPath || "/profile";

  const registerPath = footer?.account?.registerPath || "/register";
  const loginPath = footer?.account?.loginPath || "/login";

  const USER_GUIDE_URL =
    "https://drive.google.com/file/d/1g5p5UI9luWWv-yn0VvWmq580WkBhv9JV/view";

  return (
    <footer className="footer">
      <div className="container section">
        <div className="grid">
          <div>
            <h5>{loading ? "Keytietkiem" : settings.name}</h5>

            {/* Giới thiệu -> About us */}
            <Link to={aboutPath} onClick={() => window.scrollTo(0, 0)}>Giới thiệu</Link>
            <Link to="/tai-lieu/dieu-khoan-dich-vu" onClick={() => window.scrollTo(0, 0)}>Điều khoản & dịch vụ</Link>
            <Link to="/tai-lieu/chinh-sach-bao-mat" onClick={() => window.scrollTo(0, 0)}>Chính sách bảo mật</Link>
          </div>

          <div>
            <h5>Hỗ trợ</h5>

            <a href={USER_GUIDE_URL} target="_blank" rel="noopener noreferrer">
              Hướng dẫn kích hoạt
            </a>
            <Link to={helpCenterPath} onClick={() => window.scrollTo(0, 0)}>Trung tâm trợ giúp</Link>

            {settings.contact.email && (
              <a style={{ pointerEvents: "none", color: "inherit" }} href={`mailto:${settings.contact.email}`}>✉️ {settings.contact.email}</a>
            )}
            {settings.contact.phone && (
              <a style={{ pointerEvents: "none", color: "inherit" }} href={`tel:${settings.contact.phone.replace(/\s/g, "")}`}>
                📞 {settings.contact.phone}
              </a>
            )}
          </div>

          <div>
            <h5>Tài khoản</h5>

            {isLoggedIn ? (
              <>
                <Link to={cartPath}>Giỏ hàng</Link>
                <Link to={orderHistoryPath}>Đơn hàng</Link>
              </>
            ) : (
              <>
                <Link to={registerPath}>Đăng ký</Link>
                <Link to={loginPath}>Đăng nhập</Link>
              </>
            )}
          </div>

          <div>
            <h5>Kết nối</h5>
            {settings.social.facebook ? (
              <a href={settings.social.facebook} target="_blank" rel="noopener noreferrer">
                Facebook
              </a>
            ) : (
              <a href="#facebook">Facebook</a>
            )}

            {settings.social.instagram ? (
              <a href={settings.social.instagram} target="_blank" rel="noopener noreferrer">
                Instagram
              </a>
            ) : (
              <a href="#instagram">Instagram</a>
            )}

            {settings.social.zalo ? (
              <a href={settings.social.zalo} target="_blank" rel="noopener noreferrer">
                Zalo OA
              </a>
            ) : (
              <a href="#zalo">Zalo OA</a>
            )}
          </div>

          <div className="legal">
            <div>
              © {currentYear} {loading ? "Keytietkiem" : settings.name}. Các nhãn hiệu thuộc chủ sở hữu tương ứng.
            </div>
            {settings?.contact?.address && (
              <div style={{ marginTop: 8, fontSize: 14 }}>📍 {settings.contact.address}</div>
            )}
          </div>
        </div>
      </div>
    </footer>
  );
}
