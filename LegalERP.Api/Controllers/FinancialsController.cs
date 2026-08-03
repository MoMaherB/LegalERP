using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Cases;
using LegalERP.Application.Companies;
using LegalERP.Application.Financials;
using LegalERP.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FinancialsController : ControllerBase
{
    private readonly ICaseRepository _caseRepository;
    private readonly ICompanyRepository _companyRepository;

    public FinancialsController(ICaseRepository caseRepository, ICompanyRepository companyRepository)
    {
        _caseRepository = caseRepository;
        _companyRepository = companyRepository;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<GlobalFinancialsDto>> GetSummary(CancellationToken ct)
    {
        var cases = await _caseRepository.GetAllAsync(ct);
        var companies = await _companyRepository.GetAllAsync(ct);

        var dto = new GlobalFinancialsDto();

        foreach (var c in cases)
        {
            var collected = c.FeeTransactions?.Sum(t => t.Amount) ?? 0;
            dto.TotalCaseAgreedFees += c.AgreedFee ?? 0;
            dto.TotalCaseCollected += collected;

            dto.Records.Add(new FinancialRecordDto
            {
                OwnerId = c.Id,
                OwnerType = "Case",
                OwnerName = c.Title,
                ReferenceNumber = c.CaseNumber,
                AgreedFee = c.AgreedFee,
                TotalCollected = collected,
                PaymentStatus = CalculateStatus(c.AgreedFee, collected)
            });
        }

        foreach (var c in companies)
        {
            var collected = c.FeeTransactions?.Sum(t => t.Amount) ?? 0;
            dto.TotalCompanyAgreedFees += c.AgreedFee ?? 0;
            dto.TotalCompanyCollected += collected;

            dto.Records.Add(new FinancialRecordDto
            {
                OwnerId = c.Id,
                OwnerType = "Company",
                OwnerName = c.CompanyName,
                ReferenceNumber = c.CommercialRegisterNumber ?? "N/A",
                AgreedFee = c.AgreedFee,
                TotalCollected = collected,
                PaymentStatus = CalculateStatus(c.AgreedFee, collected)
            });
        }
        
        dto.Records = dto.Records.OrderByDescending(r => r.AgreedFee).ToList();

        return Ok(dto);
    }

    private PaymentStatus CalculateStatus(decimal? agreed, decimal collected)
    {
        if (agreed.HasValue && agreed.Value > 0)
        {
            if (collected >= agreed.Value) return PaymentStatus.Paid;
            if (collected > 0) return PaymentStatus.PartiallyPaid;
        }
        return PaymentStatus.Unpaid;
    }
}
