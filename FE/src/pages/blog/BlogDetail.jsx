// 📝 pages/BlogDetail/BlogDetail.jsx

import React, { useEffect, useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { postsApi } from '../../services/postsApi';
import '../../styles/BlogDetail.css';

const BlogDetail = () => {
    const { slug } = useParams();
    const navigate = useNavigate();

    const [post, setPost] = useState(null);
    const [relatedPosts, setRelatedPosts] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    useEffect(() => {
        loadPost();
    }, [slug]);

    const loadPost = async () => {
        setLoading(true);
        setError('');
        try {
            console.log('📖 Loading post:', slug);
            const postData = await postsApi.getPostBySlug(slug);
            console.log('✅ Post loaded:', postData);
            setPost(postData);

            // Load related posts
            if (postData?.postId) {
                const related = await postsApi.getRelatedPosts(postData.postId, 3);
                console.log('✅ Related posts:', related);
                setRelatedPosts(Array.isArray(related) ? related : []);
            }
        } catch (err) {
            console.error('❌ Load post error:', err);
            setError(err.message || 'Không thể tải bài viết');
        } finally {
            setLoading(false);
        }
    };

    const formatDate = (dateString) => {
        if (!dateString) return '';
        try {
            const date = new Date(dateString);
            return date.toLocaleDateString('vi-VN', {
                year: 'numeric',
                month: '2-digit',
                day: '2-digit'
            });
        } catch {
            return '';
        }
    };

    const calculateReadTime = (content) => {
        if (!content) return '5';
        const words = content.split(/\s+/).length;
        const minutes = Math.ceil(words / 200); // Avg reading speed: 200 words/min
        return minutes.toString();
    };

    const sharePost = (platform) => {
        const url = window.location.href;
        const title = post?.title || '';

        const shareUrls = {
            facebook: `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(url)}`,
            twitter: `https://twitter.com/intent/tweet?url=${encodeURIComponent(url)}&text=${encodeURIComponent(title)}`,
            copy: url
        };

        if (platform === 'copy') {
            navigator.clipboard.writeText(url)
                .then(() => alert('Đã sao chép link!'))
                .catch(() => alert('Không thể sao chép'));
        } else {
            window.open(shareUrls[platform], '_blank', 'width=600,height=400');
        }
    };

    if (loading) {
        return (
            <div className="blog-detail-container">
                <div style={{ textAlign: 'center', padding: '60px 20px' }}>
                    <div className="loading-spinner" />
                    <div style={{ marginTop: '16px' }}>Đang tải bài viết...</div>
                </div>
            </div>
        );
    }

    if (error || !post) {
        return (
            <div className="blog-detail-container">
                <div style={{ textAlign: 'center', padding: '60px 20px' }}>
                    <div style={{ fontSize: '48px', marginBottom: '16px' }}>📄</div>
                    <h2>Không tìm thấy bài viết</h2>
                    <p style={{ color: '#666', marginBottom: '24px' }}>{error}</p>
                    <button className="btn primary" onClick={() => navigate('/blogs')}>
                        ← Quay lại danh sách
                    </button>
                </div>
            </div>
        );
    }

    return (
        <div className="blog-detail-container">
            {/* Breadcrumb */}
            <div className="breadcrumb">
                <Link to="/">Trang chủ</Link>
                <span> › </span>
                <Link to="/blogs">Blog</Link>
                <span> › </span>
                <span>{post.title}</span>
            </div>

            {/* Post Header */}
            <article className="post-article">
                <h1 className="post-title">{post.title}</h1>

                <div className="post-meta">
                    <span> {formatDate(post.createdAt)}</span>
                    <span> {post.authorName || 'Admin'}</span>
                    {post.viewCount && <span> {post.viewCount} lượt xem</span>}
                </div>

                {/* Thumbnail */}
                {post.thumbnail && (
                    <div className="post-thumbnail">
                        <img src={post.thumbnail} alt={post.title} />
                    </div>
                )}

                {/* Short Description */}
                {post.shortDescription && (
                    <div className="post-intro">
                        <p>{post.shortDescription}</p>
                    </div>
                )}

                {/* Content */}
                <div
                    className="post-content"
                    dangerouslySetInnerHTML={{ __html: post.content }}
                />

                {/* Tags */}
                {post.tags && post.tags.length > 0 && (
                    <div className="post-tags">
                        <span>Tags:</span>
                        {post.tags.map(tag => (
                            <Link
                                key={tag.tagId}
                                to={`/blogs?tag=${tag.slug}`}
                                className="tag-badge"
                            >
                                #{tag.tagName}
                            </Link>
                        ))}
                    </div>
                )}

                {/* Share Buttons */}
                <div className="post-share">
                    <span>Chia sẻ:</span>
                    <button
                        className="share-btn"
                        onClick={() => sharePost('facebook')}
                        title="Share on Facebook"
                    >
                        📘
                    </button>
                    <button
                        className="share-btn"
                        onClick={() => sharePost('twitter')}
                        title="Share on Twitter"
                    >
                        🐦
                    </button>
                    <button
                        className="share-btn"
                        onClick={() => sharePost('linkedin')}
                        title="Share on LinkedIn"
                    >
                        💼
                    </button>
                    <button
                        className="share-btn"
                        onClick={() => sharePost('copy')}
                        title="Copy link"
                    >
                        🔗
                    </button>
                </div>
            </article>

            {/* Related Posts */}
            {relatedPosts.length > 0 && (
                <section className="related-posts">
                    <h3>Bài viết liên quan</h3>
                    <div className="related-grid">
                        {relatedPosts.map(relatedPost => (
                            <Link
                                key={relatedPost.postId}
                                to={`/blog/${relatedPost.slug}`}
                                className="related-card"
                            >
                                {relatedPost.thumbnail && (
                                    <img
                                        src={relatedPost.thumbnail}
                                        alt={relatedPost.title}
                                        className="related-thumb"
                                    />
                                )}
                                <div className="related-content">
                                    <h4>{relatedPost.title}</h4>
                                    {relatedPost.shortDescription && (
                                        <p>{relatedPost.shortDescription.substring(0, 100)}...</p>
                                    )}
                                    <div className="related-meta">
                                        <span>📅 {formatDate(relatedPost.createdAt)}</span>
                                        {relatedPost.viewCount && (
                                            <span>👁️ {relatedPost.viewCount}</span>
                                        )}
                                    </div>
                                </div>
                            </Link>
                        ))}
                    </div>
                </section>
            )}

            {/* Navigation */}
            <div className="post-navigation">
                <button className="btn" onClick={() => navigate('/blogs')}>
                    ← Quay lại danh sách
                </button>
            </div>
        </div>
    );
};

export default BlogDetail;