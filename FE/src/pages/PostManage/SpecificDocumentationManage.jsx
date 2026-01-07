/**
 * File: SpecificDocumentationManage.jsx
 * Author: Keytietkiem Team
 * Created: 2025
 * Version: 2.0.0
 * Purpose: Admin page for managing SpecificDocumentation posts
 */
import React, { useState, useEffect, useRef } from 'react';
import { specificDocumentationApi } from '../../services/specificDocumentationApi';
import { postsApi, extractPublicId } from '../../services/postsApi';
import useToast from '../../hooks/useToast';
import ToastContainer from '../../components/Toast/ToastContainer';
import Quill from 'quill';
import 'quill/dist/quill.snow.css';
import mammoth from 'mammoth';
import './SpecificDocumentationManage.css';

// Helper function to generate slug from title
const toSlug = (text) => {
  if (!text) return '';
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

// Slug validation function
const validateSlug = (slugValue) => {
  // Chỉ cho phép: chữ cái (a-z, A-Z), số (0-9), dấu gạch ngang (-)
  // Không cho phép: dấu tiếng Việt, ký tự đặc biệt, khoảng cách
  const slugRegex = /^[a-zA-Z0-9-]+$/;
  if (!slugValue) return 'Slug không được để trống';
  if (!slugRegex.test(slugValue)) {
    return 'Slug chỉ được chứa chữ cái, số và dấu gạch ngang (-)';
  }
  if (slugValue.startsWith('-') || slugValue.endsWith('-')) {
    return 'Slug không được bắt đầu hoặc kết thúc bằng dấu gạch ngang';
  }
  if (slugValue.includes('--')) {
    return 'Slug không được chứa hai dấu gạch ngang liên tiếp';
  }
  return '';
};

// Helper function to remove RTL direction from HTML
const removeRTLDirection = (html) => {
  if (!html) return html;
  
  try {
    const tmp = document.createElement('div');
    tmp.innerHTML = html;
    
    // Remove dir="rtl" from all elements
    const allElements = tmp.querySelectorAll('[dir="rtl"]');
    allElements.forEach(el => {
      el.removeAttribute('dir');
    });
    
    // Also check root element
    if (tmp.firstElementChild && tmp.firstElementChild.getAttribute('dir') === 'rtl') {
      tmp.firstElementChild.removeAttribute('dir');
    }
    
    return tmp.innerHTML;
  } catch (err) {
    console.error('Error cleaning HTML:', err);
    return html;
  }
};

const SpecificDocumentationManage = () => {
  // List view state
  const [posts, setPosts] = useState([]);
  const [loading, setLoading] = useState(false);
  const [viewMode, setViewMode] = useState('list'); // 'list' or 'form'
  const [editingPostId, setEditingPostId] = useState(null);

  // Form state
  const [title, setTitle] = useState('');
  const [slug, setSlug] = useState('');
  const [content, setContent] = useState('');
  const [featuredImageUrl, setFeaturedImageUrl] = useState(null);
  const [featuredImage, setFeaturedImage] = useState(null);
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState({ title: '', slug: '' });
  const [postId, setPostId] = useState(null);
  const [postTypeId, setPostTypeId] = useState(null);

  const { toasts, showSuccess, showError, removeToast, showConfirm, confirmDialog } = useToast();
  
  const fileInputRef = useRef(null);
  const quillRef = useRef(null);
  const editorContainerRef = useRef(null);
  const imageInputRef = useRef(null);
  const docxInputRef = useRef(null);
  const prevImagesRef = useRef([]);
  const contentLoadedRef = useRef(false);
  const [editorReady, setEditorReady] = useState(false);

  // Get SpecificDocumentation PostTypeId
  const getSpecificDocumentationPostTypeId = async () => {
    try {
      const postTypes = await postsApi.getPosttypes();
      const postTypesArray = Array.isArray(postTypes) ? postTypes : (postTypes?.data || []);
      const specificDocType = postTypesArray.find(pt => {
        const name = (pt.postTypeName || pt.PostTypeName || '').toLowerCase();
        const slug = name.replace(/\s+/g, '-').replace(/_/g, '-');
        return slug === 'specific-documentation' || name === 'specificdocumentation';
      });
      return specificDocType?.postTypeId || specificDocType?.PostTypeId || specificDocType?.posttypeId || null;
    } catch (err) {
      console.error('Failed to fetch PostTypes:', err);
      return null;
    }
  };

  // Load all SpecificDocumentation posts
  const loadPosts = async () => {
    try {
      setLoading(true);
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
    } catch (err) {
      console.error('Failed to load posts:', err);
      showError('Lỗi tải dữ liệu', 'Không thể tải danh sách tài liệu.');
    } finally {
      setLoading(false);
    }
  };

  // Initialize PostTypeId on mount
  useEffect(() => {
    const initPostTypeId = async () => {
      const id = await getSpecificDocumentationPostTypeId();
      if (id) {
        setPostTypeId(id);
      } else {
        showError('Lỗi', 'Không tìm thấy PostType "SpecificDocumentation". Vui lòng tạo PostType trước.');
      }
    };
    initPostTypeId();
    loadPosts();
  }, []);

  // Initialize Quill editor
  useEffect(() => {
    if (quillRef.current || viewMode !== 'form') {
      return;
    }

    let scrollCleanup = null;
    const timer = setTimeout(() => {
      if (!editorContainerRef.current) {
        return;
      }

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
        placeholder: 'Nhập nội dung tại đây...'
      });

      quillRef.current = q;

      // Force LTR direction on initialization
      q.format('direction', 'ltr', 'user');

      // Prevent unwanted scroll
      const editorElement = q.root;
      if (editorElement) {
        let savedScrollPosition = window.pageYOffset || document.documentElement.scrollTop;
        let isUserScrolling = false;
        let scrollTimeout = null;
        
        const updateScrollPosition = () => {
          if (isUserScrolling) {
            savedScrollPosition = window.pageYOffset || document.documentElement.scrollTop;
          }
        };
        window.addEventListener('scroll', updateScrollPosition, { passive: true });
        
        const handleUserScroll = () => {
          isUserScrolling = true;
          if (scrollTimeout) clearTimeout(scrollTimeout);
          scrollTimeout = setTimeout(() => {
            isUserScrolling = false;
          }, 150);
        };
        window.addEventListener('wheel', handleUserScroll, { passive: true });
        window.addEventListener('touchmove', handleUserScroll, { passive: true });
        
        const originalScrollIntoView = editorElement.scrollIntoView;
        editorElement.scrollIntoView = function(options) {
          if (options && typeof options === 'object' && options.block === 'nearest') {
            return;
          }
          return;
        };
        
        const preventScrollOnInteraction = (e) => {
          const currentScroll = window.pageYOffset || document.documentElement.scrollTop;
          const restoreScroll = () => {
            const newScroll = window.pageYOffset || document.documentElement.scrollTop;
            if (Math.abs(newScroll - currentScroll) > 30) {
              window.scrollTo({
                top: currentScroll,
                behavior: 'instant'
              });
            }
          };
          restoreScroll();
          requestAnimationFrame(() => {
            restoreScroll();
            requestAnimationFrame(() => {
              restoreScroll();
            });
          });
        };
        
        const preventScrollOnSelection = () => {
          const currentScroll = window.pageYOffset || document.documentElement.scrollTop;
          requestAnimationFrame(() => {
            const newScroll = window.pageYOffset || document.documentElement.scrollTop;
            if (Math.abs(newScroll - currentScroll) > 30) {
              window.scrollTo({
                top: currentScroll,
                behavior: 'instant'
              });
            }
            requestAnimationFrame(() => {
              const finalScroll = window.pageYOffset || document.documentElement.scrollTop;
              if (Math.abs(finalScroll - currentScroll) > 30) {
                window.scrollTo({
                  top: currentScroll,
                  behavior: 'instant'
                });
              }
            });
          });
        };
        
        editorElement.addEventListener('focus', preventScrollOnInteraction, true);
        editorElement.addEventListener('mousedown', preventScrollOnInteraction, true);
        editorElement.addEventListener('click', preventScrollOnInteraction, true);
        q.on('selection-change', preventScrollOnSelection);
        
        scrollCleanup = () => {
          window.removeEventListener('scroll', updateScrollPosition);
          window.removeEventListener('wheel', handleUserScroll);
          window.removeEventListener('touchmove', handleUserScroll);
          editorElement.removeEventListener('focus', preventScrollOnInteraction, true);
          editorElement.removeEventListener('mousedown', preventScrollOnInteraction, true);
          editorElement.removeEventListener('click', preventScrollOnInteraction, true);
          q.off('selection-change', preventScrollOnSelection);
          if (scrollTimeout) clearTimeout(scrollTimeout);
          if (originalScrollIntoView) {
            editorElement.scrollIntoView = originalScrollIntoView;
          }
        };
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
                  });
                }
              } catch (err) {
                console.error('Error deleting image:', err);
              }
            }
          }
          prevImagesRef.current = newImages;
        } catch (err) {
          console.error('Error processing image changes:', err);
        }
      });

      setEditorReady(true);
    }, 100);

    return () => {
      clearTimeout(timer);
      if (scrollCleanup) {
        scrollCleanup();
      }
      if (quillRef.current) {
        quillRef.current.off('text-change');
        try {
          const mountNode = editorContainerRef.current?.querySelector('.quill-mount');
          if (mountNode && mountNode.parentNode) {
            mountNode.parentNode.removeChild(mountNode);
          }
        } catch (err) {
          // ignore
        }
        quillRef.current = null;
      }
    };
  }, [viewMode]);

  // Sync content to Quill when ready
  useEffect(() => {
    if (!editorReady || !contentLoadedRef.current || !quillRef.current || viewMode !== 'form') {
      return;
    }

    const timer = setTimeout(() => {
      if (quillRef.current) {
        // Clear editor first
        quillRef.current.root.innerHTML = '';
        
        if (content && content.trim() !== '' && content !== '<p></p>') {
          // Remove RTL direction from HTML before pasting
          const cleanedContent = removeRTLDirection(content);
          quillRef.current.clipboard.dangerouslyPasteHTML(cleanedContent);
          // Force LTR direction after pasting
          quillRef.current.format('direction', 'ltr', 'user');
        } else {
          quillRef.current.clipboard.dangerouslyPasteHTML('<p><br></p>');
          quillRef.current.format('direction', 'ltr', 'user');
        }
      }
    }, 100); // Increase delay to ensure editor is fully ready

    return () => clearTimeout(timer);
  }, [editorReady, content, viewMode]);

  // Slug must be entered manually - no auto-generation from title

  // Handle create new post
  const handleCreateNew = () => {
    setViewMode('form');
    setEditingPostId(null);
    setPostId(null);
    setTitle('');
    setSlug('');
    setContent('');
    setFeaturedImageUrl(null);
    setFeaturedImage(null);
    setErrors({ title: '', slug: '' });
    contentLoadedRef.current = false;
    setEditorReady(false);
  };

  // Handle edit post
  const handleEdit = async (post) => {
    try {
      setLoading(true);
      const postId = post.postId || post.PostId || post.id;
      const response = await specificDocumentationApi.getSpecificDocumentationBySlug(post.slug || post.Slug);
      const data = response?.data || response;
      
      setPostId(postId);
      setEditingPostId(postId);
      setTitle(data.title || '');
      setSlug(data.slug || '');
      setFeaturedImageUrl(data.thumbnail || null);
      setFeaturedImage(data.thumbnail || null);
      setErrors({ title: '', slug: '' });
      
      // Reset editor state before setting content
      setEditorReady(false);
      contentLoadedRef.current = false;
      
      // Set content after a small delay to ensure state is reset
      setTimeout(() => {
        setContent(data.content || '');
        contentLoadedRef.current = true;
        setViewMode('form');
      }, 50);
    } catch (err) {
      console.error('Failed to load post:', err);
      showError('Lỗi', 'Không thể tải thông tin bài viết.');
    } finally {
      setLoading(false);
    }
  };

  // Handle delete post
  const handleDelete = (post) => {
    const postId = post.postId || post.PostId || post.id;
    showConfirm(
      'Xác nhận xóa',
      `Bạn có chắc chắn muốn xóa bài viết "${post.title || post.Title}"?`,
      async () => {
        try {
          await specificDocumentationApi.deleteSpecificDocumentation(postId);
          showSuccess('Xóa thành công', 'Bài viết đã được xóa.');
          loadPosts();
        } catch (err) {
          console.error('Failed to delete post:', err);
          showError('Lỗi xóa', err.response?.data?.message || 'Không thể xóa bài viết.');
        }
      }
    );
  };

  // Validation
  const validateForm = () => {
    let isValid = true;
    const newErrors = { title: '', slug: '' };

    if (title.length === 0) {
      newErrors.title = 'Tiêu đề không được để trống';
      isValid = false;
    } else if (title.length > 250) {
      newErrors.title = 'Tiêu đề không được vượt quá 250 ký tự';
      isValid = false;
    }

    const slugError = validateSlug(slug);
    if (slugError) {
      newErrors.slug = slugError;
      isValid = false;
    }

    if (!content || content.trim() === '' || content === '<p></p>') {
      showError('Lỗi validation', 'Nội dung không được để trống.');
      isValid = false;
    }

    setErrors(newErrors);
    return isValid;
  };

  // Handle save
  const handleSave = async () => {
    if (!validateForm()) {
      return;
    }

    if (!postTypeId) {
      const id = await getSpecificDocumentationPostTypeId();
      if (!id) {
        showError('Lỗi', 'Không tìm thấy PostType "SpecificDocumentation". Vui lòng tạo PostType trước.');
        return;
      }
      setPostTypeId(id);
    }

    try {
      setSaving(true);

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

      // Use slug from state, validate it's not empty (slug must be entered manually)
      if (!slug || !slug.trim()) {
        showError('Lỗi validation', 'Slug không được để trống. Vui lòng nhập slug.');
        setSaving(false);
        return;
      }
      
      const postData = {
        title: title.trim(),
        slug: slug.trim(),
        shortDescription: '',
        content: content || '<p></p>',
        thumbnail: featuredImageUrl || null,
        authorId: authorId,
        posttypeId: postTypeId,
        status: 'Published'
      };

      if (postId) {
        // Update existing post
        await specificDocumentationApi.updateSpecificDocumentation(postId, postData);
        showSuccess('Lưu thành công', 'Tài liệu đã được cập nhật.');
      } else {
        // Create new post
        await specificDocumentationApi.createSpecificDocumentation(postData);
        showSuccess('Lưu thành công', 'Tài liệu đã được tạo mới.');
      }

      // Reload posts and switch to list view
      await loadPosts();
      setViewMode('list');
      setPostId(null);
      setEditingPostId(null);
    } catch (err) {
      console.error('Failed to save post:', err);
      const errorMessage = err.response?.data?.message || err.message || 'Không thể lưu thông tin.';
      showError('Lỗi lưu', errorMessage);
    } finally {
      setSaving(false);
    }
  };

  // Handle image upload
  const handleImageUpload = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    try {
      const reader = new FileReader();
      reader.onload = (ev) => setFeaturedImage(ev.target.result);
      reader.readAsDataURL(file);

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

  // Handle .docx import
  const handleDocxImport = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.name.toLowerCase().endsWith('.docx')) {
      showError('Lỗi', 'Vui lòng chọn file .docx');
      e.target.value = '';
      return;
    }

    try {
      showSuccess('Đang xử lý...', 'Đang chuyển đổi file Word sang HTML...');
      const arrayBuffer = await file.arrayBuffer();
      const result = await mammoth.convertToHtml({ arrayBuffer });
      const html = result.value;
      
      if (quillRef.current) {
        quillRef.current.clipboard.dangerouslyPasteHTML(html);
        setContent(quillRef.current.root.innerHTML);
        showSuccess('Import thành công', 'Nội dung từ file Word đã được chèn vào trình soạn thảo.');
      }
    } catch (err) {
      console.error('Docx import failed:', err);
      showError('Lỗi import', err.message || 'Không thể chuyển đổi file Word. Vui lòng thử lại.');
    } finally {
      e.target.value = '';
    }
  };

  // Handle Quill image upload
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
      showSuccess('Upload thành công', 'Ảnh đã được chèn vào nội dung.');
    } catch (err) {
      console.error('Image upload failed:', err);
      showError('Tải ảnh thất bại', err.message || 'Có lỗi xảy ra khi tải ảnh lên.');
    } finally {
      e.target.value = '';
    }
  };

  // Format date helper - treat as UTC if no timezone indicator
  const formatDate = (value) => {
    if (!value) return '';
    try {
      // Ensure the date string is treated as UTC by appending 'Z' if not present
      const dateString = typeof value === 'string' ? value : value.toString();
      const utcDateString = dateString.endsWith("Z") || dateString.includes("+") || dateString.includes("-", 10)
        ? dateString
        : `${dateString}Z`;
      const d = new Date(utcDateString);
      if (Number.isNaN(d.getTime())) return '';
      return d.toLocaleString('vi-VN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
      });
    } catch {
      return '';
    }
  };

  if (viewMode === 'form') {
    return (
      <div className="sdm-container">
        <div className="sdm-header">
          <h1 className="sdm-title">{editingPostId ? 'Chỉnh sửa tài liệu' : 'Tạo tài liệu mới'}</h1>
          <div className="sdm-header-actions">
            <button 
              onClick={() => {
                setViewMode('list');
                setPostId(null);
                setEditingPostId(null);
              }}
              className="sdm-btn-secondary"
            >
              Hủy
            </button>
            <button 
              onClick={handleSave} 
              disabled={saving}
              className="sdm-btn-primary"
            >
              {saving ? 'Đang lưu...' : 'Lưu'}
            </button>
          </div>
        </div>

        <div className="sdm-form">
          <div className="sdm-form-group">
            <label htmlFor="title">Tiêu đề <span className="sdm-required">*</span></label>
            <input
              id="title"
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              className={`sdm-input ${errors.title ? 'sdm-input-error' : ''}`}
              placeholder="Nhập tiêu đề"
            />
            {errors.title && <span className="sdm-error-message">{errors.title}</span>}
          </div>

          <div className="sdm-form-group">
            <label htmlFor="slug">Slug <span className="sdm-required">*</span></label>
            <input
              id="slug"
              type="text"
              value={slug}
              onChange={(e) => setSlug(e.target.value)}
              className={`sdm-input ${errors.slug ? 'sdm-input-error' : ''}`}
              placeholder="Nhập slug (chỉ chữ, số và dấu gạch ngang)"
            />
            {errors.slug && <span className="sdm-error-message">{errors.slug}</span>}
            <small className="sdm-hint">Slug chỉ được chứa chữ cái (a-z, A-Z), số (0-9) và dấu gạch ngang (-). Không được bắt đầu hoặc kết thúc bằng dấu gạch ngang.</small>
          </div>

          <div className="sdm-form-group">
            <div className="sdm-content-header">
              <label htmlFor="content">Nội dung <span className="sdm-required">*</span></label>
              <button
                type="button"
                onClick={() => docxInputRef.current?.click()}
                className="sdm-btn-secondary sdm-btn-small"
              >
                Import từ Word
              </button>
            </div>
            <div className="sdm-quill-editor">
              <div ref={editorContainerRef} style={{ minHeight: '400px' }} />
              <input
                type="file"
                accept="image/*"
                ref={imageInputRef}
                style={{ display: 'none' }}
                onChange={handleQuillImage}
              />
              <input
                type="file"
                accept=".docx"
                ref={docxInputRef}
                style={{ display: 'none' }}
                onChange={handleDocxImport}
              />
            </div>
          </div>

          <div className="sdm-form-group">
            <label htmlFor="thumbnail">Ảnh đại diện</label>
            <div className="sdm-image-upload-area">
              {featuredImage ? (
                <div className="sdm-image-preview">
                  <img src={featuredImage} alt="Preview" />
                  <button onClick={removeImage} className="sdm-btn-danger sdm-btn-small">Xóa</button>
                </div>
              ) : (
                <button
                  type="button"
                  className="sdm-image-upload-placeholder"
                  onClick={() => fileInputRef.current?.click()}
                  aria-label="Click để tải ảnh"
                >
                  <span>Click để tải ảnh</span>
                </button>
              )}
              <input
                id="thumbnail"
                ref={fileInputRef}
                type="file"
                accept="image/*"
                onChange={handleImageUpload}
                style={{ display: 'none' }}
              />
            </div>
          </div>
        </div>

        <ToastContainer toasts={toasts} onRemove={removeToast} confirmDialog={confirmDialog} />
      </div>
    );
  }

  // List view
  return (
    <div className="sdm-container">
      <div className="sdm-header">
        <div>
          <h1 className="sdm-title">Quản lý tài liệu</h1>
          <p className="sdm-subtitle">Quản lý các tài liệu đặc biệt (About Us, Điều khoản dịch vụ, Chính sách bảo mật, v.v.)</p>
        </div>
        <button 
          onClick={handleCreateNew}
          className="sdm-btn-primary"
        >
          Tạo mới
        </button>
      </div>

      {loading && posts.length === 0 ? (
        <div className="sdm-loading">Đang tải...</div>
      ) : (
        <div className="sdm-content">
          {posts.length === 0 ? (
            <div className="sdm-empty-state">
              <p>Chưa có tài liệu nào. Nhấn "Tạo mới" để bắt đầu.</p>
            </div>
          ) : (
            <table className="sdm-table">
              <thead>
                <tr>
                  <th>Tiêu đề</th>
                  <th>Slug</th>
                  <th>Ngày tạo</th>
                  <th>Ngày cập nhật</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {posts.map((post) => {
                  const postId = post.postId || post.PostId || post.id;
                  const title = post.title || post.Title || '';
                  const slug = post.slug || post.Slug || '';
                  return (
                    <tr key={postId}>
                      <td>{title}</td>
                      <td className="sdm-slug-cell">{slug}</td>
                      <td className="sdm-date-cell">{formatDate(post.createdAt || post.CreatedAt)}</td>
                      <td className="sdm-date-cell">{formatDate(post.updatedAt || post.UpdatedAt)}</td>
                      <td className="sdm-actions-cell">
                        <button
                          onClick={() => handleEdit(post)}
                          className="sdm-btn-secondary sdm-btn-small"
                        >
                          Sửa
                        </button>
                        <button
                          onClick={() => handleDelete(post)}
                          className="sdm-btn-danger sdm-btn-small"
                        >
                          Xóa
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      )}

      <ToastContainer toasts={toasts} onRemove={removeToast} confirmDialog={confirmDialog} />
    </div>
  );
};

export default SpecificDocumentationManage;

