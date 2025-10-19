import React, { useState } from "react";
import "../styles/auth.css";
import { post } from "../api/api";

export default function ForgotPassword() {
  const [email, setEmail] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setMessage("");

    try {
      const res = await post("/auth/forgot-password", { email });
      setMessage(res.message || "Liên kết đặt lại mật khẩu đã được gửi tới email của bạn.");
    } catch (err) {
      setError(err.data?.message || "Không thể gửi email đặt lại mật khẩu.");
    }
  };

  return (
    <div className="auth-wrap container">
      <form className="auth-card" onSubmit={handleSubmit}>
        <h1>Quên mật khẩu?</h1>
        <p className="helper">Nhập email đã đăng ký để nhận liên kết đặt lại mật khẩu.</p>

        <div className="form-row">
          <label htmlFor="email">Email</label>
          <input
            id="email"
            type="email"
            className="input"
            placeholder="email@address.com"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </div>

        <div className="form-row" style={{ marginTop: 12 }}>
          <button className="btn primary" style={{ width: "100%" }} type="submit">
            Đặt lại mật khẩu
          </button>
        </div>

        <p className="helper" style={{ textAlign: "center", marginTop: 10 }}>
          <a href="/login">Trở lại trang đăng nhập</a>
        </p>

        {message && (
          <div aria-live="polite" className="helper" style={{ color: "green" }}>
            {message}
          </div>
        )}
        {error && (
          <div aria-live="polite" className="helper" style={{ color: "red" }}>
            {error}
          </div>
        )}
      </form>
    </div>
  );
}
