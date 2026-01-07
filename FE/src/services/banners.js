// src/services/banners.js
import axiosClient from "../api/axiosClient";

const END = {
    ADMIN_BANNERS: "admin/banners",
    STOREFRONT_BANNERS: "storefront/banners",
};

export const bannersApi = {
    list: (params) => axiosClient.get(END.ADMIN_BANNERS, { params }),
    create: (payload) => axiosClient.post(END.ADMIN_BANNERS, payload),
    update: (id, payload) => axiosClient.put(`${END.ADMIN_BANNERS}/${id}`, payload),
    remove: (id) => axiosClient.delete(`${END.ADMIN_BANNERS}/${id}`),
};

export default bannersApi;
