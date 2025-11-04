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

  // Filter tags based on input
  useEffect(() => {
    if (tagInput.trim()) {
      const filtered = availableTags.filter(tag => 
        tag.tagName.toLowerCase().includes(tagInput.toLowerCase()) &&
        !tags.some(t => t.tagName === tag.tagName || t === tag.tagName)
      );
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
  const generateSlug = (text) => {
    if (!text) return '';
    
    // Convert to lowercase
    let slug = text.toLowerCase();
    
    // Remove Vietnamese diacritics
    const vietnameseMap = {
      'à': 'a', 'á': 'a', 'ả': 'a', 'ã': 'a', 'ạ': 'a',
      'ă': 'a', 'ắ': 'a', 'ằ': 'a', 'ẳ': 'a', 'ẵ': 'a', 'ặ': 'a',
      'â': 'a', 'ấ': 'a', 'ầ': 'a', 'ẩ': 'a', 'ẫ': 'a', 'ậ': 'a',
      'è': 'e', 'é': 'e', 'ẻ': 'e', 'ẽ': 'e', 'ẹ': 'e',
      'ê': 'e', 'ế': 'e', 'ề': 'e', 'ể': 'e', 'ễ': 'e', 'ệ': 'e',
      'ì': 'i', 'í': 'i', 'ỉ': 'i', 'ĩ': 'i', 'ị': 'i',
      'ò': 'o', 'ó': 'o', 'ỏ': 'o', 'õ': 'o', 'ọ': 'o',
      'ô': 'o', 'ố': 'o', 'ồ': 'o', 'ổ': 'o', 'ỗ': 'o', 'ộ': 'o',
      'ơ': 'o', 'ớ': 'o', 'ờ': 'o', 'ở': 'o', 'ỡ': 'o', 'ợ': 'o',
      'ù': 'u', 'ú': 'u', 'ủ': 'u', 'ũ': 'u', 'ụ': 'u',
      'ư': 'u', 'ứ': 'u', 'ừ': 'u', 'ử': 'u', 'ữ': 'u', 'ự': 'u',
      'ỳ': 'y', 'ý': 'y', 'ỷ': 'y', 'ỹ': 'y', 'ỵ': 'y',
      'đ': 'd'
    };
    
    // Replace Vietnamese characters
    slug = slug.split('').map(char => vietnameseMap[char] || char).join('');
    
    // Remove invalid characters, keep only a-z, 0-9, spaces, and hyphens
    slug = slug.replace(/[^a-z0-9\s-]/g, '');
    
    // Replace multiple spaces/hyphens with single hyphen
    slug = slug.replace(/[\s-]+/g, '-');
    
    // Trim hyphens from start and end
    slug = slug.replace(/^-+|-+$/g, '');
    
    return slug;
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
        const slug = generateSlug(trimmedInput);
        const newTag = await onCreateNewTag(trimmedInput, slug);
        setTags(prev => [...prev, newTag]);
      } else {
        // If no create handler, just add as string
        setTags(prev => [...prev, trimmedInput]);
      }
      setTagInput('');
      setError('');
    } catch (err) {
      setError(err.message || 'Không thể tạo tag mới');
    }
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      
      if (showDropdown && selectedIndex >= 0 && selectedIndex < filteredTags.length) {
        // Select highlighted tag from dropdown
        handleSelectTag(filteredTags[selectedIndex]);
      } else {
        // Create new tag
        handleCreateNewTag();
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
          placeholder="Nhập tag (có dấu được)... Ấn Enter để tạo"
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
              title={isNew ? 'Tag mới' : 'Tag từ database'}
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