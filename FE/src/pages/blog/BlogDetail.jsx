// // pages/BlogDetail/BlogDetail.jsx

// import React, { useEffect, useState, useMemo } from "react";
// import { useParams, Link, useNavigate } from "react-router-dom";
// import { postsApi } from "../../services/postsApi";
// import "../../styles/BlogDetail.css";

// const RELATED_PAGE_SIZE = 3;
// const COMMENT_PAGE_SIZE = 10;

// const BlogDetail = () => {
//     const { slug } = useParams();
//     const navigate = useNavigate();

//     const [post, setPost] = useState(null);

//     // Related posts
//     const [relatedPosts, setRelatedPosts] = useState([]);
//     const [relatedPage, setRelatedPage] = useState(1);

//     // Comments
//     const [comments, setComments] = useState([]);
//     const [commentPage, setCommentPage] = useState(1);
//     const [commentTotalPages, setCommentTotalPages] = useState(1);
//     const [commentsLoading, setCommentsLoading] = useState(false);

//     const [newComment, setNewComment] = useState("");
//     const [newCommentLoading, setNewCommentLoading] = useState(false);

//     // Reply state: chỉ cho phép mở 1 form reply tại 1 comment
//     const [replyTargetId, setReplyTargetId] = useState(null);
//     const [replyContent, setReplyContent] = useState("");

//     const [currentUser, setCurrentUser] = useState(null);

//     // General state
//     const [loading, setLoading] = useState(true);
//     const [error, setError] = useState("");

//     // Đọc user từ localStorage (để gửi comment / reply)
//     useEffect(() => {
//         try {
//             const raw = localStorage.getItem("user");
//             if (raw) {
//                 setCurrentUser(JSON.parse(raw));
//             }
//         } catch (e) {
//             console.error("Cannot parse user from localStorage", e);
//         }
//     }, []);

//     useEffect(() => {
//         setRelatedPage(1);
//         loadPost();
//         // eslint-disable-next-line react-hooks/exhaustive-deps
//     }, [slug]);

//     const formatDate = (dateString) => {
//         if (!dateString) return "";
//         try {
//             const date = new Date(dateString);
//             return date.toLocaleDateString("vi-VN", {
//                 year: "numeric",
//                 month: "2-digit",
//                 day: "2-digit",
//             });
//         } catch {
//             return "";
//         }
//     };

//     const loadComments = async (postIdValue, page = 1) => {
//         if (!postIdValue) return;

//         setCommentsLoading(true);
//         try {
//             const resp = await postsApi.getComments(
//                 postIdValue,
//                 page,
//                 COMMENT_PAGE_SIZE
//             );
//             const data = resp?.data || resp || {};

//             let items = [];
//             let pagination = null;

//             if (Array.isArray(data)) {
//                 items = data;
//             } else {
//                 items = data.comments || [];
//                 pagination = data.pagination || null;
//             }

//             // Chỉ hiển thị comment đã duyệt
//             const visible = (items || []).filter(
//                 (c) => (c.isApproved ?? c.IsApproved ?? false) === true
//             );

//             const normalized = visible.map((c) => ({
//                 id: c.commentId || c.CommentId,
//                 parentId: c.parentCommentId || c.ParentCommentId || null,
//                 userName: c.userName || c.UserName || "Ẩn danh",
//                 userEmail: c.userEmail || c.UserEmail || "",
//                 content: c.content || c.Content || "",
//                 createdAt: c.createdAt || c.CreatedAt,
//             }));

//             setComments(normalized);
//             setCommentPage(page);
//             setCommentTotalPages(
//                 pagination?.totalPages && pagination.totalPages > 0
//                     ? pagination.totalPages
//                     : 1
//             );
//         } catch (err) {
//             console.error("Error loading comments", err);
//         } finally {
//             setCommentsLoading(false);
//         }
//     };

//     const loadPost = async () => {
//         setLoading(true);
//         setError("");

