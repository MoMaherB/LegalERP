using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Auth;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Auth;

public class AuthControllerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockUserManager = IdentityMockHelpers.CreateMockUserManager();
        _mockSignInManager = IdentityMockHelpers.CreateMockSignInManager(_mockUserManager.Object);
        _controller = new AuthController(_mockSignInManager.Object, _mockUserManager.Object);
    }

    [Fact]
    public async Task Login_WhenUserNotFound_ReturnsUnauthorized()
    {
        _mockUserManager.Setup(m => m.FindByEmailAsync("notfound@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Login(new LoginDto("notfound@example.com", "Password123!", false));

        result.Should().BeOfType<UnauthorizedObjectResult>();
        _mockSignInManager.Verify(s => s.PasswordSignInAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Login_WhenUserIsDeactivated_ReturnsUnauthorizedAndBlocksSignIn()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "inactive@example.com",
            FullName = "مستخدم معطل",
            IsActive = false
        };

        _mockUserManager.Setup(m => m.FindByEmailAsync("inactive@example.com"))
            .ReturnsAsync(user);

        var result = await _controller.Login(new LoginDto("inactive@example.com", "Password123!", false));

        result.Should().BeOfType<UnauthorizedObjectResult>();
        _mockSignInManager.Verify(s => s.PasswordSignInAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Login_WhenPasswordIncorrect_ReturnsUnauthorized()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "active@example.com",
            FullName = "مستخدم نشط",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByEmailAsync("active@example.com"))
            .ReturnsAsync(user);

        _mockSignInManager.Setup(s => s.PasswordSignInAsync(user, "WrongPassword", false, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var result = await _controller.Login(new LoginDto("active@example.com", "WrongPassword", false));

        result.Should().BeOfType<UnauthorizedObjectResult>();
        _mockUserManager.Verify(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Login_WhenActiveAndValid_ReturnsOkWithRoleClaims()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "admin@example.com",
            FullName = "أدمن النظام",
            IsActive = true,
            ProfilePicturePath = "admin-avatar.jpg"
        };

        _mockUserManager.Setup(m => m.FindByEmailAsync("admin@example.com"))
            .ReturnsAsync(user);

        _mockSignInManager.Setup(s => s.PasswordSignInAsync(user, "CorrectPass123!", false, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        _mockUserManager.Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(["Admin"]);

        var result = await _controller.Login(new LoginDto("admin@example.com", "CorrectPass123!", false));

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CurrentUserDto>().Subject;
        dto.Id.Should().Be(userId);
        dto.FullName.Should().Be("أدمن النظام");
        dto.Email.Should().Be("admin@example.com");
        dto.Role.Should().Be("Admin");
        dto.ProfilePicturePath.Should().Be("admin-avatar.jpg");
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ReturnsCurrentUserDtoWithRole()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "me@example.com",
            FullName = "أنا المستخدم",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);
        _mockUserManager.Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(["Editor"]);

        var result = await _controller.GetCurrentUser();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CurrentUserDto>().Subject;
        dto.Id.Should().Be(userId);
        dto.FullName.Should().Be("أنا المستخدم");
        dto.Role.Should().Be("Editor");
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserNotFound_ReturnsUnauthorized()
    {
        _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.GetCurrentUser();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Logout_CallsSignOutAndReturnsOk()
    {
        _mockSignInManager.Setup(s => s.SignOutAsync())
            .Returns(Task.CompletedTask);

        var result = await _controller.Logout();

        result.Should().BeOfType<OkResult>();
        _mockSignInManager.Verify(s => s.SignOutAsync(), Times.Once);
    }
}
