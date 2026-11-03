using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Clients;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Clients;

public class ClientsControllerTests
{
    private readonly Mock<IClientRepository> _mockRepo = new();
    private readonly ClientsController _controller;

    public ClientsControllerTests()
    {
        _controller = new ClientsController(_mockRepo.Object);
    }

    [Fact]
    public void ClientsController_ShouldHaveAuthorizeAttribute()
    {
        var authAttr = typeof(ClientsController).GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull("ClientsController must require authentication");
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Delete")]
    public void MutatingEndpoints_ShouldRequireSuperAdminAdminOrEditorRoles(string methodName)
    {
        var method = typeof(ClientsController).GetMethod(methodName);
        method.Should().NotBeNull();
        var authAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Roles.Should().Be("SuperAdmin,Admin,Editor");
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithMappedSummaries()
    {
        var clients = new List<Client>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FileNumber = "CL-001",
                FullName = "أحمد محمد",
                NationalIdNumber = "29001011234567",
                PhoneNumber = "01001234567"
            }
        };

        _mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(clients);

        var result = await _controller.GetAll();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var summaries = okResult.Value.Should().BeAssignableTo<IEnumerable<ClientSummaryDto>>().Subject.ToList();
        summaries.Should().HaveCount(1);
        summaries[0].FullName.Should().Be("أحمد محمد");
        summaries[0].FileNumber.Should().Be("CL-001");
        summaries[0].NationalIdNumber.Should().Be("29001011234567");
    }

