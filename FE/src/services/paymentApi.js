// services/paymentApi.js
import axiosClient from "../api/axiosClient";

const END = { PAYMENTS: "payments" };

export const paymentApi = {
  /**
   * params (BE): status, provider, email, targetType, sortBy, sortDir, ...
   * (compat) FE cũ: transactionType -> sẽ map sang targetType
   */
  list: (params = {}) => {
    const p = { ...(params || {}) };

    // compat: nếu còn nơi dùng transactionType
    if (!p.targetType && p.transactionType) {
      p.targetType = p.transactionType;
      delete p.transactionType;
    }

    return axiosClient.get(END.PAYMENTS, { params: p });
  },

  get: (id) => axiosClient.get(`${END.PAYMENTS}/${id}`),
};
