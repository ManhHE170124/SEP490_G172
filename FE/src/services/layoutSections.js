import axiosClient from '../api/axiosClient';

const END = {
    SECTIONS: 'admin/layout-sections'
};

export const layoutSectionsApi = {
    getAll: (params) => axiosClient.get(END.SECTIONS, { params }),
    getById: (id) => axiosClient.get(`${END.SECTIONS}/${id}`),
    create: (payload) => axiosClient.post(END.SECTIONS, payload),
    update: (id, payload) => axiosClient.put(`${END.SECTIONS}/${id}`, payload),
    remove: (id) => axiosClient.delete(`${END.SECTIONS}/${id}`),
    reorder: (list) => axiosClient.patch(`${END.SECTIONS}/reorder`, list),
};

export default layoutSectionsApi;