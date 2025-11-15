// 📝 File: Bloglist.jsx - SỬA PHẦN useEffect

import React, { useEffect, useState, useMemo } from "react";
import "../../styles/Bloglist.css";
import { postsApi } from "../../services/postsApi";

const pageSize = 10;

const BlogList = () => {
    const [posts, setPosts] = useState([]);
    const [categories, setCategories] = useState([]);
    const [tags, setTags] = useState([]);
    const [loading, setLoading] = useState(false);
    const [page, setPage] = useState(1);
    const [search, setSearch] = useState("");
    const [categoryFilter, setCategoryFilter] = useState("all");

    // 🔧 Helper: Normalize properties
    const normalize = (obj) => ({
        ...obj,
        postId: obj.postId || obj.PostId,
        postTypeId: obj.postTypeId || obj.PostTypeId,
        postTypeName: obj.postTypeName || obj.PostTypeName,
        tagId: obj.tagId || obj.TagId,
        tagName: obj.tagName || obj.TagName,
        title: obj.title || obj.Title,
        slug: obj.slug || obj.Slug,
        thumbnail: obj.thumbnail || obj.Thumbnail,
        shortDescription: obj.shortDescription || obj.ShortDescription,
        createdAt: obj.createdAt || obj.CreatedAt,
    });

    // ✅ FIX: Load Posts
    useEffect(() => {
        setLoading(true);
        postsApi.getAllPosts()
            .then(res => {
                // Xử lý cả 2 trường hợp: res.data hoặc res
                const rawData = res?.data || res;
                console.log("✅ Raw API Response:", rawData);

                const data = Array.isArray(rawData) ? rawData : [];
                const normalized = data.map(normalize);
                console.log("✅ Normalized Posts:", normalized);

                setPosts(normalized);
            })
            .catch(err => {
                console.error("❌ Load posts error:", err);
                setPosts([]);
            })
            .finally(() => setLoading(false));
    }, []);

    // ✅ FIX: Load Categories & Tags
    useEffect(() => {
        Promise.all([
            postsApi.getPosttypes().catch(() => []),
            postsApi.getTags().catch(() => [])
        ]).then(([catRes, tagRes]) => {
            // Xử lý cả 2 trường hợp
            const catData = catRes?.data || catRes;
            const tagData = tagRes?.data || tagRes;

            console.log("✅ Categories:", catData);
            console.log("✅ Tags:", tagData);

            setCategories(
                Array.isArray(catData) ? catData.map(normalize) : []
            );
            setTags(
                Array.isArray(tagData) ? tagData.map(normalize) : []
            );
        }).catch(err => {
            console.error("❌ Load metadata error:", err);
        });
    }, []);

    // Filter logic
    const filteredPosts = useMemo(() => {
        let filtered = [...posts];

        if (search.trim()) {
            const s = search.trim().toLowerCase();
            filtered = filtered.filter(post =>
                (post.title || "").toLowerCase().includes(s) ||
                (post.shortDescription || "").toLowerCase().includes(s)
            );
        }

        if (categoryFilter !== "all") {
            filtered = filtered.filter(post =>
                String(post.postTypeId) === String(categoryFilter)
            );
        }

        console.log(`🔍 Filter: ${posts.length} posts → ${filtered.length} results`);
        return filtered;
    }, [posts, search, categoryFilter]);

    const total = filteredPosts.length;
    const pageCount = Math.max(1, Math.ceil(total / pageSize));

    const pagedPosts = useMemo(() => {
        const start = (page - 1) * pageSize;
        return filteredPosts.slice(start, start + pageSize);
    }, [filteredPosts, page]);

    useEffect(() => {
        setPage(1);
    }, [search, categoryFilter]);

    return (
        <div className="bloglist-layout">
            <div className="bloglist-title">
                Blog chia sẻ & hướng dẫn sử dụng phần mềm
            </div>
            <div className="bloglist-subtitle">
                Tổng hợp tin tức, mẹo, và kinh nghiệm giúp bạn khai thác tối đa giá trị phần mềm bản quyền.
            </div>
            <div className="bloglist-main">
                <div className="bloglist-left">
                    {/* Search & Filter */}
                    <div style={{ display: "flex", gap: "12px", marginBottom: 24 }}>
                        <input
                            type="text"
                            placeholder="Tìm kiếm bài viết..."
                            value={search}
                            onChange={e => setSearch(e.target.value)}
                            style={{
                                padding: "8px 12px",
                                borderRadius: "6px",
                                border: "1px solid #ddd",
                                minWidth: 250,
                                fontSize: 15
                            }}
                        />
                        <select
                            value={categoryFilter}
                            onChange={e => setCategoryFilter(e.target.value)}
                            style={{
                                padding: "8px 12px",
                                borderRadius: "6px",
                                border: "1px solid #ddd",
                                fontSize: 15,
                                minWidth: 150
                            }}
                        >
                            <option value="all">Tất cả danh mục</option>
                            {categories.map(item => (
                                <option key={item.postTypeId} value={item.postTypeId}>
                                    {item.postTypeName}
                                </option>
                            ))}
                        </select>
                    </div>

                    {/* Content */}
                    {loading ? (
                        <div style={{ textAlign: "center", padding: "40px" }}>
                            Đang tải...
                        </div>
                    ) : pagedPosts.length === 0 ? (
                        <div style={{ textAlign: "center", padding: "40px", color: "#666" }}>
                            {posts.length === 0
                                ? "Chưa có bài viết nào. Hãy tạo bài viết đầu tiên!"
                                : "Không tìm thấy bài viết phù hợp."}
                        </div>
                    ) : (
                        <>
                            {pagedPosts.map(post => (
                                <div className="bloglist-post-card" key={post.postId}>
                                    {post.thumbnail && (
                                        <img
                                            src={post.thumbnail}
                                            alt={post.title}
                                            className="bloglist-post-thumb"
                                            onError={(e) => {
                                                e.target.style.display = 'none';
                                            }}
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
                                    <div className="bloglist-post-title">
                                        {post.title || "(Không có tiêu đề)"}
                                    </div>
                                    <div className="bloglist-post-desc">
                                        {post.shortDescription}
                                    </div>
                                    <a
                                        className="bloglist-readmore"
                                        href={`/blog/${post.slug}`}
                                    >
                                        Đọc tiếp →
                                    </a>
                                </div>
                            ))}

                            {/* Pagination */}
                            {pageCount > 1 && (
                                <div className="bloglist-pagination">
                                    <button
                                        className="bloglist-page-btn"
                                        disabled={page <= 1}
                                        onClick={() => setPage(p => Math.max(1, p - 1))}
                                    >
                                        &lt; Trước
                                    </button>
                                    {[...Array(pageCount)].map((_, idx) => (
                                        <button
                                            key={idx}
                                            className={`bloglist-page-btn${page === idx + 1 ? " selected" : ""}`}
                                            onClick={() => setPage(idx + 1)}
                                        >
                                            {idx + 1}
                                        </button>
                                    ))}
                                    <button
                                        className="bloglist-page-btn"
                                        disabled={page >= pageCount}
                                        onClick={() => setPage(p => Math.min(pageCount, p + 1))}
                                    >
                                        Sau &gt;
                                    </button>
                                </div>
                            )}
                        </>
                    )}
                </div>

                {/* Sidebar */}
                <div className="bloglist-right">
                    <div className="bloglist-sidebar-box">
                        <div className="bloglist-sidebar-title">Danh mục</div>
                        <ul>
                            {categories.length === 0 ? (
                                <li style={{ color: "#999" }}>Chưa có danh mục</li>
                            ) : (
                                categories.map(item => (
                                    <li
                                        key={item.postTypeId}
                                        style={{ cursor: "pointer" }}
                                        onClick={() => setCategoryFilter(String(item.postTypeId))}
                                    >
                                        {item.postTypeName}
                                    </li>
                                ))
                            )}
                        </ul>
                    </div>
                    <div className="bloglist-sidebar-box">
                        <div className="bloglist-sidebar-title">Tags</div>
                        <div className="bloglist-tags">
                            {tags.length === 0 ? (
                                <span style={{ color: "#999" }}>Chưa có tag</span>
                            ) : (
                                tags.map(tag => (
                                    <span key={tag.tagId} className="bloglist-tag">
                                        #{tag.tagName}
                                    </span>
                                ))
                            )}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default BlogList;