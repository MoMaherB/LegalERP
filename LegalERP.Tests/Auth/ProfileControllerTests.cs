using System;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Auth;
using LegalERP.Application.Common;
using LegalERP.Application.Storage;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Auth;

public class ProfileControllerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IFileStorageService> _mockStorage = new();
    private readonly ProfileController _controller;

    public ProfileControllerTests()
    {
        _mockUserManager = IdentityMockHelpers.CreateMockUserManager();
        _controller = new ProfileController(_mockUserManager.Object, _mockStorage.Object);
    }

    [Fact]
    public void ProfileController_ShouldHaveAuthorizeAttribute()
    {
        var authAttr = typeof(ProfileController).GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull("ProfileController must require authentication");
    }

    [Fact]
    public async Task ChangePassword_WhenNewPasswordDoesNotMatchConfirmation_ReturnsBadRequest()
    {
        var dto = new ChangePasswordDto("Current123!", "NewPass123!", "DifferentConfirm123!");

        var result = await _controller.ChangePassword(dto);

        result.Should().BeOfType<BadRequestObjectResult>();
        _mockUserManager.Verify(m => m.ChangePasswordAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_WhenUserNotFound_ReturnsUnauthorized()
    {
        _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var dto = new ChangePasswordDto("Current123!", "NewPass123!", "NewPass123!");

        var result = await _controller.ChangePassword(dto);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ChangePassword_WhenChangeFails_ReturnsBadRequestWithErrors()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user@test.com" };
        _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);

        _mockUserManager.Setup(m => m.ChangePasswordAsync(user, "WrongCurrent!", "NewPass123!"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Incorrect password." }));

        var dto = new ChangePasswordDto("WrongCurrent!", "NewPass123!", "NewPass123!");

        var result = await _controller.ChangePassword(dto);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.ToString().Should().Contain("Incorrect password.");
    }

    [Fact]
    public async Task ChangePassword_WhenValid_ReturnsOkWithMessage()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user@test.com" };
        _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);

        _mockUserManager.Setup(m => m.ChangePasswordAsync(user, "Current123!", "NewPass123!"))
            .ReturnsAsync(IdentityResult.Success);

        var dto = new ChangePasswordDto("Current123!", "NewPass123!", "NewPass123!");

        var result = await _controller.ChangePassword(dto);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UploadProfilePicture_WhenNoFileProvided_ReturnsBadRequest()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "user@test.com" };
        _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);

        var result = await _controller.UploadProfilePicture(null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadProfilePicture_WhenUserNotFound_ReturnsUnauthorized()
    {
        _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(100);

        var result = await _controller.UploadProfilePicture(fileMock.Object);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UploadProfilePicture_WhenNoExistingPicture_SavesWithUsersOwnerTypeAndUpdatesUser()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "user@test.com",
            ProfilePicturePath = null
        };

        _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);
        _mockUserManager.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var content = "dummy image data";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(stream.Length);
        fileMock.Setup(f => f.FileName).Returns("my-avatar.png");
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

        _mockStorage.Setup(s => s.SaveFileAsync(It.IsAny<Stream>(), "my-avatar.png", DocumentOwnerTypes.Users, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("saved-avatar.png");

        var result = await _controller.UploadProfilePicture(fileMock.Object);

        result.Should().BeOfType<OkObjectResult>();
        user.ProfilePicturePath.Should().Be("saved-avatar.png");

        // Old file must NOT be deleted since there was none
        _mockStorage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // Saved with DocumentOwnerTypes.Users
        _mockStorage.Verify(s => s.SaveFileAsync(It.IsAny<Stream>(), "my-avatar.png", DocumentOwnerTypes.Users, userId, It.IsAny<CancellationToken>()), Times.Once);

        _mockUserManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UploadProfilePicture_WhenExistingPictureExists_DeletesOldFileViaStorageServiceAndSavesNew()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "user@test.com",
            ProfilePicturePath = "superseded-avatar.jpg"
        };

        _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);
        _mockUserManager.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var content = "new image bytes";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(stream.Length);
        fileMock.Setup(f => f.FileName).Returns("new-avatar.png");
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

        _mockStorage.Setup(s => s.SaveFileAsync(It.IsAny<Stream>(), "new-avatar.png", DocumentOwnerTypes.Users, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-stored-avatar.png");

        var result = await _controller.UploadProfilePicture(fileMock.Object);

        result.Should().BeOfType<OkObjectResult>();
        user.ProfilePicturePath.Should().Be("new-stored-avatar.png");

        // Verifies previous profile picture was deleted with DocumentOwnerTypes.Users
        _mockStorage.Verify(s => s.DeleteFileAsync(DocumentOwnerTypes.Users, userId, "superseded-avatar.jpg", It.IsAny<CancellationToken>()), Times.Once);

        // Verifies new file was saved
        _mockStorage.Verify(s => s.SaveFileAsync(It.IsAny<Stream>(), "new-avatar.png", DocumentOwnerTypes.Users, userId, It.IsAny<CancellationToken>()), Times.Once);

        _mockUserManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UploadProfilePicture_WhenUpdateAsyncFails_CleansUpNewlySavedFileAndReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "user@test.com",
            ProfilePicturePath = "existing.jpg"
        };

        _mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(user);
        _mockUserManager.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Concurrency failure" }));

        var content = "new bytes";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(stream.Length);
        fileMock.Setup(f => f.FileName).Returns("failed-update.png");
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);

        _mockStorage.Setup(s => s.SaveFileAsync(It.IsAny<Stream>(), "failed-update.png", DocumentOwnerTypes.Users, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("failed-saved.png");

        var result = await _controller.UploadProfilePicture(fileMock.Object);

        result.Should().BeOfType<BadRequestObjectResult>();

        // Newly saved file deleted on failure
        _mockStorage.Verify(s => s.DeleteFileAsync(DocumentOwnerTypes.Users, userId, "failed-saved.png", It.IsAny<CancellationToken>()), Times.Once);

        // Existing file preserved
        _mockStorage.Verify(s => s.DeleteFileAsync(DocumentOwnerTypes.Users, userId, "existing.jpg", It.IsAny<CancellationToken>()), Times.Never);
    }
}
