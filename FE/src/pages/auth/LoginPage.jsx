import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useModal } from "../../components/common/ModalProvider";
import { AuthService } from "../../services/authService";
import "./Auth.css";

export default function LoginPage() {
  const navigate = useNavigate();
  const modal = useModal();

  const [formData, setFormData] = useState({
    username: "",
    password: "",
    rememberMe: false,
  });

  const [errorMessage, setErrorMessage] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleInputChange = (fieldName, value) => {
    setFormData((previousFormData) => ({
      ...previousFormData,
      [fieldName]: value,
    }));
  };

  const validateForm = () => {
    if (!formData.username.trim()) {
      return "Vui lòng nhập tên đăng nhập";
    }
    if (!formData.password) {
      return "Vui lòng nhập mật khẩu";
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

      const response = await AuthService.login(
        formData.username,
        formData.password
      );

      // Store tokens
      localStorage.setItem("access_token", response.accessToken);
      localStorage.setItem("refresh_token", response.refreshToken);
      localStorage.setItem("user", JSON.stringify(response.user));

      if (typeof window !== "undefined") {
        window.dispatchEvent(new Event("profile-updated"));
      }

      // Store username if remember me is checked
      if (formData.rememberMe) {
        localStorage.setItem("remembered_username", formData.username);
      } else {
        localStorage.removeItem("remembered_username");
      }

      // Show success modal
      await modal.showSuccess(
        `Chào mừng trở lại, ${response.user.fullName}!`,
        "Đăng nhập thành công"
      );

      // Redirect based on user role
      // Backend returns Role.Code (e.g., "ADMIN", "STORAGE_STAFF", "CONTENT_CREATOR")
      // Also check for legacy role names for backward compatibility
      const userRoles = response.user.roles || [];
      const firstRole = userRoles[0]?.toUpperCase() || "";

      if (
        firstRole === "ADMIN"
      ) {
        navigate("/admin/home");
      } else if (
        firstRole === "CUSTOMER_CARE"
      ) {
        navigate("/staff/tickets");
      } else if (
        firstRole === "CONTENT_CREATOR" ||
        firstRole === "CONTENT CREATOR"
      ) {
        navigate("/post-dashboard");
      } else if (
        firstRole === "STORAGE_STAFF" ||
        firstRole === "STORAGE STAFF"
      ) {
        navigate("/key-monitor");
      } else {
        // Default: Customer or other roles -> homepage
        navigate("/");
      }
    } catch (error) {
      const responseData = error?.response?.data;
      const apiErrorMessage =
        (typeof responseData === "string"
          ? responseData
          : responseData?.message) ||
        error?.message ||
        "Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin.";
      setErrorMessage(apiErrorMessage);
    } finally {
      setIsSubmitting(false);
    }
  };

  // Load remembered username on component mount
  React.useEffect(() => {
    const rememberedUsername = localStorage.getItem("remembered_username");
    if (rememberedUsername) {
      setFormData((prev) => ({
        ...prev,
        username: rememberedUsername,
        rememberMe: true,
      }));
    }
  }, []);

  return (
    <div className="public-page">
      <section className="container section auth-wrap">
        <div className="auth-card" role="form" aria-labelledby="loginTitle">
          <h1 id="loginTitle">Đăng nhập</h1>
          <p className="helper" style={{ textAlign: "center" }}>
            Truy cập đơn hàng, bảo hành & điểm thưởng của bạn.
          </p>

          <form onSubmit={handleSubmit}>
            <div className="form-row">
              <label htmlFor="username">Tên đăng nhập</label>
              <input
                className="input"
                id="username"
                type="text"
                placeholder="Tên đăng nhập"
                value={formData.username}
                onChange={(e) => handleInputChange("username", e.target.value)}
                required
                autoFocus
              />
            </div>

            <div className="form-row">
              <div className="row-inline">
                <label htmlFor="password">Mật khẩu</label>
                <a
                  className="helper"
                  href="/forgot-password"
                  onClick={(e) => {
                    e.preventDefault();
                    navigate("/forgot-password");
                  }}
                >
                  Quên mật khẩu?
                </a>
              </div>
              <input
                className="input"
                id="password"
                type="password"
                placeholder="••••••••"
                value={formData.password}
                onChange={(e) => handleInputChange("password", e.target.value)}
                required
              />
            </div>

            <label className="checkbox" style={{ marginTop: 8 }}>
              <input
                type="checkbox"
                checked={formData.rememberMe}
                onChange={(e) =>
                  handleInputChange("rememberMe", e.target.checked)
                }
              />
              Ghi nhớ tài khoản
            </label>

            <div className="form-row" style={{ marginTop: 12 }}>
              <button
                className="btn primary"
                type="submit"
                style={{ width: "100%" }}
                disabled={isSubmitting}
              >
                {isSubmitting ? "Đang đăng nhập..." : "Đăng nhập"}
              </button>
            </div>

            {errorMessage && (
              <div
                aria-live="polite"
                className="helper"
                id="login-errors"
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

            <div className="form-row">
              <hr />
            </div>

            {/* <div className="form-row">
              <button
                className="btn"
                type="button"
                style={{ width: "100%" }}
                onClick={() => {
                  modal.showInfo(
                    "Tính năng đăng nhập bằng Google sẽ sớm được ra mắt!",
                    "Thông báo"
                  );
                }}
              >
                Đăng nhập bằng Google
              </button>
            </div> */}

            <p
              className="helper"
              style={{ textAlign: "center", marginTop: 10 }}
            >
              Chưa có tài khoản?{" "}
              <a
                href="/register"
                onClick={(e) => {
                  e.preventDefault();
                  navigate("/register");
                }}
              >
                Đăng ký
              </a>
            </p>
          </form>
        </div>
      </section>
    </div>
  );
}
