// Product Variants service – theo ProductVariantsController (/api/products/{productId}/variants)
import axiosClient from "../api/axiosClient";

export const ProductVariantsApi = {
  // NHẬN query param đầy đủ
  list: async (productId, params = {}) => {
    const res = await axiosClient.get(`products/${productId}/variants`, { params });
    // Chuẩn hoá theo PagedResult<T>
    return {
      items: res?.items ?? res?.Items ?? [],
      totalItems: res?.totalItems ?? res?.TotalItems ?? (res?.items?.length ?? 0),
      totalPages: res?.totalPages ?? res?.TotalPages ?? 1,
      page: res?.page ?? res?.Page ?? 1,
      pageSize: res?.pageSize ?? res?.PageSize ?? (params.pageSize ?? 10),
    };
  },

  get: (productId, variantId) =>
    axiosClient.get(`products/${productId}/variants/${variantId}`),

  create: (productId, dto) =>
    axiosClient.post(`products/${productId}/variants`, dto),

  update: (productId, variantId, dto) =>
    axiosClient.put(`products/${productId}/variants/${variantId}`, dto),

  remove: (productId, variantId) =>
    axiosClient.delete(`products/${productId}/variants/${variantId}`),

  reorder: (productId, variantIdsInOrder) =>
    axiosClient.post(`products/${productId}/variants/reorder`, { variantIdsInOrder }),

  // NEW: toggle status (PATCH)
 toggle: async (productId, variantId) => {
  const res = await axiosClient.patch(`products/${productId}/variants/${variantId}/toggle`);
  return res?.data ?? res; // luôn trả về JSON { VariantId, Status }
},
};

export default ProductVariantsApi;
