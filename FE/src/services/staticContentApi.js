/**
 * File: staticContentApi.js
 * Author: Keytietkiem Team
 * Created: 2025
 * Version: 1.0.0
 * Purpose: REST client for Static Content management endpoints (Policy, UserGuide, AboutUs).
 */
import axiosClient from "../api/axiosClient";
import { postsApi } from "./postsApi";

const END = {
  STATIC: "posts/static"
};

export const staticContentApi = {
  /**
   * Get Policy content
   */
  getPolicy: () => axiosClient.get(`${END.STATIC}/policy`),
  
  /**
   * Get UserGuide content
   */
  getUserGuide: () => axiosClient.get(`${END.STATIC}/user-guide`),
  
  /**
   * Get AboutUs content
   */
  getAboutUs: () => axiosClient.get(`${END.STATIC}/about-us`),
  
  /**
   * Create static content
   */
  createStaticContent: (data) => axiosClient.post(END.STATIC, data),
  
  /**
   * Update static content
   */
  updateStaticContent: (id, data) => axiosClient.put(`${END.STATIC}/${id}`, data),
  
  /**
   * Update Policy content (uses new static content endpoint)
   */
  updatePolicy: (data) => {
    if (data.postId) {
      return axiosClient.put(`${END.STATIC}/${data.postId}`, {
        title: data.title,
        slug: data.slug,
        shortDescription: data.shortDescription,
        content: data.content,
        thumbnail: data.thumbnail,
        postTypeId: data.posttypeId
      });
    } else {
      return axiosClient.post(END.STATIC, {
        title: data.title,
        slug: data.slug,
        shortDescription: data.shortDescription,
        content: data.content,
        thumbnail: data.thumbnail,
        postTypeId: data.posttypeId,
        authorId: data.authorId
      });
    }
  },
  
  /**
   * Update UserGuide content (uses new static content endpoint)
   */
  updateUserGuide: (data) => {
    if (data.postId) {
      return axiosClient.put(`${END.STATIC}/${data.postId}`, {
        title: data.title,
        slug: data.slug,
        shortDescription: data.shortDescription,
        content: data.content,
        thumbnail: data.thumbnail,
        postTypeId: data.posttypeId
      });
    } else {
      return axiosClient.post(END.STATIC, {
        title: data.title,
        slug: data.slug,
        shortDescription: data.shortDescription,
        content: data.content,
        thumbnail: data.thumbnail,
        postTypeId: data.posttypeId,
        authorId: data.authorId
      });
    }
  },
  
  /**
   * Update AboutUs content (uses new static content endpoint)
   */
  updateAboutUs: (data) => {
    if (data.postId) {
      return axiosClient.put(`${END.STATIC}/${data.postId}`, {
        title: data.title,
        slug: data.slug,
        shortDescription: data.shortDescription,
        content: data.content,
        thumbnail: data.thumbnail,
        postTypeId: data.posttypeId
      });
    } else {
      return axiosClient.post(END.STATIC, {
        title: data.title,
        slug: data.slug,
        shortDescription: data.shortDescription,
        content: data.content,
        thumbnail: data.thumbnail,
        postTypeId: data.posttypeId,
        authorId: data.authorId
      });
    }
  },
};

