// File: src/pages/admin/SupportDashboardAdminPage.jsx
import React, { useEffect, useMemo, useState, useRef } from "react";
import "../../styles/SupportDashboardAdminPage.css";
import { supportDashboardAdminApi } from "../../api/supportDashboardAdminApi";
import useToast from "../../hooks/useToast";
import ToastContainer from "../../components/Toast/ToastContainer";
import { usePermission } from "../../hooks/usePermission";
import { MODULE_CODES } from "../../constants/accessControl";
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
    ResponsiveContainer,
    PieChart,
    Pie,
    Cell,
} from "recharts";

const DAY_OPTIONS = [7, 14, 30];

const COLORS = [
    "#2563eb", // blue
    "#10b981", // green
    "#f97316", // orange
    "#ec4899", // pink
    "#8b5cf6", // violet
];

function formatPercent(value) {
    if (value === null || value === undefined || Number.isNaN(value)) return "—";
    return `${(value * 100).toFixed(1)}%`;
}

function formatMinutes(value) {
    if (value === null || value === undefined || Number.isNaN(value)) return "—";
    return `${value.toFixed(1)} phút`;
}

function formatDateLabel(dateStr) {
    // DateOnly / string "YYYY-MM-DD"
    if (!dateStr) return "";
    const parts = `${dateStr}`.split("-");
    if (parts.length !== 3) return dateStr;
    const [y, m, d] = parts;
    return `${d}/${m}`;
}

function formatYearMonthLabel(ym) {
    // "YYYY-MM" => "MM/YYYY"
    if (!ym) return "";
    const [y, m] = ym.split("-");
    if (!y || !m) return ym;
    return `${m}/${y}`;
}

function mapPriorityLabel(level) {
    if (level === 0) return "0 - Standard";
    if (level === 1) return "1 - Priority";
    if (level === 2) return "2 - VIP";
    return `P${level}`;
}

function buildLastMonthsOptions(count = 12) {
    const now = new Date();
    const res = [];
    const anchor = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));

    for (let i = 0; i < count; i++) {
        const d = new Date(anchor);
        d.setUTCMonth(anchor.getUTCMonth() - i);
        const year = d.getUTCFullYear();
        const month = `${d.getUTCMonth() + 1}`.padStart(2, "0");
        const value = `${year}-${month}`;
        res.push({
            value,
            label: formatYearMonthLabel(value),
        });
    }

    return res;
}

function KpiCard({ label, value, subLabel, tone = "default" }) {
    const className = `sd-kpi-card sd-kpi-card-${tone}`;
    return (
        <div className={className}>
            <div className="sd-kpi-label">{label}</div>
            <div className="sd-kpi-value">{value}</div>
            {subLabel && <div className="sd-kpi-sub">{subLabel}</div>}
        </div>
    );
}

function EmptyState({ message = "Không có dữ liệu." }) {
    return <div className="sd-empty">{message}</div>;
}

