import React from "react";
import AdminLayout from "../../components/admin/Layout";
import { CategoryApi, CategoryCsv } from "../../services/categories";
import { Link } from "react-router-dom";
import { useConfirm } from "../../components/common/ConfirmProvider.jsx";
import { BadgesApi } from "../../services/badges";

export default function CategoryPage() {
	const confirm = useConfirm();

	// ====== Danh mục ======
	const [catQuery, setCatQuery] = React.useState({ keyword: "", code: "", active: "" });
	const [categories, setCategories] = React.useState([]);
	const [catLoading, setCatLoading] = React.useState(false);

	const loadCategories = React.useCallback(() => {
		setCatLoading(true);
		const params = { ...catQuery };
		if (params.active === "") delete params.active;
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
		// quick toggle without confirmation (match product behavior)
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

	const loadBadges = React.useCallback(() => {
		setBadgesLoading(true);
		BadgesApi.list()
			.then(setBadges)
			.finally(() => setBadgesLoading(false));
	}, []);

	React.useEffect(() => {
		loadBadges();
	}, [loadBadges]);

	return (
		<AdminLayout>
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
					style={{ gap: 10, marginTop: 12, flexWrap: "wrap", alignItems: "end" }}
				>
					<div className="group" style={{ minWidth: 220 }}>
						<span>Tên/Slug</span>
						<input
							value={catQuery.keyword}
							onChange={(e) => setCatQuery((s) => ({ ...s, keyword: e.target.value }))}
							placeholder="VD: Office / office…"
						/>
					</div>
					<div className="group" style={{ minWidth: 180 }}>
						<span>Mã/Slug chính xác</span>
						<input
							value={catQuery.code}
							onChange={(e) => setCatQuery((s) => ({ ...s, code: e.target.value }))}
							placeholder="VD: office"
						/>
					</div>
					<div className="group" style={{ minWidth: 160 }}>
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

					{/* Nhãn trạng thái tải */}
					{catLoading && <span className="badge gray">Đang tải…</span>}

					{/* Reset nhanh */}
					<button
						className="btn"
						onClick={() => setCatQuery({ keyword: "", code: "", active: "" })}
						title="Xoá bộ lọc"
					>
						Reset
					</button>
				</div>

				<table className="table" style={{ marginTop: 10 }}>
					<thead>
						<tr>
							<th>Tên</th>
							<th>Slug</th>
							<th>Thứ tự</th>
							<th>Số SP</th>
							<th>Trạng thái</th>
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
						<h2>Nhãn</h2>
						<div className="row">
							<Link className="btn primary" to="/admin/badges/add">+ Thêm nhãn</Link>
						</div>
					</div>

					{badgesLoading && <div className="badge gray">Đang tải…</div>}
					<table className="table" style={{ marginTop: 10 }}>
						<thead>
							<tr>
								<th>Mã</th>
								<th>Tên</th>
								<th>Màu</th>
								<th>Trạng thái</th>
								<th>Thao tác</th>
							</tr>
						</thead>
						<tbody>
							{badges.map(b => (
								<tr key={b.badgeCode}>
									<td className="mono">{b.badgeCode}</td>
									<td>{b.displayName}</td>
									<td className="mono">{b.colorHex}</td>
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
		</AdminLayout>
	);
}

