/**
 * File: DocumentationViewPage.jsx
 * Author: Keytietkiem Team
 * Created: 2025
 * Version: 1.0.0
 * Purpose: Public page for displaying SpecificDocumentation posts with sidebar navigation
 */
import React, { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { specificDocumentationApi } from '../../services/specificDocumentationApi';
import './DocumentationViewPage.css';

const DocumentationViewPage = () => {
  const { slug } = useParams();
  const navigate = useNavigate();
  const [posts, setPosts] = useState([]);
  const [currentPost, setCurrentPost] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Load all posts for sidebar
  useEffect(() => {
    const loadPosts = async () => {
      try {
        const response = await specificDocumentationApi.getAllSpecificDocumentation();

        // Handle multiple response structures
        let data = [];
        if (Array.isArray(response)) {
          data = response;
        } else if (Array.isArray(response?.data)) {
          data = response.data;
        } else if (response?.data && typeof response.data === 'object' && !Array.isArray(response.data)) {
          // If it's an object, try to extract array from values
          const values = Object.values(response.data);
          data = values.filter(item => Array.isArray(item)).flat() || [];
        }

        setPosts(data);

        // If no slug, navigate to first post or 404 if no posts
        if (!slug) {
          if (data.length > 0) {
            const firstSlug = data[0].slug || data[0].Slug;
            if (firstSlug) {
              navigate(`/tai-lieu/${firstSlug}`, { replace: true });
              return;
            }
          } else {
            // No posts available, redirect to 404
            navigate('/404', { replace: true });
            return;
          }
        }

        // Load current post if slug is provided
        if (slug) {
          loadCurrentPost(slug, data);
        }
      } catch (err) {
        console.error('Failed to load posts:', err);
        // If there's a slug and we can't load posts, redirect to 404
        if (slug) {
          navigate('/404', { replace: true });
        } else {
          setError('Không thể tải danh sách tài liệu.');
        }
      } finally {
        setLoading(false);
      }
    };

    loadPosts();
  }, [slug, navigate]);

  // Load current post when slug changes
  useEffect(() => {
    if (slug && posts.length > 0) {
      loadCurrentPost(slug, posts);
    }
  }, [slug, posts]);

  const loadCurrentPost = async (postSlug, postsList) => {
    try {
      setLoading(true);
      setError(null);

      // Try to find post in the list first
      const foundPost = postsList.find(p =>
        (p.slug || p.Slug) === postSlug
      );

      if (foundPost) {
        // Load full post details
        const response = await specificDocumentationApi.getSpecificDocumentationBySlug(postSlug);
        const data = response?.data || response;
        setCurrentPost(data);
      } else {
        // Post not found in list, redirect to 404
        navigate('/404', { replace: true });
      }
    } catch (err) {
      console.error('Failed to load post:', err);
      // If 404 or any error, redirect to 404 page
      navigate('/404', { replace: true });
    } finally {
      setLoading(false);
    }
  };

  // Get current post index
  const getCurrentPostIndex = () => {
    if (!currentPost || !posts.length) return -1;
    const currentSlug = currentPost.slug || currentPost.Slug;
    return posts.findIndex(p => (p.slug || p.Slug) === currentSlug);
  };

  // Get previous and next posts
  const getNavigationPosts = () => {
    const currentIndex = getCurrentPostIndex();
    if (currentIndex === -1) return { previous: null, next: null };

    return {
      previous: currentIndex > 0 ? posts[currentIndex - 1] : null,
      next: currentIndex < posts.length - 1 ? posts[currentIndex + 1] : null
    };
  };

  // Format date helper
  const formatDate = (value) => {
    if (!value) return '';
    try {
      const d = new Date(value);
      if (Number.isNaN(d.getTime())) return '';
      const now = new Date();
      const diffTime = Math.abs(now - d);
      const diffDays = Math.floor(diffTime / (1000 * 60 * 60 * 24));
      const diffMonths = Math.floor(diffDays / 30);

      if (diffMonths > 0) {
        return `${diffMonths} tháng trước`;
      } else if (diffDays > 0) {
        return `${diffDays} ngày trước`;
      } else {
        return 'Hôm nay';
      }
    } catch {
      return '';
    }
  };

  const { previous, next } = getNavigationPosts();
  const currentIndex = getCurrentPostIndex();

  if (loading && !currentPost) {
    return (
      <div className="documentation-page">
        <div className="documentation-loading">Đang tải...</div>
      </div>
    );
  }

  if (error && !currentPost) {
    return (
      <div className="documentation-page">
        <div className="documentation-error">
          <p>{error}</p>
          <Link to="/tai-lieu" style={{ color: '#3b82f6', textDecoration: 'underline' }}>
            Quay lại danh sách
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="documentation-page">
      <div className="documentation-container">
        {/* Sidebar */}
        <aside className="documentation-sidebar">
          <h2 className="sidebar-title">Tài liệu</h2>
          <nav className="sidebar-nav">
            {posts.length === 0 ? (
              <p style={{ padding: '20px', color: '#666', textAlign: 'center' }}>
                Chưa có tài liệu nào
              </p>
            ) : (
              <ul className="sidebar-list">
                {posts.map((post, index) => {
                  const postSlug = post.slug || post.Slug;
                  const postTitle = post.title || post.Title || '';
                  const isActive = currentPost && (currentPost.slug || currentPost.Slug) === postSlug;

                  return (
                    <li key={post.postId || post.PostId || index} className={isActive ? 'active' : ''}>
                      <Link
                        to={`/tai-lieu/${postSlug}`}
                        className="sidebar-link"
                      >
                        {postTitle}
                      </Link>
                    </li>
                  );
                })}
              </ul>
            )}
          </nav>
        </aside>

        {/* Main Content */}
        <main className="documentation-content">
          {currentPost ? (
            <>
              <div className="content-header">
                <h1 className="content-title">{currentPost.title || currentPost.Title}</h1>
                <div className="content-actions">
                  <button className="copy-button" title="Copy">
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                      <path d="M4 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z" stroke="currentColor" strokeWidth="1.5" />
                      <path d="M6 6h4M6 9h3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
                    </svg>
                  </button>
                </div>
              </div>

              <div
                className="content-body"
                dangerouslySetInnerHTML={{ __html: currentPost.content || currentPost.Content || '' }}
              />

              {/* Navigation */}
              <div className="content-navigation">
                {previous ? (
                  <Link
                    to={`/tai-lieu/${previous.slug || previous.Slug}`}
                    className="nav-button nav-previous"
                  >
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                      <path d="M10 12L6 8l4-4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                    <div className="nav-button-content">
                      <span className="nav-button-label">Trang trước</span>
                      <span className="nav-button-title">{previous.title || previous.Title}</span>
                    </div>
                  </Link>
                ) : (
                  <div className="nav-button nav-disabled"></div>
                )}

                {next ? (
                  <Link
                    to={`/tai-lieu/${next.slug || next.Slug}`}
                    className="nav-button nav-next"
                  >
                    <div className="nav-button-content">
                      <span className="nav-button-label">Trang sau</span>
                      <span className="nav-button-title">{next.title || next.Title}</span>
                    </div>
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                      <path d="M6 4l4 4-4 4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </Link>
                ) : (
                  <div className="nav-button nav-disabled"></div>
                )}
              </div>

              {/* Last updated */}
              {currentPost.updatedAt || currentPost.UpdatedAt ? (
                <div className="content-footer">
                  <p className="last-updated">
                    Cập nhật lần cuối: {formatDate(currentPost.updatedAt || currentPost.UpdatedAt)}
                  </p>
                </div>
              ) : null}
            </>
          ) : (
            <div className="content-empty">
              <p>Chưa có nội dung để hiển thị.</p>
            </div>
          )}
        </main>
      </div>
    </div>
  );
};

export default DocumentationViewPage;

