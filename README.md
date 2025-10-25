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

- **v1.1** — Initialize base project structure (**BE**, **FE**).
- **v1.2 — 2025-10-25**
  - Merge **HieuND (RBAC)** → **Develop** (pre-merge testing branch).
  - Merge **ThanBD (User Management)** → **Develop** (pre-merge testing branch).

---

## 📄 License

MIT (or the license you choose for this repository).
