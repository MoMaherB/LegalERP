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
