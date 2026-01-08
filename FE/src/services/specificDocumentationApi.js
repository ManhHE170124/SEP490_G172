/**
 * File: specificDocumentationApi.js
 * Author: Keytietkiem Team
 * Created: 2025
 * Version: 1.0.0
 * Purpose: REST client for SpecificDocumentation management endpoints
 */
import axiosClient from "../api/axiosClient";

const END = {
  SPECIFIC_DOCUMENTATION: "posts/specific-documentation"
};

export const specificDocumentationApi = {
  /**
   * Get all SpecificDocumentation posts
   */
  getAllSpecificDocumentation: () => 
    axiosClient.get(END.SPECIFIC_DOCUMENTATION),
  
  /**
   * Get SpecificDocumentation post by slug
   */
  getSpecificDocumentationBySlug: (slug) => 
    axiosClient.get(`${END.SPECIFIC_DOCUMENTATION}/${slug}`),
  
  /**
   * Create new SpecificDocumentation post
   */
  createSpecificDocumentation: (data) => 
    axiosClient.post(END.SPECIFIC_DOCUMENTATION, data),
  
  /**
   * Update SpecificDocumentation post
   */
  updateSpecificDocumentation: (id, data) => 
    axiosClient.put(`${END.SPECIFIC_DOCUMENTATION}/${id}`, data),
  
  /**
   * Delete SpecificDocumentation post
   */
  deleteSpecificDocumentation: (id) => 
    axiosClient.delete(`${END.SPECIFIC_DOCUMENTATION}/${id}`)
};

