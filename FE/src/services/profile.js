import axiosClient from "../api/axiosClient";

const CUSTOMER_PROFILE_ENDPOINT = "/customer/account/profile";
const ADMIN_PROFILE_ENDPOINT = "/admin/account/profile";

/**
 * ProfileService - centralizes account/profile API calls.
 */
const profileService = {
  /**
   * Retrieve the current user's profile overview (customer scope).
   * @returns {Promise<object>}
   */
  getProfile: () => axiosClient.get(CUSTOMER_PROFILE_ENDPOINT),

  /**
   * Persist updates to customer profile basics.
   * @param {object} payload
   * @returns {Promise<object>}
   */
  updateProfile: (payload) =>
    axiosClient.put(CUSTOMER_PROFILE_ENDPOINT, payload),

  /**
   * Retrieve admin/staff profile overview.
   * @returns {Promise<object>}
   */
  getAdminProfile: () => axiosClient.get(ADMIN_PROFILE_ENDPOINT),

  /**
   * Update admin/staff profile basics.
   * @param {object} payload
   * @returns {Promise<object>}
   */
  updateAdminProfile: (payload) =>
    axiosClient.put(ADMIN_PROFILE_ENDPOINT, payload),

  /**
   * Fetch paginated order history of the authenticated user.
   * @param {{keyword?: string, fromDate?: string, toDate?: string, page?: number, pageSize?: number}} params
   * @returns {Promise<object>}
   */
  getOrders: (params) =>
    axiosClient.get("/orders/history", {
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