//         try {
//             const postData = await postsApi.getPostBySlug(slug);

//             if (!postData) {
//                 setPost(null);
//                 setRelatedPosts([]);
//                 setComments([]);
//                 setError("Không tìm thấy bài viết");
//                 return;
//             }

//             setPost(postData);

//             // Related posts
//             if (postData.postId) {
//                 const related = await postsApi.getRelatedPosts(postData.postId, 20);

//                 const filtered = Array.isArray(related)
//                     ? related.filter((r) => r.postId !== postData.postId)
//                     : [];

//                 setRelatedPosts(filtered);
//                 setRelatedPage(1);

//                 // Comments cho bài viết
//                 await loadComments(postData.postId, 1);
//             } else {
//                 setRelatedPosts([]);
//                 setComments([]);
//             }
//         } catch (err) {
//             console.error("Load post error:", err);
//             setError(err.message || "Không thể tải bài viết");
//         } finally {
//             setLoading(false);
//         }
//     };

//     const sharePost = (platform) => {
//         const url = window.location.href;
//         const title = post?.title || "";

//         const shareUrls = {
//             facebook: `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(
//                 url
//             )}`,
//             twitter: `https://twitter.com/intent/tweet?url=${encodeURIComponent(
//                 url
//             )}&text=${encodeURIComponent(title)}`,
//             linkedin: `https://www.linkedin.com/shareArticle?mini=true&url=${encodeURIComponent(
//                 url
//             )}&title=${encodeURIComponent(title)}`,
//             copy: url,
//         };

//         if (platform === "copy") {
//             navigator.clipboard
//                 .writeText(url)
//                 .then(() => alert("Đã sao chép link!"))
//                 .catch(() => alert("Không thể sao chép"));
//         } else {
//             window.open(shareUrls[platform], "_blank", "width=600,height=400");
//         }
//     };

//     // Slider related posts
//     const totalRelated = relatedPosts.length;
//     const relatedPageCount =
//         totalRelated > 0 ? Math.ceil(totalRelated / RELATED_PAGE_SIZE) : 0;

//     const safeRelatedPage =
//         relatedPageCount > 0 ? Math.min(relatedPage, relatedPageCount) : 1;

//     const visibleRelated = useMemo(() => {
//         if (relatedPageCount === 0) return [];
//         const start = (safeRelatedPage - 1) * RELATED_PAGE_SIZE;
//         return relatedPosts.slice(start, start + RELATED_PAGE_SIZE);
//     }, [relatedPosts, safeRelatedPage, relatedPageCount]);

//     // Comment: đổi trang
//     const handleChangeCommentPage = (newPage) => {
//         if (!post?.postId) return;
//         if (newPage < 1 || newPage > commentTotalPages) return;
//         loadComments(post.postId, newPage);
//     };

//     // Comment: click "Phản hồi"
//     const handleClickReply = (commentId) => {
//         if (!currentUser) {
//             alert("Bạn cần đăng nhập để phản hồi.");
//             return;
//         }

//         // bấm lại cùng 1 comment thì đóng form
//         setReplyTargetId((prev) => (prev === commentId ? null : commentId));
//         setReplyContent("");
//     };

//     // Comment / Reply: gửi
//     const handleSubmitComment = async (e, parentId = null) => {
//         e.preventDefault();

//         const contentToSend = parentId ? replyContent : newComment;
//         if (!contentToSend.trim()) return;

//         if (!currentUser) {
//             alert("Bạn cần đăng nhập để bình luận.");
//             return;
//         }

//         try {
//             setNewCommentLoading(true);

//             const userId =
//                 currentUser.userId || currentUser.UserId || currentUser.id;

//             if (!userId) {
//                 alert("Không tìm thấy thông tin người dùng.");
//                 return;
//             }

//             await postsApi.createComment({
//                 postId: post.postId,
//                 userId,
//                 content: contentToSend.trim(),
//                 parentCommentId: parentId,
//             });

