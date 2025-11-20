/**
 * File: postsApi.js
 * Author: HieuNDHE173169
 * Created: 25/10/2025
 * Last Updated: 30/10/2025
 * Version: 1.0.0
 * Purpose: REST client for Post and Tag management endpoints.
 *          Provides API methods for managing posts, tags, post types, and post images.
 * Endpoints:
 *   Posts:
 *     - GET    /api/posts                 : List all posts
 *     - GET    /api/posts/{id}            : Get post by id
 *     - POST   /api/posts                 : Create a post
 *     - PUT    /api/posts/{id}            : Update a post
 *     - DELETE /api/posts/{id}            : Delete a post
 *   Tags:
 *     - GET    /api/tags                  : List all tags
 *     - GET    /api/tags/{id}             : Get tag by id
 *     - POST   /api/tags                  : Create a tag
 *     - PUT    /api/tags/{id}             : Update a tag
 *     - DELETE /api/tags/{id}             : Delete a tag
 *   PostTypes:
 *     - GET    /api/posts/posttypes       : List all post types
 *   Post Images:
 *     - POST   /api/PostImages/uploadImage     : Upload a post image
 *     - DELETE /api/PostImages/deleteImage : Delete a post image by publicId
 */
import axiosClient from "../api/axiosClient";

const END = {
  POSTS: "posts",
  TAGS: "tags",
  POST_IMAGES: "PostImages",
  COMMENTS: "comments"
};

export const postsApi = {
  // Post CRUD
  getAllPosts: () => axiosClient.get(END.POSTS),
  getPostById: (id) => axiosClient.get(`${END.POSTS}/${id}`),
  createPost: (data) => axiosClient.post(END.POSTS, data),
  updatePost: (id, data) => axiosClient.put(`${END.POSTS}/${id}`, data),
  deletePost: (id) => axiosClient.delete(`${END.POSTS}/${id}`),

  // Tag CRUD group
  getTags: () => axiosClient.get(END.TAGS),
  getTagById: (id) => axiosClient.get(`${END.TAGS}/${id}`),
  createTag: (data) => axiosClient.post(END.TAGS, data),
  updateTag: (id, data) => axiosClient.put(`${END.TAGS}/${id}`, data),
  deleteTag: (id) => axiosClient.delete(`${END.TAGS}/${id}`),

  // PostType 
  getPosttypes: () => axiosClient.get(`${END.POSTS}/posttypes`),
  getPosttypeById: (id) => axiosClient.get(`${END.POSTS}/posttypes/${id}`),
  createPosttype: (data) => axiosClient.post(`${END.POSTS}/posttypes`, data),
  updatePosttype: (id, data) => axiosClient.put(`${END.POSTS}/posttypes/${id}`, data),
  deletePosttype: (id) => axiosClient.delete(`${END.POSTS}/posttypes/${id}`),
  // PostImage 
  uploadImage: (file) => {
    const form = new FormData();
    form.append("file", file);
    return axiosClient.post(`${END.POST_IMAGES}/uploadImage`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  deleteImage: (publicId) => {
  return axiosClient.delete(`${END.POST_IMAGES}/deleteImage`, {
    data: { publicId }, 
    headers: { "Content-Type": "application/json" },
  });
  },

  // Comment CRUD
  getComments: (postId, page = 1, pageSize = 20) => axiosClient.get(`${END.COMMENTS}/posts/${postId}/comments`, {
    params: { page, pageSize }
  }), // GET /api/comments/posts/{postId}/comments?page=1&pageSize=20
  getCommentById: (id) => axiosClient.get(`${END.COMMENTS}/${id}`),
  getCommentReplies: (id) => axiosClient.get(`${END.COMMENTS}/${id}/replies`),
  createComment: (data) => axiosClient.post(END.COMMENTS, data),
  updateComment: (id, data) => axiosClient.put(`${END.COMMENTS}/${id}`, data),
  deleteComment: (id) => axiosClient.delete(`${END.COMMENTS}/${id}`),
  showComment: (id) => axiosClient.patch(`${END.COMMENTS}/${id}/show`),
  hideComment: (id) => axiosClient.patch(`${END.COMMENTS}/${id}/hide`),
};

export function extractPublicId(imageUrl) {
  if (!imageUrl) return null;

  try {
    // Tách phần sau "/upload/" và trước phần mở rộng (.jpg, .png, ...)
    const regex = /\/upload\/(?:v\d+\/)?(.+?)\.[a-zA-Z]+$/;
    const match = imageUrl.match(regex);
    return match ? match[1] : null;
  } catch (error) {
    console.error("Error extracting publicId:", error);
    return null;
  }
}