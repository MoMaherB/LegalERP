# Legal Management System (ERP/CRM) — Requirements Reference Document

**Project:** Self-Hosted Legal Management System for Investment Lawyer
**Document Type:** Business Requirements Document (BRD) + Technical Requirements Document (TRD)
**Architecture Style:** Single-Server (VPS) Deployment, ASP.NET Core Modular Monolith
**Version:** 3.0

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [System Scope & Architecture Overview](#2-system-scope--architecture-overview)
3. [Business Requirements](#3-business-requirements)
4. [Technical Requirements](#4-technical-requirements)
5. [Module Specifications](#5-module-specifications)
6. [Roles & Access Control Matrix](#6-roles--access-control-matrix)
7. [Non-Functional Requirements](#7-non-functional-requirements)
8. [Data Model Reference (Conceptual)](#8-data-model-reference-conceptual)
9. [Third-Party Integrations](#9-third-party-integrations)
10. [Glossary (Arabic/English Terms)](#10-glossary-arabicenglish-terms)
11. [Open Decisions / Assumptions](#11-open-decisions--assumptions)
12. [Development Workflow — Feature-by-Feature Build & Test Process](#12-development-workflow--feature-by-feature-build--test-process)
13. [UI Design Language — Glassmorphism (Light/Dark)](#13-ui-design-language--glassmorphism-lightdark)
14. [Progress Log](#14-progress-log)

---

## 1. Executive Summary

This document defines the business and technical requirements for a Legal Management System (LMS) built for an Investment Lawyer's practice. The system replaces manual/paper-based tracking of companies, legal cases, court hearings, and client fee management with a centralized, cloud-based platform accessible from a **single, responsive Blazor Web Application** that works well on desktop, tablet, and mobile browsers.

> **Mobile app note:** A dedicated native/hybrid mobile app (e.g., .NET MAUI Blazor Hybrid) is **deferred, not required for this phase** — it added a large local toolchain footprint (~18GB of Visual Studio mobile workloads) that isn't justified yet. Instead, the Blazor Web App itself is required to be fully **mobile-friendly/responsive**, so it works well on a phone browser. The architecture (shared component library, API-first design) is kept compatible with adding a MAUI or other native app later without rework — see Section 11.

The system is designed as a **single-server, cloud-hosted architecture** — there is no local/on-premise database on the office computer and no offline sync engine. The client communicates directly with a central Web API. To minimize hosting cost, the **entire production stack — API, database, web frontend, and uploaded document/photo files — runs on a single Linux VPS**, rather than being split across separate managed cloud services (no Google Drive, no separate managed database provider).

The system must strictly enforce **role-based access control**, most critically around financial data — Standard/Assistant users must never receive financial figures in any form, not even in a restricted or masked format.

The system will be built and delivered **feature-by-feature**, with each feature tested against dummy/seed data before moving to the next (see Section 12).

---

## 2. System Scope & Architecture Overview

### 2.1 In Scope
- Companies management (3 categories, dynamic amendment contracts, partner records)
- Case management (4 case types, parties, hearings/postponements, legal memos)
- Financial/fee tracking per case (append-only ledger, role-restricted)
- Document management via local server file storage (upload, storage, streamed retrieval)
- Authentication & Role-Based Access Control (Admin / Standard)
- Bilingual UI (Arabic RTL / English LTR)
- Light/Dark theming with a modern glassmorphism visual style
- Fully responsive, mobile-friendly Blazor Web App layout (usable on phones/tablets, no separate mobile app)
- Automated hearing reminders (1 day prior, push notification)
- Fuzzy search (company names, case numbers, party names — Arabic & English)

### 2.2 Out of Scope (for this phase)
- Offline/local database and sync engine (explicitly removed from this architecture)
- **Dedicated native/hybrid mobile app (.NET MAUI Blazor Hybrid or similar)** — deferred to a later phase to avoid the large local toolchain install (~18GB of Visual Studio mobile workloads) at this stage; the responsive Web App serves as the mobile experience for now
- Multi-firm SaaS tenancy (single-firm deployment; schema may allow future extension but is not required now)
- E-signature / document generation workflows
- Billing/invoicing integrations (e.g., accounting software, payment gateways)
- Client-facing portal (system is for internal firm staff only, not external clients)
- Third-party cloud storage providers (Google Drive, S3, R2, etc.) — superseded by single-VPS local file storage

### 2.3 High-Level Architecture

All production components are deployed on **one Linux VPS** to keep hosting cost as low as possible. There is no split across multiple managed cloud services — the API, the database, the web frontend, and the uploaded file storage all live on the same machine, isolated from each other via containers or systemd services.

```
┌──────────────────────────────────────────────────────────────────┐
│                     Single Linux VPS (Production)                  │
│                                                                      │
│   ┌────────────────────┐        ┌──────────────────────────────┐  │
│   │ Nginx / Reverse      │        │  ASP.NET Core Web API          │  │
│   │ Proxy + HTTPS (SSL)  │──────► │  (Modular Monolith)             │  │
│   └────────┬─────────────┘        │                                 │  │
│            │                       │  - Companies Module             │  │
│            │  serves               │  - Cases Module                 │  │
│            ▼                       │  - Financials Module            │  │
│   ┌────────────────────┐          │  - Documents Module (local FS)  │  │
│   │ Blazor Web App       │          │  - Auth / RBAC Module           │  │
│   │ (responsive/mobile-   │          │  - Notifications Module         │  │
│   │  friendly layout)     │          └────────┬──────────┬─────────┘  │
│   └────────────────────┘                     │          │             │
│                                    ┌─────────▼──┐  ┌────▼──────────┐ │
│                                    │ PostgreSQL  │  │ Local File     │ │
│                                    │ (same VPS,  │  │ Storage        │ │
│                                    │  own volume)│  │ (contracts,    │ │
│                                    └─────────────┘  │  ID scans,     │ │
│                                              │       │  photos)       │ │
│                                    ┌─────────▼──────┴──────────────┐ │
│                                    │ Hangfire (Postgres storage)     │ │
│                                    │  -> FCM push                    │ │
│                                    └──────────────────────────────┘ │
│                                                                      │
└──────────────────────────────────────────────────────────────────┘
                     ▲                                    ▲
                     │ HTTPS + SignalR                     │ HTTPS + SignalR
        ┌────────────┴───────────┐            ┌────────────┴───────────┐
        │ Desktop / PC Browser    │            │ Mobile Phone Browser   │
        │ (Blazor Web App)        │            │ (same Blazor Web App,  │
        │                         │            │  responsive layout)    │
        └─────────────────────────┘            └─────────────────────────┘
```

**Key architectural principles:**
- **Single source of truth, single machine:** PostgreSQL runs on the same VPS as the API — no local office replicas, no external managed database.
- **Local file storage instead of cloud storage:** Uploaded documents and photos are written to a dedicated storage volume/directory on the VPS (e.g., `/var/legalerp/storage/`), organized by owner (company/case), not to any third-party service. The database stores only file metadata + the relative storage path.
- **Derived, not stored, UI state:** Colors, statuses, and computed financial fields are never persisted as literal values — they are computed server-side from underlying facts (booleans, ledgers) to prevent data drift.
- **One responsive client, not two codebases:** A single Blazor Web App serves both desktop and mobile browsers via responsive layout — there is no separate mobile app codebase to maintain for this phase, which also avoids the ~18GB Visual Studio mobile (MAUI) workload install.
- **Single point of backup responsibility:** Because everything lives on one VPS, automated off-site backups (database dump + storage directory) are a first-class operational requirement, not optional — see TR-3 and Section 11.

---

## 3. Business Requirements

### BR-1: Companies Management
| ID | Requirement |
|----|-------------|
| BR-1.1 | The system shall classify every company record under exactly one of three categories: Sole Proprietorship (فردي), Capital/Funds Company (أموال), or Partnership/Persons Company (أشخاص). |
| BR-1.2 | The system shall capture Company Name, Trade Name (السمة التجارية), and the Articles of Incorporation (عقد التأسيس) as a document reference. |
| BR-1.3 | The system shall support an unlimited, ordered list of Amendment Contracts (عقد التعديل الأول، الثاني، الثالث...) per company, added dynamically without schema changes. |
| BR-1.4 | The system shall allow uploading high-quality scans/images for: incorporation contracts, amendment contracts, partner national ID copies, and general corporate documents. |
| BR-1.5 | Users shall be able to filter companies by category. |
| BR-1.6 | Users shall be able to perform fuzzy (approximate) search on company names in both Arabic and English. |

### BR-2: Cases Management
| ID | Requirement |
|----|-------------|
| BR-2.1 | The system shall support four case types: Criminal (جنائي), Personal Status/Family (شرعي), Civil (مدني), Administrative Court (قضاء إداري). |
| BR-2.2 | Each case shall record Case Type, associated Legal Memos/Briefs (مذكرات), and its parties. |
| BR-2.3 | Each party in a case shall be classified as Defendant (المتهم) or Victim (المجني عليه). |
| BR-2.4 | Each party shall be flagged as either "our client" or "opponent," independent of their Defendant/Victim role. |
| BR-2.5 | In the UI, parties flagged as our client shall always render in **Green**; opponents shall always render in **Red** — regardless of whether they are the Defendant or the Victim. |
| BR-2.6 | Each case shall have a status of Active or Closed, visually distinguished by color (e.g., Amber = Active/Approaching, Green = Closed/Completed). |
| BR-2.7 | Users shall be able to search cases by case number, filing date, or party name(s), using fuzzy search. |
| BR-2.8 | The system shall notify the responsible user **1 day before** a scheduled hearing date. |
| BR-2.9 | After a hearing occurs, the user shall log an outcome: either a final Judgment (حكم) or a Postponement (تأجيل). |
| BR-2.10 | If postponed, the system shall capture the next hearing date and automatically schedule a new 1-day-prior reminder. |
| BR-2.11 | The full history of postponements for a case shall be retrievable as an audit trail (no hearing record is overwritten or lost). |
| BR-2.12 | Every document uploaded to a case or case party shall support a custom user-defined title/label for identification. |
| BR-2.13 | Closing a case shall capture the final Case Outcome (Won / Lost / Settled) and render distinct color badges (Green = Won, Red = Lost, Blue = Settled/Dismissed). |
| BR-2.14 | Every person added to a case (whether our client or opponent) **must** be a registered Client record in the system. The UI shall provide a quick-search selector to pick an existing Client, or a "Client not found — Add new" option that redirects to create a new Client record and then returns to pick them. No manual inline name entry is allowed for case parties. |

### BR-3: Financials & Fees Management
| ID | Requirement |
|----|-------------|
| BR-3.1 | Each case shall track an Agreed Fee (المتفق عليه), total Collected Amount (المحصل), and Remaining Balance (المتبقي). |
| BR-3.2 | Recording a new collected payment shall automatically recalculate the Remaining Balance without manual adjustment. |
| BR-3.3 | The system shall clearly indicate whether a case's balance is fully resolved or still pending collection. |
| BR-3.4 | Fee status shall be visually color-coded: e.g., Green = Paid, Amber = Partially Paid, Red = Unpaid. |
| BR-3.5 | The Agreed Fee field, and all financial data generally, shall be visible **only** to Admin users. |

### BR-4: Access Control
| ID | Requirement |
|----|-------------|
| BR-4.1 | The system shall support two roles at minimum: Admin and Standard User (Viewer/Assistant). |
| BR-4.2 | Admins shall have full CRUD access to companies, cases, documents, and all financial details. |
| BR-4.3 | Standard Users shall be able to view and browse companies, cases, and documents, but shall be **completely restricted** from viewing any financial data — including agreed fees, collected amounts, and balances — in any form, partial or otherwise. |

### BR-5: Usability & Accessibility
| ID | Requirement |
|----|-------------|
| BR-5.1 | The system shall support Arabic and English languages, switchable by the user. |
| BR-5.2 | The UI layout shall switch direction automatically: RTL for Arabic, LTR for English. |
| BR-5.3 | The system shall support Light and Dark visual themes, switchable by the user. |
| BR-5.4 | The user's theme and language preference shall persist across sessions and devices (stored against the user's profile), while also rendering instantly on app startup from local device storage. |
| BR-5.5 | The UI shall use a modern **glassmorphism** design language (translucent, blurred, layered surfaces) across all screens, correctly adapted for both Light and Dark themes. See Section 13 for full design specification. |
| BR-5.6 | The Web App shall be fully **responsive/mobile-friendly**: all screens (dashboards, forms, tables, document viewers) shall remain usable and legible on phone-sized screens, without requiring a separate native mobile app. |

### BR-6: Clients Management (الموكلين)
| ID | Requirement |
|----|-------------|
| BR-6.1 | The system shall maintain a central **Clients (الموكلين)** registry — every person the firm deals with (whether represented by the firm or an opposing party) must have a Client record before they can be linked to a case or company. |
| BR-6.2 | Each Client record shall capture: Full Name (Arabic, required), Full Name (English, optional), **National ID Number (رقم الهوية, mandatory / required)**, Phone Number, Email, Address, and Notes. |
| BR-6.3 | Each Client record shall support uploading two key documents: a **National ID scan** (صورة الهوية) and an **Attorney/Power of Attorney document** (صورة الوكالة — the lawyer's authorization paper). |
| BR-6.4 | The Client detail page shall display all **related Cases** (every case where this Client is a party, with case number, title, role, client/opponent status, and case status badge). |
| BR-6.5 | The Client detail page shall display all **related Companies** (every company where this Client is a partner, with company name and ownership percentage). |
| BR-6.6 | Client quick-search shall require **all query terms to match** (strict multi-word search) so searching for "محمد عيسي" does not match unrelated clients who only share "محمد". |
| BR-6.7 | When adding a party to a Case, the UI shall provide a **quick-search dropdown** displaying `Full Name (National ID)` for clear disambiguation. The dropdown shall always include a **"➕ Client not found — Add new client"** option that redirects to `/clients/new`. |
| BR-6.8 | When adding a Partner to a Company, the UI shall use the same **Client quick-search dropdown** (BR-6.7) to pick an existing Client record (or redirect to create a new Client if not found). The partner's name, National ID, and ID document shall be pulled directly from the central Client record to eliminate duplicate data entry. |

---

## 4. Technical Requirements

### TR-1: Backend Platform
| ID | Requirement |
|----|-------------|
| TR-1.1 | Backend shall be built with ASP.NET Core, structured as a Modular Monolith (distinct modules: Companies, Cases, Financials, Documents, Auth, Notifications — each with clear internal boundaries, sharing one deployable). |
| TR-1.2 | Data access shall use Entity Framework Core against PostgreSQL. |
| TR-1.3 | All entity mutations (insert/update/soft-delete) shall be recorded with `is_deleted` soft-delete flags — no hard deletes on business data. |
| TR-1.4 | Optimistic concurrency shall be enforced on mutable entities (e.g., `row_version` column) to prevent silent overwrite conflicts. |

### TR-2: Database
| ID | Requirement |
|----|-------------|
| TR-2.1 | PostgreSQL shall be the sole system of record; no local or on-device database is used. |
| TR-2.2 | The `pg_trgm` extension shall be enabled for fuzzy/trigram similarity search on company names, case numbers, and party names. |
| TR-2.3 | Financial figures (collected amount, remaining balance, payment status) shall be computed dynamically via a database view or equivalent EF Core query — never stored as mutable columns — to eliminate drift between the ledger and the displayed totals. |
| TR-2.4 | Hearings/postponements shall be modeled as an append-only chain of records (each postponement creates a new linked row), never as an in-place update of a single hearing record. |

### TR-3: Document Storage (Local VPS Storage)
| ID | Requirement |
|----|-------------|
| TR-3.1 | All uploaded documents and photos shall be stored on the **local filesystem of the production VPS** (a dedicated storage directory/volume), not in any third-party cloud storage service (no Google Drive, no S3/R2). |
| TR-3.2 | Files shall be organized on disk in a predictable, owner-based folder structure (e.g., `/var/legalerp/storage/{owner_type}/{owner_id}/{filename}`), mirroring the polymorphic `owner_type` + `owner_id` pattern used in the database. |
| TR-3.3 | The database shall store only document metadata and the **relative storage path** on disk — never the file bytes themselves. |
| TR-3.4 | File retrieval shall be streamed through the API to authorized users only; uploaded files shall **not** be placed in a directory directly served by the reverse proxy/static file server, so access always passes through authentication and role checks. |
| TR-3.5 | File uploads shall be validated for type and size before being written to disk, to prevent disk exhaustion or unexpected file types. |
| TR-3.6 | Because all files live on a single server with no external redundancy, **automated scheduled backups** of the storage directory (in addition to the database) are a mandatory operational requirement — see Section 11. A daily backup job (e.g., compressed archive shipped to an off-site/secondary location) shall be configured before go-live. |
| TR-3.7 | Disk space on the VPS shall be monitored, with alerting configured before the volume approaches capacity, since there is no automatic scaling of local storage the way there is with a managed cloud storage service. |

### TR-4: Frontend Platform
| ID | Requirement |
|----|-------------|
| TR-4.1 | The application shall be built as a single Blazor Web App (.NET 8/9), using interactive render modes as appropriate. |
| TR-4.2 | The Web App shall be **responsive**, using fluid/adaptive layouts (e.g., CSS Grid/Flexbox with breakpoints) so the same application is fully usable on desktop, tablet, and phone screen sizes — no separate mobile codebase for this phase. |
| TR-4.3 | UI components, localization logic, theming logic, and glassmorphism styling shall live in a single Razor Class Library (RCL) even though there is currently only one host (the Web App), so the structure remains ready to plug into a native/hybrid mobile app (e.g., .NET MAUI Blazor Hybrid) later without a rewrite — see Section 11. |
| TR-4.4 | The client shall communicate with the backend via HTTPS REST calls and SignalR (for real-time updates, e.g., notification delivery, live status changes). |

### TR-5: Localization & Theming
| ID | Requirement |
|----|-------------|
| TR-5.1 | Backend error messages and notification text shall be localized using `IStringLocalizer` with `.resx` resource files (Arabic and English resource sets). |
| TR-5.2 | The frontend shall dynamically set the HTML/root container `dir` attribute (`rtl` for Arabic, `ltr` for English) based on the active language. |
| TR-5.3 | Theme and language preferences shall be cached in the browser's LocalStorage for instant startup rendering, and persisted to the user's profile in the database for cross-device/cross-browser consistency. |
| TR-5.4 | The Light and Dark themes shall both implement the glassmorphism design language defined in Section 13 (translucency, background blur, subtle borders/shadows), sharing the same component structure with only the CSS variable palette differing between modes. |

### TR-6: Background Jobs & Notifications
| ID | Requirement |
|----|-------------|
| TR-6.1 | Hangfire (backed by PostgreSQL storage, avoiding a separate Redis dependency) shall handle scheduled and recurring background jobs. |
| TR-6.2 | A reminder job shall be scheduled for exactly 1 day before each hearing date, at a fixed time (e.g., 9:00 AM). |
| TR-6.3 | When a hearing is postponed, the prior reminder job shall be cancelled and a new job scheduled against the new hearing date. |
| TR-6.4 | Reminder notifications shall be delivered via Firebase Cloud Messaging (FCM) web push, reaching the browser (including on mobile devices where supported). |

### TR-7: Search
| ID | Requirement |
|----|-------------|
| TR-7.1 | Fuzzy search shall be implemented using PostgreSQL's native `pg_trgm` trigram similarity functions, exposed through EF Core (`EF.Functions` trigram methods). |
| TR-7.2 | Search indexes (GIN, trigram ops) shall be created on all fuzzy-searchable text columns to keep queries performant at scale. |
| TR-7.3 | Search shall function correctly against both Arabic and English text fields (e.g., company name and its English equivalent, where present). |

### TR-8: Security & Access Control
| ID | Requirement |
|----|-------------|
| TR-8.1 | Authentication shall be handled via ASP.NET Core Identity (or equivalent), issuing role claims (Admin / Standard). |
| TR-8.2 | Field-level financial restriction shall be enforced **server-side** via distinct response DTOs per role — never via client-side hiding of fields that are still present in the payload. |
| TR-8.3 | Endpoints exposing full financial detail shall additionally be protected by an authorization policy (e.g., `ViewFullFinancials`, requiring the Admin role), so access cannot be bypassed by a missed manual check in a future endpoint. |
| TR-8.4 | All API calls shall require authentication; anonymous access is not permitted anywhere in the system. |

### TR-9: Hosting & Deployment (Single VPS)
| ID | Requirement |
|----|-------------|
| TR-9.1 | Production shall run on a single Linux VPS (e.g., Ubuntu Server) hosting the ASP.NET Core API, the Blazor Web App, PostgreSQL, and the local document/photo storage volume together. |
| TR-9.2 | Each component (API, database, reverse proxy) shall run in isolated processes or containers (e.g., Docker Compose) on the VPS, so components can be restarted, updated, or resource-limited independently despite sharing one machine. |
| TR-9.3 | An Nginx (or equivalent) reverse proxy shall terminate HTTPS/SSL (e.g., via Let's Encrypt) and route traffic to the API and Web App. |
| TR-9.4 | A single VPS provider (e.g., Hetzner, Contabo, DigitalOcean) shall be selected primarily on a cost basis, sized for a single-firm workload (moderate CPU/RAM, SSD storage sized with headroom for document growth). |
| TR-9.5 | Automated daily backups shall cover both the PostgreSQL database (e.g., `pg_dump`) and the document storage directory, shipped to a secondary location off the VPS (e.g., a low-cost object storage bucket used purely for backup archives, or a second small VPS/disk) to protect against total server loss. |
| TR-9.6 | Because there is no managed-service redundancy, a basic disaster-recovery runbook shall be documented (how to restore the database dump and storage archive onto a fresh VPS) before go-live. |

---

## 5. Module Specifications

### 5.1 Companies Module
- **Entities:** Company, CompanyAmendment (ordered, dynamic), CompanyPartner (linked to Client), Document (linked)
- **Key behavior:** Amendments are modeled as a child table with a sequence number — not as fixed columns — so any number of amendments can be added without schema changes.
- **Search:** Fuzzy search across `company_name` (Arabic) and `company_name_en` (English equivalent, optional).

### 5.2 Cases Module
- **Entities:** Case, CaseParty (linked to Client), CaseMemo, CaseHearing, Document (linked)
- **Key behavior:** Party color (Green/Client, Red/Opponent) is derived from the `is_our_client` boolean at render time — never stored as a literal color value. Case status color (Active/Amber, Closed/Green) is derived the same way from the `status` enum. Every party in a case must reference a registered Client record — no inline manual entry.
- **Hearings:** Each hearing is an independent record; postponements create a new record linked to the prior one via `previous_hearing_id`, preserving a complete, non-destructive history.

### 5.3 Financials Module
- **Entities:** CaseFee (agreed fee, one per case), FeeTransaction (append-only ledger of payments)
- **Key behavior:** Collected Amount, Remaining Balance, and Payment Status are never stored directly — they are computed from the sum of ledger transactions against the agreed fee, exposed via a database view or equivalent query, guaranteeing the displayed numbers can never drift from the underlying transaction history.
- **Access:** Entirely gated behind the Admin role at the API layer; Standard Users receive no financial fields in any response related to this module.

### 5.4 Documents Module
- **Entities:** Document (polymorphic — attaches to Company, CompanyAmendment, CompanyPartner, Case, or CaseMemo via `owner_type` + `owner_id`)
- **Key behavior:** Physical storage lives on the production VPS's local filesystem in a dedicated storage volume; the database only ever holds the relative file path and descriptive metadata (file name, type, size, uploader, timestamps). All access is streamed through an authenticated API endpoint — the storage directory itself is never publicly served.

### 5.5 Auth & RBAC Module
- **Entities:** User (role, language preference, theme preference)
- **Key behavior:** Role is the sole gate for financial visibility; language/theme preferences sync bidirectionally between local device storage (for instant load) and the user profile (for cross-device consistency).

### 5.6 Notifications Module
- **Key behavior:** Hangfire schedules a reminder exactly 1 day before each hearing; postponement cancels and reschedules automatically; delivery goes out over FCM to whichever devices the user is logged into.

### 5.7 Clients Module (الموكلين)
- **Entities:** Client (central person record), linked from CaseParty (via `client_id` FK) and CompanyPartner (via `client_id` FK)
- **Key behavior:** Every person the firm interacts with — whether represented as "our client" (موكلنا) or as an opponent (خصمنا) — must be a registered Client. The Client record is the single source of truth for personal data (name, national ID, phone, email, address), National ID document (صورة الهوية), and Attorney/Power of Attorney document (صورة الوكالة). The Client detail page aggregates all related Cases and Companies for a 360° view of the person.
- **Search:** Fuzzy search across `full_name` (Arabic) and `full_name_en` (English equivalent, optional).
- **UI Pattern:** Quick-search dropdown selector when adding a party to a Case or a partner to a Company. If the person doesn't exist, a "Client not found — Add new" option redirects to the Client creation form.

---

## 6. Roles & Access Control Matrix

| Capability | Admin | Standard User |
|---|:---:|:---:|
| View companies & documents | ✅ | ✅ |
| Add / edit / delete companies | ✅ | ❌ |
| View case details, parties, memos | ✅ | ✅ |
| Add / edit / delete cases | ✅ | ❌ (view-only, or per firm policy) |
| Log hearing outcomes / postponements | ✅ | ⚠️ Optional — decide per firm workflow |
| View Agreed Fee | ✅ | ❌ |
| View Collected Amount / Remaining Balance | ✅ | ❌ |
| View Payment Status label (Paid/Partial/Unpaid) only | ✅ | ❌ (per current requirement: **no** financial data at all, not even status) |
| Record fee payments | ✅ | ❌ |
| Manage users / roles | ✅ | ❌ |
| Upload / view documents | ✅ | ✅ |

> **Note:** The current requirement is stricter than a typical "hide numbers, show status" pattern — Standard Users are to be restricted from **any** financial data, including the Paid/Partial/Unpaid status label. This should be confirmed as final policy (see Section 11, Open Decisions) since some firms prefer assistants to at least see payment status without amounts.

---

## 7. Non-Functional Requirements

| Category | Requirement |
|---|---|
| **Performance** | Fuzzy search queries shall return results in well under 1 second for a dataset scaled to a single firm's realistic volume (thousands of companies/cases). |
| **Availability** | The VPS shall target the hosting provider's standard baseline uptime; no requirement for multi-region or high-availability failover, given single-firm usage. Scheduled maintenance windows are acceptable. |
| **Cost Efficiency** | Architecture consolidates the API, database, web frontend, and file storage onto a single VPS to minimize recurring costs to one server bill, avoiding managed database fees, cloud storage egress charges, and multi-service pricing entirely. |
| **Scalability** | The Modular Monolith shall be structured so that any module (e.g., Financials) could be extracted into an independent service later without a full rewrite, should the firm's usage grow substantially or expand to multiple firms. |
| **Auditability** | All financial transactions and hearing postponements shall be fully traceable historically; no destructive updates on these records. |
| **Data Integrity** | Computed fields (balances, payment status, colors) shall never be independently stored in a way that could desynchronize from their source data. |
| **Security** | All financial data access restrictions shall be enforced at the API layer, not merely hidden in the UI. |
| **Localization Coverage** | All user-facing text (labels, error messages, notifications) shall be available in both Arabic and English with no hard-coded strings in either client or backend. |

---

## 8. Data Model Reference (Conceptual)

This is a conceptual entity map for reference — see prior technical discussion for full DDL.

```
User ──< (preferences: role, theme, language)

Client ──> NationalIdDocument (Document)
       ──> AttorneyDocument (Document)
       ──< CaseParty (via client_id)
       ──< CompanyPartner (via client_id)

Company ──< CompanyAmendment (ordered, sequence_number)
        ──< CompanyPartner ──> Client (via client_id)
        ──< Document (via owner_type='Company')

Case ──< CaseParty (role: Defendant/Victim; is_our_client: bool) ──> Client (via client_id)
     ──< CaseMemo ──> Document
     ──< CaseHearing (chained via previous_hearing_id)
     ──1 CaseFee ──< FeeTransaction (append-only ledger)

Document (polymorphic: owner_type + owner_id) ──> Local VPS Storage Path
```

**Key modeling decisions carried through the whole system:**
1. Amendments, partners, hearings, and fee transactions are all **child tables**, not fixed columns — supporting unbounded, dynamic growth.
2. Colors and computed financial totals are **never stored** — always derived at query/render time from booleans, enums, and ledger sums.
3. Documents are **polymorphic** — one `documents` table serves every module, keyed by `owner_type` + `owner_id`, avoiding a proliferation of per-module document tables.
4. Hearings and fee transactions are **append-only** — history is preserved by adding new rows, never overwriting old ones.

---

## 9. Third-Party Integrations

| Integration | Purpose | Notes |
|---|---|---|
| **Firebase Cloud Messaging (FCM)** | Push notifications for hearing reminders | Free tier sufficient for single-firm scale; used for web push to the browser for now (mobile push to a native app would apply if a MAUI app is added later per Section 11) |
| **Hangfire** | Background job scheduling (reminders) | Uses PostgreSQL storage — no separate Redis/SQL Server dependency needed; runs as part of the same VPS deployment |
| **Let's Encrypt / Certbot** | Free SSL/TLS certificate for HTTPS | Required for secure API and Web App access on the VPS's domain |
| **VPS Provider** (e.g., Hetzner, Contabo, DigitalOcean) | Hosts everything: API, database, web app, file storage | Selected on cost basis; sized for single-firm workload with headroom for document/photo growth |
| **Off-site backup target** (e.g., a low-cost object storage bucket used only for backup archives) | Disaster recovery for database dumps + storage directory | Not used for live application storage — purely a backup destination, kept cheap and minimal |

> Google Drive and any managed cloud database provider have been intentionally removed from this architecture — the single-VPS approach consolidates all costs into one server bill.

---

## 10. Glossary (Arabic/English Terms)

| Arabic Term | English Meaning | System Field |
|---|---|---|
| فردي | Sole Proprietorship | `company_category = SoleProprietorship` |
| أموال | Capital/Funds Company | `company_category = CapitalCompany` |
| أشخاص | Partnership/Persons Company | `company_category = Partnership` |
| السمة التجارية | Trade Name | `trade_name` |
| عقد التأسيس | Articles of Incorporation | `incorporation_doc_id` |
| عقد التعديل | Amendment Contract | `company_amendments` table |
| جنائي | Criminal Case | `case_type = Criminal` |
| شرعي | Personal Status/Family Case | `case_type = PersonalStatus` |
| مدني | Civil Case | `case_type = Civil` |
| قضاء إداري | Administrative Court Case | `case_type = Administrative` |
| مذكرات | Legal Memos/Briefs | `case_memos` table |
| المتهم | Defendant | `party_role = Defendant` |
| المجني عليه | Victim | `party_role = Victim` |
| الأجلات | Hearing Postponements | `case_hearings` chain |
| تأجيل | Postponement | `hearing_outcome = Postponed` |
| حكم | Judgment | `hearing_outcome = Judgment` |
| المتفق عليه | Agreed Fee | `agreed_fee` |
| المحصل | Collected Amount | Computed from `fee_transactions` |
| المتبقي | Remaining Balance | Computed (`agreed_fee` − collected) |
| الموكلين | Clients (People Registry) | `clients` table |
| الموكل | Client (Our Represented) | `client` record where `is_our_client = true` on the CaseParty |
| الخصم | Opponent | `client` record where `is_our_client = false` on the CaseParty |
| صورة الهوية | National ID Scan | `client.national_id_document_id` |
| صورة الوكالة | Attorney / Power of Attorney | `client.attorney_document_id` |

---

## 11. Open Decisions / Assumptions

These items should be confirmed with the firm before/during implementation, as they affect scope:

1. **Standard User & payment status visibility:** Should Standard Users see the Paid/Partial/Unpaid *label* without amounts, or truly nothing at all regarding financials? Current spec says the latter (strictest interpretation) — confirm this is intended.
2. **Standard User case-editing rights:** Can Standard Users log hearing outcomes/postponements, or is that also Admin-only? Assumed view-only for cases unless clarified.
3. **Multiple Admins:** Is more than one Admin account expected? If so, confirm whether all Admins have identical full access, or whether a future "Partner vs. Senior Associate" tier is anticipated.
4. **Data retention:** Is there a legal requirement (bar association, local law) for minimum retention periods on case files or financial ledgers that should influence backup policy?
5. **VPS provider & sizing:** Which VPS provider and plan (CPU/RAM/disk) will be used? Should be sized with headroom for document/photo storage growth over the next 1–2 years, not just current volume.
6. **Backup destination:** Where will off-site backups be stored (a separate cheap storage bucket, a secondary VPS, etc.)? This needs to be decided before go-live per TR-3.6/TR-9.5, since the single-VPS model has no built-in redundancy.
7. **Single point of failure acceptance:** Confirm the firm accepts that a single VPS is a single point of failure (if the server goes down, so does the entire system) in exchange for lower cost — this is a deliberate tradeoff, not an oversight, but should be explicitly acknowledged.
8. **Growth assumption:** Current architecture assumes single-firm, moderate data volume. If multi-user concurrent editing at high volume (e.g., 20+ staff) or data volume outgrows a single VPS becomes likely, revisit both the Modular Monolith module boundaries and the single-server hosting model.
9. **Native mobile app revisit:** A dedicated mobile app (.NET MAUI Blazor Hybrid or similar) was deferred to keep the local dev environment lightweight (avoiding the ~18GB MAUI workload). If, after using the responsive Web App on mobile browsers in practice, the firm decides a native app (offline capability, camera integration for document scanning, push notification reliability) is worth it, this can be revisited — the shared RCL structure (TR-4.3) is deliberately kept ready for that without requiring a rebuild of the core UI logic.

---

## 12. Development Workflow — Feature-by-Feature Build & Test Process

This section defines **how development shall proceed**, independent of what is being built. It is a standing instruction for whoever (or whatever AI tool) is implementing this system.

### 12.1 Process
1. Development shall proceed **one feature at a time**, not in large batches. A "feature" corresponds roughly to one module or sub-capability from Section 5 (e.g., "Companies CRUD," "Company Amendments," "Case Parties & Color Coding," "Fee Ledger & Balance Calculation," "Hearing Reminders," etc.).
2. For each feature, the implementer shall:
   - Build the feature (backend + relevant frontend pieces) end-to-end.
   - Seed the database with **dummy/sample test data** relevant to that feature (e.g., a handful of sample companies, a sample case with parties on both sides, a sample fee ledger with several transactions).
   - Test the feature against that dummy data, including edge cases described in the relevant requirement (e.g., for fees: partially paid, fully paid, unpaid states; for parties: client-as-defendant, client-as-victim, opponent-as-defendant, opponent-as-victim).
   - Confirm the feature behaves per its BR/TR requirements before considering it complete.
3. **The implementer shall not proceed to the next feature automatically.** After finishing and testing a feature, the implementer shall stop and wait for explicit confirmation.
4. The signal to proceed will be a message such as **"let's go to the next feature"** (or equivalent). Only then should work begin on the next feature in sequence.
5. If testing reveals an issue, it shall be fixed and re-tested against the dummy data **before** the "next feature" signal is given — features are not marked complete with known open issues.

### 12.2 Suggested Feature Sequence
This is a starting point — order may be adjusted, but each item should be treated as an individual feature slice:
1. Project scaffolding: solution structure, database connection, base authentication (Admin/Standard roles), single-VPS deployment skeleton
2. Companies: core CRUD + category classification
3. Companies: dynamic amendment contracts (ordered child records)
4. Companies: partners + document/photo uploads (local VPS storage)
5. Companies: fuzzy search + category filter
6. Cases: core CRUD + case types
7. Cases: parties (Defendant/Victim) + client/opponent color-coding logic
8. **Clients: central person registry (الموكلين) — Client CRUD, ID & attorney document uploads, quick-search selector for case parties**
9. Cases: legal memos + document uploads
9. Cases: hearings, postponement chain, and audit trail
10. Cases: 1-day-prior reminder scheduling (Hangfire) + FCM delivery
11. Financials: agreed fee + append-only fee transaction ledger
12. Financials: computed balance/status + strict role-based visibility (Admin vs Standard)
13. Cases/Companies: fuzzy search across case numbers and party names
14. UI: glassmorphism component library, Light/Dark theming (Section 13)
15. UI: Arabic/English localization + RTL/LTR switching
16. UI: responsive/mobile-friendly layout pass — test every screen at phone, tablet, and desktop breakpoints
17. Deployment: VPS provisioning, reverse proxy/SSL, backup automation (TR-9)

> **Note on build order — English-only UI during construction (deliberate, not an oversight):**
> All screens are built with hardcoded English labels and no RTL support until Section 12.2, item 15 is reached. This is intentional:
> - Localization (`IStringLocalizer`, `.resx` files, RTL/LTR switching per TR-5.1/TR-5.2) touches every screen at once — it is far more efficient as one dedicated pass across a mostly-complete UI than translating each screen piecemeal as it's built, only to re-touch all of them again once the shared language-switching mechanism exists.
> - Debugging core logic (data binding, validation, API contracts) is faster to read/reason about in English while chasing bugs, especially early on.
> - This does **not** mean Arabic data support is deferred — the **data layer** (e.g., `Company.CompanyName` holding Arabic text, `CompanyNameEn` as a separate optional field) is built correctly and tested with real Arabic input from the very first feature. Only the **UI chrome** (labels, button text, enum display names) stays English until the dedicated localization pass.

> **Note on build pattern per feature (clarifies "are we building Api or frontend?"):**
> Every feature in this sequence is built as one **vertical slice**, not as separate backend-only or frontend-only phases: Api piece first (controller + repository method, proven working alone in Swagger), then the matching Web piece (the Razor page/component that calls it), and the feature is only marked done once both work together end-to-end with dummy data. Section 12.1's "build the feature (backend + relevant frontend pieces) end-to-end" always means both halves, every time.

### 12.3 Dummy Data Guidelines for Testing
- Use clearly fake, obviously-non-real names/numbers for test companies, cases, and parties (e.g., "Test Co. — DO NOT USE IN PRODUCTION") so dummy records are never mistaken for real client data.
- Include at least one dummy record per category/type/status enum for each module, so every color-coded state (client/opponent, active/closed, paid/partial/unpaid) is exercised at least once during testing.
- Dummy data shall be easy to reset/re-seed (e.g., a seed script or a "reset test data" command) so each feature can be re-tested cleanly without manual cleanup.

---

## 13. UI Design Language — Glassmorphism (Light/Dark)

### 13.1 Design Principle
The frontend (the Blazor Web App, via the shared RCL) shall use a **glassmorphism** aesthetic: translucent, frosted-glass-like surfaces with background blur, subtle borders, soft shadows, and layered depth — applied consistently across cards, panels, navigation, modals, and the color-coded status badges already defined in Section 5, and remaining legible and performant at all responsive breakpoints (desktop, tablet, phone).

### 13.2 Core Visual Characteristics
| Property | Treatment |
|---|---|
| **Surface background** | Semi-transparent fill (not fully opaque) over a blurred backdrop, so underlying page content/gradient subtly shows through panels, cards, and modals. |
| **Backdrop blur** | Applied behind glass surfaces so content underneath is softened, not sharp — this is the defining visual trait of the style. |
| **Borders** | Thin, low-opacity light-colored borders (even in Dark mode) to define edges of glass panels without harsh lines. |
| **Shadows** | Soft, diffused drop shadows to reinforce a sense of floating layers/depth rather than flat design. |
| **Corner radius** | Consistently rounded corners across cards, buttons, and inputs to reinforce a soft, modern feel. |
| **Background canvas** | A subtle gradient or muted pattern behind the glass layers (not a flat solid color), since glassmorphism depends on *something* visible behind the blur to read correctly. |

### 13.3 Light Mode
- Base canvas: light, soft gradient (e.g., pale tones) rather than pure white.
- Glass surfaces: white-based translucency, dark text for contrast.
- Accent colors (status greens/reds/ambers from Section 5) remain fully saturated on top of the glass surfaces so color-coded badges stay clearly legible.

### 13.4 Dark Mode
- Base canvas: deep, muted dark gradient (not pure black) so blur/translucency remains visible.
- Glass surfaces: dark-based translucency with light text for contrast.
- Accent colors (status greens/reds/ambers) shall be slightly adjusted in brightness/saturation for Dark mode so they remain legible and don't appear washed out or overly harsh against dark glass.

### 13.5 Implementation Notes
- Both themes shall share the **same component markup/structure** — only CSS custom properties (colors, opacity, blur values) differ between Light and Dark, consistent with the theming architecture already defined in TR-5 and the shared RCL structure.
- Performance note: backdrop blur can be expensive on lower-end phones and older mobile browsers — the responsive layout should be tested for smoothness on real mobile devices during feature testing (Section 12), and may need a reduced blur radius or a "reduce transparency" fallback at narrow breakpoints if performance issues appear.
- The color-coded elements defined throughout this document (client/opponent badges, case status indicators, fee status indicators) shall be treated as glass surfaces themselves — e.g., a "Green" client badge is a translucent green glass chip, not a flat solid-green tag — so the color-coding system and the glassmorphism design language are visually unified rather than two separate styles bolted together.
- The shared RCL structure (TR-4.3) means this same glassmorphism component library is ready to be reused as-is if a native mobile app (e.g., MAUI Blazor Hybrid) is added in a later phase — see Section 11, item 9.

---

## 14. Progress Log

This section is a running record of what has actually been built and confirmed working, updated as each feature from Section 12.2 is completed. Its purpose is to preserve context across sessions — anyone (or any tool) picking up this project should read this section first to know exactly where the build stands before touching anything.

**How to keep this updated:** after each feature is tested and confirmed working, add a dated entry below before starting the next feature.

### Solution Structure (as of last update)
```
LegalERP.Web.sln
CLAUDE.md
LegalERP.Domain          — entities/enums, no dependencies
LegalERP.Application     — interfaces (IRepository<T>, ICompanyRepository), DTOs
LegalERP.Infrastructure  — EF Core DbContext, entity configurations, CompanyRepository, migrations
LegalERP.Api             — ASP.NET Core Web API, controllers, Swagger, .NET 8 LTS
LegalERP.Web             — Blazor Web App, Interactive Server, .NET 8 LTS
```
- Local dev database: PostgreSQL installed directly on Windows, database `legalerp_dev`
- Api runs on `https://localhost:7148` (port may vary by machine — check `LegalERP.Api/Properties/launchSettings.json`)
- Web runs on `https://localhost:7209` (port may vary by machine)
- Web → Api connection: registered `HttpClient` named `"LegalErpApi"` in `LegalERP.Web/Program.cs`, consumed via `CompanyApiClient` in `LegalERP.Web/Services/`
- `BaseEntity.RowVersion` is `[NotMapped]` — not a real database column on any table. Optimistic concurrency (TR-1.4) is deliberately deferred; do not re-add `.IsRowVersion()` or any per-entity concurrency config without designing it properly first (Npgsql's `xmin` shadow-property approach is the correct path when this is revisited).

### Completed & Confirmed Working

**2026-07-23 — Initial Audit (Antigravity Agent)**
Audited existing codebase. Found the following already built but NOT yet tested/confirmed by user:

- **Solution scaffolding (Feature 1 — partial):**
  - 5-project modular monolith structure in place
  - PostgreSQL connection configured (`legalerp_dev`)
  - EF Core DbContext with `ApplyConfigurationsFromAssembly`
  - Soft-delete global query filters on all entities
  - Swagger with enum-as-string serialization
  - CORS policy `AllowWebApp` (AllowAnyOrigin for dev)
  - Multi-project launch profile (Api + Web simultaneously)
  - ⚠️ **Authentication NOT yet implemented** — no ASP.NET Core Identity, no roles, no auth middleware

- **Companies core CRUD (Feature 2 — partial):**
  - Domain: `Company`, `CompanyAmendment`, `CompanyPartner` entities, `CompanyCategory` enum
  - Application: `IRepository<T>`, `ICompanyRepository` (with `SearchAsync`), DTOs (`CompanyDto`, `CreateCompanyDto`, etc.)
  - Infrastructure: `CompanyRepository`, entity configs with proper table names/constraints
  - API: `CompaniesController` with GET all, GET search, POST create, GET amendments, POST amendment
  - Web: `Home.razor` with basic create form + company list table, `CompanyApiClient` service
  - DB Migrations: `InitialCompaniesSchema` + `RemoveUnusedRowVersionColumn`
  - ⚠️ Search uses `ILike` (partial match), NOT `pg_trgm` fuzzy search yet
  - ⚠️ No Edit/Update/Delete endpoints yet
  - ⚠️ No dummy seed data

**2026-07-23 22:08 — User Manual Testing (Swagger / API)**
User tested the following endpoints via Swagger and confirmed working:

- ✅ `GET /api/companies` — Returns all companies correctly. 4 test records in DB:
  - `الشركة الرائعة` (Partnership) / wonderful one
  - `سيف للاستثمار` (SoleProprietorship) / Saif inc.
  - `شركة الاختبار للاستثمار` (CapitalCompany) / Test Investment Co.
  - `شركة الليف للاستثمار` (CapitalCompany) / LEAF Investment Co.
- ✅ `POST /api/companies` — Creates new company successfully
- ✅ `GET /api/companies/search?term=...` — Search works (ILike partial match)
- ✅ `GET /api/companies/{id}/amendments` — Returns amendments correctly (tested after BUG-001 fix)
- ✅ `POST /api/companies/{id}/amendments` — **BUG-001 fixed & confirmed**: Creates amendment with auto-sequencing.
- ✅ `GET /api/companies/{id}` — Returns single company with amendments and partners.
- ✅ `PUT /api/companies/{id}` — Updates company fields. Edge case tested: optional fields can be cleared.
- ✅ `DELETE /api/companies/{id}` — Soft-deletes company. **BUG-002 fixed**: Added JS confirmation dialog.
- ✅ Web App — Dashboard (Home), CompanyList, CompanyDetail, CompanyForm (Create + Edit) all working.
- ✅ Edge cases tested (2026-07-26): Empty required field blocked, optional fields cleared OK, delete cancel leaves record intact.

### Completed Features

**Feature 2: Companies CRUD + Category Classification — ✅ COMPLETE (2026-07-26)**
- API: GET all, GET by ID, POST, PUT, DELETE, Search endpoints
- Application: CreateCompanyDto, UpdateCompanyDto, CompanyDto, CompanyAmendmentDto, CompanyPartnerDto
- Web UI: Dashboard (Home.razor), CompanyList.razor, CompanyDetail.razor, CompanyForm.razor (shared create/edit)
- NavMenu: Cleaned up (removed Counter/Weather template links, added Companies)
- Template cleanup: Deleted WeatherForecast.cs
- Files created/modified:
  - `LegalERP.Api/Controllers/CompaniesController.cs` — Full CRUD + amendments endpoints
  - `LegalERP.Application/Companies/CompanyDto.cs` — Added UpdateCompanyDto
  - `LegalERP.Web/Services/CompanyApiClient.cs` — GetByIdAsync, UpdateAsync, DeleteAsync
  - `LegalERP.Web/Components/Pages/Companies/CompanyList.razor` — [NEW]
  - `LegalERP.Web/Components/Pages/Companies/CompanyDetail.razor` — [NEW]
  - `LegalERP.Web/Components/Pages/Companies/CompanyForm.razor` — [NEW]
  - `LegalERP.Web/Components/Pages/Home.razor` — Converted to dashboard
  - `LegalERP.Web/Components/Layout/NavMenu.razor` — Updated links

**Feature 3+4: Professional Company Management System (Files + Partners) — ✅ COMPLETED**
- **Domain/Infrastructure:** Created polymorphic `Document` entity, added `EstablishmentDate`, `RegistrationNumber`, and `Address` to `Company`. Configured `ApplicationDbContext` and added EF migration.
- **Storage:** Created `LocalFileStorageService` saving files directly to `wwwroot/uploads`.
- **API:** Created `DocumentsController` (POST/GET/DELETE). Added `Partners` CRUD endpoints to `CompaniesController`. Updated `Company` endpoints to map new fields. Implemented auto-generating Arabic ordinal sequence titles (عقد التعديل الأول).
- **Web UI:** Created `FileThumbnail.razor` component with modal popups for PDFs and Images. Rewrote `CompanyForm.razor` to handle new fields and initial file upload. Rewrote `CompanyDetail.razor` to include interactive Partner Management, Amendment Management, and direct Incorporation Contract Upload/Preview/Replacement with integrated document uploads. All deletions include `confirm()` JS dialogs.

- **Bug Fixes (2026-07-26 & 2026-07-28):** Fixed `500 Internal Server Error` on recreating soft-deleted amendments by making the `SequenceNumber` database unique index a Partial Index (ignoring soft-deleted rows). Fixed UI placeholder to dynamically reflect correct Arabic ordinal based on active amendments. Added direct Incorporation Contract uploading and preview thumbnails to `CompanyDetail.razor`. Fixed Client Status dropdown in `CaseDetail.razor` (BUG-006) to use explicit string selection binding so selecting "Opponent" correctly sets `IsOurClient = false` (Red badge).

**Feature 5: Fuzzy Search (pg_trgm) + Category Filter — ✅ COMPLETED (2026-07-28)**
- **Infrastructure:** Enabled `pg_trgm` extension in `ApplicationDbContext`. Added GIN trigram indexes (`gin_trgm_ops`) on `CompanyName`, `CompanyNameEn`, and `TradeName` in `CompanyConfiguration`.
- **API/Repository:** Updated `CompanyRepository.SearchAsync` to use `EF.Functions.TrigramsAreSimilar` and `ILike` pattern matching.
- **Web UI:** Added `SearchAsync` to `CompanyApiClient`. Added Search bar (with Enter key binding), Category filter dropdown, and Reset button to `CompanyList.razor`.

**Feature 6: Cases Core CRUD & Case Types — ✅ COMPLETED (2026-07-28)**
- **Domain/Infrastructure:** Created `Case` entity, `CaseType` enum, `CaseStatus` enum, `CaseConfiguration` with GIN trigram indexes on `CaseNumber` and `Title`.
- **API:** Created `CasesController` (CRUD + Search).
- **Web UI:** Created `CaseApiClient`, `CaseList.razor`, `CaseForm.razor`, `CaseDetail.razor`, updated `NavMenu.razor`.

**Feature 7 & 8: Case Parties, Color Coding, Custom Document Titles & Closing Outcomes — ✅ COMPLETED (2026-07-28)**
- Domain/Infrastructure: `CaseParty` entity, `PartyRole` enum (Defendant/Victim), `CaseOutcome` enum (Won/Lost/Settled), `CasePartyConfiguration`.
- API: Party endpoints (`POST/PUT/DELETE /api/cases/{caseId}/parties`).
- Web UI: Updated `CaseDetail.razor` with Parties management, Client/Opponent color badges (Client=Green, Opponent=Red), Party document uploads, custom document titles for case files, and outcome badges on case closing (Won=Green, Lost=Red, Settled=Blue).
- Bug Fix (BUG-006): Fixed Client Status dropdown binding to correctly select Opponent.

**Feature 9: Clients Module (الموكلين) — Central Person Registry — ✅ COMPLETED (2026-07-31)**
- Domain: New `Client` entity (name, national ID, phone, email, address, notes, National ID document FK, Attorney document FK). Added `ClientId` FK to `CaseParty` and `CompanyPartner`.
- Infrastructure: `ClientConfiguration`, `ClientRepository`, updated `CasePartyConfiguration` and `CompanyPartnerConfiguration` with Client FK.
- API: `ClientsController` (CRUD + search). Registered `IClientRepository` in DI.
- Web UI: `ClientList.razor`, `ClientForm.razor`, `ClientDetail.razor` (with related cases & companies). Updated `CaseDetail.razor` with Client quick-search selector when adding parties. Added "Clients (الموكلين)" nav link.
- Migration: `20260731092545_AddClientsModule` applied to database.
- Company Partner Refactor (BR-6.8): Refactored `CompanyDetail.razor` "+ Add Partner" form to use Client Quick-Search Selector. Partner name, National ID, and ID document are now pulled directly from the central `Client` record. `CompanyPartnerDto` and `CompaniesController.cs` map `partner.Client`. `CompanyRepository.cs` includes `p.Client.NationalIdDocument`.

### In Progress

**Feature 10: Cases Legal Memos (مذكرات) & Document Management**
- Domain: Create `CaseMemo` entity (Title, Content/Notes, Date, CaseId, DocumentId FK).
- Infrastructure: `CaseMemoConfiguration`, update `ApplicationDbContext`, update `CaseRepository`.
- API: Memo CRUD endpoints on `CasesController` (`POST/GET/DELETE /api/cases/{caseId}/memos`).
- Web UI: Update `CaseDetail.razor` with a dedicated **Legal Memos & Briefs (المذكرات القانونية)** section supporting title, text content, file upload attachment with thumbnails + preview popups, and delete dialogs.

### Current Position

**Feature Sequence Position: Ready for Feature 10 (Cases: Legal Memos & Documents)**
Clients Module is tested and approved. Awaiting user green light to build Feature 10.

### Active Agent Instructions

- **User Builds and Tests**: The user is solely responsible for running `dotnet build`/`run` via Visual Studio and performing manual UI tests. The agent will wait for the user to report bugs or give the green light.
- **Delete Confirmations**: ALL delete actions in the system must have a confirmation dialog (e.g., JS `confirm`) to prevent accidental data loss.
- **Bug Tracking Loop**: If the user reports a bug, the agent must fix it, add it to `BUGS.md`, and update `CLAUDE.md` before returning to the user.
- **Feature Loop**: Agent builds feature then updates CLAUDE.md then provides test instructions + edge cases then user builds/runs/tests then user reports results then agent logs to BUGS.md if needed then agent updates CLAUDE.md then repeat or move to next feature.
- **No API-only testing needed**: Since the Web UI calls the API, testing the Web UI automatically tests the API. User only needs to test Web UI.
- **File uploads**: Thumbnails + popup preview for all uploaded files. Allowed types: PDF, JPG, JPEG, PNG. Compress images over 10MB. No hard upload limit.

---

*End of reference document.*

