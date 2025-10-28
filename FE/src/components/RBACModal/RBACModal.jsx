import React, { useState } from 'react';
import './RBACModal.css';

const RBACModal = ({ 
  isOpen, 
  title, 
  fields, 
  onClose, 
  onSubmit, 
  submitting = false 
}) => {
  const [formData, setFormData] = useState({});
  const [errors, setErrors] = useState({});

  // Initialize form data when modal opens
  React.useEffect(() => {
    if (isOpen) {
      const initialData = {};
      fields.forEach(field => {
        initialData[field.name] = field.defaultValue || (field.type === 'checkbox' ? false : '');
      });
      setFormData(initialData);
      setErrors({});
    }
  }, [isOpen, fields]);

  const handleInputChange = (name, value) => {
    setFormData(prev => ({
      ...prev,
      [name]: value
    }));
    
    // Clear error when user starts typing
    if (errors[name]) {
      setErrors(prev => ({
        ...prev,
        [name]: ''
      }));
    }
  };

  const validateForm = () => {
    const newErrors = {};
    
    fields.forEach(field => {
      if (field.required && (!formData[field.name] || formData[field.name].toString().trim() === '')) {
        newErrors[field.name] = `${field.label} là bắt buộc`;
      }
    });
    
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    
    if (validateForm()) {
      onSubmit(formData);
    }
  };

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
          {fields.map((field) => (
            <div key={field.name} className="form-group">
              <label className="form-label">
                {field.label}
                {field.required && <span style={{ color: 'red' }}> *</span>}
              </label>
              
              {field.type === 'textarea' ? (
                <textarea
                  className={`form-input form-textarea ${errors[field.name] ? 'error' : ''}`}
                  value={formData[field.name] || ''}
                  onChange={(e) => handleInputChange(field.name, e.target.value)}
                  placeholder={field.placeholder || `Nhập ${field.label.toLowerCase()}`}
                  rows={4}
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
              ) : (
                <input
                  type={field.type || 'text'}
                  className={`form-input ${errors[field.name] ? 'error' : ''}`}
                  value={formData[field.name] || ''}
                  onChange={(e) => handleInputChange(field.name, e.target.value)}
                  placeholder={field.placeholder || `Nhập ${field.label.toLowerCase()}`}
                />
              )}
              
              {errors[field.name] && (
                <div className="error-message">{errors[field.name]}</div>
              )}
            </div>
          ))}
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

export default RBACModal;