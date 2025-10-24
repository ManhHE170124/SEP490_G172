import axios from "axios";

export const api = axios.create({
  baseURL:"http://localhost:5173",
  withCredentials: false,
});

export type CategoryListItemDto = {
  categoryId: number;
  categoryCode: string;
  categoryName: string;
  isActive: boolean;
  displayOrder: number;
  productCount: number;
};

export type CategoryDetailDto = {
  categoryId: number;
  categoryCode: string;
  categoryName: string;
  description?: string | null;
  isActive: boolean;
  displayOrder: number;
};

export type CategoryCreateDto = {
  categoryCode: string;
  categoryName: string;
  description?: string | null;
  isActive: boolean;
  displayOrder: number;
};

export type CategoryUpdateDto = Omit<CategoryCreateDto, "categoryCode">;

export type ProductListItemDto = {
  productId: string;
  productCode: string;
  productName: string;
  productType: string;
  salePrice: number;
  stockQty: number;
  warrantyDays: number;
  status: string;
  categoryIds: number[];
};

export type PagedResult<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export type ProductDetailDto = {
  productId: string;
  productCode: string;
  productName: string;
  supplierId: number;
  productType: string;
  costPrice?: number | null;
  salePrice: number;
  stockQty: number;
  warrantyDays: number;
  expiryDate?: string | null; // yyyy-MM-dd
  autoDelivery: boolean;
  status: string;
  description?: string | null;
  categoryIds: number[];
};

export type ProductCreateDto = Omit<ProductDetailDto, "productId">;
export type ProductUpdateDto = Omit<ProductDetailDto, "productId"|"productCode">;

export const ProductsApi = {
  list: (params: {
    keyword?: string; categoryId?: number; type?: string; status?: string;
    page?: number; pageSize?: number;
  }) => api.get<PagedResult<ProductListItemDto>>("/api/products/list",{ params }),

  get: (id: string) => api.get<ProductDetailDto>(`/api/products/${id}`),

  create: (dto: ProductCreateDto) => api.post<ProductDetailDto>("/api/products", dto),

  update: (id: string, dto: ProductUpdateDto) => api.put(`/api/products/${id}`, dto),

  remove: (id: string) => api.delete(`/api/products/${id}`),

  changeStatus: (id: string, status: string) =>
    api.patch(`/api/products/${id}/status`, JSON.stringify(status), {
      headers: { "Content-Type": "application/json" },
    }),

  bulkPrice: (dto: { categoryIds?: number[] | null; productType?: string | null; percent: number }) =>
    api.post("/api/products/bulk-price", dto),

  exportCsv: () => api.get("/api/products/export-csv",{ responseType:"blob" }),

  importCsv: (file: File) => {
    const f = new FormData(); f.append("file", file);
    return api.post("/api/products/import-price-csv", f, { headers: { "Content-Type":"multipart/form-data" } });
  }
};

export const CategoriesApi = {
  list: (params?: { keyword?: string; code?: string; active?: boolean }) =>
    api.get<CategoryListItemDto[]>("/api/categories",{ params }),

  get: (id: number) => api.get<CategoryDetailDto>(`/api/categories/${id}`),

  create: (dto: CategoryCreateDto) => api.post<CategoryDetailDto>("/api/categories", dto),

  update: (id: number, dto: CategoryUpdateDto) => api.put(`/api/categories/${id}`, dto),

  remove: (id: number) => api.delete(`/api/categories/${id}`),

  toggle: (id: number) => api.patch(`/api/categories/${id}/toggle`,{}),

  bulkUpsert: (items: {
    categoryCode: string; categoryName: string; isActive: boolean; displayOrder: number; description?: string | null;
  }[]) => api.post("/api/categories/bulk-upsert",{ items })
};
