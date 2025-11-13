# Keytietkiem — Account Selling & Software Support Platform

> A platform for selling digital accounts/software keys with **automatic payments**, **automatic key delivery**, **RBAC (Role-Based Access Control)**, and **audit logging**.

---

## 🧩 What’s This Project?

A web application (Frontend + Backend) that enables:
- **Guests/Customers**: browse products, purchase, pay, receive keys automatically, create support tickets.
- **Admin/Support/Content/Storage Staff**: manage catalog, key inventory, blog content, tickets, reports, and permissions.

**Goal:** reduce fraud, shorten post-payment handling time, keep key inventory clean, and provide transparent user support.

---

## 🎓 Learning Objectives

By exploring this repo, you will practice:
- **Web API** design: REST (GET/POST/PUT/DELETE), HTTP status codes, JSON.
- **Authentication & Authorization**: Sign-in, **JWT (JSON Web Token)**, screen/module-level **RBAC**.
- **Data & Inventory**: product, key, order, payment modeling; **no duplicate keys**; expiry/low-stock handling.
- **Support Flows**: ticketing (traceability, assignment), optional **real-time chat**.
- **Observability & Security**: **audit logs**, backup/restore, **TLS (Transport Layer Security)** configuration.

---

### 📁 Repository Structure

```text
SEP490_G172/
├─ BE/   # ASP.NET Core 8 Web API (Auth, Orders, Tickets, etc.)
└─ FE/   # Frontend (Storefront, Cart, Ticket, Blog)
```
---

## ⚡ Quick Start

### 1) Database & Config
- Create a **SQL Server** database and prepare credentials.
- Configure **BE** `appsettings.*` and **FE** `.env`:
  - **DB** – connection string
  - **JWT** – secret & token expiration
  - **SMTP** – for OTP/notifications
  - **PAYMENT** – sandbox keys & callback URLs (PayOS/Momo/ZaloPay)
- Seed minimal data: **Roles**, **Admin user**, **Permissions**, **Categories**, **Sample Products/Keys**.

### 2) Run

**Backend**
```bash
cd BE
dotnet restore
# if using migrations
dotnet ef database update
dotnet run
# Swagger: https://localhost:xxxx/swagger

```

**Frontend**
```bash
cd FE
npm install
npm run dev   # or: npm start
```

---
## 🛡️ Security & Quality

RBAC per module/screen; JWT for API access.

Mask keys in UI/logs; avoid logging sensitive data.

Audit logs for create/update/delete, sign-in, payments, key imports.

Regular backups, performance monitoring, sensible timeouts for Payment/Email.

---

## 🧰 Tech Stack

Backend: .NET 8, ASP.NET Core Web API, Entity Framework Core, SQL Server.

Frontend: React/Vite (or similar) — Storefront, Cart/Checkout, Ticket.

Integrations: SMTP (OTP/notifications), PayOS/Momo/ZaloPay (sandbox → production).

Tooling: Swagger/OpenAPI, optional CI with GitHub Actions.

---

## 🗺️ Roadmap

MVP: VN payment gateways, auto-delivery, ticket + chat, sales dashboard.

v2: i18n, FAQ chatbot, advanced analytics, UX/performance improvements, international gateways.

---

## 📊 Project Tracking

- **v0.1 - 2025-10-18** — Initialize base project structure (**BE**, **FE**).
- **v0.2 - 2025-10-25**
  - Merge **HieuND (RBAC)** → **Develop** (pre-merge testing branch).
  - Merge **ThanBD (User Management)** → **Develop** (pre-merge testing branch).
- **v0.3 - 2025-10-28**
  - Merge **ManhLD (Product,Category,Badge)** → **Develop** (pre-merge testing branch).
- **v0.4 - 2025-10-30**
  - Merge **TrungDQ (Authentication,Login,Registration,OTP Servive)** → **Develop** (pre-merge testing branch).
- **v0.5 - 2025-10-31**
  - Merge **HieuND (Post List, Post Create)** → **Develop** (pre-merge testing branch).
  - Merge **TrungDQ (Password Reset, Role Seeding)** → **Develop** (pre-merge testing branch).
- **v0.6 - 2025-11-2**
  - Merge **ThanBD (Admin Ticket List, Admin Ticket Detail)** → **Develop** (pre-merge testing branch).
  - Merge **TrungDQ (Product Key List, CSV Import)** → **Develop** (pre-merge testing branch).
- **v0.7 - 2025-11-3**
  - Merge **TrungDQ (Fix Product Key Bug)** → **Develop** (pre-merge testing branch).
  - Merge **TungNV (Website Configuration)** → **Develop** (pre-merge testing branch).
- **v0.8 - 2025-11-4**
  - Merge **TrungDQ (Fix Product Key Bug, Using Enum)** → **Develop** (pre-merge testing branch).
  - Merge **ThanBD (Fix User Management Bug)** → **Develop** (pre-merge testing branch).
  - Merge **ManhLD (Fix Product Management Bug)** → **Develop** (pre-merge testing branch).
  - Merge **HieuND (Fix Create Post Logic)** → **Develop** (pre-merge testing branch).
  - Merge **TungNV (Update Website Configuration UI)** → **Develop** (pre-merge testing branch).
- **v0.9 - 2025-11-6**
  - Merge **TrungDQ (Add Token Recovation, Admin Seeding, Product Account Management)** → **Develop** (pre-merge testing branch).
  - Merge **HieuND (Fix Create Edit Post UI)** → **Develop** (pre-merge testing branch).
- **v0.10 - 2025-11-7**
  - Merge **HieuND (Add Tag, Post Type)** → **Develop** (pre-merge testing branch).
- **v0.11 - 2025-11-8**
  - Merge **TrungDQ (Add Key Monitor, CSV Import for product keys, Implement role-based sidebar for storage staff)** → **Develop** (pre-merge testing branch).
  - Merge **ThanBD (Update Admin Ticket Detail)** → **Develop** (pre-merge testing branch).
  - Merge **HieuND (Update Post Type)** → **Develop** (pre-merge testing branch).
  - **v0.12 - 2025-11-9**
  - Merge **ThanBD (Update Admin Ticket)** → **Develop** (pre-merge testing branch).
  - Merge **HieuND (Fix Add Post Bug)** → **Develop** (pre-merge testing branch).
- **v0.13 - 2025-11-10**
  - Merge **ThanBD (Update Ticket Detail)** → **Develop** (pre-merge testing branch).
  - Merge **ManhLD (Add Product Variant)** → **Develop** (pre-merge testing branch).
  - Merge **HieuND (Update Ubload Image)** → **Develop** (pre-merge testing branch).
- **v0.14 - 2025-11-11**
  - Merge **TrungDQ (Add COGS Price, Fix CSV Import)** → **Develop** (pre-merge testing branch).
  - Merge **ManhLD (Add Product FAQ, Product Section)** → **Develop** (pre-merge testing branch).
- **v0.15 - 2025-11-13**
  - Merge **ThanBD (Update Ticket Detail, Add SignarIR, Fix Ticket Reply)** → **Develop** (pre-merge testing branch).
---

## 📄 License

MIT (or the license you choose for this repository).
