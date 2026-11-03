# 05: Review and Test Company & Case Create/Edit Forms and List Views

**What to build:** Review and add bUnit tests for `CompanyForm`, `CompanyList`, `CaseForm`, and `CaseList` so all primary CRUD entry forms and list filters across Companies (`الشركات`) and Cases (`القضايا`) have automated regression coverage.

**Blocked by:** 04: Harden and Test Authentication, SuperAdmin User Management Guards, and User Profile Picture Lifecycle

**Status:** ready-for-agent

- [ ] Automated bUnit tests cover `CompanyForm` in Create mode (creating the company first and uploading the Incorporation Contract `عقد التأسيس` with `OwnerId == newCompany.Id`) and Edit mode (preserving `HasManyPartners`, `PartnersIdsDocumentId`, and `PartnersAttorneysDocumentId` without wiping existing bulk partner document links).
- [ ] Automated bUnit tests cover `CaseForm` in Create and Edit modes (verifying `CaseStatus.Closed` outcome selection `مكسب`/`خسارة`/`تصالح` and ensuring `Outcome` is cleared when `CaseStatus.Active` is selected).
- [ ] Automated bUnit tests cover `CompanyList` (category filtering, search by term, and delete confirmation) and `CaseList` (search by term, status/outcome badges, and delete confirmation).
