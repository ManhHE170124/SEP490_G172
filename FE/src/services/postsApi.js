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
 *     - GET    /api/posts/{postId}/images         : List images for a post
 *     - POST   /api/posts/{postId}/images        : Add image to a post
 *     - PUT    /api/posts/{postId}/images/{imageId} : Update post image
 *     - DELETE /api/posts/{postId}/images/{imageId} : Delete post image
 */
import axiosClient from "../api/axiosClient";

const END = {
  POSTS: "posts",
  TAGS: "tags",
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
  getAllPostTypes: () => axiosClient.get(`${END.POSTS}/posttypes`),

  // PostImage 
  getPostImages: (postId) => axiosClient.get(`${END.POSTS}/${postId}/images`),
  addPostImage: (postId, data) => axiosClient.post(`${END.POSTS}/${postId}/images`, data),
  updatePostImage: (postId, imageId, data) => axiosClient.put(`${END.POSTS}/${postId}/images/${imageId}`, data),
  deletePostImage: (postId, imageId) => axiosClient.delete(`${END.POSTS}/${postId}/images/${imageId}`),
  uploadImage: (file) => {
    const form = new FormData();
    form.append("file", file);
    return axiosClient.post(`${END.POSTS}/upload`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },


  
};