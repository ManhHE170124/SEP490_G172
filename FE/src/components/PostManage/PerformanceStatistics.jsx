/**
 * @file: PerformanceStatistics.jsx
 * @summary: Component for displaying post performance statistics with charts
 */

import React, { useMemo, useState } from "react";
import {
  LineChart,
  Line,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer
} from "recharts";
import "./PerformanceStatistics.css";

const COLORS = {
  primary: "#007bff",
  success: "#28a745",
  warning: "#ffc107",
  danger: "#dc3545",
  info: "#17a2b8",
  secondary: "#6c757d"
};

const CHART_COLORS = ["#007bff", "#28a745", "#ffc107", "#dc3545", "#17a2b8", "#6c757d", "#e83e8c", "#20c997"];

export default function PerformanceStatistics({ posts = [] }) {
  const [timeRange, setTimeRange] = useState("30"); // 7, 30, 90, custom

  // Calculate date range based on selection
  const dateRange = useMemo(() => {
    const endDate = new Date();
    const startDate = new Date();
    
    switch (timeRange) {
      case "7":
        startDate.setDate(endDate.getDate() - 7);
        break;
      case "30":
        startDate.setDate(endDate.getDate() - 30);
        break;
      case "90":
        startDate.setDate(endDate.getDate() - 90);
        break;
      default:
        startDate.setDate(endDate.getDate() - 30);
    }
    
    return { startDate, endDate };
  }, [timeRange]);

  // Prepare data for views over time (Line Chart)
  const viewsOverTimeData = useMemo(() => {
    const days = [];
    const dayCount = Math.ceil((dateRange.endDate - dateRange.startDate) / (1000 * 60 * 60 * 24));
    
    // Initialize days array
    for (let i = 0; i <= dayCount; i++) {
      const date = new Date(dateRange.startDate);
      date.setDate(date.getDate() + i);
      days.push({
        date: date.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" }),
        views: 0,
        posts: 0
      });
    }

    // Aggregate data by day
    posts.forEach(post => {
      if (!post.createdAt) return;
      const postDate = new Date(post.createdAt);
      if (postDate >= dateRange.startDate && postDate <= dateRange.endDate) {
        const dayIndex = Math.floor((postDate - dateRange.startDate) / (1000 * 60 * 60 * 24));
        if (dayIndex >= 0 && dayIndex < days.length) {
          days[dayIndex].views += post.viewCount || 0;
          days[dayIndex].posts += 1;
        }
      }
    });

    return days;
  }, [posts, dateRange]);

  // Prepare data for comments (Bar Chart) - Simulated based on posts
  // In real implementation, this would come from comments API
  const commentsData = useMemo(() => {
    const days = [];
    const dayCount = Math.ceil((dateRange.endDate - dateRange.startDate) / (1000 * 60 * 60 * 24));
    
    // Initialize with zero comments
    for (let i = 0; i <= dayCount; i++) {
      const date = new Date(dateRange.startDate);
      date.setDate(date.getDate() + i);
      days.push({
        date: date.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" }),
        comments: 0 // Will be replaced with real data from API
      });
    }

    // TODO: Replace with actual comments data from API
    // For now, simulate based on posts (each post might have some comments)
    posts.forEach(post => {
      if (!post.createdAt) return;
      const postDate = new Date(post.createdAt);
      if (postDate >= dateRange.startDate && postDate <= dateRange.endDate) {
        const dayIndex = Math.floor((postDate - dateRange.startDate) / (1000 * 60 * 60 * 24));
        if (dayIndex >= 0 && dayIndex < days.length) {
          // Simulate: posts with more views might have more comments
          days[dayIndex].comments += Math.floor((post.viewCount || 0) / 100) || 0;
        }
      }
    });

    return days;
  }, [posts, dateRange]);


  // Top posts by views
  const topPostsData = useMemo(() => {
    return [...posts]
      .sort((a, b) => (b.viewCount || 0) - (a.viewCount || 0))
      .slice(0, 10)
      .map(post => ({
        name: post.title ? (post.title.length > 30 ? post.title.substring(0, 30) + "..." : post.title) : "Không có tiêu đề",
        views: post.viewCount || 0
      }));
  }, [posts]);

  return (
    <div className="performance-statistics">
      <div className="performance-statistics-header">
        <h3 className="performance-statistics-title">Thống kê hiệu suất</h3>
        <div className="performance-time-range-selector">
          <button
            className={`time-range-btn ${timeRange === "7" ? "active" : ""}`}
            onClick={() => setTimeRange("7")}
          >
            7 ngày
          </button>
          <button
            className={`time-range-btn ${timeRange === "30" ? "active" : ""}`}
            onClick={() => setTimeRange("30")}
          >
            30 ngày
          </button>
          <button
            className={`time-range-btn ${timeRange === "90" ? "active" : ""}`}
            onClick={() => setTimeRange("90")}
          >
            90 ngày
          </button>
        </div>
      </div>

      <div className="performance-charts-grid">
        {/* Views Over Time - Line Chart */}
        <div className="performance-chart-card">
          <div className="chart-card-header">
            <h4 className="chart-card-title">Lượt xem bài viết</h4>
            <p className="chart-card-subtitle">Theo thời gian</p>
          </div>
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={viewsOverTimeData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" />
              <YAxis />
              <Tooltip />
              <Legend />
              <Line
                type="monotone"
                dataKey="views"
                stroke={COLORS.primary}
                strokeWidth={2}
                name="Lượt xem"
                dot={{ r: 4 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>

        {/* Comments Over Time - Bar Chart */}
        <div className="performance-chart-card">
          <div className="chart-card-header">
            <h4 className="chart-card-title">Tương tác bình luận</h4>
            <p className="chart-card-subtitle">Số lượng bình luận mới</p>
          </div>
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={commentsData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" />
              <YAxis />
              <Tooltip />
              <Legend />
              <Bar dataKey="comments" fill={COLORS.success} name="Bình luận" />
            </BarChart>
          </ResponsiveContainer>
        </div>

        {/* Top Posts - Bar Chart */}
        <div className="performance-chart-card">
          <div className="chart-card-header">
            <h4 className="chart-card-title">Bài viết phổ biến nhất</h4>
            <p className="chart-card-subtitle">Top 10 bài viết có lượt xem cao nhất</p>
          </div>
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={topPostsData} layout="vertical">
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis type="number" />
              <YAxis dataKey="name" type="category" width={150} />
              <Tooltip />
              <Legend />
              <Bar dataKey="views" fill={COLORS.info} name="Lượt xem" />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  );
}

