import { useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useModal } from "../../components/common/ModalProvider";
import PublicFooter from "../../components/public/PublicFooter";
import PublicHeader from "../../components/public/PublicHeader";
import { AuthService } from "../../services/authService";
import "./Auth.css";

export default function SignUpPage() {
  const navigate = useNavigate();
  const modal = useModal();

  const [currentStep, setCurrentStep] = useState("email");

  // Form data state
  const [formData, setFormData] = useState({
    email: "",
    otp: ["", "", "", "", "", ""],
    verificationToken: "",
    username: "",
    password: "",
    confirmPassword: "",
    firstName: "",
    lastName: "",
    phone: "",
    address: "",
    agreedToTerms: false,
  });

  // UI state
  const [errorMessage, setErrorMessage] = useState("");
  const [successMessage, setSuccessMessage] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [otpResendCooldown, setOtpResendCooldown] = useState(0);

  // OTP input refs for auto-focus
  const otpInputRefs = useRef([]);

  const handleInputChange = (fieldName, value) => {
    setFormData((previousFormData) => ({
      ...previousFormData,
      [fieldName]: value,
    }));
  };

  const handleOtpChange = (index, value) => {
    // Only allow numeric input
    if (value && !/^\d$/.test(value)) return;

    const newOtp = [...formData.otp];
    newOtp[index] = value;
    setFormData((prev) => ({ ...prev, otp: newOtp }));

    // Auto-focus next input
    if (value && index < 5) {
      otpInputRefs.current[index + 1]?.focus();
    }
  };

  const handleOtpKeyDown = (index, event) => {
    // Handle backspace to go to previous input
    if (event.key === "Backspace" && !formData.otp[index] && index > 0) {
      otpInputRefs.current[index - 1]?.focus();
    }
  };

  const handleOtpPaste = (event) => {
    event.preventDefault();
    const pastedData = event.clipboardData.getData("text").trim();

    // Check if pasted data is 6 digits
    if (/^\d{6}$/.test(pastedData)) {
      const newOtp = pastedData.split("");
      setFormData((prev) => ({ ...prev, otp: newOtp }));
      // Focus last input
      otpInputRefs.current[5]?.focus();
    }
  };

  // Step 1: Send OTP to email
  const handleSendOtp = async (event) => {
    event.preventDefault();
    setErrorMessage("");
    setSuccessMessage("");

    if (!formData.email.trim()) {
      setErrorMessage("Vui lòng nhập email");
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
      setErrorMessage("Email không hợp lệ");
      return;
    }

    try {
      setIsSubmitting(true);
      const response = await AuthService.sendOtp(formData.email);
      setSuccessMessage(response);
      setCurrentStep("otp");

      // Start resend cooldown (60 seconds)
      setOtpResendCooldown(60);
      const interval = setInterval(() => {
        setOtpResendCooldown((prev) => {
          if (prev <= 1) {
            clearInterval(interval);
            return 0;
          }
          return prev - 1;
        });
      }, 1000);
    } catch (error) {
      console.error("Send OTP Error:", error);
      const responseData = error?.response?.data;
      const apiErrorMessage =
        (typeof responseData === 'string' ? responseData : responseData?.message) ||
        error?.message ||
        "Không thể gửi OTP. Vui lòng thử lại.";
      setErrorMessage(apiErrorMessage);
    } finally {
      setIsSubmitting(false);
    }
  };

  // Step 2: Verify OTP
  const handleVerifyOtp = async (event) => {
    event.preventDefault();
    setErrorMessage("");
    setSuccessMessage("");

    const otpCode = formData.otp.join("");

    if (otpCode.length !== 6) {
      setErrorMessage("Vui lòng nhập đầy đủ 6 chữ số");
      return;
    }

    try {
      setIsSubmitting(true);
      const response = await AuthService.verifyOtp(formData.email, otpCode);

      console.log("OTP Verification Response:", response);

      const isSuccess =
        response.isVerified === true ||
        (response.verificationToken && response.isVerified !== false);

      if (isSuccess) {
        // Store verification token if available
        const token =
          response.verificationToken ||
          response.token ||
          response.verification_token;

        if (token) {
          setFormData((prev) => ({
            ...prev,
            verificationToken: token,
          }));
        }

        setSuccessMessage(
          response.message ||
            "Xác thực OTP thành công! Vui lòng hoàn tất đăng ký."
        );
        setCurrentStep("register");
      } else {
        // Only show error if explicitly failed
        setErrorMessage(response.message || "Mã OTP không chính xác");
      }
    } catch (error) {
      console.error("OTP Verification Error:", error);
      const responseData = error?.response?.data;
      const apiErrorMessage =
        (typeof responseData === 'string' ? responseData : responseData?.message) ||
        error?.message ||
        "Xác thực OTP thất bại. Vui lòng thử lại.";
      setErrorMessage(apiErrorMessage);
    } finally {
      setIsSubmitting(false);
    }
  };

  // Resend OTP
  const handleResendOtp = async () => {
    if (otpResendCooldown > 0) return;

    try {
      setIsSubmitting(true);
      setErrorMessage("");
      const response = await AuthService.sendOtp(formData.email);
      setSuccessMessage(response);

      // Reset OTP inputs
      setFormData((prev) => ({ ...prev, otp: ["", "", "", "", "", ""] }));

      // Start cooldown again
      setOtpResendCooldown(60);
      const interval = setInterval(() => {
        setOtpResendCooldown((prev) => {
          if (prev <= 1) {
            clearInterval(interval);
            return 0;
          }
          return prev - 1;
        });
      }, 1000);
    } catch (error) {
      const responseData = error?.response?.data;
      const apiErrorMessage =
        (typeof responseData === 'string' ? responseData : responseData?.message) ||
        error?.message ||
        "Không thể gửi lại OTP. Vui lòng thử lại.";
      setErrorMessage(apiErrorMessage);
    } finally {
      setIsSubmitting(false);
    }
  };

  // Step 3: Complete registration
  const validateRegistrationForm = () => {
    if (!formData.username.trim()) {
      return "Vui lòng nhập tên đăng nhập";
    }
    if (formData.username.length < 3 || formData.username.length > 60) {
      return "Tên đăng nhập phải từ 3-60 ký tự";
    }
    // Username validation: only allow alphanumeric and underscore, no whitespace or special characters
    if (!/^[a-zA-Z0-9_]+$/.test(formData.username)) {
      return "Tên đăng nhập chỉ được chứa chữ cái, số và dấu gạch dưới (_)";
    }
    if (!formData.firstName.trim()) {
      return "Vui lòng nhập tên";
    }
    if (!formData.lastName.trim()) {
      return "Vui lòng nhập họ";
    }
    if (!formData.password) {
      return "Vui lòng nhập mật khẩu";
    }
    if (formData.password.length < 6) {
      return "Mật khẩu phải có ít nhất 6 ký tự";
    }
    if (formData.password !== formData.confirmPassword) {
      return "Mật khẩu không khớp";
    }
    if (!formData.agreedToTerms) {
      return "Vui lòng đồng ý với điều khoản dịch vụ";
    }
    return null;
  };

  const handleRegister = async (event) => {
    event.preventDefault();
    setErrorMessage("");

    const validationError = validateRegistrationForm();
    if (validationError) {
      setErrorMessage(validationError);
      return;
    }

    try {
      setIsSubmitting(true);

      const payload = {
        username: formData.username,
        password: formData.password,
        email: formData.email,
        firstName: formData.firstName,
        lastName: formData.lastName,
        phone: formData.phone || undefined,
        address: formData.address || undefined,
        verificationToken: formData.verificationToken,
      };

      const response = await AuthService.register(payload);

      // Store tokens
      localStorage.setItem("access_token", response.accessToken);
      localStorage.setItem("refresh_token", response.refreshToken);
      localStorage.setItem("user", JSON.stringify(response.user));

      // Show success modal and redirect
      await modal.showSuccess(
        `Chào mừng ${response.user.fullName} đến với Keytietkiem! Tài khoản của bạn đã được tạo thành công.`,
        "Đăng ký thành công"
      );
      navigate("/");
    } catch (error) {
      const responseData = error?.response?.data;
      const apiErrorMessage =
        (typeof responseData === 'string' ? responseData : responseData?.message) ||
        error?.message ||
        "Đăng ký thất bại. Vui lòng thử lại.";
      setErrorMessage(apiErrorMessage);
    } finally {
      setIsSubmitting(false);
    }
  };

  // Render Step 1: Email Input
  const renderEmailStep = () => (
    <form onSubmit={handleSendOtp}>
      <div className="form-row">
        <label htmlFor="email">Email</label>
        <input
          className="input"
          id="email"
          type="email"
          placeholder="email@address.com"
          value={formData.email}
          onChange={(e) => handleInputChange("email", e.target.value)}
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
          {isSubmitting ? "Đang gửi..." : "Nhận mã OTP"}
        </button>
      </div>

      <p className="helper" style={{ textAlign: "center", marginTop: 10 }}>
        Đã có tài khoản?{" "}
        <a
          href="/login"
          onClick={(e) => {
            e.preventDefault();
            navigate("/login");
          }}
        >
          Đăng nhập
        </a>
      </p>
    </form>
  );

  // Render Step 2: OTP Verification
  const renderOtpStep = () => (
    <form onSubmit={handleVerifyOtp}>
      <p className="helper" style={{ marginBottom: 12 }}>
        Nhập mã 6 chữ số được gửi tới <strong>{formData.email}</strong>
      </p>

      <div
        className="otp"
        style={{
          display: "flex",
          gap: "8px",
          margin: "12px 0",
        }}
        aria-label="Nhập mã OTP"
      >
        {formData.otp.map((digit, index) => (
          <input
            key={index}
            ref={(el) => (otpInputRefs.current[index] = el)}
            type="text"
            inputMode="numeric"
            maxLength="1"
            value={digit}
            onChange={(e) => handleOtpChange(index, e.target.value)}
            onKeyDown={(e) => handleOtpKeyDown(index, e)}
            onPaste={index === 0 ? handleOtpPaste : undefined}
            style={{
              width: "46px",
              height: "48px",
              border: "1px solid var(--line, #e5e7eb)",
              borderRadius: "12px",
              textAlign: "center",
              fontSize: "20px",
            }}
            autoFocus={index === 0}
          />
        ))}
      </div>

      <div className="form-row" style={{ marginTop: 12 }}>
        <button
          className="btn primary"
          type="submit"
          style={{ width: "100%" }}
          disabled={isSubmitting}
        >
          {isSubmitting ? "Đang xác thực..." : "Xác nhận"}
        </button>
      </div>

      <p className="helper" style={{ marginTop: 10, textAlign: "center" }}>
        Không nhận được mã?{" "}
        {otpResendCooldown > 0 ? (
          <span style={{ color: "var(--muted, #6b7280)" }}>
            Gửi lại sau {otpResendCooldown}s
          </span>
        ) : (
          <button
            type="button"
            onClick={handleResendOtp}
            style={{
              background: "none",
              border: "none",
              color: "var(--primary, #3b82f6)",
              textDecoration: "underline",
              cursor: "pointer",
              padding: 0,
              font: "inherit",
            }}
          >
            Gửi lại mã
          </button>
        )}
      </p>

      <p className="helper" style={{ textAlign: "center", marginTop: 8 }}>
        <button
          type="button"
          onClick={() => {
            setCurrentStep("email");
            setFormData((prev) => ({ ...prev, otp: ["", "", "", "", "", ""] }));
            setErrorMessage("");
            setSuccessMessage("");
          }}
          style={{
            background: "none",
            border: "none",
            color: "var(--primary, #3b82f6)",
            textDecoration: "underline",
            cursor: "pointer",
            padding: 0,
            font: "inherit",
          }}
        >
          Thay đổi email
        </button>
      </p>
    </form>
  );

  // Render Step 3: Registration Form
  const renderRegisterStep = () => (
    <form onSubmit={handleRegister}>
      <div className="form-row">
        <label htmlFor="username">Tên đăng nhập *</label>
        <input
          className="input"
          id="username"
          type="text"
          placeholder="vd: user_name123"
          value={formData.username}
          onChange={(e) => handleInputChange("username", e.target.value)}
          required
          autoFocus
        />
        <small className="helper" style={{ marginTop: 4 }}>
          Chỉ chữ cái, số và dấu gạch dưới (_), không khoảng trắng
        </small>
      </div>

      <div
        style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}
      >
        <div className="form-row">
          <label htmlFor="firstName">Tên *</label>
          <input
            className="input"
            id="firstName"
            type="text"
            placeholder="Tên"
            value={formData.firstName}
            onChange={(e) => handleInputChange("firstName", e.target.value)}
            required
          />
        </div>

        <div className="form-row">
          <label htmlFor="lastName">Họ *</label>
          <input
            className="input"
            id="lastName"
            type="text"
            placeholder="Họ"
            value={formData.lastName}
            onChange={(e) => handleInputChange("lastName", e.target.value)}
            required
          />
        </div>
      </div>

      <div className="form-row">
        <label htmlFor="phone">Số điện thoại</label>
        <input
          className="input"
          id="phone"
          type="tel"
          placeholder="0123456789"
          value={formData.phone}
          onChange={(e) => handleInputChange("phone", e.target.value)}
        />
      </div>

      <div className="form-row">
        <label htmlFor="address">Địa chỉ</label>
        <input
          className="input"
          id="address"
          type="text"
          placeholder="Địa chỉ của bạn"
          value={formData.address}
          onChange={(e) => handleInputChange("address", e.target.value)}
        />
      </div>

      <div className="form-row">
        <label htmlFor="password">Mật khẩu *</label>
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

      <div className="form-row">
        <label htmlFor="confirmPassword">Nhập lại mật khẩu *</label>
        <input
          className="input"
          id="confirmPassword"
          type="password"
          placeholder="••••••••"
          value={formData.confirmPassword}
          onChange={(e) => handleInputChange("confirmPassword", e.target.value)}
          required
        />
      </div>

      <div className="row-inline" style={{ marginTop: 10 }}>
        <input
          id="agreedToTerms"
          type="checkbox"
          checked={formData.agreedToTerms}
          onChange={(e) => handleInputChange("agreedToTerms", e.target.checked)}
          required
        />
        <label className="checkbox" htmlFor="agreedToTerms">
          Tôi đồng ý với{" "}
          <a href="/terms" onClick={(e) => e.preventDefault()}>
            Điều khoản dịch vụ
          </a>{" "}
          &{" "}
          <a href="/privacy" onClick={(e) => e.preventDefault()}>
            Chính sách bảo mật
          </a>
          .
        </label>
      </div>

      <div className="form-row" style={{ marginTop: 12 }}>
        <button
          className="btn primary"
          type="submit"
          style={{ width: "100%" }}
          disabled={isSubmitting}
        >
          {isSubmitting ? "Đang xử lý..." : "Tạo tài khoản"}
        </button>
      </div>

      <p className="helper" style={{ textAlign: "center", marginTop: 10 }}>
        Đã có tài khoản?{" "}
        <a
          href="/login"
          onClick={(e) => {
            e.preventDefault();
            navigate("/login");
          }}
        >
          Đăng nhập
        </a>
      </p>
    </form>
  );

  const getStepTitle = () => {
    switch (currentStep) {
      case "email":
        return "Đăng ký";
      case "otp":
        return "Xác minh OTP";
      case "register":
        return "Hoàn tất đăng ký";
      default:
        return "Đăng ký";
    }
  };

  const getStepDescription = () => {
    switch (currentStep) {
      case "email":
        return "Theo dõi đơn hàng, lưu bảo hành & nhận ưu đãi dành riêng cho bạn.";
      case "otp":
        return "Bước 2/3 - Xác thực email của bạn";
      case "register":
        return "Bước 3/3 - Hoàn tất thông tin tài khoản";
      default:
        return "";
    }
  };

  return (
    <div className="public-page">
      <PublicHeader />

      <section className="container section auth-wrap">
        <div className="auth-card" role="form" aria-labelledby="signUpTitle">
          <h1 id="signUpTitle">{getStepTitle()}</h1>
          <p className="helper" style={{ textAlign: "center" }}>
            {getStepDescription()}
          </p>

          {currentStep === "email" && renderEmailStep()}
          {currentStep === "otp" && renderOtpStep()}
          {currentStep === "register" && renderRegisterStep()}

          {successMessage && (
            <div
              aria-live="polite"
              className="helper"
              style={{
                color: "var(--success, #16a34a)",
                marginTop: 10,
                padding: "8px",
                backgroundColor: "var(--success-bg, #f0fdf4)",
                borderRadius: "8px",
                textAlign: "center",
              }}
            >
              {successMessage}
            </div>
          )}

          {errorMessage && (
            <div
              aria-live="polite"
              className="helper"
              id="errors"
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
        </div>
      </section>

      <PublicFooter />
    </div>
  );
}
