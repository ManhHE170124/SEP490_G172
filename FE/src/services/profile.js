import axiosClient from "../api/axiosClient";

/**
 * ProfileService - centralizes account/profile API calls.
 */
const profileService = {
  /**
   * Retrieve the current user's profile overview.
   * @returns {Promise<object>}
   */
  getProfile: () => axiosClient.get("/account/profile"),

  /**
   * Persist updates to profile basics (name, phone, etc.).
   * @param {object} payload
   * @returns {Promise<object>}
   */
  updateProfile: (payload) => axiosClient.put("/account/profile", payload),

  /**
   * Fetch paginated order history of the authenticated user.
   * @param {{keyword?: string, fromDate?: string, toDate?: string, page?: number, pageSize?: number}} params
   * @returns {Promise<object>}
   */
  getOrders: (params) =>
    axiosClient.get("/account/orders", {
      params,
    }),

  /**
   * Fetch transaction history (wallet, payments, etc.).
   * @param {{keyword?: string, fromDate?: string, toDate?: string, page?: number, pageSize?: number}} params
   * @returns {Promise<object>}
   */
  getTransactions: (params) =>
    axiosClient.get("/account/transactions", {
      params,
    }),

  /**
   * Change current password.
   * @param {{currentPassword: string, newPassword: string, confirmPassword?: string}} payload
   */
  changePassword: (payload) =>
    axiosClient.post("/account/change-password", payload),

  /**
   * Toggle two-factor authentication preference.
   * @param {{enabled: boolean}} payload
   */
  updateTwoFactor: (payload) =>
    axiosClient.post("/account/security/two-factor", payload),
};

export default profileService;