//             if (parentId) {
//                 setReplyContent("");
//                 setReplyTargetId(null);
//             } else {
//                 setNewComment("");
//             }

//             await loadComments(post.postId, 1);
//         } catch (err) {
//             console.error("Error creating comment", err);
//             alert("Không thể gửi bình luận. Vui lòng thử lại.");
//         } finally {
//             setNewCommentLoading(false);
//         }
//     };

//     // ===== Loading / Error =====
//     if (loading) {
//         return (
//             <div className="blog-detail-container">
//                 <div style={{ textAlign: "center", padding: "60px 20px" }}>
//                     <div className="loading-spinner" />
//                     <div style={{ marginTop: "16px" }}>Đang tải bài viết...</div>
//                 </div>
//             </div>
//         );
//     }

//     if (error || !post) {
//         return (
//             <div className="blog-detail-container">
//                 <div style={{ textAlign: "center", padding: "60px 20px" }}>
//                     <div style={{ fontSize: "48px", marginBottom: "16px" }}>📄</div>
//                     <h2>Không tìm thấy bài viết</h2>
//                     <p style={{ color: "#666", marginBottom: "24px" }}>{error}</p>
//                     <button className="btn primary" onClick={() => navigate("/blogs")}>
//                         ← Quay lại danh sách
//                     </button>
//                 </div>
//             </div>
//         );
//     }

//     // ===== Main render =====
//     return (
//         <div className="blog-detail-container">
//             {/* Admin Back Button */}
//             {isAdminUser && (
//                 <div style={{ marginBottom: '16px', padding: '12px', background: '#f5f5f5', borderRadius: '8px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
//                     <button 
//                         className="btn" 
//                         onClick={() => navigate('/admin-post-list')}
//                         style={{ background: '#007bff', color: 'white', border: 'none', padding: '8px 16px', borderRadius: '4px', cursor: 'pointer' }}
//                     >
//                         ← Quay lại quản lý bài viết
//                     </button>
//                     <span style={{ fontSize: '14px', color: '#666' }}>Chế độ xem trước (Admin)</span>
//                 </div>
//             )}
            
//             {/* Breadcrumb */}
//             <div className="breadcrumb">
//                 <Link to="/">Trang chủ</Link>
//                 <span> › </span>
//                 <Link to="/blogs">Blog</Link>
//                 <span> › </span>
//                 <span>{post.title}</span>
//             </div>

//             {/* Post Article */}
//             <article className="post-article">
//                 <h1 className="post-title">{post.title}</h1>

//                 <div className="post-meta">
//                     <span>{formatDate(post.createdAt)}</span>
//                     <span>{post.authorName || "Admin"}</span>
//                     {post.viewCount && <span>{post.viewCount} lượt xem</span>}
//                 </div>

//                 {/* Thumbnail */}
//                 {post.thumbnail && (
//                     <div className="post-thumbnail">
//                         <img src={post.thumbnail} alt={post.title} />
//                     </div>
//                 )}

//                 {/* Short Description */}
//                 {post.shortDescription && (
//                     <div className="post-intro">
//                         <p>{post.shortDescription}</p>
//                     </div>
//                 )}

//                 {/* Content */}
//                 <div
//                     className="post-content"
//                     dangerouslySetInnerHTML={{ __html: post.content }}
//                 />

//                 {/* Tags */}
//                 {post.tags && post.tags.length > 0 && (
//                     <div className="post-tags">
//                         <span>Tags:</span>
//                         {post.tags.map((tag) => (
//                             <Link
//                                 key={tag.tagId}
//                                 to={`/blogs?tag=${tag.slug}`}
//                                 className="tag-badge"
//                             >
//                                 #{tag.tagName}
//                             </Link>
//                         ))}
//                     </div>
//                 )}

