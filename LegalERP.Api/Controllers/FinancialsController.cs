using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Cases;
using LegalERP.Application.Companies;
using LegalERP.Application.Financials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
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

        var dto = FinancialLedgerCalculator.BuildSummary(cases, companies);
        return Ok(dto);
    }
}
