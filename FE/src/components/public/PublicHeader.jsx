import { useState } from "react";
import { useNavigate } from "react-router-dom";

export default function PublicHeader() {
  const navigate = useNavigate();
  const [searchQuery, setSearchQuery] = useState("");

  const handleSearch = (event) => {
    event.preventDefault();
    if (searchQuery.trim()) {
      // TODO: Implement search functionality
      console.log("Searching for:", searchQuery);
    }
  };

  return (
    <div className="topbar">
      <div className="container header">
        <a
          className="logo"
          href="/"
          onClick={(e) => {
            e.preventDefault();
            navigate("/");
          }}
        >
          <div className="mark">K</div>
          Keytietkiem
        </a>

        <form className="searchbar" onSubmit={handleSearch}>
          <input
            placeholder="Tìm: Office 365, Windows 11 Pro, ChatGPT Plus, Adobe..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
          <button className="btn" type="submit">
            Tìm kiếm
          </button>
        </form>

        <div className="account">
          <a
            className="btn"
            href="/cart"
            onClick={(e) => {
              e.preventDefault();
              navigate("/cart");
            }}
          >
            🛒 Giỏ hàng
          </a>
          <a
            className="btn"
            href="/login"
            onClick={(e) => {
              e.preventDefault();
              navigate("/login");
            }}
          >
            Đăng nhập
          </a>
          <a
            className="btn primary"
            href="/register"
            onClick={(e) => {
              e.preventDefault();
              navigate("/register");
            }}
          >
            Đăng ký
          </a>
        </div>

        <nav className="navbar">
          <div className="nav-item">
            <a className="nav-link" href="#products">
              <strong>Danh mục sản phẩm ▾</strong>
            </a>
            <div className="dropdown">
              <a href="#ai">AI</a>
              <a href="#education">Học tập</a>
              <a href="#entertainment">Giải trí / Steam</a>
              <a href="#office">Công việc (Office/Windows)</a>
              <a href="#design">Thiết kế (Adobe…)</a>
              <a href="#dev">Dev & Cloud</a>
            </div>
          </div>

          <div className="nav-item">
            <a className="nav-link" href="#support">
              <strong>Dịch vụ hỗ trợ ▾</strong>
            </a>
            <div className="dropdown">
              <a href="#remote-support">Hỗ trợ cài đặt từ xa</a>
              <a href="#guides">Hướng dẫn sử dụng</a>
              <a href="#fix">Fix lỗi phần mềm đã mua</a>
            </div>
          </div>

          <div className="nav-item">
            <a className="nav-link" href="#blog">
              <strong>Bài viết ▾</strong>
            </a>
            <div className="dropdown">
              <a href="#tips">Mẹo vặt</a>
              <a href="#news">Tin tức</a>
              <a href="#quick-guides">Hướng dẫn nhanh</a>
            </div>
          </div>

          <div className="nav-item">
            <a className="nav-link" href="#tutorials">
              <strong>Hướng dẫn</strong>
            </a>
          </div>
        </nav>
      </div>
    </div>
  );
}
