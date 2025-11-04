// Product FAQs service – theo ProductFaqsController (đường dẫn /api/products/{productId}/faqs)
import axiosClient from "../api/axiosClient";

export const ProductFaqsApi = {
  list: (productId) =>
    axiosClient.get(`products/${productId}/faqs`),

  // dto: { question, answer, sortOrder, isActive }
  create: (productId, dto) =>
    axiosClient.post(`products/${productId}/faqs`, dto),

  // dto: { question, answer, sortOrder, isActive }
  update: (productId, faqId, dto) =>
    axiosClient.put(`products/${productId}/faqs/${faqId}`, dto),

  remove: (productId, faqId) =>
    axiosClient.delete(`products/${productId}/faqs/${faqId}`),
};

export default ProductFaqsApi;
