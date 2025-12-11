import React, { useCallback, useEffect, useMemo, useState } from "react";
import ImageUploader from "../../components/ImageUploader/ImageUploader";
import useToast from "../../hooks/useToast";
import ToastContainer from "../../components/Toast/ToastContainer";
import profileService from "../../services/profile";
import "./AdminProfilePage.css";

const unwrap = (payload) =>
  payload?.data !== undefined ? payload.data : payload;

const AdminProfilePage = () => {
  const storedUser = useMemo(() => {
    try {
      return JSON.parse(localStorage.getItem("user")) || null;
    } catch {
      return null;
    }
  }, []);

  const { toasts, showSuccess, showError, removeToast, confirmDialog } =
    useToast();

  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [savingProfile, setSavingProfile] = useState(false);
  const [savingPassword, setSavingPassword] = useState(false);
  const [avatarUploading, setAvatarUploading] = useState(false);

  const [fullName, setFullName] = useState("");
  const [avatarUrl, setAvatarUrl] = useState("");
  const [passwordForm, setPasswordForm] = useState({
    currentPassword: "",
    newPassword: "",
    confirmPassword: "",
  });

  const loadProfile = useCallback(async () => {
    setLoading(true);
    try {
      const response = await profileService.getAdminProfile();
      const data = unwrap(response);
      setProfile(data);
      setFullName(
        data?.fullName ||
          data?.displayName ||
          data?.username ||
          data?.userName ||
          ""
      );
      setAvatarUrl(
        data?.avatarUrl ||
          data?.avatar ||
          data?.avatarURL ||
          data?.avatarUrlProfile ||
          ""
      );
    } catch (error) {
      const message =
        error?.response?.data?.message ||
        error?.message ||
        "Không thể tải hồ sơ.";
      showError("Hồ sơ", message);
    } finally {
      setLoading(false);
    }
  }, [showError]);

  useEffect(() => {
    loadProfile();
  }, [loadProfile]);

  const handleSaveProfile = async (event) => {
    event?.preventDefault();
    setSavingProfile(true);
    try {
      const payload = { fullName, avatarUrl };
      const response = await profileService.updateAdminProfile(payload);
      const updated = unwrap(response) || {};
      setProfile((prev) => ({
        ...(prev || {}),
        ...updated,
        fullName: fullName,
        avatarUrl: avatarUrl,
      }));
      try {
        const cached = localStorage.getItem("user");
        if (cached) {
          const parsed = JSON.parse(cached);
          const next = { ...parsed, fullName, avatarUrl };
          localStorage.setItem("user", JSON.stringify(next));
        }
      } catch (error) {
        console.warn("Không thể cập nhật cache user", error);
      }
      window.dispatchEvent(new Event("profile-updated"));
      showSuccess("Thành công", "Cập nhật thông tin cá nhân thành công.");
    } catch (error) {
      const message =
        error?.response?.data?.message ||
        error?.message ||
        "Không thể cập nhật hồ sơ.";
      showError("Hồ sơ", message);
    } finally {
      setSavingProfile(false);
    }
  };

  const handlePasswordInput = (event) => {
    const { name, value } = event.target;
    setPasswordForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleSavePassword = async (event) => {
    event.preventDefault();
    if (!passwordForm.currentPassword || !passwordForm.newPassword) {
      showError("Mật khẩu", "Vui lòng nhập đầy đủ thông tin.");
      return;
    }
    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      showError("Mật khẩu", "Mật khẩu xác nhận không khớp.");
      return;
    }

    setSavingPassword(true);
    try {
      await profileService.changePassword({
        currentPassword: passwordForm.currentPassword,
        newPassword: passwordForm.newPassword,
        confirmPassword: passwordForm.confirmPassword,
      });

      setPasswordForm({
        currentPassword: "",
        newPassword: "",
        confirmPassword: "",
      });
      showSuccess("Thành công", "Đổi mật khẩu thành công.");
    } catch (error) {
      const message =
        error?.response?.data?.message ||
        error?.message ||
        "Không thể đổi mật khẩu.";
      showError("Mật khẩu", message);
    } finally {
      setSavingPassword(false);
    }
  };

  const roleLabel = useMemo(() => {
    if (profile?.role?.name) return profile.role.name;
    if (storedUser?.role) return storedUser.role;
    if (Array.isArray(storedUser?.roles) && storedUser.roles.length) {
      return storedUser.roles.join(", ");
    }
    return "Staff";
  }, [profile?.role?.name, storedUser?.role, storedUser?.roles]);

  return (
    <div className="admin-profile-page">
      <div className="admin-profile-header">
        <div>
          <h1 className="admin-profile-title">Hồ sơ tài khoản</h1>
          <p className="admin-profile-subtitle">
            Quản lý tên hiển thị, avatar và mật khẩu.
          </p>
        </div>
        <div className="admin-profile-chip">{roleLabel}</div>
      </div>

      {loading ? (
        <div className="admin-profile-card">Loading...</div>
      ) : (
        <div className="admin-profile-grid">
          <section className="admin-profile-card admin-profile-basics">
            <div className="admin-profile-section-header">
              <div>
                <h3>Thông tin cá nhân</h3>
                <p>Cập nhật avatar và tên hiển thị.</p>
              </div>
              <button
                type="button"
                className="admin-profile-btn primary"
                onClick={handleSaveProfile}
                disabled={savingProfile || avatarUploading}
              >
                {savingProfile ? "Đang lưu..." : "Lưu thay đổi"}
              </button>
            </div>

            <div className="admin-profile-form">
              <label className="admin-profile-label" htmlFor="fullName">
                Họ và tên hiển thị
              </label>
              <input
                id="fullName"
                name="fullName"
                className="admin-profile-input"
                placeholder="Nhập tên hiển thị trong giao diện admin"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
              />
              <div className="admin-profile-hint">
                Hiển thị trên header và nhật ký hoạt động.
              </div>
            </div>

            <div className="admin-profile-basics-grid">
              <div>
                <label className="admin-profile-label">Ảnh đại diện</label>
                <ImageUploader
                  value={avatarUrl}
                  onChange={(url) => setAvatarUrl(url || "")}
                  onError={(msg) => showError("Upload", msg)}
                  onUploadingChange={setAvatarUploading}
                  helperText="Kéo/thả ảnh, click hoặc dán URL hình ảnh."
                  height={100}
                />
              </div>
            </div>
          </section>

          <section className="admin-profile-card">
            <div className="admin-profile-section-header">
              <div>
                <h3>Đổi mật khẩu</h3>
                <p>Dùng mật khẩu mạnh để giữ quyền truy cập admin an toàn.</p>
              </div>
            </div>

            <form
              className="admin-profile-form-grid"
              onSubmit={handleSavePassword}
            >
              <div className="admin-profile-field">
                <label
                  className="admin-profile-label"
                  htmlFor="currentPassword"
                >
                  Mật khẩu hiện tại
                </label>
                <input
                  id="currentPassword"
                  type="password"
                  name="currentPassword"
                  className="admin-profile-input"
                  placeholder="Nhập mật khẩu hiện tại"
                  value={passwordForm.currentPassword}
                  onChange={handlePasswordInput}
                />
              </div>
              <div className="admin-profile-field">
                <label className="admin-profile-label" htmlFor="newPassword">
                  Mật khẩu mới
                </label>
                <input
                  id="newPassword"
                  type="password"
                  name="newPassword"
                  className="admin-profile-input"
                  placeholder="Ít nhất 8 ký tự, chứa chữ cái và số"
                  value={passwordForm.newPassword}
                  onChange={handlePasswordInput}
                />
              </div>
              <div className="admin-profile-field admin-profile-field-inline">
                <label
                  className="admin-profile-label"
                  htmlFor="confirmPassword"
                >
                  Xác nhận mật khẩu mới
                </label>
                <div className="admin-profile-inline-actions">
                  <input
                    id="confirmPassword"
                    type="password"
                    name="confirmPassword"
                    className="admin-profile-input"
                    placeholder="Nhập lại mật khẩu mới"
                    value={passwordForm.confirmPassword}
                    onChange={handlePasswordInput}
                  />
                  <button
                    type="submit"
                    className="admin-profile-btn primary"
                    disabled={savingPassword}
                  >
                    {savingPassword ? "Đang cập nhật..." : "Đổi mật khẩu"}
                  </button>
                </div>
              </div>
            </form>
          </section>
        </div>
      )}

      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />
    </div>
  );
};

export default AdminProfilePage;
