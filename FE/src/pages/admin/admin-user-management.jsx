/**
 * File: admin-user-management.jsx
 * Purpose: React page for managing users in Keytietkiem admin.
 * Notes (update):
 *  - BỎ passwordPlain và toggle xem mật khẩu.
 *  - THÊM input `username` (tạo/sửa). Nếu bỏ trống sẽ mặc định dùng email.
 *  - Mức độ ưu tiên (Users.SupportPriorityLevel):
 *      + List: hiển thị Mức độ ưu tiên.
 *      + View: hiển thị Mức độ ưu tiên hiện tại (read-only).
 *      + Create/Update: KHÔNG cho chỉnh, priority chỉ dựa vào gói + loyalty.
 *  - Gói hỗ trợ:
 *      + Create: cho phép gán 1 gói trả phí (không có gói 0 mặc định).
 *      + Edit:
 *          * Giữ nguyên gói hiện tại.
 *          * Xóa gói hỗ trợ (về trạng thái không có gói).
 *          * Chọn gói mới (kể cả trùng planId với gói hiện tại) → BE sẽ tạo subscription mới, làm mới ngày tháng.
 *  - THÊM filter:
 *      + Filter Mức độ ưu tiên.
 *      + Filter Người dùng tạm thời (isTemp), mặc định = false → chỉ người dùng thật.
 *  - Màn "Chi tiết người dùng" KHÔNG hiển thị mật khẩu (vì mật khẩu băm 1 chiều).
 *  - Không cho xem/sửa/disable user tạm thời (isTemp = true).
 */

import React, { useEffect, useMemo, useState, useCallback } from "react";
import "../../styles/admin-user-management.css";
import { usersApi } from "../../api/usersApi";
import { USER_STATUS, USER_STATUS_OPTIONS } from "../../constants/userStatus";
import ToastContainer from "../../components/Toast/ToastContainer";
import useToast from "../../hooks/useToast";
import axiosClient from "../../api/axiosClient";
import { usePermission } from "../../hooks/usePermission";
import { MODULE_CODES } from "../../constants/accessControl";

function ErrorDialog({ message, onClose, showError }) {
  // Đẩy lỗi chung lên toast
  if (message) {
    showError("Thông báo lỗi", message);
    if (onClose) onClose();
  }
  return null;
}

