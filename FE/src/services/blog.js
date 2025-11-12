import axiosClient from "../api/axiosClient";

export function getBlogList({ page = 1, pageSize = 10 } = {}) {
    return axiosClient.get("/posts", {
        params: { page, pageSize }
    });
}

export function getPostTypes() {
    return axiosClient.get("/posts/posttypes");
}

// Lấy danh sách tags
export function getTags() {
    return axiosClient.get("/tags");
}