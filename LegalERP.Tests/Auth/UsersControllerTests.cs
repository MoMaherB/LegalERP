using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Auth;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Auth;

public class UsersControllerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _mockUserManager = IdentityMockHelpers.CreateMockUserManager();
        _controller = new UsersController(_mockUserManager.Object);
    }

    [Fact]
    public void UsersController_ShouldRequireSuperAdminRole()
    {
        var authAttr = typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Roles.Should().Be("SuperAdmin");
    }

    [Fact]
    public async Task Update_WhenSuperAdminAttemptsToChangeOwnRole_ReturnsBadRequest()
    {
        var currentUserId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = currentUserId,
            FullName = "المدير العام",
            Email = "super@test.com",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(currentUserId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(["SuperAdmin"]);
        _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(currentUserId.ToString());

        var updateDto = new UpdateUserDto("المدير العام", "super@test.com", "Admin", true);

        var result = await _controller.Update(currentUserId, updateDto);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.ToString().Should().Contain("لا يمكنك تغيير صفتك");
        _mockUserManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenDemotingLastActiveSuperAdmin_ReturnsBadRequest()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var targetUser = new ApplicationUser
        {
            Id = targetUserId,
            FullName = "سوبر أدمن المستهدف",
            Email = "target@test.com",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(targetUserId.ToString())).ReturnsAsync(targetUser);
        _mockUserManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(["SuperAdmin"]);
        _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(currentUserId.ToString());
        _mockUserManager.Setup(m => m.GetUsersInRoleAsync("SuperAdmin")).ReturnsAsync([targetUser]);

        var updateDto = new UpdateUserDto("سوبر أدمن المستهدف", "target@test.com", "Editor", true);

        var result = await _controller.Update(targetUserId, updateDto);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.ToString().Should().Contain("لا يمكن تغيير صفة المدير العام الوحيد النشط");
        _mockUserManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenAnotherActiveSuperAdminExists_AllowsRoleChange()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var targetUser = new ApplicationUser
        {
            Id = targetUserId,
            FullName = "سوبر أدمن ثانوي",
            Email = "secondary@test.com",
            IsActive = true
        };

        var otherSuperAdmin = new ApplicationUser
        {
            Id = currentUserId,
            FullName = "سوبر أدمن أول",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(targetUserId.ToString())).ReturnsAsync(targetUser);
        _mockUserManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(["SuperAdmin"]);
        _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(currentUserId.ToString());
        _mockUserManager.Setup(m => m.GetUsersInRoleAsync("SuperAdmin")).ReturnsAsync([targetUser, otherSuperAdmin]);
        _mockUserManager.Setup(m => m.UpdateAsync(targetUser)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(m => m.RemoveFromRolesAsync(targetUser, It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(m => m.AddToRoleAsync(targetUser, "Editor")).ReturnsAsync(IdentityResult.Success);

        var updateDto = new UpdateUserDto("سوبر أدمن ثانوي", "secondary@test.com", "Editor", true);

        var result = await _controller.Update(targetUserId, updateDto);

        result.Should().BeOfType<OkObjectResult>();
        _mockUserManager.Verify(m => m.UpdateAsync(targetUser), Times.Once);
        _mockUserManager.Verify(m => m.AddToRoleAsync(targetUser, "Editor"), Times.Once);
    }

    [Fact]
    public async Task Deactivate_WhenSuperAdminAttemptsToDeactivateThemselves_ReturnsBadRequest()
    {
        var currentUserId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = currentUserId,
            FullName = "أنا السوبر أدمن",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(currentUserId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(currentUserId.ToString());

        var result = await _controller.Deactivate(currentUserId);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.ToString().Should().Contain("لا يمكنك تعطيل حسابك الشخصي");
        user.IsActive.Should().BeTrue();
        _mockUserManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Deactivate_WhenTargetIsLastActiveSuperAdmin_ReturnsBadRequest()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var targetUser = new ApplicationUser
        {
            Id = targetUserId,
            FullName = "السوبر أدمن الوحيد",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(targetUserId.ToString())).ReturnsAsync(targetUser);
        _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(currentUserId.ToString());
        _mockUserManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(["SuperAdmin"]);
        _mockUserManager.Setup(m => m.GetUsersInRoleAsync("SuperAdmin")).ReturnsAsync([targetUser]);

        var result = await _controller.Deactivate(targetUserId);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.ToString().Should().Contain("لا يمكن تعطيل حساب المدير العام الوحيد النشط");
        targetUser.IsActive.Should().BeTrue();
        _mockUserManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Deactivate_WhenAnotherActiveSuperAdminExists_SetsIsActiveFalseAndUpdates()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var targetUser = new ApplicationUser
        {
            Id = targetUserId,
            FullName = "السوبر أدمن الثاني",
            IsActive = true
        };

        var otherSuperAdmin = new ApplicationUser
        {
            Id = currentUserId,
            FullName = "السوبر أدمن الأول",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(targetUserId.ToString())).ReturnsAsync(targetUser);
        _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(currentUserId.ToString());
        _mockUserManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(["SuperAdmin"]);
        _mockUserManager.Setup(m => m.GetUsersInRoleAsync("SuperAdmin")).ReturnsAsync([targetUser, otherSuperAdmin]);
        _mockUserManager.Setup(m => m.UpdateAsync(targetUser)).ReturnsAsync(IdentityResult.Success);

        var result = await _controller.Deactivate(targetUserId);

        result.Should().BeOfType<OkObjectResult>();
        targetUser.IsActive.Should().BeFalse();
        _mockUserManager.Verify(m => m.UpdateAsync(targetUser), Times.Once);
    }

    [Fact]
    public async Task DeletePermanent_WhenSuperAdminAttemptsToDeleteThemselves_ReturnsBadRequest()
    {
        var currentUserId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = currentUserId,
            FullName = "أنا السوبر أدمن",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(currentUserId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(currentUserId.ToString());

        var result = await _controller.DeletePermanent(currentUserId);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.ToString().Should().Contain("لا يمكنك حذف حسابك الشخصي");
        _mockUserManager.Verify(m => m.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task DeletePermanent_WhenTargetIsLastActiveSuperAdmin_ReturnsBadRequest()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var targetUser = new ApplicationUser
        {
            Id = targetUserId,
            FullName = "السوبر أدمن الوحيد",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(targetUserId.ToString())).ReturnsAsync(targetUser);
        _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(currentUserId.ToString());
        _mockUserManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(["SuperAdmin"]);
        _mockUserManager.Setup(m => m.GetUsersInRoleAsync("SuperAdmin")).ReturnsAsync([targetUser]);

        var result = await _controller.DeletePermanent(targetUserId);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.ToString().Should().Contain("لا يمكن حذف حساب المدير العام الوحيد النشط");
        _mockUserManager.Verify(m => m.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task DeletePermanent_WhenAnotherActiveSuperAdminExists_DeletesUser()
    {
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var targetUser = new ApplicationUser
        {
            Id = targetUserId,
            FullName = "السوبر أدمن المحذوف",
            IsActive = true
        };

        var otherSuperAdmin = new ApplicationUser
        {
            Id = currentUserId,
            FullName = "السوبر أدمن الباقي",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(targetUserId.ToString())).ReturnsAsync(targetUser);
        _mockUserManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(currentUserId.ToString());
        _mockUserManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(["SuperAdmin"]);
        _mockUserManager.Setup(m => m.GetUsersInRoleAsync("SuperAdmin")).ReturnsAsync([targetUser, otherSuperAdmin]);
        _mockUserManager.Setup(m => m.DeleteAsync(targetUser)).ReturnsAsync(IdentityResult.Success);

        var result = await _controller.DeletePermanent(targetUserId);

        result.Should().BeOfType<OkObjectResult>();
        _mockUserManager.Verify(m => m.DeleteAsync(targetUser), Times.Once);
    }

    [Fact]
    public async Task Reactivate_WhenUserExists_SetsIsActiveTrueAndUpdates()
    {
        var targetUserId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = targetUserId,
            FullName = "مستخدم معطل سابقاً",
            IsActive = false
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(targetUserId.ToString())).ReturnsAsync(user);
        _mockUserManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _controller.Reactivate(targetUserId);

        result.Should().BeOfType<OkObjectResult>();
        user.IsActive.Should().BeTrue();
        _mockUserManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }
}
