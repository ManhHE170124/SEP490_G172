let axiosClient = null;
try {
    axiosClient = require("../api/axiosClient").default;
} catch (e) {
    axiosClient = null;
}

const baseUrl = "/api/admin/settings";

const getSettings = async () => {
    try {
        if (axiosClient) {
            // axiosClient interceptor đã trả về res.data, nhưng trong axiosClient của bạn interceptor trả res.data ?? res
            const data = await axiosClient.get(baseUrl);
            return data;
        } else {
            const res = await fetch(baseUrl);
            if (!res.ok) throw new Error("Fetch failed");
            return await res.json();
        }
    } catch (err) {
        console.error("getSettings error", err);
        throw err;
    }
};

const saveSettings = async (payload, logoFile) => {
    try {
        if (logoFile) {
            const form = new FormData();
            form.append("logo", logoFile);
            form.append("payload", JSON.stringify(payload));
            if (axiosClient) {
                const res = await axiosClient.post(baseUrl, form, {
                    headers: { "Content-Type": "multipart/form-data" },
                });
                return res;
            } else {
                const res = await fetch(baseUrl, { method: "POST", body: form });
                if (!res.ok) throw new Error("Save failed");
                return await res.json();
            }
        } else {
            if (axiosClient) {
                const res = await axiosClient.post(baseUrl, payload);
                return res;
            } else {
                const res = await fetch(baseUrl, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload),
                });
                if (!res.ok) throw new Error("Save failed");
                return await res.json();
            }
        }
    } catch (err) {
        console.error("saveSettings error", err);
        throw err;
    }
};

const testSmtp = async (smtp) => {
    try {
        const url = "/api/admin/smtp/test";
        if (axiosClient) {
            const data = await axiosClient.post(url, smtp);
            return data;
        } else {
            const r = await fetch(url, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(smtp),
            });
            return r;
        }
    } catch (err) {
        console.error("testSmtp error", err);
        throw err;
    }
};

export default {
    getSettings,
    saveSettings,
    testSmtp,
};