// 📝 services/settings.js

import axiosClient from "../api/axiosClient";

const END = {
    SETTINGS: "admin/settings",        // ✅ Relative path như "posts"
    SMTP_TEST: "admin/smtp/test"
};

export const settingsApi = {
    // ✅ Clone từ postsApi.getAllPosts()
    getSettings: () => axiosClient.get(END.SETTINGS),

    // ✅ Clone từ postsApi.createPost() với FormData
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

    // ✅ Test SMTP
    testSmtp: (smtp) => axiosClient.post(END.SMTP_TEST, smtp),
};

// Default export for backward compatibility
export default settingsApi;