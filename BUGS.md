# LegalERP — Bug Tracker

This file tracks all bugs discovered during testing, their root cause, and resolution.

---

## BUG-001: DbUpdateConcurrencyException on POST amendment

| Field | Detail |
|-------|--------|
| **Date** | 2026-07-23 22:14 |
| **Endpoint** | `POST /api/companies/{companyId}/amendments` |
| **Error** | `Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException: The database operation was expected to affect 1 row(s), but actually affected 0 row(s)` |
| **Status** | ✅ FIXED |

### Root Cause
The `AddAmendment` controller method added the new `CompanyAmendment` via `company.Amendments.Add(amendment)`, which caused EF Core to mark the parent `Company` entity as **Modified**. When EF tried to UPDATE the Company row, it triggered a phantom concurrency check — a ghost from the previously removed `xmin` concurrency token (removed in migration `RemoveUnusedRowVersionColumn`). EF expected to match a concurrency token that no longer exists in the database, so the UPDATE affected 0 rows → exception.

### Fix
Changed the controller to add the amendment **directly to the DbContext** via a new `AddAmendmentAsync()` method on the repository, bypassing the Company navigation property entirely. This way only an INSERT on `company_amendments` is issued — no UPDATE on `companies`.

### Files Changed
- `LegalERP.Api/Controllers/CompaniesController.cs` — Use `_repository.AddAmendmentAsync(amendment, ct)` instead of `company.Amendments.Add(amendment)`
- `LegalERP.Application/Companies/ICompanyRepository.cs` — Added `Task AddAmendmentAsync(CompanyAmendment amendment, CancellationToken ct = default)`
- `LegalERP.Infrastructure/Repositories/CompanyRepository.cs` — Implemented `AddAmendmentAsync` via `_db.CompanyAmendments.AddAsync()`

---

## BUG-002: Missing Delete Confirmation

| Field | Detail |
|-------|--------|
| **Date** | 2026-07-23 22:45 |
| **Component** | `CompanyList.razor` |
| **Error** | Deleting a company happened instantly on button click without any warning/confirmation dialog, which is dangerous. |
| **Status** | ✅ FIXED |

### Root Cause
The `DeleteCompany(Guid id)` method directly called the API when the Delete button was clicked, without prompting the user.

### Fix
Injected `IJSRuntime JS` into `CompanyList.razor` and added `await JS.InvokeAsync<bool>("confirm", ...)` to prompt the user before calling the API.

### Files Changed
- `LegalERP.Web/Components/Pages/Companies/CompanyList.razor` — Added `JS.InvokeAsync` and wrapped delete logic in an `if (confirmed)` block.

---

## BUG-003: 500 Internal Server Error when recreating a deleted amendment

| Field | Detail |
|-------|--------|
| **Date** | 2026-07-26 17:50 |
| **Endpoint** | `POST /api/companies/{companyId}/amendments` |
| **Error** | PostgreSQL unique constraint violation on `SequenceNumber` and `CompanyId`. |
| **Status** | ✅ FIXED |

### Root Cause
When an amendment (e.g. sequence 1) is soft-deleted, it remains in the DB. The API calculates the next sequence number by looking at visible amendments (`company.Amendments.Max + 1`), which evaluates to 1 again because the deleted row is hidden by EF query filters. Inserting a new amendment with `SequenceNumber = 1` triggered the DB unique constraint, causing a crash.

### Fix
Instead of calculating a non-conflicting sequence number on the backend, the user requested that sequence numbers reuse the old slots identically to array indexing (e.g., if you delete sequence 3, the next added should be 3). To support this without 500 errors, we converted the unique DB constraint into a **Partial Index** (`.HasFilter("\"IsDeleted\" = false")`) so PostgreSQL ignores soft-deleted rows when enforcing uniqueness.

### Files Changed
- `LegalERP.Infrastructure/Persistence/Configurations/CompanyAmendmentConfiguration.cs` — Added partial index filter.
- **Migration:** `PartialIndexForAmendments`

---

## BUG-004: UI Amendment Placeholder didn't dynamically increment

| Field | Detail |
|-------|--------|
| **Date** | 2026-07-26 17:50 |
| **Component** | `CompanyDetail.razor` |
| **Error** | The placeholder for the "New Amendment" title form field was hardcoded to "عقد التعديل الأول" instead of reflecting the actual next sequence. |
| **Status** | ✅ FIXED |

