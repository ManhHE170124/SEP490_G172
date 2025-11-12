import React, { useEffect, useState } from "react";
import "../../styles/Bloglist.css";
import { getBlogList, getPostTypes, getTags } from "../../services/blog";


const pageSize = 10;

const BlogList = () => {
    const [posts, setPosts] = useState([]);
    const [categories, setCategories] = useState([]);
    const [tags, setTags] = useState([]);
    const [page, setPage] = useState(1);
    const [total, setTotal] = useState(0);
    const [loading, setLoading] = useState(true);

    // Lấy danh sách bài viết khi đổi trang
    useEffect(() => {
        setLoading(true);
        getBlogList({ page, pageSize }).then((res) => {
            setPosts(res.data.data || []);
            setTotal(res.data.total || 0);
            setLoading(false);
        });
    }, [page]);

    // Lấy danh mục và tag sidebar
    useEffect(() => {
        getPostTypes().then(res => setCategories(res.data || []));
        getTags().then(res => setTags(res.data || []));
    }, []);

    return (
        <div className="bloglist-layout">
            <div className="bloglist-title">
                Blog chia sẻ & hướng dẫn sử dụng phần mềm
            </div>
            <div className="bloglist-subtitle">
                Tổng hợp tin tức, mẹo, và kinh nghiệm giúp bạn khai thác tối đa giá trị phần mềm bản quyền.
            </div>
            <div className="bloglist-main">
                {/* LEFT: List bài viết */}
                <div className="bloglist-left">
                    {loading
                        ? <div>Đang tải...</div>
                        : posts.map((post) => (
                            <div className="bloglist-post-card" key={post.postId}>
                                <div className="bloglist-post-meta">
                                    <span className="bloglist-post-date">
                                        {new Date(post.createdAt).toLocaleDateString("vi-VN")}
                                    </span>
                                    <span className="bloglist-post-type">
                                        {" • " + post.postTypeName}
                                    </span>
                                </div>
                                <div className="bloglist-post-title">{post.title}</div>
                                <div className="bloglist-post-desc">{post.shortDescription}</div>
                                <a className="bloglist-readmore" href={`/blog/${post.slug}`}>
                                    Đọc tiếp
                                </a>
                            </div>
                        ))
                    }

                    {/* Phân trang */}
                    <div className="bloglist-pagination">
                        <button
                            className="bloglist-page-btn"
                            disabled={page <= 1}
                            onClick={() => setPage((p) => Math.max(1, p - 1))}
                        >
                            &lt; Trước
                        </button>
                        {[...Array(Math.ceil(total / pageSize))].map((_, idx) => (
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
                            disabled={page >= Math.ceil(total / pageSize)}
                            onClick={() => setPage((p) => Math.min(Math.ceil(total / pageSize), p + 1))}
                        >
                            Sau &gt;
                        </button>
                    </div>
                </div>
                {/* RIGHT: Sidebar */}
                <div className="bloglist-right">
                    <div className="bloglist-sidebar-box">
                        <div className="bloglist-sidebar-title">Danh mục</div>
                        <ul>
                            {categories.map((item) => (
                                <li key={item.postTypeId}>{item.postTypeName}</li>
                            ))}
                        </ul>
                    </div>
                    <div className="bloglist-sidebar-box">
                        <div className="bloglist-sidebar-title">Tags</div>
                        <div className="bloglist-tags">
                            {tags.map((tag) => (
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