using LegalERP.Application.Auth;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "SuperAdmin")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserDto(
                user.Id, user.FullName, user.Email!,
                roles.FirstOrDefault() ?? "Viewer",
                user.IsActive, user.ProfilePicturePath, user.CreatedAt));
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        var existing = await _userManager.FindByEmailAsync(dto.Email);
        if (existing != null)
            return BadRequest(new { message = "البريد الإلكتروني مستخدم بالفعل." });

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName,
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });

        await _userManager.AddToRoleAsync(user, dto.Role);
        return Ok(new { message = "تم إنشاء الحساب بنجاح." });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        var currentRoles = await _userManager.GetRolesAsync(user);
        var currentRole = currentRoles.FirstOrDefault();
        
        // Prevent changing own role
        var currentUserId = _userManager.GetUserId(User);
        if (currentUserId == id.ToString() && currentRole != dto.Role)
            return BadRequest(new { message = "لا يمكنك تغيير صفتك (Role) الخاصة بك." });

        // Prevent demoting the last active SuperAdmin
        if (currentRole == "SuperAdmin" && dto.Role != "SuperAdmin")
        {
            var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");
            if (superAdmins.Count(u => u.IsActive) <= 1 && user.IsActive)
                return BadRequest(new { message = "لا يمكن تغيير صفة المدير العام الوحيد النشط في النظام." });
        }

        user.FullName = dto.FullName;
        user.Email = dto.Email;
        user.UserName = dto.Email;
        user.IsActive = dto.IsActive;
        await _userManager.UpdateAsync(user);

        // Update role
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, dto.Role);

        return Ok(new { message = "تم تحديث الحساب بنجاح." });
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetUserPasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });

        return Ok(new { message = "تم إعادة تعيين كلمة المرور بنجاح." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        // Bug fix: prevent deactivating yourself
        var currentUserId = _userManager.GetUserId(User);
        if (currentUserId == id.ToString())
            return BadRequest(new { message = "لا يمكنك تعطيل حسابك الشخصي." });

        // Prevent deactivating the last SuperAdmin
        var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");
        var userRoles = await _userManager.GetRolesAsync(user);
        if (userRoles.Contains("SuperAdmin") && superAdmins.Count(u => u.IsActive) <= 1)
            return BadRequest(new { message = "لا يمكن تعطيل حساب المدير العام الوحيد النشط في النظام." });

        user.IsActive = false;
        await _userManager.UpdateAsync(user);
        return Ok(new { message = "تم تعطيل الحساب بنجاح." });
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        user.IsActive = true;
        await _userManager.UpdateAsync(user);
        return Ok(new { message = "تم إعادة تفعيل الحساب بنجاح." });
    }

    [HttpDelete("{id:guid}/permanent")]
    public async Task<IActionResult> DeletePermanent(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        // Prevent deleting yourself
        var currentUserId = _userManager.GetUserId(User);
        if (currentUserId == id.ToString())
            return BadRequest(new { message = "لا يمكنك حذف حسابك الشخصي." });

        // Prevent deleting the last active SuperAdmin
        var userRoles = await _userManager.GetRolesAsync(user);
        if (userRoles.Contains("SuperAdmin"))
        {
            var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");
            if (superAdmins.Count(u => u.IsActive) <= 1 && user.IsActive)
                return BadRequest(new { message = "لا يمكن حذف حساب المدير العام الوحيد النشط في النظام." });
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });

        return Ok(new { message = "تم حذف الحساب نهائياً." });
    }
}
