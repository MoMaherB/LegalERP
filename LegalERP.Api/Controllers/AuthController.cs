using LegalERP.Application.Auth;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        Console.WriteLine($"[AUTH DEBUG] Attempting login for {dto.Email}");
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            Console.WriteLine($"[AUTH DEBUG] User not found!");
            return Unauthorized(new { message = "بيانات الدخول غير صحيحة أو الحساب معطّل." });
        }
        
        Console.WriteLine($"[AUTH DEBUG] User found. IsActive: {user.IsActive}");
        if (!user.IsActive)
        {
            Console.WriteLine($"[AUTH DEBUG] User is inactive!");
            return Unauthorized(new { message = "بيانات الدخول غير صحيحة أو الحساب معطّل." });
        }

        var result = await _signInManager.PasswordSignInAsync(user, dto.Password, dto.RememberMe, lockoutOnFailure: false);
        Console.WriteLine($"[AUTH DEBUG] SignIn result: {result.Succeeded} (IsLockedOut: {result.IsLockedOut}, IsNotAllowed: {result.IsNotAllowed})");
        
        if (!result.Succeeded)
            return Unauthorized(new { message = "البريد الإلكتروني أو كلمة المرور غير صحيحة." });

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new CurrentUserDto(
            user.Id,
            user.FullName,
            user.Email!,
            roles.FirstOrDefault() ?? "Viewer",
            user.ProfilePicturePath
        ));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new CurrentUserDto(
            user.Id,
            user.FullName,
            user.Email!,
            roles.FirstOrDefault() ?? "Viewer",
            user.ProfilePicturePath
        ));
    }
}
