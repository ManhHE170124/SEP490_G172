import axiosClient from "../api/axiosClient";

function baseUrl(productId, variantId) {
  return variantId
    ? `products/${productId}/variants/${variantId}/sections`
    : `products/${productId}/sections`;
}

// Map sort key UI -> API (phòng trường hợp gọi service trực tiếp)
const mapSortKeyForApi = (key) => {
  switch (String(key)) {
    case "type":
      return "sectionType";
    case "sort":
      return "sortOrder";
    case "active":
      return "isActive";
    case "title":
      return "title";
    default:
      return key || "sortOrder";
  }
};

export const ProductSectionsApi = {
  async listPaged(
    productId,
    variantId,
    { q, type, active, sort = "sort", dir = "asc", page = 1, pageSize = 10 } = {}
  ) {
    const params = { page, pageSize, sort: mapSortKeyForApi(sort), dir };
    if (q) params.q = q;
    if (type) params.type = String(type).toUpperCase(); // DETAIL|WARRANTY|NOTE
    if (active !== "" && active !== undefined)
      params.active = String(active) === "true" || active === true;

    const data = await axiosClient.get(baseUrl(productId, variantId), {
      params,
    });

    const items = (data.items ?? data.Items ?? []).map((r) => ({
      // chuẩn hoá ngay trong service
      sectionId: r.sectionId ?? r.SectionId,
      title: r.title ?? r.Title ?? "",
      sectionType:
        String(
          r.sectionType ?? r.SectionType ?? r.type ?? r.Type ?? ""
        ).toUpperCase() || "DETAIL",
      isActive: Boolean(r.isActive ?? r.IsActive ?? r.active ?? r.Active ?? true),
      sortOrder: Number(
        r.sortOrder ?? r.SortOrder ?? r.sort ?? r.Sort ?? 0
      ),
      content: r.content ?? r.Content ?? "",
      // giữ nguyên các field gốc nếu cần dùng thêm
      _raw: r,
    }));

    return {
      page: data.page ?? data.Page ?? page,
      pageSize: data.pageSize ?? data.PageSize ?? pageSize,
      totalItems: data.totalItems ?? data.TotalItems ?? 0,
      totalPages:
        data.totalPages ??
        data.TotalPages ??
        Math.max(
          1,
          Math.ceil(
            (data.totalItems ?? 0) / (data.pageSize ?? pageSize)
          )
        ),
      items,
    };
  },

  async get(productId, variantId, sectionId) {
    const data = await axiosClient.get(
      `${baseUrl(productId, variantId)}/${sectionId}`
    );
    return data;
  },

  async create(productId, variantId, dto) {
    // dto cần: title, sectionType, content, isActive, sortOrder
    const data = await axiosClient.post(baseUrl(productId, variantId), dto);
    return data;
  },

  async update(productId, variantId, sectionId, dto) {
    // trả về data để UI có thể cập nhật state tức thì
    const data = await axiosClient.put(
      `${baseUrl(productId, variantId)}/${sectionId}`,
      dto
    );
    return data;
  },

  async remove(productId, variantId, sectionId) {
    await axiosClient.delete(`${baseUrl(productId, variantId)}/${sectionId}`);
  },

  async toggle(productId, variantId, sectionId) {
    // nhiều API trả record sau toggle -> trả về để UI cập nhật state
    const data = await axiosClient.patch(
      `${baseUrl(productId, variantId)}/${sectionId}/toggle`
    );
    return data;
  },

  async reorder(productId, variantId, sectionIdsInOrder) {
    await axiosClient.post(`${baseUrl(productId, variantId)}/reorder`, {
      sectionIdsInOrder,
    });
  },
};
