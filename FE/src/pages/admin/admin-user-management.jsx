/**
 * File: admin-user-management.jsx
 * Purpose: React page for managing users in Keytietkiem admin.
 * Notes (update):
 *  - BỎ passwordPlain và toggle xem mật khẩu.
 *  - THÊM input `username` (tạo/sửa). Nếu bỏ trống sẽ mặc định dùng email.
 *  - Không thay đổi layout/flow. Chỉ thêm/bớt field theo API mới.
 */
import React, { useEffect, useMemo, useState, useCallback } from "react";
import "../../styles/admin-user-management.css";
import { usersApi } from "../../api/usersApi";
import { USER_STATUS, USER_STATUS_OPTIONS } from "../../constants/userStatus";
import ToastContainer from "../../components/Toast/ToastContainer";
import useToast from "../../hooks/useToast";

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

export default function AdminUserManagement() {
  const { toasts, showSuccess, showError, showWarning, removeToast } = useToast();

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
    username: "",      // NEW
    phone: "",
    address: "",
    status: USER_STATUS.Active,
    roleId: "",
    newPassword: "",
    hasAccount: false,
  });

  const totalPages = useMemo(
    () => Math.max(1, Math.ceil((data.totalItems || 0) / (applied.pageSize || 10))),
    [data, applied.pageSize]
  );

  const fetchRoles = async () => {
    try {
      const res = await usersApi.roles();
      setRoles((res || []).filter(r => !(r.name || "").toLowerCase().includes("admin")));
    } catch (err) {
      setErrorMsg(err.message || "Không tải được danh sách vai trò.");
    }
  };

  const fetchList = useCallback(async (take = applied) => {
    setLoading(true);
    try {
      const res = await usersApi.list(take);
      const filtered = {
        ...res,
        items: (res?.items || []).filter(x => !((x.roleName || "").toLowerCase().includes("admin")))
      };
      setData(filtered || { items: [], totalItems: 0, page: take.page, pageSize: take.pageSize });
    } catch (err) {
      setErrorMsg(err.message || "Không tải được danh sách người dùng.");
      setData(prev => ({ ...prev, items: [] }));
    } finally {
      setLoading(false);
    }
  }, [applied]);

  useEffect(() => { fetchRoles(); }, []);
  useEffect(() => { fetchList(applied); }, [
    applied.page, applied.pageSize, applied.sortBy, applied.sortDir,
    applied.q, applied.roleId, applied.status, fetchList
  ]);

  const onApply = (e) => {
    e.preventDefault();
    setApplied(prev => ({ ...prev, ...uiFilters, page: 1 }));
  };

  const onReset = () => {
    setUiFilters({ ...initialFilters });
    setApplied({ ...initialFilters });
  };

  const gotoPage = (p) => setApplied(prev => ({ ...prev, page: Math.max(1, Math.min(totalPages, p)) }));

  const openAdd = () => {
    setMode("add");
    setForm({
      userId: "",
      firstName: "",
      lastName: "",
      email: "",
      username: "", // NEW
      phone: "",
      address: "",
      status: USER_STATUS.Active,
      roleId: "",
      newPassword: "",
      hasAccount: false,
    });
    setOpen(true);
  };

  const openViewOrEdit = async (id, m) => {
    try {
      const u = await usersApi.get(id);
      setMode(m);
      setForm({
        userId: u.userId,
        firstName: u.firstName,
        lastName: u.lastName,
        email: u.email,
        username: u.username || "", // NEW
        phone: u.phone || "",
        address: u.address || "",
        status: u.status,
        roleId: u.roleId || "",
        newPassword: "",
        hasAccount: !!u.hasAccount,
      });
      setOpen(true);
    } catch (err) {
      setErrorMsg(err.message || "Không lấy được thông tin người dùng.");
    }
  };

  const submit = async (e) => {
    e.preventDefault();
    try {
      if (mode === "add") {
        if (!form.roleId) { setErrorMsg("Vui lòng chọn vai trò."); return; }
        await usersApi.create({
          email: form.email,
          firstName: form.firstName,
          lastName: form.lastName,
          username: form.username || null, // NEW
          phone: form.phone,
          address: form.address,
          status: form.status,
          roleId: form.roleId,
          newPassword: form.newPassword || null, // chỉ gửi khi có
        });
      } else if (mode === "edit") {
        await usersApi.update(form.userId, {
          userId: form.userId,
          email: form.email,
          firstName: form.firstName,
          lastName: form.lastName,
          username: form.username || null, // NEW
          phone: form.phone,
          address: form.address,
          status: form.status,
          roleId: form.roleId || null,
          newPassword: form.newPassword || null, // chỉ gửi khi muốn đổi
        });
      }
      setOpen(false);
      fetchList(applied);
    } catch (err) {
      setErrorMsg(err.message || "Không lưu được dữ liệu.");
    }
  };

  const toggleDisable = async (u) => {
    const goingDisable = u.status === USER_STATUS.Active;
    const msg = goingDisable ? "Disable tài khoản này?" : "Reactive (kích hoạt lại) tài khoản này?";
    if (!window.confirm(msg)) return;
    try {
      await usersApi.delete(u.userId);
      fetchList(applied);
    } catch (err) {
      setErrorMsg(err.message || "Không thay đổi được trạng thái người dùng.");
    }
  };

  return (
    <>
      <div className="kt-admin wrap">
        <main className="main">
          <section className="card filters" aria-labelledby="title">
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <h2 id="title" style={{ margin: 0 }}>Quản lý người dùng</h2>
              <button className="btn primary" onClick={openAdd}>+ Thêm người dùng</button>
            </div>

            <form className="row" style={{ marginTop: 10 }} onSubmit={onApply}>
              <input
                className="input"
                placeholder="Tìm tên, email, username, điện thoại…"
                value={uiFilters.q}
                onChange={(e) => setUiFilters({ ...uiFilters, q: e.target.value })}
              />
              <select value={uiFilters.roleId} onChange={(e) => setUiFilters({ ...uiFilters, roleId: e.target.value })}>
                <option value="">Tất cả vai trò</option>
                {roles.map(r => <option key={r.roleId} value={r.roleId}>{r.name}</option>)}
              </select>
              <select value={uiFilters.status} onChange={(e) => setUiFilters({ ...uiFilters, status: e.target.value })}>
                {USER_STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
              <div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
                <button className="btn primary" type="submit">Áp dụng</button>
                <button className="btn" type="button" onClick={onReset}>Reset</button>
              </div>
            </form>
          </section>

          <section className="card" style={{ padding: 14 }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <h3 style={{ margin: 0 }}>Danh sách người dùng</h3>
              <small className="muted">{data.totalItems} mục · phân trang</small>
            </div>

            <div style={{ overflow: "auto", marginTop: 8 }}>
              <table className="table" aria-label="Bảng quản lý người dùng" id="userTable">
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
                    <tr><td colSpan="7" style={{ padding: 14, textAlign: "center" }}>Không có dữ liệu</td></tr>
                  )}
                  {loading && (
                    <tr><td colSpan="7" style={{ padding: 14, textAlign: "center" }}>Đang tải…</td></tr>
                  )}
                  {data.items?.map((u, idx) => (
                    <tr key={u.userId}>
                      <td>{(applied.page - 1) * applied.pageSize + idx + 1}</td>
                      <td>{u.fullName}</td>
                      <td>{u.email}</td>
                      <td>{u.roleName || "-"}</td>
                      <td>{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : "-"}</td>
                      <td>
                        <span className={`status ${u.status === USER_STATUS.Active ? "s-ok" : "s-bad"}`}>
                          {u.status}
                        </span>
                      </td>
                      <td className="actions-td" style={{ display: "flex", gap: 6 }}>
                        <button className="btn" onClick={() => openViewOrEdit(u.userId, "view")} title="Xem">👁️</button>
                        <button className="btn" onClick={() => openViewOrEdit(u.userId, "edit")} title="Sửa">✏️</button>
                        <button
                          className="btn"
                          onClick={() => toggleDisable(u)}
                          title={u.status === USER_STATUS.Active ? "Disable" : "Reactive"}
                        >
                          {u.status === USER_STATUS.Active ? "🚫" : "✅"}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div style={{ display: "flex", justifyContent: "center", gap: 6, marginTop: 12 }}>
              <button className="btn" onClick={() => gotoPage(applied.page - 1)}>«</button>
              <span style={{ padding: 8 }}>Trang {applied.page}/{totalPages}</span>
              <button className="btn" onClick={() => gotoPage(applied.page + 1)}>»</button>
            </div>
          </section>
        </main>

        {/* Modal */}
        {open && (
          <div className="modal-overlay active" onClick={() => setOpen(false)}>
            <div className="modal" onClick={(e) => e.stopPropagation()}>
              <div className="modal-header">
                <h3 className="modal-title">
                  {mode === "add" ? "Thêm người dùng" : mode === "edit" ? "Cập nhật người dùng" : "Chi tiết người dùng"}
                </h3>
                <button className="modal-close" onClick={() => setOpen(false)}>
                  ×
                </button>
              </div>

              <form onSubmit={submit} className="modal-body">
                <div className="form-grid">
                  <div className="form-group">
                    <label className="form-label">Họ <span style={{ color: 'red' }}>*</span></label>
                    <input
                      type="text"
                      className="form-input"
                      value={form.firstName}
                      onChange={e => setForm({ ...form, firstName: e.target.value })}
                      required
                      disabled={mode === "view"}
                      placeholder="Nhập họ"
                    />
                  </div>

                  <div className="form-group">
                    <label className="form-label">Tên <span style={{ color: 'red' }}>*</span></label>
                    <input
                      type="text"
                      className="form-input"
                      value={form.lastName}
                      onChange={e => setForm({ ...form, lastName: e.target.value })}
                      required
                      disabled={mode === "view"}
                      placeholder="Nhập tên"
                    />
                  </div>

                  <div className="form-group">
                    <label className="form-label">Email <span style={{ color: 'red' }}>*</span></label>
                    <input
                      type="email"
                      className="form-input"
                      value={form.email}
                      onChange={e => setForm({ ...form, email: e.target.value })}
                      required
                      disabled={mode === "view"}
                      placeholder="Nhập email"
                    />
                  </div>

                  <div className="form-group">
                    <label className="form-label">Username</label>
                    <input
                      type="text"
                      className="form-input"
                      value={form.username}
                      onChange={e => setForm({ ...form, username: e.target.value })}
                      disabled={mode === "view"}
                      placeholder="Để trống sẽ mặc định dùng email"
                    />
                  </div>

                  <div className="form-group">
                    <label className="form-label">Điện thoại</label>
                    <input
                      type="tel"
                      className="form-input"
                      value={form.phone}
                      onChange={e => setForm({ ...form, phone: e.target.value })}
                      disabled={mode === "view"}
                      placeholder="Nhập số điện thoại"
                    />
                  </div>

                  <div className="form-group">
                    <label className="form-label">Địa chỉ</label>
                    <input
                      type="text"
                      className="form-input"
                      value={form.address}
                      onChange={e => setForm({ ...form, address: e.target.value })}
                      disabled={mode === "view"}
                      placeholder="Nhập địa chỉ"
                    />
                  </div>

                  <div className="form-group">
                    <label className="form-label">Vai trò <span style={{ color: 'red' }}>*</span></label>
                    <select
                      className="form-input"
                      value={form.roleId}
                      onChange={(e) => setForm({ ...form, roleId: e.target.value })}
                      disabled={mode === "view"}
                    >
                      <option value="">-- Chọn vai trò --</option>
                      {roles.map(r => (
                        <option key={r.roleId} value={r.roleId}>{r.name}</option>
                      ))}
                    </select>
                  </div>

                  <div className="form-group">
                    <label className="form-label">Trạng thái</label>
                    <select
                      className="form-input"
                      value={form.status}
                      onChange={(e) => setForm({ ...form, status: e.target.value })}
                      disabled={mode === "view"}
                    >
                      {Object.values(USER_STATUS).map(s => (
                        <option key={s} value={s}>{s}</option>
                      ))}
                    </select>
                  </div>

                  <div className="form-group form-group-full">
                    <label className="form-label">Mật khẩu mới (tùy chọn)</label>
                    <input
                      type="password"
                      className="form-input"
                      value={form.newPassword}
                      onChange={e => setForm({ ...form, newPassword: e.target.value })}
                      disabled={mode === "view"}
                      placeholder="Để trống nếu không thay đổi"
                      autoComplete="new-password"
                    />
                  </div>
                </div>

                <div className="modal-footer">
                  <button className="btn" type="button" onClick={() => setOpen(false)}>Hủy</button>
                  {mode !== "view" && <button className="btn primary" type="submit">Lưu</button>}
                </div>
              </form>
            </div>
          </div>
        )}
      </div>

      <ToastContainer toasts={toasts} removeToast={removeToast} />
      <ErrorDialog message={errorMsg} onClose={() => setErrorMsg("")} showError={showError} />
    </>
  );
}
