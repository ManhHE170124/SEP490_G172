/**
 * @file: CreateEditPost.jsx
 * @author: HieuNDHE173169
 * @created 2025-10-30
 * @lastUpdated 2025-10-30
 * @version 2.0.0
 * @summary Provides a full-featured post editor (for blog/guide management) with:
 * - Rich text editing (insert images, manage links)
 * - Tag and post type selection
 * - SEO metadata and status management
 * - Auto-save, preview, and publishing features
 * 
 * @returns
 * A React component that allows creating and editing posts with integrated tag, image, and SEO controls.
 */
import React, { useState, useRef, useEffect, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import './CreateEditPost.css';
import Quill from 'quill';
import 'quill/dist/quill.snow.css';
import { postsApi, extractPublicId } from '../../services/postsApi';
import useToast from '../../hooks/useToast';
import ToastContainer from '../../components/Toast/ToastContainer';
import TagsInput from '../../components/TagsInput/TagsInput';

const CreateEditPost = () => {
  const { postId } = useParams();
  const navigate = useNavigate();
  const isEditMode = Boolean(postId);

  const [title, setTitle] = useState('');
  const [metaTitle, setMetaTitle] = useState('');
  const [description, setDescription] = useState('');
  const [content, setContent] = useState('');
  const { toasts, showSuccess, showError, showInfo, removeToast, confirmDialog, showConfirm } = useToast();
  const [status, setStatus] = useState('Draft');
  const [posttypeId, setPosttypeId] = useState('');
  const [posttypes, setPosttypes] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState({ title: '', description: '' });
  const [tags, setTags] = useState([]);
  const [availableTags, setAvailableTags] = useState([]);
  const [featuredImage, setFeaturedImage] = useState(null);
  const [featuredImageUrl, setFeaturedImageUrl] = useState(null); // Cloudinary URL
  const [postSlug, setPostSlug] = useState(''); // Store slug for preview
  const [comments, setComments] = useState([]);
  const [commentsLoading, setCommentsLoading] = useState(false);
  const [commentFilter, setCommentFilter] = useState('all'); // 'all', 'visible', 'hidden'
  const [commentPage, setCommentPage] = useState(1);
  const [commentPageSize] = useState(20);
  const [commentTotalPages, setCommentTotalPages] = useState(1);
  const [commentTotalCount, setCommentTotalCount] = useState(0);
  const [replyingTo, setReplyingTo] = useState(null); // CommentId being replied to
  const [replyContent, setReplyContent] = useState('');
  const [replyLoading, setReplyLoading] = useState(false);
  const [newCommentContent, setNewCommentContent] = useState('');
  const [newCommentLoading, setNewCommentLoading] = useState(false);
  const [openDropdown, setOpenDropdown] = useState(null); // CommentId with open dropdown
  const [seoScore, setSeoScore] = useState(null); // null = chưa đánh giá, object = đã đánh giá
  const [isEvaluatingSeo, setIsEvaluatingSeo] = useState(false);


  const fileInputRef = useRef(null);
  const quillRef = useRef(null);
  const editorContainerRef = useRef(null);
  const imageInputRef = useRef(null);
  const prevImagesRef = useRef([]);

  // Reset form to initial state
  const resetForm = () => {
    setTitle('');
    setMetaTitle('');
    setDescription('');
    setContent('');
    setStatus('Draft');
    setPosttypeId('');
    setErrors({ title: '', description: '' });
    setTags([]);
    setFeaturedImage(null);
    setFeaturedImageUrl(null);
    setSeoScore(null);
    if (quillRef.current) {
      quillRef.current.clipboard.dangerouslyPasteHTML('<p></p>');
    }
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  // Reset form when switching from edit to create mode
  useEffect(() => {
    if (!isEditMode && !postId) {
      // Only reset if we're in create mode and there's no postId
      // This handles the case when navigating from edit to create
      // Check if form has data to avoid unnecessary resets
      if (title || metaTitle || description || content || posttypeId || tags.length > 0 || featuredImageUrl) {
        resetForm();
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isEditMode, postId]);

  // Global network error handler - only show one toast for network errors
  const networkErrorShownRef = useRef(false);
  useEffect(() => {
    // Reset the flag when component mounts or when network is restored
    networkErrorShownRef.current = false;
  }, []);

  // Fetch available tags
  useEffect(() => {
    const fetchTags = async () => {
      try {
        const tagsList = await postsApi.getTags();
        setAvailableTags(tagsList || []);
      } catch (err) {
        console.error('Failed to fetch tags:', err);
        // Handle network errors globally - only show one toast
        if (err.isNetworkError || err.message === 'Lỗi kết nối đến máy chủ') {
          if (!networkErrorShownRef.current) {
            networkErrorShownRef.current = true;
            showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
          }
        } else {
          showError('Lỗi tải tags', 'Không thể tải danh sách tags.');
        }
      }
    };

    fetchTags();
  }, [showError]);

  // Fetch post types
  useEffect(() => {
    const fetchPosttypes = async () => {
      try {
        setLoading(true);
        const types = await postsApi.getPosttypes();
        setPosttypes(types || []);
      } catch (err) {
        console.error('Failed to fetch post types:', err);
        // Handle network errors globally - only show one toast
        if (err.isNetworkError || err.message === 'Lỗi kết nối đến máy chủ') {
          if (!networkErrorShownRef.current) {
            networkErrorShownRef.current = true;
            showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
          }
        } else {
          showError('Lỗi tải danh mục', 'Không thể tải danh sách danh mục bài viết.');
        }
      } finally {
        setLoading(false);
      }
    };

    fetchPosttypes();
  }, [showError]);

  // Fetch post data in edit mode
  useEffect(() => {
    if (!isEditMode || !postId) return;

    const fetchPostData = async () => {
      try {
        setLoading(true);
        const postData = await postsApi.getPostById(postId);

        setTitle(postData.title || '');
        setMetaTitle(postData.metaTitle || '');
        setDescription(postData.shortDescription || '');
        setContent(postData.content || '');
        setStatus(postData.status || 'Draft');
        // Handle different possible property names for posttype ID
        setPosttypeId(postData.posttypeId || postData.postTypeId || postData.PosttypeId || '');
        setFeaturedImageUrl(postData.thumbnail || null);
        setFeaturedImage(postData.thumbnail || null);
        setPostSlug(postData.slug || postData.Slug || ''); // Store slug for preview

        if (postData.tags && Array.isArray(postData.tags)) {
          setTags(postData.tags);
        }

        if (quillRef.current && postData.content) {
          quillRef.current.clipboard.dangerouslyPasteHTML(postData.content);
        }
      } catch (err) {
        console.error('Failed to fetch post data:', err);
        // Handle network errors globally - only show one toast
        if (err.isNetworkError || err.message === 'Lỗi kết nối đến máy chủ') {
          if (!networkErrorShownRef.current) {
            networkErrorShownRef.current = true;
            showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
          }
        } else {
          showError('Lỗi tải bài viết', 'Không thể tải thông tin bài viết.');
        }
      } finally {
        setLoading(false);
      }
    };

    fetchPostData();
  }, [isEditMode, postId, showError]);

  // Fetch comments in edit mode
  useEffect(() => {
    if (!isEditMode || !postId) return;

    const fetchComments = async () => {
      try {
        setCommentsLoading(true);
        const response = await postsApi.getComments(postId, commentPage, commentPageSize);
        const data = response?.data || response || {};
        
        console.log('Comments API Response:', data); // Debug log
        
        // Handle paginated response
        if (data.comments) {
          setComments(data.comments || []);
          if (data.pagination) {
            const totalPages = data.pagination.totalPages || 1;
            const totalCount = data.pagination.totalCount || 0;
            console.log('Setting totalPages:', totalPages, 'totalCount:', totalCount); // Debug log
            setCommentTotalPages(totalPages);
            setCommentTotalCount(totalCount);
          } else {
            console.log('No pagination data, setting to 1'); // Debug log
            setCommentTotalPages(1);
            setCommentTotalCount(data.comments?.length || 0);
          }
        } else if (Array.isArray(data)) {
          // Fallback for non-paginated response
          console.log('Array response, setting to 1 page'); // Debug log
          setComments(data);
          setCommentTotalPages(1);
          setCommentTotalCount(data.length || 0);
        } else {
          console.log('Empty response, setting to 1 page'); // Debug log
          setComments([]);
          setCommentTotalPages(1);
          setCommentTotalCount(0);
        }
      } catch (err) {
        console.error('Failed to fetch comments:', err);
        // Handle network errors globally - only show one toast
        if (err.isNetworkError || err.message === 'Lỗi kết nối đến máy chủ') {
          if (!networkErrorShownRef.current) {
            networkErrorShownRef.current = true;
            showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
          }
        } else {
          showError('Lỗi tải bình luận', 'Không thể tải danh sách bình luận.');
        }
        setComments([]);
        setCommentTotalPages(1);
        setCommentTotalCount(0);
      } finally {
        setCommentsLoading(false);
      }
    };

    fetchComments();
  }, [isEditMode, postId, commentPage, commentPageSize, showError]);

  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (event) => {
      if (openDropdown && !event.target.closest('[data-dropdown]')) {
        setOpenDropdown(null);
      }
    };
    
    document.addEventListener('mousedown', handleClickOutside);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [openDropdown]);

  // Format date helper
  const formatDateTime = (value) => {
    if (!value) return "-";
    try {
      const date = new Date(value);
      if (Number.isNaN(date.getTime())) return "-";
      return date.toLocaleString("vi-VN", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit"
      });
    } catch {
      return "-";
    }
  };

  // Handle comment actions
  const handleShowComment = async (commentId) => {
    // Check if comment has a parent and if parent is hidden
    const comment = filteredComments.find(c => 
      (c.commentId || c.CommentId) === commentId
    );
    
    if (comment) {
      const parentId = comment.parentCommentId || comment.ParentCommentId;
      if (parentId) {
        const parent = filteredComments.find(c => 
          (c.commentId || c.CommentId) === parentId
        );
        
        if (parent && !(parent.isApproved ?? parent.IsApproved ?? false)) {
          showError('Lỗi', 'Không thể hiển thị bình luận con khi bình luận cha đang bị ẩn.');
          return;
        }
      }
    }

    try {
      await postsApi.showComment(commentId);
      showSuccess('Đã hiển thị', 'Bình luận đã được hiển thị.');
      // Refresh comments
      const response = await postsApi.getComments(postId, commentPage, commentPageSize);
      const data = response?.data || response || {};
      if (data.comments) {
        setComments(data.comments || []);
      } else if (Array.isArray(data)) {
        setComments(data);
      }
    } catch (err) {
      console.error('Failed to show comment:', err);
      showError('Lỗi hiển thị bình luận', err.response?.data?.message || 'Không thể hiển thị bình luận.');
    }
  };

  const handleHideComment = async (commentId) => {
    try {
      await postsApi.hideComment(commentId);
      showSuccess('Đã ẩn', 'Bình luận đã bị ẩn.');
      // Refresh comments
      const response = await postsApi.getComments(postId, commentPage, commentPageSize);
      const data = response?.data || response || {};
      if (data.comments) {
        setComments(data.comments || []);
      } else if (Array.isArray(data)) {
        setComments(data);
      }
    } catch (err) {
      console.error('Failed to hide comment:', err);
      showError('Lỗi ẩn bình luận', err.response?.data?.message || 'Không thể ẩn bình luận.');
    }
  };

  const handleDeleteComment = async (commentId) => {
    showConfirm(
      'Xác nhận xóa bình luận',
      'Bạn có chắc chắn muốn xóa bình luận này? Tất cả các bình luận con cũng sẽ bị xóa.',
      async () => {
        try {
          await postsApi.deleteComment(commentId);
          showSuccess('Đã xóa', 'Bình luận đã được xóa thành công.');
          // Refresh comments
          const response = await postsApi.getComments(postId, commentPage, commentPageSize);
          const data = response?.data || response || {};
          if (data.comments) {
            setComments(data.comments || []);
          } else if (Array.isArray(data)) {
            setComments(data);
          }
        } catch (err) {
          console.error('Failed to delete comment:', err);
          showError('Lỗi xóa bình luận', err.response?.data?.message || 'Không thể xóa bình luận.');
        }
      }
    );
  };

  // Handle reply to comment
  const handleReply = async (parentCommentId) => {
    if (!replyContent.trim()) {
      showError('Lỗi', 'Vui lòng nhập nội dung bình luận.');
      return;
    }

    // Check if parent comment is visible
    const parentComment = filteredComments.find(c => 
      (c.commentId || c.CommentId) === parentCommentId
    );
    
    if (parentComment && !(parentComment.isApproved ?? parentComment.IsApproved ?? false)) {
      showError('Lỗi', 'Không thể trả lời bình luận đã bị ẩn.');
      return;
    }

    try {
      setReplyLoading(true);
      // Get userId from localStorage
      let userId = null;
      try {
        const userStr = localStorage.getItem("user");
        if (userStr) {
          const user = JSON.parse(userStr);
          userId = user?.userId || user?.UserId || user?.id || null;
        }
      } catch (err) {
        console.error("Failed to parse user from localStorage:", err);
      }

      if (!userId) {
        showError('Lỗi', 'Không tìm thấy thông tin người dùng.');
        return;
      }

      await postsApi.createComment({
        postId: postId,
        userId: userId,
        content: replyContent.trim(),
        parentCommentId: parentCommentId
      });

      showSuccess('Thành công', 'Bình luận đã được gửi.');
      setReplyContent('');
      setReplyingTo(null);
      
      // Refresh comments
      const response = await postsApi.getComments(postId, commentPage, commentPageSize);
      const data = response?.data || response || {};
      if (data.comments) {
        setComments(data.comments || []);
      } else if (Array.isArray(data)) {
        setComments(data);
      }
    } catch (err) {
      console.error('Failed to reply comment:', err);
      showError('Lỗi gửi bình luận', err.response?.data?.message || 'Không thể gửi bình luận.');
    } finally {
      setReplyLoading(false);
    }
  };

  // Handle new comment (top-level)
  const handleNewComment = async () => {
    if (!newCommentContent.trim()) {
      showError('Lỗi', 'Vui lòng nhập nội dung bình luận.');
      return;
    }

    try {
      setNewCommentLoading(true);
      // Get userId from localStorage
      let userId = null;
      try {
        const userStr = localStorage.getItem("user");
        if (userStr) {
          const user = JSON.parse(userStr);
          userId = user?.userId || user?.UserId || user?.id || null;
        }
      } catch (err) {
        console.error("Failed to parse user from localStorage:", err);
      }

      if (!userId) {
        showError('Lỗi', 'Không tìm thấy thông tin người dùng.');
        return;
      }

      await postsApi.createComment({
        postId: postId,
        userId: userId,
        content: newCommentContent.trim(),
        parentCommentId: null
      });

      showSuccess('Thành công', 'Bình luận đã được gửi.');
      setNewCommentContent('');
      
      // Refresh comments and reset to first page
      setCommentPage(1);
      const response = await postsApi.getComments(postId, 1, commentPageSize);
      const data = response?.data || response || {};
      if (data.comments) {
        setComments(data.comments || []);
      } else if (Array.isArray(data)) {
        setComments(data);
      }
    } catch (err) {
      console.error('Failed to create comment:', err);
      showError('Lỗi gửi bình luận', err.response?.data?.message || 'Không thể gửi bình luận.');
    } finally {
      setNewCommentLoading(false);
    }
  };

  // Reset page when filter changes
  useEffect(() => {
    setCommentPage(1);
  }, [commentFilter]);

  // Filter comments based on filter state
  const filteredComments = useMemo(() => {
    if (commentFilter === 'all') return comments;
    if (commentFilter === 'visible') return comments.filter(c => c.isApproved ?? c.IsApproved ?? false);
    if (commentFilter === 'hidden') return comments.filter(c => !(c.isApproved ?? c.IsApproved ?? false));
    return comments;
  }, [comments, commentFilter]);

  // Count comments based on filter
  const commentCount = useMemo(() => {
    return filteredComments.length;
  }, [filteredComments]);

  // Group comments by thread (root parent)
  const groupedComments = useMemo(() => {
    const groups = new Map();
    const commentMap = new Map(); // Map commentId to comment for quick lookup
    
    // Build comment map
    filteredComments.forEach(comment => {
      const commentId = comment.commentId || comment.CommentId;
      commentMap.set(commentId, comment);
    });
    
    filteredComments.forEach(comment => {
      const commentId = comment.commentId || comment.CommentId;
      
      // Find root comment ID (traverse up the parent chain)
      let rootId = commentId;
      const visited = new Set();
      let current = comment;
      
      while (current) {
        const currentId = current.commentId || current.CommentId;
        if (visited.has(currentId)) break; // Prevent infinite loop
        visited.add(currentId);
        
        const parentId = current.parentCommentId || current.ParentCommentId;
        if (!parentId) {
          // This is a root comment
          rootId = currentId;
          break;
        }
        
        // Find parent in comment map
        const parent = commentMap.get(parentId);
        if (parent) {
          current = parent;
          // Continue traversing up
        } else {
          // Parent not in current page, but this comment has a parent
          // So the root is the first parent we can't find (it's on another page)
          // For now, treat this as its own root
          rootId = currentId;
          break;
        }
      }
      
      if (!groups.has(rootId)) {
        groups.set(rootId, []);
      }
      groups.get(rootId).push(comment);
    });
    
    // Sort each thread by creation time
    groups.forEach((thread, rootId) => {
      thread.sort((a, b) => {
        const timeA = new Date(a.createdAt || a.CreatedAt || 0).getTime();
        const timeB = new Date(b.createdAt || b.CreatedAt || 0).getTime();
        return timeA - timeB; // Chronological order
      });
    });
    
    return groups;
  }, [filteredComments]);

  // Render flat comment (no nesting)
  const renderComment = (comment, isLastInThread = false) => {
    const commentId = comment.commentId || comment.CommentId;
    const content = comment.content || comment.Content || '';
    const userName = comment.userName || comment.UserName || 'Không rõ';
    const userEmail = comment.userEmail || comment.UserEmail || '';
    const createdAt = comment.createdAt || comment.CreatedAt;
    const isVisible = comment.isApproved ?? comment.IsApproved ?? false;
    const parentId = comment.parentCommentId || comment.ParentCommentId;
    const isChild = !!parentId;

    return (
      <div 
        key={commentId} 
        style={{ 
          marginBottom: '12px',
          position: 'relative'
        }}
      >
        {isChild && (
          <div style={{
            position: 'absolute',
            left: 0,
            top: 0,
            bottom: 0,
            width: '32px',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            paddingTop: '8px'
          }}>
            {/* Curved arrow icon - vertical line, curved turn, horizontal line with arrowhead */}
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#d1d5db" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" style={{ flexShrink: 0 }}>
              {/* Vertical line from top, curved turn, horizontal line with arrowhead */}
              <path d="M8 2v8a4 4 0 0 0 4 4h10" />
              <path d="M20 12l-2 2 2 2" />
            </svg>
            {/* Vertical line */}
            <div style={{
              width: '2px',
              flex: 1,
              background: '#e5e7eb',
              marginTop: '4px',
              marginBottom: '8px'
            }} />
          </div>
        )}
        <div 
          style={{ 
            marginLeft: isChild ? '32px' : '0',
            padding: '12px', 
            border: '1px solid #e5e7eb', 
            borderRadius: '8px',
            background: isChild ? '#f9fafb' : '#ffffff',
            position: 'relative'
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', marginBottom: '8px' }}>
            <div style={{ flex: 1 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
                <span style={{ fontWeight: 600, fontSize: '14px' }}>{userName}</span>
              {userEmail && (
                <span style={{ fontSize: '12px', color: '#6b7280' }}>({userEmail})</span>
              )}
              {isVisible ? (
                <span style={{ padding: '2px 8px', background: '#d1fae5', color: '#065f46', borderRadius: '4px', fontSize: '11px', marginLeft: '8px' }}>
                  Hiển thị
                </span>
              ) : (
                <span style={{ padding: '2px 8px', background: '#fee2e2', color: '#991b1b', borderRadius: '4px', fontSize: '11px', marginLeft: '8px' }}>
                  Đã ẩn
                </span>
              )}
              </div>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <span style={{ fontSize: '12px', color: '#9ca3af' }}>
                {formatDateTime(createdAt)}
              </span>
              {/* Dropdown menu with three dots */}
              <div style={{ position: 'relative' }} data-dropdown>
                <button
                  onClick={() => setOpenDropdown(openDropdown === commentId ? null : commentId)}
                  style={{
                    background: 'none',
                    border: 'none',
                    cursor: 'pointer',
                    padding: '4px',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    color: '#6b7280'
                  }}
                >
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <circle cx="12" cy="5" r="1" />
                    <circle cx="12" cy="12" r="1" />
                    <circle cx="12" cy="19" r="1" />
                  </svg>
                </button>
                {openDropdown === commentId && (
                  <div style={{
                    position: 'absolute',
                    right: 0,
                    top: '100%',
                    marginTop: '4px',
                    background: '#ffffff',
                    border: '1px solid #e5e7eb',
                    borderRadius: '8px',
                    boxShadow: '0 4px 6px rgba(0, 0, 0, 0.1)',
                    zIndex: 1000,
                    minWidth: '120px'
                  }} data-dropdown>
                    {!isVisible ? (
                      <button
                        onClick={() => {
                          handleShowComment(commentId);
                          setOpenDropdown(null);
                        }}
                        style={{
                          width: '100%',
                          padding: '8px 12px',
                          textAlign: 'left',
                          background: 'none',
                          border: 'none',
                          cursor: 'pointer',
                          fontSize: '14px',
                          color: '#374151'
                        }}
                        onMouseEnter={(e) => e.target.style.background = '#f3f4f6'}
                        onMouseLeave={(e) => e.target.style.background = 'transparent'}
                      >
                        Hiển thị
                      </button>
                    ) : (
                      <button
                        onClick={() => {
                          handleHideComment(commentId);
                          setOpenDropdown(null);
                        }}
                        style={{
                          width: '100%',
                          padding: '8px 12px',
                          textAlign: 'left',
                          background: 'none',
                          border: 'none',
                          cursor: 'pointer',
                          fontSize: '14px',
                          color: '#374151'
                        }}
                        onMouseEnter={(e) => e.target.style.background = '#f3f4f6'}
                        onMouseLeave={(e) => e.target.style.background = 'transparent'}
                      >
                        Ẩn
                      </button>
                    )}
                    <button
                      onClick={() => {
                        handleDeleteComment(commentId);
                        setOpenDropdown(null);
                      }}
                      style={{
                        width: '100%',
                        padding: '8px 12px',
                        textAlign: 'left',
                        background: 'none',
                        border: 'none',
                        cursor: 'pointer',
                        fontSize: '14px',
                        color: '#dc2626',
                        borderTop: '1px solid #e5e7eb'
                      }}
                      onMouseEnter={(e) => e.target.style.background = '#fef2f2'}
                      onMouseLeave={(e) => e.target.style.background = 'transparent'}
                    >
                      Xóa
                    </button>
                  </div>
                )}
              </div>
            </div>
          </div>
          <div style={{ marginBottom: '8px', whiteSpace: 'pre-wrap', fontSize: '14px' }}>{content}</div>
        </div>
      </div>
    );
  };

  // Handle creating new tag with slug
  const handleCreateNewTag = async (tagName, slug) => {
    try {
      const newTag = await postsApi.createTag({
        tagName: tagName,
        slug: slug
      });
      const tagsList = await postsApi.getTags();
      setAvailableTags(tagsList || []);
      showSuccess('Tag mới', `Tag "${tagName}" đã được tạo thành công!`);
      return newTag;
    } catch (err) {
      console.error('Failed to create tag:', err);
      
      // Check if it's a permission error (403 Forbidden)
      if (err?.response?.status === 403) {
        const errorMessage = err?.response?.data?.message || 'Bạn không có quyền tạo tag mới. Vui lòng chọn tag từ danh sách có sẵn.';
        showError('Không có quyền', errorMessage);
        throw new Error(errorMessage);
      }
      
      // Other errors
      const errorMessage = err?.response?.data?.message || err?.message || 'Không thể tạo tag mới. Vui lòng thử lại.';
      showError('Lỗi tạo tag mới', errorMessage);
      throw new Error(errorMessage);
    }
  };

  // Featured image upload
  // Convert URL to File object
  const urlToFile = async (url) => {
    try {
      const response = await fetch(url);
      const blob = await response.blob();
      return new File([blob], 'image.' + blob.type.split('/')[1], { type: blob.type });
    } catch (error) {
      throw new Error('Không thể tải ảnh từ URL');
    }
  };

  // Handle file upload process
  const processImageUpload = async (file) => {
    try {
      // Show preview immediately
      const reader = new FileReader();
      reader.onload = (ev) => setFeaturedImage(ev.target.result);
      reader.readAsDataURL(file);

      // Upload to Cloudinary
      showSuccess('Đang tải ảnh...', 'Vui lòng đợi');
      const resp = await postsApi.uploadImage(file);

      let imageUrl = null;
      if (typeof resp === 'string') imageUrl = resp;
      else if (resp.path) imageUrl = resp.path;
      else if (resp.imageUrl) imageUrl = resp.imageUrl;
      else if (resp.url) imageUrl = resp.url;
      else if (resp.data && typeof resp.data === 'string') imageUrl = resp.data;
      else {
        const vals = Object.values(resp);
        if (vals.length && typeof vals[0] === 'string') imageUrl = vals[0];
      }

      if (!imageUrl) {
        throw new Error('Không thể lấy đường dẫn ảnh từ server');
      }

      setFeaturedImageUrl(imageUrl);
      showSuccess('Upload thành công', 'Ảnh đại diện đã được tải lên!');
    } catch (err) {
      console.error('Failed to upload thumbnail:', err);
      showError('Lỗi tải ảnh', err.message || 'Không thể tải ảnh đại diện lên.');
      setFeaturedImage(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  // Handle file input change
  const handleImageUpload = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    await processImageUpload(file);
  };

  // Handle drag and drop
  const handleDragOver = (e) => {
    e.preventDefault();
    e.stopPropagation();
  };

  const handleDrop = async (e) => {
    e.preventDefault();
    e.stopPropagation();

    const items = Array.from(e.dataTransfer.items);
    
    for (const item of items) {
      if (item.kind === 'file' && item.type.startsWith('image/')) {
        const file = item.getAsFile();
        await processImageUpload(file);
        break;
      } else if (item.kind === 'string' && item.type === 'text/uri-list') {
        item.getAsString(async (url) => {
          try {
            const file = await urlToFile(url);
            await processImageUpload(file);
          } catch (err) {
            showError('Lỗi tải ảnh', 'Không thể tải ảnh từ URL này.');
          }
        });
        break;
      }
    }
  };

  // Handle paste
  const handlePaste = async (e) => {
    const items = Array.from(e.clipboardData.items);

    for (const item of items) {
      if (item.kind === 'file' && item.type.startsWith('image/')) {
        const file = item.getAsFile();
        await processImageUpload(file);
        break;
      } else if (item.kind === 'string' && item.type === 'text/plain') {
        item.getAsString(async (text) => {
          if (text.match(/^https?:\/\/.+\.(jpg|jpeg|png|gif|webp)$/i)) {
            try {
              const file = await urlToFile(text);
              await processImageUpload(file);
            } catch (err) {
              showError('Lỗi tải ảnh', 'Không thể tải ảnh từ URL này.');
            }
          }
        });
        break;
      }
    }
  };

  const handleImageClick = () => {
    fileInputRef.current?.click();
  };

  const removeImage = async () => {
    if (featuredImageUrl) {
      try {
        const publicId = extractPublicId(featuredImageUrl);
        if (publicId) {
          await postsApi.deleteImage(publicId);
          showSuccess('Đã xóa', 'Ảnh đại diện đã được xóa.');
        }
      } catch (err) {
        console.error('Failed to delete thumbnail:', err);
        showError('Lỗi xóa ảnh', 'Không thể xóa ảnh khỏi server.');
      }
    }

    setFeaturedImage(null);
    setFeaturedImageUrl(null);
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  // Validation
  const validateForm = (postStatus = null) => {
    let isValid = true;
    const newErrors = { title: '', description: '' };

    // Title validation
    if (title.length === 0) {
      newErrors.title = 'Tiêu đề không được để trống';
      isValid = false;
    } else if (title.length < 10) {
      newErrors.title = 'Tiêu đề phải có ít nhất 10 ký tự';
      isValid = false;
    } else if (title.length > 250) {
      newErrors.title = 'Tiêu đề không được vượt quá 250 ký tự';
      isValid = false;
    }

    // Description validation
    if (description.length > 255) {
      newErrors.description = 'Mô tả không được vượt quá 255 ký tự';
      isValid = false;
    }

    // Content validation (required for publish)
    if ((postStatus === 'Published' || postStatus === 'Public') && (!content || content.trim() === '' || content === '<p></p>')) {
      showError('Lỗi validation', 'Nội dung bài viết không được để trống khi đăng bài.');
      isValid = false;
    }

    // PostType validation (required for publish)
    if ((postStatus === 'Published' || postStatus === 'Public') && !posttypeId) {
      showError('Lỗi validation', 'Vui lòng chọn danh mục bài viết khi đăng bài.');
      isValid = false;
    }

    setErrors(newErrors);
    return isValid;
  };

  // Save post (draft or publish)
  const handlePostAction = async (postStatus, successTitle, successMessage) => {
    if (!validateForm(postStatus)) {
      showError('Validation lỗi', 'Vui lòng kiểm tra lại thông tin bài viết.');
      return;
    }

    try {
      setSaving(true);

      // Get userId from localStorage
      let authorId = null;
      try {
        const userStr = localStorage.getItem("user");
        if (userStr) {
          const user = JSON.parse(userStr);
          authorId = user?.userId || user?.UserId || user?.id || null;
        }
      } catch (err) {
        console.error("Failed to parse user from localStorage:", err);
      }

      // Prepare tag IDs - ensure we get valid IDs
      const tagIds = tags
        .filter(t => t.tagId || t.TagId || t.id) // Handle different ID property names
        .map(t => t.tagId || t.TagId || t.id)
        .filter(id => id); // Filter out any undefined/null values

      const postData = {
        title: title.trim(),
        slug: toSlug(title),
        metaTitle: metaTitle.trim() || null,
        shortDescription: description.trim(),
        content: content || '<p></p>',
        thumbnail: featuredImageUrl || null,
        posttypeId: posttypeId || null,
        authorId: authorId,
        status: typeof postStatus === 'string' ? postStatus : postStatus.status,
        tagIds: tagIds.length > 0 ? tagIds : []
      };

      let result;
      if (isEditMode) {
        // Update existing post
        const updatedPost = await postsApi.updatePost(postId, postData);
        // Update slug if returned from API
        if (updatedPost?.slug || updatedPost?.Slug) {
          setPostSlug(updatedPost.slug || updatedPost.Slug);
        }
        showSuccess(
          successTitle,
          successMessage
        );
      } else {
        // Create new post
        result = await postsApi.createPost(postData);
        // Store slug if returned from API
        if (result?.slug || result?.Slug) {
          setPostSlug(result.slug || result.Slug);
        }
        showSuccess(
          successTitle,
          successMessage
        );

        if (result && result.postId) {
          setTimeout(() => {
            navigate(`/post-create-edit/${result.postId}`);
          }, 1500);
        }
      }
    } catch (err) {
      console.error(err);
      const errorMessage = err.response?.data?.message || err.message || '';
      
      // Kiểm tra lỗi trùng title
      if (errorMessage.includes('Tiêu đề đã tồn tại') || 
          errorMessage.toLowerCase().includes('duplicate') ||
          errorMessage.toLowerCase().includes('unique')) {
        setErrors(prev => ({ 
          ...prev, 
          title: 'Tiêu đề đã tồn tại. Vui lòng chọn tiêu đề khác.' 
        }));
        return; // Không show toast error chung
      }
      
      // Các lỗi khác vẫn show toast như cũ
      showError(
        'Lỗi xử lý bài viết',
        errorMessage || 'Không thể thực hiện thao tác. Vui lòng thử lại.'
      );
    } finally {
      setSaving(false);
    }
  };

  const handleSaveDraft = () =>
  handlePostAction(
    'Draft',
    'Lưu nháp thành công',
    'Bài viết đã được lưu nháp.'
  );

const handlePublish = () =>
  handlePostAction(
    'Published',
    'Đăng bài thành công',
    'Bài viết đã được đăng công khai!'
  );

  const handleSaveChange = () =>
  handlePostAction(
    status, // Use current status
    'Cập nhật thành công',
    'Cập nhật thông tin bài viết thành công'
  );

  const handlePreview = () => {
    
    // In edit mode, use stored slug
    if (isEditMode && postSlug) {
      window.open(`/blog/${postSlug}`, '_blank');
      return;
    }
    
    // In create mode, generate slug from title
    if (!isEditMode && title.trim()) {
      const slug = toSlug(title);
      if (slug) {
        // For new posts, we can't preview until saved, but we can show a message
        showInfo('Xem trước', 'Vui lòng lưu bài viết trước để có thể xem trước. Sau khi lưu, bạn có thể xem trước bài viết.');
      } else {
        showError('Lỗi', 'Vui lòng nhập tiêu đề bài viết trước.');
      }
      return;
    }
    
    showError('Lỗi', 'Không thể xem trước bài viết. Vui lòng kiểm tra lại thông tin.');
  };

  const handleDelete = () => {
    showConfirm(
      'Xác nhận xóa bài viết',
      `Bạn có chắc chắn muốn xóa bài viết "${title}"?\n\nHành động này sẽ xóa vĩnh viễn bài viết và tất cả ảnh liên quan. Không thể hoàn tác.`,
      async () => {
        try {
          setSaving(true);
          await postsApi.deletePost(postId);
          showSuccess('Đã xóa bài viết', 'Bài viết đã được xóa thành công.');
          setTimeout(() => {
            navigate('/post-create-edit');
          }, 1500);
        } catch (err) {
          console.error('Failed to delete post:', err);
          showError('Lỗi xóa bài viết', err.message || 'Không thể xóa bài viết. Vui lòng thử lại.');
        } finally {
          setSaving(false);
        }
      }
    );
  };

  const toSlug = (text) => {
  return text
    .normalize('NFD') 
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd').replace(/Đ/g, 'D') 
    .replace(/[^a-zA-Z0-9\s-]/g, '') 
    .trim() 
    .replace(/\s+/g, '-') 
    .replace(/-+/g, '-') 
    .toLowerCase(); 
};

  // Helper function để xác định màu sắc dựa trên điểm SEO (thang 10)
  const getSeoScoreTone = (score) => {
    if (score >= 9) return 'excellent';
    if (score >= 7) return 'good';
    if (score >= 5) return 'fair';
    return 'poor';
  };

  // Helper function để lấy nhãn điểm
  const getSeoScoreLabel = (score) => {
    if (score >= 9) return 'Xuất sắc';
    if (score >= 7) return 'Tốt';
    if (score >= 5) return 'Trung bình';
    return 'Cần cải thiện';
  };

  // Hàm phân tích Title
  const analyzeTitle = (title, metaTitle) => {
    let score = 0;
    const issues = [];
    const suggestions = [];

    if (!title || title.trim().length === 0) {
      issues.push('Chưa có tiêu đề');
      suggestions.push('Thêm tiêu đề cho bài viết');
      return { score: 0, issues, suggestions };
    }

    const titleLen = title.length;
    // Độ dài tối ưu 50-60 ký tự cho Google
    if (titleLen >= 50 && titleLen <= 60) {
      score += 4;
    } else if (titleLen < 50) {
      issues.push(`Tiêu đề quá ngắn (${titleLen} ký tự)`);
      suggestions.push('Tiêu đề nên có 50-60 ký tự để hiển thị tốt trên Google');
      score += 2;
    } else {
      issues.push(`Tiêu đề quá dài (${titleLen} ký tự)`);
      suggestions.push('Rút ngắn tiêu đề xuống 50-60 ký tự');
      score += 2;
    }

    // Kiểm tra chứa từ khóa chính
    if (metaTitle && title.toLowerCase().includes(metaTitle.toLowerCase())) {
      score += 3;
    } else if (metaTitle) {
      issues.push('Tiêu đề không chứa từ khóa chính');
      suggestions.push('Thêm từ khóa chính vào tiêu đề');
    }

    // Kiểm tra có số liệu
    const hasNumbers = /\d/.test(title);
    if (hasNumbers) {
      score += 2;
    } else {
      suggestions.push('Cân nhắc thêm số liệu vào tiêu đề (VD: "5 cách", "Top 10")');
      score += 1;
    }

    // Kiểm tra power words
    const hasPowerWords = /\b(tốt nhất|hướng dẫn|cách|mẹo|bí quyết|chuyên nghiệp|hiệu quả|nhanh|miễn phí|toàn diện|chi tiết)\b/i.test(title);
    if (hasPowerWords) {
      score += 1;
    } else {
      suggestions.push('Thêm từ khóa mạnh như "tốt nhất", "hướng dẫn", "hiệu quả"');
    }

    return { score: Math.min(10, score), issues, suggestions };
  };

  // Hàm phân tích Keywords (MetaTitle)
  const analyzeKeywords = (metaTitle) => {
    let score = 0;
    const issues = [];
    const suggestions = [];

    if (!metaTitle || metaTitle.trim().length === 0) {
      issues.push('Chưa có từ khóa');
      suggestions.push('Thêm 3-5 từ khóa liên quan đến nội dung');
      return { score: 0, issues, suggestions };
    }

    // Tách từ khóa bằng dấu phẩy
    const keywordList = metaTitle.split(',').map(k => k.trim()).filter(k => k);
    
    if (keywordList.length >= 3 && keywordList.length <= 5) {
      score += 5;
    } else if (keywordList.length < 3) {
      issues.push(`Số lượng từ khóa ít (${keywordList.length})`);
      suggestions.push('Thêm từ khóa để đạt 3-5 từ khóa');
      score += 2;
    } else {
      issues.push(`Quá nhiều từ khóa (${keywordList.length})`);
      suggestions.push('Giảm xuống 3-5 từ khóa chính');
      score += 3;
    }

    // Kiểm tra long-tail keywords (3+ từ)
    const hasLongTail = keywordList.some(k => k.split(' ').length >= 3);
    if (hasLongTail) {
      score += 3;
    } else {
      suggestions.push('Thêm từ khóa dài (long-tail) 3+ từ để tăng cơ hội xếp hạng');
      score += 1;
    }

    // Kiểm tra độ dài trung bình
    const avgLength = keywordList.reduce((sum, k) => sum + k.length, 0) / keywordList.length;
    if (avgLength >= 15) {
      score += 2;
    } else {
      suggestions.push('Sử dụng từ khóa cụ thể hơn');
      score += 1;
    }

    return { score: Math.min(10, score), issues, suggestions };
  };

  // Hàm phân tích Description
  const analyzeDescription = (description, metaTitle) => {
    let score = 0;
    const issues = [];
    const suggestions = [];

    if (!description || description.trim().length === 0) {
      issues.push('Chưa có mô tả');
      suggestions.push('Thêm mô tả 150-160 ký tự');
      return { score: 0, issues, suggestions };
    }

    const descLen = description.length;
    // Độ dài tối ưu 150-160 ký tự
    if (descLen >= 150 && descLen <= 160) {
      score += 4;
    } else if (descLen < 150) {
      issues.push(`Mô tả quá ngắn (${descLen} ký tự)`);
      suggestions.push('Mở rộng mô tả lên 150-160 ký tự');
      score += 2;
    } else {
      issues.push(`Mô tả quá dài (${descLen} ký tự)`);
      suggestions.push('Rút ngắn mô tả xuống 150-160 ký tự');
      score += 2;
    }

    // Kiểm tra chứa từ khóa
    if (metaTitle) {
      const keywordList = metaTitle.toLowerCase().split(',').map(k => k.trim()).filter(k => k);
      const descLower = description.toLowerCase();
      const hasKeyword = keywordList.length > 0 && keywordList.some(k => descLower.includes(k));
      
      if (hasKeyword) {
        score += 4;
      } else {
        issues.push('Mô tả không chứa từ khóa');
        suggestions.push('Thêm từ khóa chính vào mô tả');
        score += 1;
      }
    }

    // Kiểm tra CTA (Call to Action)
    const hasCTA = /\b(xem|đọc|tìm hiểu|khám phá|tải|nhận|ngay|chi tiết)\b/i.test(description);
    if (hasCTA) {
      score += 2;
    } else {
      suggestions.push('Thêm lời kêu gọi hành động (CTA) như "Tìm hiểu ngay", "Xem chi tiết"');
      score += 1;
    }

    return { score: Math.min(10, score), issues, suggestions };
  };

  // Hàm phân tích Content
  const analyzeContent = (content, metaTitle) => {
    let score = 0;
    const issues = [];
    const suggestions = [];

    if (!content || content.trim() === '' || content === '<p></p>') {
      issues.push('Chưa có nội dung');
      suggestions.push('Viết nội dung tối thiểu 800 từ');
      return { score: 0, issues, suggestions };
    }

    // Extract text from HTML
    const textContent = content.replace(/<[^>]*>/g, ' ').trim();
    const wordCount = textContent.split(/\s+/).filter(w => w.length > 0).length;
    
    // Độ dài nội dung
    if (wordCount >= 800) {
      score += 6;
    } else if (wordCount >= 500) {
      issues.push(`Nội dung hơi ngắn (${wordCount} từ)`);
      suggestions.push('Mở rộng nội dung lên 800+ từ để SEO tốt hơn');
      score += 4;
    } else {
      issues.push(`Nội dung quá ngắn (${wordCount} từ)`);
      suggestions.push('Viết thêm nội dung chi tiết, tối thiểu 800 từ');
      score += 2;
    }

    // Kiểm tra mật độ từ khóa (keyword density)
    if (metaTitle) {
      const keywordList = metaTitle.toLowerCase().split(',').map(k => k.trim()).filter(k => k);
      if (keywordList.length > 0) {
        const mainKeyword = keywordList[0];
        const contentLower = textContent.toLowerCase();
        const keywordCount = (contentLower.match(new RegExp(mainKeyword.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g')) || []).length;
        const density = wordCount > 0 ? (keywordCount / wordCount) * 100 : 0;
        
        if (density >= 1 && density <= 2.5) {
          score += 6;
        } else if (density < 1) {
          issues.push(`Mật độ từ khóa thấp (${density.toFixed(2)}%)`);
          suggestions.push(`Thêm từ khóa "${mainKeyword}" để đạt mật độ 1-2.5%`);
          score += 3;
        } else {
          issues.push(`Mật độ từ khóa cao (${density.toFixed(2)}%)`);
          suggestions.push('Giảm lặp lại từ khóa, tránh spam');
          score += 3;
        }
      }
    }

    // Kiểm tra headings
    const hasHeadings = /<h[1-6]>/i.test(content);
    if (hasHeadings) {
      score += 4;
    } else {
      issues.push('Nội dung thiếu tiêu đề phụ (H2, H3)');
      suggestions.push('Thêm tiêu đề phụ để cấu trúc nội dung rõ ràng');
      score += 1;
    }

    // Kiểm tra lists
    const hasList = /<[ou]l>/i.test(content) || /[-*]\s|\d+\.\s/.test(textContent);
    if (hasList) {
      score += 2;
    } else {
      suggestions.push('Thêm danh sách (bullet points) để dễ đọc');
      score += 1;
    }

    // Kiểm tra links
    const hasLinks = /<a\s+href/i.test(content);
    if (hasLinks) {
      score += 2;
    } else {
      suggestions.push('Thêm liên kết nội bộ và liên kết ngoài uy tín');
      score += 1;
    }

    return { score: Math.min(10, score), issues, suggestions };
  };

  // Hàm phân tích Category
  const analyzeCategory = (posttypeId, posttypes) => {
    let score = 0;
    const issues = [];
    const suggestions = [];

    if (!posttypeId || posttypeId.trim() === '') {
      issues.push('Chưa chọn danh mục');
      suggestions.push('Chọn danh mục phù hợp cho bài viết');
      return { score: 0, issues, suggestions };
    }

    // Tìm tên danh mục
    const category = posttypes.find(t => 
      (t.posttypeId || t.postTypeId || t.PosttypeId || t.id) === posttypeId
    );
    const categoryName = category ? (category.posttypeName || category.postTypeName || category.PosttypeName || category.PostTypeName || '') : '';

    if (categoryName && categoryName.length >= 3) {
      score += 10;
    } else {
      issues.push('Tên danh mục quá ngắn');
      suggestions.push('Chọn danh mục cụ thể hơn');
      score += 5;
    }

    return { score: Math.min(10, score), issues, suggestions };
  };

  // Hàm phân tích Tags
  const analyzeTags = (tags, metaTitle) => {
    let score = 0;
    const issues = [];
    const suggestions = [];

    if (!tags || tags.length === 0) {
      issues.push('Chưa có thẻ');
      suggestions.push('Thêm 3-7 thẻ liên quan');
      return { score: 0, issues, suggestions };
    }

    const tagList = Array.isArray(tags) ? tags : [];
    
    // Số lượng tags tối ưu 3-7
    if (tagList.length >= 3 && tagList.length <= 7) {
      score += 5;
    } else if (tagList.length < 3) {
      issues.push(`Số lượng thẻ ít (${tagList.length})`);
      suggestions.push('Thêm thẻ để đạt 3-7 thẻ');
      score += 2;
    } else {
      issues.push(`Quá nhiều thẻ (${tagList.length})`);
      suggestions.push('Giảm xuống 3-7 thẻ quan trọng nhất');
      score += 3;
    }

    // Kiểm tra tags liên quan đến keywords
    if (metaTitle) {
      const keywordList = metaTitle.toLowerCase().split(',').map(k => k.trim()).filter(k => k);
      const hasRelatedTags = tagList.some(tag => {
        const tagName = (tag.tagName || tag.name || tag || '').toLowerCase();
        return keywordList.some(kw => tagName.includes(kw) || kw.includes(tagName));
      });
      
      if (hasRelatedTags) {
        score += 5;
      } else {
        suggestions.push('Sử dụng thẻ liên quan đến từ khóa chính');
        score += 2;
      }
    }

    return { score: Math.min(10, score), issues, suggestions };
  };

  // Hàm phân tích Thumbnail
  const analyzeThumbnail = (featuredImageUrl) => {
    let score = 0;
    const issues = [];
    const suggestions = [];

    if (featuredImageUrl) {
      score += 10;
    } else {
      issues.push('Chưa có hình đại diện');
      suggestions.push('Thêm hình đại diện cho bài viết');
    }

    return { score: Math.min(10, score), issues, suggestions };
  };

  // Hàm tính điểm SEO tổng thể
  const calculateSeoScore = (data) => {
    const { title, metaTitle, description, content, posttypeId, tags, featuredImageUrl, posttypes } = data;

    // Phân tích từng yếu tố
    const scores = {
      title: analyzeTitle(title, metaTitle),
      keywords: analyzeKeywords(metaTitle),
      description: analyzeDescription(description, metaTitle),
      content: analyzeContent(content, metaTitle),
      category: analyzeCategory(posttypeId, posttypes || []),
      tags: analyzeTags(tags, metaTitle),
      thumbnail: analyzeThumbnail(featuredImageUrl)
    };

    // Tính điểm tổng theo trọng số
    const totalScore = (
      scores.title.score * 0.2 +
      scores.keywords.score * 0.15 +
      scores.description.score * 0.15 +
      scores.content.score * 0.35 +
      scores.category.score * 0.05 +
      scores.tags.score * 0.1 +
      scores.thumbnail.score * 0.1
    );

    // Thu thập tất cả issues và suggestions
    const allIssues = [];
    const allSuggestions = [];
    
    Object.entries(scores).forEach(([key, data]) => {
      if (data.issues && data.issues.length > 0) {
        allIssues.push(...data.issues);
      }
      if (data.suggestions && data.suggestions.length > 0) {
        allSuggestions.push(...data.suggestions);
      }
    });

    // Sắp xếp suggestions theo mức độ ưu tiên (yếu tố có điểm thấp nhất)
    const improvements = Object.entries(scores)
      .filter(([key, data]) => data.score < 8)
      .sort((a, b) => a[1].score - b[1].score)
      .slice(0, 5)
      .map(([key, data]) => ({
        field: key,
        score: data.score,
        suggestions: data.suggestions.slice(0, 3)
      }));

    return {
      score: Math.round(totalScore * 10) / 10, // Làm tròn 1 chữ số thập phân
      maxScore: 10,
      scores, // Chi tiết điểm từng yếu tố
      issues: allIssues,
      suggestions: allSuggestions.slice(0, 8), // Tối đa 8 suggestions
      improvements // Các cải thiện ưu tiên
    };
  };

  // Hàm xử lý đánh giá SEO
  const handleEvaluateSeo = () => {
    setIsEvaluatingSeo(true);
    
    // Sử dụng setTimeout để tạo delay và hiển thị loading
    setTimeout(() => {
      try {
        // Tính điểm SEO dựa trên dữ liệu hiện tại
        const result = calculateSeoScore({
          title,
          metaTitle,
          description,
          content,
          posttypeId,
          tags,
          featuredImageUrl,
          posttypes
        });
        
        setSeoScore(result);
        setIsEvaluatingSeo(false);
        
        // Hiển thị toast thông báo thành công
        const scoreLabel = getSeoScoreLabel(result.score);
        showSuccess(
          'Đánh giá SEO hoàn tất',
          `Điểm SEO của bạn: ${result.score}/10 - ${scoreLabel}`
        );
      } catch (error) {
        setIsEvaluatingSeo(false);
        showError('Lỗi đánh giá SEO', 'Đã có lỗi xảy ra khi đánh giá SEO. Vui lòng thử lại.');
      }
    }, 800); // Delay 800ms để hiển thị loading
  };

  // Quill setup
  useEffect(() => {
    console.log('Quill useEffect running');

    if (quillRef.current) {
      console.log('Quill already initialized');
      showError('Trình soạn thảo đã được khởi tạo', 'Trình soạn thảo nội dung chỉ được khởi tạo một lần.');
      return;
    }

    if (!editorContainerRef.current) return;

    const toolbarOptions = [
      ['bold', 'italic', 'underline', 'strike'],
      ['blockquote', 'code-block'],
      ['link', 'image', 'video', 'formula'],
      [{ 'header': 1 }, { 'header': 2 }],
      [{ 'list': 'ordered' }, { 'list': 'bullet' }, { 'list': 'check' }],
      [{ 'script': 'sub' }, { 'script': 'super' }],
      [{ 'indent': '-1' }, { 'indent': '+1' }],
      [{ 'direction': 'rtl' }],
      [{ 'size': ['small', false, 'large', 'huge'] }],
      [{ 'header': [1, 2, 3, 4, 5, 6, false] }],
      [{ 'color': [] }, { 'background': [] }],
      [{ 'font': [] }],
      [{ 'align': [] }],
      ['clean']
    ];

    const container = editorContainerRef.current;
    if (!container) return;

    try {
      const existingToolbar = container.querySelector('.ql-toolbar');
      const existingContainer = container.querySelector('.ql-container');
      if (existingToolbar || existingContainer) {
        container.innerHTML = '';
      }
    } catch (err) {
      // ignore
    }

    const mountNode = document.createElement('div');
    mountNode.className = 'quill-mount';
    container.appendChild(mountNode);

    const q = new Quill(mountNode, {
      modules: {
        toolbar: {
          container: toolbarOptions,
          handlers: {
            image: function () {
              imageInputRef.current && imageInputRef.current.click();
            }
          }
        }
      },
      theme: 'snow',
      placeholder: 'Nhập nội dung bài viết tại đây...'
    });

    quillRef.current = q;

    if (content) {
      q.clipboard.dangerouslyPasteHTML(content);
    }

    const getImageSrcsFromHTML = (html) => {
      try {
        const tmp = document.createElement('div');
        tmp.innerHTML = html || '';
        return Array.from(tmp.querySelectorAll('img')).map((i) => i.getAttribute('src'));
      } catch (err) {
        return [];
      }
    };

    try {
      prevImagesRef.current = getImageSrcsFromHTML(q.root.innerHTML);
    } catch (err) {
      prevImagesRef.current = [];
    }

    q.on('text-change', async () => {
      const html = q.root.innerHTML;
      setContent(html);

      try {
        const newImages = getImageSrcsFromHTML(html);
        const prevImages = prevImagesRef.current || [];
        const removed = prevImages.filter((src) => !newImages.includes(src));

        if (removed.length) {
          for (const src of removed) {
            if (!src || src.startsWith('data:')) continue;

            try {
              const publicId = extractPublicId(src);
              if (publicId) {
                postsApi.deleteImage(publicId).catch((err) => {
                  console.error('Failed to delete image:', err);
                  showError('Lỗi xóa ảnh', `Không thể xóa ảnh với ID: ${publicId}. Lỗi: ${err.message || err}`);
                });
              }
            } catch (err) {
              console.error('Error deleting image:', err);
              showError('Lỗi xóa ảnh', `Không thể xóa ảnh. Lỗi: ${err.message || err}`);
            }
          }
        }
        prevImagesRef.current = newImages;
      } catch (err) {
        console.error('Error processing image changes:', err);
        showError('Lỗi xử lý ảnh', `Có lỗi xảy ra khi xử lý ảnh trong nội dung. Lỗi: ${err.message || err}`);
      }
    });

    return () => {
      q.off('text-change');
      try {
        if (mountNode && mountNode.parentNode) {
          mountNode.parentNode.removeChild(mountNode);
        }
      } catch (err) {
        // ignore
      }
      quillRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleQuillImage = async (e) => {
    const file = e.target.files?.[0];
    if (!file || !quillRef.current) return;

    const range = quillRef.current.getSelection(true) || { index: 0 };

    try {
      const resp = await postsApi.uploadImage(file);

      let imageUrl = null;
      if (typeof resp === 'string') imageUrl = resp;
      else if (resp.path) imageUrl = resp.path;
      else if (resp.imageUrl) imageUrl = resp.imageUrl;
      else if (resp.url) imageUrl = resp.url;
      else if (resp.data && typeof resp.data === 'string') imageUrl = resp.data;
      else {
        const vals = Object.values(resp);
        if (vals.length && typeof vals[0] === 'string') imageUrl = vals[0];
      }

      if (!imageUrl) {
        throw new Error('Không thể lấy đường dẫn ảnh từ server');
      }

      quillRef.current.insertEmbed(range.index, 'image', imageUrl);
      quillRef.current.setSelection(range.index + 1);
      setContent(quillRef.current.root.innerHTML);
    } catch (err) {
      console.error('Image upload failed:', err);
      showError('Tải ảnh thất bại', err.message || 'Có lỗi xảy ra khi tải ảnh lên.');
    } finally {
      e.target.value = '';
    }
  };

  // Permission checks removed - BE handles authorization
  return (
    <main className="cep-main">
      {/* Page Header */}
      <div className="cep-page-header">
        <h1 className="cep-page-title">
          {isEditMode ? 'Cập nhật bài viết' : 'Tạo bài viết mới'}
        </h1>
      </div>
      
      <div className="cep-blog-create-container">
        {/* Left Column */}
        <div className="cep-main-content">
          {/* Title */}
          <div className="cep-post-title-section">
            <div className="cep-label-row">
              <label htmlFor="cep-post-title">Tiêu đề bài viết</label>
              <div className="cep-field-meta">{title.length}/250</div>
            </div>
            <input
              type="text"
              id="post-title"
              placeholder="Nhập tiêu đề"
              value={title}
              maxLength={250}
              onChange={(e) => {
                const newTitle = e.target.value;
                setTitle(newTitle);
                if (newTitle.length === 0) {
                  setErrors(prev => ({ ...prev, title: 'Tiêu đề không được để trống' }));
                } else if (newTitle.length < 10) {
                  setErrors(prev => ({ ...prev, title: 'Tiêu đề phải có ít nhất 10 ký tự' }));
                } else if (newTitle.length > 250) {
                  setErrors(prev => ({ ...prev, title: 'Tiêu đề không được vượt quá 250 ký tự' }));
                } else {
                  setErrors(prev => ({ ...prev, title: '' }));
                }
              }}
              className={errors.title ? 'error' : ''}
              disabled={saving}
            />
            {errors.title && <div className="cep-error-message">{errors.title}</div>}
          </div>

          {/* Meta Title */}
          <div className="cep-post-title-section">
            <div className="cep-label-row">
              <label htmlFor="cep-post-meta-title">Từ khóa bài viết</label>
              <div className="cep-field-meta">{metaTitle.length}/60</div>
            </div>
            <input
              type="text"
              id="post-meta-title"
              placeholder="Nhập từ khóa bài viết (Meta Title)"
              value={metaTitle}
              maxLength={60}
              onChange={(e) => {
                const newMetaTitle = e.target.value;
                setMetaTitle(newMetaTitle);
              }}
              disabled={saving}
            />
            <div className="cep-field-hint" style={{ fontSize: '12px', color: '#6b7280', marginTop: '4px' }}>
              Từ khóa này sẽ hiển thị trong thẻ title của trang web (SEO)
            </div>
          </div>

          {/* Description */}
          <div className="cep-post-description-section">
            <div className="cep-label-row">
              <label htmlFor="cep-post-description">Mô tả ngắn</label>
              <div className="cep-field-meta">{description.length}/255</div>
            </div>
            <textarea
              id="post-description"
              placeholder="Nhập mô tả ngắn cho bài viết"
              value={description}
              onChange={(e) => {
                const newDesc = e.target.value;
                setDescription(newDesc);
                if (newDesc.length > 255) {
                  setErrors(prev => ({ ...prev, description: 'Mô tả không được vượt quá 255 ký tự' }));
                } else {
                  setErrors(prev => ({ ...prev, description: '' }));
                }
              }}
              maxLength={256}
              rows={4}
              className={errors.description ? 'error' : ''}
              disabled={saving}
            />
            {errors.description && <div className="cep-error-message">{errors.description}</div>}
          </div>

          {/* Content */}
          <div className="cep-post-content-section">
            <label htmlFor="cep-post-content">Nội dung bài viết</label>
            <div className="cep-rich-text-editor">
              <div ref={editorContainerRef} className="cep-editor-content" />
              <input
                type="file"
                accept="image/*"
                ref={imageInputRef}
                style={{ display: 'none' }}
                onChange={handleQuillImage}
              />
            </div>
          </div>

          {/* Comments Section - Only in Edit Mode */}
          {isEditMode && (
            <div className="cep-post-content-section" style={{ marginTop: '32px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <label style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>
                  Bình luận ({commentTotalCount})
                </label>
                <div style={{ display: 'flex', gap: '8px' }}>
                  <button
                    className={commentFilter === 'all' ? 'btn primary' : 'btn secondary'}
                    style={{ fontSize: '12px', padding: '6px 12px' }}
                    onClick={() => setCommentFilter('all')}
                  >
                    Tất cả
                  </button>
                  <button
                    className={commentFilter === 'visible' ? 'btn primary' : 'btn secondary'}
                    style={{ fontSize: '12px', padding: '6px 12px' }}
                    onClick={() => setCommentFilter('visible')}
                  >
                    Không bị ẩn
                  </button>
                  <button
                    className={commentFilter === 'hidden' ? 'btn primary' : 'btn secondary'}
                    style={{ fontSize: '12px', padding: '6px 12px' }}
                    onClick={() => setCommentFilter('hidden')}
                  >
                    Bị ẩn
                  </button>
                </div>
              </div>
              {commentsLoading ? (
                <div className="loading-text" style={{ padding: '24px', textAlign: 'center' }}>Đang tải bình luận...</div>
              ) : filteredComments.length === 0 ? (
                <div style={{ padding: '24px', textAlign: 'center', color: '#6b7280', border: '1px solid #e5e7eb', borderRadius: '8px' }}>
                  Chưa có bình luận nào
                </div>
              ) : (
                <>
                  <div style={{ border: '1px solid #e5e7eb', borderRadius: '8px', padding: '16px', maxHeight: '600px', overflowY: 'auto' }}>
                    {Array.from(groupedComments.entries()).map(([rootId, thread]) => {
                      // Find root comment (first comment in thread, which has no parent)
                      const rootComment = thread.find(c => !(c.parentCommentId || c.ParentCommentId)) || thread[0];
                      const rootCommentId = rootComment?.commentId || rootComment?.CommentId || rootId;
                      const isRootVisible = rootComment ? (rootComment.isApproved ?? rootComment.IsApproved ?? false) : false;
                      
                      return (
                        <div key={rootId} style={{ marginBottom: '24px' }}>
                          {thread.map((comment) => renderComment(comment))}
                          {/* Reply button and form outside the last comment - reply to root comment */}
                          {isRootVisible && (
                            <div style={{ marginTop: '8px', display: 'flex', justifyContent: 'flex-end' }}>
                              <button
                                className="btn secondary"
                                style={{ fontSize: '12px', padding: '4px 8px' }}
                                onClick={() => setReplyingTo(replyingTo === rootCommentId ? null : rootCommentId)}
                              >
                                {replyingTo === rootCommentId ? 'Hủy' : 'Phản hồi'}
                              </button>
                            </div>
                          )}
                          {!isRootVisible && (
                            <div style={{ marginTop: '8px', display: 'flex', justifyContent: 'flex-end' }}>
                              <span style={{ fontSize: '12px', color: '#9ca3af', fontStyle: 'italic' }}>
                                Không thể trả lời bình luận đã bị ẩn
                              </span>
                            </div>
                          )}
                          {replyingTo === rootCommentId && isRootVisible && (
                            <div style={{ marginTop: '12px', padding: '12px', background: '#ffffff', borderRadius: '8px', border: '1px solid #e5e7eb' }}>
                              <textarea
                                value={replyContent}
                                onChange={(e) => setReplyContent(e.target.value)}
                                placeholder="Nhập bình luận của bạn..."
                                rows={3}
                                style={{
                                  width: '100%',
                                  padding: '8px',
                                  border: '1px solid #d1d5db',
                                  borderRadius: '4px',
                                  fontSize: '14px',
                                  resize: 'vertical',
                                  marginBottom: '8px',
                                  fontFamily: 'inherit'
                                }}
                              />
                              <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
                                <button
                                  className="btn secondary"
                                  style={{ fontSize: '12px', padding: '6px 12px' }}
                                  onClick={() => {
                                    setReplyingTo(null);
                                    setReplyContent('');
                                  }}
                                  disabled={replyLoading}
                                >
                                  Hủy
                                </button>
                                <button
                                  className="btn primary"
                                  style={{ fontSize: '12px', padding: '6px 12px' }}
                                  onClick={() => handleReply(rootCommentId)}
                                  disabled={replyLoading || !replyContent.trim()}
                                >
                                  {replyLoading ? 'Đang gửi...' : 'Gửi'}
                                </button>
                              </div>
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                  {/* Pagination */}
                  <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', marginTop: '16px', padding: '12px', background: '#f9fafb', borderRadius: '8px', border: '1px solid #e5e7eb' }}>
                    {commentTotalPages > 1 ? (
                      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '4px' }}>
                        {/* First page button */}
                        <button
                          className="btn secondary"
                          style={{ fontSize: '14px', padding: '8px 12px', minWidth: '40px' }}
                          onClick={() => setCommentPage(1)}
                          disabled={commentPage === 1 || commentsLoading}
                          title="Về đầu"
                        >
                          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M11 17l-5-5 5-5M18 17l-5-5 5-5" />
                          </svg>
                        </button>
                        {/* Previous page button */}
                        <button
                          className="btn secondary"
                          style={{ fontSize: '14px', padding: '8px 12px', minWidth: '40px' }}
                          onClick={() => setCommentPage(prev => Math.max(1, prev - 1))}
                          disabled={commentPage === 1 || commentsLoading}
                          title="Trước"
                        >
                          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M15 18l-6-6 6-6" />
                          </svg>
                        </button>
                        <div style={{ display: 'flex', gap: '4px', alignItems: 'center' }}>
                          {Array.from({ length: Math.min(commentTotalPages, 5) }, (_, i) => {
                            let pageNum;
                            if (commentTotalPages <= 5) {
                              pageNum = i + 1;
                            } else if (commentPage <= 3) {
                              pageNum = i + 1;
                            } else if (commentPage >= commentTotalPages - 2) {
                              pageNum = commentTotalPages - 4 + i;
                            } else {
                              pageNum = commentPage - 2 + i;
                            }
                            
                            return (
                              <button
                                key={pageNum}
                                className={commentPage === pageNum ? 'btn primary' : 'btn secondary'}
                                style={{ 
                                  fontSize: '14px', 
                                  padding: '8px 12px',
                                  minWidth: '40px'
                                }}
                                onClick={() => setCommentPage(pageNum)}
                                disabled={commentsLoading}
                              >
                                {pageNum}
                              </button>
                            );
                          })}
                          {commentTotalPages > 5 && commentPage < commentTotalPages - 2 && (
                            <>
                              <span style={{ color: '#9ca3af' }}>...</span>
                              <button
                                className="btn secondary"
                                style={{ fontSize: '14px', padding: '8px 12px', minWidth: '40px' }}
                                onClick={() => setCommentPage(commentTotalPages)}
                                disabled={commentsLoading}
                              >
                                {commentTotalPages}
                              </button>
                            </>
                          )}
                        </div>
                        {/* Next page button */}
                        <button
                          className="btn secondary"
                          style={{ fontSize: '14px', padding: '8px 12px', minWidth: '40px' }}
                          onClick={() => setCommentPage(prev => Math.min(commentTotalPages, prev + 1))}
                          disabled={commentPage >= commentTotalPages || commentsLoading}
                          title="Sau"
                        >
                          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M9 18l6-6-6-6" />
                          </svg>
                        </button>
                        {/* Last page button */}
                        <button
                          className="btn secondary"
                          style={{ fontSize: '14px', padding: '8px 12px', minWidth: '40px' }}
                          onClick={() => setCommentPage(commentTotalPages)}
                          disabled={commentPage >= commentTotalPages || commentsLoading}
                          title="Về cuối"
                        >
                          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M13 17l5-5-5-5M6 17l5-5-5-5" />
                          </svg>
                        </button>
                      </div>
                    ) : null}
                  </div>
                  
                  {/* New Comment Form - Always show when in edit mode */}
                  <div style={{ border: '1px solid #e5e7eb', borderRadius: '8px', padding: '16px', marginTop: '16px', background: '#ffffff' }}>
                    <label style={{ display: 'block', marginBottom: '8px', fontWeight: 600, fontSize: '14px' }}>
                      Đăng bình luận mới
                    </label>
                    <textarea
                      value={newCommentContent}
                      onChange={(e) => setNewCommentContent(e.target.value)}
                      placeholder="Nhập bình luận của bạn..."
                      rows={4}
                      style={{
                        width: '100%',
                        padding: '12px',
                        border: '1px solid #d1d5db',
                        borderRadius: '8px',
                        fontSize: '14px',
                        resize: 'vertical',
                        marginBottom: '12px',
                        fontFamily: 'inherit'
                      }}
                    />
                    <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
                      <button
                        className="btn primary"
                        style={{ fontSize: '14px', padding: '8px 16px' }}
                        onClick={handleNewComment}
                        disabled={newCommentLoading || !newCommentContent.trim()}
                      >
                        {newCommentLoading ? 'Đang gửi...' : 'Gửi bình luận'}
                      </button>
                    </div>
                  </div>
                </>
              )}
            </div>
          )}
        </div>

        {/* Right Column - Sidebar */}
        <div className="cep-sidebar-content">
          {/* Action Buttons */}
          <div className="cep-sidebar-section">
            <div className="cep-action-buttons">
              {!isEditMode && (
                <button
                  className="btn secondary"
                  onClick={handleSaveDraft}
                  disabled={saving}
                >
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                    <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                  {saving ? 'Đang lưu...' : 'Lưu nháp'}
                </button>
              )}
              {!isEditMode && (
                <button
                  className="btn primary"
                  onClick={handlePublish}
                  disabled={saving}
                >
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                    <path d="M12 19l7-7 3 3-7 7-3-3zM18 13l-1.5-7.5L2 2l3.5 14.5L13 18l5-5zM2 2l7.586 7.586" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                  {saving ? 'Đang lưu...' : 'Đăng bài'}
                </button>
              )}
              {isEditMode && (
                <button
                  className="btn primary"
                  onClick={handleSaveChange}
                  disabled={saving}
                >
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                    <path d="M12 19l7-7 3 3-7 7-3-3zM18 13l-1.5-7.5L2 2l3.5 14.5L13 18l5-5zM2 2l7.586 7.586" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                  {saving ? 'Đang lưu...' : 'Lưu thay đổi'}
                </button>
              )}
              {isEditMode && (
                <button
                  className="btn secondary"
                  onClick={handlePreview}
                  disabled={saving}
                >
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8S1 12 1 12Zm11 3a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
                  </svg>
                  Xem trước
                </button>
              )}
              {isEditMode && (
                <button
                  className="btn secondary"
                  onClick={() => {
                    resetForm();
                    navigate('/post-create-edit');
                  }}
                  disabled={saving}
                >
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                    <path d="M12 5v14M5 12h14" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                  Tạo bài viết mới
                </button>
              )}
            </div>

            {isEditMode && (
              <button
                className="btn danger"
                style={{ width: '100%', marginTop: '12px' }}
                onClick={handleDelete}
                disabled={saving}
              >
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                  <path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M10 11v6M14 11v6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
                {saving ? 'Đang xóa...' : 'Xóa bài'}
              </button>
            )}
          </div>

          {/* Status */}
          {isEditMode && (
            <div className="cep-sidebar-section">
              <label htmlFor="post-status">Trạng thái</label>
              <select
                id="post-status"
                value={status}
                onChange={(e) => setStatus(e.target.value)}
                disabled={saving}
              >
                <option value="Private">Riêng tư</option>
                <option value="Published">Công khai</option>
              </select>
            </div>
          )}

          {/* Category */}
          <div className="cep-sidebar-section">
            <label htmlFor="post-category">Danh mục</label>
            {loading ? (
              <div className="loading-text">Đang tải...</div>
            ) : (
              <select
                id="post-category"
                value={posttypeId}
                onChange={(e) => setPosttypeId(e.target.value)}
                disabled={saving}
              >
                <option value="">Chọn danh mục</option>
                {posttypes.map((type) => (
                  <option 
                    key={type.posttypeId || type.postTypeId || type.PosttypeId || type.id} 
                    value={type.posttypeId || type.postTypeId || type.PosttypeId || type.id}
                  >
                    {type.posttypeName || type.postTypeName || type.PosttypeName || type.PostTypeName}
                  </option>
                ))}
              </select>
            )}
          </div>

          {/* Tags */}
          <div className="cep-sidebar-section">
            <label>Tags</label>
            <TagsInput
              tags={tags}
              setTags={setTags}
              availableTags={availableTags}
              onCreateNewTag={handleCreateNewTag}
            />
          </div>

          {/* Featured Image */}
          <div className="cep-sidebar-section">
            <label htmlFor="featured-image">Hình đại diện</label>
            <input
              id="featured-image"
              type="file"
              ref={fileInputRef}
              style={{ display: 'none' }}
              onChange={handleImageUpload}
              accept="image/*"
            />
            <div
              className={`cep-featured-image-upload ${featuredImage ? 'has-image' : ''}`}
              onClick={handleImageClick}
              onDragOver={handleDragOver}
              onDrop={handleDrop}
              onPaste={handlePaste}
              tabIndex="0"
              role="button"
              style={{ outline: 'none' }}
            >
              {featuredImage ? (
                <img
                  src={featuredImage}
                  alt="Featured"
                  className="cep-featured-image-preview"
                />
              ) : (
                <div>
                  <div>Kéo thả ảnh vào đây</div>
                  <div>hoặc</div>
                  <div>Click để chọn ảnh</div>
                  <div>hoặc</div>
                  <div>Paste URL ảnh (Ctrl+V)</div>
                </div>
              )}
            </div>
            {featuredImage && (
              <button
                className="cep-remove-image-btn"
                onClick={removeImage}
                disabled={saving}
              >
                Xóa ảnh
              </button>
            )}
          </div>

          {/* SEO Score Section */}
          <div className="cep-sidebar-section">
            <label>Đánh giá SEO</label>
            
            {isEvaluatingSeo ? (
              // Đang đánh giá - hiển thị loading
              <div className="cep-seo-loading">
                <div className="cep-seo-loading-spinner"></div>
                <div className="cep-seo-loading-text">Đang phân tích SEO...</div>
              </div>
            ) : !seoScore ? (
              // Chưa đánh giá - hiển thị nút đánh giá
              <button
                className="cep-btn primary"
                onClick={handleEvaluateSeo}
                disabled={saving}
                style={{ width: '100%' }}
              >
                Đánh giá điểm
              </button>
            ) : (
              // Đã đánh giá - hiển thị kết quả
              <>
                <div className="cep-seo-score-header">
                  <div className={`cep-seo-score-badge cep-seo-score-${getSeoScoreTone(seoScore.score)}`}>
                    {seoScore.score}/10
                  </div>
                  <div className="cep-seo-score-label">{getSeoScoreLabel(seoScore.score)}</div>
                </div>
                
                {/* Progress Bar */}
                <div className="cep-seo-progress-bar">
                  <div 
                    className={`cep-seo-progress-fill cep-seo-progress-${getSeoScoreTone(seoScore.score)}`}
                    style={{ width: `${(seoScore.score / 10) * 100}%` }}
                  />
                </div>

                {/* Chi tiết điểm từng yếu tố */}
                {seoScore.scores && (
                  <div className="cep-seo-details">
                    <div className="cep-seo-details-title">Chi tiết điểm:</div>
                    <div className="cep-seo-details-grid">
                      {Object.entries({
                        title: 'Tiêu đề',
                        keywords: 'Từ khóa',
                        description: 'Mô tả',
                        content: 'Nội dung',
                        category: 'Danh mục',
                        tags: 'Thẻ',
                        thumbnail: 'Hình đại diện'
                      }).map(([key, label]) => {
                        const fieldScore = seoScore.scores[key];
                        if (!fieldScore) return null;
                        return (
                          <div key={key} className="cep-seo-detail-item">
                            <div className="cep-seo-detail-label">{label}</div>
                            <div className={`cep-seo-detail-score cep-seo-score-${getSeoScoreTone(fieldScore.score)}`}>
                              {fieldScore.score.toFixed(1)}/10
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}
                
                {/* Improvements - Các cải thiện ưu tiên */}
                {seoScore.improvements && seoScore.improvements.length > 0 && (
                  <div className="cep-seo-suggestions">
                    <div className="cep-seo-suggestions-title">Đề xuất cải thiện:</div>
                    {seoScore.improvements.map((improvement, idx) => {
                      const fieldLabels = {
                        title: 'Tiêu đề',
                        keywords: 'Từ khóa',
                        description: 'Mô tả',
                        content: 'Nội dung',
                        category: 'Danh mục',
                        tags: 'Thẻ',
                        thumbnail: 'Hình đại diện'
                      };
                      return (
                        <div key={idx} className="cep-seo-improvement-item">
                          <div className="cep-seo-improvement-header">
                            {fieldLabels[improvement.field]} (Điểm: {improvement.score.toFixed(1)}/10)
                          </div>
                          <ul className="cep-seo-suggestions-list">
                            {improvement.suggestions.map((suggestion, i) => (
                              <li key={i}>{suggestion}</li>
                            ))}
                          </ul>
                        </div>
                      );
                    })}
                  </div>
                )}
                
                {seoScore.score >= 9 && (
                  <div className="cep-seo-success-message">
                    ✓ Bài viết của bạn đã được tối ưu SEO tốt!
                  </div>
                )}
                
                {/* Nút đánh giá lại */}
                <button
                  className="cep-btn secondary"
                  onClick={handleEvaluateSeo}
                  disabled={isEvaluatingSeo || saving}
                  style={{ width: '100%', marginTop: '12px' }}
                >
                  {isEvaluatingSeo ? 'Đang đánh giá...' : 'Đánh giá lại'}
                </button>
              </>
            )}
          </div>
        </div>
      </div>
      <ToastContainer toasts={toasts} onRemove={removeToast} confirmDialog={confirmDialog} />
    </main>
  );
};

export default CreateEditPost;