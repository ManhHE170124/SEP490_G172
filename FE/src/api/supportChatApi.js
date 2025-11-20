// File: src/api/supportChatApi.js
import axiosClient from "./axiosClient";

export const supportChatApi = {
  // Customer mở widget: open or get current session
  openOrGet(payload) {
    // payload optional: { initialMessage }
    return axiosClient.post("/support-chats/open-or-get", payload || {});
  },

  // Danh sách phiên chat của user hiện tại (customer: của mình, staff: đang assigned)
  getMySessions(params = {}) {
    const p = {
      includeClosed: params.includeClosed ?? false,
    };
    return axiosClient.get("/support-chats/my-sessions", { params: p });
  },

  // Queue chờ nhận: các session Waiting + chưa gán staff
  getUnassigned(params = {}) {
    const p = {
      page: params.page || 1,
      pageSize: params.pageSize || 20,
    };
    return axiosClient.get("/support-chats/unassigned", { params: p });
  },

  // Staff nhận (claim) 1 session
  claim(sessionId) {
    return axiosClient.post(`/support-chats/${sessionId}/claim`, {});
  },

  // Lấy danh sách message của 1 session
  getMessages(sessionId, params = {}) {
    const p = {
      // chừa chỗ cho paging trong tương lai nếu cần
      page: params.page || 1,
      pageSize: params.pageSize || 200,
    };
    return axiosClient.get(`/support-chats/${sessionId}/messages`, {
      params: p,
    });
  },

  // Gửi message trong 1 session
  postMessage(sessionId, payload) {
    // payload: { content }
    return axiosClient.post(`/support-chats/${sessionId}/messages`, payload);
  },

  // Đóng session
  close(sessionId) {
    return axiosClient.post(`/support-chats/${sessionId}/close`, {});
  },
};
