using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Companies;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Reflection;
using Xunit;

namespace LegalERP.Tests.Companies;

public class CompaniesControllerAmendmentTests
{
    private readonly Mock<ICompanyRepository> _mockRepo = new();
    private readonly CompaniesController _controller;

    public CompaniesControllerAmendmentTests()
    {
        _controller = new CompaniesController(_mockRepo.Object);
    }

    [Fact]
    public async Task AddAmendment_WhenTitleIsBlank_AssignsAutoIncrementedOrdinalTitleAndSequenceNumber()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var company = new Company
        {
            Id = companyId,
            CompanyName = "Test Firm",
            Amendments = new List<CompanyAmendment>
            {
                new() { Id = Guid.NewGuid(), CompanyId = companyId, SequenceNumber = 1, Title = "عقد التعديل الأول" }
            }
        };

        _mockRepo.Setup(r => r.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var dto = new CreateCompanyAmendmentDto(
            Title: null,
            AmendmentDate: new DateOnly(2025, 4, 1),
            DocumentId: null
        );

        // Act
        var result = await _controller.AddAmendment(companyId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedDto = okResult.Value.Should().BeOfType<CompanyAmendmentDto>().Subject;

        returnedDto.SequenceNumber.Should().Be(2);
        returnedDto.Title.Should().Be("عقد التعديل الثاني");
        _mockRepo.Verify(r => r.AddAmendmentAsync(It.Is<CompanyAmendment>(a =>
            a.CompanyId == companyId &&
            a.SequenceNumber == 2 &&
            a.Title == "عقد التعديل الثاني"), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAmendment_WhenCustomTitleProvided_PreservesCustomTitle()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var company = new Company
        {
            Id = companyId,
            CompanyName = "Test Firm",
            Amendments = new List<CompanyAmendment>()
        };

        _mockRepo.Setup(r => r.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var dto = new CreateCompanyAmendmentDto(
            Title: "Custom Amendment Name",
            AmendmentDate: new DateOnly(2025, 4, 1),
            DocumentId: null
        );

        // Act
        var result = await _controller.AddAmendment(companyId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedDto = okResult.Value.Should().BeOfType<CompanyAmendmentDto>().Subject;

        returnedDto.SequenceNumber.Should().Be(1);
        returnedDto.Title.Should().Be("Custom Amendment Name");
    }

    [Fact]
    public async Task AddAmendment_WhenCompanyNotFound_ReturnsNotFound()
    {
        // Arrange
        var nonExistentCompanyId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(nonExistentCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var dto = new CreateCompanyAmendmentDto("Any Title", null, null);

        // Act
        var result = await _controller.AddAmendment(nonExistentCompanyId, dto, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteAmendment_WhenAmendmentExists_CallsSoftDeleteAndSavesChanges()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var amendmentId = Guid.NewGuid();
        var amendment = new CompanyAmendment
        {
            Id = amendmentId,
            CompanyId = companyId,
            SequenceNumber = 1,
            Title = "First"
        };

        _mockRepo.Setup(r => r.GetAmendmentByIdAsync(amendmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(amendment);

        // Act
        var result = await _controller.DeleteAmendment(companyId, amendmentId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.SoftDeleteAmendment(amendment), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TogglePartnerActive_RequiresEditorOrAdminRoleAndTogglesPartnerActiveFlag()
    {
        var method = typeof(CompaniesController).GetMethod(nameof(CompaniesController.TogglePartnerActive));
        method.Should().NotBeNull();
        var authAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Roles.Should().Be("SuperAdmin,Admin,Editor");

        var companyId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var partner = new CompanyPartner { Id = partnerId, CompanyId = companyId, FullName = "شريك 1", IsActive = true };
        var company = new Company { Id = companyId, CompanyName = "شركة النور", Partners = [partner] };

        _mockRepo.Setup(r => r.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var result = await _controller.TogglePartnerActive(companyId, partnerId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        partner.IsActive.Should().BeFalse();
        _mockRepo.Verify(r => r.Update(company), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
