/**
 * File: staticContentHelper.js
 * Author: Keytietkiem Team
 * Created: 2025
 * Version: 1.0.0
 * Purpose: Utility functions for identifying static content posts
 */

export const STATIC_CONTENT_SLUGS = ['policy', 'user-guide', 'about-us'];

/**
 * Check if a post is a static content post based on its PostType slug
 * @param {Object} post - The post object
 * @returns {boolean} - True if the post is static content
 */
export const isStaticContentPost = (post) => {
  if (!post) return false;
  
  // Get postTypeName from various possible property names
  const postTypeName = post.postTypeName || post.posttypeName || post.PostTypeName || post.PosttypeName || '';
  
  if (!postTypeName) return false;
  
  // Convert to slug format (lowercase, replace spaces with hyphens)
  const slug = postTypeName.toLowerCase().replace(/\s+/g, '-').replace(/_/g, '-');
  
  return STATIC_CONTENT_SLUGS.includes(slug);
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

