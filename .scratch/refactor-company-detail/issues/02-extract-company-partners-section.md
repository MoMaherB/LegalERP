# 02: Extract CompanyPartnersSection

**What to build:**
Extract all partner management functionality out of `CompanyDetail.razor` into a self-contained, deep component `CompanyPartnersSection.razor`. The component encapsulates:
- Supporting both modes:
  - Mode 1 (&le;5 partners): Client search autocomplete dropdown with linking to central Client profile.
  - Mode 2 (&gt;5 partners): Direct text input for partner name and ownership percentage (no client search required).
- Split partner tables: **شركاء حاليون** (Current Partners) and **شركاء سابقون** (Former Partners).
- Independent row numbering (`#`) resetting for each section.
- Instant active/former status toggling via API + in-place list update without a full page reload.
- Bulk PDF upload and replacement slots for >5 mode (**بطاقات الشركاء** & **توكيلات الشركاء**).

**Blocked by:** 01: Extract CompanyAmendmentsSection

**Status:** resolved

- [x] `CompanyPartnersSection.razor` created under `LegalERP.Web/Components/Pages/Companies/Sections/`.
- [x] Accepts parameters for `Company`, `CanEdit`, `CanDelete`, and `OnChanged` callback.
- [x] Handles client search with debounce and autocomplete selection for &le;5 mode.
- [x] Handles direct name entry for &gt;5 mode.
- [x] Renders Current and Former partner tables with 1-based `#` row numbering.
- [x] Instant toggle switches partners between Current and Former sections without full page reload.
- [x] Bulk document cards for IDs and Attorneys remain functional in >5 mode.
- [x] `CompanyDetail.razor` delegates partner rendering to `<CompanyPartnersSection>`.
- [x] Solution builds with 0 errors and all partner workflows function seamlessly.
