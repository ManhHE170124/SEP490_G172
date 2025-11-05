import axiosClient from "../api/axiosClient";

export const AuthService = {
  sendOtp: (email) => axiosClient.post("/account/send-otp", { email }),

  verifyOtp: (email, otp) =>
    axiosClient.post("/account/verify-otp", { email, otp }),

  register: (payload) => axiosClient.post("/account/register", payload),

  login: (username, password) =>
    axiosClient.post("/account/login", { username, password }),

  refreshToken: (refreshToken) =>
    axiosClient.post("/account/refresh-token", { refreshToken }),

  changePassword: (currentPassword, newPassword) =>
    axiosClient.post("/account/change-password", {
      currentPassword,
      newPassword,
    }),

  checkUsernameExists: (username) =>
    axiosClient.get(`/account/check-username/${username}`),

  checkEmailExists: (email) => axiosClient.get(`/account/check-email/${email}`),

  forgotPassword: (email) =>
    axiosClient.post("/account/forgot-password", { email }),

  resetPassword: (token, newPassword) =>
    axiosClient.post("/account/reset-password", { token, newPassword }),

  revokeToken: (accessToken, refreshToken) =>
    axiosClient.post("/account/revoke-token", { accessToken, refreshToken }),

  logout: async () => {
    const accessToken = localStorage.getItem("access_token");
    const refreshToken = localStorage.getItem("refresh_token");

    if (accessToken && refreshToken) {
      try {
        await axiosClient.post("/account/revoke-token", {
          accessToken,
          refreshToken,
        });
      } catch (error) {
        // Ignore errors during logout - token might already be expired
        console.error("Error revoking token:", error);
      }
    }

    // Clear local storage regardless of API call result
    localStorage.removeItem("access_token");
    localStorage.removeItem("refresh_token");
    localStorage.removeItem("user");
  },
};
