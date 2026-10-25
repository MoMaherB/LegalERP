---
id: 01
title: Adopt ClientQuickSearchSelector in AttorneyDetail and add RBAC to TogglePartnerActive
status: resolved
depends_on: []
---

# Adopt `ClientQuickSearchSelector` in `AttorneyDetail.razor` & Add RBAC to `TogglePartnerActive`

## Goal

Complete Candidate 4 from the Architecture Review by replacing the legacy manual client search box and results table in `AttorneyDetail.razor` with the shared debounced `<ClientQuickSearchSelector>` component, and adding the missing `[Authorize(Roles = "SuperAdmin,Admin,Editor")]` attribute on `CompaniesController.TogglePartnerActive`.

## Scope & Requirements

1. **`ClientQuickSearchSelector.razor` (`ExcludedClientIds` support):**
   - Add an optional `[Parameter] public IReadOnlyCollection<Guid>? ExcludedClientIds { get; set; }` parameter.
   - When a client in `clientSearchResults` has its `Id` in `ExcludedClientIds`, render the item as `disabled` with the `@Loc["AlreadyLinked"]` badge (`<span class="ios-status-badge ios-status-green ms-2">@Loc["AlreadyLinked"]</span>`) so users cannot re-select an already-linked client.

2. **`AttorneyDetail.razor` Migration to `<ClientQuickSearchSelector>`:**
   - Replace the manual search input, `@Loc["Search"]` button, `HandleClientSearchKeyUp`, `SearchClients`, and `searchResults` table in `AttorneyDetail.razor` with `<ClientQuickSearchSelector @bind-SelectedClient="selectedClient" ExcludedClientIds="LinkedClientIds" Required="true" />`.
   - Provide a `@Loc["Link"]` button (enabled when `selectedClient != null`) that calls `LinkClient(selectedClient.Id)` and a `@Loc["Cancel"]` button that resets `showAddClientForm` and `selectedClient`.
   - Remove the direct `@inject ClientApiClient ClientApi` dependency from `AttorneyDetail.razor` (since `<ClientQuickSearchSelector>` encapsulates `ClientApiClient`).

3. **`CompaniesController.TogglePartnerActive` RBAC:**
   - Add `[Authorize(Roles = "SuperAdmin,Admin,Editor")]` to `TogglePartnerActive` (`PUT api/companies/{companyId}/partners/{partnerId}/toggle-active`) in `LegalERP.Api/Controllers/CompaniesController.cs`.

## Acceptance Criteria

- [ ] `ClientQuickSearchSelector.razor` supports `ExcludedClientIds`, rendering already-linked clients with the `@Loc["AlreadyLinked"]` badge and preventing selection.
- [ ] `AttorneyDetail.razor` uses `<ClientQuickSearchSelector>` for linking clients to a Power of Attorney (`توكيل`) and no longer injects `ClientApiClient` directly or maintains manual `clientSearchTerm`/`searchResults` state.
- [ ] `CompaniesController.TogglePartnerActive` is decorated with `[Authorize(Roles = "SuperAdmin,Admin,Editor")]`.
- [ ] bUnit tests in `LegalERP.Tests` cover `ClientQuickSearchSelector` `ExcludedClientIds` behavior, `AttorneyDetail` client-linking via `ClientQuickSearchSelector`, and `TogglePartnerActive` `[Authorize]` attribute verification.
- [ ] Full test suite (`dotnet test LegalERP.Tests/LegalERP.Tests.csproj`) passes with 0 failures.
