using System;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Cases;
using LegalERP.Application.Companies;
using LegalERP.Application.Financials;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class FinancialsController : ControllerBase
{
    private const string CaseOwner = "case";
    private const string CompanyOwner = "company";

    private readonly ICaseRepository _caseRepository;
    private readonly ICompanyRepository _companyRepository;

    public FinancialsController(ICaseRepository caseRepository, ICompanyRepository companyRepository)
    {
        _caseRepository = caseRepository;
        _companyRepository = companyRepository;
    }

    // GET /api/financials/summary
    [HttpGet("summary")]
    public async Task<ActionResult<GlobalFinancialsDto>> GetSummary(CancellationToken ct)
    {
        var cases = await _caseRepository.GetAllAsync(ct);
        var companies = await _companyRepository.GetAllAsync(ct);

        return Ok(FinancialLedgerCalculator.BuildSummary(cases, companies));
    }

    // GET /api/financials/{ownerType}/{ownerId}
    [HttpGet("{ownerType}/{ownerId:guid}")]
    public async Task<ActionResult<EntityFinancialsDto>> GetEntityFinancials(string ownerType, Guid ownerId, CancellationToken ct)
    {
        switch (ownerType.ToLowerInvariant())
        {
            case CaseOwner:
                var caseEntity = await _caseRepository.GetByIdAsync(ownerId, ct);
                if (caseEntity is null) return NotFound();
                return Ok(FinancialLedgerCalculator.ToEntityFinancialsDto(caseEntity.AgreedFee, caseEntity.FeeTransactions));

            case CompanyOwner:
                var company = await _companyRepository.GetByIdAsync(ownerId, ct);
                if (company is null) return NotFound();
                return Ok(FinancialLedgerCalculator.ToEntityFinancialsDto(company.AgreedFee, company.FeeTransactions));

            default:
                return UnknownOwnerType(ownerType);
        }
    }

    // PUT /api/financials/{ownerType}/{ownerId}/agreed-fee
    [HttpPut("{ownerType}/{ownerId:guid}/agreed-fee")]
    public async Task<ActionResult> UpdateAgreedFee(string ownerType, Guid ownerId, UpdateAgreedFeeDto dto, CancellationToken ct)
    {
        switch (ownerType.ToLowerInvariant())
        {
            case CaseOwner:
                var caseEntity = await _caseRepository.GetByIdAsync(ownerId, ct);
                if (caseEntity is null) return NotFound();
                caseEntity.AgreedFee = dto.AgreedFee;
                _caseRepository.Update(caseEntity);
                await _caseRepository.SaveChangesAsync(ct);
                return NoContent();

            case CompanyOwner:
                var company = await _companyRepository.GetByIdAsync(ownerId, ct);
                if (company is null) return NotFound();
                company.AgreedFee = dto.AgreedFee;
                _companyRepository.Update(company);
                await _companyRepository.SaveChangesAsync(ct);
                return NoContent();

            default:
                return UnknownOwnerType(ownerType);
        }
    }

    // POST /api/financials/{ownerType}/{ownerId}/transactions
    [HttpPost("{ownerType}/{ownerId:guid}/transactions")]
    public async Task<ActionResult<FeeTransactionDto>> AddFeeTransaction(string ownerType, Guid ownerId, AddFeeTransactionDto dto, CancellationToken ct)
    {
        var transaction = new FeeTransaction
        {
            Amount = dto.Amount,
            TransactionDate = dto.TransactionDate,
            ReceiptNumber = dto.ReceiptNumber,
            Notes = dto.Notes
        };

        switch (ownerType.ToLowerInvariant())
        {
            case CaseOwner:
                if (await _caseRepository.GetByIdAsync(ownerId, ct) is null) return NotFound();
                transaction.CaseId = ownerId;
                await _caseRepository.AddFeeTransactionAsync(transaction, ct);
                await _caseRepository.SaveChangesAsync(ct);
                break;

            case CompanyOwner:
                if (await _companyRepository.GetByIdAsync(ownerId, ct) is null) return NotFound();
                transaction.CompanyId = ownerId;
                await _companyRepository.AddFeeTransactionAsync(transaction, ct);
                await _companyRepository.SaveChangesAsync(ct);
                break;

            default:
                return UnknownOwnerType(ownerType);
        }

        return Ok(FinancialLedgerCalculator.ToTransactionDto(transaction));
    }

    // DELETE /api/financials/{ownerType}/{ownerId}/transactions/{transactionId}
    [HttpDelete("{ownerType}/{ownerId:guid}/transactions/{transactionId:guid}")]
    public async Task<ActionResult> DeleteFeeTransaction(string ownerType, Guid ownerId, Guid transactionId, CancellationToken ct)
    {
        switch (ownerType.ToLowerInvariant())
        {
            case CaseOwner:
                var caseTransaction = await _caseRepository.GetFeeTransactionByIdAsync(transactionId, ct);
                if (caseTransaction is null || caseTransaction.CaseId != ownerId) return NotFound();
                _caseRepository.SoftDeleteFeeTransaction(caseTransaction);
                await _caseRepository.SaveChangesAsync(ct);
                return NoContent();

            case CompanyOwner:
                var companyTransaction = await _companyRepository.GetFeeTransactionByIdAsync(transactionId, ct);
                if (companyTransaction is null || companyTransaction.CompanyId != ownerId) return NotFound();
                _companyRepository.SoftDeleteFeeTransaction(companyTransaction);
                await _companyRepository.SaveChangesAsync(ct);
                return NoContent();

            default:
                return UnknownOwnerType(ownerType);
        }
    }

    private BadRequestObjectResult UnknownOwnerType(string ownerType) =>
        BadRequest($"Unknown financials owner type '{ownerType}'. Expected '{CaseOwner}' or '{CompanyOwner}'.");
}
