// File: src/api/supportChatApi.js
import axiosClient from "./axiosClient";

export const supportChatApi = {
  // Customer mở hoặc lấy lại phiên chat
  openOrGet(body) {
    return axiosClient.post("/support-chats/open-or-get", body ?? {});
  },

  // Danh sách phiên chat của chính user hiện tại (customer hoặc staff)
  getMySessions(params) {
    return axiosClient.get("/support-chats/my-sessions", { params });
  },

  // Queue các phiên Waiting + chưa gán staff (dùng cho staff/admin page)
  getUnassigned(params) {
    return axiosClient.get("/support-chats/unassigned", { params });
  },

  // Staff claim 1 phiên đang ở hàng chờ
  claim(sessionId) {
    return axiosClient.post(`/support-chats/${sessionId}/claim`);
  },

  // Staff/Admin trả lại phiên về hàng chờ (chỉ khi đang là người phụ trách)
  unassign(sessionId) {
    return axiosClient.post(`/support-chats/${sessionId}/unassign`);
  },

  // Đóng phiên chat (chỉ người phụ trách)
  close(sessionId) {
    return axiosClient.post(`/support-chats/${sessionId}/close`);
  },

  // Lấy lịch sử tin nhắn của 1 session
  getMessages(sessionId, params) {
    return axiosClient.get(`/support-chats/${sessionId}/messages`, {
      params,
    });
  },

  // Tạo tin nhắn (customer hoặc staff đang phụ trách)
  postMessage(sessionId, body) {
    return axiosClient.post(`/support-chats/${sessionId}/messages`, body);
  },

  // Danh sách các phiên chat (bao gồm Closed) của 1 customer – cho staff/admin
  // dùng cho panel "Các phiên chat trước với user này"
  getCustomerSessions(customerId, params) {
    if (!customerId) throw new Error("customerId is required");
    return axiosClient.get(`/support-chats/customer/${customerId}/sessions`, {
      params,
    });
  },

  // === ADMIN APIs ===

  // Cột "Đã nhận": tất cả phiên đã được bất kỳ staff nào nhận
  adminGetAssignedSessions(params) {
    return axiosClient.get("/support-chats/admin/assigned-sessions", {
      params,
    });
  },

  // Admin gửi tin nhắn mà KHÔNG claim / KHÔNG đổi trạng thái
  adminPostMessage(sessionId, body) {
    return axiosClient.post(
      `/support-chats/admin/${sessionId}/messages`,
      body
    );
  },

  // 🆕 Admin gán nhân viên cho 1 phiên chat (dùng cho popup "Gán" ở cột Chờ nhận)
  adminAssignStaff(sessionId, assigneeId) {
    return axiosClient.post(`/support-chats/admin/${sessionId}/assign`, {
      assigneeId,
    });
  },

  // 🆕 Admin chuyển phiên chat sang nhân viên khác (dùng cho nút "Chuyển nhân viên")
  adminTransferStaff(sessionId, assigneeId) {
    return axiosClient.post(
      `/support-chats/admin/${sessionId}/transfer-staff`,
      {
        assigneeId,
      }
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
