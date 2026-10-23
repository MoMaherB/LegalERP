# 01: Extract FinancialLedgerCalculator and Fix Global Financials Summary

**What to build:**
Unify the calculation of Agreed Fee (`المتفق عليه`), Collected Amount (`المحصل`), Remaining Balance (`المتبقي`), and Payment Status (`Paid` / `PartiallyPaid` / `Unpaid`) into a single pure in-process `FinancialLedgerCalculator` module in the Application layer, and fix the firm-wide Financials (`الماليات`) summary in `FinancialsController`:
- Extract `FinancialLedgerCalculator` to compute `TotalCollected`, `RemainingBalance` (`AgreedFee.HasValue ? AgreedFee.Value - TotalCollected : 0m`), `PaymentStatus`, and full `EntityFinancialsDto` / `FinancialRecordDto` projections so entity detail views and `/financials` overview never drift when `AgreedFee` is null or updated.
- Update `FinancialsController.GetSummary`, `CasesController.GetFinancials`, and `CompaniesController.GetFinancials` to delegate ledger math to `FinancialLedgerCalculator`.
- Fix `FinancialsController.GetSummary` for Companies so `ReferenceNumber` resolves `RegistrationNumber` (`السجل التجاري`), falling back to `FileNumber` (`رقم الملف`) or `"N/A"`, instead of reading the unused `CommercialRegisterNumber` field.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] `FinancialLedgerCalculator` computes `TotalCollected`, `RemainingBalance`, `PaymentStatus`, and `EntityFinancialsDto` consistently across all callers (including when `AgreedFee` is null, zero, partially paid, or overpaid).
- [x] `FinancialsController.GetSummary`, `CasesController.GetFinancials`, and `CompaniesController.GetFinancials` delegate all balance and status calculations to `FinancialLedgerCalculator`.
- [x] Company records in `FinancialsController.GetSummary` display their `RegistrationNumber` (or `FileNumber` / `"N/A"` fallback) instead of always returning `"N/A"` from the unused `CommercialRegisterNumber` property.
- [x] Automated unit tests cover `FinancialLedgerCalculator` edge cases and `FinancialsController.GetSummary` aggregation and reference number resolution.
