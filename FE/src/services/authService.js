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
};
