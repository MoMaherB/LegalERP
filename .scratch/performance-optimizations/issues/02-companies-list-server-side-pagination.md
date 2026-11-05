# 02: Companies List Server-Side Pagination

**What to build:** Extend the shared pagination model to the Companies module so users can navigate and filter corporate records with bounded page sizes, keeping initial page load fast regardless of total company volume.

**Blocked by:** 01 (Core Pagination Abstraction & Cases List Pagination)

**Status:** resolved

- [x] `GET /api/companies` and `GET /api/companies/search` accept pagination parameters alongside category and search term filters, returning a structured `PagedResult<CompanyDto>`.
- [x] `CompanyList.razor` includes pagination controls aligned with the design system, preserving active category and search filters across page navigation.
- [x] Automated tests verify that searching and changing pages on companies preserves query filters, handles out-of-range pages cleanly, and updates the list view.
