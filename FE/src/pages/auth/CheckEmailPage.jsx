import { useState, useEffect } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { AuthService } from "../../services/authService";
import "./Auth.css";

export default function CheckEmailPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const email = location.state?.email;

  const [canResend, setCanResend] = useState(false);
  const [countdown, setCountdown] = useState(60);
  const [isResending, setIsResending] = useState(false);
  const [message, setMessage] = useState("");

  useEffect(() => {
    // If no email in state, redirect to forgot password page
    if (!email) {
      navigate("/forgot-password");
      return;
    }

    // Start countdown
    const timer = setInterval(() => {
      setCountdown((prevCountdown) => {
        if (prevCountdown <= 1) {
          setCanResend(true);
          clearInterval(timer);
          return 0;
        }
        return prevCountdown - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [email, navigate]);

  const handleResendEmail = async () => {
    setMessage("");
    setIsResending(true);

    try {
      await AuthService.forgotPassword(email);
      setMessage("Email đã được gửi lại thành công!");
      setCanResend(false);
      setCountdown(60);

      // Restart countdown
      const timer = setInterval(() => {
        setCountdown((prevCountdown) => {
          if (prevCountdown <= 1) {
            setCanResend(true);
            clearInterval(timer);
            return 0;
          }
          return prevCountdown - 1;
        });
      }, 1000);
    } catch (error) {
      const responseData = error?.response?.data;
      const apiErrorMessage =
        (typeof responseData === "string"
          ? responseData
          : responseData?.message) ||
        error?.message ||
        "Không thể gửi lại email. Vui lòng thử lại.";
      setMessage(apiErrorMessage);
    } finally {
      setIsResending(false);
    }
  };

  return (
    <div className="public-page">
      <section className="container section auth-wrap">
        <div className="auth-card" aria-labelledby="checkEmailTitle">
          <h1 id="checkEmailTitle" style={{ margin: "0 0 6px" }}>
            Kiểm tra email của bạn
          </h1>
          <p className="helper" style={{ textAlign: "center" }}>
            Chúng tôi đã gửi đường dẫn đặt lại mật khẩu tới email{" "}
            <strong>{email}</strong>. Hãy kiểm tra <b>Inbox</b> hoặc mục{" "}
            <i>Spam/Quảng cáo</i>.
          </p>
          <p className="helper" style={{ textAlign: "center" }}>
            Không thấy email?{" "}
            {canResend ? (
              <a
                href="#forgot-password"
                onClick={(e) => {
                  e.preventDefault();
                  handleResendEmail();
                }}
                style={{ cursor: isResending ? "wait" : "pointer" }}
              >
                {isResending ? "Đang gửi..." : "Gửi lại"}
              </a>
            ) : (
              <span>
                Bạn có thể gửi lại sau <strong>{countdown}</strong> giây
              </span>
            )}
          </p>

          {message && (
            <div
              aria-live="polite"
              className="helper"
              style={{
                color: message.includes("thành công")
                  ? "var(--primary)"
                  : "var(--error)",
                marginTop: 10,
                padding: "8px",
                backgroundColor: message.includes("thành công")
                  ? "var(--primary-bg, #eff6ff)"
                  : "var(--error-bg, #fef2f2)",
                borderRadius: "8px",
                textAlign: "center",
              }}
            >
              {message}
            </div>
          )}

          <div
            style={{ marginTop: 12, display: "flex", gap: 8, flexWrap: "wrap" }}
          >
            <button
              className="btn primary"
              onClick={() => navigate("/login")}
              style={{ flex: 1 }}
            >
              Quay trở lại đăng nhập
            </button>
            <button
              className="btn"
              onClick={() => navigate("/")}
              style={{ flex: 1 }}
            >
              Về trang chủ
            </button>
          </div>
        </div>
      </section>
    </div>
  );
}
