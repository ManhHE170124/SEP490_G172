// File: src/pages/tickets/customer-ticket-create.jsx
import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ticketsApi } from "../../api/ticketsApi";
import axiosClient from "../../api/axiosClient";
import "../../styles/customer-ticket-create.css";

// Map severity -> tiếng Việt
const SEVERITY_LABELS = {
  Low: "Thấp",
  Medium: "Trung bình",
  High: "Cao",
  Critical: "Nghiêm trọng",
};

// Map Category (trong DB) -> tiếng Việt
const TEMPLATE_CATEGORY_LABELS = {
  Payment: "Thanh toán",
  Key: "Key / License",
  Account: "Tài khoản dịch vụ",
  Refund: "Hoàn tiền / đổi sản phẩm",
  Support: "Hỗ trợ kỹ thuật / cài đặt",
  Security: "Bảo mật / rủi ro",
  General: "Tư vấn / khác",
};

// Thứ tự ưu tiên hiển thị category
const CATEGORY_ORDER = [
  "Payment",
  "Key",
  "Account",
  "Refund",
  "Support",
  "Security",
  "General",
];

export default function CustomerTicketCreatePage() {
  const navigate = useNavigate();

  // Form tạo ticket
  const [createSubject, setCreateSubject] = useState("");
  const [createDescription, setCreateDescription] = useState("");
  const [createError, setCreateError] = useState("");
  const [creating, setCreating] = useState(false);

  const SUBJECT_MAX = 120;
  const DESCRIPTION_MAX = 1000;

  // Template tiêu đề từ BE
  const [templates, setTemplates] = useState([]);
  const [templatesLoading, setTemplatesLoading] = useState(false);
  const [templatesError, setTemplatesError] = useState("");

  // Lựa chọn trên UI
  const [selectedCategory, setSelectedCategory] = useState("");
  const [selectedTemplateCode, setSelectedTemplateCode] = useState("");

  // Tải TicketSubjectTemplates từ BE
  useEffect(() => {
    let cancelled = false;

    const loadTemplates = async () => {
      setTemplatesLoading(true);
      setTemplatesError("");
      try {
        // BE gợi ý: GET /api/Tickets/subject-templates?activeOnly=true
        const res = await axiosClient.get("/tickets/subject-templates", {
          params: { activeOnly: true },
        });
        if (cancelled) return;

        const list = Array.isArray(res?.data) ? res.data : [];
        setTemplates(list);
      } catch (err) {
        console.error("Failed to load ticket subject templates", err);
        if (!cancelled) {
          setTemplatesError(
            "Không tải được danh sách tiêu đề mẫu. Bạn vẫn có thể tự nhập tiêu đề."
          );
        }
      } finally {
        if (!cancelled) {
          setTemplatesLoading(false);
        }
      }
    };

    loadTemplates();
    return () => {
      cancelled = true;
    };
  }, []);

  // Danh sách Category thực tế có template
  const categories = CATEGORY_ORDER.filter((cat) =>
    templates.some((t) => (t.category || "General") === cat && t.isActive !== false)
  );

  // Lọc template theo Category đã chọn
  const filteredTemplates = templates.filter((t) => {
    if (t.isActive === false) return false;
    const cat = t.category || "General";
    if (!selectedCategory) return false;
    return cat === selectedCategory;
  });

  const selectedTemplate =
    filteredTemplates.find((t) => t.templateCode === selectedTemplateCode) ||
    templates.find((t) => t.templateCode === selectedTemplateCode) ||
    null;

  function validateCreateForm() {
    const subjectTrimmed = createSubject.trim();
    const descriptionTrimmed = createDescription.trim();

    if (!subjectTrimmed) {
      setCreateError("Vui lòng nhập tiêu đề ticket.");
      return false;
    }

    if (!descriptionTrimmed) {
      setCreateError("Vui lòng mô tả vấn đề của bạn.");
      return false;
    }

    if (subjectTrimmed.length > SUBJECT_MAX) {
      setCreateError(`Tiêu đề ticket tối đa ${SUBJECT_MAX} ký tự.`);
      return false;
    }

    if (descriptionTrimmed.length > DESCRIPTION_MAX) {
      setCreateError(`Mô tả ticket tối đa ${DESCRIPTION_MAX} ký tự.`);
      return false;
    }

    setCreateError("");
    return true;
  }

  async function handleCreateSubmit(e) {
    e.preventDefault();
    if (!validateCreateForm()) return;

    setCreating(true);
    setCreateError("");

    try {
      const subjectTrimmed = createSubject.trim();
      const descriptionTrimmed = createDescription.trim();

      const payload = {
        // BE: CustomerCreateTicketDto { subject, description }
        subject: subjectTrimmed,
        description: descriptionTrimmed,
        // Mở rộng sẵn cho tương lai: BE có thể nhận thêm 2 field này
        templateCode: selectedTemplate?.templateCode || null,
        severity: selectedTemplate?.severity || null,
      };

      const res = await ticketsApi.create(payload);
      const created = res?.data;

      // Reset form
      setCreateSubject("");
      setCreateDescription("");
      setSelectedCategory("");
      setSelectedTemplateCode("");

      if (created && created.ticketId) {
        navigate(`/tickets/${created.ticketId}`);
      } else {
        alert("Tạo ticket thành công.");
      }
    } catch (err) {
      console.error("Failed to create ticket", err);
      const message =
        err?.response?.data?.message ||
        (err?.isNetworkError
          ? "Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại."
          : "Không tạo được ticket. Vui lòng thử lại.");
      setCreateError(message);
    } finally {
      setCreating(false);
    }
  }

  return (
    <div className="ctc-page">
      {/* Header giống phong cách admin-ticket-detail */}
      <div className="ctc-header">
        <div className="ctc-header-left">
          <div className="ctc-badge">Trung tâm hỗ trợ</div>
          <h1 className="ctc-title">Gửi yêu cầu hỗ trợ</h1>
          <p className="ctc-subtitle">
            Vui lòng chọn nhóm vấn đề, tiêu đề phù hợp và mô tả chi tiết sự cố.
            Đội ngũ hỗ trợ sẽ phản hồi trong thời gian sớm nhất.
          </p>
        </div>
        <div className="ctc-header-right">
          <button
            type="button"
            className="btn btn-outline-secondary btn-sm ctc-back-btn"
            onClick={() => navigate(-1)}
          >
            Quay lại
          </button>
        </div>
      </div>

      <div className="ctc-content">
        {/* Cột trái: Form tạo ticket */}
        <div className="ctc-left">
          <div className="ctc-card">
            <div className="ctc-card-title">Thông tin ticket</div>
            <div className="ctc-card-body">
              {createError && (
                <div className="alert alert-danger py-2 mb-3">
                  {createError}
                </div>
              )}

              <form onSubmit={handleCreateSubmit} noValidate>
                {/* Nhóm vấn đề */}
                <div className="mb-3">
                  <label className="form-label fw-semibold">
                    Nhóm vấn đề <span className="text-danger">*</span>
                  </label>
                  <select
                    className="form-select"
                    value={selectedCategory}
                    onChange={(e) => {
                      const value = e.target.value;
                      setSelectedCategory(value);
                      setSelectedTemplateCode("");
                      setCreateError("");
                    }}
                  >
                    <option value="">
                      -- Chọn nhóm vấn đề bạn đang gặp phải --
                    </option>
                    {categories.map((cat) => (
                      <option key={cat} value={cat}>
                        {TEMPLATE_CATEGORY_LABELS[cat] || cat}
                      </option>
                    ))}
                  </select>
                  <div className="ctc-field-footer">
                    <small className="text-muted">
                      Nhóm vấn đề giúp hệ thống gợi ý đúng tiêu đề và mức độ
                      ưu tiên xử lý.
                    </small>
                  </div>
                </div>

                {/* Tiêu đề mẫu theo Category */}
                <div className="mb-3">
                  <label className="form-label fw-semibold">
                    Tiêu đề mẫu (gợi ý)
                  </label>
                  <div className="ctc-template-row">
                    <select
                      className="form-select"
                      value={selectedTemplateCode}
                      disabled={
                        !selectedCategory || filteredTemplates.length === 0
                      }
                      onChange={(e) => {
                        const code = e.target.value;
                        setSelectedTemplateCode(code);
                        setCreateError("");
                        const tmpl = templates.find(
                          (t) => t.templateCode === code
                        );
                        if (tmpl) {
                          setCreateSubject(tmpl.title || "");
                        }
                      }}
                    >
                      <option value="">
                        {!selectedCategory
                          ? "-- Vui lòng chọn nhóm vấn đề trước --"
                          : filteredTemplates.length === 0
                          ? "-- Chưa có tiêu đề mẫu cho nhóm này --"
                          : "-- Chọn tiêu đề gần với vấn đề của bạn --"}
                      </option>
                      {filteredTemplates.map((t) => (
                        <option key={t.templateCode} value={t.templateCode}>
                          [{SEVERITY_LABELS[t.severity] || t.severity}]{" "}
                          {t.title}
                        </option>
                      ))}
                    </select>

                    {selectedTemplate && selectedTemplate.severity && (
                      <span
                        className={
                          "ctc-severity-pill " +
                          `ctc-severity-${String(
                            selectedTemplate.severity
                          ).toLowerCase()}`
                        }
                      >
                        Mức độ:{" "}
                        {SEVERITY_LABELS[selectedTemplate.severity] ||
                          selectedTemplate.severity}
                      </span>
                    )}
                  </div>
                  <div className="ctc-field-footer">
                    {templatesLoading && (
                      <small className="text-muted">
                        Đang tải danh sách tiêu đề mẫu...
                      </small>
                    )}
                    {!templatesLoading && templatesError && (
                      <small className="text-danger">{templatesError}</small>
                    )}
                    {!templatesLoading && !templatesError && (
                      <small className="text-muted">
                        Sau khi chọn, tiêu đề bên dưới sẽ tự được điền sẵn. Bạn
                        vẫn có thể chỉnh sửa lại cho đúng.
                      </small>
                    )}
                  </div>
                </div>

                {/* Tiêu đề (cho phép chỉnh sửa) */}
                <div className="mb-3">
                  <label className="form-label fw-semibold">
                    Tiêu đề <span className="text-danger">*</span>
                  </label>
                  <input
                    type="text"
                    className="form-control"
                    value={createSubject}
                    maxLength={SUBJECT_MAX}
                    onChange={(e) => {
                      setCreateSubject(e.target.value);
                      if (createError) setCreateError("");
                    }}
                    placeholder="Ví dụ: Không nhận được key sau khi thanh toán"
                  />
                  <div className="ctc-field-footer">
                    <small className="text-muted">
                      Ghi ngắn gọn, đúng nội dung chính của vấn đề.
                    </small>
                    <small className="text-muted">
                      {createSubject.length}/{SUBJECT_MAX}
                    </small>
                  </div>
                </div>

                {/* Mô tả chi tiết */}
                <div className="mb-3">
                  <label className="form-label fw-semibold">
                    Mô tả chi tiết <span className="text-danger">*</span>
                  </label>
                  <textarea
                    className="form-control"
                    rows={6}
                    value={createDescription}
                    maxLength={DESCRIPTION_MAX}
                    onChange={(e) => {
                      setCreateDescription(e.target.value);
                      if (createError) setCreateError("");
                    }}
                    placeholder="Mô tả chi tiết vấn đề, kèm mã đơn hàng, thời gian thanh toán, tài khoản sử dụng (nếu có)..."
                  />
                  <div className="ctc-field-footer">
                    <small className="text-muted">
                      Càng nhiều thông tin, đội ngũ hỗ trợ càng xử lý nhanh.
                    </small>
                    <small className="text-muted">
                      {createDescription.length}/{DESCRIPTION_MAX}
                    </small>
                  </div>
                </div>

                <button
                  type="submit"
                  className="btn btn-primary w-100 ctc-submit-btn"
                  disabled={creating}
                >
                  {creating ? "Đang tạo ticket..." : "Gửi ticket"}
                </button>
              </form>

              <div className="ctc-footer-note">
                Sau khi gửi, bạn sẽ được chuyển đến trang chi tiết ticket để
                theo dõi trạng thái xử lý và trao đổi với nhân viên hỗ trợ.
              </div>
            </div>
          </div>
        </div>

        {/* Cột phải: Tip / hướng dẫn */}
        <div className="ctc-right">
          <div className="ctc-card ctc-tips-card">
            <div className="ctc-card-title">Gợi ý để xử lý nhanh hơn</div>
            <ul className="ctc-tips-list">
              <li>
                Đính kèm <strong>mã đơn hàng</strong> hoặc <strong>email</strong>{" "}
                đã dùng để mua.
              </li>
              <li>
                Ghi rõ <strong>thời gian thanh toán</strong> và{" "}
                <strong>phương thức thanh toán</strong>.
              </li>
              <li>
                Nếu liên quan đến tài khoản dịch vụ, vui lòng ghi rõ{" "}
                <strong>tên tài khoản</strong>.
              </li>
              <li>
                Không gửi thông tin nhạy cảm như mật khẩu thanh toán, mã OTP...
              </li>
            </ul>
            <div className="ctc-sla-note">
              Ticket sẽ được xếp hàng và xử lý theo{" "}
              <strong>mức độ ưu tiên</strong> của tài khoản cùng{" "}
              <strong>quy tắc SLA</strong> của hệ thống.
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
