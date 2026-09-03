using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace LegalERP.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly HttpClient _http;

    public DocumentsController(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("LegalErpApi");
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        try
        {
            var response = await _http.GetAsync($"api/documents/{id}/download");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode);

            var stream = await response.Content.ReadAsStreamAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar 
                           ?? response.Content.Headers.ContentDisposition?.FileName 
                           ?? "document";
            return File(stream, contentType, fileName.Trim('"'));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("{id:guid}/view")]
    public async Task<IActionResult> View(Guid id)
    {
        try
        {
            var response = await _http.GetAsync($"api/documents/{id}/view");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode);

            var stream = await response.Content.ReadAsStreamAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("download")]
    public async Task<IActionResult> DownloadByPath([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return BadRequest("Path is required.");

        try
        {
            var response = await _http.GetAsync($"api/documents/download?path={Uri.EscapeDataString(path)}");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode);

            var stream = await response.Content.ReadAsStreamAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar 
                           ?? response.Content.Headers.ContentDisposition?.FileName 
                           ?? Path.GetFileName(path);
            return File(stream, contentType, fileName.Trim('"'));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}
