// src/pages/admin/components/FaqsPanel.jsx
import React from "react";
import { ProductFaqsApi } from "../../services/productFaqs";
import ToastContainer from "../../components/Toast/ToastContainer";

const truncate = (s, n = 140) =>
  s && s.length > n ? s.slice(0, n) + "…" : s ?? "—";

const QUESTION_MIN = 10;
const QUESTION_MAX = 200;
const ANSWER_MIN = 10;
const ANSWER_MAX = 2000;

export default function FaqsPanel({ productId, onTotalChange }) {
  // Data + paging
  const [items, setItems] = React.useState([]);
  const [total, setTotal] = React.useState(0);
  const [page, setPage] = React.useState(1);
  const [size, setSize] = React.useState(10);
  const [loading, setLoading] = React.useState(false);
  const totalPages = Math.max(1, Math.ceil(total / size));

  // Query
  const [q, setQ] = React.useState("");
  const [active, setActive] = React.useState(""); // "", "true", "false"
  const [sort, setSort] = React.useState("sortOrder");
  const [dir, setDir] = React.useState("asc");

  // Modal & form state
  const [showModal, setShowModal] = React.useState(false);
  const [editing, setEditing] = React.useState(null);
  const [faqForm, setFaqForm] = React.useState({
    question: "",
    answer: "",
    sortOrder: 0,
  });
  const [faqErrors, setFaqErrors] = React.useState({});

  // Toast + Confirm
  const [toasts, setToasts] = React.useState([]);
  const [confirmDialog, setConfirmDialog] = React.useState(null);

  const removeToast = React.useCallback(
    (id) => setToasts((ts) => ts.filter((t) => t.id !== id)),
    []
  );

  const addToast = React.useCallback(
    (type, title, message) => {
      const id = `${Date.now()}-${Math.random()}`;
      setToasts((ts) => [...ts, { id, type, title, message }]);
      setTimeout(() => removeToast(id), 5000);
    },
    [removeToast]
  );

  const askConfirm = React.useCallback((title, message) => {
    return new Promise((resolve) => {
      setConfirmDialog({
        title,
        message,
        onConfirm: () => {
          setConfirmDialog(null);
          resolve(true);
        },
        onCancel: () => {
          setConfirmDialog(null);
          resolve(false);
        },
      });
    });
  }, []);

  // ===== Load =====
  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      const res = await ProductFaqsApi.listPaged(productId, {
        keyword: q || undefined,
        active: active === "" ? undefined : active === "true",
        sort,
        direction: dir,
        page,
        pageSize: size,
      });
      const list = res?.items ?? [];
      setItems(list);
      const totalNumber = Number(res?.total ?? 0);
      setTotal(totalNumber);

      if (typeof onTotalChange === "function") {
        onTotalChange(totalNumber);
      }
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        "Lỗi tải FAQ",
        e?.response?.data?.message || e.message
      );
    } finally {
      setLoading(false);
    }
  }, [productId, q, active, sort, dir, page, size, onTotalChange, addToast]);

  React.useEffect(() => {
    load();
  }, [load]);

  React.useEffect(() => {
    setPage(1);
  }, [q, active, sort, dir, size]);

  // Reset filter
  const resetFilters = () => {
    setQ("");
    setActive("");
    setSort("sortOrder");
    setDir("asc");
    setPage(1);
    setSize(10);
  };

  // ===== Sort =====
  const headerSort = (key) => {
    setSort((cur) => {
      if (cur === key) {
        setDir((d) => (d === "asc" ? "desc" : "asc"));
        return cur;
      }
      setDir("asc");
      return key;
    });
  };
  const sortMark = (key) =>
    sort === key ? (dir === "asc" ? " ▲" : " ▼") : "";

  // ===== CRUD =====
  const openCreate = () => {
    setEditing(null);
    setFaqForm({ question: "", answer: "", sortOrder: 0 });
    setFaqErrors({});
    setShowModal(true);
  };

  const openEdit = (f) => {
    setEditing(f);
    setFaqForm({
      question: f.question || "",
      answer: f.answer || "",
      sortOrder: f.sortOrder ?? 0,
    });
    setFaqErrors({});
    setShowModal(true);
  };

  const handleFaqChange = (field) => (e) => {
    const value =
      field === "sortOrder" ? e.target.value : e.target.value || "";
    setFaqForm((prev) => ({ ...prev, [field]: value }));
  };

  const validateFaq = () => {
    const errors = {};
    const qText = faqForm.question.trim();
    const aText = faqForm.answer.trim();
    const qLen = qText.length;
    const aLen = aText.length;

    if (!qText) {
      errors.question = "Câu hỏi là bắt buộc.";
    } else if (qLen < QUESTION_MIN || qLen > QUESTION_MAX) {
      errors.question = `Câu hỏi phải từ ${QUESTION_MIN}–${QUESTION_MAX} ký tự.`;
    }

    if (!aText) {
      errors.answer = "Trả lời là bắt buộc.";
    } else if (aLen < ANSWER_MIN || aLen > ANSWER_MAX) {
      errors.answer = `Trả lời phải từ ${ANSWER_MIN}–${ANSWER_MAX} ký tự.`;
    }

    setFaqErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const onSubmit = async (e) => {
    e.preventDefault();
    if (!validateFaq()) {
      addToast(
        "warning",
        "Dữ liệu FAQ chưa hợp lệ",
        "Vui lòng kiểm tra lại Câu hỏi và Trả lời."
      );
      return;
    }

    const isActiveSwitch = document.getElementById("faqActiveSwitch");
    const isActiveValue = isActiveSwitch ? !!isActiveSwitch.checked : true;

    const dto = {
      question: faqForm.question.trim(),
      answer: faqForm.answer.trim(),
      sortOrder: Number(faqForm.sortOrder || 0) || 0,
      isActive: isActiveValue,
    };

    try {
      if (editing?.faqId) {
        await ProductFaqsApi.update(productId, editing.faqId, dto);
        addToast(
          "success",
          "Cập nhật FAQ",
          "Câu hỏi thường gặp đã được cập nhật."
        );
      } else {
        await ProductFaqsApi.create(productId, dto);
        addToast("success", "Thêm FAQ", "Câu hỏi thường gặp mới đã được tạo.");
      }
      setShowModal(false);
      setPage(1);
      await load();
    } catch (e2) {
      console.error(e2);
      addToast(
        "error",
        "Lưu FAQ thất bại",
        e2?.response?.data?.message || e2.message
      );
    }
  };

  const onDelete = async (faqId) => {
    const ok = await askConfirm(
      "Xoá FAQ",
      "Bạn có chắc chắn muốn xoá câu hỏi này?"
    );
    if (!ok) return;

    try {
      await ProductFaqsApi.remove(productId, faqId);
      addToast("success", "Đã xoá FAQ", "Câu hỏi đã được xoá.");
      await load();
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        "Xoá FAQ thất bại",
        e?.response?.data?.message || e.message
      );
    }
  };

  const toggleActive = async (f) => {
    try {
      await ProductFaqsApi.toggle(productId, f.faqId);

      const willBeActive = !f.isActive;
      const newStateText = willBeActive
        ? "bật hiển thị"
        : "ẩn khỏi danh sách";
      const qShort = truncate(f.question, 80);

      addToast(
        "success",
        "Cập nhật trạng thái FAQ",
        `Câu hỏi "${qShort}" đã được ${newStateText}.`
      );

      await load();
    } catch (e) {
      console.error(e);
      addToast(
        "error",
        "Đổi trạng thái FAQ thất bại",
        e?.response?.data?.message || e.message
      );
    }
  };

  // ===== Pager =====
  const goto = (p) => setPage(Math.min(Math.max(1, p), totalPages));
  const makePageList = React.useMemo(() => {
    const pages = [];
    const win = 2;
    const from = Math.max(1, page - win);
    const to = Math.min(totalPages, page + win);
    if (from > 1) {
      pages.push(1);
      if (from > 2) pages.push("…L");
    }
    for (let i = from; i <= to; i++) pages.push(i);
    if (to < totalPages) {
      if (to < totalPages - 1) pages.push("…R");
      pages.push(totalPages);
    }
    return pages;
  }, [page, totalPages]);

  const startIdx = total === 0 ? 0 : (page - 1) * size + 1;
  const endIdx = Math.min(total, page * size);

  const qLen = faqForm.question.length;
  const aLen = faqForm.answer.length;
  const qLenInvalid =
    qLen > 0 && (qLen < QUESTION_MIN || qLen > QUESTION_MAX);
  const aLenInvalid =
    aLen > 0 && (aLen < ANSWER_MIN || aLen > ANSWER_MAX);

  return (
    <div className="group" style={{ gridColumn: "1 / 3" }}>
      <div className="panel">
        <div className="panel-header" style={{ alignItems: "center" }}>
          <h4>
            Câu hỏi thường gặp (FAQ)
            <span
              style={{
                fontSize: 12,
                color: "var(--muted)",
                marginLeft: 8,
              }}
            >
              ({total})
            </span>
          </h4>

          {/* Toolbar: search + filter + reset + thêm */}
          <div className="variants-toolbar">
            <input
              className="ctl"
              placeholder="Tìm theo câu hỏi / đáp án…"
              value={q}
              onChange={(e) => setQ(e.target.value)}
            />
            <select
              className="ctl"
              value={active}
              onChange={(e) => setActive(e.target.value)}
            >
              <option value="">Tất cả trạng thái</option>
              <option value="true">Đang hiển thị</option>
              <option value="false">Đang ẩn</option>
            </select>
            <button className="btn" onClick={resetFilters}>
              Đặt lại
            </button>
            <button className="btn primary" onClick={openCreate}>
              + Thêm FAQ
            </button>
          </div>
        </div>

        <div className="panel-body variants-area">
          {loading ? (
            <div>Đang tải…</div>
          ) : (
            <div className="variants-wrap">
              <div className="variants-scroller">
                <table className="variants-table">
                  <colgroup>
                    <col style={{ width: "30%" }} />
                    <col style={{ width: "46%" }} />
                    <col style={{ width: "10%" }} />
                    <col style={{ width: "14%" }} />
                  </colgroup>
                  <thead>
                    <tr>
                      <th
                        onClick={() => headerSort("question")}
                        style={{ cursor: "pointer" }}
                      >
                        Câu hỏi{sortMark("question")}
                      </th>
                      <th>Trả lời</th>
                      <th
                        onClick={() => headerSort("sortOrder")}
                        style={{ cursor: "pointer" }}
                      >
                        Thứ tự{sortMark("sortOrder")}
                      </th>
                      <th
                        onClick={() => headerSort("active")}
                        style={{ cursor: "pointer" }}
                      >
                        Trạng thái{sortMark("active")}
                      </th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>

                  <tbody>
                    {items.map((f) => (
                      <tr key={f.faqId}>
                        <td>
                          <div style={{ fontWeight: 600 }}>{f.question}</div>
                        </td>
                        <td className="muted">
                          {truncate(f.answer, 220)}
                        </td>
                        <td className="mono">{f.sortOrder ?? 0}</td>
                        <td className="col-status">
                          <span
                            className={`badge ${
                              f.isActive ? "green" : "gray"
                            }`}
                            style={{ textTransform: "none" }}
                          >
                            {f.isActive ? "Hiển thị" : "Ẩn"}
                          </span>
                        </td>
                        <td className="td-actions td-left">
                          <div className="row" style={{ gap: 8 }}>
                            <button
                              className="action-btn edit-btn"
                              title="Sửa"
                              onClick={() => openEdit(f)}
                            >
                              <svg
                                viewBox="0 0 24 24"
                                fill="currentColor"
                                aria-hidden="true"
                              >
                                <path d="M3 17.25V21h3.75l11.06-11.06-3.75-3.75L3 17.25z" />
                                <path d="M20.71 7.04a1.003 1.003 0 0 0 0-1.42l-2.34-2.34a1.003 1.003 0 0 0-1.42 0l-1.83 1.83 3.75 3.75 1.84-1.82z" />
                              </svg>
                            </button>

                            <button
                              className="action-btn delete-btn"
                              title="Xoá"
                              onClick={() => onDelete(f.faqId)}
                            >
                              <svg
                                viewBox="0 0 24 24"
                                fill="currentColor"
                                aria-hidden="true"
                              >
                                <path d="M16 9v10H8V9h8m-1.5-6h-5l-1 1H5v2h14V4h-3.5l-1-1z" />
                              </svg>
                            </button>

                            <label
                              className="switch"
                              title="Bật/Tắt hiển thị"
                            >
                              <input
                                type="checkbox"
                                checked={Boolean(f.isActive)}
                                onChange={() => toggleActive(f)}
                              />
                              <span className="slider" />
                            </label>
                          </div>
                        </td>
                      </tr>
                    ))}

                    {items.length === 0 && (
                      <tr>
                        <td
                          colSpan={5}
                          style={{
                            textAlign: "center",
                            color: "var(--muted)",
                            padding: 18,
                          }}
                        >
                          Chưa có câu hỏi nào.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              <div
                className="variants-footer"
                style={{
                  gap: 12,
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  flexWrap: "wrap",
                }}
              >
                <div className="muted">
                  Hiển thị {startIdx}-{endIdx} / {total}
                </div>

                <div
                  className="row"
                  style={{ gap: 8, alignItems: "center", flexWrap: "wrap" }}
                >
                  <div
                    className="row"
                    style={{ gap: 6, alignItems: "center" }}
                  >
                    <span
                      className="muted"
                      style={{ fontSize: 12 }}
                    >
                      Dòng/trang
                    </span>
                    <select
                      className="ctl"
                      value={size}
                      onChange={(e) => {
                        setSize(Number(e.target.value));
                        setPage(1);
                      }}
                    >
                      <option value={5}>5</option>
                      <option value={10}>10</option>
                      <option value={20}>20</option>
                      <option value={50}>50</option>
                    </select>
                  </div>

                  <div className="row" style={{ gap: 6 }}>
                    <button
                      className="btn"
                      disabled={page <= 1}
                      onClick={() => goto(1)}
                      title="Trang đầu"
                    >
                      «
                    </button>
                    <button
                      className="btn"
                      disabled={page <= 1}
                      onClick={() => goto(page - 1)}
                      title="Trang trước"
                    >
                      ←
                    </button>

                    {makePageList.map((pKey, idx) => {
                      if (typeof pKey !== "number")
                        return (
                          <span key={pKey + idx} className="muted">
                            …
                          </span>
                        );
                      const activeBtn = pKey === page;
                      return (
                        <button
                          key={pKey}
                          className={`btn ${activeBtn ? "primary" : ""}`}
                          onClick={() => goto(pKey)}
                          disabled={activeBtn}
                          style={{ minWidth: 36 }}
                          title={`Trang ${pKey}`}
                        >
                          {pKey}
                        </button>
                      );
                    })}

                    <button
                      className="btn"
                      disabled={page >= totalPages}
                      onClick={() => goto(page + 1)}
                      title="Trang sau"
                    >
                      →
                    </button>
                    <button
                      className="btn"
                      disabled={page >= totalPages}
                      onClick={() => goto(totalPages)}
                      title="Trang cuối"
                    >
                      »
                    </button>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Modal create/edit */}
      {showModal && (
        <div className="modal-backdrop">
          <div className="modal">
            <div className="modal-topbar">
              <h3 style={{ margin: 0 }}>
                {editing ? "Sửa FAQ" : "Thêm FAQ"}
              </h3>
              <div className="row" style={{ gap: 8, alignItems: "center" }}>
                <span className="muted" style={{ fontSize: 12 }}>
                  Hiển thị
                </span>
                <label className="switch">
                  <input
                    type="checkbox"
                    id="faqActiveSwitch"
                    defaultChecked={editing?.isActive ?? true}
                  />
                  <span className="slider" />
                </label>
              </div>
            </div>

            <form
              onSubmit={onSubmit}
              className="input-group"
              style={{ marginTop: 12 }}
            >
              <div className="group">
                <span>
                  Câu hỏi <span style={{ color: "#dc2626" }}>*</span>
                </span>
                <input
                  name="question"
                  value={faqForm.question}
                  onChange={handleFaqChange("question")}
                  className={faqErrors.question ? "input-error" : ""}
                />
                <div
                  className="muted"
                  style={{
                    fontSize: 12,
                    marginTop: 2,
                    color: qLenInvalid ? "#dc2626" : "var(--muted)",
                  }}
                >
                  {QUESTION_MIN}–{QUESTION_MAX} ký tự. Hiện tại: {qLen}/
                  {QUESTION_MAX}
                </div>
                {faqErrors.question && (
                  <div className="field-error">{faqErrors.question}</div>
                )}
              </div>
              <div className="group">
                <span>
                  Trả lời <span style={{ color: "#dc2626" }}>*</span>
                </span>
                <textarea
                  name="answer"
                  rows={4}
                  value={faqForm.answer}
                  onChange={handleFaqChange("answer")}
                  className={faqErrors.answer ? "input-error" : ""}
                />
                <div
                  className="muted"
                  style={{
                    fontSize: 12,
                    marginTop: 2,
                    color: aLenInvalid ? "#dc2626" : "var(--muted)",
                  }}
                >
                  {ANSWER_MIN}–{ANSWER_MAX} ký tự. Hiện tại: {aLen}/
                  {ANSWER_MAX}
                </div>
                {faqErrors.answer && (
                  <div className="field-error">{faqErrors.answer}</div>
                )}
              </div>
              <div className="grid cols-2" style={{ marginTop: 8 }}>
                <div className="group">
                  <span>Thứ tự</span>
                  <input
                    type="number"
                    min={0}
                    step={1}
                    name="sortOrder"
                    value={faqForm.sortOrder}
                    onChange={handleFaqChange("sortOrder")}
                  />
                </div>
              </div>

              <div
                className="row"
                style={{ marginTop: 12, justifyContent: "flex-end", gap: 8 }}
              >
                <button
                  type="button"
                  className="btn"
                  onClick={() => setShowModal(false)}
                >
                  Hủy
                </button>
                <button type="submit" className="btn primary">
                  {editing ? "Lưu" : "Thêm"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </div>
  );
}
