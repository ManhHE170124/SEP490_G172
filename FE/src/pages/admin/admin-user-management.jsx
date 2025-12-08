/**
 * File: admin-user-management.jsx
 * Purpose: React page for managing users in Keytietkiem admin.
 * Notes (update):
 *  - BỎ passwordPlain và toggle xem mật khẩu.
 *  - THÊM input `username` (tạo/sửa). Nếu bỏ trống sẽ mặc định dùng email.
 *  - Validate FE theo giới hạn DB (max length) + rule mật khẩu:
 *      + Tạo mới: bắt buộc, >= 6 ký tự.
 *      + Cập nhật: tùy chọn, nếu nhập thì >= 6 ký tự.
 *  - Màn "Chi tiết người dùng" KHÔNG hiển thị trường mật khẩu (vì mật khẩu băm 1 chiều).
 *  - Nâng cấp validate: highlight từng field, hiện message dưới input,
 *    và disable nút Lưu khi form đang có lỗi (đúng yêu cầu đề bài).
 */
import React, { useEffect, useMemo, useState, useCallback } from "react";
import "../../styles/admin-user-management.css";
import { usersApi } from "../../api/usersApi";
import { USER_STATUS, USER_STATUS_OPTIONS } from "../../constants/userStatus";
import ToastContainer from "../../components/Toast/ToastContainer";
import useToast from "../../hooks/useToast";
import PermissionGuard from "../../components/PermissionGuard";
import { MODULE_CODES, PERMISSION_CODES } from "../../constants/roleConstants";

function ErrorDialog({ message, onClose, showError }) {
  if (message) {
    showError("Thông báo lỗi", message);
  }
  return null;
}

const initialFilters = {
  q: "",
  roleId: "",
  status: "",
  page: 1,
  pageSize: 10,
  sortBy: "CreatedAt",
  sortDir: "desc",
};

// Giới hạn theo DB / DTO
const FIELD_LIMITS = {
  firstName: 80,
  lastName: 80,
  email: 254,
  username: 60,
  phone: 32,
  address: 300,
  passwordMin: 6,
  passwordMax: 200,
};

