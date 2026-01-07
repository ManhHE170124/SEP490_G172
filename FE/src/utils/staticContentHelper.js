/**
 * File: staticContentHelper.js
 * Author: Keytietkiem Team
 * Created: 2025
 * Version: 2.0.0
 * Purpose: Utility functions for identifying SpecificDocumentation posts
 */

/**
 * Check if a post is a SpecificDocumentation post based on its PostType name
 * @param {Object} post - The post object
 * @returns {boolean} - True if the post is SpecificDocumentation
 */
export const isStaticContentPost = (post) => {
  if (!post) return false;
  
  // Get postTypeName from various possible property names
  const postTypeName = post.postTypeName || post.posttypeName || post.PostTypeName || post.PosttypeName || '';
  
  if (!postTypeName) return false;
  
  // Convert to slug format (lowercase, replace spaces with hyphens)
  const slug = postTypeName.toLowerCase().replace(/\s+/g, '-').replace(/_/g, '-');
  
  // Check if it's SpecificDocumentation
  return slug === 'specific-documentation';
};

/**
 * Filter out static content posts from an array
 * @param {Array} posts - Array of post objects
 * @returns {Array} - Filtered array without static content posts
 */
export const filterStaticContentPosts = (posts) => {
  if (!Array.isArray(posts)) return [];
  return posts.filter(post => !isStaticContentPost(post));
};

