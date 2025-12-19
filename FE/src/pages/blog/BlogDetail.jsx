// pages/BlogDetail/BlogDetail.jsx

import React, { useEffect, useState, useMemo } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { postsApi } from "../../services/postsApi";
import "../../styles/BlogDetail.css";

const RELATED_PAGE_SIZE = 3;
const COMMENT_PAGE_SIZE = 10;

const BlogDetail = () => {
    const { slug } = useParams();
    const navigate = useNavigate();

    const [post, setPost] = useState(null);

    // Related posts
    const [relatedPosts, setRelatedPosts] = useState([]);
    const [relatedPage, setRelatedPage] = useState(1);

    // Comments
    const [comments, setComments] = useState([]);
    const [commentPage, setCommentPage] = useState(1);
    const [commentTotalPages, setCommentTotalPages] = useState(1);
    const [commentsLoading, setCommentsLoading] = useState(false);

    // Form bình luận / reply
    const [newComment, setNewComment] = useState("");
    const [newCommentLoading, setNewCommentLoading] = useState(false);

    // comment đang được rep (để hiển thị preview + @)
    const [replyTargetComment, setReplyTargetComment] = useState(null);

    // trạng thái mở/đóng danh sách reply theo comment cha
    const [expandedReplies, setExpandedReplies] = useState({});

    const [currentUser, setCurrentUser] = useState(null);

    // General state
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");

    // ===== Helpers =====
    const formatDate = (dateString) => {
        if (!dateString) return "";
        try {
            const date = new Date(dateString);
            return date.toLocaleDateString("vi-VN", {
                year: "numeric",
                month: "2-digit",
                day: "2-digit",
            });
        } catch {
            return "";
        }
    };

    const truncateText = (text, maxLength = 120) => {
        if (!text) return "";
        const plain = String(text).replace(/\s+/g, " ").trim();
        if (plain.length <= maxLength) return plain;
        return plain.slice(0, maxLength - 1) + "…";
    };

    // ===== Effect: đọc user & load post =====
    useEffect(() => {
        try {
            const raw = localStorage.getItem("user");
            if (raw) setCurrentUser(JSON.parse(raw));
        } catch (e) {
            console.error("Cannot parse user from localStorage", e);
        }
    }, []);

    useEffect(() => {
        setRelatedPage(1);
        loadPost();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [slug]);

    // ===== API: load comments =====
    const loadComments = async (postIdValue, page = 1) => {
        if (!postIdValue) return;

        setCommentsLoading(true);
        try {
            const resp = await postsApi.getComments(
                postIdValue,
                page,
                COMMENT_PAGE_SIZE
            );
            const data = resp?.data || resp || {};

            let items = [];
            let pagination = null;

            if (Array.isArray(data)) {
                items = data;
            } else {
                items = data.comments || [];
                pagination = data.pagination || null;
            }

            // Chỉ hiển thị comment đã được duyệt
            const visible = (items || []).filter(
                (c) => (c.isApproved ?? c.IsApproved ?? false) === true
            );

            const normalized = visible.map((c) => ({
                id: c.commentId || c.CommentId,
                parentId: c.parentCommentId || c.ParentCommentId || null,
                userName: c.userName || c.UserName || "Ẩn danh",
                userEmail: c.userEmail || c.UserEmail || "",
                content: c.content || c.Content || "",
                createdAt: c.createdAt || c.CreatedAt,
            }));

            setComments(normalized);
            setCommentPage(page);
            setCommentTotalPages(
                pagination?.totalPages && pagination.totalPages > 0
                    ? pagination.totalPages
                    : 1
            );
        } catch (err) {
            console.error("Error loading comments", err);
        } finally {
            setCommentsLoading(false);
        }
    };

    // ===== API: load post + related + comments =====
    const loadPost = async () => {
        setLoading(true);
        setError("");

        try {
            const postData = await postsApi.getPostBySlug(slug);

            if (!postData) {
                setPost(null);
                setRelatedPosts([]);
                setComments([]);
                setError("Không tìm thấy bài viết");
                return;
            }

            setPost(postData);

            if (postData.postId) {
                const related = await postsApi.getRelatedPosts(postData.postId, 20);

                const filtered = Array.isArray(related)
                    ? related.filter((r) => r.postId !== postData.postId)
                    : [];

                setRelatedPosts(filtered);
                setRelatedPage(1);

                await loadComments(postData.postId, 1);
            } else {
                setRelatedPosts([]);
                setComments([]);
            }
        } catch (err) {
            console.error("Load post error:", err);
            setError(err.message || "Không thể tải bài viết");
        } finally {
            setLoading(false);
        }
    };

    // ===== Share =====
    const sharePost = (platform) => {
        const url = window.location.href;
        const title = post?.title || "";

        const shareUrls = {
            facebook: `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(
                url
            )}`,
            twitter: `https://twitter.com/intent/tweet?url=${encodeURIComponent(
                url
            )}&text=${encodeURIComponent(title)}`,
            linkedin: `https://www.linkedin.com/shareArticle?mini=true&url=${encodeURIComponent(
                url
            )}&title=${encodeURIComponent(title)}`,
            copy: url,
        };

        if (platform === "copy") {
            navigator.clipboard
                .writeText(url)
                .then(() => alert("Đã sao chép link!"))
                .catch(() => alert("Không thể sao chép"));
        } else {
            window.open(shareUrls[platform], "_blank", "width=600,height=400");
        }
    };

    // ===== Comment helpers (UI) =====
    const topLevelComments = useMemo(
        () => comments.filter((c) => !c.parentId),
        [comments]
    );

    const repliesByParent = useMemo(() => {
        const map = {};
        comments.forEach((c) => {
            if (!c.parentId) return;
            if (!map[c.parentId]) map[c.parentId] = [];
            map[c.parentId].push(c);
        });
        return map;
    }, [comments]);

    const toggleRepliesForComment = (commentId) => {
        setExpandedReplies((prev) => ({
            ...prev,
            [commentId]: !prev[commentId],
        }));
    };

    const handleChangeCommentPage = (newPage) => {
        if (!post?.postId) return;
        if (newPage < 1 || newPage > commentTotalPages) return;
        loadComments(post.postId, newPage);
    };

    const handleClickReply = (comment) => {
        if (!currentUser) {
            alert("Bạn cần đăng nhập để phản hồi.");
            return;
        }

        setReplyTargetComment(comment);

        // Tự chèn @Tên vào đầu ô nhập
        const mention = `@${comment.userName} `;
        setNewComment(mention);
    };

    const clearReplyTarget = () => {
        setReplyTargetComment(null);
        setNewComment("");
    };

    const handleSubmitComment = async (e) => {
        e.preventDefault();
        if (!newComment.trim()) return;

        if (!currentUser) {
            alert("Bạn cần đăng nhập để bình luận.");
            return;
        }

        try {
            setNewCommentLoading(true);

            const userId =
                currentUser.userId || currentUser.UserId || currentUser.id;

            if (!userId) {
                alert("Không tìm thấy thông tin người dùng.");
                return;
            }

            // Nếu đang reply thì parentId là comment gốc (nếu reply 1 reply) hoặc chính comment đó
            let parentIdForApi = null;
            if (replyTargetComment) {
                parentIdForApi =
                    replyTargetComment.parentId || replyTargetComment.id;
            }

            await postsApi.createComment({
                postId: post.postId,
                userId,
                content: newComment.trim(),
                parentCommentId: parentIdForApi,
            });

            setNewComment("");
            setReplyTargetComment(null);

            await loadComments(post.postId, 1);
        } catch (err) {
            console.error("Error creating comment", err);
            alert("Không thể gửi bình luận. Vui lòng thử lại.");
        } finally {
            setNewCommentLoading(false);
        }
    };

    // ===== Detect admin user for preview banner =====
    let isAdminUser = false;
    try {
        const storedUser = localStorage.getItem("user");
        if (storedUser) {
            const parsedUser = JSON.parse(storedUser);
            const roles = Array.isArray(parsedUser?.roles) ? parsedUser.roles : [];
            isAdminUser = roles.some((r) =>
                String(r).toUpperCase().includes("ADMIN")
            );
        }
    } catch {
        isAdminUser = false;
    }
    // ===== Related slider =====
    const totalRelated = relatedPosts.length;
    const relatedPageCount =
        totalRelated > 0 ? Math.ceil(totalRelated / RELATED_PAGE_SIZE) : 0;

    const safeRelatedPage =
        relatedPageCount > 0 ? Math.min(relatedPage, relatedPageCount) : 1;

    const visibleRelated = useMemo(() => {
        if (relatedPageCount === 0) return [];
        const start = (safeRelatedPage - 1) * RELATED_PAGE_SIZE;
        return relatedPosts.slice(start, start + RELATED_PAGE_SIZE);
    }, [relatedPosts, safeRelatedPage, relatedPageCount]);

    // ===== Loading / Error =====
    if (loading) {
        return (
            <div className="blog-detail-container">
                <div style={{ textAlign: "center", padding: "60px 20px" }}>
                    <div className="loading-spinner" />
                    <div style={{ marginTop: "16px" }}>Đang tải bài viết...</div>
                </div>
            </div>
        );
    }

    if (error || !post) {
        return (
            <div className="blog-detail-container">
                <div style={{ textAlign: "center", padding: "60px 20px" }}>
                    <div style={{ fontSize: "48px", marginBottom: "16px" }}>📄</div>
                    <h2>Không tìm thấy bài viết</h2>
                    <p style={{ color: "#666", marginBottom: "24px" }}>{error}</p>
                    <button className="btn primary" onClick={() => navigate("/blogs")}>
                        ← Quay lại danh sách
                    </button>
                </div>
            </div>
        );
    }

    // ===== Main render =====
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

            {/* Post Article */}
            <article className="post-article">
                <h1 className="post-title">{post.title}</h1>

                <div className="post-meta">
                    <span>{formatDate(post.createdAt)}</span>
                    <span>{post.authorName || "Admin"}</span>
                    {post.viewCount && <span>{post.viewCount} lượt xem</span>}
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
                        {post.tags.map((tag) => (
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
                        onClick={() => sharePost("facebook")}
                        title="Share on Facebook"
                    >
                        📘
                    </button>
                    <button
                        className="share-btn"
                        onClick={() => sharePost("twitter")}
                        title="Share on Twitter"
                    >
                        🐦
                    </button>
                    <button
                        className="share-btn"
                        onClick={() => sharePost("linkedin")}
                        title="Share on LinkedIn"
                    >
                        💼
                    </button>
                    <button
                        className="share-btn"
                        onClick={() => sharePost("copy")}
                        title="Copy link"
                    >
                        🔗
                    </button>
                </div>
            </article>

            {/* ===== Comments ===== */}
            <section className="comments-section">
                <h3 className="comments-title">
                    Bình luận
                    {comments.length > 0 && <span> ({comments.length})</span>}
                </h3>

                {commentsLoading ? (
                    <div className="comments-loading">Đang tải bình luận...</div>
                ) : topLevelComments.length === 0 ? (
                    <p className="comments-empty">
                        Chưa có bình luận nào. Hãy là người đầu tiên!
                    </p>
                ) : (
                    <ul className="comment-list">
                        {topLevelComments.map((c) => {
                            const replies = repliesByParent[c.id] || [];
                            const isExpanded = !!expandedReplies[c.id];

                            return (
                                <li key={c.id} className="comment-item">
                                    <div className="comment-header">
                                        <div className="comment-author-block">
                                            <span className="comment-author">{c.userName}</span>
                                            {c.userEmail && (
                                                <span className="comment-email">({c.userEmail})</span>
                                            )}
                                        </div>
                                        <span className="comment-date">
                                            {formatDate(c.createdAt)}
                                        </span>
                                    </div>

                                    <p className="comment-content">{c.content}</p>

                                    <div className="comment-actions">
                                        <button
                                            type="button"
                                            className="comment-reply-btn"
                                            onClick={() => handleClickReply(c)}
                                        >
                                            ↪ Phản hồi
                                        </button>

                                        {replies.length > 0 && (
                                            <button
                                                type="button"
                                                className="comment-replies-toggle"
                                                onClick={() => toggleRepliesForComment(c.id)}
                                            >
                                                {isExpanded
                                                    ? `Ẩn ${replies.length} phản hồi`
                                                    : `${replies.length} phản hồi`}
                                            </button>
                                        )}
                                    </div>

                                    {isExpanded && replies.length > 0 && (
                                        <ul className="comment-replies-list">
                                            {replies.map((r) => (
                                                <li
                                                    key={r.id}
                                                    className="comment-item comment-item--child"
                                                >
                                                    <div className="comment-header">
                                                        <div className="comment-author-block">
                                                            <span className="comment-author">
                                                                {r.userName}
                                                            </span>
                                                            {r.userEmail && (
                                                                <span className="comment-email">
                                                                    ({r.userEmail})
                                                                </span>
                                                            )}
                                                        </div>
                                                        <span className="comment-date">
                                                            {formatDate(r.createdAt)}
                                                        </span>
                                                    </div>
                                                    <p className="comment-content">{r.content}</p>

                                                    <div className="comment-actions">
                                                        <button
                                                            type="button"
                                                            className="comment-reply-btn"
                                                            onClick={() => handleClickReply(r)}
                                                        >
                                                            ↪ Phản hồi
                                                        </button>
                                                    </div>
                                                </li>
                                            ))}
                                        </ul>
                                    )}
                                </li>
                            );
                        })}
                    </ul>
                )}

                {commentTotalPages > 1 && (
                    <div className="comments-pagination">
                        <button
                            className="comments-page-btn"
                            disabled={commentPage === 1}
                            onClick={() => handleChangeCommentPage(commentPage - 1)}
                        >
                            Trang trước
                        </button>
                        <span className="comments-page-info">
                            Trang {commentPage} / {commentTotalPages}
                        </span>
                        <button
                            className="comments-page-btn"
                            disabled={commentPage === commentTotalPages}
                            onClick={() => handleChangeCommentPage(commentPage + 1)}
                        >
                            Trang sau
                        </button>
                    </div>
                )}

                <div className="comment-form-wrapper">
                    {currentUser ? (
                        <>
                            <div className="comment-form-user">
                                Đang đăng nhập:&nbsp;
                                <strong>
                                    {currentUser.fullName ||
                                        currentUser.FullName ||
                                        currentUser.email ||
                                        currentUser.Email}
                                </strong>
                            </div>

                            {/* Preview comment đang rep (kiểu Zalo) */}
                            {replyTargetComment && (
                                <div className="reply-preview">
                                    <div className="reply-preview-header">
                                        <span className="reply-preview-label">Đang phản hồi</span>
                                        <button
                                            type="button"
                                            className="reply-preview-close"
                                            onClick={clearReplyTarget}
                                        >
                                            ✕
                                        </button>
                                    </div>
                                    <div className="reply-preview-user">
                                        @{replyTargetComment.userName}
                                    </div>
                                    <div className="reply-preview-content">
                                        {truncateText(replyTargetComment.content, 120)}
                                    </div>
                                </div>
                            )}

                            <form className="comment-form" onSubmit={handleSubmitComment}>
                                <textarea
                                    className="comment-textarea"
                                    rows={3}
                                    placeholder={
                                        replyTargetComment
                                            ? `Phản hồi ${replyTargetComment.userName}...`
                                            : "Nhập bình luận của bạn..."
                                    }
                                    value={newComment}
                                    onChange={(e) => setNewComment(e.target.value)}
                                />
                                <button
                                    type="submit"
                                    className="btn comment-submit-btn"
                                    disabled={newCommentLoading || !newComment.trim()}
                                >
                                    {newCommentLoading ? "Đang gửi..." : "Gửi bình luận"}
                                </button>
                            </form>
                        </>
                    ) : (
                        <p className="comments-login-hint">
                            Bạn cần đăng nhập để viết bình luận.
                        </p>
                    )}
                </div>
            </section>

            {/* ===== Related posts slider ===== */}
            {totalRelated > 0 && (
                <section className="related-posts">
                    <div className="related-header">
                        <div className="related-title-box">BÀI VIẾT LIÊN QUAN</div>
                        <div className="related-header-line" />
                    </div>

                    <div className="related-row">
                        {visibleRelated.map((relatedPost) => (
                            <Link
                                key={relatedPost.postId}
                                to={`/blog/${relatedPost.slug}`}
                                className="related-card"
                            >
                                <div className="related-thumb-wrapper">
                                    {relatedPost.thumbnail && (
                                        <img
                                            src={relatedPost.thumbnail}
                                            alt={relatedPost.title}
                                            className="related-thumb"
                                        />
                                    )}

                                    {relatedPost.postTypeName && (
                                        <span className="related-tag-chip">
                                            {relatedPost.postTypeName}
                                        </span>
                                    )}
                                </div>

                                <h4 className="related-title">{relatedPost.title}</h4>

                                {relatedPost.shortDescription && (
                                    <p className="related-desc">
                                        {relatedPost.shortDescription}
                                    </p>
                                )}

                                <div className="related-meta-row">
                                    <span className="related-date">
                                        {formatDate(relatedPost.createdAt)}
                                    </span>

                                    {typeof relatedPost.viewCount === "number" && (
                                        <span className="related-views">
                                            {relatedPost.viewCount}
                                        </span>
                                    )}
                                </div>
                            </Link>
                        ))}
                    </div>

                    {relatedPageCount > 1 && (
                        <div className="related-controls">
                            <button
                                className="related-arrow-btn"
                                disabled={safeRelatedPage <= 1}
                                onClick={() =>
                                    setRelatedPage((prev) => Math.max(1, prev - 1))
                                }
                            >
                                &#60;
                            </button>
                            <button
                                className="related-arrow-btn"
                                disabled={safeRelatedPage >= relatedPageCount}
                                onClick={() =>
                                    setRelatedPage((prev) =>
                                        Math.min(relatedPageCount, prev + 1)
                                    )
                                }
                            >
                                &#62;
                            </button>
                        </div>
                    )}
                </section>
            )}

            {/* Navigation back */}
            <div className="post-navigation">
                <button className="btn" onClick={() => navigate("/blogs")}>
                    ← Quay lại danh sách
                </button>
            </div>
        </div>
    );
};

export default BlogDetail;
