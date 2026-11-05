# 01: Core Pagination Abstraction & Cases List Pagination

**What to build:** Introduce a reusable server-side pagination model across the application layer, apply it to the Cases module so users can navigate large case volumes in bounded pages (e.g. 25–50 cases per page), and update the UI with responsive page controls.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] A reusable `PagedResult<T>` and `PaginationParams` record exists in `LegalERP.Application.Common` defining `PageNumber`, `PageSize`, `TotalCount`, `TotalPages`, `HasPreviousPage`, and `HasNextPage`.
- [x] `GET /api/cases` accepts optional pagination parameters, returns paginated data along with total counts, and defaults gracefully when parameters are omitted.
- [x] `CaseList.razor` displays clean pagination controls (Previous, Next, Current Page indicator, and page size selector) allowing the lawyer to navigate across pages seamlessly.
- [x] Automated tests cover paginated retrieval, boundary conditions (first page, last page, empty results), and UI navigation.
