import React, { useState } from "react";
import "../styles/auth.css";
import { post } from "../api";

export default function Register() {
  const [email, setEmail] = useState("");
  const [pw, setPw] = useState("");
  const [display, setDisplay] = useState("");
  const [error, setError] = useState("");

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    try {
      const res = await post("/auth/register", {
        email,
        password: pw,
        displayName: display,
      });
      localStorage.setItem("token", res.token);
      alert("Đăng ký thành công");
      window.location = "/";
    } catch (err) {
      setError(err.data?.message || "Lỗi đăng ký");
    }
  };

  return (
    <div className="auth-wrap container">
      <form className="auth-card" onSubmit={handleSubmit}>
        <h1>Đăng ký</h1>
        <p className="helper" style={{ textAlign: "center" }}>
          Tạo tài khoản để quản lý đơn hàng & điểm thưởng.
        </p>

        <div className="form-row">
          <label>Email</label>
          <input
            className="input"
            type="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </div>

        <div className="form-row">
          <label>Họ & tên (tuỳ chọn)</label>
          <input
            className="input"
            type="text"
            value={display}
            onChange={(e) => setDisplay(e.target.value)}
          />
        </div>

        <div className="form-row">
          <label>Mật khẩu</label>
          <input
            className="input"
            type="password"
            required
            value={pw}
            onChange={(e) => setPw(e.target.value)}
          />
        </div>

        <div className="form-row" style={{ marginTop: 12 }}>
          <button
            className="btn primary"
            style={{ width: "100%" }}
            type="submit"
          >
            Đăng ký
          </button>
        </div>

        <div
          aria-live="polite"
          className="helper"
          style={{ textAlign: "center", marginTop: 10 }}
        >
          {error}
        </div>
      </form>
    </div>
  );
}
