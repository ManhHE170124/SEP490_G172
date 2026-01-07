/**
 * File: RoleModal.jsx
 * Author: HieuNDHE173169
 * Created: 20/10/2025
 * Last Updated: 29/10/2025
 * Version: 1.0.0
 * Purpose: Reusable modal form component for Role operations (roles, modules, permissions).
 */
import React, { useState } from 'react';
import './RoleModal.css';

/**
 * @summary: Reusable modal form component for Role entity creation and editing.
 * @param {Object} props - Component props
 * @param {boolean} props.isOpen - Whether modal is visible
 * @param {string} props.title - Modal title
 * @param {Array} props.fields - Form field configurations
 * @param {Function} props.onClose - Callback when modal is closed
 * @param {Function} props.onSubmit - Callback when form is submitted
 * @param {boolean} props.submitting - Whether form is being submitted
 * @returns {JSX.Element|null} - Modal form element or null if not open
 */
const RoleModal = ({ 
  isOpen, 
  title, 
  fields, 
  onClose, 
  onSubmit, 
  submitting = false 
}) => {
  const [formData, setFormData] = useState({});
  const [errors, setErrors] = useState({});
  
  // Utility: generate slug from text
  const toSlug = (text) => {
    if (!text) return "";
    return text
      .normalize('NFD')
      // remove combining diacritical marks only (keep base letters)
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/đ/g, 'd').replace(/Đ/g, 'D')
      .replace(/[^a-zA-Z0-9\s-]/g, '')
      .trim()
      .replace(/\s+/g, '-')
      .replace(/-+/g, '-')
      .toLowerCase();
  };

  
  /**
   * @summary: Initialize form data when modal opens.
   * Effect: Resets form data and errors based on field configurations.
   */
  React.useEffect(() => {
    if (isOpen) {
      const initialData = {};
      fields.forEach(field => {
        initialData[field.name] = field.defaultValue || (field.type === 'checkbox' ? false : '');
      });
      // If any field declares syncWith, compute its initial value
      fields.forEach(field => {
        if (field.syncWith) {
          const sourceVal = initialData[field.syncWith] || '';
          initialData[field.name] = toSlug(sourceVal);
        }
      });
      setFormData(initialData);
      setErrors({});
    }
  }, [isOpen, fields]);

  /**
   * @summary: Handle input field value changes.
   * @param {string} name - Field name
   * @param {*} value - New field value
   */
  const handleInputChange = (name, value) => {
    // Auto-convert to uppercase for code fields
    const field = fields.find(f => f.name === name);
    let processedValue = value;
    
    if (field && field.format === 'code' && typeof value === 'string') {
      // Convert to uppercase and remove invalid characters
      processedValue = value.toUpperCase().replace(/[^A-Z0-9_]/g, '');
    }
    
    setFormData(prev => ({
      ...prev,
      [name]: processedValue
    }));
    
    // Clear error when user starts typing
    if (errors[name]) {
      setErrors(prev => ({
        ...prev,
        [name]: ''
      }));
    }
    // If this field is a source for any synced fields, update them
    fields.forEach(f => {
      if (f.syncWith === name) {
        const slugName = f.name;
        const newSlug = toSlug(processedValue);
        setFormData(prev => ({ ...prev, [slugName]: newSlug }));
      }
    });
  };

  /**
   * @summary: Validate form fields based on required rules, length, and format.
   * @returns {boolean} - Whether form is valid
   */
  const validateForm = () => {
    const newErrors = {};
    
    fields.forEach(field => {
      const value = formData[field.name];
      const valueStr = value ? value.toString().trim() : '';
      
      // Required validation
      if (field.required && valueStr === '') {
        newErrors[field.name] = `${field.label} là bắt buộc`;
        return;
      }
      
      // Skip further validation if field is empty and not required
      if (valueStr === '') return;
      
      // Length validation
      if (field.minLength && valueStr.length < field.minLength) {
        newErrors[field.name] = `${field.label} phải có ít nhất ${field.minLength} ký tự`;
        return;
      }
      
      if (field.maxLength && valueStr.length > field.maxLength) {
        newErrors[field.name] = `${field.label} không được vượt quá ${field.maxLength} ký tự`;
        return;
      }
      
      // Format validation (for Code fields - uppercase, numbers, underscore)
      if (field.format === 'code' && valueStr) {
        const codeRegex = /^[A-Z0-9_]+$/;
        if (!codeRegex.test(valueStr)) {
          newErrors[field.name] = `${field.label} chỉ được chứa chữ in hoa, số và dấu gạch dưới`;
          return;
        }
      }
      
      // Format validation (for Slug fields - lowercase, numbers, hyphen)
      if (field.format === 'slug' && valueStr) {
        const slugRegex = /^[a-z0-9-]+$/;
        if (!slugRegex.test(valueStr)) {
          newErrors[field.name] = `${field.label} chỉ được chứa chữ thường, số và dấu gạch ngang`;
          return;
        }
      }
    });
    
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  /**
   * @summary: Handle form submission.
   * @param {Event} e - Form submit event
   */
  const handleSubmit = (e) => {
    e.preventDefault();
    
    if (validateForm()) {
      onSubmit(formData);
    }
  };

  /**
   * @summary: Handle modal close action.
   */
  const handleClose = () => {
    setFormData({});
    setErrors({});
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="modal-overlay active" onClick={handleClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3 className="modal-title">{title}</h3>
          <button className="modal-close" onClick={handleClose}>
            ×
          </button>
        </div>
        
        <form onSubmit={handleSubmit} className="modal-body">
          {fields.map((field) => {
            const fieldValue = formData[field.name] || '';
            const currentLength = fieldValue.toString().length;
            const maxLength = field.maxLength;
            const showCharCount = maxLength && field.type !== 'checkbox';
            
            return (
              <div key={field.name} className="form-group">
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                  <label className="form-label" style={{ margin: 0 }}>
                    {field.label}
                    {field.required && <span style={{ color: 'red' }}> *</span>}
                  </label>
                  {showCharCount && (
                    <div style={{ fontSize: '12px', color: '#6b7280' }}>
                      {currentLength}/{maxLength}
                    </div>
                  )}
                </div>
              
                {field.type === 'textarea' ? (
                  <textarea
                    className={`form-input form-textarea ${errors[field.name] ? 'error' : ''}`}
                    value={fieldValue}
                    onChange={(e) => handleInputChange(field.name, e.target.value)}
                    placeholder={field.placeholder || `Nhập ${field.label.toLowerCase()}`}
                    rows={4}
                    maxLength={maxLength}
                    disabled={field.disabled}
                  />
                ) : field.type === 'checkbox' ? (
                  <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <input
                      type="checkbox"
                      checked={formData[field.name] || false}
                      onChange={(e) => handleInputChange(field.name, e.target.checked)}
                    />
                    <span>{field.label}</span>
                  </label>
                ) : field.type === 'select' ? (
                  <select
                    className={`form-input ${errors[field.name] ? 'error' : ''}`}
                    value={fieldValue}
                    onChange={(e) => handleInputChange(field.name, e.target.value)}
                    disabled={field.disabled}
                  >
                    {field.options && field.options.length > 0 ? (
                      field.options.map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))
                    ) : (
                      <option value="">Không có lựa chọn</option>
                    )}
                  </select>
                ) : (
                  <input
                    type={field.type || 'text'}
                    className={`form-input ${errors[field.name] ? 'error' : ''}`}
                    value={fieldValue}
                    onChange={(e) => handleInputChange(field.name, e.target.value)}
                    placeholder={field.placeholder || `Nhập ${field.label.toLowerCase()}`}
                    disabled={field.disabled || field.readonly}
                    readOnly={field.readonly}
                    maxLength={maxLength}
                    style={field.readonly ? { backgroundColor: '#f3f4f6', cursor: 'not-allowed' } : {}}
                  />
                )}
              
                {errors[field.name] && (
                  <div className="error-message">{errors[field.name]}</div>
                )}
              </div>
            );
          })}
        </form>
        
        <div className="modal-footer">
          <button
            type="button"
            className="btn-modal btn-modal-secondary"
            onClick={handleClose}
            disabled={submitting}
          >
            Hủy
          </button>
          <button
            type="submit"
            className="btn-modal btn-modal-primary"
            onClick={handleSubmit}
            disabled={submitting}
          >
            {submitting ? 'Đang xử lý...' : 'Lưu'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default RoleModal;