using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Cases;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Cases;

public class CasesControllerMemoTests
{
    private readonly Mock<ICaseRepository> _mockRepo = new();
    private readonly CasesController _controller;

    public CasesControllerMemoTests()
    {
        _controller = new CasesController(_mockRepo.Object);
    }

    [Fact]
    public async Task AddMemo_WhenValidRequest_PersistsMemoAndReturnsCreatedMemoDto()
    {
        var caseId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Case { Id = caseId, CaseNumber = "2026/700", Title = "قضية" });

        var createDto = new CreateCaseMemoDto(
            Title: "مذكرة دفاع",
            Content: "نص المذكرة",
            MemoDate: new DateOnly(2026, 5, 1),
            DocumentId: null
        );

        var result = await _controller.AddMemo(caseId, createDto, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var memoDto = okResult.Value.Should().BeOfType<CaseMemoDto>().Subject;

        memoDto.Id.Should().NotBeEmpty();
        memoDto.Title.Should().Be("مذكرة دفاع");
        memoDto.Content.Should().Be("نص المذكرة");
        memoDto.MemoDate.Should().Be(new DateOnly(2026, 5, 1));
        _mockRepo.Verify(r => r.AddMemoAsync(It.Is<CaseMemo>(m =>
            m.CaseId == caseId &&
            m.Title == "مذكرة دفاع"), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMemo_WhenAttachingDocumentToExistingMemo_UpdatesDocumentIdAndSaves()
    {
        var caseId = Guid.NewGuid();
        var memoId = Guid.NewGuid();
        var newDocId = Guid.NewGuid();
        var existingMemo = new CaseMemo
        {
            Id = memoId,
            CaseId = caseId,
            Title = "مذكرة أولية",
            Content = "محتوى",
            MemoDate = new DateOnly(2026, 5, 1),
            DocumentId = null
        };

        _mockRepo.Setup(r => r.GetMemoByIdAsync(memoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMemo);

        var updateDto = new UpdateCaseMemoDto(
            Title: "مذكرة أولية",
            Content: "محتوى",
            MemoDate: new DateOnly(2026, 5, 1),
            DocumentId: newDocId
        );

        var result = await _controller.UpdateMemo(caseId, memoId, updateDto, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        existingMemo.DocumentId.Should().Be(newDocId);
        _mockRepo.Verify(r => r.UpdateMemo(existingMemo), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMemo_WhenCaseIdMismatch_ReturnsNotFoundWithoutDeleting()
    {
        var caseId = Guid.NewGuid();
        var otherCaseId = Guid.NewGuid();
        var memoId = Guid.NewGuid();
        var existingMemo = new CaseMemo
        {
            Id = memoId,
            CaseId = otherCaseId,
            Title = "مذكرة"
        };

        _mockRepo.Setup(r => r.GetMemoByIdAsync(memoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMemo);

        var result = await _controller.DeleteMemo(caseId, memoId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _mockRepo.Verify(r => r.SoftDeleteMemo(It.IsAny<CaseMemo>()), Times.Never);
    }
}
