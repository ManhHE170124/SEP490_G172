import axiosClient from '../api/axiosClient';

const END = {
    GATEWAYS: 'admin/payment-gateways'
};

export const paymentGatewaysApi = {
    getAll: () => axiosClient.get(END.GATEWAYS),
    getById: (id) => axiosClient.get(`${END.GATEWAYS}/${id}`),
    create: (payload) => axiosClient.post(END.GATEWAYS, payload),
    update: (id, payload) => axiosClient.put(`${END.GATEWAYS}/${id}`, payload),
    remove: (id) => axiosClient.delete(`${END.GATEWAYS}/${id}`),
    toggle: (id) => axiosClient.patch(`${END.GATEWAYS}/${id}/toggle`),
};

export default paymentGatewaysApi;