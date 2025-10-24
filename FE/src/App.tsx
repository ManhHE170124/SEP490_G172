import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import TopBar from "./components/TopBar";
import ProductsPage from "./pages/ProductsPage";
import ProductAddPage from "./pages/ProductAddPage";
import CategoryAddPage from "./pages/CategoryAddPage";

export default function App(){
  return (
    <BrowserRouter>
      <TopBar/>
      <Routes>
        <Route path="/" element={<Navigate to="/products" replace/>}/>
        <Route path="/products" element={<ProductsPage/>}/>
        <Route path="/products/add" element={<ProductAddPage/>}/>
        <Route path="/categories/add" element={<CategoryAddPage/>}/>
      </Routes>
    </BrowserRouter>
  );
}
