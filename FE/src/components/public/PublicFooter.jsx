export default function PublicFooter() {
  const currentYear = new Date().getFullYear();

  return (
    <footer className="footer">
      <div className="container section">
        <div className="grid">
          <div>
            <h5>Keytietkiem</h5>
            <a href="#about">Giới thiệu</a>
            <a href="#warranty">Chính sách bảo hành</a>
            <a href="#refund">Hoàn tiền</a>
          </div>

          <div>
            <h5>Hỗ trợ</h5>
            <a href="#activation-guide">Hướng dẫn kích hoạt</a>
            <a href="#help-center">Trung tâm trợ giúp</a>
            <a href="#contact">Liên hệ</a>
          </div>

          <div>
            <h5>Tài khoản</h5>
            <a href="#orders">Đơn hàng</a>
            <a href="#rewards">Điểm thưởng</a>
            <a href="#warranty-check">Bảo hành</a>
          </div>

          <div>
            <h5>Kết nối</h5>
            <a href="#facebook" target="_blank" rel="noopener noreferrer">
              Facebook
            </a>
            <a href="#youtube" target="_blank" rel="noopener noreferrer">
              YouTube
            </a>
            <a href="#zalo" target="_blank" rel="noopener noreferrer">
              Zalo OA
            </a>
          </div>

          <div className="legal">
            © {currentYear} Keytietkiem. Các nhãn hiệu thuộc chủ sở hữu tương
            ứng.
          </div>
        </div>
      </div>
    </footer>
  );
}
