import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import PublicFooter from "../../components/public/PublicFooter";
import PublicHeader from "../../components/public/PublicHeader";
import { AuthService } from "../../services/authService";
import "./Auth.css";

export default function ForgotPasswordPage() {
  const navigate = useNavigate();

  const [email, setEmail] = useState("");
  const [errorMessage, setErrorMessage] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const validateEmail = (emailValue) => {
    if (!emailValue.trim()) {
      return "Vui lòng nhập email";
    }
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(emailValue)) {
      return "Email không hợp lệ";
    }
    return null;
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setErrorMessage("");

    const validationError = validateEmail(email);
    if (validationError) {
      setErrorMessage(validationError);
      return;
    }

    try {
      setIsSubmitting(true);

      await AuthService.forgotPassword(email);

      // Navigate to check email page with email as state
      navigate("/check-reset-email", { state: { email } });
    } catch (error) {
      const responseData = error?.response?.data;
      const apiErrorMessage =
        (typeof responseData === "string"
          ? responseData
          : responseData?.message) ||
        error?.message ||
        "Có lỗi xảy ra. Vui lòng thử lại.";
      setErrorMessage(apiErrorMessage);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="public-page">
      <PublicHeader />

      <section className="container section auth-wrap">
        <div className="auth-card" role="form" aria-labelledby="forgotPasswordTitle">
          <h1 id="forgotPasswordTitle">Quên mật khẩu?</h1>
          <p className="helper" style={{ textAlign: "center" }}>
            Nhập email đã đăng ký để nhận liên kết đặt lại mật khẩu.
          </p>

          <form onSubmit={handleSubmit}>
            <div className="form-row">
              <label htmlFor="email">Email</label>
              <input
                className="input"
                id="email"
                type="email"
                placeholder="email@address.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                autoFocus
              />
            </div>

            <div className="form-row" style={{ marginTop: 12 }}>
              <button
                className="btn primary"
                type="submit"
                style={{ width: "100%" }}
                disabled={isSubmitting}
              >
                {isSubmitting ? "Đang gửi..." : "Đặt lại mật khẩu"}
              </button>
            </div>

            {errorMessage && (
              <div
                aria-live="polite"
                className="helper"
                id="forgot-password-errors"
                style={{
                  color: "var(--error, #dc2626)",
                  marginTop: 10,
                  padding: "8px",
                  backgroundColor: "var(--error-bg, #fef2f2)",
                  borderRadius: "8px",
                  textAlign: "center",
                }}
              >
                {errorMessage}
              </div>
            )}

            <p
              className="helper"
              style={{ textAlign: "center", marginTop: 12 }}
            >
              <a
                href="/login"
                onClick={(e) => {
                  e.preventDefault();
                  navigate("/login");
                }}
              >
                Trở lại trang đăng nhập
              </a>
            </p>
          </form>
        </div>
      </section>

      <PublicFooter />
    </div>
  );
}
