using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Backups;
using LegalERP.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Backups;

public class BackupsControllerTests
{
    [Fact]
    public async Task ExportArchive_WhenBackupAlreadyRunning_ReturnsConflictWithStartResult()
    {
        var mockArchiveService = new Mock<IBackupArchiveService>();
        var mockCoordinator = new Mock<IBackupJobCoordinator>();
        var activeId = Guid.NewGuid();
        var startResult = new BackupStartResultDto(false, activeId, "A backup job is already running.");

        mockCoordinator
            .Setup(c => c.StartArchiveBackupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(startResult);

        var controller = new BackupsController(mockArchiveService.Object, mockCoordinator.Object);

        var actionResult = await controller.ExportArchive(CancellationToken.None);

        var conflictResult = actionResult.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.Value.Should().BeEquivalentTo(startResult);
    }

    [Fact]
    public async Task ExportArchive_WhenIdle_ReturnsOkWithStartResult()
    {
        var mockArchiveService = new Mock<IBackupArchiveService>();
        var mockCoordinator = new Mock<IBackupJobCoordinator>();
        var recordId = Guid.NewGuid();
        var startResult = new BackupStartResultDto(true, recordId, "Archive backup started in background.");

        mockCoordinator
            .Setup(c => c.StartArchiveBackupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(startResult);

        var controller = new BackupsController(mockArchiveService.Object, mockCoordinator.Object);

        var actionResult = await controller.ExportArchive(CancellationToken.None);

        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(startResult);
    }

    [Fact]
    public async Task GetActiveBackup_ReturnsOkWithActiveBackupStatus()
    {
        var mockArchiveService = new Mock<IBackupArchiveService>();
        var mockCoordinator = new Mock<IBackupJobCoordinator>();
        var recordId = Guid.NewGuid();
        var activeStatus = new ActiveBackupStatusDto(
            IsRunning: true,
            BackupId: recordId,
            BackupType: BackupType.GoogleDriveArchive,
            StartedAt: DateTime.UtcNow);

        mockCoordinator
            .Setup(c => c.GetActiveBackupStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeStatus);

        var controller = new BackupsController(mockArchiveService.Object, mockCoordinator.Object);

        var actionResult = await controller.GetActiveBackup(CancellationToken.None);

        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(activeStatus);
    }

    [Fact]
    public async Task CancelActiveBackup_DelegatesToCoordinatorAndReturnsOk()
    {
        var mockArchiveService = new Mock<IBackupArchiveService>();
        var mockCoordinator = new Mock<IBackupJobCoordinator>();
        var recordId = Guid.NewGuid();
        var cancelResult = new BackupCancelResultDto(true, recordId, "Backup job cancelled.");

        mockCoordinator
            .Setup(c => c.CancelActiveBackupAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cancelResult);

        var controller = new BackupsController(mockArchiveService.Object, mockCoordinator.Object);

        var actionResult = await controller.CancelActiveBackup(CancellationToken.None);

        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(cancelResult);
    }
}
