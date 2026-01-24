// File: AdminNotificationsPage.jsx

import React, { useEffect, useRef, useState, useCallback } from "react";
import "./admin-notifications-page.css";
import { NotificationsApi } from "../../services/notifications";

const severityOptions = [
  { value: "", label: "Tất cả mức độ" },
  { value: "0", label: "Thông tin" },
  { value: "1", label: "Thành công" },
  { value: "2", label: "Cảnh báo" },
  { value: "3", label: "Lỗi" },
];

function severityLabel(sev) {
  switch (sev) {
    case 0:
      return "Thông tin";
    case 1:
      return "Thành công";
    case 2:
      return "Cảnh báo";
    case 3:
      return "Lỗi";
    default:
      return sev;
  }
}

function severityClass(sev) {
  switch (sev) {
    case 0:
      return "badge-severity badge-info";
    case 1:
      return "badge-severity badge-success";
    case 2:
      return "badge-severity badge-warning";
    case 3:
      return "badge-severity badge-error";
    default:
      return "badge-severity";
  }
}

function notificationTypeLabel(type) {
  const v = String(type || "").trim();
  if (!v) return "-";

  // Normalize để map được cả dạng "Ticket.Assigned", "Key.ImportCsv", ...
  // Ví dụ: "Ticket.Assigned" -> "ticketassigned"
  const raw = v.toLowerCase();
  const key = raw.replace(/[^a-z0-9]/g, "");

  if (key === "manual") return "Thủ công";
  if (key === "system") return "Hệ thống";

  // Map type hệ thống -> tiếng Việt (hiện tại đang dùng trong BE)
  // NOTE: key đã được normalize (bỏ dấu chấm, dấu gạch, khoảng trắng...)
  const map = {
    // Ticket
    ticketassigned: "Gán ticket",
  tickettransferred: "Chuyển ticket",
  ticketstaffreplied: "Ticket có phản hồi",
  supportchatadminassigned: "Gán phiên chat",            
  supportchatadmintransferredtoyou: "Được chuyển phiên chat",  
  supportchatadmintransferredaway: "Bị chuyển phiên chat",  
    keyimportcsv: "Nhập key hàng loạt",

    // Report
  productreportcreated: "Báo lỗi sản phẩm",
  orderneedsmanualaction: "Đơn hàng cần xử lý thủ công",
  ordersharedaccountpurchased: "Đơn hàng mua tài khoản chia sẻ",       
  paymentneedreview: "Giao dịch cần review thủ công",
    // Product account
    productaccountcustomerrevoked: "Thu hồi quyền truy cập tài khoản",

    // (Tuỳ bạn mở rộng sau)
    ordercreated: "Đơn hàng mới",
  };

  if (map[key]) return map[key];

  // fallback: giữ nguyên (dành cho các type tự động mới)
  return v;
}

function notificationTypeLabelForList(item) {
  // Ưu tiên field Type; nếu null thì dựa vào IsSystemGenerated
  const t = String(item?.type || "").trim();
  if (t) return notificationTypeLabel(t);
  return item?.isSystemGenerated ? "Hệ thống" : "Thủ công";
}

function scopeLabelForList(item) {
  if (item?.isGlobal) return "Toàn hệ thống";
  // Nếu sau này BE trả thêm targetRoleCount / targetRolesCount thì FE tự nhận
  const roleCount =
    typeof item?.targetRoleCount === "number"
      ? item.targetRoleCount
      : typeof item?.targetRolesCount === "number"
      ? item.targetRolesCount
      : 0;
  if (roleCount > 0) return "Nhóm quyền";
  return "Người dùng cụ thể";
}


function creatorLabelOnlyName(label) {
  const s = String(label || "").trim();
  if (!s) return s;

  // Nếu BE trả dạng "FullName (email)" thì chỉ lấy FullName
  const idx = s.indexOf(" (");
  if (idx > 0 && s.endsWith(")")) return s.slice(0, idx).trim();

  return s;
}

const isLikelyAbsoluteUrl = (s) => /^https?:\/\//i.test(String(s || "").trim());
const isLikelyRelativeUrl = (s) => /^\//.test(String(s || "").trim());

const renderRelatedUrl = (url) => {
  const v = String(url || "").trim();
  if (!v) return "-";

  if (isLikelyAbsoluteUrl(v)) {
    return (
      <a href={v} target="_blank" rel="noreferrer">
        {v}
      </a>
    );
  }

  if (isLikelyRelativeUrl(v)) {
    return (
      <a href={v} target="_blank" rel="noreferrer">
        {v}
      </a>
    );
  }

  return v;
};

