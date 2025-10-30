import axiosClient from "../api/axiosClient";

/**
 * Authentication Service
 * Handles user authentication operations including OTP verification and registration
 */
export const AuthService = {
  /**
   * Send OTP to email for registration verification
   * @param {string} email - User email address
   * @returns {Promise<string>} Success message
   */
  sendOtp: (email) =>
    axiosClient.post("/account/send-otp", { email }).then((r) => r.data),

  /**
   * Verify OTP code sent to email
   * @param {string} email - User email address
   * @param {string} otp - 6-digit OTP code
   * @returns {Promise<{isVerified: boolean, message: string, verificationToken?: string}>}
   */
  verifyOtp: (email, otp) =>
    axiosClient.post("/account/verify-otp", { email, otp }).then((r) => r.data),

  /**
   * Register new user account with OTP verification
   * @param {Object} payload - Registration data
   * @param {string} payload.username - Username (3-60 chars)
   * @param {string} payload.password - Password (6-100 chars)
   * @param {string} payload.email - Email address
   * @param {string} payload.firstName - First name
   * @param {string} payload.lastName - Last name
   * @param {string} payload.phone - Phone number (optional)
   * @param {string} payload.address - Address (optional)
   * @param {string} payload.verificationToken - Token from OTP verification
   * @returns {Promise<{accessToken: string, refreshToken: string, expiresAt: string, user: Object}>}
   */
  register: (payload) =>
    axiosClient.post("/account/register", payload).then((r) => r.data),

  /**
   * Login with username and password
   * @param {string} username - Username
   * @param {string} password - Password
   * @returns {Promise<{accessToken: string, refreshToken: string, expiresAt: string, user: Object}>}
   */
  login: (username, password) =>
    axiosClient
      .post("/account/login", { username, password })
      .then((r) => r.data),

  /**
   * Refresh access token using refresh token
   * @param {string} refreshToken - Refresh token
   * @returns {Promise<{accessToken: string, refreshToken: string, expiresAt: string, user: Object}>}
   */
  refreshToken: (refreshToken) =>
    axiosClient
      .post("/account/refresh-token", { refreshToken })
      .then((r) => r.data),

  /**
   * Change password for authenticated user
   * @param {string} currentPassword - Current password
   * @param {string} newPassword - New password (6-100 chars)
   * @returns {Promise<void>}
   */
  changePassword: (currentPassword, newPassword) =>
    axiosClient
      .post("/account/change-password", { currentPassword, newPassword })
      .then((r) => r.data),

  /**
   * Check if username already exists
   * @param {string} username - Username to check
   * @returns {Promise<boolean>}
   */
  checkUsernameExists: (username) =>
    axiosClient.get(`/account/check-username/${username}`).then((r) => r.data),

  /**
   * Check if email already exists
   * @param {string} email - Email to check
   * @returns {Promise<boolean>}
   */
  checkEmailExists: (email) =>
    axiosClient.get(`/account/check-email/${email}`).then((r) => r.data),
};
