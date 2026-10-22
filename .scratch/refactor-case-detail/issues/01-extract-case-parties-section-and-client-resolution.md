# 01: Extract CasePartiesSection and Fix Client Party Resolution

**What to build:**
Extract the Case Parties (`الخصوم`) management workflow and reusable Client (`الموكلين`) quick-search selector out of `CaseDetail.razor` into deep, self-contained modules, and fix the backend `CaseParty` projection so parties linked to `Our Client` (`الموكل`) resolve their identity fields directly from the central `Client` record:
- Extract a debounced, race-safe `ClientQuickSearchSelector` module (`FullName`, `NationalIdNumber`, and `"➕ Add new client"` `/clients/new` action) shared between `CasePartiesSection` and `CompanyPartnersSection`.
- Extract `CasePartiesSection` encapsulating the Parties table, Client (`الموكل` — green badge) vs Opponent (`الخصم` — red badge) entry modes, clickable `/clients/{id}` profile links, dynamic legal role labels (`المتهم` / `المجني عليه` for Criminal cases vs `المدعى عليه` / `المدعي` for non-Criminal cases), party document preview/upload, and RBAC permission checks (`CanEdit`, `CanDelete`).
- Fix `CasesController` party mapping so when a `CaseParty` has a linked `Client`, `FullName`, `NationalIdNumber` (`رقم البطاقة`), `PhoneNumber`, and `Document` fall back to `p.Client` rather than storing `"Name (ID: ...)"` in `FullName` and rendering `"—"` for National ID and Phone.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] A reusable `ClientQuickSearchSelector` module handles debounced Client search, cancellation, selection, and the `/clients/new` fallback without leaking formatted display text into `FullName`.
- [x] `CasesController` resolves `FullName`, `NationalIdNumber`, `PhoneNumber`, and `Document` from `p.Client` when `p.ClientId` is present, while preserving manual fields for Opponents (`الخصم`).
- [x] Dynamic legal role terminology (`المتهم` / `المجني عليه` for `Criminal` vs `المدعى عليه` / `المدعي` for non-`Criminal`) is centralized in an in-process helper and tested across all `CaseType` values.
- [x] `CasePartiesSection` encapsulates adding, editing, and deleting Case Parties with `CanEdit` and `CanDelete` role enforcement, and `CaseDetail.razor` delegates party rendering to `<CasePartiesSection>`.
- [x] Automated unit and bUnit tests verify backend Client-linked party resolution, role terminology, and `CasePartiesSection` rendering/interactions.
