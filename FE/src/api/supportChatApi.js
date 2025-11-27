// File: src/api/supportChatApi.js
import axiosClient from "./axiosClient";

export const supportChatApi = {
  // Customer mở hoặc lấy lại phiên chat
  openOrGet(body) {
    return axiosClient.post("/support-chats/open-or-get", body ?? {});
  },

  // Danh sách phiên chat của chính user hiện tại (customer)
  getMySessions(params) {
    return axiosClient.get("/support-chats/my-sessions", { params });
  },

  // Queue các phiên Waiting + chưa gán staff (dùng cho staff page)
  getUnassigned(params) {
    return axiosClient.get("/support-chats/unassigned", { params });
  },

  // Nhân viên claim 1 phiên chat
  claim(sessionId) {
    if (!sessionId) throw new Error("sessionId is required");
    return axiosClient.post(`/support-chats/${sessionId}/claim`);
  },

  // Nhân viên trả lại hàng chờ
  unassign(sessionId) {
    if (!sessionId) throw new Error("sessionId is required");
    return axiosClient.post(`/support-chats/${sessionId}/unassign`);
  },

  // Đóng phiên chat
  close(sessionId) {
    if (!sessionId) throw new Error("sessionId is required");
    return axiosClient.post(`/support-chats/${sessionId}/close`);
  },

  // Lấy lịch sử tin nhắn của 1 phiên
  getMessages(sessionId) {
    if (!sessionId) throw new Error("sessionId is required");
    return axiosClient.get(`/support-chats/${sessionId}/messages`);
  },

  // Gửi 1 tin nhắn trong phiên chat
  postMessage(sessionId, body) {
    if (!sessionId) throw new Error("sessionId is required");
    return axiosClient.post(`/support-chats/${sessionId}/messages`, body ?? {});
  },

  // Danh sách các phiên chat (bao gồm Closed) của 1 customer – cho staff
  // dùng cho panel "Các phiên chat trước với user này"
  getCustomerSessions(customerId, params) {
    if (!customerId) throw new Error("customerId is required");
    return axiosClient.get(
      `/support-chats/customer/${customerId}/sessions`,
      { params }
    );
  },

  // ---- Alias giữ backward compatibility ----

  claimSession(sessionId) {
    return this.claim(sessionId);
  },

  unassignSession(sessionId) {
    return this.unassign(sessionId);
  },

  closeSession(sessionId) {
    return this.close(sessionId);
  },

  createMessage(sessionId, body) {
    return this.postMessage(sessionId, body);
  },
};
