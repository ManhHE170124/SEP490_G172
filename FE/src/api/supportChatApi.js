// File: src/api/supportChatApi.js
import axiosClient from "./axiosClient";

export const supportChatApi = {
  // Customer mở hoặc lấy lại phiên chat
  openOrGet(body) {
    return axiosClient.post("/support-chats/open-or-get", body ?? {});
  },

  // Danh sách phiên chat của chính user hiện tại
  getMySessions(params) {
    return axiosClient.get("/support-chats/my-sessions", { params });
  },

  // Queue các phiên Waiting + chưa gán staff (dùng cho staff page)
  getUnassigned(params) {
    return axiosClient.get("/support-chats/unassigned", { params });
  },

  // Nhân viên nhận 1 phiên chat
  claim(sessionId) {
    if (!sessionId) throw new Error("sessionId is required");
    return axiosClient.post(`/support-chats/${sessionId}/claim`, {});
  },

  // Nhân viên trả lại 1 phiên chat về queue
  unassign(sessionId) {
    if (!sessionId) throw new Error("sessionId is required");
    return axiosClient.post(`/support-chats/${sessionId}/unassign`, {});
  },

  // Đóng 1 phiên chat
  close(sessionId) {
    if (!sessionId) throw new Error("sessionId is required");
    return axiosClient.post(`/support-chats/${sessionId}/close`, {});
  },

  // Lấy danh sách tin nhắn trong 1 phiên chat
  getMessages(sessionId) {
    if (!sessionId) throw new Error("sessionId is required");
    return axiosClient.get(`/support-chats/${sessionId}/messages`);
  },

  // Gửi tin nhắn mới vào 1 phiên chat
  postMessage(sessionId, body) {
    if (!sessionId) throw new Error("sessionId is required");
    return axiosClient.post(`/support-chats/${sessionId}/messages`, body);
  },

  // ===== Alias để không phải sửa nhiều chỗ FE cũ =====

  // Một số màn FE (staff) đang gọi claimSession
  claimSession(sessionId) {
    return this.claim(sessionId);
  },

  // Và unassignSession
  unassignSession(sessionId) {
    return this.unassign(sessionId);
  },
};
