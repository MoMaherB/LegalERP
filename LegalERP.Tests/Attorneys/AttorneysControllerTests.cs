using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Attorneys;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Attorneys;

public class AttorneysControllerTests
{
    private readonly Mock<IAttorneyRepository> _mockRepo = new();
    private readonly AttorneysController _controller;

    public AttorneysControllerTests()
    {
        _controller = new AttorneysController(_mockRepo.Object);
    }

    [Fact]
    public void AttorneysController_ShouldHaveAuthorizeAttribute()
    {
        var authAttr = typeof(AttorneysController).GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull("AttorneysController must require authentication");
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("AddClient")]
    [InlineData("RemoveClient")]
    public void MutatingEndpoints_ShouldRequireSuperAdminAdminOrEditorRoles(string methodName)
    {
        var method = typeof(AttorneysController).GetMethod(methodName);
        method.Should().NotBeNull($"Method '{methodName}' must exist on AttorneysController");
        var authAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull($"Method '{methodName}' must have [Authorize]");
        authAttr!.Roles.Should().Be("SuperAdmin,Admin,Editor");
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithMappedAttorneySummaries()
    {
        var attorneyId = Guid.NewGuid();
        var attorneys = new List<Attorney>
        {
            new()
            {
                Id = attorneyId,
                AttorneyNumber = "ATT-2026-01",
                AttorneyClients =
                [
                    new AttorneyClient { Id = Guid.NewGuid(), AttorneyId = attorneyId, ClientId = Guid.NewGuid() },
                    new AttorneyClient { Id = Guid.NewGuid(), AttorneyId = attorneyId, ClientId = Guid.NewGuid() }
                ]
            }
        };

        _mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(attorneys);

        var result = await _controller.GetAll();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<AttorneySummaryDto>>().Subject.ToList();
        dtos.Should().HaveCount(1);
        dtos[0].Id.Should().Be(attorneyId);
        dtos[0].AttorneyNumber.Should().Be("ATT-2026-01");
        dtos[0].ClientCount.Should().Be(2);
    }

    [Fact]
    public async Task Search_DelegatesToRepositoryAndReturnsMappedSummaries()
    {
        var attorneyId = Guid.NewGuid();
        var attorneys = new List<Attorney>
        {
            new()
            {
                Id = attorneyId,
                AttorneyNumber = "ATT-SEARCH-99",
                AttorneyClients = []
            }
        };

        _mockRepo.Setup(r => r.SearchAsync("SEARCH", It.IsAny<CancellationToken>())).ReturnsAsync(attorneys);

        var result = await _controller.Search("SEARCH");

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<AttorneySummaryDto>>().Subject.ToList();
        dtos.Should().HaveCount(1);
        dtos[0].AttorneyNumber.Should().Be("ATT-SEARCH-99");
        dtos[0].ClientCount.Should().Be(0);
        _mockRepo.Verify(r => r.SearchAsync("SEARCH", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsFullAttorneyDtoWithDocumentAndClients()
    {
        var attorneyId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var linkId = Guid.NewGuid();

        var attorney = new Attorney
        {
            Id = attorneyId,
            AttorneyNumber = "ATT-FULL-1",
            Notes = "ملاحظات توكيل عام",
            DocumentId = docId,
            Document = new Document
            {
                Id = docId,
                FileName = "scan.pdf",
                StoredFileName = "stored-scan.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 2048
            },
            AttorneyClients =
            [
                new AttorneyClient
                {
                    Id = linkId,
                    AttorneyId = attorneyId,
                    ClientId = clientId,
                    Client = new Client
                    {
                        Id = clientId,
                        FullName = "سعيد محمود",
                        NationalIdNumber = "29010101234567",
                        FileNumber = "CL-888"
                    }
                }
            ]
        };

        _mockRepo.Setup(r => r.GetByIdAsync(attorneyId, It.IsAny<CancellationToken>())).ReturnsAsync(attorney);

        var result = await _controller.GetById(attorneyId);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<AttorneyDto>().Subject;
        dto.Id.Should().Be(attorneyId);
        dto.AttorneyNumber.Should().Be("ATT-FULL-1");
        dto.Notes.Should().Be("ملاحظات توكيل عام");
        dto.Document.Should().NotBeNull();
        dto.Document!.Id.Should().Be(docId);
        dto.Document.FileName.Should().Be("scan.pdf");
        dto.Clients.Should().HaveCount(1);
        dto.Clients[0].Id.Should().Be(linkId);
        dto.Clients[0].ClientId.Should().Be(clientId);
        dto.Clients[0].ClientFullName.Should().Be("سعيد محمود");
        dto.Clients[0].ClientNationalId.Should().Be("29010101234567");
        dto.Clients[0].ClientFileNumber.Should().Be("CL-888");
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var attorneyId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(attorneyId, It.IsAny<CancellationToken>())).ReturnsAsync((Attorney?)null);

        var result = await _controller.GetById(attorneyId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetByClientId_ReturnsMappedClientAttorneyDtosWithDocument()
    {
        var clientId = Guid.NewGuid();
        var attorneyId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        var attorneys = new List<Attorney>
        {
            new()
            {
                Id = attorneyId,
                AttorneyNumber = "ATT-BY-CLIENT",
                DocumentId = docId,
                Document = new Document
                {
                    Id = docId,
                    FileName = "doc.pdf",
                    StoredFileName = "stored-doc.pdf",
                    ContentType = "application/pdf",
                    FileSizeBytes = 1024
                }
            }
        };

        _mockRepo.Setup(r => r.GetByClientIdAsync(clientId, It.IsAny<CancellationToken>())).ReturnsAsync(attorneys);

        var result = await _controller.GetByClientId(clientId);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ClientAttorneyDto>>().Subject.ToList();
        dtos.Should().HaveCount(1);
        dtos[0].AttorneyId.Should().Be(attorneyId);
        dtos[0].AttorneyNumber.Should().Be("ATT-BY-CLIENT");
        dtos[0].Document.Should().NotBeNull();
        dtos[0].Document!.Id.Should().Be(docId);
        dtos[0].Document!.FileName.Should().Be("doc.pdf");
    }

    [Fact]
    public async Task Create_ValidDto_AddsAttorneySavesAndReturnsCreatedAtAction()
    {
        var docId = Guid.NewGuid();
        var dto = new CreateAttorneyDto("ATT-2026-NEW", "ملاحظات جديدة", docId);

        Attorney? savedAttorney = null;
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<Attorney>(), It.IsAny<CancellationToken>()))
            .Callback<Attorney, CancellationToken>((a, _) => savedAttorney = a)
            .Returns(Task.CompletedTask);

        var result = await _controller.Create(dto);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(AttorneysController.GetById));
        savedAttorney.Should().NotBeNull();
        savedAttorney!.AttorneyNumber.Should().Be("ATT-2026-NEW");
        savedAttorney.Notes.Should().Be("ملاحظات جديدة");
        savedAttorney.DocumentId.Should().Be(docId);

        _mockRepo.Verify(r => r.AddAsync(It.IsAny<Attorney>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenExists_UpdatesPropertiesSavesAndReturnsNoContent()
    {
        var attorneyId = Guid.NewGuid();
        var existing = new Attorney
        {
            Id = attorneyId,
            AttorneyNumber = "ATT-OLD",
            Notes = "Old notes",
            DocumentId = null,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var newDocId = Guid.NewGuid();
        var updateDto = new UpdateAttorneyDto("ATT-UPDATED", "New notes", newDocId);

        _mockRepo.Setup(r => r.GetByIdAsync(attorneyId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await _controller.Update(attorneyId, updateDto);

        result.Should().BeOfType<NoContentResult>();
        existing.AttorneyNumber.Should().Be("ATT-UPDATED");
        existing.Notes.Should().Be("New notes");
        existing.DocumentId.Should().Be(newDocId);
        existing.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _mockRepo.Verify(r => r.Update(existing), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        var attorneyId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(attorneyId, It.IsAny<CancellationToken>())).ReturnsAsync((Attorney?)null);

        var result = await _controller.Update(attorneyId, new UpdateAttorneyDto("ATT-1", null, null));

        result.Should().BeOfType<NotFoundResult>();
        _mockRepo.Verify(r => r.Update(It.IsAny<Attorney>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenExists_SoftDeletesSavesAndReturnsNoContent()
    {
        var attorneyId = Guid.NewGuid();
        var existing = new Attorney { Id = attorneyId, AttorneyNumber = "ATT-DEL" };

        _mockRepo.Setup(r => r.GetByIdAsync(attorneyId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await _controller.Delete(attorneyId);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.SoftDelete(existing), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenNotFound_ReturnsNotFound()
    {
        var attorneyId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(attorneyId, It.IsAny<CancellationToken>())).ReturnsAsync((Attorney?)null);

        var result = await _controller.Delete(attorneyId);

        result.Should().BeOfType<NotFoundResult>();
        _mockRepo.Verify(r => r.SoftDelete(It.IsAny<Attorney>()), Times.Never);
    }

    [Fact]
    public async Task AddClient_WhenClientNotAlreadyLinked_AddsLinkSavesAndReturnsOk()
    {
        var attorneyId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var attorney = new Attorney
        {
            Id = attorneyId,
            AttorneyNumber = "ATT-LINK",
            AttorneyClients = []
        };

        _mockRepo.Setup(r => r.GetByIdAsync(attorneyId, It.IsAny<CancellationToken>())).ReturnsAsync(attorney);
        _mockRepo.Setup(r => r.AddClientAsync(It.IsAny<AttorneyClient>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.AddClient(attorneyId, new AddAttorneyClientDto(clientId));

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        _mockRepo.Verify(r => r.AddClientAsync(
            It.Is<AttorneyClient>(ac => ac.AttorneyId == attorneyId && ac.ClientId == clientId),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddClient_WhenClientAlreadyLinked_Returns409Conflict()
    {
        var attorneyId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var attorney = new Attorney
        {
            Id = attorneyId,
            AttorneyNumber = "ATT-LINK",
            AttorneyClients =
            [
                new AttorneyClient { Id = Guid.NewGuid(), AttorneyId = attorneyId, ClientId = clientId }
            ]
        };

        _mockRepo.Setup(r => r.GetByIdAsync(attorneyId, It.IsAny<CancellationToken>())).ReturnsAsync(attorney);

        var result = await _controller.AddClient(attorneyId, new AddAttorneyClientDto(clientId));

        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(409);
        _mockRepo.Verify(r => r.AddClientAsync(It.IsAny<AttorneyClient>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddClient_WhenAttorneyNotFound_ReturnsNotFound()
    {
        var attorneyId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(attorneyId, It.IsAny<CancellationToken>())).ReturnsAsync((Attorney?)null);

        var result = await _controller.AddClient(attorneyId, new AddAttorneyClientDto(Guid.NewGuid()));

        result.Should().BeOfType<NotFoundResult>();
        _mockRepo.Verify(r => r.AddClientAsync(It.IsAny<AttorneyClient>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveClient_WhenLinkExistsAndMatchesAttorney_SoftDeletesLinkSavesAndReturnsNoContent()
    {
        var attorneyId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var link = new AttorneyClient
        {
            Id = linkId,
            AttorneyId = attorneyId,
            ClientId = Guid.NewGuid()
        };

        _mockRepo.Setup(r => r.GetAttorneyClientByIdAsync(linkId, It.IsAny<CancellationToken>())).ReturnsAsync(link);

        var result = await _controller.RemoveClient(attorneyId, linkId);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.SoftDeleteAttorneyClient(link), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveClient_WhenLinkNotFound_ReturnsNotFound()
    {
        var attorneyId = Guid.NewGuid();
        var linkId = Guid.NewGuid();

        _mockRepo.Setup(r => r.GetAttorneyClientByIdAsync(linkId, It.IsAny<CancellationToken>())).ReturnsAsync((AttorneyClient?)null);

        var result = await _controller.RemoveClient(attorneyId, linkId);

        result.Should().BeOfType<NotFoundResult>();
        _mockRepo.Verify(r => r.SoftDeleteAttorneyClient(It.IsAny<AttorneyClient>()), Times.Never);
    }

    [Fact]
    public async Task RemoveClient_WhenLinkBelongsToDifferentAttorney_ReturnsNotFound()
    {
        var attorneyId = Guid.NewGuid();
        var differentAttorneyId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var link = new AttorneyClient
        {
            Id = linkId,
            AttorneyId = differentAttorneyId,
            ClientId = Guid.NewGuid()
        };

        _mockRepo.Setup(r => r.GetAttorneyClientByIdAsync(linkId, It.IsAny<CancellationToken>())).ReturnsAsync(link);

        var result = await _controller.RemoveClient(attorneyId, linkId);

        result.Should().BeOfType<NotFoundResult>();
        _mockRepo.Verify(r => r.SoftDeleteAttorneyClient(It.IsAny<AttorneyClient>()), Times.Never);
    }
}