export default function AdminUserManagement() {
  const { toasts, showSuccess, showError, removeToast } = useToast();

  const [uiFilters, setUiFilters] = useState(initialFilters);
  const [applied, setApplied] = useState(initialFilters);

  const [data, setData] = useState({ items: [], totalItems: 0, page: 1, pageSize: 10 });
  const [roles, setRoles] = useState([]);
  const [loading, setLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState("");

  const [open, setOpen] = useState(false);
  const [mode, setMode] = useState("view"); // 'view' | 'edit' | 'add'
  const [form, setForm] = useState({
    userId: "",
    firstName: "",
    lastName: "",
    email: "",
    username: "",
    phone: "",
    address: "",
    status: USER_STATUS.Active,
    roleId: "",
    newPassword: "",
    hasAccount: false,
  });

  // Lỗi theo từng field trong form modal
  const [fieldErrors, setFieldErrors] = useState({});

  const totalPages = useMemo(
    () => Math.max(1, Math.ceil((data.totalItems || 0) / (applied.pageSize || 10))),
    [data, applied.pageSize]
  );

  const fetchRoles = async () => {
    try {
      const res = await usersApi.roles();
      setRoles((res || []).filter((r) => !(r.name || "").toLowerCase().includes("admin")));
    } catch (err) {
      setErrorMsg(err.message || "Không tải được danh sách vai trò.");
    }
  };

  const fetchList = useCallback(
    async (take = applied) => {
      setLoading(true);
      try {
        const res = await usersApi.list(take);
        const filtered = {
          ...res,
          items: (res?.items || []).filter(
            (x) => !((x.roleName || "").toLowerCase().includes("admin"))
          ),
        };
        setData(filtered || { items: [], totalItems: 0, page: take.page, pageSize: take.pageSize });
      } catch (err) {
        setErrorMsg(err.message || "Không tải được danh sách người dùng.");
        setData((prev) => ({ ...prev, items: [] }));
      } finally {
        setLoading(false);
      }
    },
    [applied]
  );

  useEffect(() => {
    fetchRoles();
  }, []);

  useEffect(() => {
    fetchList(applied);
  }, [
    applied.page,
    applied.pageSize,
    applied.sortBy,
    applied.sortDir,
    applied.q,
    applied.roleId,
    applied.status,
    fetchList,
  ]);

  const onApply = (e) => {
    e.preventDefault();
    setApplied((prev) => ({ ...prev, ...uiFilters, page: 1 }));
  };

  const onReset = () => {
    setUiFilters({ ...initialFilters });
    setApplied({ ...initialFilters });
  };

  const gotoPage = (p) =>
    setApplied((prev) => ({
      ...prev,
      page: Math.max(1, Math.min(totalPages, p)),
    }));

  const openAdd = () => {
    setMode("add");
    setForm({
      userId: "",
      firstName: "",
      lastName: "",
      email: "",
      username: "",
      phone: "",
      address: "",
      status: USER_STATUS.Active,
      roleId: "",
      newPassword: "",
      hasAccount: false,
    });
    setFieldErrors({});
    setOpen(true);
  };

  const openViewOrEdit = async (id, m) => {
    try {
      const u = await usersApi.get(id);
      setMode(m);
      setForm({
        userId: u.userId,
        firstName: u.firstName || "",
        lastName: u.lastName || "",
        email: u.email || "",
        username: u.username || "",
        phone: u.phone || "",
        address: u.address || "",
        status: u.status,
        roleId: u.roleId || "",
        newPassword: "",
        hasAccount: !!u.hasAccount,
      });
      setFieldErrors({});
      setOpen(true);
    } catch (err) {
      setErrorMsg(err.message || "Không lấy được thông tin người dùng.");
    }
  };

  const trim = (v) => (v || "").trim();

  // Regex email cơ bản: phải có "@" và "."
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  /**
   * Validate toàn bộ form modal theo giới hạn DB + rule nghiệp vụ.
   * Trả về object { fieldName: message } nếu có lỗi.
   */
  const validateFields = useCallback(
    (currentForm, currentMode) => {
      const errors = {};

      const fn = trim(currentForm.firstName);
      if (!fn) {
        errors.firstName = "Họ không được để trống.";
      } else if (fn.length > FIELD_LIMITS.firstName) {
        errors.firstName = `Họ tối đa ${FIELD_LIMITS.firstName} ký tự.`;
      }

      const ln = trim(currentForm.lastName);
      if (!ln) {
        errors.lastName = "Tên không được để trống.";
      } else if (ln.length > FIELD_LIMITS.lastName) {
        errors.lastName = `Tên tối đa ${FIELD_LIMITS.lastName} ký tự.`;
      }

      const email = trim(currentForm.email);
      if (!email) {
        errors.email = "Email không được để trống.";
      } else if (email.length > FIELD_LIMITS.email) {
        errors.email = `Email tối đa ${FIELD_LIMITS.email} ký tự.`;
      } else if (!emailRegex.test(email)) {
        errors.email = "Email không hợp lệ.";
      }

      const username = trim(currentForm.username);
      if (username && username.length > FIELD_LIMITS.username) {
        errors.username = `Username tối đa ${FIELD_LIMITS.username} ký tự.`;
      }

      const phone = trim(currentForm.phone);
      if (phone) {
        if (phone.length > FIELD_LIMITS.phone) {
          errors.phone = `Điện thoại tối đa ${FIELD_LIMITS.phone} ký tự.`;
        } else if (!/^[0-9+\s\-()]+$/.test(phone)) {
          errors.phone =
            "Số điện thoại chỉ được chứa số và các ký tự + - ( ) khoảng trắng.";
        }
      }

      const address = trim(currentForm.address);
      if (address && address.length > FIELD_LIMITS.address) {
        errors.address = `Địa chỉ tối đa ${FIELD_LIMITS.address} ký tự.`;
      }

      if (!currentForm.roleId) {
        errors.roleId = "Vui lòng chọn vai trò.";
      }

      const pw = currentForm.newPassword || "";
      if (currentMode === "add") {
        if (!pw.trim()) {
          errors.newPassword = "Mật khẩu không được để trống.";
        } else if (pw.length < FIELD_LIMITS.passwordMin) {
          errors.newPassword = `Mật khẩu phải có ít nhất ${FIELD_LIMITS.passwordMin} ký tự.`;
        } else if (pw.length > FIELD_LIMITS.passwordMax) {
          errors.newPassword = `Mật khẩu không được dài quá ${FIELD_LIMITS.passwordMax} ký tự.`;
        }
      } else if (currentMode === "edit" && pw) {
        if (pw.length < FIELD_LIMITS.passwordMin) {
          errors.newPassword = `Mật khẩu mới phải có ít nhất ${FIELD_LIMITS.passwordMin} ký tự.`;
        } else if (pw.length > FIELD_LIMITS.passwordMax) {
          errors.newPassword = `Mật khẩu mới không được dài quá ${FIELD_LIMITS.passwordMax} ký tự.`;
        }
      }

      return errors;
    },
    []
  );

  // Re-validate mỗi khi form/modal thay đổi (add / edit)
  useEffect(() => {
    if (!open || mode === "view") {
      setFieldErrors({});
      return;
    }
    const errors = validateFields(form, mode);
    setFieldErrors(errors);
  }, [open, form, mode, validateFields]);

  const validateForm = () => {
    const errors = validateFields(form, mode);
    setFieldErrors(errors);
    const hasErrors = Object.keys(errors).length > 0;
    if (hasErrors) {
      // Lấy message lỗi đầu tiên để show lên toast
      const firstError = Object.values(errors)[0];
      if (firstError) {
        setErrorMsg(firstError);
      }
      return false;
    }
    return true;
  };

  const submit = async (e) => {
    e.preventDefault();
    if (!validateForm()) {
      return;
    }

    const payloadBase = {
      email: trim(form.email),
      firstName: trim(form.firstName),
      lastName: trim(form.lastName),
      username: trim(form.username) || null,
      phone: trim(form.phone) || null,
      address: trim(form.address) || null,
      status: form.status,
      roleId: form.roleId || null,
    };

    const passwordValue = trim(form.newPassword);
    try {
      if (mode === "add") {
        await usersApi.create({
          ...payloadBase,
          newPassword: passwordValue, // bắt buộc, đã validate
        });
        showSuccess("Thành công", "Đã tạo người dùng mới.");
      } else if (mode === "edit") {
        await usersApi.update(form.userId, {
          userId: form.userId,
          ...payloadBase,
          newPassword: passwordValue === "" ? null : passwordValue,
        });
        showSuccess("Thành công", "Đã cập nhật thông tin người dùng.");
      }
      setOpen(false);
      fetchList(applied);
    } catch (err) {
      const resp = err?.response;
      let msg = "Không lưu được dữ liệu.";
      if (resp?.data) {
        if (typeof resp.data === "string") {
          msg = resp.data;
        } else if (resp.data.message) {
          msg = resp.data.message;
        }
      } else if (err.message) {
        msg = err.message;
      }
      setErrorMsg(msg);
    }
  };

  const toggleDisable = async (u) => {
    const goingDisable = u.status === USER_STATUS.Active;
    const msg = goingDisable
      ? "Disable tài khoản này?"
      : "Reactive (kích hoạt lại) tài khoản này?";
    if (!window.confirm(msg)) return;
    try {
      await usersApi.delete(u.userId);
      showSuccess("Thành công", "Đã thay đổi trạng thái người dùng.");
      fetchList(applied);
    } catch (err) {
      setErrorMsg(err.message || "Không thay đổi được trạng thái người dùng.");
    }
  };

  const hasFormErrors = mode !== "view" && Object.keys(fieldErrors).length > 0;

  return (
    <>
      <div className="kt-admin wrap">
        <main className="main">
          <section className="card filters" aria-labelledby="title">
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
              }}
            >
              <h2 id="title" style={{ margin: 0 }}>
                Quản lý người dùng
              </h2>
              <PermissionGuard moduleCode={MODULE_CODES.USER_MANAGER} permissionCode={PERMISSION_CODES.CREATE}>
                <button className="btn primary" onClick={openAdd}>
                  + Thêm người dùng
                </button>
              </PermissionGuard>
            </div>

            <form className="row" style={{ marginTop: 10 }} onSubmit={onApply}>
              <input
                className="input"
                placeholder="Tìm tên, email, username, điện thoại…"
                value={uiFilters.q}
                onChange={(e) =>
                  setUiFilters({ ...uiFilters, q: e.target.value })
                }
              />
              <select
                value={uiFilters.roleId}
                onChange={(e) =>
                  setUiFilters({ ...uiFilters, roleId: e.target.value })
                }
              >
                <option value="">Tất cả vai trò</option>
                {roles.map((r) => (
                  <option key={r.roleId} value={r.roleId}>
                    {r.name}
                  </option>
                ))}
              </select>
              <select
                value={uiFilters.status}
                onChange={(e) =>
                  setUiFilters({ ...uiFilters, status: e.target.value })
                }
              >
                {USER_STATUS_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
              <div
                style={{
                  display: "flex",
                  gap: 8,
                  justifyContent: "flex-end",
                }}
              >
                <button className="btn primary" type="submit">
                  Áp dụng
                </button>
                <button className="btn" type="button" onClick={onReset}>
                  Reset
                </button>
              </div>
            </form>
          </section>

          <section className="card" style={{ padding: 14 }}>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
              }}
            >
              <h3 style={{ margin: 0 }}>Danh sách người dùng</h3>
              <small className="muted">
                {data.totalItems} mục · phân trang
              </small>
            </div>

            <div style={{ overflow: "auto", marginTop: 8 }}>
              <table
                className="table"
                aria-label="Bảng quản lý người dùng"
                id="userTable"
              >
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Họ tên</th>
                    <th>Email</th>
                    <th>Vai trò</th>
                    <th>Lần đăng nhập cuối</th>
                    <th>Trạng thái</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {!loading && data.items?.length === 0 && (
                    <tr>
                      <td colSpan="7" style={{ padding: 14, textAlign: "center" }}>
                        Không có dữ liệu
                      </td>
                    </tr>
                  )}
                  {loading && (
                    <tr>
                      <td colSpan="7" style={{ padding: 14, textAlign: "center" }}>
                        Đang tải…
                      </td>
                    </tr>
                  )}
                  {data.items?.map((u, idx) => (
                    <tr key={u.userId}>
                      <td>
                        {(applied.page - 1) * applied.pageSize + idx + 1}
                      </td>
                      <td>{u.fullName}</td>
                      <td>{u.email}</td>
                      <td>{u.roleName || "-"}</td>
                      <td>
                        {u.lastLoginAt
                          ? new Date(u.lastLoginAt).toLocaleString()
                          : "-"}
                      </td>
                      <td>
                        <span
                          className={`status ${
                            u.status === USER_STATUS.Active ? "s-ok" : "s-bad"
                          }`}
                        >
                          {u.status}
                        </span>
                      </td>
                      <td
                        className="actions-td"
                        style={{ display: "flex", gap: 6 }}
                      >
                        <PermissionGuard moduleCode={MODULE_CODES.USER_MANAGER} permissionCode={PERMISSION_CODES.VIEW_DETAIL}>
                          <button
                            className="btn"
                            onClick={() => openViewOrEdit(u.userId, "view")}
                            title="Xem"
                          >
                            👁️
                          </button>
                        </PermissionGuard>
                        <PermissionGuard moduleCode={MODULE_CODES.USER_MANAGER} permissionCode={PERMISSION_CODES.EDIT}>
                          <button
                            className="btn"
                            onClick={() => openViewOrEdit(u.userId, "edit")}
                            title="Sửa"
                          >
                            ✏️
                          </button>
                        </PermissionGuard>
                        <PermissionGuard moduleCode={MODULE_CODES.USER_MANAGER} permissionCode={PERMISSION_CODES.DELETE}>
                          <button
                            className="btn"
                            onClick={() => toggleDisable(u)}
                            title={
                              u.status === USER_STATUS.Active
                                ? "Disable"
                                : "Reactive"
                            }
                          >
                            {u.status === USER_STATUS.Active ? "🚫" : "✅"}
                          </button>
                        </PermissionGuard>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div
              style={{
                display: "flex",
                justifyContent: "center",
                gap: 6,
                marginTop: 12,
              }}
            >
              <button className="btn" onClick={() => gotoPage(applied.page - 1)}>
                «
              </button>
              <span style={{ padding: 8 }}>
                Trang {applied.page}/{totalPages}
              </span>
              <button className="btn" onClick={() => gotoPage(applied.page + 1)}>
                »
              </button>
            </div>
          </section>
        </main>

        {/* Modal */}
        {open && (
          <div
            className="modal-overlay active"
            onClick={() => setOpen(false)}
          >
            <div className="modal" onClick={(e) => e.stopPropagation()}>
              <div className="modal-header">
                <h3 className="modal-title">
                  {mode === "add"
                    ? "Thêm người dùng"
                    : mode === "edit"
                    ? "Cập nhật người dùng"
                    : "Chi tiết người dùng"}
                </h3>
                <button className="modal-close" onClick={() => setOpen(false)}>
                  ×
                </button>
              </div>

              <form onSubmit={submit} className="modal-body">
                <div className="form-grid">
                  <div className="form-group">
                    <label className="form-label">
                      Họ <span style={{ color: "red" }}>*</span>
                    </label>
                    <input
                      type="text"
                      className={`form-input ${
                        fieldErrors.firstName ? "error" : ""
                      }`}
                      value={form.firstName}
                      onChange={(e) =>
                        setForm({ ...form, firstName: e.target.value })
                      }
                      required
                      disabled={mode === "view"}
                      placeholder="Nhập họ"
                      maxLength={FIELD_LIMITS.firstName}
                    />
                    {fieldErrors.firstName && (
                      <div className="error-message">{fieldErrors.firstName}</div>
                    )}
                  </div>

                  <div className="form-group">
                    <label className="form-label">
                      Tên <span style={{ color: "red" }}>*</span>
                    </label>
                    <input
                      type="text"
                      className={`form-input ${
                        fieldErrors.lastName ? "error" : ""
                      }`}
                      value={form.lastName}
                      onChange={(e) =>
                        setForm({ ...form, lastName: e.target.value })
                      }
                      required
                      disabled={mode === "view"}
                      placeholder="Nhập tên"
                      maxLength={FIELD_LIMITS.lastName}
                    />
                    {fieldErrors.lastName && (
                      <div className="error-message">{fieldErrors.lastName}</div>
                    )}
                  </div>

                  <div className="form-group">
                    <label className="form-label">
                      Email <span style={{ color: "red" }}>*</span>
                    </label>
                    <input
                      type="email"
                      className={`form-input ${
                        fieldErrors.email ? "error" : ""
                      }`}
                      value={form.email}
                      onChange={(e) =>
                        setForm({ ...form, email: e.target.value })
                      }
                      required
                      disabled={mode === "view"}
                      placeholder="Nhập email"
                      maxLength={FIELD_LIMITS.email}
                    />
                    {fieldErrors.email && (
                      <div className="error-message">{fieldErrors.email}</div>
                    )}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Username</label>
                    <input
                      type="text"
                      className={`form-input ${
                        fieldErrors.username ? "error" : ""
                      }`}
                      value={form.username}
                      onChange={(e) =>
                        setForm({ ...form, username: e.target.value })
                      }
                      disabled={mode === "view"}
                      placeholder="Để trống sẽ mặc định dùng email"
                      maxLength={FIELD_LIMITS.username}
                    />
                    {fieldErrors.username && (
                      <div className="error-message">{fieldErrors.username}</div>
                    )}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Điện thoại</label>
                    <input
                      type="tel"
                      className={`form-input ${
                        fieldErrors.phone ? "error" : ""
                      }`}
                      value={form.phone}
                      onChange={(e) =>
                        setForm({ ...form, phone: e.target.value })
                      }
                      disabled={mode === "view"}
                      placeholder="Nhập số điện thoại"
                      maxLength={FIELD_LIMITS.phone}
                    />
                    {fieldErrors.phone && (
                      <div className="error-message">{fieldErrors.phone}</div>
                    )}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Địa chỉ</label>
                    <input
                      type="text"
                      className={`form-input ${
                        fieldErrors.address ? "error" : ""
                      }`}
                      value={form.address}
                      onChange={(e) =>
                        setForm({ ...form, address: e.target.value })
                      }
                      disabled={mode === "view"}
                      placeholder="Nhập địa chỉ"
                      maxLength={FIELD_LIMITS.address}
                    />
                    {fieldErrors.address && (
                      <div className="error-message">{fieldErrors.address}</div>
                    )}
                  </div>

                  <div className="form-group">
                    <label className="form-label">
                      Vai trò <span style={{ color: "red" }}>*</span>
                    </label>
                    <select
                      className={`form-input ${
                        fieldErrors.roleId ? "error" : ""
                      }`}
                      value={form.roleId}
                      onChange={(e) =>
                        setForm({ ...form, roleId: e.target.value })
                      }
                      disabled={mode === "view"}
                    >
                      <option value="">-- Chọn vai trò --</option>
                      {roles.map((r) => (
                        <option key={r.roleId} value={r.roleId}>
                          {r.name}
                        </option>
                      ))}
                    </select>
                    {fieldErrors.roleId && (
                      <div className="error-message">{fieldErrors.roleId}</div>
                    )}
                  </div>

                  <div className="form-group">
                    <label className="form-label">Trạng thái</label>
                    <select
                      className="form-input"
                      value={form.status}
                      onChange={(e) =>
                        setForm({ ...form, status: e.target.value })
                      }
                      disabled={mode === "view"}
                    >
                      {Object.values(USER_STATUS).map((s) => (
                        <option key={s} value={s}>
                          {s}
                        </option>
                      ))}
                    </select>
                  </div>

                  {/* Trường mật khẩu:
                      - Chỉ hiển thị cho add / edit.
                      - Add: bắt buộc, label "Mật khẩu".
                      - Edit: tùy chọn, label "Mật khẩu mới (tùy chọn)". */}
                  {mode !== "view" && (
                    <div className="form-group form-group-full">
                      <label className="form-label">
                        {mode === "add" ? (
                          <>
                            Mật khẩu <span style={{ color: "red" }}>*</span>
                          </>
                        ) : (
                          "Mật khẩu mới (tùy chọn)"
                        )}
                      </label>
                      <input
                        type="password"
                        className={`form-input ${
                          fieldErrors.newPassword ? "error" : ""
                        }`}
                        value={form.newPassword}
                        onChange={(e) =>
                          setForm({ ...form, newPassword: e.target.value })
                        }
                        required={mode === "add"}
                        placeholder={
                          mode === "add"
                            ? `Nhập mật khẩu (ít nhất ${FIELD_LIMITS.passwordMin} ký tự)`
                            : "Để trống nếu không thay đổi"
                        }
                        autoComplete="new-password"
                        minLength={FIELD_LIMITS.passwordMin}
                        maxLength={FIELD_LIMITS.passwordMax}
                      />
                      {fieldErrors.newPassword && (
                        <div className="error-message">
                          {fieldErrors.newPassword}
                        </div>
                      )}
                    </div>
                  )}
                </div>

                <div className="modal-footer">
                  <button
                    className="btn"
                    type="button"
                    onClick={() => setOpen(false)}
                  >
                    Hủy
                  </button>
                  {mode !== "view" && (
                    <button
                      className="btn primary"
                      type="submit"
                      disabled={hasFormErrors}
                    >
                      Lưu
                    </button>
                  )}
                </div>
              </form>
            </div>
          </div>
        )}
      </div>

      <ToastContainer toasts={toasts} removeToast={removeToast} />
      <ErrorDialog
        message={errorMsg}
        onClose={() => setErrorMsg("")}
        showError={showError}
      />
    </>
  );
}
