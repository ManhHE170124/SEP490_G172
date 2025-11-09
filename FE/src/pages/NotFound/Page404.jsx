import React from 'react';
import './Page404.css';

const Page404 = () => {
  return (
    <main className="not-found">
      <div className="not-found-container">
        <div className="not-found-content">
          <h1 className="not-found-title">404</h1>
          <h2 className="not-found-subtitle">Trang không tìm thấy</h2>
          <p className="not-found-description">
            Xin lỗi, trang bạn đang tìm kiếm không tồn tại hoặc đã bị di chuyển.
          </p>
          <div className="not-found-actions">
            <button 
              className="not-found-button primary"
              onClick={() => window.history.back()}
            >
              Quay lại
            </button>
            <button 
              className="not-found-button secondary"
              onClick={() => window.location.href = '/'}
            >
              Về trang chủ
            </button>
          </div>
        </div>
       
      </div>
    </main>
  );
};

export default Page404;
