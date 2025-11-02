import React from "react";
import { CategoryApi, CategoryCsv } from "../../services/categories";
import { Link } from "react-router-dom";
import { BadgesApi } from "../../services/badges";
import "./admin.css";
export default function CategoryPage() {

	// ====== Danh mục ======
	const [catQuery, setCatQuery] = React.useState({ keyword: "", active: "", sort: "displayorder", direction: "asc" });
	const [categories, setCategories] = React.useState([]);
	const [catLoading, setCatLoading] = React.useState(false);

	const loadCategories = React.useCallback(() => {
		setCatLoading(true);
		const params = { ...catQuery };
		if (params.active === "") delete params.active;
		// ensure sort/direction are passed through and match backend keys
		if (!params.sort) params.sort = "displayorder";
		if (!params.direction) params.direction = "asc";
		CategoryApi.list(params)
			.then(setCategories)
			.finally(() => setCatLoading(false));
	}, [catQuery]);

	// Debounce 400ms cho filter danh mục (bỏ nút Áp dụng)
	React.useEffect(() => {
		const t = setTimeout(() => {
			loadCategories();
		}, 400);
		return () => clearTimeout(t);
	}, [catQuery, loadCategories]);

	const catToggle = async (id) => {
		try {
			await CategoryApi.toggle(id);
		} catch (err) {
			console.error(err);
		}
		loadCategories();
	};

	// ====== CSV danh mục ======
	const catExportCsv = async () => {
		const blob = await CategoryCsv.exportCsv();
		const url = URL.createObjectURL(blob);
		const a = document.createElement("a");
		a.href = url;
		a.download = "categories.csv";
		a.click();
		URL.revokeObjectURL(url);
	};

	const catImportCsv = async (e) => {
		const file = e.target.files?.[0];
		if (!file) return;
		const res = await CategoryCsv.importCsv(file);
		alert(`Import xong: total=${res.total}, created=${res.created}, updated=${res.updated}`);
		e.target.value = "";
		loadCategories();
	};

	// ====== Badges (show simple list inside this page) ======
	const [badges, setBadges] = React.useState([]);
	const [badgesLoading, setBadgesLoading] = React.useState(false);
	// badgeQuery maps to backend query params: keyword, color, icon, active
	const [badgeQuery, setBadgeQuery] = React.useState({ keyword: "", color: "", icon: "", active: "" });
	// backend sort keys: code, name, color, active, icon
	const [badgeSort, setBadgeSort] = React.useState("name");
	const [badgeDirection, setBadgeDirection] = React.useState("asc");

	const loadBadges = React.useCallback(() => {
		setBadgesLoading(true);
		const params = { ...badgeQuery, sort: badgeSort, direction: badgeDirection };
		if (params.active === "") delete params.active;
		if (!params.keyword) delete params.keyword;
		if (!params.color) delete params.color;
		if (!params.icon) delete params.icon;
		BadgesApi.list(params)
			.then(setBadges)
			.finally(() => setBadgesLoading(false));
	}, [badgeSort, badgeDirection, badgeQuery]);

	React.useEffect(() => {
		loadBadges();
	}, [loadBadges]);

	return (
		<div className="page">
			<div className="card">
				<div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
					<h2>Danh mục sản phẩm</h2>
					<div className="row" style={{ gap: 8 }}>
						<label className="btn">
							⬆ Nhập CSV
							<input
								type="file"
								accept=".csv,text/csv"
								style={{ display: "none" }}
								onChange={catImportCsv}
							/>
						</label>
						<button className="btn" onClick={catExportCsv}>
							⬇ Xuất CSV
						</button>
						<Link className="btn primary" to="/admin/categories/add">
							+ Thêm danh mục
						</Link>
					</div>
				</div>

				{/* Bộ lọc gọn gàng – cân đối */}
				<div
					className="row input-group"
					style={{ gap: 10, marginTop: 12, flexWrap: "nowrap", alignItems: "end", overflowX: 'auto' }}
				>
					<div className="group" style={{ minWidth: 320, maxWidth: 520 }}>
						<span>Tìm kiếm</span>
						<input
							value={catQuery.keyword}
							onChange={(e) => setCatQuery((s) => ({ ...s, keyword: e.target.value }))}
							placeholder="Tìm theo mã, tên hoặc mô tả…"
						/>
					</div>
					<div className="group" style={{ minWidth: 140 }}>
						<span>Trạng thái</span>
						<select
							value={catQuery.active}
							onChange={(e) => setCatQuery((s) => ({ ...s, active: e.target.value }))}
						>
							<option value="">Tất cả</option>
							<option value="true">Hiện</option>
							<option value="false">Ẩn</option>
						</select>
					</div>
					{/* Sắp xếp sẽ được thực hiện khi nhấn vào tiêu đề bảng (th) */}

					{/* Nhãn trạng thái tải */}
					{catLoading && <span className="badge gray">Đang tải…</span>}

					{/* Reset nhanh */}
					<button
						className="btn"
						onClick={() => setCatQuery((s) => ({ ...s, keyword: "", code: "", active: "" }))}
						title="Xoá bộ lọc"
					>
						Đặt lại
					</button>
				</div>

				<table className="table" style={{ marginTop: 10 }}>
					<thead>
						<tr>
							<th onClick={() => setCatQuery((s) => ({ ...s, sort: "name", direction: s.sort === "name" && s.direction === "asc" ? "desc" : "asc" }))} style={{ cursor: "pointer" }}>
								Tên {catQuery.sort === "name" ? (catQuery.direction === "asc" ? " ▲" : " ▼") : ""}
							</th>
							<th onClick={() => setCatQuery((s) => ({ ...s, sort: "code", direction: s.sort === "code" && s.direction === "asc" ? "desc" : "asc" }))} style={{ cursor: "pointer" }}>
								Mã danh mục {catQuery.sort === "code" ? (catQuery.direction === "asc" ? " ▲" : " ▼") : ""}
							</th>
							<th onClick={() => setCatQuery((s) => ({ ...s, sort: "displayorder", direction: s.sort === "displayorder" && s.direction === "asc" ? "desc" : "asc" }))} style={{ cursor: "pointer" }}>
								Thứ tự {catQuery.sort === "displayorder" ? (catQuery.direction === "asc" ? " ▲" : " ▼") : ""}
							</th>
							<th>Số SP</th>
							<th onClick={() => setCatQuery((s) => ({ ...s, sort: "active", direction: s.sort === "active" && s.direction === "asc" ? "desc" : "asc" }))} style={{ cursor: "pointer" }}>
								Trạng thái {catQuery.sort === "active" ? (catQuery.direction === "asc" ? " ▲" : " ▼") : ""}
							</th>
							<th>Thao tác</th>
						</tr>
					</thead>
					<tbody>
						{categories.map((c) => (
							<tr key={c.categoryId}>
								<td>{c.categoryName}</td>
								<td className="mono">{c.categoryCode}</td>
								<td>{c.displayOrder ?? 0}</td>
								<td>{c.productsCount ?? c.productCount ?? c.products ?? 0}</td>
								<td>
									<span className={c.isActive ? "badge green" : "badge gray"}>
										{c.isActive ? "Hiện" : "Ẩn"}
									</span>
								</td>
								<td className="row" style={{ gap: 8 }}>
									<Link
										className="btn"
										to={`/admin/categories/${c.categoryId}`}
										title="Xem chi tiết / chỉnh sửa"
									>
										✏️
									</Link>
									<label className="switch" title="Bật/Tắt hiển thị">
										<input type="checkbox" checked={!!c.isActive} onChange={() => catToggle(c.categoryId)} />
										<span className="slider" />
									</label>
								</td>
							</tr>
						))}
					</tbody>
				</table>
			</div>

				{/* Badges card */}
				<div className="card" style={{ marginTop: 14 }}>
					<div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
						<h2>Nhãn sản phẩm</h2>
						<div style={{ display: 'flex', flexDirection: 'column', gap: 8, alignItems: 'flex-end' }}>
							<div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
								<Link className="btn primary" to="/admin/badges/add">+ Thêm nhãn</Link>
							</div>
							{/* Badge filters */}
							<div className="row input-group" style={{ gap: 10, alignItems: 'end', flexWrap: 'nowrap' }}>
								<div className="group" style={{ minWidth: 320 }}>
									<span>Tìm kiếm</span>
									<input value={badgeQuery.keyword} onChange={(e) => setBadgeQuery((s) => ({ ...s, keyword: e.target.value }))} placeholder="Tìm theo mã, tên, màu, icon…" />
								</div>
								<div className="group" style={{ minWidth: 140 }}>
									<span>Trạng thái</span>
									<select value={badgeQuery.active} onChange={(e) => setBadgeQuery((s) => ({ ...s, active: e.target.value }))}>
										<option value="">Tất cả</option>
										<option value="true">Hiện</option>
										<option value="false">Ẩn</option>
									</select>
								</div>
								<button className="btn" onClick={() => setBadgeQuery({ keyword: "", active: "" })}>Đặt lại</button>
							</div>
						</div>
					</div>

					{badgesLoading && <div className="badge gray">Đang tải…</div>}
						<table className="table" style={{ marginTop: 10 }}>
							<thead>
								<tr>
									<th onClick={() => {
										const key = "code";
										setBadgeSort((prev) => {
											setBadgeDirection((d) => (prev === key && d === "asc" ? "desc" : "asc"));
											return key;
										});
									}} style={{ cursor: "pointer" }}>
										Mã {badgeSort === "code" ? (badgeDirection === "asc" ? " ▲" : " ▼") : ""}
									</th>
									<th onClick={() => {
										const key = "name";
										setBadgeSort((prev) => {
											setBadgeDirection((d) => (prev === key && d === "asc" ? "desc" : "asc"));
											return key;
										});
									}} style={{ cursor: "pointer" }}>
										Tên {badgeSort === "name" ? (badgeDirection === "asc" ? " ▲" : " ▼") : ""}
									</th>
									<th onClick={() => {
										const key = "color";
										setBadgeSort((prev) => {
											setBadgeDirection((d) => (prev === key && d === "asc" ? "desc" : "asc"));
											return key;
										});
									}} style={{ cursor: "pointer" }}>
										Màu {badgeSort === "color" ? (badgeDirection === "asc" ? " ▲" : " ▼") : ""}
									</th>
									<th onClick={() => {
										const key = "icon";
										setBadgeSort((prev) => {
											setBadgeDirection((d) => (prev === key && d === "asc" ? "desc" : "asc"));
											return key;
										});
									}} style={{ cursor: "pointer" }}>
										Icon {badgeSort === "icon" ? (badgeDirection === "asc" ? " ▲" : " ▼") : ""}
									</th>
									<th onClick={() => {
										const key = "active";
										setBadgeSort((prev) => {
											setBadgeDirection((d) => (prev === key && d === "asc" ? "desc" : "asc"));
											return key;
										});
									}} style={{ cursor: "pointer" }}>
										Trạng thái {badgeSort === "active" ? (badgeDirection === "asc" ? " ▲" : " ▼") : ""}
									</th>
									<th>Thao tác</th>
								</tr>
							</thead>
						<tbody>
							{badges.map(b => (
								<tr key={b.badgeCode}>
									<td className="mono">{b.badgeCode}</td>
									<td>{b.displayName}</td>
									<td className="mono">{b.colorHex}</td>
									<td className="mono">{b.icon ?? "-"}</td>
									<td><span className={b.isActive ? 'badge green' : 'badge gray'}>{b.isActive ? 'Hiện' : 'Ẩn'}</span></td>
									<td className="row" style={{ gap: 8 }}>
										<Link className="btn" to={`/admin/badges/${encodeURIComponent(b.badgeCode)}`} title="Xem chi tiết">✏️</Link>
										<label className="switch" title="Bật/Tắt nhãn">
											<input type="checkbox" checked={!!b.isActive} onChange={async () => { try { await BadgesApi.toggle(b.badgeCode); loadBadges(); } catch(e){ console.error(e); } }} />
											<span className="slider" />
										</label>
									</td>
								</tr>
							))}
						</tbody>
					</table>
				</div>
		</div>
	);
}

