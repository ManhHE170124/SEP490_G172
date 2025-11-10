// services/products.js
import axiosClient from "../api/axiosClient";

const ROOT = "products";

// VIỆT HOÁ loại sản phẩm
export const PRODUCT_TYPES = [
  { value: "SHARED_KEY",      label: "Key dùng chung" },
  { value: "PERSONAL_KEY",    label: "Key cá nhân" },
  { value: "SHARED_ACCOUNT",  label: "Tài khoản dùng chung" },
  { value: "PERSONAL_ACCOUNT",label: "Tài khoản cá nhân" },
];

// VIỆT HOÁ trạng thái
export const PRODUCT_STATUSES = [
  { value: "ACTIVE",        label: "Hiển thị" },
  { value: "INACTIVE",      label: "Ẩn" },
  { value: "OUT_OF_STOCK",  label: "Hết hàng" },
];

// Helper: map code -> label (VI)
export const typeLabelOf = (v) =>
  PRODUCT_TYPES.find(x => x.value === v)?.label || v;

export const statusLabelOf = (v) =>
  PRODUCT_STATUSES.find(x => x.value === v)?.label || v;

export const ProductApi = {
  // ===== CRUD & list =====
  list: (params = {}) => axiosClient.get(`${ROOT}/list`, { params }),
  get: (id) => axiosClient.get(`${ROOT}/${id}`),
  create: (payload) => axiosClient.post(ROOT, payload),
  update: (id, payload) => axiosClient.put(`${ROOT}/${id}`, payload),
  toggle: (id) => axiosClient.patch(`${ROOT}/${id}/toggle`),
createWithImages: (payload, files, primaryIndex = 0) => {
   const form = new FormData();
   form.append("json", new Blob([JSON.stringify(payload)], { type: "application/json" }));
   files.forEach((f, i) => form.append("images", f, f.name));
   form.append("primaryIndex", String(primaryIndex));
   return axiosClient.post(`${ROOT}/with-images`, form, {
     headers: { "Content-Type": "multipart/form-data" },
   });
 },
  // constants + helpers cho FE
  types: PRODUCT_TYPES,
  statuses: PRODUCT_STATUSES,
  typeLabelOf,
  statusLabelOf,
};

export default ProductApi;