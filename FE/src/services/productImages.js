// Product Images service – theo ProductImagesController (đường dẫn /api/products/{productId}/images)
import axiosClient from "../api/axiosClient";

export const ProductImagesApi = {
  list: (productId) =>
    axiosClient.get(`products/${productId}/images`),

  upload: async (productId, file) => {
    const form = new FormData();
    form.append("file", file);
    const { data } = await axiosClient.post(`products/${productId}/images/upload`, form);
    // Chuẩn hoá key (phòng khi backend không camelCase)
    const d = data ?? {};
    return {
      imageId: d.imageId ?? d.ImageId,
      url: d.url ?? d.Url,
      sortOrder: d.sortOrder ?? d.SortOrder,
      isPrimary: d.isPrimary ?? d.IsPrimary,
      altText: d.altText ?? d.AltText ?? null,
    };
  },

  addByUrl: (productId, { url, altText = null, sortOrder = null, isPrimary = false }) =>
    axiosClient.post(`products/${productId}/images/by-url`, {
      url, altText, sortOrder, isPrimary,
    }),

  // Body là raw string JSON theo controller: [FromBody] string url
  setThumbnail: (productId, url) =>
    axiosClient.post(`products/${productId}/images/thumbnail`, JSON.stringify(url), {
      headers: { "Content-Type": "application/json" },
    }),

  // Reorder: DTO { imageIdsInOrder: number[] }
  reorder: (productId, imageIdsInOrder) =>
    axiosClient.post(`products/${productId}/images/reorder`, { imageIdsInOrder }),

  setPrimary: (productId, imageId) =>
    axiosClient.post(`products/${productId}/images/${imageId}/primary`),

  remove: (productId, imageId) =>
    axiosClient.delete(`products/${productId}/images/${imageId}`),
};

export default ProductImagesApi;
