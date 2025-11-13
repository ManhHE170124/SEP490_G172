import React, { useEffect, useState, useMemo } from "react";
import "../../styles/Bloglist.css";
import { postsApi } from "../../services/postsApi"; // import đúng như admin

const pageSize = 10;

const BlogList = () => {
    const [posts, setPosts] = useState([]);
    const [categories, setCategories] = useState([]);
    const [tags, setTags] = useState([]);
    const [loading, setLoading] = useState(false);

    // Paging & filter
    const [page, setPage] = useState(1);
    const [search, setSearch] = useState("");
    const [categoryFilter, setCategoryFilter] = useState("all");

    // Lấy ALL data 1 lần (paging client)
    useEffect(() => {
        setLoading(true);
        postsApi.getAllPosts()
            .then(res => {
                // ALWAYS log data để chắc mapping đúng!
                console.log("All Posts (bloglist):", res.data);
                setPosts(Array.isArray(res.data) ? res.data : []);
            })
            .finally(() => setLoading(false));
    }, []);

    useEffect(() => {
        postsApi.getPosttypes().then(res => {
            console.log("All PostTypes (bloglist):", res.data);
            setCategories(Array.isArray(res.data) ? res.data : []);
        });
        postsApi.getTags().then(res => {
            console.log("All Tags (bloglist):", res.data);
            setTags(Array.isArray(res.data) ? res.data : []);
        });
    }, []);

    // Filter & paging phía client
    const filteredPosts = useMemo(() => {
        let filtered = [...posts];
        if (search.trim()) {
            const s = search.trim().toLowerCase();
            filtered = filtered.filter(
                post =>
                    (post.title || "").toLowerCase().includes(s) ||
                    (post.shortDescription || "").toLowerCase().includes(s)
            );
        }
        if (categoryFilter !== "all") {
            filtered = filtered.filter(post => String(post.postTypeId) === String(categoryFilter));
        }
        return filtered;
    }, [posts, search, categoryFilter]);

    const total = filteredPosts.length;
    const pageCount = Math.ceil(total / pageSize);

    // Lấy 1 page
    const pagedPosts = useMemo(() => {
        const start = (page - 1) * pageSize;
        return filteredPosts.slice(start, start + pageSize);
    }, [filteredPosts, page, pageSize]);

    // Reset page khi search/filter change
    useEffect(() => { setPage(1); }, [search, categoryFilter]);

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
                    {/* Controls */}
                    <div style={{ display: "flex", gap: "12px", marginBottom: 24 }}>
                        <input
                            type="text"
                            placeholder="Tìm kiếm bài viết..."
                            value={search}
                            onChange={e => setSearch(e.target.value)}
                            style={{ padding: "8px 12px", borderRadius: "6px", border: "1px solid #ddd", minWidth: 250, fontSize: 15 }}
                        />
                        <select
                            value={categoryFilter}
                            onChange={e => setCategoryFilter(e.target.value)}
                            style={{ padding: "8px 12px", borderRadius: "6px", border: "1px solid #ddd", fontSize: 15, minWidth: 150 }}
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
                        <div>Đang tải...</div>
                    ) : pagedPosts.length === 0 ? (
                        <div>Không có bài viết nào.</div>
                    ) : (
                        pagedPosts.map(post => (
                            <div className="bloglist-post-card" key={post.postId}>
                                {post.thumbnail && (
                                    <img src={post.thumbnail} alt={post.title} className="bloglist-post-thumb" />
                                )}
                                <div className="bloglist-post-meta">
                                    <span className="bloglist-post-date">
                                        {post.createdAt ? new Date(post.createdAt).toLocaleDateString("vi-VN") : ""}
                                    </span>
                                    <span className="bloglist-post-type">
                                        {" • " + (post.postTypeName || "")}
                                    </span>
                                </div>
                                <div className="bloglist-post-title">{post.title}</div>
                                <div className="bloglist-post-desc">{post.shortDescription}</div>
                                <a className="bloglist-readmore" href={`/blog/${post.slug}`}>
                                    Đọc tiếp
                                </a>
                            </div>
                        ))
                    )}
                    {/* Pagination client-side */}
                    <div className="bloglist-pagination">
                        <button className="bloglist-page-btn"
                            disabled={page <= 1}
                            onClick={() => setPage(Math.max(1, page - 1))}
                        >&lt; Trước</button>
                        {[...Array(pageCount)].map((_, idx) => (
                            <button
                                key={idx}
                                className={`bloglist-page-btn${page === idx + 1 ? " selected" : ""}`}
                                onClick={() => setPage(idx + 1)}
                            >
                                {idx + 1}
                            </button>
                        ))}
                        <button className="bloglist-page-btn"
                            disabled={page >= pageCount}
                            onClick={() => setPage(Math.min(pageCount, page + 1))}
                        >Sau &gt;</button>
                    </div>
                </div>
                {/* RIGHT: Sidebar */}
                <div className="bloglist-right">
                    <div className="bloglist-sidebar-box">
                        <div className="bloglist-sidebar-title">Danh mục</div>
                        <ul>
                            {categories.map(item => (
                                <li key={item.postTypeId}>{item.postTypeName}</li>
                            ))}
                        </ul>
                    </div>
                    <div className="bloglist-sidebar-box">
                        <div className="bloglist-sidebar-title">Tags</div>
                        <div className="bloglist-tags">
                            {tags.map(tag => (
                                <span key={tag.tagId} className="bloglist-tag">
                                    #{tag.tagName}
                                </span>
                            ))}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default BlogList;