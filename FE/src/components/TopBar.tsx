import { Link, useLocation } from "react-router-dom";

export default function TopBar(){
  const { pathname } = useLocation();
  return (
    <div className="sticky top-0 z-20 bg-white/70 backdrop-blur border-b">
      <div className="max-w-6xl mx-auto px-4 py-3 flex items-center gap-3">
        <Link to="/" className="font-bold">Keytietkiem · Admin</Link>
        <nav className="ml-auto flex gap-4 text-sm">
          <Link className={pathname.startsWith("/products")? "font-semibold":"text-gray-600"} to="/products">Sản phẩm & Danh mục</Link>
          <Link className={pathname.startsWith("/categories/add")? "font-semibold":"text-gray-600"} to="/categories/add">Thêm danh mục</Link>
          <Link className={pathname.startsWith("/products/add")? "font-semibold":"text-gray-600"} to="/products/add">Thêm sản phẩm</Link>
        </nav>
      </div>
    </div>
  );
}
