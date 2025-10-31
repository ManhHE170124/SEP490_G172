/**
 * File: CreateEditPost.jsx
 * Author: HieuNDHE173169
 * Created: 30/10/2025
 * Last Updated: 30/10/2025
 * Version: 2.0.0
 * Purpose: Advanced post/blog/guide editor with full features:
 *          - Rich text editor with image insertion at multiple positions
 *          - Tag management (select from list or create new)
 *          - Post type selection
 *          - Link management
 *          - SEO metadata
 *          - Status management
 */

import React, { useCallback, useRef, useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { postsApi } from "../../services/postsApi";
import useToast from "../../hooks/useToast";
import ToastContainer from "../../components/Toast/ToastContainer";
import "./CreateEditPost.css";

export default function CreateEditPost() {
  const { postId } = useParams();
  const navigate = useNavigate();
  const isEditMode = !!postId;
  // Toast system
  const { toasts, showSuccess, showError, removeToast, confirmDialog } = useToast();

  // Form state
  const [title, setTitle] = useState("");
  const [shortDescription, setShortDescription] = useState("");
  const [content, setContent] = useState("");
  const [thumbnailFile, setThumbnailFile] = useState(null);
  const [thumbnailUrl, setThumbnailUrl] = useState("");
  const [postTypeId, setPostTypeId] = useState(null);
  const [status, setStatus] = useState("Draft");
  const [metaTitle, setMetaTitle] = useState("");
  const [metaDescription, setMetaDescription] = useState("");

  // Tags management
  const [availableTags, setAvailableTags] = useState([]);
  const [selectedTags, setSelectedTags] = useState([]);
  const [newTagName, setNewTagName] = useState("");
  const [newTagSlug, setNewTagSlug] = useState("");
  const [showAddTagModal, setShowAddTagModal] = useState(false);
  const [tagSearch, setTagSearch] = useState("");

  // Post types
  const [postTypes, setPostTypes] = useState([]);

  // Link management
  const [showLinkDialog, setShowLinkDialog] = useState(false);
  const [linkUrl, setLinkUrl] = useState("");
  const [linkText, setLinkText] = useState("");

  // Image management
  const [uploadingImage, setUploadingImage] = useState(false);
  const [imagePreview, setImagePreview] = useState(null);

  // Validation
  const [touchedPublish, setTouchedPublish] = useState(false);

  // Slug management
  const [slug, setSlug] = useState("");
  const [autoGenerateSlug, setAutoGenerateSlug] = useState(true);

  // Auto-save draft
  const [autoSaveTimer, setAutoSaveTimer] = useState(null);
  const [lastSaved, setLastSaved] = useState(null);
  const [isSaving, setIsSaving] = useState(false);

  // Refs
  const editorRef = useRef(null);
  const thumbnailInputRef = useRef(null);
  const editorImageInputRef = useRef(null);
  const linkDialogRef = useRef(null);

  // Generate slug from title
  const generateSlugFromTitle = (text) => {
    if (!text) return "";
    return text
      .toLowerCase()
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/[^a-z0-9\s-]/g, "")
      .replace(/\s+/g, "-")
      .replace(/-+/g, "-")
      .trim();
  };

  // Load initial data and post if edit mode
  useEffect(() => {
    const loadData = async () => {
      try {
        const [tagsData, postTypesData] = await Promise.all([
          postsApi.getTags(),
          postsApi.getAllPostTypes()
        ]);
        setAvailableTags(Array.isArray(tagsData) ? tagsData : []);
        setPostTypes(Array.isArray(postTypesData) ? postTypesData : []);

        // Load post data if edit mode
        if (isEditMode && postId) {
          try {
            const postData = await postsApi.getPostById(postId);
            setTitle(postData.title || "");
            setShortDescription(postData.shortDescription || "");
            setContent(postData.content || "");
            setThumbnailUrl(postData.thumbnail || "");
            setPostTypeId(postData.postTypeId || null);
            setStatus(postData.status || "Draft");
            setMetaTitle(postData.metaTitle || "");
            setMetaDescription(postData.metaDescription || "");
            setSlug(postData.slug || "");
            setAutoGenerateSlug(false);
            
            // Set tags
            if (postData.tags && Array.isArray(postData.tags)) {
              setSelectedTags(postData.tags.map(t => t.tagId));
            }

            // Set editor content
            if (editorRef.current && postData.content) {
              editorRef.current.innerHTML = postData.content;
            }
          } catch (error) {
            showError("Lỗi tải bài viết", error.message || "Không thể tải dữ liệu bài viết");
            navigate("/admin-post-list");
          }
        }
      } catch (error) {
        showError("Lỗi tải dữ liệu", error.message || "Không thể tải danh sách tags và post types");
      }
    };
    loadData();
  }, [postId, isEditMode, navigate, showError]);

  // Auto-generate slug when title changes
  useEffect(() => {
    if (autoGenerateSlug && title) {
      setSlug(generateSlugFromTitle(title));
    }
  }, [title, autoGenerateSlug]);

  // Auto-save draft every 60 seconds (silent)
  useEffect(() => {
    if (!title.trim() || isEditMode) return; // Only auto-save for new posts

    const autoSave = async () => {
      if (title.trim() && !isSaving) {
        try {
          const htmlContent = editorRef.current ? editorRef.current.innerHTML : "";
          if (!htmlContent || htmlContent.trim() === "<br>" || htmlContent.trim() === "") return;
          
          let thumbnailPath = thumbnailUrl && !thumbnailUrl.startsWith('blob:') ? thumbnailUrl : "";
          
          const payload = {
            title: title.trim(),
            shortDescription: shortDescription.trim(),
            content: htmlContent,
            thumbnail: thumbnailPath,
            postTypeId: postTypeId || null,
            status: "Draft",
            metaTitle: metaTitle.trim() || null,
            metaDescription: metaDescription.trim() || null,
            tagIds: selectedTags,
          };

          await postsApi.createPost(payload);
          setLastSaved(new Date());
        } catch (error) {
          // Silent fail for auto-save
        }
      }
    };

    const interval = setInterval(autoSave, 60000); // 60 seconds
    return () => clearInterval(interval);
  }, [title, isSaving, isEditMode]);

  // Editor commands
  function exec(command, value = null) {
    if (editorRef.current) editorRef.current.focus();
    document.execCommand(command, false, value);
  }

  const onBold = useCallback(() => exec("bold"), []);
  const onItalic = useCallback(() => exec("italic"), []);
  const onUnderline = useCallback(() => exec("underline"), []);

  // Link management
  const openLinkDialog = useCallback(() => {
    const selection = window.getSelection();
    const selectedText = selection.toString().trim();
    
    if (selectedText) {
      setLinkText(selectedText);
    } else {
      setLinkText("");
    }
    
    // Check if there's already a link selected
    if (selection.rangeCount > 0) {
      const range = selection.getRangeAt(0);
      const linkElement = range.commonAncestorContainer.nodeType === Node.TEXT_NODE
        ? range.commonAncestorContainer.parentElement.closest('a')
        : range.commonAncestorContainer.closest('a');
      
      if (linkElement) {
        setLinkUrl(linkElement.href);
        setLinkText(linkElement.textContent || selectedText);
      }
    }
    
    setLinkUrl("");
    setShowLinkDialog(true);
  }, []);

  const insertLink = useCallback(() => {
    if (!linkUrl.trim()) {
      showError("Lỗi", "Vui lòng nhập URL");
      return;
    }

    const text = linkText.trim() || linkUrl;
    
    if (editorRef.current) {
      editorRef.current.focus();
      
      // Check if text is selected
      const selection = window.getSelection();
      if (selection.rangeCount > 0 && selection.toString().trim()) {
        // If text is selected, create link with that text
        exec("createLink", linkUrl);
      } else {
        // Insert new link
        exec("insertHTML", `<a href="${linkUrl}" target="_blank" rel="noopener noreferrer">${text}</a>`);
      }
      
      setShowLinkDialog(false);
      setLinkUrl("");
      setLinkText("");
    }
  }, [linkUrl, linkText, showError]);

  const removeLink = useCallback(() => {
    exec("unlink");
  }, []);

  // Image management
  const onImage = useCallback(() => {
    editorImageInputRef.current?.click();
  }, []);

  async function handleEditorImageChange(e) {
    const file = e.target.files?.[0];
    if (!file) return;

    // Validate file type
    if (!file.type.startsWith('image/')) {
      showError("Lỗi", "Chỉ chấp nhận file ảnh");
      e.target.value = "";
      return;
    }

    // Validate file size (max 5MB)
    if (file.size > 5 * 1024 * 1024) {
      showError("Lỗi", "Kích thước ảnh không được vượt quá 5MB");
      e.target.value = "";
      return;
    }

    try {
      setUploadingImage(true);
      
      // Show preview
      const reader = new FileReader();
      reader.onload = (event) => {
        setImagePreview(event.target.result);
      };
      reader.readAsDataURL(file);

      const uploaded = await postsApi.uploadImage(file);
      const path = uploaded?.path || uploaded?.data?.path;
      
      if (path) {
        // Insert image at cursor position
        const imageHtml = `<img src="${path}" alt="image" style="max-width: 100%; height: auto; margin: 10px 0;" />`;
        exec("insertHTML", imageHtml);
        showSuccess("Thành công", "Ảnh đã được chèn vào nội dung");
      }
      
      setImagePreview(null);
    } catch (err) {
      showError("Lỗi", err.message || "Tải ảnh thất bại");
    } finally {
      setUploadingImage(false);
      e.target.value = "";
    }
  }

  // Tag management
  const handleTagToggle = (tagId) => {
    setSelectedTags(prev => {
      if (prev.includes(tagId)) {
        return prev.filter(id => id !== tagId);
      } else {
        return [...prev, tagId];
      }
    });
  };

  const handleRemoveTag = (tagId) => {
    setSelectedTags(prev => prev.filter(id => id !== tagId));
  };

  const generateSlug = (text) => {
    return text
      .toLowerCase()
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/[^a-z0-9\s-]/g, "")
      .replace(/\s+/g, "-")
      .replace(/-+/g, "-")
      .trim();
  };

  const handleCreateNewTag = async () => {
    if (!newTagName.trim()) {
      showError("Lỗi", "Tên tag không được để trống");
      return;
    }

    const slug = newTagSlug.trim() || generateSlug(newTagName);
    
    try {
      const newTag = await postsApi.createTag({
        tagName: newTagName.trim(),
        slug: slug
      });
      
      setAvailableTags(prev => [...prev, newTag]);
      setSelectedTags(prev => [...prev, newTag.tagId]);
      setShowAddTagModal(false);
      setNewTagName("");
      setNewTagSlug("");
      showSuccess("Thành công", `Tag "${newTag.tagName}" đã được tạo và thêm vào bài viết`);
    } catch (error) {
      showError("Lỗi", error.message || "Không thể tạo tag mới");
    }
  };

  const filteredTags = availableTags.filter(tag =>
    tag.tagName.toLowerCase().includes(tagSearch.toLowerCase())
  );

  // Thumbnail upload
  const handleThumbnailChange = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      showError("Lỗi", "Chỉ chấp nhận file ảnh");
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      showError("Lỗi", "Kích thước ảnh không được vượt quá 5MB");
      return;
    }

    setThumbnailFile(file);
    const localUrl = URL.createObjectURL(file);
    setThumbnailUrl(localUrl);
  };

  // Publish/Submit
  async function handlePublish() {
    setTouchedPublish(true);

    if (!title.trim()) {
      showError("Lỗi", "Tiêu đề không được để trống");
      return;
    }

    const htmlContent = editorRef.current ? editorRef.current.innerHTML : content;
    
    if (!htmlContent || htmlContent.trim() === "<br>" || htmlContent.trim() === "") {
      showError("Lỗi", "Nội dung bài viết không được để trống");
      return;
    }

    try {
      let thumbnailPath = "";
      
      // Upload thumbnail if new file selected
      if (thumbnailFile) {
        try {
          const uploaded = await postsApi.uploadImage(thumbnailFile);
          thumbnailPath = uploaded?.path || uploaded?.data?.path || "";
        } catch (uploadError) {
          showError("Lỗi", uploadError.message || "Không thể tải thumbnail");
          return;
        }
      } else if (thumbnailUrl && !thumbnailUrl.startsWith('blob:')) {
        thumbnailPath = thumbnailUrl;
      }

      const payload = {
        title: title.trim(),
        shortDescription: shortDescription.trim(),
        content: htmlContent,
        thumbnail: thumbnailPath,
        postTypeId: postTypeId || null,
        status: "Published",
        metaTitle: metaTitle.trim() || null,
        metaDescription: metaDescription.trim() || null,
        tagIds: selectedTags,
      };

      if (isEditMode && postId) {
        await postsApi.updatePost(postId, payload);
        showSuccess("Thành công", "Bài viết đã được cập nhật và xuất bản!");
        navigate("/admin-post-list");
      } else {
        await postsApi.createPost(payload);
        showSuccess("Thành công", "Bài viết đã được tạo và xuất bản!");
        navigate("/admin-post-list");
      }
    } catch (error) {
      showError("Lỗi", error.message || "Đăng bài thất bại");
    }
  }

  // Save Draft
  async function handleSaveDraft() {
    if (!title.trim()) {
      showError("Lỗi", "Tiêu đề không được để trống");
      return;
    }

    const htmlContent = editorRef.current ? editorRef.current.innerHTML : content;
    
    try {
      setIsSaving(true);
      let thumbnailPath = "";
      
      if (thumbnailFile) {
        try {
          const uploaded = await postsApi.uploadImage(thumbnailFile);
          thumbnailPath = uploaded?.path || uploaded?.data?.path || "";
        } catch (uploadError) {
          showError("Lỗi", uploadError.message || "Không thể tải thumbnail");
          setIsSaving(false);
          return;
        }
      } else if (thumbnailUrl && !thumbnailUrl.startsWith('blob:')) {
        thumbnailPath = thumbnailUrl;
      }

      const payload = {
        title: title.trim(),
        shortDescription: shortDescription.trim(),
        content: htmlContent,
        thumbnail: thumbnailPath,
        postTypeId: postTypeId || null,
        status: "Draft",
        metaTitle: metaTitle.trim() || null,
        metaDescription: metaDescription.trim() || null,
        tagIds: selectedTags,
      };

      if (isEditMode && postId) {
        await postsApi.updatePost(postId, payload);
        showSuccess("Thành công", "Bản nháp đã được lưu!");
      } else {
        const created = await postsApi.createPost(payload);
        showSuccess("Thành công", "Bản nháp đã được lưu!");
        navigate(`/post-create-edit/${created.postId}`);
      }
      
      setLastSaved(new Date());
      setThumbnailFile(null);
    } catch (error) {
      showError("Lỗi", error.message || "Lưu bản nháp thất bại");
    } finally {
      setIsSaving(false);
    }
  }

  // Handle Preview
  const handlePreview = () => {
    const htmlContent = editorRef.current ? editorRef.current.innerHTML : content;
    const previewWindow = window.open("", "_blank", "width=800,height=600");
    if (previewWindow) {
      previewWindow.document.write(`
        <html>
          <head>
            <title>${title || "Preview"}</title>
            <style>
              body { font-family: Arial, sans-serif; max-width: 800px; margin: 40px auto; padding: 20px; }
              img { max-width: 100%; height: auto; }
            </style>
          </head>
          <body>
            <h1>${title || "Tiêu đề"}</h1>
            ${thumbnailUrl ? `<img src="${thumbnailUrl}" alt="thumbnail" style="max-width: 100%; margin: 20px 0;" />` : ""}
            ${htmlContent || "<p>Nội dung...</p>"}
          </body>
        </html>
      `);
      previewWindow.document.close();
    }
  };

  // Keyboard shortcuts
  function onEditorKeyDown(e) {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "b") {
      e.preventDefault();
      onBold();
    } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "i") {
      e.preventDefault();
      onItalic();
    } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "u") {
      e.preventDefault();
      onUnderline();
    } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
      e.preventDefault();
      openLinkDialog();
    }
  }

  // Auto-generate slug when tag name changes
  useEffect(() => {
    if (newTagName && !newTagSlug) {
      setNewTagSlug(generateSlug(newTagName));
    }
  }, [newTagName, newTagSlug]);

  return (
    <div className="post-editor">
      <ToastContainer 
        toasts={toasts} 
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />

      <div className="post-editor-left">
        {/* Title */}
        <div className="form-group">
          <label htmlFor="post-title">
            Tiêu đề bài viết <span className="required">*</span>
          </label>
          <input
            id="post-title"
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Nhập tiêu đề bài viết..."
            className={touchedPublish && !title.trim() ? "error" : ""}
          />
          {touchedPublish && !title.trim() && (
            <div className="field-error">Tiêu đề không được để trống</div>
          )}
        </div>

        {/* Slug */}
        <div className="form-group">
          <label htmlFor="post-slug">
            URL Slug
            <button
              type="button"
              onClick={() => setAutoGenerateSlug(!autoGenerateSlug)}
              style={{
                marginLeft: '8px',
                padding: '2px 8px',
                fontSize: '12px',
                background: 'transparent',
                border: '1px solid #ddd',
                borderRadius: '4px',
                cursor: 'pointer'
              }}
            >
              {autoGenerateSlug ? 'Chỉnh sửa' : 'Tự động'}
            </button>
          </label>
          <input
            id="post-slug"
            type="text"
            value={slug}
            onChange={(e) => {
              setSlug(e.target.value);
              setAutoGenerateSlug(false);
            }}
            placeholder="url-slug-tu-dong-tao"
            disabled={autoGenerateSlug && !isEditMode}
            style={{ fontFamily: 'monospace' }}
          />
          <div className="hint">URL slug sẽ được tự động tạo từ tiêu đề</div>
        </div>

        {/* Short Description */}
        <div className="form-group">
          
          <div className="hint"><label htmlFor="short-description">Mô tả ngắn</label> ({shortDescription.length}/500 ký tự)</div>
          <textarea
            id="short-description"
            value={shortDescription}
            onChange={(e) => setShortDescription(e.target.value)}
            placeholder="Mô tả ngắn về bài viết (hiển thị ở danh sách)..."
            rows={3}
            maxLength={500}
          />
        </div>

        {/* Content Editor */}
        <div className="form-group">
          <label htmlFor="post-content">Nội dung <span className="required">*</span></label>
          <div className="editor-toolbar" role="toolbar" aria-label="Trình chỉnh sửa">
            <div className="toolbar-group flat">
              <button type="button" className="tool-icon" title="Hoàn tác (Ctrl+Z)" onClick={() => exec('undo')}>↶</button>
              <button type="button" className="tool-icon" title="Làm lại (Ctrl+Y)" onClick={() => exec('redo')}>↷</button>
            </div>
            <span className="tool-divider" />
            
            {/* Styles */}
            <select className="tool-select" aria-label="Styles" defaultValue="p" onChange={(e) => {
              const v = e.target.value;
              if (v === 'p') exec('formatBlock', 'P');
              else if (v === 'h1') exec('formatBlock', 'H1');
              else if (v === 'h2') exec('formatBlock', 'H2');
              else if (v === 'h3') exec('formatBlock', 'H3');
            }}>
              <option value="p">Đoạn văn</option>
              <option value="h1">Tiêu đề 1</option>
              <option value="h2">Tiêu đề 2</option>
              <option value="h3">Tiêu đề 3</option>
            </select>
            
            {/* Font Size */}
            <select className="tool-select" aria-label="Font Size" defaultValue="3" onChange={(e) => exec('fontSize', e.target.value)}>
              <option value="2">10pt</option>
              <option value="3">12pt</option>
              <option value="4">14pt</option>
              <option value="5">18pt</option>
              <option value="6">24pt</option>
            </select>
            
            <span className="tool-divider" />
            
            {/* Bold, Italic, Underline */}
            <div className="toolbar-group flat">
              <button type="button" className="tool-icon" title="Đậm (Ctrl+B)" onClick={onBold}><strong>B</strong></button>
              <button type="button" className="tool-icon" title="Nghiêng (Ctrl+I)" onClick={onItalic}><em>I</em></button>
              <button type="button" className="tool-icon" title="Gạch chân (Ctrl+U)" onClick={onUnderline}><u>U</u></button>
            </div>
            
            <span className="tool-divider" />
            
            {/* Link, Image */}
            <div className="toolbar-group flat">
              <button type="button" className="tool-icon" title="Chèn liên kết (Ctrl+K)" onClick={openLinkDialog}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z"/>
                </svg>
              </button>
              <button type="button" className="tool-icon" title="Chèn ảnh" onClick={onImage} disabled={uploadingImage}>
                {uploadingImage ? (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                    <path d="M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z"/>
                  </svg>
                ) : (
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                    <path d="M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z"/>
                  </svg>
                )}
              </button>
            </div>
            
            <span className="tool-divider" />
            
            {/* Alignment */}
            <div className="toolbar-group flat">
              <button type="button" className="tool-icon" title="Căn trái" onClick={() => exec('justifyLeft')}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M15 15H3v2h12v-2zm0-8H3v2h12V7zM3 13h18v-2H3v2zm0 8h18v-2H3v2zM3 3v2h18V3H3z"/>
                </svg>
              </button>
              <button type="button" className="tool-icon" title="Căn giữa" onClick={() => exec('justifyCenter')}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M7 15v2h10v-2H7zm-4 6h18v-2H3v2zm0-8h18v-2H3v2zm4-6v2h10V7H7zM3 3v2h18V3H3z"/>
                </svg>
              </button>
              <button type="button" className="tool-icon" title="Căn phải" onClick={() => exec('justifyRight')}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M21 15H9v2h12v-2zM21 7H9v2h12V7zM3 13h18v-2H3v2zM3 21h18v-2H3v2zM3 3v2h18V3H3z"/>
                </svg>
              </button>
            </div>
            
            <span className="tool-divider" />
            
            {/* Lists */}
            <div className="toolbar-group flat">
              <button type="button" className="tool-icon" title="Danh sách bullet" onClick={() => exec('insertUnorderedList')}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M4 10.5c-.83 0-1.5.67-1.5 1.5s.67 1.5 1.5 1.5 1.5-.67 1.5-1.5-.67-1.5-1.5-1.5zm0-6c-.83 0-1.5.67-1.5 1.5S3.17 7.5 4 7.5 5.5 6.83 5.5 6 4.83 4.5 4 4.5zm0 12c-.83 0-1.5.67-1.5 1.5s.67 1.5 1.5 1.5 1.5-.67 1.5-1.5-.67-1.5-1.5-1.5zM7 19h14v-2H7v2zm0-6h14v-2H7v2zm0-8v2h14V5H7z"/>
                </svg>
              </button>
              <button type="button" className="tool-icon" title="Danh sách số" onClick={() => exec('insertOrderedList')}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M2 17h2v.5H3v1h1v.5H2v1h3v-4H2v1zm1-9h1V4H2v1h1v3zm-1 3h1.8L2 13.1v.9h3v-1H3.2L5 10.9V10H2v1zm5-6v2h14V5H7zm0 14h14v-2H7v2zm0-6h14v-2H7v2z"/>
                </svg>
              </button>
            </div>
          </div>
          
          <div
            id="post-content"
            ref={editorRef}
            className="editor-area"
            contentEditable
            suppressContentEditableWarning
            aria-label="Vùng soạn thảo nội dung"
            onKeyDown={onEditorKeyDown}
            placeholder="Nhập nội dung bài viết... Bạn có thể chèn ảnh, links, và định dạng văn bản."
            style={{ minHeight: '400px' }}
          />
          
          <input 
            ref={editorImageInputRef} 
            type="file" 
            accept="image/*" 
            style={{ display: 'none' }} 
            onChange={handleEditorImageChange} 
          />
        </div>
      </div>

      {/* Right Sidebar */}
      <div className="post-editor-right">
        {/* Actions */}
        <div className="side-card">
          <div className="side-card-header">Hành động</div>
          <div className="side-card-body">
            <button 
              type="button" 
              className="primary-btn" 
              onClick={handlePublish}
              disabled={isSaving}
              style={{ width: '100%', marginBottom: '8px' }}
            >
              {isSaving ? "Đang xử lý..." : "📢 Xuất bản"}
            </button>
            <button 
              type="button" 
              className="btn-secondary" 
              onClick={handleSaveDraft}
              disabled={isSaving}
              style={{ width: '100%', marginBottom: '8px' }}
            >
              {isSaving ? "Đang lưu..." : "💾 Lưu bản nháp"}
            </button>
            <button 
              type="button" 
              className="btn-secondary" 
              onClick={handlePreview}
              style={{ width: '100%' }}
            >
              👁️ Xem trước
            </button>
            {lastSaved && (
              <div className="hint" style={{ marginTop: '8px', fontSize: '0.85rem' }}>
                Đã lưu lúc: {lastSaved.toLocaleTimeString()}
              </div>
            )}
          </div>
        </div>

        {/* Status & Post Type */}
        <div className="side-card">
          <div className="side-card-header">Trạng thái & Loại</div>
          <div className="side-card-body">
            <div className="form-group">
              <label htmlFor="post-status">Trạng thái</label>
              <select
                id="post-status"
                value={status}
                onChange={(e) => setStatus(e.target.value)}
                style={{ width: '100%', padding: '8px' }}
              >
                <option value="Draft">Bản nháp</option>
                <option value="Published">Đã xuất bản</option>
                <option value="Archived">Đã lưu trữ</option>
              </select>
            </div>
            
            <div className="form-group" style={{ marginTop: '12px' }}>
              <label htmlFor="post-type">Loại bài viết</label>
              <select
                id="post-type"
                value={postTypeId || ""}
                onChange={(e) => setPostTypeId(e.target.value ? parseInt(e.target.value) : null)}
                style={{ width: '100%', padding: '8px' }}
              >
                <option value="">-- Chọn loại --</option>
                {postTypes.map((pt) => (
                  <option key={pt.postTypeId} value={pt.postTypeId}>
                    {pt.postTypeName}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>

        {/* Thumbnail */}
        <div className="side-card">
          <div className="side-card-header">Ảnh đại diện</div>
          <div className="side-card-body">
            <div className="form-group">
              <input
                ref={thumbnailInputRef}
                id="thumb-input"
                type="file"
                accept="image/*"
                onChange={handleThumbnailChange}
                style={{ marginBottom: '8px' }}
              />
              {thumbnailUrl && (
                <div style={{ marginTop: '8px' }}>
                  <img 
                    src={thumbnailUrl} 
                    alt="thumbnail preview" 
                    className="thumb-preview"
                    style={{ maxWidth: '100%', borderRadius: '4px' }}
                  />
                </div>
              )}
              <div className="hint">Ảnh sẽ được lưu trong /images và DB lưu đường dẫn</div>
            </div>
          </div>
        </div>

        {/* Tags */}
        <div className="side-card">
          <div className="side-card-header">
            Tags
            <button
              type="button"
              onClick={() => setShowAddTagModal(true)}
              style={{
                float: 'right',
                padding: '4px 8px',
                fontSize: '12px',
                background: '#007bff',
                color: 'white',
                border: 'none',
                borderRadius: '4px',
                cursor: 'pointer'
              }}
            >
              + Thêm mới
            </button>
          </div>
          <div className="side-card-body">
            {/* Tag Search */}
            <div className="form-group">
              <input
                type="text"
                placeholder="Tìm kiếm tags..."
                value={tagSearch}
                onChange={(e) => setTagSearch(e.target.value)}
                style={{ width: '100%', padding: '6px', marginBottom: '8px' }}
              />
            </div>

            {/* Selected Tags */}
            {selectedTags.length > 0 && (
              <div style={{ marginBottom: '12px' }}>
                <div style={{ fontSize: '12px', color: '#666', marginBottom: '6px' }}>Đã chọn:</div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px' }}>
                  {selectedTags.map((tagId) => {
                    const tag = availableTags.find(t => t.tagId === tagId);
                    return tag ? (
                      <span
                        key={tagId}
                        className="tag-chip"
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          padding: '6px 12px',
                          background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
                          color: 'white',
                          borderRadius: '20px',
                          fontSize: '13px',
                          fontWeight: '500',
                          gap: '6px',
                          boxShadow: '0 2px 4px rgba(0,0,0,0.1)',
                          transition: 'all 0.2s'
                        }}
                        onMouseEnter={(e) => e.currentTarget.style.transform = 'scale(1.05)'}
                        onMouseLeave={(e) => e.currentTarget.style.transform = 'scale(1)'}
                      >
                        {tag.tagName}
                        <button
                          type="button"
                          onClick={() => handleRemoveTag(tagId)}
                          className="tag-remove-btn"
                          style={{
                            background: 'rgba(255,255,255,0.3)',
                            border: 'none',
                            color: 'white',
                            cursor: 'pointer',
                            fontSize: '16px',
                            padding: '0 4px',
                            borderRadius: '50%',
                            width: '18px',
                            height: '18px',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            lineHeight: 1,
                            transition: 'background 0.2s'
                          }}
                          onMouseEnter={(e) => e.currentTarget.style.background = 'rgba(255,255,255,0.5)'}
                          onMouseLeave={(e) => e.currentTarget.style.background = 'rgba(255,255,255,0.3)'}
                        >
                          ×
                        </button>
                      </span>
                    ) : null;
                  })}
                </div>
              </div>
            )}

            {/* Available Tags */}
            <div style={{ maxHeight: '200px', overflowY: 'auto' }}>
              {filteredTags.length === 0 ? (
                <div className="hint">Không tìm thấy tag nào</div>
              ) : (
                filteredTags.map((tag) => (
                  <label
                    key={tag.tagId}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      padding: '6px',
                      cursor: 'pointer',
                      borderRadius: '4px',
                      marginBottom: '4px'
                    }}
                    onMouseEnter={(e) => e.currentTarget.style.background = '#f0f0f0'}
                    onMouseLeave={(e) => e.currentTarget.style.background = 'transparent'}
                  >
                    <input
                      type="checkbox"
                      checked={selectedTags.includes(tag.tagId)}
                      onChange={() => handleTagToggle(tag.tagId)}
                      style={{ marginRight: '8px' }}
                    />
                    <span>{tag.tagName}</span>
                  </label>
                ))
              )}
            </div>
          </div>
        </div>

        {/* SEO Metadata */}
        <div className="side-card">
          <div className="side-card-header">SEO & Metadata</div>
          <div className="side-card-body">
            <div className="form-group">
              <label htmlFor="meta-title">Meta Title</label>
              <input
                id="meta-title"
                type="text"
                value={metaTitle}
                onChange={(e) => setMetaTitle(e.target.value)}
                placeholder="Tiêu đề SEO (tùy chọn)"
                maxLength={60}
              />
              <div className="hint">{metaTitle.length}/60 ký tự</div>
            </div>
            
            <div className="form-group" style={{ marginTop: '12px' }}>
              <label htmlFor="meta-description">Meta Description</label>
              <textarea
                id="meta-description"
                value={metaDescription}
                onChange={(e) => setMetaDescription(e.target.value)}
                placeholder="Mô tả SEO (tùy chọn)"
                rows={3}
                maxLength={160}
              />
              <div className="hint">{metaDescription.length}/160 ký tự</div>
            </div>
          </div>
        </div>
      </div>

      {/* Link Dialog */}
      {showLinkDialog && (
        <div 
          className="modal-overlay active" 
          onClick={() => setShowLinkDialog(false)}
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: 'rgba(0,0,0,0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000
          }}
        >
          <div 
            ref={linkDialogRef}
            onClick={(e) => e.stopPropagation()}
            style={{
              background: 'white',
              padding: '24px',
              borderRadius: '8px',
              minWidth: '400px',
              maxWidth: '90%'
            }}
          >
            <h3 style={{ marginTop: 0 }}>Chèn liên kết</h3>
            <div className="form-group">
              <label>Văn bản hiển thị</label>
              <input
                type="text"
                value={linkText}
                onChange={(e) => setLinkText(e.target.value)}
                placeholder="Văn bản hiển thị"
                style={{ width: '100%', padding: '8px' }}
              />
            </div>
            <div className="form-group" style={{ marginTop: '12px' }}>
              <label>URL <span className="required">*</span></label>
              <input
                type="url"
                value={linkUrl}
                onChange={(e) => setLinkUrl(e.target.value)}
                placeholder="https://example.com"
                style={{ width: '100%', padding: '8px' }}
              />
            </div>
            <div style={{ display: 'flex', gap: '8px', marginTop: '16px', justifyContent: 'flex-end' }}>
              <button
                type="button"
                onClick={() => {
                  setShowLinkDialog(false);
                  setLinkUrl("");
                  setLinkText("");
                }}
                style={{
                  padding: '8px 16px',
                  border: '1px solid #ddd',
                  background: 'white',
                  borderRadius: '4px',
                  cursor: 'pointer'
                }}
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={insertLink}
                style={{
                  padding: '8px 16px',
                  border: 'none',
                  background: '#007bff',
                  color: 'white',
                  borderRadius: '4px',
                  cursor: 'pointer'
                }}
              >
                Chèn
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Add Tag Modal */}
      {showAddTagModal && (
        <div 
          className="modal-overlay active" 
          onClick={() => setShowAddTagModal(false)}
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: 'rgba(0,0,0,0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000
          }}
        >
          <div 
            onClick={(e) => e.stopPropagation()}
            style={{
              background: 'white',
              padding: '24px',
              borderRadius: '8px',
              minWidth: '400px',
              maxWidth: '90%'
            }}
          >
            <h3 style={{ marginTop: 0 }}>Thêm Tag mới</h3>
            <div className="form-group">
              <label>Tên Tag <span className="required">*</span></label>
              <input
                type="text"
                value={newTagName}
                onChange={(e) => {
                  setNewTagName(e.target.value);
                  if (!newTagSlug) {
                    setNewTagSlug(generateSlug(e.target.value));
                  }
                }}
                placeholder="Nhập tên tag"
                style={{ width: '100%', padding: '8px' }}
              />
            </div>
            <div className="form-group" style={{ marginTop: '12px' }}>
              <label>Slug</label>
              <input
                type="text"
                value={newTagSlug}
                onChange={(e) => setNewTagSlug(e.target.value)}
                placeholder="Slug tự động tạo từ tên"
                style={{ width: '100%', padding: '8px' }}
              />
              <div className="hint">Slug sẽ được tự động tạo từ tên tag nếu để trống</div>
            </div>
            <div style={{ display: 'flex', gap: '8px', marginTop: '16px', justifyContent: 'flex-end' }}>
              <button
                type="button"
                onClick={() => {
                  setShowAddTagModal(false);
                  setNewTagName("");
                  setNewTagSlug("");
                }}
                style={{
                  padding: '8px 16px',
                  border: '1px solid #ddd',
                  background: 'white',
                  borderRadius: '4px',
                  cursor: 'pointer'
                }}
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={handleCreateNewTag}
                style={{
                  padding: '8px 16px',
                  border: 'none',
                  background: '#007bff',
                  color: 'white',
                  borderRadius: '4px',
                  cursor: 'pointer'
                }}
              >
                Tạo Tag
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

