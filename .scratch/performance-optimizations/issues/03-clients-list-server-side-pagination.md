# 03: Clients List Server-Side Pagination

**What to build:** Extend server-side pagination to the Clients module so large client rosters load instantly in discrete pages, keeping memory consumption low on both server and client.

**Blocked by:** 01 (Core Pagination Abstraction & Cases List Pagination)

**Status:** resolved

- [x] `GET /api/clients` and `GET /api/clients/search` support `pageNumber` and `pageSize`, returning a `PagedResult<ClientSummaryDto>`.
- [x] `ClientList.razor` displays intuitive page controls, preserving text search queries across pagination actions.
- [x] Automated tests cover paginated client fetching, search filter preservation, and page boundary handling.
