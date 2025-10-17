import React, { useState } from "react";
import "../styles/auth.css";
import { authApi } from "../api";   

export default function Register() {
  const [email, setEmail] = useState("");
  const [pw, setPw] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [error, setError] = useState("");

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");

    try {
      const res = await authApi.register({ email, password: pw, displayName });
      localStorage.setItem("token", res.token);
      localStorage.setItem("email", res.email);
      alert("Đăng ký thành công!");
      window.location.href = "/login";
    } catch (err) {
      setError(err.message || "Lỗi khi đăng ký");
    }
  };

  return (
    <div className="auth-wrap container">
      <form className="auth-card" onSubmit={handleSubmit}>
        <h1>Đăng ký tài khoản</h1>
        <p className="helper" style={{ textAlign: "center" }}>
          Tạo tài khoản mới để quản lý đơn hàng, bảo hành và điểm thưởng.
        </p>

        <div className="form-row">
          <label htmlFor="fullname">Họ và tên</label>
          <input
            className="input"
            id="fullname"
            type="text"
            placeholder="Nguyễn Văn A"
            required
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
          />
        </div>

        <div className="form-row">
          <label htmlFor="remail">Email</label>
          <input
            className="input"
            id="remail"
            type="email"
            placeholder="email@address.com"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </div>

        <div className="form-row">
          <label htmlFor="rpw">Mật khẩu</label>
          <input
            className="input"
            id="rpw"
            type="password"
            placeholder="••••••••"
            required
            value={pw}
            onChange={(e) => setPw(e.target.value)}
          />
        </div>

        <div className="form-row" style={{ marginTop: 12 }}>
          <button className="btn primary" style={{ width: "100%" }} type="submit">
            Đăng ký
          </button>
        </div>

        <p className="helper" style={{ textAlign: "center", marginTop: 10 }}>
          Đã có tài khoản? <a href="/login">Đăng nhập</a>
        </p>

        <div aria-live="polite" className="helper" id="register-errors">
          {error}
        </div>
      </form>
    </div>
  );
}