//                 {/* Share Buttons */}
//                 <div className="post-share">
//                     <span>Chia sẻ:</span>
//                     <button
//                         className="share-btn"
//                         onClick={() => sharePost("facebook")}
//                         title="Share on Facebook"
//                     >
//                         📘
//                     </button>
//                     <button
//                         className="share-btn"
//                         onClick={() => sharePost("twitter")}
//                         title="Share on Twitter"
//                     >
//                         🐦
//                     </button>
//                     <button
//                         className="share-btn"
//                         onClick={() => sharePost("linkedin")}
//                         title="Share on LinkedIn"
//                     >
//                         💼
//                     </button>
//                     <button
//                         className="share-btn"
//                         onClick={() => sharePost("copy")}
//                         title="Copy link"
//                     >
//                         🔗
//                     </button>
//                 </div>
//             </article>

//             {/* ===== Comments ===== */}
//             <section className="comments-section">
//                 <h3 className="comments-title">
//                     Bình luận
//                     {comments.length > 0 && <span> ({comments.length})</span>}
//                 </h3>

//                 {commentsLoading ? (
//                     <div className="comments-loading">Đang tải bình luận...</div>
//                 ) : comments.length === 0 ? (
//                     <p className="comments-empty">
//                         Chưa có bình luận nào. Hãy là người đầu tiên!
//                     </p>
//                 ) : (
//                     <ul className="comment-list">
//                         {comments.map((c) => (
//                             <li
//                                 key={c.id}
//                                 className={
//                                     c.parentId
//                                         ? "comment-item comment-item--child"
//                                         : "comment-item"
//                                 }
//                             >
//                                 <div className="comment-header">
//                                     <div className="comment-author-block">
//                                         <span className="comment-author">{c.userName}</span>
//                                         {c.userEmail && (
//                                             <span className="comment-email">({c.userEmail})</span>
//                                         )}
//                                     </div>
//                                     <span className="comment-date">
//                                         {formatDate(c.createdAt)}
//                                     </span>
//                                 </div>

//                                 <p className="comment-content">{c.content}</p>

//                                 <div className="comment-actions">
//                                     <button
//                                         type="button"
//                                         className="comment-reply-btn"
//                                         onClick={() => handleClickReply(c.id)}
//                                     >
//                                         ↪ Phản hồi
//                                     </button>
//                                 </div>

//                                 {/* Reply form dưới mỗi comment */}
//                                 {currentUser && replyTargetId === c.id && (
//                                     <form
//                                         className="comment-reply-form"
//                                         onSubmit={(e) => handleSubmitComment(e, c.id)}
//                                     >
//                                         <textarea
//                                             className="comment-textarea comment-reply-textarea"
//                                             rows={2}
//                                             placeholder={`Phản hồi lại ${c.userName}...`}
//                                             value={replyContent}
//                                             onChange={(e) => setReplyContent(e.target.value)}
//                                         />
//                                         <button
//                                             type="submit"
//                                             className="btn comment-submit-btn"
//                                             disabled={newCommentLoading || !replyContent.trim()}
//                                         >
//                                             {newCommentLoading ? "Đang gửi..." : "Gửi phản hồi"}
//                                         </button>
//                                     </form>
//                                 )}
//                             </li>
//                         ))}
//                     </ul>
//                 )}

//                 {commentTotalPages > 1 && (
//                     <div className="comments-pagination">
//                         <button
//                             className="comments-page-btn"
//                             disabled={commentPage === 1}
//                             onClick={() => handleChangeCommentPage(commentPage - 1)}
//                         >
//                             Trang trước
//                         </button>
//                         <span className="comments-page-info">
//                             Trang {commentPage} / {commentTotalPages}
//                         </span>
//                         <button
//                             className="comments-page-btn"
//                             disabled={commentPage === commentTotalPages}
//                             onClick={() => handleChangeCommentPage(commentPage + 1)}
//                         >
//                             Trang sau
//                         </button>
//                     </div>
//                 )}

