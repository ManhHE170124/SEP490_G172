import React, { useState } from "react";
import "../styles/auth.css";
import { authApi } from "../api"; 

export default function Login() {
  const [email, setEmail] = useState("");
  const [pw, setPw] = useState("");
  const [error, setError] = useState("");

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    try {
      const res = await authApi.login({ email, password: pw });
      localStorage.setItem("token", res.token);
      localStorage.setItem("email", res.email);
      localStorage.setItem("role", res.role);
      alert("Đăng nhập thành công!");
      window.location.href = "/";
    } catch (err) {
      setError(err.message || "Lỗi đăng nhập");
    }
  };

  return (
    <div className="auth-wrap container">
      <form className="auth-card" onSubmit={handleSubmit}>
        <h1>Đăng nhập</h1>
        <p className="helper" style={{ textAlign: "center" }}>
          Truy cập đơn hàng, bảo hành & điểm thưởng của bạn.
        </p>

        <div className="form-row">
          <label htmlFor="lemail">Email</label>
          <input
            className="input"
            id="lemail"
            type="email"
            placeholder="email@address.com"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </div>

        <div className="form-row">
          <div className="row-inline">
            <label htmlFor="lpw">Mật khẩu</label>
            <a className="helper" href="/forgot">
              Quên mật khẩu?
            </a>
          </div>
          <input
            className="input"
            id="lpw"
            type="password"
            placeholder="••••••••"
            required
            value={pw}
            onChange={(e) => setPw(e.target.value)}
          />
        </div>

        <div className="row-inline" style={{ marginTop: 8 }}>
          <label style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <input type="checkbox" /> Ghi nhớ tài khoản
          </label>
          <span></span>
        </div>

        <div className="form-row" style={{ marginTop: 12 }}>
          <button className="btn primary" style={{ width: "100%" }} type="submit">
            Đăng nhập
          </button>
        </div>

        <p className="helper" style={{ textAlign: "center", marginTop: 10 }}>
          Chưa có tài khoản? <a href="/register">Đăng ký</a>
        </p>

        <div aria-live="polite" className="helper" id="login-errors">
          {error}
        </div>
      </form>
    </div>
  );
}
