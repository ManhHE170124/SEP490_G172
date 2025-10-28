# Keytietkiem FE Addon — FIXED (Categories & Products)

Cleaned up code to avoid duplicates/typos and align with BE:
- Categories: `/api/categories` CRUD, toggle, bulk-upsert
- Products: `/api/products/*` list with filters, CRUD, status patch, bulk price, CSV import/export

## Run as standalone
```bash
npm install
cp .env.example .env
# Edit VITE_API_BASE_URL if your BE port differs
npm run dev
```

## Integrate into existing FE
- Copy `src/` into your FE
- Ensure deps: react, react-dom, react-router-dom, axios, @vitejs/plugin-react
- Add routes for admin pages or use provided App.jsx