//                 <div className="comment-form-wrapper">
//                     {currentUser ? (
//                         <>
//                             <div className="comment-form-user">
//                                 Đang đăng nhập:&nbsp;
//                                 <strong>
//                                     {currentUser.fullName ||
//                                         currentUser.FullName ||
//                                         currentUser.email ||
//                                         currentUser.Email}
//                                 </strong>
//                             </div>

//                             <form
//                                 className="comment-form"
//                                 onSubmit={(e) => handleSubmitComment(e, null)}
//                             >
//                                 <textarea
//                                     className="comment-textarea"
//                                     rows={3}
//                                     placeholder="Nhập bình luận của bạn..."
//                                     value={newComment}
//                                     onChange={(e) => setNewComment(e.target.value)}
//                                 />
//                                 <button
//                                     type="submit"
//                                     className="btn comment-submit-btn"
//                                     disabled={newCommentLoading || !newComment.trim()}
//                                 >
//                                     {newCommentLoading ? "Đang gửi..." : "Gửi bình luận"}
//                                 </button>
//                             </form>
//                         </>
//                     ) : (
//                         <p className="comments-login-hint">
//                             Bạn cần đăng nhập để viết bình luận.
//                         </p>
//                     )}
//                 </div>
//             </section>

//             {/* ===== Related posts slider ===== */}
//             {totalRelated > 0 && (
//                 <section className="related-posts">
//                     <div className="related-header">
//                         <div className="related-title-box">BÀI VIẾT LIÊN QUAN</div>
//                         <div className="related-header-line" />
//                     </div>

//                     <div className="related-row">
//                         {visibleRelated.map((relatedPost) => (
//                             <Link
//                                 key={relatedPost.postId}
//                                 to={`/blog/${relatedPost.slug}`}
//                                 className="related-card"
//                             >
//                                 <div className="related-thumb-wrapper">
//                                     {relatedPost.thumbnail && (
//                                         <img
//                                             src={relatedPost.thumbnail}
//                                             alt={relatedPost.title}
//                                             className="related-thumb"
//                                         />
//                                     )}

//                                     {relatedPost.postTypeName && (
//                                         <span className="related-tag-chip">
//                                             {relatedPost.postTypeName}
//                                         </span>
//                                     )}
//                                 </div>

//                                 <h4 className="related-title">{relatedPost.title}</h4>

//                                 {relatedPost.shortDescription && (
//                                     <p className="related-desc">
//                                         {relatedPost.shortDescription}
//                                     </p>
//                                 )}

//                                 <div className="related-meta-row">
//                                     <span className="related-date">
//                                         {formatDate(relatedPost.createdAt)}
//                                     </span>

//                                     {typeof relatedPost.viewCount === "number" && (
//                                         <span className="related-views">
//                                             {relatedPost.viewCount}
//                                         </span>
//                                     )}
//                                 </div>
//                             </Link>
//                         ))}
//                     </div>

//                     {relatedPageCount > 1 && (
//                         <div className="related-controls">
//                             <button
//                                 className="related-arrow-btn"
//                                 disabled={safeRelatedPage <= 1}
//                                 onClick={() =>
//                                     setRelatedPage((prev) => Math.max(1, prev - 1))
//                                 }
//                             >
//                                 &#60;
//                             </button>
//                             <button
//                                 className="related-arrow-btn"
//                                 disabled={safeRelatedPage >= relatedPageCount}
//                                 onClick={() =>
//                                     setRelatedPage((prev) =>
//                                         Math.min(relatedPageCount, prev + 1)
//                                     )
//                                 }
//                             >
//                                 &#62;
//                             </button>
//                         </div>
//                     )}
//                 </section>
//             )}

//             {/* Navigation back */}
//             <div className="post-navigation">
//                 <button className="btn" onClick={() => navigate("/blogs")}>
//                     ← Quay lại danh sách
//                 </button>
//             </div>
//         </div>
//     );
// };

// export default BlogDetail;
