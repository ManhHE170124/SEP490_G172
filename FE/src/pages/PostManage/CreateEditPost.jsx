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
import React, { useState, useRef, useEffect } from 'react';
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

  const fileInputRef = useRef(null);
  const quillRef = useRef(null);
  const editorContainerRef = useRef(null);
  const imageInputRef = useRef(null);
  const prevImagesRef = useRef([]);

  // Reset form to initial state
  const resetForm = () => {
    setTitle('');
    setDescription('');
    setContent('');
    setStatus('Draft');
    setPosttypeId('');
    setErrors({ title: '', description: '' });
    setTags([]);
    setFeaturedImage(null);
    setFeaturedImageUrl(null);
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
      if (title || description || content || posttypeId || tags.length > 0 || featuredImageUrl) {
        resetForm();
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isEditMode, postId]);

  // Fetch available tags
  useEffect(() => {
    const fetchTags = async () => {
      try {
        const tagsList = await postsApi.getTags();
        setAvailableTags(tagsList || []);
      } catch (err) {
        console.error('Failed to fetch tags:', err);
        showError('Lỗi tải tags', 'Không thể tải danh sách tags.');
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
        showError('Lỗi tải danh mục', 'Không thể tải danh sách danh mục bài viết.');
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
        setDescription(postData.shortDescription || '');
        setContent(postData.content || '');
        setStatus(postData.status || 'Draft');
        // Handle different possible property names for posttype ID
        setPosttypeId(postData.posttypeId || postData.postTypeId || postData.PosttypeId || '');
        setFeaturedImageUrl(postData.thumbnail || null);
        setFeaturedImage(postData.thumbnail || null);

        if (postData.tags && Array.isArray(postData.tags)) {
          setTags(postData.tags);
        }

        if (quillRef.current && postData.content) {
          quillRef.current.clipboard.dangerouslyPasteHTML(postData.content);
        }
      } catch (err) {
        console.error('Failed to fetch post data:', err);
        showError('Lỗi tải bài viết', 'Không thể tải thông tin bài viết.');
      } finally {
        setLoading(false);
      }
    };

    fetchPostData();
  }, [isEditMode, postId, showError]);

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
      showError('Lỗi tạo tag mới', 'Không thể tạo tag mới. Vui lòng thử lại.');
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
  const validateForm = () => {
    let isValid = true;
    const newErrors = { title: '', description: '' };

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

    if (description.length > 255) {
      newErrors.description = 'Mô tả không được vượt quá 255 ký tự';
      isValid = false;
    }

    setErrors(newErrors);
    return isValid;
  };

  // Save post (draft or publish)
  const handlePostAction = async (postStatus, successTitle, successMessage) => {
    if (!validateForm()) {
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
        await postsApi.updatePost(postId, postData);
        showSuccess(
          successTitle,
          successMessage
        );
      } else {
        // Create new post
        result = await postsApi.createPost(postData);
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
    // TODO: Open preview in new tab
    console.log('Previewing post...', { title, content, thumbnail: featuredImageUrl });
    showInfo('Preview', 'Chức năng xem trước đang được phát triển.');
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

  return (
    <main className="cep-main">
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
        </div>
      </div>
      <ToastContainer toasts={toasts} onRemove={removeToast} confirmDialog={confirmDialog} />
    </main>
  );
};

export default CreateEditPost;