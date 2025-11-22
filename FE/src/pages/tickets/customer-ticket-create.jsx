// File: src/pages/tickets/customer-ticket-create.jsx
import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ticketsApi } from "../../api/ticketsApi";
import axiosClient from "../../api/axiosClient";
import "../../styles/customer-ticket-create.css";

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
  const [createDescription, setCreateDescription] = useState("");
  const [createError, setCreateError] = useState("");
  const [creating, setCreating] = useState(false);

  const DESCRIPTION_MAX = 1000;

  // Templates từ backend
  const [templates, setTemplates] = useState([]);
  const [templatesLoading, setTemplatesLoading] = useState(false);
  const [templatesError, setTemplatesError] = useState("");

  // Lựa chọn của user
  const [selectedCategory, setSelectedCategory] = useState("");
  const [selectedTemplateCode, setSelectedTemplateCode] = useState("");

  // Tải TicketSubjectTemplates từ backend
  useEffect(() => {
    let cancelled = false;

    const loadTemplates = async () => {
      setTemplatesLoading(true);
      setTemplatesError("");
      try {
        // BE: GET /api/Tickets/subject-templates?activeOnly=true
        const res = await axiosClient.get("/tickets/subject-templates", {
          params: { activeOnly: true },
        });
        if (cancelled) return;

        const list = Array.isArray(res) ? res : [];
        setTemplates(list);
      } catch (err) {
        console.error("Failed to load ticket subject templates", err);
        if (!cancelled) {
          setTemplatesError(
            "Không tải được danh sách tiêu đề mẫu. Vui lòng thử lại sau."
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
    templates.some(
      (t) =>
        (t.category || "General") === cat &&
        (t.isActive === undefined || t.isActive)
    )
  );

  // Templates theo Category được chọn
  const filteredTemplates = templates.filter((t) => {
    const cat = t.category || "General";
    const active = t.isActive === undefined || t.isActive;
    if (!active) return false;
    if (!selectedCategory) return false;
    return cat === selectedCategory;
  });

  const selectedTemplate =
    filteredTemplates.find((t) => t.templateCode === selectedTemplateCode) ||
    templates.find((t) => t.templateCode === selectedTemplateCode) ||
    null;

  function validateCreateForm() {
    const descTrimmed = createDescription.trim();

    if (!selectedCategory) {
      setCreateError("Vui lòng chọn nhóm vấn đề.");
      return false;
    }

    if (!selectedTemplateCode) {
      setCreateError("Vui lòng chọn tiêu đề phù hợp với vấn đề của bạn.");
      return false;
    }

    if (!descTrimmed) {
      setCreateError("Vui lòng mô tả chi tiết vấn đề của bạn.");
      return false;
    }

    if (descTrimmed.length > DESCRIPTION_MAX) {
      setCreateError(`Mô tả tối đa ${DESCRIPTION_MAX} ký tự.`);
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
      const descTrimmed = createDescription.trim();

      const payload = {
        // BE mới: chỉ cần templateCode, description
        templateCode: selectedTemplate?.templateCode || selectedTemplateCode,
        description: descTrimmed || null,
      };

      const created = await ticketsApi.create(payload);

      // Reset form
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

                {/* Tiêu đề (template) theo Category */}
                <div className="mb-3">
                  <label className="form-label fw-semibold">
                    Tiêu đề ticket <span className="text-danger">*</span>
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
                      }}
                    >
                      <option value="">
                        {!selectedCategory
                          ? "-- Vui lòng chọn nhóm vấn đề trước --"
                          : filteredTemplates.length === 0
                          ? "-- Chưa có tiêu đề mẫu cho nhóm này --"
                          : "-- Chọn tiêu đề phù hợp với vấn đề của bạn --"}
                      </option>
                      {filteredTemplates.map((t) => (
                        <option key={t.templateCode} value={t.templateCode}>
                          {t.title}
                        </option>
                      ))}
                    </select>
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
                        Tiêu đề được cố định theo mẫu, bạn chỉ cần chọn đúng
                        nhóm và nội dung phù hợp.
                      </small>
                    )}
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
                Sau khi gửi, hệ thống sẽ tạo ticket với tiêu đề và mức độ ưu
                tiên tương ứng với mẫu bạn đã chọn. Bạn có thể theo dõi trạng
                thái xử lý trong mục “Ticket của tôi”.
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
                Đính kèm <strong>mã đơn hàng</strong> hoặc{" "}
                <strong>email</strong> đã dùng để mua.
              </li>
              <li>
                Ghi rõ <strong>thời gian thanh toán</strong> và{" "}
                <strong>phương thức thanh toán</strong>.
              </li>
              <li>
                Nếu liên quan đến tài khoản dịch vụ, vui lòng cung cấp{" "}
                <strong>tài khoản đăng nhập</strong> hoặc{" "}
                <strong>email</strong> dùng để sử dụng dịch vụ.
              </li>
              <li>
                Mô tả các bước thao tác trước khi gặp lỗi (nếu có) để đội ngũ
                hỗ trợ dễ tái hiện vấn đề.
              </li>
            </ul>
            <div className="ctc-sla-note">
              Ticket sẽ được xếp hàng xử lý theo mức độ ưu tiên tương ứng với
              loại vấn đề mà bạn chọn. Bạn có thể tiếp tục trao đổi trong luồng
              ticket sau khi gửi.
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
