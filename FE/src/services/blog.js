import axiosClient from "../api/axiosClient";

// Lấy tất cả bài viết (array)
export function getAllPosts() {
    return axiosClient.get("/posts");
}
export function getPostTypes() {
    return axiosClient.get("/posts/posttypes");
}
export function getTags() {
    return axiosClient.get("/tags");
}