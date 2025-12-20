import axiosClient from '../api/axiosClient';

const END = {
    GATEWAYS: 'admin/payment-gateways'
};

export const paymentGatewaysApi = {
    // PayOS-only
    getPayOS: () => axiosClient.get(`${END.GATEWAYS}/payos`),
    updatePayOS: (payload) => axiosClient.put(`${END.GATEWAYS}/payos`, payload),
};

export default paymentGatewaysApi;
