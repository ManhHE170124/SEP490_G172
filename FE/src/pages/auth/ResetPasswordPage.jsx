import React, { useState, useEffect } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useModal } from "../../components/common/ModalProvider";
import PublicFooter from "../../components/public/PublicFooter";
import PublicHeader from "../../components/public/PublicHeader";
import { AuthService } from "../../services/authService";
import "./Auth.css";

export default function ResetPasswordPage() {
  const navigate = useNavigate();
  const modal = useModal();
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");

  const [formData, setFormData] = useState({
    newPassword: "",
    confirmPassword: "",
  });

  const [errorMessage, setErrorMessage] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  useEffect(() => {
    // If no token in URL, redirect to forgot password page
    if (!token) {
      navigate("/forgot-password");
    }
  }, [token, navigate]);

  const handleInputChange = (fieldName, value) => {
    setFormData((previousFormData) => ({
      ...previousFormData,
      [fieldName]: value,
    }));
  };

  const validateForm = () => {
    if (!formData.newPassword) {
      return "Vui lòng nhập mật khẩu mới";
    }
    if (formData.newPassword.length < 6) {
      return "Mật khẩu phải có ít nhất 6 ký tự";
    }
    if (formData.newPassword.length > 100) {
      return "Mật khẩu không được vượt quá 100 ký tự";
    }
    if (!formData.confirmPassword) {
      return "Vui lòng xác nhận mật khẩu";
    }
    if (formData.newPassword !== formData.confirmPassword) {
      return "Mật khẩu xác nhận không khớp";
    }
    return null;
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setErrorMessage("");

    const validationError = validateForm();
    if (validationError) {
      setErrorMessage(validationError);
      return;
    }

    try {
      setIsSubmitting(true);

      await AuthService.resetPassword(token, formData.newPassword);

      // Show success modal and redirect to login
      await modal.showSuccess(
        "Mật khẩu của bạn đã được đặt lại thành công!",
        "Đặt lại mật khẩu thành công"
      );

      navigate("/login");
    } catch (error) {
      const responseData = error?.response?.data;
      const apiErrorMessage =
        (typeof responseData === "string"
          ? responseData
          : responseData?.message) ||
        error?.message ||
        "Không thể đặt lại mật khẩu. Token có thể đã hết hạn.";
      setErrorMessage(apiErrorMessage);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="public-page">
      <PublicHeader />

      <section className="container section auth-wrap">
        <div className="auth-card" role="form" aria-labelledby="resetPasswordTitle">
          <h1 id="resetPasswordTitle">Đặt lại mật khẩu</h1>
          <p className="helper" style={{ textAlign: "center" }}>
            Nhập mật khẩu mới cho tài khoản của bạn.
          </p>

          <form onSubmit={handleSubmit}>
            <div className="form-row">
              <label htmlFor="newPassword">Mật khẩu mới</label>
              <div style={{ position: "relative" }}>
                <input
                  className="input"
                  id="newPassword"
                  type={showPassword ? "text" : "password"}
                  placeholder="••••••••"
                  value={formData.newPassword}
                  onChange={(e) => handleInputChange("newPassword", e.target.value)}
                  required
                  autoFocus
                  style={{ paddingRight: "45px" }}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  style={{
                    position: "absolute",
                    right: "12px",
                    top: "50%",
                    transform: "translateY(-50%)",
                    background: "none",
                    border: "none",
                    cursor: "pointer",
                    fontSize: "12px",
                    color: "var(--muted)",
                    padding: "4px 8px",
                  }}
                >
                  {showPassword ? "Ẩn" : "Hiện"}
                </button>
              </div>
              <small className="helper">Mật khẩu phải từ 6-100 ký tự</small>
            </div>

            <div className="form-row">
              <label htmlFor="confirmPassword">Xác nhận mật khẩu</label>
              <div style={{ position: "relative" }}>
                <input
                  className="input"
                  id="confirmPassword"
                  type={showConfirmPassword ? "text" : "password"}
                  placeholder="••••••••"
                  value={formData.confirmPassword}
                  onChange={(e) =>
                    handleInputChange("confirmPassword", e.target.value)
                  }
                  required
                  style={{ paddingRight: "45px" }}
                />
                <button
                  type="button"
                  onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                  style={{
                    position: "absolute",
                    right: "12px",
                    top: "50%",
                    transform: "translateY(-50%)",
                    background: "none",
                    border: "none",
                    cursor: "pointer",
                    fontSize: "12px",
                    color: "var(--muted)",
                    padding: "4px 8px",
                  }}
                >
                  {showConfirmPassword ? "Ẩn" : "Hiện"}
                </button>
              </div>
            </div>

            <div className="form-row" style={{ marginTop: 12 }}>
              <button
                className="btn primary"
                type="submit"
                style={{ width: "100%" }}
                disabled={isSubmitting}
              >
                {isSubmitting ? "Đang đặt lại..." : "Đặt lại mật khẩu"}
              </button>
            </div>

            {errorMessage && (
              <div
                aria-live="polite"
                className="helper"
                id="reset-password-errors"
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
