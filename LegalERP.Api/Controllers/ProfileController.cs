using LegalERP.Application.Auth;
using LegalERP.Application.Storage;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorageService _storage;

    public ProfileController(UserManager<ApplicationUser> userManager, IFileStorageService storage)
    {
        _userManager = userManager;
        _storage = storage;
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        if (dto.NewPassword != dto.ConfirmNewPassword)
            return BadRequest(new { message = "كلمة المرور الجديدة وتأكيدها غير متطابقتين." });

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });

        return Ok(new { message = "تم تغيير كلمة المرور بنجاح." });
    }

    [HttpPost("upload-picture")]
    public async Task<IActionResult> UploadProfilePicture(IFormFile file)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "لم يتم رفع أي ملف." });

        using var stream = file.OpenReadStream();
        var storedPath = await _storage.SaveFileAsync(stream, file.FileName, "users", user.Id);
        user.ProfilePicturePath = storedPath;
        await _userManager.UpdateAsync(user);

        return Ok(new { profilePicturePath = storedPath });
    }
}