    [Fact]
    public async Task Search_DelegatesToRepositoryAndReturnsMappedSummaries()
    {
        var clients = new List<Client>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FullName = "محمود عيسى",
                NationalIdNumber = "29505051234567"
            }
        };

        _mockRepo.Setup(r => r.SearchAsync("محمود", It.IsAny<CancellationToken>())).ReturnsAsync(clients);

        var result = await _controller.Search("محمود");

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var summaries = okResult.Value.Should().BeAssignableTo<IEnumerable<ClientSummaryDto>>().Subject.ToList();
        summaries.Should().HaveCount(1);
        summaries[0].FullName.Should().Be("محمود عيسى");
        _mockRepo.Verify(r => r.SearchAsync("محمود", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_WhenClientNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Client?)null);

        var result = await _controller.GetById(id);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_WhenClientExists_ReturnsFullClientDtoWith360CasesAndCompanies()
    {
        var clientId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        var nationalIdDoc = new Document
        {
            Id = docId,
            FileName = "id_card.pdf",
            StoredFileName = "stored_id_card.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 2048
        };

        var client = new Client
        {
            Id = clientId,
            FileNumber = "CL-777",
            FullName = "سعيد الألفي",
            FullNameEn = "Saeed El Alfy",
            NationalIdNumber = "28801011234567",
            PhoneNumber = "01122334455",
            Email = "saeed@example.com",
            Address = "القاهرة، مصر",
            Notes = "عميل مميز",
            NationalIdDocument = nationalIdDoc,
            CaseParties = new List<CaseParty>
            {
                new()
                {
                    CaseId = caseId,
                    Case = new Case
                    {
                        Id = caseId,
                        CaseNumber = "55/2026",
                        Title = "دعوى تعويض مدني",
                        CaseType = CaseType.Civil,
                        Status = CaseStatus.Active,
                        Outcome = null
                    },
                    Role = PartyRole.Defendant,
                    IsOurClient = true
                }
            },
            CompanyPartnerships = new List<CompanyPartner>
            {
                new()
                {
                    CompanyId = companyId,
                    Company = new Company
                    {
                        Id = companyId,
                        CompanyName = "شركة النور للمقاولات"
                    },
                    OwnershipPercentage = 40m
                }
            }
        };

        _mockRepo.Setup(r => r.GetByIdAsync(clientId, It.IsAny<CancellationToken>())).ReturnsAsync(client);

        var result = await _controller.GetById(clientId);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<ClientDto>().Subject;

        dto.Id.Should().Be(clientId);
        dto.FullName.Should().Be("سعيد الألفي");
        dto.FullNameEn.Should().Be("Saeed El Alfy");
        dto.FileNumber.Should().Be("CL-777");
        dto.NationalIdNumber.Should().Be("28801011234567");
        dto.NationalIdDocument.Should().NotBeNull();
        dto.NationalIdDocument!.FileName.Should().Be("id_card.pdf");

        dto.RelatedCases.Should().HaveCount(1);
        var relatedCase = dto.RelatedCases[0];
        relatedCase.CaseId.Should().Be(caseId);
        relatedCase.CaseNumber.Should().Be("55/2026");
        relatedCase.CaseTitle.Should().Be("دعوى تعويض مدني");
        relatedCase.CaseType.Should().Be(CaseType.Civil);
        relatedCase.PartyRole.Should().Be(PartyRole.Defendant);
        relatedCase.IsOurClient.Should().BeTrue();

        dto.RelatedCompanies.Should().HaveCount(1);
        var relatedCompany = dto.RelatedCompanies[0];
        relatedCompany.CompanyId.Should().Be(companyId);
        relatedCompany.CompanyName.Should().Be("شركة النور للمقاولات");
        relatedCompany.OwnershipPercentage.Should().Be(40m);
    }

    [Fact]
    public async Task Create_AddsClientAndReturnsCreatedAtAction()
    {
        var docId = Guid.NewGuid();
        var createDto = new CreateClientDto(
            "CL-100",
            "طارق عبد الله",
            "Tarek Abdullah",
            "29202021234567",
            "01234567890",
            "tarek@example.com",
            "الإسكندرية",
            "ملاحظات",
            docId
        );

        var result = await _controller.Create(createDto);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(ClientsController.GetById));

        var summary = createdResult.Value.Should().BeOfType<ClientSummaryDto>().Subject;
        summary.FullName.Should().Be("طارق عبد الله");
        summary.FileNumber.Should().Be("CL-100");

        _mockRepo.Verify(r => r.AddAsync(It.Is<Client>(c =>
            c.FullName == "طارق عبد الله" &&
            c.NationalIdNumber == "29202021234567" &&
            c.NationalIdDocumentId == docId), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenClientNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var updateDto = new UpdateClientDto("CL-101", "اسم جديد", null, "123", null, null, null, null, null);
        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Client?)null);

        var result = await _controller.Update(id, updateDto);

        result.Should().BeOfType<NotFoundResult>();
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenClientExists_UpdatesFieldsAndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var existing = new Client
        {
            Id = id,
            FullName = "اسم قديم",
            NationalIdNumber = "11111111111111"
        };

        var updateDto = new UpdateClientDto(
            "CL-200",
            "اسم معدل",
            "Updated Name",
            "29999999999999",
            "01555555555",
            "updated@example.com",
            "الجيزة",
            "ملاحظات معدلة",
            null
        );

        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await _controller.Update(id, updateDto);

        result.Should().BeOfType<NoContentResult>();
        existing.FullName.Should().Be("اسم معدل");
        existing.FileNumber.Should().Be("CL-200");
        existing.FullNameEn.Should().Be("Updated Name");
        existing.PhoneNumber.Should().Be("01555555555");
        existing.Email.Should().Be("updated@example.com");
        existing.Address.Should().Be("الجيزة");
        existing.Notes.Should().Be("ملاحظات معدلة");

        _mockRepo.Verify(r => r.Update(existing), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenClientNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Client?)null);

        var result = await _controller.Delete(id);

        result.Should().BeOfType<NotFoundResult>();
        _mockRepo.Verify(r => r.SoftDelete(It.IsAny<Client>()), Times.Never);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenClientExists_CallsSoftDeleteAndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var client = new Client { Id = id, FullName = "عميل محذوف" };
        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(client);

        var result = await _controller.Delete(id);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.SoftDelete(client), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