export default function SupportDashboardAdminPage() {
    const { toasts, showError, removeToast } = useToast();
    
    // Check permission to view list
    const { hasPermission: canViewList, loading: permissionLoading } = usePermission(MODULE_CODES.SUPPORT_MANAGER, "VIEW_LIST");
    
    // Global network error handler
    const networkErrorShownRef = useRef(false);
    // Global permission error handler - only show one toast for permission errors
    const permissionErrorShownRef = useRef(false);
    useEffect(() => {
        networkErrorShownRef.current = false;
        permissionErrorShownRef.current = false;
    }, []);

    const [rangeDays, setRangeDays] = useState(7);
    const monthOptions = useMemo(() => buildLastMonthsOptions(12), []);
    const [selectedMonth, setSelectedMonth] = useState(
        monthOptions.length > 0 ? monthOptions[0].value : null
    );

    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    // Data states
    const [overview, setOverview] = useState(null);
    const [ticketDaily, setTicketDaily] = useState([]); // hiện chưa dùng nhưng giữ lại cho đúng note
    const [ticketPriorityDist, setTicketPriorityDist] = useState([]);
    const [chatDaily, setChatDaily] = useState([]);
    const [chatWeeklyPriority, setChatWeeklyPriority] = useState([]);
    const [staffPerformance, setStaffPerformance] = useState([]);
    const [planActiveDist, setPlanActiveDist] = useState([]);
    const [planMonthlyStats, setPlanMonthlyStats] = useState([]);
    const [priorityDistribution, setPriorityDistribution] = useState([]);
    const [prioritySupportVolume, setPrioritySupportVolume] = useState([]);

    // Ticket weekly severity/priority -> dùng cho bảng SLA theo segment
    const [ticketSeverityPriorityWeekly, setTicketSeverityPriorityWeekly] =
        useState([]);

    const [refreshStamp, setRefreshStamp] = useState(0);

    useEffect(() => {
        let isMounted = true;

        async function fetchAll() {
            setLoading(true);
            setError("");

            try {
                const [
                    overviewRes,
                    ticketDailyRes,
                    ticketWeeklySeverityRes,
                    ticketPriorityDistRes,
                    chatDailyRes,
                    chatWeeklyPriorityRes,
                    staffPerfRes,
                    planActiveDistRes,
                    planMonthlyStatsRes,
                    priorityDistRes,
                    prioritySupportVolumeRes,
                ] = await Promise.all([
                    supportDashboardAdminApi.getOverview({
                        days: rangeDays,
                        yearMonth: selectedMonth,
                    }),
                    supportDashboardAdminApi.getTicketDailyKpi({ days: rangeDays }),
                    supportDashboardAdminApi.getTicketSeverityPriorityWeekly({
                        weeks: 8,
                    }),
                    supportDashboardAdminApi.getTicketPriorityDistribution({
                        days: rangeDays,
                    }),
                    supportDashboardAdminApi.getChatDailyKpi({ days: rangeDays }),
                    supportDashboardAdminApi.getChatPriorityWeekly({ weeks: 8 }),
                    supportDashboardAdminApi.getStaffPerformance({ days: rangeDays }),
                    supportDashboardAdminApi.getActiveSupportPlanDistribution(),
                    supportDashboardAdminApi.getSupportPlanMonthlyStats({ months: 12 }),
                    supportDashboardAdminApi.getPriorityDistribution(),
                    supportDashboardAdminApi.getPrioritySupportVolume({ weeks: 8 }),
                ]);

                if (!isMounted) return;

                // axiosClient đã trả thẳng data, KHÔNG còn .data nữa
                setOverview(overviewRes || null);
                setTicketDaily(ticketDailyRes || []);
                setTicketSeverityPriorityWeekly(ticketWeeklySeverityRes || []);
                setTicketPriorityDist(ticketPriorityDistRes || []);
                setChatDaily(chatDailyRes || []);
                setChatWeeklyPriority(chatWeeklyPriorityRes || []);
                setStaffPerformance(staffPerfRes || []);
                setPlanActiveDist(planActiveDistRes || []);
                setPlanMonthlyStats(planMonthlyStatsRes || []);
                setPriorityDistribution(priorityDistRes || []);
                setPrioritySupportVolume(prioritySupportVolumeRes || []);
            } catch (err) {
                console.error("Failed to load support dashboard data", err);
                if (!isMounted) return;
                const msg =
                    err?.response?.data?.message ||
                    err?.message ||
                    "Không thể tải dữ liệu dashboard.";
                setError(msg);
                
                // Handle network errors globally - only show one toast
                if (err.isNetworkError || err.message === 'Lỗi kết nối đến máy chủ') {
                    if (!networkErrorShownRef.current) {
                        networkErrorShownRef.current = true;
                        showError('Lỗi kết nối', 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối.');
                    }
                } else {
                    // Check if error message contains permission denied - only show once
                    const isPermissionError = err.message?.includes('không có quyền') || 
                                              err.message?.includes('quyền truy cập') ||
                                              err.response?.status === 403;
                    if (isPermissionError && !permissionErrorShownRef.current) {
                        permissionErrorShownRef.current = true;
                        showError("Lỗi tải dữ liệu", msg);
                    } else if (!isPermissionError) {
                        showError("Lỗi", msg);
                    }
                }
            } finally {
                if (isMounted) setLoading(false);
            }
        }

        fetchAll();

        return () => {
            isMounted = false;
        };
    }, [rangeDays, selectedMonth, refreshStamp]);

    // ==== Derived chart data ====

    const overviewDailyChartData = useMemo(() => {
        if (!overview) return [];
        const dailyTrend = overview.dailyTrend ?? overview.DailyTrend;
        if (!dailyTrend || !Array.isArray(dailyTrend)) return [];
        return dailyTrend.map((x) => ({
            dateLabel: formatDateLabel(x.date || x.Date),
            newTickets: x.newTickets ?? x.NewTickets,
            closedTickets: x.closedTickets ?? x.ClosedTickets,
            newChatSessions: x.newChatSessions ?? x.NewChatSessions,
            openTicketsEndOfDay: x.openTicketsEndOfDay ?? x.OpenTicketsEndOfDay,
        }));
    }, [overview]);

    const overviewWeeklyChartData = useMemo(() => {
        if (!overview) return [];
        const weeklyTicketChat =
            overview.weeklyTicketChat ?? overview.WeeklyTicketChat;
        if (!weeklyTicketChat || !Array.isArray(weeklyTicketChat)) return [];
        return weeklyTicketChat.map((x) => ({
            weekLabel: formatDateLabel(x.weekStartDate || x.WeekStartDate),
            ticketCount: x.ticketCount ?? x.TicketCount,
            chatSessionCount: x.chatSessionCount ?? x.ChatSessionCount,
        }));
    }, [overview]);

    const ticketPriorityChartData = useMemo(
        () =>
            ticketPriorityDist.map((x) => ({
                priorityLevel: x.priorityLevel ?? x.PriorityLevel,
                priorityLabel: mapPriorityLabel(x.priorityLevel ?? x.PriorityLevel),
                ticketCount: x.ticketCount ?? x.TicketCount,
            })),
        [ticketPriorityDist]
    );

    const chatDailyChartData = useMemo(
        () =>
            chatDaily.map((x) => ({
                dateLabel: formatDateLabel(x.statDate || x.StatDate),
                sessions: x.newChatSessionsCount ?? x.NewChatSessionsCount,
                frt: x.avgFirstResponseMinutes ?? x.AvgFirstResponseMinutes,
                dur: x.avgDurationMinutes ?? x.AvgDurationMinutes,
            })),
        [chatDaily]
    );

    const planMonthlyChartData = useMemo(() => {
        if (!planMonthlyStats || planMonthlyStats.length === 0) return [];

        // Gộp theo YearMonth (tổng revenue & volume)
        const map = new Map();
        for (const item of planMonthlyStats) {
            const ym = item.yearMonth ?? item.YearMonth;
            if (!map.has(ym)) {
                map.set(ym, {
                    yearMonth: ym,
                    label: formatYearMonthLabel(ym),
                    revenue: 0,
                    tickets: 0,
                    chats: 0,
                });
            }
            const agg = map.get(ym);
            agg.revenue += item.supportPlanRevenue ?? item.SupportPlanRevenue ?? 0;
            agg.tickets += item.ticketsCount ?? item.TicketsCount ?? 0;
            agg.chats += item.chatSessionsCount ?? item.ChatSessionsCount ?? 0;
        }

        return Array.from(map.values()).sort((a, b) =>
            a.yearMonth.localeCompare(b.yearMonth)
        );
    }, [planMonthlyStats]);

    const prioritySupportVolumeChartData = useMemo(
        () =>
            prioritySupportVolume.map((x) => ({
                priorityLevel: x.priorityLevel ?? x.PriorityLevel,
                priorityLabel: mapPriorityLabel(x.priorityLevel ?? x.PriorityLevel),
                ticketCount: x.ticketCount ?? x.TicketCount,
                chatSessionsCount: x.chatSessionsCount ?? x.ChatSessionsCount,
            })),
        [prioritySupportVolume]
    );

    const staffTop10 = useMemo(
        () => (staffPerformance || []).slice(0, 10),
        [staffPerformance]
    );

    // Ticket SLA theo Severity + Priority => bảng
    const ticketSlaTable = useMemo(() => {
        if (!ticketSeverityPriorityWeekly || ticketSeverityPriorityWeekly.length === 0)
            return [];

        // Gộp theo Severity + PriorityLevel
        const map = new Map();
        for (const item of ticketSeverityPriorityWeekly) {
            const severity = item.severity ?? item.Severity ?? "Unknown";
            const priority = item.priorityLevel ?? item.PriorityLevel;
            const key = `${severity}#${priority}`;
            if (!map.has(key)) {
                map.set(key, {
                    severity,
                    priorityLevel: priority,
                    tickets: 0,
                    respMet: 0,
                    respTotal: 0,
                    resMet: 0,
                    resTotal: 0,
                });
            }
            const agg = map.get(key);
            agg.tickets += item.ticketsCount ?? item.TicketsCount ?? 0;
            agg.respMet += item.responseSlaMetCount ?? item.ResponseSlaMetCount ?? 0;
            agg.respTotal +=
                item.responseSlaTotalCount ?? item.ResponseSlaTotalCount ?? 0;
            agg.resMet += item.resolutionSlaMetCount ?? item.ResolutionSlaMetCount ?? 0;
            agg.resTotal +=
                item.resolutionSlaTotalCount ?? item.ResolutionSlaTotalCount ?? 0;
        }

        return Array.from(map.values()).sort((a, b) => {
            if (a.severity === b.severity) {
                return a.priorityLevel - b.priorityLevel;
            }
            return a.severity.localeCompare(b.severity);
        });
    }, [ticketSeverityPriorityWeekly]);

    const priorityPieData = useMemo(() => {
        if (!priorityDistribution || priorityDistribution.length === 0) return [];
        return priorityDistribution.map((x) => ({
            priorityLevel: x.priorityLevel ?? x.PriorityLevel,
            priorityLabel: mapPriorityLabel(x.priorityLevel ?? x.PriorityLevel),
            userCount: x.userCount ?? x.UserCount,
        }));
    }, [priorityDistribution]);

    const activePlanPieData = useMemo(() => {
        if (!planActiveDist || planActiveDist.length === 0) return [];
        return planActiveDist.map((x) => ({
            id: x.supportPlanId ?? x.SupportPlanId,
            name: x.planName ?? x.PlanName,
            priorityLevel: x.priorityLevel ?? x.PriorityLevel,
            activeCount: x.activeSubscriptionsCount ?? x.ActiveSubscriptionsCount,
        }));
    }, [planActiveDist]);

    const totalActiveSubs = activePlanPieData.reduce(
        (sum, x) => sum + x.activeCount,
        0
    );
    const totalPriorityUsers = priorityPieData.reduce(
        (sum, x) => sum + x.userCount,
        0
    );

    const overviewCards = useMemo(() => {
        if (!overview) return [];

        const openTicketsNow =
            overview.openTicketsNow ?? overview.OpenTicketsNow;
        const newTicketsLastNDays =
            overview.newTicketsLastNDays ?? overview.NewTicketsLastNDays;
        const newChatSessionsLastNDays =
            overview.newChatSessionsLastNDays ?? overview.NewChatSessionsLastNDays;
        const ticketResponseSlaRateLastNDays =
            overview.ticketResponseSlaRateLastNDays ??
            overview.TicketResponseSlaRateLastNDays;
        const ticketResolutionSlaRateLastNDays =
            overview.ticketResolutionSlaRateLastNDays ??
            overview.TicketResolutionSlaRateLastNDays;

        return [
            {
                key: "openTickets",
                label: "Ticket đang mở",
                value: openTicketsNow,
                tone: "primary",
            },
            {
                key: "newTickets",
                label: `Ticket mới ${rangeDays} ngày`,
                value: newTicketsLastNDays,
                tone: "default",
            },
            {
                key: "newChats",
                label: `Phiên chat mới ${rangeDays} ngày`,
                value: newChatSessionsLastNDays,
                tone: "default",
            },
            {
                key: "sla",
                label: "Tỉ lệ SLA (Resp / Reso)",
                value: `${formatPercent(
                    ticketResponseSlaRateLastNDays
                )} / ${formatPercent(ticketResolutionSlaRateLastNDays)}`,
                tone: "success",
            },
        ];
    }, [overview, rangeDays]);

    // Show loading while checking permission
    if (permissionLoading) {
        return (
            <div className="sd-page">
                <div className="sd-loading">Đang kiểm tra quyền truy cập...</div>
            </div>
        );
    }

    // Show access denied message if no VIEW_LIST permission
    if (!canViewList) {
        return (
            <div className="sd-page">
                <div className="sd-error">
                    <h2>Không có quyền truy cập</h2>
                    <p>Bạn không có quyền xem dashboard hỗ trợ. Vui lòng liên hệ quản trị viên để được cấp quyền.</p>
                </div>
            </div>
        );
    }

    return (
        <div className="sd-page">
            <ToastContainer toasts={toasts} removeToast={removeToast} />
            <div className="sd-header">
                <div>
                    <h1 className="sd-title">Support Dashboard</h1>
                    <p className="sd-subtitle">
                        Tổng quan hiệu suất hỗ trợ (ticket + live chat), hiệu suất nhân viên
                        và hiệu quả các gói hỗ trợ (Support Plans).
                    </p>
                </div>
                <div className="sd-filters">
                    <div className="sd-filter-group">
                        <label className="sd-filter-label">Khoảng ngày</label>
                        <select
                            className="sd-select"
                            value={rangeDays}
                            onChange={(e) => setRangeDays(Number(e.target.value) || 7)}
                        >
                            {DAY_OPTIONS.map((d) => (
                                <option key={d} value={d}>
                                    {d} ngày gần nhất
                                </option>
                            ))}
                        </select>
                    </div>

                    <div className="sd-filter-group">
                        <label className="sd-filter-label">Tháng (Weekly Ticket vs Chat)</label>
                        <select
                            className="sd-select"
                            value={selectedMonth || ""}
                            onChange={(e) => setSelectedMonth(e.target.value || null)}
                        >
                            {monthOptions.map((m) => (
                                <option key={m.value} value={m.value}>
                                    {m.label}
                                </option>
                            ))}
                        </select>
                    </div>

                    <button
                        type="button"
                        className="sd-btn-refresh"
                        onClick={() => setRefreshStamp(Date.now())}
                    >
                        Làm mới
                    </button>
                </div>
            </div>

            {error && <div className="sd-error">{error}</div>}
            {loading && (
                <div className="sd-loading">Đang tải dữ liệu dashboard, vui lòng đợi…</div>
            )}

            {/* Khu 1: Overview */}
            <section className="sd-section">
                <div className="sd-section-header">
                    <h2 className="sd-section-title">1. Tổng quan (Overview)</h2>
                    <p className="sd-section-desc">
                        Số lượng ticket mở/đóng, phiên chat, SLA tổng quan và xu hướng theo ngày
                        / tuần.
                    </p>
                </div>

                <div className="sd-kpi-row">
                    {overviewCards.map((kpi) => (
                        <KpiCard
                            key={kpi.key}
                            label={kpi.label}
                            value={kpi.value ?? "—"}
                            tone={kpi.tone}
                        />
                    ))}
                </div>

                <div className="sd-grid sd-grid-2">
                    <div className="sd-card">
                        <div className="sd-card-header">
                            <div>
                                <div className="sd-card-title">Xu hướng hằng ngày</div>
                                <div className="sd-card-subtitle">
                                    Ticket mới/đóng & phiên chat theo ngày (trong {rangeDays} ngày).
                                </div>
                            </div>
                        </div>
                        <div className="sd-card-body sd-chart-body">
                            {overviewDailyChartData.length === 0 ? (
                                <EmptyState />
                            ) : (
                                <ResponsiveContainer width="100%" height={260}>
                                    <LineChart data={overviewDailyChartData}>
                                        <CartesianGrid strokeDasharray="3 3" />
                                        <XAxis dataKey="dateLabel" />
                                        <YAxis />
                                        <Tooltip />
                                        <Legend />
                                        <Line
                                            type="monotone"
                                            dataKey="newTickets"
                                            name="Ticket mới"
                                            stroke={COLORS[0]}
                                            dot={false}
                                        />
                                        <Line
                                            type="monotone"
                                            dataKey="closedTickets"
                                            name="Ticket đóng"
                                            stroke={COLORS[1]}
                                            dot={false}
                                        />
                                        <Line
                                            type="monotone"
                                            dataKey="newChatSessions"
                                            name="Phiên chat"
                                            stroke={COLORS[2]}
                                            dot={false}
                                        />
                                    </LineChart>
                                </ResponsiveContainer>
                            )}
                        </div>
                    </div>

                    <div className="sd-card">
                        <div className="sd-card-header">
                            <div>
                                <div className="sd-card-title">Ticket vs Chat theo tuần</div>
                                <div className="sd-card-subtitle">
                                    Tổng số ticket & phiên chat theo tuần trong tháng{" "}
                                    {selectedMonth && formatYearMonthLabel(selectedMonth)}.
                                </div>
                            </div>
                        </div>
                        <div className="sd-card-body sd-chart-body">
                            {overviewWeeklyChartData.length === 0 ? (
                                <EmptyState />
                            ) : (
                                <ResponsiveContainer width="100%" height={260}>
                                    <BarChart data={overviewWeeklyChartData}>
                                        <CartesianGrid strokeDasharray="3 3" />
                                        <XAxis dataKey="weekLabel" />
                                        <YAxis />
                                        <Tooltip />
                                        <Legend />
                                        <Bar
                                            dataKey="ticketCount"
                                            name="Ticket"
                                            fill={COLORS[0]}
                                            radius={[4, 4, 0, 0]}
                                        />
                                        <Bar
                                            dataKey="chatSessionCount"
                                            name="Phiên chat"
                                            fill={COLORS[2]}
                                            radius={[4, 4, 0, 0]}
                                        />
                                    </BarChart>
                                </ResponsiveContainer>
                            )}
                        </div>
                    </div>
                </div>
            </section>

            {/* Khu 2: Ticket & SLA */}
            <section className="sd-section">
                <div className="sd-section-header">
                    <h2 className="sd-section-title">2. Ticket & SLA</h2>
                    <p className="sd-section-desc">
                        Phân bố ticket theo PriorityLevel, SLA theo Severity + PriorityLevel và
                        một số KPI chính.
                    </p>
                </div>

                <div className="sd-grid sd-grid-2">
                    {/* Ticket Priority Distribution */}
                    <div className="sd-card">
                        <div className="sd-card-header">
                            <div>
                                <div className="sd-card-title">
                                    Ticket theo PriorityLevel ({rangeDays} ngày)
                                </div>
                                <div className="sd-card-subtitle">
                                    Dùng cho bar chart & so sánh lượng ticket giữa các segment.
                                </div>
                            </div>
                        </div>
                        <div className="sd-card-body sd-chart-body">
                            {ticketPriorityChartData.length === 0 ? (
                                <EmptyState />
                            ) : (
                                <ResponsiveContainer width="100%" height={260}>
                                    <BarChart data={ticketPriorityChartData}>
                                        <CartesianGrid strokeDasharray="3 3" />
                                        <XAxis dataKey="priorityLabel" />
                                        <YAxis allowDecimals={false} />
                                        <Tooltip />
                                        <Bar
                                            dataKey="ticketCount"
                                            name="Số ticket"
                                            radius={[4, 4, 0, 0]}
                                        >
                                            {ticketPriorityChartData.map((entry, index) => (
                                                <Cell
                                                    key={entry.priorityLevel}
                                                    fill={COLORS[index % COLORS.length]}
                                                />
                                            ))}
                                        </Bar>
                                    </BarChart>
                                </ResponsiveContainer>
                            )}
                        </div>
                    </div>

                    {/* Ticket SLA by Severity & Priority */}
                    <div className="sd-card">
                        <div className="sd-card-header">
                            <div>
                                <div className="sd-card-title">
                                    SLA theo Severity + PriorityLevel (8 tuần gần nhất)
                                </div>
                                <div className="sd-card-subtitle">
                                    Bảng tóm tắt tỷ lệ SLA response / resolution theo segment.
                                </div>
                            </div>
                        </div>
                        <div className="sd-card-body">
                            {ticketSlaTable.length === 0 ? (
                                <EmptyState />
                            ) : (
                                <div className="sd-table-wrapper">
                                    <table className="sd-table">
                                        <thead>
                                            <tr>
                                                <th>Severity</th>
                                                <th>Priority</th>
                                                <th>Tickets</th>
                                                <th>SLA Response</th>
                                                <th>SLA Resolution</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {ticketSlaTable.map((row) => {
                                                const respRate =
                                                    row.respTotal > 0
                                                        ? row.respMet / row.respTotal
                                                        : null;
                                                const resRate =
                                                    row.resTotal > 0 ? row.resMet / row.resTotal : null;
                                                return (
                                                    <tr
                                                        key={`${row.severity}-${row.priorityLevel}`}
                                                        className="sd-row-small"
                                                    >
                                                        <td>{row.severity}</td>
                                                        <td>{mapPriorityLabel(row.priorityLevel)}</td>
                                                        <td>{row.tickets}</td>
                                                        <td>{formatPercent(respRate)}</td>
                                                        <td>{formatPercent(resRate)}</td>
                                                    </tr>
                                                );
                                            })}
                                        </tbody>
                                    </table>
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            </section>

            {/* Khu 3: Live Chat KPI */}
            <section className="sd-section">
                <div className="sd-section-header">
                    <h2 className="sd-section-title">3. Live Chat KPI</h2>
                    <p className="sd-section-desc">
                        Số phiên chat theo ngày, thời gian phản hồi đầu tiên và thời lượng trung
                        bình; histogram thời lượng theo PriorityLevel.
                    </p>
                </div>

                <div className="sd-grid sd-grid-2">
                    {/* Daily chat */}
                    <div className="sd-card">
                        <div className="sd-card-header">
                            <div>
                                <div className="sd-card-title">
                                    Phiên chat theo ngày ({rangeDays} ngày)
                                </div>
                                <div className="sd-card-subtitle">
                                    Số phiên chat & thời gian phản hồi trung bình.
                                </div>
                            </div>
                        </div>
                        <div className="sd-card-body sd-chart-body">
                            {chatDailyChartData.length === 0 ? (
                                <EmptyState />
                            ) : (
                                <ResponsiveContainer width="100%" height={260}>
                                    <LineChart data={chatDailyChartData}>
                                        <CartesianGrid strokeDasharray="3 3" />
                                        <XAxis dataKey="dateLabel" />
                                        <YAxis yAxisId="left" />
                                        <YAxis yAxisId="right" orientation="right" />
                                        <Tooltip />
                                        <Legend />
                                        <Line
                                            yAxisId="left"
                                            type="monotone"
                                            dataKey="sessions"
                                            name="Phiên chat"
                                            stroke={COLORS[0]}
                                            dot={false}
                                        />
                                        <Line
                                            yAxisId="right"
                                            type="monotone"
                                            dataKey="frt"
                                            name="FRT (phút)"
                                            stroke={COLORS[1]}
                                            dot={false}
                                        />
                                        <Line
                                            yAxisId="right"
                                            type="monotone"
                                            dataKey="dur"
                                            name="Duration (phút)"
                                            stroke={COLORS[2]}
                                            dot={false}
                                        />
                                    </LineChart>
                                </ResponsiveContainer>
                            )}
                        </div>
                    </div>

                    {/* Weekly chat by priority (histogram) */}
                    <div className="sd-card">
                        <div className="sd-card-header">
                            <div>
                                <div className="sd-card-title">
                                    Phân bố thời lượng chat theo PriorityLevel (8 tuần)
                                </div>
                                <div className="sd-card-subtitle">
                                    Tổng hợp bucket &lt;5, 5–10, 10–20, &gt;20 phút cho từng
                                    PriorityLevel.
                                </div>
                            </div>
                        </div>
                        <div className="sd-card-body">
                            {chatWeeklyPriority.length === 0 ? (
                                <EmptyState />
                            ) : (
                                <div className="sd-table-wrapper">
                                    <table className="sd-table">
                                        <thead>
                                            <tr>
                                                <th>Priority</th>
                                                <th>Sessions</th>
                                                <th>&lt;5 phút</th>
                                                <th>5–10 phút</th>
                                                <th>10–20 phút</th>
                                                <th>&gt;20 phút</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {chatWeeklyPriority.map((row) => (
                                                <tr
                                                    key={`${row.priorityLevel ?? row.PriorityLevel}-${row.weekStartDate || row.WeekStartDate
                                                        }`}
                                                >
                                                    <td>
                                                        {mapPriorityLabel(
                                                            row.priorityLevel ?? row.PriorityLevel
                                                        )}
                                                    </td>
                                                    <td>{row.sessionsCount ?? row.SessionsCount}</td>
                                                    <td>{row.duration05Count ?? row.Duration05Count}</td>
                                                    <td>{row.duration510Count ?? row.Duration510Count}</td>
                                                    <td>{row.duration1020Count ?? row.Duration1020Count}</td>
                                                    <td>
                                                        {row.duration20plusCount ?? row.Duration20plusCount}
                                                    </td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            </section>

            {/* Khu 4: Staff Performance */}
            <section className="sd-section">
                <div className="sd-section-header">
                    <h2 className="sd-section-title">4. Hiệu suất nhân viên</h2>
                    <p className="sd-section-desc">
                        Ranking top nhân viên theo số ticket xử lý, SLA và số phiên chat.
                    </p>
                </div>

                <div className="sd-card">
                    <div className="sd-card-header">
                        <div>
                            <div className="sd-card-title">
                                Top 10 nhân viên (theo ticket resolved)
                            </div>
                            <div className="sd-card-subtitle">
                                Gộp số liệu trong {rangeDays} ngày gần nhất.
                            </div>
                        </div>
                    </div>
                    <div className="sd-card-body">
                        {staffTop10.length === 0 ? (
                            <EmptyState />
                        ) : (
                            <div className="sd-table-wrapper">
                                <table className="sd-table">
                                    <thead>
                                        <tr>
                                            <th>#</th>
                                            <th>Nhân viên</th>
                                            <th>Ticket assigned</th>
                                            <th>Ticket resolved</th>
                                            <th>FRT (avg)</th>
                                            <th>Resolution (avg)</th>
                                            <th>SLA Resp</th>
                                            <th>SLA Reso</th>
                                            <th>Chat sessions</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {staffTop10.map((s, index) => {
                                            const respRate =
                                                s.ticketResponseSlaRate ?? s.TicketResponseSlaRate;
                                            const resRate =
                                                s.ticketResolutionSlaRate ?? s.TicketResolutionSlaRate;

                                            return (
                                                <tr key={s.staffId ?? s.StaffId}>
                                                    <td>{index + 1}</td>
                                                    <td>
                                                        <div className="sd-staff-col">
                                                            <div className="sd-staff-name">
                                                                {(s.fullName ?? s.FullName) || "(No name)"}
                                                            </div>
                                                            <div className="sd-staff-email">
                                                                {s.email ?? s.Email}
                                                            </div>
                                                        </div>
                                                    </td>
                                                    <td>{s.ticketsAssignedCount ?? s.TicketsAssignedCount}</td>
                                                    <td>{s.ticketsResolvedCount ?? s.TicketsResolvedCount}</td>
                                                    <td>
                                                        {formatMinutes(
                                                            s.avgTicketFirstResponseMinutes ??
                                                            s.AvgTicketFirstResponseMinutes
                                                        )}
                                                    </td>
                                                    <td>
                                                        {formatMinutes(
                                                            s.avgTicketResolutionMinutes ??
                                                            s.AvgTicketResolutionMinutes
                                                        )}
                                                    </td>
                                                    <td>{formatPercent(respRate)}</td>
                                                    <td>{formatPercent(resRate)}</td>
                                                    <td>
                                                        {s.chatSessionsHandledCount ??
                                                            s.ChatSessionsHandledCount}
                                                    </td>
                                                </tr>
                                            );
                                        })}
                                    </tbody>
                                </table>
                            </div>
                        )}
                    </div>
                </div>
            </section>

            {/* Khu 5: Support Plan & Loyalty */}
            <section className="sd-section">
                <div className="sd-section-header">
                    <h2 className="sd-section-title">5. Support Plan & Loyalty</h2>
                    <p className="sd-section-desc">
                        Phân bố user theo SupportPriorityLevel, số subscription đang active cho
                        từng plan và doanh thu support plan theo tháng.
                    </p>
                </div>

                <div className="sd-grid sd-grid-2">
                    {/* Pie: Active plan + Priority distribution */}
                    <div className="sd-card">
                        <div className="sd-card-header">
                            <div>
                                <div className="sd-card-title">Active Support Plans</div>
                                <div className="sd-card-subtitle">
                                    Phân bố subscription đang active theo SupportPlan + phân bố user
                                    theo PriorityLevel.
                                </div>
                            </div>
                        </div>
                        <div className="sd-card-body sd-card-body-split">
                            <div className="sd-pie-wrapper">
                                <div className="sd-pie-title">
                                    Subscription đang active (theo plan)
                                </div>
                                {activePlanPieData.length === 0 ? (
                                    <EmptyState message="Chưa có subscription active." />
                                ) : (
                                    <>
                                        <ResponsiveContainer width="100%" height={230}>
                                            <PieChart>
                                                <Pie
                                                    dataKey="activeCount"
                                                    data={activePlanPieData}
                                                    outerRadius={80}
                                                    label={(entry) => entry.name}
                                                >
                                                    {activePlanPieData.map((entry, index) => (
                                                        <Cell
                                                            key={entry.id}
                                                            fill={COLORS[index % COLORS.length]}
                                                        />
                                                    ))}
                                                </Pie>
                                                <Tooltip />
                                            </PieChart>
                                        </ResponsiveContainer>
                                        <div className="sd-pie-legend">
                                            <div className="sd-pie-total">
                                                Tổng active: <strong>{totalActiveSubs}</strong>
                                            </div>
                                            <ul>
                                                {activePlanPieData.map((p, index) => (
                                                    <li key={p.id}>
                                                        <span
                                                            className="sd-pie-dot"
                                                            style={{
                                                                backgroundColor:
                                                                    COLORS[index % COLORS.length],
                                                            }}
                                                        />
                                                        <span className="sd-pie-label">
                                                            {p.name} ({mapPriorityLabel(p.priorityLevel)})
                                                        </span>
                                                        <span className="sd-pie-value">
                                                            {p.activeCount}
                                                        </span>
                                                    </li>
                                                ))}
                                            </ul>
                                        </div>
                                    </>
                                )}
                            </div>

                            <div className="sd-pie-wrapper">
                                <div className="sd-pie-title">
                                    Phân bố user theo SupportPriorityLevel
                                </div>
                                {priorityPieData.length === 0 ? (
                                    <EmptyState message="Chưa có user thực tế." />
                                ) : (
                                    <>
                                        <ResponsiveContainer width="100%" height={220}>
                                            <PieChart>
                                                <Pie
                                                    dataKey="userCount"
                                                    data={priorityPieData}
                                                    outerRadius={80}
                                                    label={(entry) => entry.priorityLabel}
                                                >
                                                    {priorityPieData.map((entry, index) => (
                                                        <Cell
                                                            key={entry.priorityLevel}
                                                            fill={COLORS[index % COLORS.length]}
                                                        />
                                                    ))}
                                                </Pie>
                                                <Tooltip />
                                            </PieChart>
                                        </ResponsiveContainer>
                                        <div className="sd-pie-legend">
                                            <div className="sd-pie-total">
                                                Tổng user: <strong>{totalPriorityUsers}</strong>
                                            </div>
                                            <ul>
                                                {priorityPieData.map((p, index) => (
                                                    <li key={p.priorityLevel}>
                                                        <span
                                                            className="sd-pie-dot"
                                                            style={{
                                                                backgroundColor:
                                                                    COLORS[index % COLORS.length],
                                                            }}
                                                        />
                                                        <span className="sd-pie-label">
                                                            {p.priorityLabel}
                                                        </span>
                                                        <span className="sd-pie-value">
                                                            {p.userCount}
                                                        </span>
                                                    </li>
                                                ))}
                                            </ul>
                                        </div>
                                    </>
                                )}
                            </div>
                        </div>
                    </div>

                    {/* Bar: Support plan monthly stats + Priority support volume */}
                    <div className="sd-card">
                        <div className="sd-card-header">
                            <div>
                                <div className="sd-card-title">
                                    Doanh thu & volume Support Plan theo tháng
                                </div>
                                <div className="sd-card-subtitle">
                                    Tổng hợp theo tất cả plan – dùng cho trend doanh thu & volume
                                    ticket/chat.
                                </div>
                            </div>
                        </div>
                        <div className="sd-card-body sd-chart-body">
                            {planMonthlyChartData.length === 0 ? (
                                <EmptyState />
                            ) : (
                                <ResponsiveContainer width="100%" height={260}>
                                    <BarChart data={planMonthlyChartData}>
                                        <CartesianGrid strokeDasharray="3 3" />
                                        <XAxis dataKey="label" />
                                        <YAxis yAxisId="left" />
                                        <YAxis yAxisId="right" orientation="right" />
                                        <Tooltip />
                                        <Legend />
                                        <Bar
                                            yAxisId="left"
                                            dataKey="revenue"
                                            name="Doanh thu"
                                            fill={COLORS[0]}
                                            radius={[4, 4, 0, 0]}
                                        />
                                        <Bar
                                            yAxisId="right"
                                            dataKey="tickets"
                                            name="Ticket"
                                            fill={COLORS[1]}
                                            radius={[4, 4, 0, 0]}
                                        />
                                        <Bar
                                            yAxisId="right"
                                            dataKey="chats"
                                            name="Chat sessions"
                                            fill={COLORS[2]}
                                            radius={[4, 4, 0, 0]}
                                        />
                                    </BarChart>
                                </ResponsiveContainer>
                            )}
                        </div>

                        <div className="sd-card-footer">
                            <div className="sd-card-footer-title">
                                Volume hỗ trợ theo PriorityLevel (8 tuần)
                            </div>
                            {prioritySupportVolumeChartData.length === 0 ? (
                                <EmptyState />
                            ) : (
                                <ResponsiveContainer width="100%" height={220}>
                                    <BarChart data={prioritySupportVolumeChartData}>
                                        <CartesianGrid strokeDasharray="3 3" />
                                        <XAxis dataKey="priorityLabel" />
                                        <YAxis />
                                        <Tooltip />
                                        <Legend />
                                        <Bar
                                            dataKey="ticketCount"
                                            name="Ticket"
                                            fill={COLORS[0]}
                                            radius={[4, 4, 0, 0]}
                                        />
                                        <Bar
                                            dataKey="chatSessionsCount"
                                            name="Chat"
                                            fill={COLORS[2]}
                                            radius={[4, 4, 0, 0]}
                                        />
                                    </BarChart>
                                </ResponsiveContainer>
                            )}
                        </div>
                    </div>
                </div>
            </section>
        </div>
    );
}
