/**
 * File: AboutUsPage.jsx
 * Author: Keytietkiem Team
 * Created: 2025
 * Version: 1.0.0
 * Purpose: Admin page for managing AboutUs static content
 */
import React, { useState, useEffect, useRef } from 'react';
import { staticContentApi } from '../../services/staticContentApi';
import { postsApi, extractPublicId } from '../../services/postsApi';
import useToast from '../../hooks/useToast';
import ToastContainer from '../../components/Toast/ToastContainer';
import Quill from 'quill';
import 'quill/dist/quill.snow.css';
import mammoth from 'mammoth';
import './StaticContentPage.css';

const AboutUsPage = () => {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [content, setContent] = useState('');
  const [featuredImageUrl, setFeaturedImageUrl] = useState(null);
  const [featuredImage, setFeaturedImage] = useState(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState({ title: '', description: '' });
  const [postId, setPostId] = useState(null);
  const [postTypeId, setPostTypeId] = useState(null);
  const { toasts, showSuccess, showError, removeToast } = useToast();
  
  const fileInputRef = useRef(null);
  const quillRef = useRef(null);
  const editorContainerRef = useRef(null);
  const imageInputRef = useRef(null);
  const docxInputRef = useRef(null);
  const prevImagesRef = useRef([]);
  const contentLoadedRef = useRef(false);
  const [editorReady, setEditorReady] = useState(false);

  // Initialize Quill editor - run after component mounts
  useEffect(() => {
    if (quillRef.current) {
      return;
    }

    // Wait for container to be ready
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

      // Prevent unwanted scroll when clicking/focusing editor
      const editorElement = q.root;
      let scrollCleanup = null;
      if (editorElement) {
        // Store scroll position before any interaction
        let savedScrollPosition = window.pageYOffset || document.documentElement.scrollTop;
        let isUserScrolling = false;
        let scrollTimeout = null;
        
        // Update saved position on scroll (only if user-initiated)
        const updateScrollPosition = () => {
          if (isUserScrolling) {
            savedScrollPosition = window.pageYOffset || document.documentElement.scrollTop;
          }
        };
        window.addEventListener('scroll', updateScrollPosition, { passive: true });
        
        // Mark when user is scrolling
        const handleUserScroll = () => {
          isUserScrolling = true;
          if (scrollTimeout) clearTimeout(scrollTimeout);
          scrollTimeout = setTimeout(() => {
            isUserScrolling = false;
          }, 150);
        };
        window.addEventListener('wheel', handleUserScroll, { passive: true });
        window.addEventListener('touchmove', handleUserScroll, { passive: true });
        
        // Override scrollIntoView to prevent automatic scrolling
        const originalScrollIntoView = editorElement.scrollIntoView;
        editorElement.scrollIntoView = function(options) {
          // Prevent scrollIntoView from scrolling the window
          if (options && typeof options === 'object' && options.block === 'nearest') {
            return; // Allow only nearest scrolling (within editor container)
          }
          // For other cases, prevent window scroll
          return;
        };
        
        // Prevent scroll on focus/click/selection change
        const preventScrollOnInteraction = (e) => {
          // Save current scroll position before interaction
          const currentScroll = window.pageYOffset || document.documentElement.scrollTop;
          
          // Restore scroll position immediately and after interaction
          const restoreScroll = () => {
            const newScroll = window.pageYOffset || document.documentElement.scrollTop;
            // Only restore if scroll changed significantly (more than 30px)
            if (Math.abs(newScroll - currentScroll) > 30) {
              window.scrollTo({
                top: currentScroll,
                behavior: 'instant'
              });
            }
          };
          
          // Restore immediately
          restoreScroll();
          
          // Restore after browser's default behavior
          requestAnimationFrame(() => {
            restoreScroll();
            requestAnimationFrame(() => {
              restoreScroll();
            });
          });
        };
        
        // Prevent scroll on selection change (when clicking in editor)
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
        
        // Store cleanup function
        scrollCleanup = () => {
          window.removeEventListener('scroll', updateScrollPosition);
          window.removeEventListener('wheel', handleUserScroll);
          window.removeEventListener('touchmove', handleUserScroll);
          editorElement.removeEventListener('focus', preventScrollOnInteraction, true);
          editorElement.removeEventListener('mousedown', preventScrollOnInteraction, true);
          editorElement.removeEventListener('click', preventScrollOnInteraction, true);
          q.off('selection-change', preventScrollOnSelection);
          if (scrollTimeout) clearTimeout(scrollTimeout);
          // Restore original scrollIntoView
          if (originalScrollIntoView) {
            editorElement.scrollIntoView = originalScrollIntoView;
          }
        };
      }

      quillRef.current = q;

      // Don't paste content here - wait for sync useEffect
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Helper function to get PostTypeId for AboutUs
  const getAboutUsPostTypeId = async () => {
    try {
      const postTypes = await postsApi.getPosttypes();
      const postTypesArray = Array.isArray(postTypes) ? postTypes : (postTypes?.data || []);
      const aboutUsType = postTypesArray.find(pt => {
        const name = (pt.postTypeName || pt.PostTypeName || '').toLowerCase();
        return name === 'about us' || name === 'aboutus' || name.replace(/\s+/g, '-') === 'about-us';
      });
      return aboutUsType?.postTypeId || aboutUsType?.PostTypeId || null;
    } catch (err) {
      console.error('Failed to fetch PostTypes:', err);
      return null;
    }
  };

  // Fetch AboutUs content
  useEffect(() => {
    const fetchAboutUs = async () => {
      try {
        setLoading(true);
        const response = await staticContentApi.getAboutUs();
        const data = response?.data || response;
        
        setTitle(data.title || '');
        setDescription(data.shortDescription || '');
        setContent(data.content || '');
        setFeaturedImageUrl(data.thumbnail || null);
        setFeaturedImage(data.thumbnail || null);
        setPostId(data.postId || data.PostId || null);
        setPostTypeId(data.postTypeId || data.PostTypeId || null);
        
        contentLoadedRef.current = true;
      } catch (err) {
        console.error('Failed to fetch about us:', err);
        // If 404, it means Post doesn't exist yet - that's okay, we'll create it
        if (err.response?.status === 404) {
          // Try to get PostTypeId for AboutUs
          const aboutUsPostTypeId = await getAboutUsPostTypeId();
          if (aboutUsPostTypeId) {
            setPostTypeId(aboutUsPostTypeId);
          } else {
            showError('Chưa có PostType', 'Chưa có PostType "About Us". Vui lòng tạo PostType trước.');
          }
        } else {
          showError('Lỗi tải dữ liệu', 'Không thể tải thông tin về chúng tôi.');
        }
        // Set empty content
        setContent('');
        contentLoadedRef.current = true;
      } finally {
        setLoading(false);
      }
    };

    fetchAboutUs();
  }, [showError]);

  // Sync content to Quill editor when both are ready
  useEffect(() => {
    if (!editorReady || !contentLoadedRef.current || !quillRef.current) {
      return;
    }

    // Small delay to ensure Quill is fully initialized
    const timer = setTimeout(() => {
      if (quillRef.current) {
        if (content && content.trim() !== '' && content !== '<p></p>') {
          quillRef.current.clipboard.dangerouslyPasteHTML(content);
        } else {
          quillRef.current.clipboard.dangerouslyPasteHTML('<p><br></p>');
        }
      }
    }, 50);

    return () => clearTimeout(timer);
  }, [editorReady, content]);

  // Validation
  const validateForm = () => {
    let isValid = true;
    const newErrors = { title: '', description: '' };

    if (title.length === 0) {
      newErrors.title = 'Tiêu đề không được để trống';
      isValid = false;
    } else if (title.length > 250) {
      newErrors.title = 'Tiêu đề không được vượt quá 250 ký tự';
      isValid = false;
    }

    if (description.length > 255) {
      newErrors.description = 'Mô tả không được vượt quá 255 ký tự';
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

    // Ensure we have PostTypeId
    let finalPostTypeId = postTypeId;
    if (!finalPostTypeId) {
      finalPostTypeId = await getAboutUsPostTypeId();
      if (!finalPostTypeId) {
        showError('Lỗi', 'Không tìm thấy PostType "About Us". Vui lòng tạo PostType trước.');
        return;
      }
      setPostTypeId(finalPostTypeId);
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

      const postData = {
        title: title.trim(),
        slug: 'about-us', // Fixed slug for AboutUs
        shortDescription: description.trim(),
        content: content || '<p></p>',
        thumbnail: featuredImageUrl || null,
        authorId: authorId,
        posttypeId: finalPostTypeId
      };

      if (postId) {
        // Update existing post
        postData.postId = postId;
        await staticContentApi.updateAboutUs(postData);
        showSuccess('Lưu thành công', 'Về chúng tôi đã được cập nhật.');
      } else {
        // Create new post using static content endpoint
        const createdPost = await staticContentApi.updateAboutUs(postData);
        const createdData = createdPost?.data || createdPost;
        setPostId(createdData.postId || createdData.PostId || null);
        showSuccess('Lưu thành công', 'Về chúng tôi đã được tạo mới.');
        // Refresh to load the new post
        window.location.reload();
      }
    } catch (err) {
      console.error('Failed to save about us:', err);
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

  if (loading) {
    return <div className="static-content-loading">Đang tải...</div>;
  }

  return (
    <div className="static-content-page">
      <div className="static-content-header">
        <h1>Quản lý Về chúng tôi</h1>
        <button 
          onClick={handleSave} 
          disabled={saving}
          className="btn-save"
        >
          {saving ? 'Đang lưu...' : 'Lưu'}
        </button>
      </div>

      <div className="static-content-form">
        <div className="form-group">
          <label htmlFor="title">Tiêu đề <span className="required">*</span></label>
          <input
            id="title"
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            className={errors.title ? 'error' : ''}
            placeholder="Nhập tiêu đề"
          />
          {errors.title && <span className="error-message">{errors.title}</span>}
        </div>

        <div className="form-group">
          <label htmlFor="description">Mô tả ngắn</label>
          <textarea
            id="description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            className={errors.description ? 'error' : ''}
            placeholder="Nhập mô tả ngắn"
            rows="3"
          />
          {errors.description && <span className="error-message">{errors.description}</span>}
        </div>

        <div className="form-group">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <label htmlFor="content">Nội dung <span className="required">*</span></label>
            <button
              type="button"
              onClick={() => docxInputRef.current?.click()}
              style={{
                padding: '6px 12px',
                fontSize: '14px',
                backgroundColor: '#f3f4f6',
                border: '1px solid #d1d5db',
                borderRadius: '6px',
                cursor: 'pointer',
                color: '#374151'
              }}
            >
              Import từ Word
            </button>
          </div>
          <div className="quill-editor">
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

        <div className="form-group">
          <label htmlFor="thumbnail">Ảnh đại diện</label>
          <div className="image-upload-area">
            {featuredImage ? (
              <div className="image-preview">
                <img src={featuredImage} alt="Preview" />
                <button onClick={removeImage} className="btn-remove-image">Xóa</button>
              </div>
            ) : (
              <button
                type="button"
                className="image-upload-placeholder"
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

      <ToastContainer toasts={toasts} onRemove={removeToast} />
    </div>
  );
};

export default AboutUsPage;

