// 📝 pages/BlogList/Bloglist.jsx - URL-based filter version

import React, { useEffect, useState, useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom"; // ✅ Import useSearchParams
import "../../styles/Bloglist.css";
import { postsApi } from "../../services/postsApi";

const pageSize = 10;

const BlogList = () => {
    const [posts, setPosts] = useState([]);
    const [categories, setCategories] = useState([]);
    const [tags, setTags] = useState([]);
    const [loading, setLoading] = useState(false);

    // ✅ URL-based state management
    const [searchParams, setSearchParams] = useSearchParams();

    // Get filters from URL
    const page = parseInt(searchParams.get('page')) || 1;
    const categorySlug = searchParams.get('category') || 'all';
    const tagSlug = searchParams.get('tag') || 'all';
    const searchQuery = searchParams.get('q') || '';

    const [search, setSearch] = useState(searchQuery);

    // Load data on mount
    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        setLoading(true);
        try {
            const [postsRes, categoriesRes, tagsRes] = await Promise.all([
                postsApi.getAllPosts(),
                postsApi.getPosttypes(),
                postsApi.getTags()
            ]);

            console.log("✅ Data loaded:", {
                posts: postsRes.length,
                categories: categoriesRes.length,
                tags: tagsRes.length
            });

            setPosts(Array.isArray(postsRes) ? postsRes : []);
            setCategories(Array.isArray(categoriesRes) ? categoriesRes : []);
            setTags(Array.isArray(tagsRes) ? tagsRes : []);
        } catch (err) {
            console.error("❌ Load data error:", err);
        } finally {
            setLoading(false);
        }
    };

    // ✅ Update URL parameters helper
    const updateFilter = (key, value) => {
        const newParams = new URLSearchParams(searchParams);

        if (value === 'all' || !value) {
            newParams.delete(key);
        } else {
            newParams.set(key, value);
        }

        // Reset to page 1 when filter changes
        if (key !== 'page') {
            newParams.delete('page');
        }

        setSearchParams(newParams);
    };

    // ✅ Set page
    const setPage = (newPage) => {
        updateFilter('page', newPage > 1 ? newPage : null);
    };

    // ✅ Filter logic using URL params
    const filteredPosts = useMemo(() => {
        let result = posts;

        // Search filter
        if (search.trim()) {
            const lowerSearch = search.toLowerCase();
            result = result.filter(p =>
                p.title?.toLowerCase().includes(lowerSearch) ||
                p.shortDescription?.toLowerCase().includes(lowerSearch)
            );
        }

        // Category filter (by slug)
        if (categorySlug !== "all") {
            const category = categories.find(c => c.slug === categorySlug);
            if (category) {
                result = result.filter(p => p.postTypeId === category.postTypeId);
            }
        }

        // Tag filter (by slug)
        if (tagSlug !== "all") {
            const tag = tags.find(t => t.slug === tagSlug);
            if (tag) {
                result = result.filter(p =>
                    p.tags && p.tags.some(t => t.tagId === tag.tagId)
                );
            }
        }

        return result;
    }, [posts, search, categorySlug, tagSlug, categories, tags]);

    // Pagination
    const total = filteredPosts.length;
    const pageCount = Math.ceil(total / pageSize);

    const pagedPosts = useMemo(() => {
        const start = (page - 1) * pageSize;
        return filteredPosts.slice(start, start + pageSize);
    }, [filteredPosts, page]);

    // ✅ Handle search submit
    const handleSearchSubmit = (e) => {
        e?.preventDefault();
        updateFilter('q', search.trim());
    };

    // ✅ Handle search change with debounce
    useEffect(() => {
        const timer = setTimeout(() => {
            if (search !== searchQuery) {
                updateFilter('q', search.trim());
            }
        }, 500); // Debounce 500ms

        return () => clearTimeout(timer);
    }, [search]);

    // Get active filter labels
    const activeCategory = categories.find(c => c.slug === categorySlug);
    const activeTag = tags.find(t => t.slug === tagSlug);

    return (
        <div className="bloglist-layout">
            {/* Header */}
            <div className="bloglist-header">
                <h1 className="bloglist-title">Blog chia sẻ & hướng dẫn sử dụng phần mềm</h1>
                <p className="bloglist-subtitle">
                    Tổng hợp tin tức, mẹo, và kinh nghiệm giúp bạn khai thác tối đa giá trị phần mềm bản quyền.
                </p>
            </div>

            <div className="bloglist-main">
                {/* Left - Posts */}
                <div className="bloglist-left">
                    {/* Search */}
                    <form onSubmit={handleSearchSubmit} style={{ marginBottom: 24 }}>
                        <div style={{ position: 'relative', maxWidth: '400px' }}>
                            <span style={{
                                position: 'absolute',
                                left: '14px',
                                top: '50%',
                                transform: 'translateY(-50%)',
                                fontSize: '16px',
                                color: '#9ca3af',
                                pointerEvents: 'none'
                            }}>
                            </span>
                            <input
                                type="text"
                                placeholder="Tìm kiếm bài viết..."
                                value={search}
                                onChange={e => setSearch(e.target.value)}
                                style={{
                                    width: '100%',
                                    padding: "10px 42px 10px 42px",
                                    borderRadius: "8px",
                                    border: "1px solid #e5e7eb",
                                    fontSize: '14px',
                                    outline: 'none',
                                    transition: 'all 0.2s'
                                }}
                                onFocus={e => {
                                    e.target.style.borderColor = '#2563eb';
                                    e.target.style.boxShadow = '0 0 0 3px rgba(37, 99, 235, 0.1)';
                                }}
                                onBlur={e => {
                                    e.target.style.borderColor = '#e5e7eb';
                                    e.target.style.boxShadow = 'none';
                                }}
                            />
                            {search && (
                                <button
                                    type="button"
                                    onClick={() => {
                                        setSearch("");
                                        updateFilter('q', null);
                                    }}
                                    style={{
                                        position: 'absolute',
                                        right: '12px',
                                        top: '50%',
                                        transform: 'translateY(-50%)',
                                        background: 'none',
                                        border: 'none',
                                        cursor: 'pointer',
                                        fontSize: '18px',
                                        color: '#9ca3af',
                                        padding: '4px'
                                    }}
                                >
                                    ✕
                                </button>
                            )}
                        </div>
                    </form>

                    {/* ✅ Active filters indicator */}
                    {(categorySlug !== "all" || tagSlug !== "all" || searchQuery) && (
                        <div style={{
                            marginBottom: 16,
                            display: 'flex',
                            gap: '8px',
                            flexWrap: 'wrap',
                            alignItems: 'center'
                        }}>
                            <span style={{ fontSize: 14, color: '#6b7280', fontWeight: 500 }}>
                                Đang lọc:
                            </span>

                            {searchQuery && (
                                <span style={{
                                    display: 'inline-flex',
                                    alignItems: 'center',
                                    gap: '6px',
                                    padding: '4px 10px',
                                    background: '#fef3c7',
                                    color: '#92400e',
                                    borderRadius: '16px',
                                    fontSize: '13px',
                                    fontWeight: '500'
                                }}>
                                    "{searchQuery}"
                                    <button
                                        onClick={() => {
                                            setSearch("");
                                            updateFilter('q', null);
                                        }}
                                        style={{
                                            background: 'none',
                                            border: 'none',
                                            cursor: 'pointer',
                                            color: '#92400e',
                                            padding: 0,
                                            fontSize: '14px'
                                        }}
                                    >
                                        ✕
                                    </button>
                                </span>
                            )}

                            {activeCategory && (
                                <span style={{
                                    display: 'inline-flex',
                                    alignItems: 'center',
                                    gap: '6px',
                                    padding: '4px 10px',
                                    background: '#eff6ff',
                                    color: '#2563eb',
                                    borderRadius: '16px',
                                    fontSize: '13px',
                                    fontWeight: '500'
                                }}>
                                    {activeCategory.postTypeName}
                                    <button
                                        onClick={() => updateFilter('category', null)}
                                        style={{
                                            background: 'none',
                                            border: 'none',
                                            cursor: 'pointer',
                                            color: '#2563eb',
                                            padding: 0,
                                            fontSize: '14px'
                                        }}
                                    >
                                        ✕
                                    </button>
                                </span>
                            )}

                            {activeTag && (
                                <span style={{
                                    display: 'inline-flex',
                                    alignItems: 'center',
                                    gap: '6px',
                                    padding: '4px 10px',
                                    background: '#f0fdf4',
                                    color: '#16a34a',
                                    borderRadius: '16px',
                                    fontSize: '13px',
                                    fontWeight: '500'
                                }}>
                                    #{activeTag.tagName}
                                    <button
                                        onClick={() => updateFilter('tag', null)}
                                        style={{
                                            background: 'none',
                                            border: 'none',
                                            cursor: 'pointer',
                                            color: '#16a34a',
                                            padding: 0,
                                            fontSize: '14px'
                                        }}
                                    >
                                        ✕
                                    </button>
                                </span>
                            )}

                            <button
                                onClick={() => {
                                    setSearch("");
                                    setSearchParams({});
                                }}
                                style={{
                                    fontSize: '13px',
                                    color: '#6b7280',
                                    background: 'none',
                                    border: 'none',
                                    cursor: 'pointer',
                                    textDecoration: 'underline',
                                    padding: '4px'
                                }}
                            >
                                Xóa tất cả
                            </button>
                        </div>
                    )}

                    {/* Posts grid */}
                    <div className="bloglist-posts-grid">
                        {loading ? (
                            <div className="bloglist-loading">
                                <div style={{
                                    width: '40px',
                                    height: '40px',
                                    border: '4px solid #f3f3f3',
                                    borderTop: '4px solid #3498db',
                                    borderRadius: '50%',
                                    animation: 'spin 1s linear infinite',
                                    margin: '0 auto 12px'
                                }} />
                                Đang tải...
                            </div>
                        ) : filteredPosts.length === 0 ? (
                            <div className="bloglist-empty">
                                <div style={{ fontSize: 48, marginBottom: 12 }}>📭</div>
                                <div style={{ fontSize: 18, fontWeight: 600, marginBottom: 8 }}>
                                    Không tìm thấy bài viết
                                </div>
                                <div style={{ color: '#6b7280', marginBottom: 16 }}>
                                    Thử thay đổi từ khóa tìm kiếm hoặc bộ lọc
                                </div>
                                <button
                                    onClick={() => {
                                        setSearch("");
                                        setSearchParams({});
                                    }}
                                    style={{
                                        padding: '10px 20px',
                                        background: '#2563eb',
                                        color: 'white',
                                        border: 'none',
                                        borderRadius: '8px',
                                        cursor: 'pointer',
                                        fontSize: '14px'
                                    }}
                                >
                                    Xóa bộ lọc
                                </button>
                            </div>
                        ) : (
                            pagedPosts.map(post => (
                                <div className="bloglist-post-card" key={post.postId}>
                                    {post.thumbnail && (
                                        <img
                                            src={post.thumbnail}
                                            alt={post.title}
                                            className="bloglist-post-thumb"
                                        />
                                    )}
                                    <div className="bloglist-post-meta">
                                        <span className="bloglist-post-date">
                                            {post.createdAt
                                                ? new Date(post.createdAt).toLocaleDateString("vi-VN")
                                                : ""}
                                        </span>
                                        {post.postTypeName && (
                                            <span className="bloglist-post-type">
                                                {" • " + post.postTypeName}
                                            </span>
                                        )}
                                    </div>
                                    <div className="bloglist-post-title">{post.title}</div>
                                    <div className="bloglist-post-desc">{post.shortDescription}</div>
                                    <Link className="bloglist-readmore" to={`/blog/${post.slug}`}>
                                        Đọc tiếp →
                                    </Link>
                                </div>
                            ))
                        )}
                    </div>

                    {/* Pagination */}
                    {pageCount > 1 && (
                        <div className="bloglist-pagination">
                            <button
                                disabled={page <= 1}
                                onClick={() => setPage(page - 1)}
                                className={page <= 1 ? "disabled" : ""}
                            >
                                &lt; Trước
                            </button>

                            {[...Array(pageCount)].map((_, idx) => {
                                const pageNum = idx + 1;
                                // Show first, last, current, and ±1 around current
                                if (
                                    pageNum === 1 ||
                                    pageNum === pageCount ||
                                    (pageNum >= page - 1 && pageNum <= page + 1)
                                ) {
                                    return (
                                        <button
                                            key={idx}
                                            className={page === pageNum ? "selected" : ""}
                                            onClick={() => setPage(pageNum)}
                                        >
                                            {pageNum}
                                        </button>
                                    );
                                } else if (pageNum === page - 2 || pageNum === page + 2) {
                                    return <span key={idx} style={{ padding: '0 4px' }}>...</span>;
                                }
                                return null;
                            })}

                            <button
                                disabled={page >= pageCount}
                                onClick={() => setPage(page + 1)}
                                className={page >= pageCount ? "disabled" : ""}
                            >
                                Sau &gt;
                            </button>
                        </div>
                    )}
                </div>

                {/* Right - Sidebar */}
                <div className="bloglist-right">
                    {/* Categories */}
                    <div className="bloglist-sidebar-box">
                        <div className="bloglist-sidebar-title">Danh mục</div>
                        <ul className="bloglist-category-list">
                            <li
                                onClick={() => updateFilter('category', null)}
                                className={categorySlug === "all" ? "active" : ""}
                            >
                                Tất cả danh mục
                            </li>
                            {categories.map(item => (
                                <li
                                    key={item.postTypeId}
                                    className={categorySlug === item.slug ? "active" : ""}
                                    onClick={() => updateFilter('category', item.slug)}
                                >
                                    {item.postTypeName}
                                </li>
                            ))}
                        </ul>
                    </div>

                    {/* ✅ Tags - CLICKABLE with URL update */}
                    <div className="bloglist-sidebar-box">
                        <div className="bloglist-sidebar-title">Tags</div>
                        <div className="bloglist-tags">
                            {tags.map(tag => (
                                <button
                                    key={tag.tagId}
                                    className={`bloglist-tag ${tagSlug === tag.slug ? 'active' : ''}`}
                                    onClick={() => {
                                        if (tagSlug === tag.slug) {
                                            updateFilter('tag', null); // Toggle off
                                        } else {
                                            updateFilter('tag', tag.slug); // Select tag
                                        }
                                    }}
                                >
                                    #{tag.tagName}
                                </button>
                            ))}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default BlogList;