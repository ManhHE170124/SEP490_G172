// services/productFaqs.js
// Product FAQs service – khớp với ProductFaqsController (Route: api/products/{productId}/faqs)
import axiosClient from "../api/axiosClient";

export const ProductFaqsApi = {
  /**
   * Lấy danh sách FAQ theo trang + filter/search/sort.
   * params: { keyword, active, sort, direction, page, pageSize }
   * sort: question|sortOrder|active|created|updated ; direction: asc|desc
   * Trả: { items, total, page, pageSize }
   */
  listPaged: (productId, params = {}) =>
    axiosClient.get(`products/${productId}/faqs`, { params }),

  /** Lấy chi tiết 1 FAQ */
  getById: (productId, faqId) =>
    axiosClient.get(`products/${productId}/faqs/${faqId}`),

  /** Tạo mới FAQ: dto = { question, answer, sortOrder, isActive } */
  create: (productId, dto) =>
    axiosClient.post(`products/${productId}/faqs`, dto),

  /** Cập nhật FAQ: dto = { question, answer, sortOrder, isActive } */
  update: (productId, faqId, dto) =>
    axiosClient.put(`products/${productId}/faqs/${faqId}`, dto),

  /** Xóa FAQ */
  remove: (productId, faqId) =>
    axiosClient.delete(`products/${productId}/faqs/${faqId}`),

  /** Đổi trạng thái IsActive */
  toggle: (productId, faqId) =>
    axiosClient.patch(`products/${productId}/faqs/${faqId}/toggle`),
};

export default ProductFaqsApi;
