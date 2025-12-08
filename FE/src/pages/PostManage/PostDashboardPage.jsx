/**
 * @file: PostDashboardPage.jsx
 * @author: HieuNDHE173169
 * @created 2025-01-XX
 * @version: 1.0.0
 * @summary: Dashboard page for post management with statistics and performance charts
 */

import React, { useEffect, useState, useMemo, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { postsApi } from "../../services/postsApi";
import useToast from "../../hooks/useToast";
import ToastContainer from "../../components/Toast/ToastContainer";
import PerformanceStatistics from "../../components/PostManage/PerformanceStatistics";
import PermissionGuard from "../../components/PermissionGuard";
import "./PostDashboardPage.css";

export default function PostDashboardPage() {
  const navigate = useNavigate();
  const { toasts, showError, removeToast, confirmDialog } = useToast();
  
  // Global network error handler
  const networkErrorShownRef = useRef(false);
  useEffect(() => {
    networkErrorShownRef.current = false;
  }, []);

  // Data state
  const [posts, setPosts] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // Load data
  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    setError("");
    try {
      const postsData = await postsApi.getAllPosts();
      setPosts(Array.isArray(postsData) ? postsData : []);
    } catch (err) {
      setError(err.message || "Không thể tải dữ liệu");
      if (err.isNetworkError || err.message === 'Lỗi kết nối đến máy chủ') {
        if (!networkErrorShownRef.current) {
          networkErrorShownRef.current = true;
          showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
        }
      } else {
        showError("Lỗi", err.message || "Không thể tải dữ liệu dashboard");
      }
    } finally {
      setLoading(false);
    }
  };

  // Dashboard statistics
  const dashboardStats = useMemo(() => {
    const totalPosts = posts.length;
    const draftPosts = posts.filter(p => p.status === "Draft").length;
    const totalViews = posts.reduce((sum, p) => sum + (p.viewCount || 0), 0);

    return {
      totalPosts,
      draftPosts,
      totalViews
    };
  }, [posts]);

  return (
    <div className="pdp-dashboard-container">
      <ToastContainer
        toasts={toasts}
        onRemove={removeToast}
        confirmDialog={confirmDialog}
      />

      {/* Header */}
      <div className="pdp-dashboard-header">
        <div>
          <h1 className="pdp-dashboard-title">Dashboard Bài viết</h1>
          <p className="pdp-dashboard-subtitle">Thống kê và phân tích hiệu suất bài viết</p>
        </div>
        <div style={{ display: "flex", gap: "0.75rem" }}>
          <button 
            className="pdp-btn-secondary" 
            onClick={() => navigate("/admin-post-list")}
          >
            Quản lý bài viết
          </button>
          <PermissionGuard moduleCode="POST_MANAGER" permissionCode="CREATE">
            <button 
              className="pdp-btn-primary" 
              onClick={() => navigate("/post-create-edit")}
            >
              + Tạo bài viết mới
            </button>
          </PermissionGuard>
        </div>
      </div>

      {/* Loading State */}
      {loading ? (
        <div className="pdp-loading-state">
          <div className="pdp-loading-spinner" />
          <div>Đang tải dữ liệu...</div>
        </div>
      ) : error ? (
        <div className="pdp-error-state">
          <div>Lỗi: {error}</div>
          <button className="pdp-btn-secondary" onClick={loadData} style={{ marginTop: "12px" }}>
            Thử lại
          </button>
        </div>
      ) : (
        <>
          {/* Dashboard Cards */}
          <div className="pdp-dashboard-section">
            <div className="pdp-dashboard-grid">
              <div className="pdp-dashboard-card pdp-dashboard-card-primary">
                <div className="pdp-dashboard-card-icon">
                  <svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
                    <path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-5 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z"/>
                  </svg>
                </div>
                <div className="pdp-dashboard-card-content">
                  <div className="pdp-dashboard-card-label">Tổng số bài viết</div>
                  <div className="pdp-dashboard-card-value">{dashboardStats.totalPosts}</div>
                </div>
              </div>

              <div className="pdp-dashboard-card pdp-dashboard-card-warning">
                <div className="pdp-dashboard-card-icon">
                  <svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
                    <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/>
                  </svg>
                </div>
                <div className="pdp-dashboard-card-content">
                  <div className="pdp-dashboard-card-label">Bài viết nháp</div>
                  <div className="pdp-dashboard-card-value">{dashboardStats.draftPosts}</div>
                </div>
              </div>

              <div className="pdp-dashboard-card pdp-dashboard-card-info">
                <div className="pdp-dashboard-card-icon">
                  <svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
                    <path d="M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z"/>
                  </svg>
                </div>
                <div className="pdp-dashboard-card-content">
                  <div className="pdp-dashboard-card-label">Lượt xem</div>
                  <div className="pdp-dashboard-card-value">{dashboardStats.totalViews.toLocaleString('vi-VN')}</div>
                </div>
              </div>
            </div>

            {/* Performance Statistics with Charts */}
            <PerformanceStatistics posts={posts} />
          </div>
        </>
      )}
    </div>
  );
}


