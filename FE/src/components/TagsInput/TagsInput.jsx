import React, { useState, useRef, useEffect } from 'react';
import './TagsInput.css';

const TagsInput = ({ 
  tags = [], 
  setTags, 
  availableTags = [], 
  onCreateNewTag 
}) => {
  const [tagInput, setTagInput] = useState('');
  const [filteredTags, setFilteredTags] = useState([]);
  const [showDropdown, setShowDropdown] = useState(false);
  const [selectedIndex, setSelectedIndex] = useState(-1);
  const [error, setError] = useState('');
  const dropdownRef = useRef(null);
  const inputRef = useRef(null);

  // Filter tags based on input and slug
  useEffect(() => {
    if (tagInput.trim()) {
      const searchSlug = toSlug(tagInput);
      const filtered = availableTags.filter(tag => {
        // Check if the tag is not already selected
        const isNotSelected = !tags.some(t => t.tagName === tag.tagName || t === tag.tagName);
        
        if (!isNotSelected) return false;

        // Get or generate slug for comparison
        const tagSlug = tag.slug || toSlug(tag.tagName);
        
        // Check if either the name or slug contains the search term
        return tagSlug.includes(searchSlug) || 
               tag.tagName.toLowerCase().includes(tagInput.toLowerCase());
      });
      
      setFilteredTags(filtered);
      setShowDropdown(filtered.length > 0);
    } else {
      setFilteredTags([]);
      setShowDropdown(false);
    }
    setSelectedIndex(-1);
  }, [tagInput, availableTags, tags]);

  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (e) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setShowDropdown(false);
      }
    };
    
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Generate slug from Vietnamese text
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

  const handleInputChange = (e) => {
    const value = e.target.value;
    setTagInput(value);
    setError('');
  };

  const handleSelectTag = (tag) => {
    // Add existing tag from database
    if (!tags.some(t => t.tagName === tag.tagName || t === tag.tagName)) {
      setTags(prev => [...prev, tag]);
    }
    setTagInput('');
    setShowDropdown(false);
    setError('');
    inputRef.current?.focus();
  };

  const handleCreateNewTag = async () => {
    const trimmedInput = tagInput.trim();
    
    if (!trimmedInput) {
      setError('Tag không được để trống');
      return;
    }

    // Check if tag already exists (case-insensitive)
    const isDuplicate = tags.some(t => {
      const tagName = typeof t === 'string' ? t : t.tagName;
      return tagName.toLowerCase() === trimmedInput.toLowerCase();
    });

    if (isDuplicate) {
      setError('Tag này đã tồn tại');
      return;
    }

    if (trimmedInput.length > 100) {
      setError('Tag không được vượt quá 100 ký tự');
      return;
    }

    if (trimmedInput.length < 2) {
      setError('Tag không được dưới 2 ký tự');
      return;
    }
    // Check if it's an existing tag in database
    const existingTag = availableTags.find(
      t => t.tagName.toLowerCase() === trimmedInput.toLowerCase()
    );

    if (existingTag) {
      // Use existing tag from database
      handleSelectTag(existingTag);
      return;
    }

    // Create new tag with auto-generated slug
    try {
      if (onCreateNewTag) {
        const slug = toSlug(trimmedInput);
        const newTag = await onCreateNewTag(trimmedInput, slug);
        
        // Validate newTag before adding to tags
        if (!newTag) {
          setError('Không thể tạo tag mới. Vui lòng thử lại.');
          return;
        }
        
        // Ensure newTag has required properties
        if (typeof newTag !== 'string' && (!newTag.tagName && !newTag.name)) {
          setError('Dữ liệu tag không hợp lệ.');
          return;
        }
        
        setTags(prev => [...prev, newTag]);
        setTagInput('');
        setError('');
      } else {
        // If no create handler, user doesn't have permission to create tags
        setError('Bạn không có quyền tạo tag mới. Vui lòng chọn tag từ danh sách có sẵn.');
      }
    } catch (err) {
      // Error message should already be shown by the parent component
      setError(err.message || 'Không thể tạo tag mới. Vui lòng thử lại.');
    }
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      
      if (showDropdown && selectedIndex >= 0 && selectedIndex < filteredTags.length) {
        // Select highlighted tag from dropdown
        handleSelectTag(filteredTags[selectedIndex]);
      } else if (onCreateNewTag) {
        // Create new tag only if user has permission
        handleCreateNewTag();
      } else {
        // If no permission, show error
        setError('Bạn không có quyền tạo tag mới. Vui lòng chọn tag từ danh sách có sẵn.');
      }
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      if (showDropdown) {
        setSelectedIndex(prev => 
          prev < filteredTags.length - 1 ? prev + 1 : prev
        );
      }
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      if (showDropdown) {
        setSelectedIndex(prev => prev > 0 ? prev - 1 : -1);
      }
    } else if (e.key === 'Escape') {
      setShowDropdown(false);
      setSelectedIndex(-1);
    }
  };

  const removeTag = (tagToRemove) => {
    setTags(prev => prev.filter(t => {
      const tagName = typeof t === 'string' ? t : t.tagName;
      const removeName = typeof tagToRemove === 'string' ? tagToRemove : tagToRemove.tagName;
      return tagName !== removeName;
    }));
  };

  return (
    <div className="tags-input-container">
      <div className="tags-input-wrapper" ref={dropdownRef}>
        <input
          ref={inputRef}
          type="text"
          placeholder={onCreateNewTag ? "Nhập tag (có dấu được)... Ấn Enter để tạo" : "Tìm và chọn tag từ danh sách có sẵn"}
          value={tagInput}
          onChange={handleInputChange}
          onKeyDown={handleKeyDown}
          onFocus={() => {
            if (filteredTags.length > 0) setShowDropdown(true);
          }}
          className={error ? 'error' : ''}
        />
        
        {showDropdown && filteredTags.length > 0 && (
          <div className="tags-dropdown">
            {filteredTags.map((tag, index) => (
              <div
                key={tag.tagId}
                className={`tag-dropdown-item ${index === selectedIndex ? 'selected' : ''}`}
                onClick={() => handleSelectTag(tag)}
                onMouseEnter={() => setSelectedIndex(index)}
              >
                <span className="tag-name">{tag.tagName}</span>
                {tag.slug && <span className="tag-slug">({tag.slug})</span>}
              </div>
            ))}
          </div>
        )}
      </div>

      {error && <div className="tag-error-message">{error}</div>}

      <div className="tags-list">
        {tags.map((tag, index) => {
          const tagName = typeof tag === 'string' ? tag : tag.tagName;
          const isNew = typeof tag === 'string' || !tag.tagId;
          
          return (
            <div 
              key={index} 
              className={`tag-item ${isNew ? 'new-tag' : 'existing-tag'}`}
              title={isNew ? 'Tag mới' : 'Tag có sẵn'}
            >
              {tagName}
              {isNew && <span className="new-badge">Mới</span>}
              <button
                type="button"
                className="tag-remove"
                onClick={() => removeTag(tag)}
                aria-label={`Remove ${tagName}`}
              >
                ×
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default TagsInput;