# 02: Fix Client 360° Profile Party Role & Case Type Localization, Card Layout, and Test the Clients Slice

**What to build:** Fix the Client 360° profile page (`/clients/{id}`) so related cases display properly localized party roles (`المتهم`/`المجني عليه` for Criminal cases vs. `المدعى عليه`/`المدعي` for non-Criminal cases via `CasePartyRoleHelper`) and localized case types (`جنائي`, `شرعي`, `مدني`, `قضاء إداري`), fix horizontal flex crowding on the Client detail cards (`flex-column align-items-stretch`) and canonical `DocumentOwnerTypes` usage, and add end-to-end unit and bUnit tests for `ClientsController`, `ClientDetail`, and `ClientList`.

**Blocked by:** None (01 resolved)

**Status:** resolved

- [x] `ClientDetail` resolves party role labels in the Related Cases table using `CasePartyRoleHelper.GetRoleLocalizationKey(c.CaseType, c.PartyRole)` instead of non-existent `"Defendant"` / `"Victim"` resource keys.
- [x] `ClientDetail` localizes the case type badge in the Related Cases table (`@Loc[c.CaseType.ToString()]`) and uses canonical `DocumentOwnerTypes.Client` and `DocumentOwnerTypes.Attorney` constants.
- [x] `ClientDetail` cards use vertical flex alignment (`flex-column align-items-stretch`) so linked Power of Attorney cards and sections stack cleanly without horizontal crowding.
- [x] Automated unit tests cover `ClientsController` (CRUD, search delegation, and 360° `ClientDto` mapping of related cases and company partnerships).
- [x] Automated bUnit tests cover `ClientDetail` (rendering personal info, related cases with localized Criminal vs. Civil party roles and case types, related companies, linked attorneys, and delete confirmation) and `ClientList` (searching clients and delete confirmation).
