// Product Variants service – theo ProductVariantsController (/api/products/{productId}/variants)
import axiosClient from "../api/axiosClient";

export const ProductVariantsApi = {
  list: (productId) =>
    axiosClient.get(`products/${productId}/variants`),

  get: (productId, variantId) =>
    axiosClient.get(`products/${productId}/variants/${variantId}`),

  /**
   * dto: {
   *   variantCode?, title, durationDays?,
   *   price, originalPrice?, stockQty, warrantyDays?,
   *   status?, sortOrder?
   * }
   */
  create: (productId, dto) =>
    axiosClient.post(`products/${productId}/variants`, dto),

  update: (productId, variantId, dto) =>
    axiosClient.put(`products/${productId}/variants/${variantId}`, dto),

  remove: (productId, variantId) =>
    axiosClient.delete(`products/${productId}/variants/${variantId}`),

  // Reorder: body { variantIdsInOrder: Guid[] }
  reorder: (productId, variantIdsInOrder) =>
    axiosClient.post(`products/${productId}/variants/reorder`, { variantIdsInOrder }),
};

export default ProductVariantsApi;
