// src/services/productSections.js
import axiosClient from "../api/axiosClient";

function baseUrl(productId, variantId) {
  return variantId
    ? `/api/products/${productId}/variants/${variantId}/sections`
    : `/api/products/${productId}/sections`;
}

export const ProductSectionsApi = {
  async listPaged(productId, variantId, {
    q, type, active, sort = "sort", dir = "asc", page = 1, pageSize = 10
  } = {}) {
    const params = { page, pageSize, sort, dir };
    if (q) params.q = q;
    if (type) params.type = type;                    // WARRANTY|NOTE|DETAIL
    if (active !== "" && active !== undefined) {
      params.active = String(active) === "true" || active === true;
    }
    const { data } = await axiosClient.get(baseUrl(productId, variantId), { params });
    return {
      page: data.page ?? data.Page ?? page,
      pageSize: data.pageSize ?? data.PageSize ?? pageSize,
      totalItems: data.totalItems ?? data.TotalItems ?? 0,
      totalPages: data.totalPages ?? data.TotalPages ?? Math.max(1, Math.ceil((data.totalItems ?? 0) / (data.pageSize ?? pageSize))),
      items: data.items ?? data.Items ?? []
    };
  },

  async get(productId, variantId, sectionId) {
    const { data } = await axiosClient.get(`${baseUrl(productId, variantId)}/${sectionId}`);
    return data;
  },

  async create(productId, variantId, dto) {
    const { data } = await axiosClient.post(baseUrl(productId, variantId), dto);
    return data;
  },

  async update(productId, variantId, sectionId, dto) {
    await axiosClient.put(`${baseUrl(productId, variantId)}/${sectionId}`, dto);
  },

  async remove(productId, variantId, sectionId) {
    await axiosClient.delete(`${baseUrl(productId, variantId)}/${sectionId}`);
  },

  async toggle(productId, variantId, sectionId) {
    const { data } = await axiosClient.patch(`${baseUrl(productId, variantId)}/${sectionId}/toggle`);
    return data; // { active: true/false } hoặc payload tuỳ BE
  },

  async reorder(productId, variantId, sectionIdsInOrder) {
    await axiosClient.post(`${baseUrl(productId, variantId)}/reorder`, { sectionIdsInOrder });
  }
};
export default ProductSectionsApi;