const AdminNotificationsPage = () => {
  // Permission checks removed - now role-based on backend
  const hasCreatePermission = true;
  const hasViewDetailPermission = true;
  
  const [filters, setFilters] = useState({
    search: "",
    severity: "",
    isSystemGenerated: "",
    isGlobal: "",
    status: "",
    type: "",
    createdByEmail: "",
    sortBy: "CreatedAtUtc",
    sortDescending: true,
    createdFromUtc: "",
    createdToUtc: "",
  });

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // Abort previous list requests (debounce + cancel old)
  const listAbortRef = useRef(null);

  // Options dropdown cho bộ lọc danh sách (type + creator)
  const [filterOptions, setFilterOptions] = useState({
    types: [],
    creators: [],
  });
  const [filterOptionsLoading, setFilterOptionsLoading] = useState(false);
  const [filterOptionsError, setFilterOptionsError] = useState("");

  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState("");
  const [selectedNotification, setSelectedNotification] = useState(null);
  const [detailTab, setDetailTab] = useState("info");

  // ✅ Recipients (paged) - loaded from separate endpoint to avoid heavy admin detail payload
  const recipientsPageSize = 20;
  const [recipientsItems, setRecipientsItems] = useState([]);
  const [recipientsTotal, setRecipientsTotal] = useState(0);
  const [recipientsPageNumber, setRecipientsPageNumber] = useState(1);
  const [recipientsLoading, setRecipientsLoading] = useState(false);
  const [recipientsError, setRecipientsError] = useState("");
  const recipientsAbortRef = useRef(null);

  // Options dropdown user / role
  const [targetOptions, setTargetOptions] = useState({
    roles: [],
    users: [],
  });
  const [targetOptionsLoading, setTargetOptionsLoading] = useState(false);
  const [targetOptionsError, setTargetOptionsError] = useState("");

  // Dropdown open/close (mặc định ĐÓNG)
  const [isUserDropdownOpen, setIsUserDropdownOpen] = useState(false);
  const [isRoleDropdownOpen, setIsRoleDropdownOpen] = useState(false);

  const [createForm, setCreateForm] = useState({
    title: "",
    message: "",
    severity: "0",
    isGlobal: false,
    relatedUrl: "",
    selectedRoleIds: [],
    selectedUserIds: [],
  });
  const [createErrors, setCreateErrors] = useState({});
  const [createSubmitting, setCreateSubmitting] = useState(false);

  // ====== Load options user/role cho modal tạo thông báo ======
  useEffect(() => {
    const loadOptions = async () => {
      setTargetOptionsLoading(true);
      setTargetOptionsError("");
      try {
        const data = await NotificationsApi.getManualTargetOptions();
        setTargetOptions({
          roles: Array.isArray(data.roles) ? data.roles : [],
          users: Array.isArray(data.users) ? data.users : [],
        });
      } catch (err) {
        console.error("Không thể tải danh sách user/role", err);
        setTargetOptionsError(
          "Không tải được danh sách người dùng / quyền. Bạn sẽ không thể chọn người nhận."
        );
      } finally {
        setTargetOptionsLoading(false);
      }
    };

    loadOptions();
  }, []);

  // ====== Load options cho bộ lọc danh sách (type + creator) ======
  useEffect(() => {
    const loadFilterOptions = async () => {
      setFilterOptionsLoading(true);
      setFilterOptionsError("");
      try {
        const data = await NotificationsApi.getAdminFilterOptions();

        setFilterOptions({
          types: Array.isArray(data?.types) ? data.types : Array.isArray(data?.Types) ? data.Types : [],
          creators: Array.isArray(data?.creators)
            ? data.creators
            : Array.isArray(data?.Creators)
            ? data.Creators
            : [],
        });
      } catch (err) {
        console.error("Không thể tải option bộ lọc thông báo", err);
        setFilterOptionsError("Không tải được loại thông báo / người tạo.");
        setFilterOptions({ types: [], creators: [] });
      } finally {
        setFilterOptionsLoading(false);
      }
    };

    loadFilterOptions();
  }, []);

  // ====== Load list ======
  const refreshList = useCallback(
    async (signal) => {
      setLoading(true);
      setError("");

      try {
        const params = {
          pageNumber,
          pageSize,
          search: filters.search || undefined,
          severity:
            filters.severity !== "" ? Number(filters.severity) : undefined,
          isSystemGenerated:
            filters.isSystemGenerated === ""
              ? undefined
              : filters.isSystemGenerated === "true",
          isGlobal:
            filters.isGlobal === ""
              ? undefined
              : filters.isGlobal === "true",
          status: (filters.status || "").trim() || undefined,
          type: (filters.type || "").trim() || undefined,
          createdByEmail: (filters.createdByEmail || "").trim() || undefined,
          sortBy: filters.sortBy || "CreatedAtUtc",
          sortDescending: filters.sortDescending,
          createdFromUtc: filters.createdFromUtc || undefined,
          createdToUtc: filters.createdToUtc || undefined,
        };

        const result = await NotificationsApi.listAdminPaged(params, { signal });

        setItems(result.items || []);
        setTotal(result.total || 0);
      } catch (err) {
        if (
          err?.code === "ERR_CANCELED" ||
          err?.name === "CanceledError" ||
          err?.name === "AbortError"
        ) {
          return;
        }
        console.error("Không thể tải danh sách thông báo", err);
        setError("Không tải được danh sách thông báo. Vui lòng thử lại.");
      } finally {
        setLoading(false);
      }
    },
    [filters, pageNumber, pageSize]
  );

  // ✅ Debounce & cancel old list requests to avoid spamming API while typing search/filter
  useEffect(() => {
    if (listAbortRef.current) {
      try {
        listAbortRef.current.abort();
      } catch {}
    }
    const controller = new AbortController();
    listAbortRef.current = controller;

    const t = setTimeout(() => {
      refreshList(controller.signal);
    }, 400);

    return () => {
      clearTimeout(t);
      try {
        controller.abort();
      } catch {}
    };
  }, [refreshList]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const handleFilterChange = (field, value) => {
    setFilters((prev) => ({
      ...prev,
      [field]: value,
    }));
    setPageNumber(1);
  };

  // Sort theo tiêu đề cột: click lần 1 ASC, lần 2 DESC
  const handleSortClick = (sortBy) => {
    // ⚠️ BE cần support sortBy các field này (xem ghi chú cuối file)
    const sortable = new Set([
      "CreatedAtUtc",
      "Title",
      "Severity",
      "Type",
      "CreatedByUserEmail",
    ]);
    if (!sortable.has(sortBy)) return;

    setFilters((prev) => {
      if (prev.sortBy === sortBy) {
        return { ...prev, sortDescending: !prev.sortDescending };
      }
      return { ...prev, sortBy, sortDescending: false }; // lần đầu: ASC
    });
    setPageNumber(1);
  };

  const handleResetFilters = () => {
    setFilters({
      search: "",
      severity: "",
      isSystemGenerated: "",
      isGlobal: "",
      status: "",
      type: "",
      createdByEmail: "",
      sortBy: "CreatedAtUtc",
      sortDescending: true,
      createdFromUtc: "",
      createdToUtc: "",
    });
    setPageNumber(1);
    setPageSize(20);
  };

  const renderSortIndicator = (columnKey) =>
    filters.sortBy === columnKey && (
      <span className="sort-indicator">
        {filters.sortDescending ? " ↓" : " ↑"}
      </span>
    );

  const handleOpenDetail = async (id) => {
    setIsDetailModalOpen(true);
    setDetailTab("info");
    setSelectedNotification(null);
    setDetailError("");
    setDetailLoading(true);

    // reset recipients paging state (loaded lazily when switching to tab)
    setRecipientsItems([]);
    setRecipientsTotal(0);
    setRecipientsPageNumber(1);
    setRecipientsError("");

    try {
      const data = await NotificationsApi.getDetail(id);
      setSelectedNotification(data);
    } catch (err) {
      console.error("Không thể tải chi tiết thông báo", err);
      setDetailError("Không tải được chi tiết thông báo.");
    } finally {
      setDetailLoading(false);
    }
  };

  const loadRecipientsPage = useCallback(
    async (page) => {
      const notificationId = selectedNotification?.id;
      if (!notificationId) return;

      if (recipientsAbortRef.current) {
        try {
          recipientsAbortRef.current.abort();
        } catch {}
      }

      const controller = new AbortController();
      recipientsAbortRef.current = controller;

      setRecipientsLoading(true);
      setRecipientsError("");
      try {
        const res = await NotificationsApi.getAdminRecipientsPaged(
          notificationId,
          { pageNumber: page, pageSize: recipientsPageSize },
          { signal: controller.signal }
        );
        setRecipientsItems(res.items || []);
        setRecipientsTotal(res.total || 0);
        setRecipientsPageNumber(page);
      } catch (err) {
        if (
          err?.code === "ERR_CANCELED" ||
          err?.name === "CanceledError" ||
          err?.name === "AbortError"
        ) {
          return;
        }
        console.error("Không thể tải danh sách người nhận", err);
        setRecipientsError("Không tải được danh sách người nhận.");
      } finally {
        setRecipientsLoading(false);
      }
    },
    [selectedNotification?.id, recipientsPageSize]
  );

  // lazy load recipients when user opens the tab
  useEffect(() => {
    if (!isDetailModalOpen) return;
    // Recipients are shown inside "info" tab in current UI
    if (detailTab !== "info") return;
    if (!selectedNotification?.id) return;

    // If current page isn't loaded yet (fresh open), load page 1
    if (recipientsItems.length === 0 && !recipientsLoading && !recipientsError) {
      loadRecipientsPage(1);
    }
  }, [
    isDetailModalOpen,
    detailTab,
    selectedNotification?.id,
    recipientsItems.length,
    recipientsLoading,
    recipientsError,
    loadRecipientsPage,
  ]);

  const handleOpenCreate = () => {
    setCreateErrors({});
    setCreateForm({
      title: "",
      message: "",
      severity: "0",
      isGlobal: false,
      relatedUrl: "",
      selectedRoleIds: [],
      selectedUserIds: [],
    });
    // Mặc định đóng 2 dropdown khi mở modal
    setIsUserDropdownOpen(false);
    setIsRoleDropdownOpen(false);
    setIsCreateModalOpen(true);
  };

  // Toggle chọn 1 user
  const toggleUserSelection = (userId) => {
    setCreateForm((prev) => {
      const exists = prev.selectedUserIds.includes(userId);
      const selectedUserIds = exists
        ? prev.selectedUserIds.filter((id) => id !== userId)
        : [...prev.selectedUserIds, userId];
      return { ...prev, selectedUserIds };
    });
  };

  // Toggle chọn 1 role
  const toggleRoleSelection = (roleId) => {
    setCreateForm((prev) => {
      if (prev.isGlobal) return prev;

      const exists = prev.selectedRoleIds.includes(roleId);
      const selectedRoleIds = exists
        ? prev.selectedRoleIds.filter((id) => id !== roleId)
        : [...prev.selectedRoleIds, roleId];

      return { ...prev, selectedRoleIds };
    });
  };


  // Nút gạt Gửi toàn hệ thống
  // Global đúng nghĩa: BE tự áp dụng cho tất cả user, FE KHÔNG gửi allUserIds/allRoleIds.
  const handleGlobalToggle = (checked) => {
    setCreateForm((prev) => ({
      ...prev,
      isGlobal: checked,
      // Khi bật global: clear selections (không dùng selections để tạo recipients)
      selectedRoleIds: checked ? [] : prev.selectedRoleIds,
      selectedUserIds: checked ? [] : prev.selectedUserIds,
    }));
  };


  const validateCreateForm = () => {
    const errs = {};

    if (!createForm.title.trim()) {
      errs.title = "Vui lòng nhập tiêu đề thông báo.";
    } else if (createForm.title.trim().length > 200) {
      errs.title = "Tiêu đề tối đa 200 ký tự.";
    }

    if (!createForm.message.trim()) {
      errs.message = "Vui lòng nhập nội dung chi tiết.";
    } else if (createForm.message.trim().length > 1000) {
      errs.message = "Nội dung tối đa 1000 ký tự.";
    }

    // Nếu KHÔNG gửi toàn hệ thống thì bắt buộc phải chọn ít nhất 1 user hoặc 1 role
    const hasUsers =
      Array.isArray(createForm.selectedUserIds) &&
      createForm.selectedUserIds.length > 0;
    const hasRoles =
      Array.isArray(createForm.selectedRoleIds) &&
      createForm.selectedRoleIds.length > 0;

    if (!createForm.isGlobal && !hasUsers && !hasRoles) {
      errs.selectedUserIds =
        "Vui lòng chọn ít nhất một người nhận (user) hoặc một nhóm quyền (role), hoặc bật chế độ gửi toàn hệ thống.";
    }


    const sevNum = Number(createForm.severity);
    if (Number.isNaN(sevNum) || sevNum < 0 || sevNum > 3) {
      errs.severity = "Mức độ phải nằm trong khoảng từ 0 đến 3.";
    }

    if (createForm.relatedUrl && createForm.relatedUrl.length > 1024) {
      errs.relatedUrl = "Đường dẫn liên quan quá dài (tối đa 1024 ký tự).";
    }

    setCreateErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handleCreateSubmit = async (e) => {
    e.preventDefault();
    if (!validateCreateForm()) return;

    // Recipients:
    // - Global: BE tự áp dụng cho tất cả user => không gửi allUserIds/allRoleIds
    // - Non-global: có thể gửi theo userIds và/hoặc roleIds (BE resolve union)
    const targetUserIds = createForm.isGlobal
      ? null
      : createForm.selectedUserIds && createForm.selectedUserIds.length > 0
      ? createForm.selectedUserIds
      : null;

    const targetRoleIds = createForm.isGlobal
      ? null
      : createForm.selectedRoleIds && createForm.selectedRoleIds.length > 0
      ? createForm.selectedRoleIds
      : null;


    const payload = {
      title: createForm.title.trim(),
      message: createForm.message.trim(),
      severity: Number(createForm.severity),
      isGlobal: !!createForm.isGlobal,
      relatedEntityType: null,
      relatedEntityId: null,
      relatedUrl: createForm.relatedUrl.trim() || null,
      targetUserIds: targetUserIds,
      targetRoleIds: targetRoleIds,
    };

    setCreateSubmitting(true);

    try {
      await NotificationsApi.createManual(payload);
      setIsCreateModalOpen(false);
      await refreshList();
      window.alert("Tạo thông báo thành công.");
    } catch (err) {
      console.error("Không thể tạo thông báo", err);
      window.alert("Tạo thông báo thất bại. Vui lòng kiểm tra lại dữ liệu.");
    } finally {
      setCreateSubmitting(false);
    }
  };

  const recipients = recipientsItems;
  const recipientsTotalPages = recipientsTotal
    ? Math.ceil(recipientsTotal / recipientsPageSize)
    : 1;
  const canPrevRecipients = recipientsPageNumber > 1;
  const canNextRecipients = recipientsPageNumber < recipientsTotalPages;

  return (
    <div className="admin-notifications-page">
      <div className="admin-notifications-header">
        <div>
          <h1 className="admin-notifications-title">Thông báo hệ thống</h1>
        </div>
        <div>
          {hasCreatePermission && (
            <button
              type="button"
              className="btn btn-primary"
              onClick={handleOpenCreate}
            >
              + Tạo thông báo
            </button>
          )}
        </div>
      </div>

      {/* BỘ LỌC */}
      <div className="notif-filters">
        <div className="notif-filters-row">
          <div className="notif-filter-item">
            <label className="notif-filter-label">Tìm kiếm</label>
            <input
              type="text"
              className="notif-input"
              placeholder="Tiêu đề / Nội dung..."
              value={filters.search}
              onChange={(e) => handleFilterChange("search", e.target.value)}
            />
          </div>

          <div className="notif-filter-item">
            <label className="notif-filter-label">Mức độ</label>
            <select
              className="notif-select"
              value={filters.severity}
              onChange={(e) => handleFilterChange("severity", e.target.value)}
            >
              {severityOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          <div className="notif-filter-item">
            <label className="notif-filter-label">Nguồn tạo</label>
            <select
              className="notif-select"
              value={filters.isSystemGenerated}
              onChange={(e) =>
                handleFilterChange("isSystemGenerated", e.target.value)
              }
            >
              <option value="">Tất cả</option>
              <option value="true">Chỉ hệ thống</option>
              <option value="false">Chỉ thủ công</option>
            </select>
          </div>

          <div className="notif-filter-item">
            <label className="notif-filter-label">Phạm vi</label>
            <select
              className="notif-select"
              value={filters.isGlobal}
              onChange={(e) => handleFilterChange("isGlobal", e.target.value)}
            >
              <option value="">Tất cả</option>
              <option value="true">Thông báo toàn hệ thống</option>
              <option value="false">Thông báo cho người dùng cụ thể</option>
            </select>
          </div>


          <div className="notif-filter-item">
            <label className="notif-filter-label">Trạng thái</label>
            <select
              className="notif-select"
              value={filters.status}
              onChange={(e) => handleFilterChange("status", e.target.value)}
            >
              <option value="">Tất cả</option>
              <option value="Active">Đang hiệu lực</option>
              <option value="Expired">Hết hạn</option>
              <option value="Archived">Đã lưu trữ</option>
            </select>
          </div>

          <div className="notif-filter-item notif-filter-pagesize">
            <label className="notif-filter-label">Số dòng / trang</label>
            <select
              className="notif-select"
              value={pageSize}
              onChange={(e) => {
                setPageSize(Number(e.target.value) || 20);
                setPageNumber(1);
              }}
            >
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
            </select>
          </div>
        </div>

        <div className="notif-filters-row">
          <div className="notif-filter-item">
            <label className="notif-filter-label">Tạo từ ngày</label>
            <input
              type="date"
              className="notif-input"
              value={filters.createdFromUtc}
              onChange={(e) =>
                handleFilterChange("createdFromUtc", e.target.value)
              }
            />
          </div>
          <div className="notif-filter-item">
            <label className="notif-filter-label">Tạo đến ngày</label>
            <input
              type="date"
              className="notif-input"
              value={filters.createdToUtc}
              onChange={(e) =>
                handleFilterChange("createdToUtc", e.target.value)
              }
            />
          </div>

          <div className="notif-filter-item">
            <label className="notif-filter-label">Loại thông báo</label>
            <select
              className="notif-select"
              value={filters.type}
              onChange={(e) => handleFilterChange("type", e.target.value)}
            >
              <option value="">Tất cả</option>
              {filterOptionsLoading && (
                <option value="" disabled>
                  Đang tải...
                </option>
              )}
              {!filterOptionsLoading &&
                (Array.isArray(filterOptions.types) ? filterOptions.types : [])
                  .map((opt, idx) => {
                    const value =
                      (opt && (opt.value ?? opt.Value)) ??
                      (typeof opt === "string" ? opt : "");
                    if (!String(value || "").trim()) return null;

                    // Nếu BE có Label thì ưu tiên, nếu không thì tự translate theo mapping hiện có
                    const rawLabel =
                      (opt && (opt.label ?? opt.Label)) ??
                      (typeof opt === "string" ? opt : "");

                    const label = rawLabel && rawLabel !== value
                      ? rawLabel
                      : notificationTypeLabel(value);

                    return (
                      <option key={`${value}-${idx}`} value={value}>
                        {label}
                      </option>
                    );
                  })
                  .filter(Boolean)}
            </select>
          </div>

          <div className="notif-filter-item">
            <label className="notif-filter-label">Người tạo</label>
            <select
              className="notif-select"
              value={filters.createdByEmail}
              onChange={(e) => handleFilterChange("createdByEmail", e.target.value)}
            >
              <option value="">Tất cả</option>
              {filterOptionsLoading && (
                <option value="" disabled>
                  Đang tải...
                </option>
              )}
              {!filterOptionsLoading &&
                (Array.isArray(filterOptions.creators)
                  ? filterOptions.creators
                  : [])
                  .map((opt, idx) => {
                    const value =
                      (opt && (opt.value ?? opt.Value)) ??
                      (typeof opt === "string" ? opt : "");
                    if (value === undefined || value === null) return null;

                    const rawLabel =
                      (opt && (opt.label ?? opt.Label)) ??
                      (typeof opt === "string" ? opt : String(value));

                    const label = creatorLabelOnlyName(rawLabel);

                    return (
                      <option key={`${value}-${idx}`} value={value}>
                        {label}
                      </option>
                    );
                  })
                  .filter(Boolean)}
            </select>
          </div>

          <div className="notif-filter-item notif-filter-reset">
            <label className="notif-filter-label">&nbsp;</label>
            <button
              type="button"
              className="btn btn-secondary btn-reset"
              onClick={handleResetFilters}
            >
              Đặt lại mặc định
            </button>
          </div>
        </div>
      </div>

      {/* BẢNG DANH SÁCH */}
      <div className="notif-table-wrapper">
        {loading && <div className="notif-loading">Đang tải...</div>}
        {error && <div className="notif-error">{error}</div>}

        {!loading && !error && (
          <>
            <table className="notif-table">
              <thead>
                <tr>
                  <th
                    className="sortable col-created"
                    onClick={() => handleSortClick("CreatedAtUtc")}
                  >
                    Thời gian tạo
                    {renderSortIndicator("CreatedAtUtc")}
                  </th>
                  <th
                    className="sortable col-title"
                    onClick={() => handleSortClick("Title")}
                  >
                    Tiêu đề / Nội dung
                    {renderSortIndicator("Title")}
                  </th>
                  <th
                    className="sortable col-severity"
                    onClick={() => handleSortClick("Severity")}
                  >
                    Mức độ
                    {renderSortIndicator("Severity")}
                  </th>
                  <th
                    className="sortable col-type"
                    onClick={() => handleSortClick("Type")}
                  >
                    Loại
                    {renderSortIndicator("Type")}
                  </th>
                  <th
                    className="sortable col-createdby"
                    onClick={() => handleSortClick("CreatedByUserEmail")}
                  >
                    Người tạo
                    {renderSortIndicator("CreatedByUserEmail")}
                  </th>
                  <th className="col-scope">Phạm vi</th>
                  <th className="col-recipients">Người nhận</th>
                  <th className="col-actions">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {items.length === 0 && (
                  <tr>
                    <td colSpan={8} className="notif-empty">
                      Không có thông báo nào.
                    </td>
                  </tr>
                )}

                {items.map((n) => (
                  <tr key={n.id}>
                    <td className="col-created">
                      <span className="notif-date">
                        {new Date(n.createdAtUtc).toLocaleString("vi-VN")}
                      </span>
                    </td>
                    <td className="col-title notif-title-cell">
                      <div className="notif-title-text" title={n.title}>
                        {n.title}
                      </div>
                      <div className="notif-message-preview">
                        {n.message.length > 80
                          ? n.message.slice(0, 80) + "..."
                          : n.message}
                      </div>
                    </td>
                    <td className="col-severity">
                      <span className={severityClass(n.severity)}>
                        {severityLabel(n.severity)}
                      </span>
                    </td>
                    <td className="col-type">{notificationTypeLabelForList(n)}</td>
                    <td className="col-createdby">
                      <div className="notif-creator-block">
                        <div
                          className="notif-creator-name"
                          title={
                            n.createdByFullName ||
                            n.createdByEmail ||
                            n.createdByUserEmail ||
                            ""
                          }
                        >
                          {n.createdByFullName ||
                            n.createdByUserEmail ||
                            (n.isSystemGenerated ? "Hệ thống" : "-")}
                        </div>
                        <div
                          className="notif-creator-email"
                          title={n.createdByEmail || n.createdByUserEmail || ""}
                        >
                          {n.createdByEmail || n.createdByUserEmail || ""}
                        </div>
                      </div>
                    </td>
                    <td className="col-scope">{scopeLabelForList(n)}</td>
                    <td className="col-recipients">
                      <div className="notif-rec-block">
                        <div className="notif-rec-line">
                          {n.isGlobal ? "Tất cả người dùng" : "Người dùng cụ thể"}
                        </div>
                        <div className="notif-rec-line">
                          {n.isGlobal ? (
                            <>
                              Đã đọc:{" "}
                              {typeof n.readCount === "number" ? n.readCount : 0}
                              {typeof n.totalTargetUsers === "number" &&
                              n.totalTargetUsers > 0
                                ? ` / ${n.totalTargetUsers}`
                                : ""}
                            </>
                          ) : (
                            <>
                              Người nhận:{" "}
                              {typeof n.totalTargetUsers === "number"
                                ? n.totalTargetUsers
                                : 0}
                              {" · "}Đã đọc:{" "}
                              {typeof n.readCount === "number" ? n.readCount : 0}
                            </>
                          )}
                        </div>
                      </div>
                    </td>
                    <td className="col-actions notif-actions-cell">
                      <button
                        type="button"
                        className="notif-icon-btn"
                        onClick={() => handleOpenDetail(n.id)}
                        aria-label="Xem chi tiết"
                        title="Xem chi tiết"
                      >
                        <svg
                          width="18"
                          height="18"
                          viewBox="0 0 24 24"
                          fill="none"
                          xmlns="http://www.w3.org/2000/svg"
                        >
                          <path
                            d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z"
                            stroke="currentColor"
                            strokeWidth="1.8"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                          />
                          <path
                            d="M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z"
                            stroke="currentColor"
                            strokeWidth="1.8"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                          />
                        </svg>
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {/* PHÂN TRANG */}
            <div className="notif-pagination">
              <div className="notif-pagination-left">
                Tổng: <strong>{total}</strong> bản ghi
              </div>
              <div className="notif-pagination-right">
                <button
                  type="button"
                  className="btn btn-secondary"
                  disabled={pageNumber <= 1}
                  onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                >
                  Trang trước
                </button>
                <span className="notif-page-info">
                  Trang {pageNumber} / {totalPages}
                </span>
                <button
                  type="button"
                  className="btn btn-secondary"
                  disabled={pageNumber >= totalPages}
                  onClick={() =>
                    setPageNumber((p) => Math.min(totalPages, p + 1))
                  }
                >
                  Trang sau
                </button>
              </div>
            </div>
          </>
        )}
      </div>

      {/* CREATE MODAL */}
      {isCreateModalOpen && (
        <div className="notif-modal-backdrop">
          <div className="notif-modal">
            <div className="notif-modal-header">
              <h2>Tạo thông báo thủ công</h2>
              <div className="notif-modal-header-right">
                <span className="notif-global-label">Gửi toàn hệ thống</span>
                <label className="switch">
                  <input
                    type="checkbox"
                    checked={createForm.isGlobal}
                    onChange={(e) => handleGlobalToggle(e.target.checked)}
                  />
                  <span className="slider" />
                </label>
              </div>
            </div>

            <form className="notif-modal-body" onSubmit={handleCreateSubmit}>
              {/* HÀNG 1: Tiêu đề + Mức độ (trái) & Nội dung chi tiết (phải) */}
              <div className="notif-form-row notif-form-row-top">
                <div className="notif-form-col-left">
                  <div className="notif-form-group">
                    <label>
                      Tiêu đề <span className="required">*</span>
                    </label>
                    <input
                      type="text"
                      className={`notif-input ${
                        createErrors.title ? "has-error" : ""
                      }`}
                      value={createForm.title}
                      onChange={(e) =>
                        setCreateForm((prev) => ({
                          ...prev,
                          title: e.target.value,
                        }))
                      }
                    />
                    {createErrors.title && (
                      <div className="field-error">{createErrors.title}</div>
                    )}
                  </div>

                  <div className="notif-form-group">
                    <label>
                      Mức độ <span className="required">*</span>
                    </label>
                    <select
                      className={`notif-select ${
                        createErrors.severity ? "has-error" : ""
                      }`}
                      value={createForm.severity}
                      onChange={(e) =>
                        setCreateForm((prev) => ({
                          ...prev,
                          severity: e.target.value,
                        }))
                      }
                    >
                      <option value="0">Thông tin</option>
                      <option value="1">Thành công</option>
                      <option value="2">Cảnh báo</option>
                      <option value="3">Lỗi</option>
                    </select>
                    {createErrors.severity && (
                      <div className="field-error">
                        {createErrors.severity}
                      </div>
                    )}
                  </div>
                </div>

                <div className="notif-form-col-right">
                  <div className="notif-form-group">
                    <label>
                      Nội dung chi tiết <span className="required">*</span>
                    </label>
                    <textarea
                      className={`notif-textarea ${
                        createErrors.message ? "has-error" : ""
                      }`}
                      rows={5}
                      value={createForm.message}
                      onChange={(e) =>
                        setCreateForm((prev) => ({
                          ...prev,
                          message: e.target.value,
                        }))
                      }
                    />
                    {createErrors.message && (
                      <div className="field-error">
                        {createErrors.message}
                      </div>
                    )}
                  </div>
                </div>
              </div>

              {/* HÀNG 2: Dropdown Người nhận + Nhóm quyền (cùng hàng) */}
              <div className="notif-form-row notif-form-row-targets">
                {/* Người nhận (user) */}
                <div className="notif-form-col-half">
                  <div className="notif-dropdown-group">
                    <div
                      className="notif-dropdown-header"
                      onClick={() =>
                        setIsUserDropdownOpen((open) => !open)
                      }
                    >
                      <div className="notif-dropdown-header-left">
                        <span className="notif-dropdown-title">
                          Áp dụng cho người dùng
                        </span>
                        <span className="notif-dropdown-count">
                          {createForm.isGlobal
                            ? "(Tất cả)"
                            : `(${createForm.selectedUserIds.length}/${targetOptions.users.length} đã chọn)`}
                        </span>
                      </div>
                      <span
                        className={
                          "notif-dropdown-arrow" +
                          (isUserDropdownOpen ? " open" : "")
                        }
                      >
                        ▾
                      </span>
                    </div>

                    {isUserDropdownOpen && (
                      <div className="notif-dropdown-body">
                        {targetOptionsLoading && (
                          <div className="small-muted">
                            Đang tải danh sách người dùng...
                          </div>
                        )}
                        {targetOptionsError && (
                          <div className="field-error">
                            {targetOptionsError}
                          </div>
                        )}

                        <div className="notif-dropdown-list">
                          {targetOptions.users.map((u) => {
                            const selected =
                              createForm.selectedUserIds.includes(u.userId);
                            return (
                              <div
                                key={u.userId}
                                className="notif-dropdown-item"
                              >
                                <div className="notif-select-text">
                                  <div className="notif-select-title">
                                    {u.fullName || "(Chưa có tên)"}
                                  </div>
                                  <div className="notif-select-subtitle">
                                    {u.email}
                                  </div>
                                </div>
                                <label className="switch">
                                  <input
                                    type="checkbox"
                                    checked={selected}
                                    disabled={createForm.isGlobal}
                                    onChange={() =>
                                      toggleUserSelection(u.userId)
                                    }
                                  />
                                  <span className="slider" />
                                </label>
                              </div>
                            );
                          })}

                          {!targetOptionsLoading &&
                            targetOptions.users.length === 0 && (
                              <div className="notif-empty-inline">
                                Không có người dùng nào.
                              </div>
                            )}
                        </div>
                      </div>
                    )}

                    {createErrors.selectedUserIds && (
                      <div className="field-error">
                        {createErrors.selectedUserIds}
                      </div>
                    )}
                  </div>
                </div>

                {/* Nhóm quyền (role) */}
                <div className="notif-form-col-half">
                  <div className="notif-dropdown-group">
                    <div
                      className="notif-dropdown-header"
                      onClick={() =>
                        setIsRoleDropdownOpen((open) => !open)
                      }
                    >
                      <div className="notif-dropdown-header-left">
                        <span className="notif-dropdown-title">
                          Áp dụng cho nhóm quyền
                        </span>
                        <span className="notif-dropdown-count">
                          {createForm.isGlobal
                            ? "(Tất cả)"
                            : `(${createForm.selectedRoleIds.length}/${targetOptions.roles.length} đã chọn)`}
                        </span>
                      </div>
                      <span
                        className={
                          "notif-dropdown-arrow" +
                          (isRoleDropdownOpen ? " open" : "")
                        }
                      >
                        ▾
                      </span>
                    </div>

                    {isRoleDropdownOpen && (
                      <div className="notif-dropdown-body">
                        <div className="notif-dropdown-list">
                          {targetOptions.roles.map((r) => {
                            const selected =
                              createForm.selectedRoleIds.includes(r.roleId);
                            const roleName =
                              r.roleName || r.roleId || "Quyền";
                            return (
                              <div
                                key={r.roleId}
                                className="notif-dropdown-item"
                              >
                                <div className="notif-select-text">
                                  <div className="notif-select-title">
                                    {roleName}
                                  </div>
                                </div>
                                <label className="switch">
                                  <input
                                    type="checkbox"
                                    checked={selected}
                                    disabled={createForm.isGlobal}
                                    onChange={() =>
                                      toggleRoleSelection(r.roleId)
                                    }
                                  />
                                  <span className="slider" />
                                </label>
                              </div>
                            );
                          })}

                          {targetOptions.roles.length === 0 && (
                            <div className="notif-empty-inline">
                              Không có nhóm quyền nào.
                            </div>
                          )}
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              </div>

              {createForm.isGlobal && (
                <div className="notif-help">
                  Đang bật <b>Toàn hệ thống</b>: danh sách người dùng/nhóm quyền
                  chỉ dùng để tham khảo và hệ thống sẽ tự áp dụng cho tất cả
                  user.
                </div>
              )}

              {/* HÀNG 3: Đường dẫn liên quan – hàng cuối */}
              <div className="notif-form-group">
                <label>Đường dẫn liên quan (tùy chọn)</label>
                <input
                  type="text"
                  className={`notif-input ${
                    createErrors.relatedUrl ? "has-error" : ""
                  }`}
                  value={createForm.relatedUrl}
                  onChange={(e) =>
                    setCreateForm((prev) => ({
                      ...prev,
                      relatedUrl: e.target.value,
                    }))
                  }
                  placeholder="VD: /admin/orders/123"
                />
                {createErrors.relatedUrl && (
                  <div className="field-error">
                    {createErrors.relatedUrl}
                  </div>
                )}
              </div>

              <div className="notif-modal-footer">
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setIsCreateModalOpen(false)}
                  disabled={createSubmitting}
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="btn btn-primary"
                  disabled={createSubmitting}
                >
                  {createSubmitting ? "Đang tạo..." : "Tạo thông báo"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* DETAIL MODAL */}
      {isDetailModalOpen && (
        <div className="notif-modal-backdrop">
          <div className="notif-modal notif-modal-detail">
            <div className="notif-modal-header">
              <div className="notif-modal-header-left">
                <h2>Chi tiết thông báo</h2>
              </div>
              {selectedNotification && (
                <div className="notif-modal-header-right">
                  <span
                    className={severityClass(selectedNotification.severity)}
                    title="Mức độ thông báo"
                  >
                    {severityLabel(selectedNotification.severity)}
                  </span>
                </div>
              )}
            </div>

            <div className="notif-modal-body notif-detail-body">
              {detailLoading && (
                <div className="notif-loading">Đang tải chi tiết...</div>
              )}
              {detailError && (
                <div className="notif-error">{detailError}</div>
              )}
              {!detailLoading && !detailError && selectedNotification && (
                <>
                  <div className="notif-detail-tabs">
                    <button
                      type="button"
                      className={
                        "notif-detail-tab" + (detailTab === "info" ? " active" : "")
                      }
                      onClick={() => setDetailTab("info")}
                    >
                      Thông tin
                    </button>
                    <button
                      type="button"
                      className={
                        "notif-detail-tab" + (detailTab === "tech" ? " active" : "")
                      }
                      onClick={() => setDetailTab("tech")}
                    >
                      Kỹ thuật
                    </button>
                  </div>

                  {detailTab === "info" ? (
                    <>
                  {/* Lưới 2 cột cho thông tin meta */}
                  <div className="notif-detail-grid">
                    <div className="detail-row">
                      <span className="detail-label">Tiêu đề:</span>
                      <span className="detail-value">
                        {selectedNotification.title}
                      </span>
                    </div>

                    <div className="detail-row">
                      <span className="detail-label">Phạm vi:</span>
                      <span className="detail-value">
                        {selectedNotification.isGlobal
                          ? "Toàn hệ thống"
                          : "Người dùng cụ thể"}
                      </span>
                    </div>

                    <div className="detail-row">
                      <span className="detail-label">Nguồn tạo:</span>
                      <span className="detail-value">
                        {selectedNotification.isSystemGenerated
                          ? "Hệ thống"
                          : "Thủ công"}
                      </span>
                    </div>

                    <div className="detail-row">
                      <span className="detail-label">Người tạo:</span>
                      <span className="detail-value">
                        {selectedNotification.createdByFullName ||
                          (selectedNotification.isSystemGenerated
                            ? "Hệ thống"
                            : selectedNotification.createdByEmail ||
                              selectedNotification.createdByUserEmail) ||
                          selectedNotification.createdByUserId ||
                          "-"}
                      </span>
                    </div>

                    <div className="detail-row">
                      <span className="detail-label">Thời gian tạo:</span>
                      <span className="detail-value">
                        {new Date(
                          selectedNotification.createdAtUtc
                        ).toLocaleString("vi-VN")}
                      </span>
                    </div>

                    <div className="detail-row">
                      <span className="detail-label">
                        Đường dẫn liên quan:
                      </span>
                      <span className="detail-value">
                        {renderRelatedUrl(selectedNotification.relatedUrl)}
                      </span>
                    </div>
                  </div>

                  {/* Nội dung đầy đủ */}
                  <div className="detail-row detail-row-message">
                    <span className="detail-label">Nội dung:</span>
                    <div className="detail-value detail-value-message">
                      {selectedNotification.message}
                    </div>
                  </div>

                  {/* Thống kê người nhận */}
                  <div className="detail-row">
                    <span className="detail-label">Tổng kết:</span>
                    <span className="detail-value">
                      Người dùng: {selectedNotification.totalTargetUsers} | Đã
                      đọc: {selectedNotification.readCount} | Chưa đọc:{" "}
                      {selectedNotification.unreadCount}
                    </span>
                  </div>

                  {/* Bảng người nhận chi tiết */}
                  <div className="notif-recipient-section">
                    <div className="detail-row">
                      <span className="detail-label">Người nhận chi tiết:</span>
                      <span className="detail-value">
                        &nbsp;
                        {/* chỉ để label nằm cùng hàng, bảng nằm bên dưới */}
                      </span>
                    </div>
                    <div className="notif-recipient-table-wrapper">
                      {recipients.length === 0 ? (
                        <div className="notif-empty-inline">
                          Không có dữ liệu người nhận.
                        </div>
                      ) : (
                        <table className="notif-recipient-table">
                          <thead>
                            <tr>
                              <th>Tên người nhận</th>
                              <th>Email</th>
                              <th>Nhóm quyền</th>
                              <th>Tình trạng</th>
                            </tr>
                          </thead>
                          <tbody>
                            {recipients.map((r, idx) => (
                              <tr key={r.userId || idx}>
                                <td>{r.fullName || "(Chưa có tên)"}</td>
                                <td>{r.email}</td>
                                <td>{r.roleNames || "-"}</td>
                                <td>
                                  {r.isRead ? (
                                    <span className="recipient-status-read">
                                      Đã đọc
                                    </span>
                                  ) : (
                                    <span className="recipient-status-unread">
                                      Chưa đọc
                                    </span>
                                  )}
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      )}
                    </div>
                  </div>

                  {/* Danh sách nhóm quyền (tổng quát) */}
                  <div className="detail-row detail-row-roles">
                    <span className="detail-label">Nhóm quyền nhận:</span>
                    <div className="detail-value">
                      {selectedNotification.targetRoles &&
                      selectedNotification.targetRoles.length > 0 ? (
                        <ul className="detail-role-list">
                          {selectedNotification.targetRoles.map((r) => (
                            <li key={r.roleId}>
                              <span className="role-id">{r.roleId}</span>
                              {r.roleName && (
                                <span className="role-name">
                                  {" "}
                                  - {r.roleName}
                                </span>
                              )}
                            </li>
                          ))}
                        </ul>
                      ) : (
                        <span>-</span>
                      )}
                    </div>
                  </div>
                    </>
                  ) : (
                    <>
                      {(selectedNotification.type ||
                        selectedNotification.correlationId) ? (
                        <div className="notif-tech-panel">
                          <div className="notif-tech-grid">
                            <div className="tech-row">
                              <span className="tech-label">Loại thông báo:</span>
                              <span className="tech-value">
                                {notificationTypeLabel(selectedNotification.type)}
                              </span>
                            </div>

                            <div className="tech-row">
                              <span className="tech-label">Mã truy vết:</span>
                              <span className="tech-value tech-mono">
                                {selectedNotification.correlationId || "-"}
                              </span>
                            </div>
                          </div>
                        </div>
                      ) : (
                        <div className="notif-empty-inline">
                          Không có thông tin kỹ thuật.
                        </div>
                      )}
                    </>
                  )}

                </>
              )}
            </div>

            <div className="notif-modal-footer">
              <button
                type="button"
                className="btn btn-secondary"
                onClick={() => setIsDetailModalOpen(false)}
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminNotificationsPage;