### Root Cause
HTML placeholder was hardcoded.

### Fix
Created a `NextAmendmentPlaceholder` property in `CompanyDetail.razor` that dynamically looks at `company.Amendments` and calculates the proper Arabic ordinal fallback matching the backend logic.

### Files Changed
- `LegalERP.Web/Components/Pages/Companies/CompanyDetail.razor`

---

## BUG-005: 500 Internal Server Error navigating to /cases (Missing Database Table / Migration)

| Field | Detail |
|-------|--------|
| **Date** | 2026-07-28 18:36 |
| **Endpoint** | `GET /api/cases/search` |
| **Error** | `HttpRequestException: Response status code does not indicate success: 500 (Internal Server Error)` |
| **Status** | ✅ RESOLVING (Awaiting Migration Execution) |

### Root Cause
The `cases` database table has not been created in PostgreSQL yet. When the frontend `CaseList.razor` loads, it calls `SearchAsync` which queries `_db.Cases`. Because the `cases` table does not exist in PostgreSQL prior to running `dotnet ef database update`, PostgreSQL throws `relation "cases" does not exist` (500 Internal Server Error).

### Fix
Execute `dotnet ef migrations add AddCasesTable` and `dotnet ef database update` in the terminal to generate and apply the `cases` table and its GIN trigram indexes to PostgreSQL.

### Files Changed
- `LegalERP.Infrastructure/Migrations/` — Add `AddCasesTable` migration.

---

## BUG-006: Client Status Select Dropdown always defaulted to "Our Client"

| Field | Detail |
|-------|--------|
| **Date** | 2026-07-28 18:55 |
| **Component** | `CaseDetail.razor` |
| **Error** | Selecting "Opponent" in the Client Status dropdown was ignored and saved as "Our Client" (Green) anyway. |
| **Status** | ✅ FIXED |

### Root Cause
The `<select>` element bound directly to `newPartyIsOurClient` (a `bool`) using string option values `<option value="true">` and `<option value="false">`. HTML `<select>` value bindings in Blazor do not automatically coerce string `"false"` to boolean `false`, causing the model to remain at its default value `true`.

### Fix
Refactored the Client Status dropdown in `CaseDetail.razor` to bind to an explicit string property `newPartyClientType` (`"client"` vs `"opponent"`), converting it to boolean `IsOurClient = newPartyClientType == "client"` during submission.

### Files Changed
- `LegalERP.Web/Components/Pages/Cases/CaseDetail.razor` — Changed binding to explicit string `newPartyClientType`.

---

## BUG-007: Client Quick-Search Over-matching & Missing ID Disambiguation

| Field | Detail |
|-------|--------|
| **Date** | 2026-07-31 13:22 |
| **Component** | `ClientRepository.cs`, `CaseDetail.razor`, `ClientForm.razor` |
| **Error** | Searching for "محمد عيسي" returned "محمد ماهر" due to trigram fuzzy similarity. Also, client dropdown lacked National ID display for same-name disambiguation, and National ID was not mandatory. |
| **Status** | ✅ FIXED |

### Root Cause
1. `ClientRepository.SearchAsync` used `EF.Functions.TrigramsAreSimilar`, which fuzzy matched any name sharing common sub-words (like "محمد").
2. `CaseDetail.razor` party dropdown rendered only `FullName` without showing `NationalIdNumber` in parentheses `()`.
3. `ClientForm.razor` allowed saving a Client without entering `NationalIdNumber`.

### Fix
1. Refactored `ClientRepository.SearchAsync` to split search terms by spaces and enforce strict `ILike` matching on ALL words. Searching "محمد عيسي" now only returns clients matching both "محمد" AND "عيسي".
2. Updated `CaseDetail.razor` quick-search dropdown to display `FullName (ID: NationalId)` for clear disambiguation.
3. Updated `CaseDetail.razor` dropdown to always render the **"➕ Client not found — Add new client"** button at the bottom of search results.
4. Made `NationalIdNumber` a required field in `ClientForm.razor`.

### Files Changed
- `LegalERP.Infrastructure/Repositories/ClientRepository.cs`
- `LegalERP.Web/Components/Pages/Cases/CaseDetail.razor`
- `LegalERP.Web/Components/Pages/Clients/ClientForm.razor`

