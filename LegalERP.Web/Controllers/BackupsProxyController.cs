using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LegalERP.Web.Controllers;

[ApiController]
[Route("api/admin/backups")]
[Authorize(Roles = "SuperAdmin")]
public class BackupsProxyController : ControllerBase
{
    private readonly HttpClient _http;

    public BackupsProxyController(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("LegalErpApi");
    }

    [HttpGet("download-snapshot")]
    public async Task<IActionResult> DownloadSnapshot()
    {
        try
        {
            var response = await _http.GetAsync("api/backups/download-snapshot");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode);

            var stream = await response.Content.ReadAsStreamAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar 
                           ?? response.Content.Headers.ContentDisposition?.FileName 
                           ?? $"legalerp_snapshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dump";
            return File(stream, contentType, fileName.Trim('"'));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}