const initialFilters = {
  q: "",
  roleId: "",
  status: "",
  supportPriorityLevel: "", // filter mức độ ưu tiên
  isTemp: false, // mặc định xem người dùng thật
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

// Helper format tiền
const formatCurrency = (value) => {
  if (value === null || value === undefined) return "0";
  try {
    return Number(value).toLocaleString("vi-VN");
  } catch {
    return String(value);
  }
};

export default function AdminUserManagement() {
  const { toasts, showSuccess, showError, removeToast } = useToast();

  // Check permissions
  const { hasPermission: canViewList, loading: permissionLoading } = usePermission(
    MODULE_CODES.USER_MANAGER,
    "VIEW_LIST"
  );
  const { hasPermission: canViewDetail } = usePermission(MODULE_CODES.USER_MANAGER, "VIEW_DETAIL");
  const { hasPermission: canCreate } = usePermission(MODULE_CODES.USER_MANAGER, "CREATE");
  const { hasPermission: canEdit } = usePermission(MODULE_CODES.USER_MANAGER, "EDIT");
  const { hasPermission: canDelete } = usePermission(MODULE_CODES.USER_MANAGER, "DELETE");

  // Global network error handler
  const networkErrorShownRef = React.useRef(false);
  // Global permission error handler - only show one toast for permission errors
  const permissionErrorShownRef = React.useRef(false);
  React.useEffect(() => {
    networkErrorShownRef.current = false;
    permissionErrorShownRef.current = false;
  }, []);

  const [uiFilters, setUiFilters] = useState(initialFilters);
  const [applied, setApplied] = useState(initialFilters);

  const [data, setData] = useState({
    items: [],
    totalItems: 0,
    page: 1,
    pageSize: 10,
  });
  const [roles, setRoles] = useState([]);
  const [supportPlans, setSupportPlans] = useState([]);
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

    // ==== Priority + Support Plan (form state) ====
    supportPriorityLevel: "0", // chỉ hiển thị read-only ở view
    isTemp: false,

    activeSupportPlanId: null,
    activeSupportPlanName: "",
    activeSupportPlanStartedAt: null,
    activeSupportPlanExpiresAt: null,
    activeSupportPlanStatus: "",

    // Gói hỗ trợ muốn gán / đổi (gửi lên BE)
    selectedSupportPlanId: "", // string; "" = tuỳ theo mode (add: không gán, edit: giữ nguyên)

    // Tổng số tiền đã tiêu
    totalProductSpend: 0,
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
      setRoles(
        (res || []).filter((r) => !((r.name || "").toLowerCase().includes("admin")))
      );
    } catch (err) {
      setErrorMsg(err.message || "Không tải được danh sách vai trò.");
    }
  };

  const fetchSupportPlans = async () => {
    try {
      // Lấy danh sách gói hỗ trợ đang active cho dropdown
      const res = await axiosClient.get("/supportplans/active");
      setSupportPlans(res || []);
    } catch (err) {
      setErrorMsg(err.message || "Không tải được danh sách gói hỗ trợ.");
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
        setData(
          filtered || {
            items: [],
            totalItems: 0,
            page: take.page,
            pageSize: take.pageSize,
          }
        );
      } catch (err) {
        setErrorMsg(err.message || "Không tải được danh sách người dùng.");
        setData((prev) => ({ ...prev, items: [] }));

        // Handle network errors globally - only show one toast
        if (err.isNetworkError || err.message === "Lỗi kết nối đến máy chủ") {
          if (!networkErrorShownRef.current) {
            networkErrorShownRef.current = true;
            showError("Lỗi kết nối", "Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.");
          }
        } else {
          // Check if error message contains permission denied - only show once
          const isPermissionError =
            err.message?.includes("không có quyền") ||
            err.message?.includes("quyền truy cập") ||
            err.response?.status === 403;

          if (isPermissionError && !permissionErrorShownRef.current) {
            permissionErrorShownRef.current = true;
            const msg =
              err?.response?.data?.message || err.message || "Bạn không có quyền truy cập chức năng này.";
            showError("Lỗi tải dữ liệu", msg);
          } else if (!isPermissionError) {
            showError("Lỗi", err.message || "Không tải được danh sách người dùng.");
          }
        }
      } finally {
        setLoading(false);
      }
    },
    [applied, showError]
  );

  useEffect(() => {
    fetchRoles();
    fetchSupportPlans();
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
    applied.supportPriorityLevel,
    applied.isTemp,
    fetchList,
  ]);

  const onApply = (e) => {
    e.preventDefault();
    setApplied((prev) => ({
      ...prev,
      ...uiFilters,
      page: 1,
    }));
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
    if (!canCreate) {
      showError("Không có quyền", "Bạn không có quyền tạo người dùng mới.");
      return;
    }
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

      supportPriorityLevel: "0",
      isTemp: false, // admin tạo mới luôn là người dùng thật

      activeSupportPlanId: null,
      activeSupportPlanName: "",
      activeSupportPlanStartedAt: null,
      activeSupportPlanExpiresAt: null,
      activeSupportPlanStatus: "",

      selectedSupportPlanId: "", // add: "" = không gán gói

      totalProductSpend: 0,
    });
    setFieldErrors({});
    setOpen(true);
  };

  const openViewOrEdit = async (id, m) => {
    if (!canViewDetail) {
      showError("Không có quyền", "Bạn không có quyền xem chi tiết và chỉnh sửa người dùng.");
      return;
    }
    try {
      const u = await usersApi.get(id);

      if (u.isTemp) {
        setErrorMsg("Không thể xem / chỉnh sửa người dùng tạm thời. Vui lòng thao tác với người dùng thật.");
        return;
      }

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

        supportPriorityLevel: String(typeof u.supportPriorityLevel === "number" ? u.supportPriorityLevel : 0),
        isTemp: !!u.isTemp,

        activeSupportPlanId: typeof u.activeSupportPlanId === "number" ? u.activeSupportPlanId : null,
        activeSupportPlanName: u.activeSupportPlanName || "",
        activeSupportPlanStartedAt: u.activeSupportPlanStartedAt || null,
        activeSupportPlanExpiresAt: u.activeSupportPlanExpiresAt || null,
        activeSupportPlanStatus: u.activeSupportPlanStatus || "",

        selectedSupportPlanId: "",

        totalProductSpend: typeof u.totalProductSpend === "number" ? u.totalProductSpend : 0,
      });
      setFieldErrors({});
      setOpen(true);
    } catch (err) {
      setErrorMsg(err.message || "Không lấy được thông tin người dùng.");
    }
  };

  const trim = (v) => (v || "").trim();
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  const validateFields = useCallback((currentForm, currentMode) => {
    const errors = {};

    const fn = trim(currentForm.firstName);
    if (!fn) errors.firstName = "Họ không được để trống.";
    else if (fn.length > FIELD_LIMITS.firstName)
      errors.firstName = `Họ tối đa ${FIELD_LIMITS.firstName} ký tự.`;

    const ln = trim(currentForm.lastName);
    if (!ln) errors.lastName = "Tên không được để trống.";
    else if (ln.length > FIELD_LIMITS.lastName)
      errors.lastName = `Tên tối đa ${FIELD_LIMITS.lastName} ký tự.`;

    const email = trim(currentForm.email);
    if (!email) errors.email = "Email không được để trống.";
    else if (email.length > FIELD_LIMITS.email)
      errors.email = `Email tối đa ${FIELD_LIMITS.email} ký tự.`;
    else if (!emailRegex.test(email)) errors.email = "Email không hợp lệ.";

    const username = trim(currentForm.username);
    if (username && username.length > FIELD_LIMITS.username)
      errors.username = `Username tối đa ${FIELD_LIMITS.username} ký tự.`;

    const phone = trim(currentForm.phone);
    if (phone) {
      if (phone.length > FIELD_LIMITS.phone)
        errors.phone = `Điện thoại tối đa ${FIELD_LIMITS.phone} ký tự.`;
      else if (!/^[0-9+\s\-()]+$/.test(phone))
        errors.phone = "Số điện thoại chỉ được chứa số và các ký tự + - ( ) khoảng trắng.";
    }

    const address = trim(currentForm.address);
    if (address && address.length > FIELD_LIMITS.address)
      errors.address = `Địa chỉ tối đa ${FIELD_LIMITS.address} ký tự.`;

    if (!currentForm.roleId) errors.roleId = "Vui lòng chọn vai trò.";

    const pw = currentForm.newPassword || "";
    if (currentMode === "add") {
      if (!pw.trim()) errors.newPassword = "Mật khẩu không được để trống.";
      else if (pw.length < FIELD_LIMITS.passwordMin)
        errors.newPassword = `Mật khẩu phải có ít nhất ${FIELD_LIMITS.passwordMin} ký tự.`;
      else if (pw.length > FIELD_LIMITS.passwordMax)
        errors.newPassword = `Mật khẩu không được dài quá ${FIELD_LIMITS.passwordMax} ký tự.`;
    } else if (currentMode === "edit" && pw) {
      if (pw.length < FIELD_LIMITS.passwordMin)
        errors.newPassword = `Mật khẩu mới phải có ít nhất ${FIELD_LIMITS.passwordMin} ký tự.`;
      else if (pw.length > FIELD_LIMITS.passwordMax)
        errors.newPassword = `Mật khẩu mới không được dài quá ${FIELD_LIMITS.passwordMax} ký tự.`;
    }

    return errors;
  }, []);

  const validateForm = () => {
    const errors = validateFields(form, mode);
    setFieldErrors(errors);

    const hasErrors = Object.keys(errors).length > 0;
    if (hasErrors) {
      const firstError = Object.values(errors)[0];
      if (firstError) setErrorMsg(firstError);
      return false;
    }
    return true;
  };

  const submit = async (e) => {
    e.preventDefault();
    if (mode === "add" && !canCreate) {
      showError("Không có quyền", "Bạn không có quyền tạo người dùng mới.");
      return;
    }
    if (mode === "edit" && !canEdit) {
      showError("Không có quyền", "Bạn không có quyền cập nhật người dùng.");
      return;
    }
    if (!validateForm()) return;

    let activeSupportPlanId;
    if (mode === "add") {
      activeSupportPlanId = form.selectedSupportPlanId ? Number(form.selectedSupportPlanId) : undefined;
    } else if (mode === "edit") {
      if (form.selectedSupportPlanId === "__REMOVE__") activeSupportPlanId = 0;
      else if (form.selectedSupportPlanId) activeSupportPlanId = Number(form.selectedSupportPlanId);
      else activeSupportPlanId = undefined;
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
      ...(activeSupportPlanId !== undefined ? { activeSupportPlanId } : {}),
    };

    const passwordValue = trim(form.newPassword);
    try {
      if (mode === "add") {
        await usersApi.create({ ...payloadBase, newPassword: passwordValue });
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
        if (typeof resp.data === "string") msg = resp.data;
        else if (resp.data.message) msg = resp.data.message;
      } else if (err.message) msg = err.message;
      setErrorMsg(msg);
    }
  };

  const toggleDisable = async (u) => {
    if (!canDelete) {
      showError("Không có quyền", "Bạn không có quyền thay đổi trạng thái người dùng.");
      return;
    }
    if (u.isTemp) {
      setErrorMsg("Không thể thay đổi trạng thái người dùng tạm thời. Vui lòng thao tác với người dùng thật.");
      return;
    }
    const goingDisable = u.status === USER_STATUS.Active;
    const msg = goingDisable ? "Disable tài khoản này?" : "Reactive (kích hoạt lại) tài khoản này?";
    if (!window.confirm(msg)) return;
    try {
      await usersApi.delete(u.userId);
      showSuccess("Thành công", "Đã thay đổi trạng thái người dùng.");
      fetchList(applied);
    } catch (err) {
      setErrorMsg(err.message || "Không thay đổi được trạng thái người dùng.");
    }
  };

  const formatDate = (d) => {
    if (!d) return "-";
    try {
      return new Date(d).toLocaleDateString();
    } catch {
      return "-";
    }
  };

  const paidSupportPlans = useMemo(
    () =>
      (supportPlans || []).filter((p) =>
        typeof p.priorityLevel === "number" ? p.priorityLevel > 0 : true
      ),
    [supportPlans]
  );

  const fromIndex =
    data.totalItems === 0 ? 0 : (applied.page - 1) * applied.pageSize + 1;
  const toIndex = Math.min(data.totalItems || 0, applied.page * applied.pageSize);

  // Show loading while checking permission
  if (permissionLoading) {
    return (
      <div className="ktk-admin-user-mgmt">
        <div className="ktk-um-card" style={{ margin: "0 auto", maxWidth: 1120 }}>
          <div className="ktk-um-cardHeader">
            <h2>Quản lý người dùng</h2>
          </div>
          <div style={{ padding: "20px", textAlign: "center" }}>
            Đang kiểm tra quyền truy cập...
          </div>
        </div>
      </div>
    );
  }

  // Show access denied message if no VIEW_LIST permission
  if (!canViewList) {
    return (
      <div className="ktk-admin-user-mgmt">
        <div className="ktk-um-card" style={{ margin: "0 auto", maxWidth: 1120 }}>
          <div className="ktk-um-cardHeader">
            <h2>Quản lý người dùng</h2>
          </div>
          <div style={{ padding: "20px" }}>
            <h2>Không có quyền truy cập</h2>
            <p>
              Bạn không có quyền xem danh sách người dùng. Vui lòng liên hệ quản trị viên để
              được cấp quyền.
            </p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <>
      <div className="ktk-admin-user-mgmt">
        <div className="ktk-um-card" style={{ margin: "0 auto", maxWidth: 1120 }}>
          {/* Header */}
          <div className="ktk-um-cardHeader">
            <div className="ktk-um-left">
              <h2>Quản lý người dùng</h2>
              <p className="ktk-um-muted">
                Quản lý tài khoản khách hàng / nhân viên, trạng thái, vai trò, mức độ ưu tiên hỗ trợ
                và gói hỗ trợ.
              </p>
            </div>
          </div>

          {/* Filter bar + Add button trên cùng 1 hàng */}
          <div
            className="ktk-um-row"
            style={{
              gap: 10,
              marginTop: 14,
              alignItems: "flex-end",
              flexWrap: "nowrap",
            }}
          >
            <form
              className="ktk-um-row"
              style={{
                flex: 1,
                gap: 10,
                alignItems: "flex-end",
                flexWrap: "wrap",
              }}
              onSubmit={onApply}
            >
              <div className="ktk-um-group" style={{ flex: 1, minWidth: 260 }}>
                <span>Tìm kiếm</span>
                <input
                  className="ktk-um-input"
                  placeholder="Tìm tên, email, username, điện thoại…"
                  value={uiFilters.q}
                  onChange={(e) => setUiFilters({ ...uiFilters, q: e.target.value })}
                />
              </div>

              <div className="ktk-um-group" style={{ width: 180 }}>
                <span>Vai trò</span>
                <select
                  value={uiFilters.roleId}
                  onChange={(e) => setUiFilters({ ...uiFilters, roleId: e.target.value })}
                >
                  <option value="">Tất cả vai trò</option>
                  {roles.map((r) => (
                    <option key={r.roleId} value={r.roleId}>
                      {r.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="ktk-um-group" style={{ width: 180 }}>
                <span>Trạng thái</span>
                <select
                  value={uiFilters.status}
                  onChange={(e) => setUiFilters({ ...uiFilters, status: e.target.value })}
                >
                  {USER_STATUS_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="ktk-um-group" style={{ width: 160 }}>
                <span>Mức độ ưu tiên</span>
                <select
                  value={uiFilters.supportPriorityLevel}
                  onChange={(e) => setUiFilters({ ...uiFilters, supportPriorityLevel: e.target.value })}
                >
                  <option value="">Tất cả</option>
                  <option value="0">0</option>
                  <option value="1">1</option>
                  <option value="2">2</option>
                </select>
              </div>

              <div className="ktk-um-group" style={{ width: 190 }}>
                <span>Loại người dùng</span>
                <select
                  value={uiFilters.isTemp ? "true" : "false"}
                  onChange={(e) => setUiFilters({ ...uiFilters, isTemp: e.target.value === "true" })}
                >
                  <option value="false">Người dùng thật</option>
                  <option value="true">Người dùng tạm thời</option>
                </select>
              </div>

              <div
                className="ktk-um-row"
                style={{
                  gap: 8,
                  alignItems: "flex-end",
                  flexShrink: 0,
                }}
              >
                {loading && <span className="ktk-um-muted">Đang tải…</span>}
                <button className="ktk-um-btn ktk-um-btn--ghost" type="submit" disabled={loading}>
                  Áp dụng
                </button>
                <button
                  className="ktk-um-btn ktk-um-btn--ghost"
                  type="button"
                  onClick={onReset}
                  disabled={loading}
                >
                  Đặt lại
                </button>
              </div>
            </form>

            <button
              type="button"
              className="ktk-um-btn ktk-um-btn--primary"
              style={{ flexShrink: 0, whiteSpace: "nowrap" }}
              onClick={openAdd}
            >
              Thêm người dùng
            </button>
          </div>

          {/* Bảng danh sách */}
          <table className="ktk-um-table" aria-label="Bảng quản lý người dùng" id="userTable">
            <thead>
              <tr>
                <th style={{ width: 56 }}>#</th>
                <th style={{ minWidth: 200 }}>Họ tên</th>
                <th style={{ minWidth: 220 }}>Email</th>
                <th style={{ width: 130 }}>Vai trò</th>
                <th style={{ width: 120 }}>Mức độ ưu tiên</th>
                <th style={{ width: 190 }}>Lần đăng nhập cuối</th>
                <th style={{ width: 100 }}>Trạng thái</th>
                <th style={{ width: 180, textAlign: "right" }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {!loading && data.items?.length === 0 && (
                <tr>
                  <td colSpan="8" style={{ padding: 14, textAlign: "center" }}>
                    Không có dữ liệu
                  </td>
                </tr>
              )}
              {loading && (
                <tr>
                  <td colSpan="8" style={{ padding: 14, textAlign: "center" }}>
                    Đang tải…
                  </td>
                </tr>
              )}
              {data.items?.map((u, idx) => (
                <tr key={u.userId}>
                  <td>{(applied.page - 1) * applied.pageSize + idx + 1}</td>
                  <td>{u.fullName}</td>
                  <td>{u.email}</td>
                  <td>{u.roleName || "-"}</td>
                  <td>
                    <span className="ktk-um-badge ktk-um-badge--purple">
                      Level {u.supportPriorityLevel ?? 0}
                    </span>
                    {u.isTemp && (
                      <span className="ktk-um-badge ktk-um-badge--gray" style={{ marginLeft: 6 }}>
                        Temp
                      </span>
                    )}
                  </td>
                  <td>{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : "-"}</td>
                  <td>
                    <span
                      className={`ktk-um-status ${
                        u.status === USER_STATUS.Active ? "ktk-um-status--ok" : "ktk-um-status--bad"
                      }`}
                    >
                      {u.status}
                    </span>
                  </td>
                  <td className="ktk-um-actionsTd">
                    {u.isTemp ? (
                      <span className="ktk-um-muted" style={{ fontSize: 12 }}>
                        Người dùng tạm thời
                      </span>
                    ) : (
                      <>
                        <button
                          className="ktk-um-btn ktk-um-btn--ghost"
                          onClick={() => openViewOrEdit(u.userId, "view")}
                          title="Xem"
                        >
                          👁️
                        </button>
                        <button
                          className="ktk-um-btn ktk-um-btn--ghost"
                          onClick={() => openViewOrEdit(u.userId, "edit")}
                          title="Sửa"
                        >
                          ✏️
                        </button>
                        <button
                          className="ktk-um-btn ktk-um-btn--ghost"
                          onClick={() => toggleDisable(u)}
                          title={u.status === USER_STATUS.Active ? "Disable" : "Reactive"}
                        >
                          {u.status === USER_STATUS.Active ? "🚫" : "✅"}
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Pager */}
          <div className="ktk-um-pager">
            <div className="ktk-um-pagerLeft">
              <span>
                {data.totalItems} người dùng ·{" "}
                {data.totalItems > 0 ? `Hiển thị ${fromIndex}–${toIndex}` : "Không có bản ghi"}
              </span>
            </div>
            <div className="ktk-um-pagerRight">
              <button
                type="button"
                className="ktk-um-pagerBtn"
                onClick={() => gotoPage(applied.page - 1)}
                disabled={applied.page <= 1}
              >
                «
              </button>
              <span>
                Trang {applied.page}/{totalPages}
              </span>
              <button
                type="button"
                className="ktk-um-pagerBtn"
                onClick={() => gotoPage(applied.page + 1)}
                disabled={applied.page >= totalPages}
              >
                »
              </button>
            </div>
          </div>
        </div>

        {/* Modal */}
        {open && (
          <div className="ktk-um-modalOverlay ktk-um-modalOverlay--active" onClick={() => setOpen(false)}>
            <div className="ktk-um-modal" onClick={(e) => e.stopPropagation()}>
              <div className="ktk-um-modalHeader">
                <h3 className="ktk-um-modalTitle">
                  {mode === "add"
                    ? "Thêm người dùng"
                    : mode === "edit"
                    ? "Cập nhật người dùng"
                    : "Chi tiết người dùng"}
                </h3>
                <button className="ktk-um-modalClose" onClick={() => setOpen(false)}>
                  ×
                </button>
              </div>

              <form onSubmit={submit} className="ktk-um-modalBody">
                <div className="ktk-um-formGrid">
                  <div className="ktk-um-formGroup">
                    <label className="ktk-um-formLabel">
                      Họ <span style={{ color: "red" }}>*</span>
                    </label>
                    <input
                      type="text"
                      className={`ktk-um-formInput ${fieldErrors.firstName ? "ktk-um-isError" : ""}`}
                      value={form.firstName}
                      onChange={(e) => setForm({ ...form, firstName: e.target.value })}
                      required
                      disabled={mode === "view"}
                      placeholder="Nhập họ"
                      maxLength={FIELD_LIMITS.firstName}
                    />
                    {fieldErrors.firstName && (
                      <div className="ktk-um-errorMessage">{fieldErrors.firstName}</div>
                    )}
                  </div>

                  <div className="ktk-um-formGroup">
                    <label className="ktk-um-formLabel">
                      Tên <span style={{ color: "red" }}>*</span>
                    </label>
                    <input
                      type="text"
                      className={`ktk-um-formInput ${fieldErrors.lastName ? "ktk-um-isError" : ""}`}
                      value={form.lastName}
                      onChange={(e) => setForm({ ...form, lastName: e.target.value })}
                      required
                      disabled={mode === "view"}
                      placeholder="Nhập tên"
                      maxLength={FIELD_LIMITS.lastName}
                    />
                    {fieldErrors.lastName && (
                      <div className="ktk-um-errorMessage">{fieldErrors.lastName}</div>
                    )}
                  </div>

                  <div className="ktk-um-formGroup">
                    <label className="ktk-um-formLabel">
                      Email <span style={{ color: "red" }}>*</span>
                    </label>
                    <input
                      type="email"
                      className={`ktk-um-formInput ${fieldErrors.email ? "ktk-um-isError" : ""}`}
                      value={form.email}
                      onChange={(e) => setForm({ ...form, email: e.target.value })}
                      required
                      disabled={mode === "view"}
                      placeholder="Nhập email"
                      maxLength={FIELD_LIMITS.email}
                    />
                    {fieldErrors.email && (
                      <div className="ktk-um-errorMessage">{fieldErrors.email}</div>
                    )}
                  </div>

                  <div className="ktk-um-formGroup">
                    <label className="ktk-um-formLabel">Username</label>
                    <input
                      type="text"
                      className={`ktk-um-formInput ${fieldErrors.username ? "ktk-um-isError" : ""}`}
                      value={form.username}
                      onChange={(e) => setForm({ ...form, username: e.target.value })}
                      disabled={mode === "view"}
                      placeholder="Để trống sẽ mặc định dùng email"
                      maxLength={FIELD_LIMITS.username}
                    />
                    {fieldErrors.username && (
                      <div className="ktk-um-errorMessage">{fieldErrors.username}</div>
                    )}
                  </div>

                  <div className="ktk-um-formGroup">
                    <label className="ktk-um-formLabel">Điện thoại</label>
                    <input
                      type="tel"
                      className={`ktk-um-formInput ${fieldErrors.phone ? "ktk-um-isError" : ""}`}
                      value={form.phone}
                      onChange={(e) => setForm({ ...form, phone: e.target.value })}
                      disabled={mode === "view"}
                      placeholder="Nhập số điện thoại"
                      maxLength={FIELD_LIMITS.phone}
                    />
                    {fieldErrors.phone && (
                      <div className="ktk-um-errorMessage">{fieldErrors.phone}</div>
                    )}
                  </div>

                  <div className="ktk-um-formGroup">
                    <label className="ktk-um-formLabel">Địa chỉ</label>
                    <input
                      type="text"
                      className={`ktk-um-formInput ${fieldErrors.address ? "ktk-um-isError" : ""}`}
                      value={form.address}
                      onChange={(e) => setForm({ ...form, address: e.target.value })}
                      disabled={mode === "view"}
                      placeholder="Nhập địa chỉ"
                      maxLength={FIELD_LIMITS.address}
                    />
                    {fieldErrors.address && (
                      <div className="ktk-um-errorMessage">{fieldErrors.address}</div>
                    )}
                  </div>

                  <div className="ktk-um-formGroup">
                    <label className="ktk-um-formLabel">
                      Vai trò <span style={{ color: "red" }}>*</span>
                    </label>
                    <select
                      className={`ktk-um-formInput ${fieldErrors.roleId ? "ktk-um-isError" : ""}`}
                      value={form.roleId}
                      onChange={(e) => setForm({ ...form, roleId: e.target.value })}
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
                      <div className="ktk-um-errorMessage">{fieldErrors.roleId}</div>
                    )}
                  </div>

                  <div className="ktk-um-formGroup">
                    <label className="ktk-um-formLabel">Trạng thái</label>
                    <select
                      className="ktk-um-formInput"
                      value={form.status}
                      onChange={(e) => setForm({ ...form, status: e.target.value })}
                      disabled={mode === "view"}
                    >
                      {Object.values(USER_STATUS).map((s) => (
                        <option key={s} value={s}>
                          {s}
                        </option>
                      ))}
                    </select>
                  </div>

                  {mode === "view" && (
                    <div className="ktk-um-formGroup">
                      <label className="ktk-um-formLabel">Mức độ ưu tiên hiện tại</label>
                      <input
                        type="text"
                        className="ktk-um-formInput"
                        value={String(form.supportPriorityLevel || "0")}
                        disabled
                      />
                    </div>
                  )}

                  {mode !== "add" && (
                    <div className="ktk-um-formGroup">
                      <label className="ktk-um-formLabel">Người dùng tạm thời</label>
                      <input
                        type="text"
                        className="ktk-um-formInput"
                        value={form.isTemp ? "Có" : "Không"}
                        disabled
                      />
                    </div>
                  )}

                  {mode !== "add" && (
                    <div className="ktk-um-formGroup">
                      <label className="ktk-um-formLabel">Tổng số tiền đã tiêu</label>
                      <input
                        type="text"
                        className="ktk-um-formInput"
                        value={`${formatCurrency(form.totalProductSpend || 0)} đ`}
                        disabled
                      />
                    </div>
                  )}

                  {mode !== "view" && (
                    <div className="ktk-um-formGroup ktk-um-formGroupFull">
                      <label className="ktk-um-formLabel">
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
                        className={`ktk-um-formInput ${fieldErrors.newPassword ? "ktk-um-isError" : ""}`}
                        value={form.newPassword}
                        onChange={(e) => setForm({ ...form, newPassword: e.target.value })}
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
                        <div className="ktk-um-errorMessage">{fieldErrors.newPassword}</div>
                      )}
                    </div>
                  )}

                  <div className="ktk-um-formGroup ktk-um-formGroupFull">
                    <label className="ktk-um-formLabel">Gói hỗ trợ đang đăng ký</label>
                    {form.activeSupportPlanName ? (
                      <div
                        style={{
                          padding: "10px 12px",
                          borderRadius: 8,
                          border: "1px solid var(--border-color)",
                          background: "#f8f9fa",
                          fontSize: 14,
                        }}
                      >
                        <div>
                          <strong>Tên gói:</strong> {form.activeSupportPlanName}
                        </div>
                        <div>
                          <strong>Trạng thái:</strong> {form.activeSupportPlanStatus || "-"}
                        </div>
                        <div>
                          <strong>Ngày bắt đầu:</strong> {formatDate(form.activeSupportPlanStartedAt)}
                        </div>
                        <div>
                          <strong>Hết hạn:</strong> {formatDate(form.activeSupportPlanExpiresAt)}
                        </div>
                      </div>
                    ) : (
                      <div
                        style={{
                          padding: "10px 12px",
                          borderRadius: 8,
                          border: "1px dashed var(--border-color)",
                          color: "var(--text-muted)",
                          fontSize: 14,
                        }}
                      >
                        Chưa có gói hỗ trợ trả phí nào đang active.
                      </div>
                    )}
                  </div>

                  {mode !== "view" && (
                    <div className="ktk-um-formGroup ktk-um-formGroupFull">
                      <label className="ktk-um-formLabel">
                        {mode === "add"
                          ? "Gán gói hỗ trợ (tùy chọn)"
                          : "Chọn gói hỗ trợ mới / xóa gói (tùy chọn)"}
                      </label>
                      <select
                        className="ktk-um-formInput"
                        value={form.selectedSupportPlanId}
                        onChange={(e) => setForm({ ...form, selectedSupportPlanId: e.target.value })}
                      >
                        {mode === "add" ? (
                          <option value="">Không gán gói hỗ trợ (mặc định không có gói)</option>
                        ) : (
                          <>
                            <option value="">Giữ nguyên gói hiện tại</option>
                            <option value="__REMOVE__">Xóa gói hỗ trợ (về trạng thái không có gói)</option>
                          </>
                        )}

                        {paidSupportPlans.map((p) => (
                          <option key={p.supportPlanId} value={String(p.supportPlanId)}>
                            {p.name} (Level {p.priorityLevel}) - {formatCurrency(p.price)}đ
                          </option>
                        ))}
                      </select>

                      {mode === "add" ? (
                        <div className="ktk-um-hintText" style={{ marginTop: 4 }}>
                          Tùy chọn: nếu chọn một gói, hệ thống sẽ tạo subscription mới cho người dùng khi lưu.
                        </div>
                      ) : (
                        <div className="ktk-um-hintText" style={{ marginTop: 4, lineHeight: 1.5 }}>
                          <div>
                            - <strong>Giữ nguyên gói hiện tại</strong>: không thay đổi subscription.
                          </div>
                          <div>
                            - <strong>Xóa gói hỗ trợ</strong>: huỷ subscription hiện tại (người dùng không còn gói).
                          </div>
                          <div>
                            - <strong>Chọn một gói trong danh sách</strong> (kể cả trùng với gói hiện tại): hệ thống
                            sẽ tạo subscription mới và <strong>làm mới thời hạn gói</strong>.
                          </div>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              </form>

              <div className="ktk-um-modalFooter">
                <button
                  type="button"
                  className="ktk-um-btnModal ktk-um-btnModal--secondary"
                  onClick={() => setOpen(false)}
                >
                  Đóng
                </button>
                {mode !== "view" && (
                  <button
                    type="button"
                    className="ktk-um-btnModal ktk-um-btnModal--primary"
                    onClick={submit}
                  >
                    Lưu
                  </button>
                )}
              </div>
            </div>
          </div>
        )}
      </div>

      <ToastContainer toasts={toasts} removeToast={removeToast} />
      <ErrorDialog message={errorMsg} onClose={() => setErrorMsg("")} showError={showError} />
    </>
  );
}
