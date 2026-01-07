// 📝 services/settings.js

import axiosClient from "../api/axiosClient";

const END = {
    SETTINGS: "admin/settings",
    PUBLIC_SETTINGS: "admin/settings/public",
    SMTP_TEST: "admin/smtp/test"
};

export const settingsApi = {
    getSettings: () => axiosClient.get(END.SETTINGS),
    getPublicSettings: () => axiosClient.get(END.PUBLIC_SETTINGS),

    saveSettings: (data, logoFile) => {
        if (logoFile) {
            const form = new FormData();
            form.append("logo", logoFile);
            form.append("payload", JSON.stringify(data));
            return axiosClient.post(END.SETTINGS, form, {
                headers: { "Content-Type": "multipart/form-data" },
            });
        } else {
            return axiosClient.post(END.SETTINGS, data);
        }
    },

    testSmtp: (smtp) => axiosClient.post(END.SMTP_TEST, smtp),
};

export default settingsApi;