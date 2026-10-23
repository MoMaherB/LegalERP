# 02: Unify Entity Financials Endpoints into FinancialsController and SharedFinancials

**What to build:**
Consolidate all entity-level Financials (`الماليات`) API endpoints and HTTP client calls into `FinancialsController` and `FinancialsApiClient`, and remove the `OwnerType` branching in `SharedFinancials.razor`:
- Add unified entity endpoints on `FinancialsController` (`GET api/financials/{ownerType}/{ownerId}`, `PUT api/financials/{ownerType}/{ownerId}/agreed-fee`, `POST api/financials/{ownerType}/{ownerId}/transactions`, `DELETE api/financials/{ownerType}/{ownerId}/transactions/{transactionId}`) protected by `[Authorize(Roles = "SuperAdmin,Admin")]`.
- Remove the 8 duplicate financial endpoints from `CasesController` and `CompaniesController` and the duplicate financial methods from `CaseApiClient` and `CompanyApiClient`.
- Update `FinancialsApiClient` and `SharedFinancials.razor` so `SharedFinancials` talks exclusively to `FinancialsApiClient` without branching on `OwnerType == "case"` vs `"company"`, and update existing tests.

**Blocked by:** 01: Extract FinancialLedgerCalculator and Fix Global Financials Summary

**Status:** ready-for-agent

- [ ] `FinancialsController` and `FinancialsApiClient` handle `GetFinancials`, `UpdateAgreedFee`, `AddFeeTransaction`, and `DeleteFeeTransaction` for both `"case"` and `"company"` owners.
- [ ] The 8 duplicated financial endpoints on `CasesController` and `CompaniesController` (and their client methods on `CaseApiClient` and `CompanyApiClient`) are removed.
- [ ] `SharedFinancials.razor` delegates all operations to `FinancialsApiClient` without `if (OwnerType == "case")` branching.
- [ ] Automated tests verify `FinancialsController` entity operations, `SharedFinancials.razor`, and `FinancialsOverview.razor`.
