// services/productFaqs.js
// FAQ chung – khớp với FaqsController (Route: api/faqs)

import axiosClient from "../api/axiosClient";

export const ProductFaqsApi = {
  /**
   * Lấy danh sách FAQ theo trang + filter/search/sort.
   * params: { keyword, active, sort, direction, page, pageSize }
   * sort: question|sortOrder|active|created|updated ; direction: asc|desc
   * Trả: { items, total, page, pageSize }
   */
  listPaged: (params = {}) =>
    axiosClient.get("faqs", { params }),

  /** Lấy chi tiết 1 FAQ */
  getById: (faqId) =>
    axiosClient.get(`faqs/${faqId}`),

  /**
   * Tạo mới FAQ:
   * dto = {
   *   question,
   *   answer,
   *   sortOrder?,
   *   isActive?,
   *   categoryIds?: number[],
   *   productIds?: string[] (GUID)
   * }
   */
  create: (dto) =>
    axiosClient.post("faqs", dto),

  /**
   * Cập nhật FAQ:
   * dto = {
   *   question,
   *   answer,
   *   sortOrder,
   *   isActive,
   *   categoryIds?: number[],
   *   productIds?: string[]
   * }
   */
  update: (faqId, dto) =>
    axiosClient.put(`faqs/${faqId}`, dto),

  /** Xóa FAQ */
  remove: (faqId) =>
    axiosClient.delete(`faqs/${faqId}`),

  /** Đổi trạng thái IsActive */
  toggle: (faqId) =>
    axiosClient.patch(`faqs/${faqId}/toggle`),
};

export default ProductFaqsApi;
