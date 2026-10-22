using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Cases;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Cases;

public class CasesControllerPartyTests
{
    private readonly Mock<ICaseRepository> _mockRepo = new();
    private readonly CasesController _controller;

    public CasesControllerPartyTests()
    {
        _controller = new CasesController(_mockRepo.Object);
    }

    [Fact]
    public async Task GetById_WhenPartyIsLinkedToClient_ResolvesIdentityFieldsAndDocumentFromClient()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var clientDoc = new Document
        {
            Id = docId,
            FileName = "national-id.pdf",
            StoredFileName = "stored-id.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 2048
        };
        var client = new Client
        {
            Id = clientId,
            FullName = "أحمد محمود",
            NationalIdNumber = "29001011234567",
            PhoneNumber = "01012345678",
            NationalIdDocumentId = docId,
            NationalIdDocument = clientDoc
        };

        var caseEntity = new Case
        {
            Id = caseId,
            CaseNumber = "2026/101",
            Title = "قضية تعويض",
            CaseType = CaseType.Civil,
            Status = CaseStatus.Active,
            Parties = new List<CaseParty>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    ClientId = clientId,
                    Client = client,
                    FullName = "أحمد محمود (ID: 29001011234567)",
                    Role = PartyRole.Victim,
                    IsOurClient = true,
                    NationalIdNumber = null,
                    PhoneNumber = null,
                    DocumentId = null,
                    Document = null
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    ClientId = null,
                    Client = null,
                    FullName = "خصم يدوي",
                    Role = PartyRole.Defendant,
                    IsOurClient = false,
                    NationalIdNumber = "28505051234567",
                    PhoneNumber = "01198765432"
                }
            }
        };

        _mockRepo.Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseEntity);

        // Act
        var result = await _controller.GetById(caseId, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<CaseDto>().Subject;

        dto.Parties.Should().HaveCount(2);

        var ourClientParty = dto.Parties[0];
        ourClientParty.ClientId.Should().Be(clientId);
        ourClientParty.FullName.Should().Be("أحمد محمود");
        ourClientParty.NationalIdNumber.Should().Be("29001011234567");
        ourClientParty.PhoneNumber.Should().Be("01012345678");
        ourClientParty.DocumentId.Should().Be(docId);
        ourClientParty.Document.Should().NotBeNull();
        ourClientParty.Document!.FileName.Should().Be("national-id.pdf");

        var opponentParty = dto.Parties[1];
        opponentParty.ClientId.Should().BeNull();
        opponentParty.FullName.Should().Be("خصم يدوي");
        opponentParty.NationalIdNumber.Should().Be("28505051234567");
        opponentParty.PhoneNumber.Should().Be("01198765432");
    }

    [Fact]
    public async Task AddParty_WhenLinkedToClient_ReturnsPartyDtoWithClientFields()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        _mockRepo.Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Case { Id = caseId, CaseNumber = "2026/102", Title = "جنحة" });

        _mockRepo.Setup(r => r.GetPartyByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new CaseParty
            {
                Id = id,
                CaseId = caseId,
                ClientId = clientId,
                FullName = "سارة علي",
                Role = PartyRole.Defendant,
                IsOurClient = true,
                Client = new Client
                {
                    Id = clientId,
                    FullName = "سارة علي",
                    NationalIdNumber = "29502021234567",
                    PhoneNumber = "01234567890",
                    NationalIdDocumentId = docId,
                    NationalIdDocument = new Document
                    {
                        Id = docId,
                        FileName = "sara-id.jpg",
                        StoredFileName = "sara-id.jpg",
                        ContentType = "image/jpeg",
                        FileSizeBytes = 1024
                    }
                }
            });

        var createDto = new CreateCasePartyDto(
            ClientId: clientId,
            FullName: "سارة علي",
            Role: PartyRole.Defendant,
            IsOurClient: true,
            NationalIdNumber: null,
            PhoneNumber: null,
            Notes: "موكل رئيسي",
            DocumentId: null
        );

        // Act
        var result = await _controller.AddParty(caseId, createDto, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var partyDto = okResult.Value.Should().BeOfType<CasePartyDto>().Subject;

        partyDto.FullName.Should().Be("سارة علي");
        partyDto.NationalIdNumber.Should().Be("29502021234567");
        partyDto.PhoneNumber.Should().Be("01234567890");
        partyDto.DocumentId.Should().Be(docId);
        partyDto.Document.Should().NotBeNull();
    }
}
