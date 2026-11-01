using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using LegalERP.Application.Backups;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Admin;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace LegalERP.Tests.Backups;

public class BackupManagementTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly RecordingBackupHttpMessageHandler _httpHandler = new();

    public BackupManagementTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("TestSuperAdmin");
        auth.SetRoles("SuperAdmin");

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new BackupApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void OnInitialized_WhenActiveBackupIsRunning_RendersGenerateButtonDisabled()
    {
        var activeId = Guid.NewGuid();
        _httpHandler.ActiveStatus = new ActiveBackupStatusDto(
            IsRunning: true,
            BackupId: activeId,
            BackupType: BackupType.GoogleDriveArchive,
            StartedAt: DateTime.UtcNow.AddSeconds(-15));

        var cut = RenderComponent<BackupManagement>(parameters => parameters
            .Add(p => p.PollingInterval, TimeSpan.FromSeconds(30)));

        cut.WaitForAssertion(() =>
        {
            var generateButton = cut.Find("button.ios-btn-primary");
            generateButton.HasAttribute("disabled").Should().BeTrue();
            generateButton.TextContent.Should().Contain("GeneratingArchive");
        });
    }

    [Fact]
    public async Task GenerateArchive_WhenIdle_StartsBackupAndPollsUntilCompleted()
    {
        var backupId = Guid.NewGuid();
        _httpHandler.ActiveStatus = new ActiveBackupStatusDto(false, null, null, null);
        _httpHandler.StartResult = new BackupStartResultDto(true, backupId, "Archive backup started in background.");

        var cut = RenderComponent<BackupManagement>(parameters => parameters
            .Add(p => p.PollingInterval, TimeSpan.FromMilliseconds(50)));

        var generateButton = cut.Find("button.ios-btn-primary");
        generateButton.HasAttribute("disabled").Should().BeFalse();

        // When export-archive is called, flip ActiveStatus to running first, then complete on subsequent poll
        _httpHandler.OnExportCalled = () =>
        {
            _httpHandler.ActiveStatus = new ActiveBackupStatusDto(false, null, null, null);
            _httpHandler.History =
            [
                new BackupRecordDto(
                    Id: backupId,
                    BackupType: BackupType.GoogleDriveArchive,
                    Status: BackupStatus.Completed,
                    FileCount: 16,
                    TotalSizeBytes: 13_012_345,
                    DestinationPath: "C:\\exports\\legal_archive | Rclone: gdrive:LegalERP_Test/Lawyer_Archive",
                    ErrorMessage: null,
                    CreatedAt: DateTime.UtcNow.AddSeconds(-5),
                    CompletedAt: DateTime.UtcNow)
            ];
        };

        await cut.InvokeAsync(() => generateButton.Click());

        cut.WaitForAssertion(() =>
        {
            _httpHandler.ExportCallCount.Should().Be(1);
            _mockToast.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.AtLeastOnce);
            cut.Find("button.ios-btn-primary").HasAttribute("disabled").Should().BeFalse();
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void OnInitialized_WhenActiveBackupHasProgressSnapshot_RendersProgressBarSpeedAndEtaAndHidesWhenFinished()
    {
        var activeId = Guid.NewGuid();
        _httpHandler.ActiveStatus = new ActiveBackupStatusDto(
            IsRunning: true,
            BackupId: activeId,
            BackupType: BackupType.GoogleDriveArchive,
            StartedAt: DateTime.UtcNow.AddSeconds(-10),
            Progress: new BackupProgressSnapshotDto(
                Percentage: 45,
                TransferredBytes: 5_872_025,
                TotalBytes: 13_012_828,
                TransferredText: "5.600 MiB",
                TotalText: "12.410 MiB",
                SpeedText: "1.120 MiB/s",
                EtaText: "6s"));

        var cut = RenderComponent<BackupManagement>(parameters => parameters
            .Add(p => p.PollingInterval, TimeSpan.FromMilliseconds(50)));

        cut.WaitForAssertion(() =>
        {
            var progressWidget = cut.Find("[data-testid='backup-progress-widget']");
            progressWidget.TextContent.Should().Contain("45%");
            progressWidget.TextContent.Should().Contain("5.600 MiB / 12.410 MiB");
            progressWidget.TextContent.Should().Contain("1.120 MiB/s");
            progressWidget.TextContent.Should().Contain("6s");
        });

        // Simulate job finishing on next poll tick
        _httpHandler.ActiveStatus = new ActiveBackupStatusDto(false, null, null, null);

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("[data-testid='backup-progress-widget']").Should().BeEmpty();
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CancelBackup_WhenRunningAndConfirmed_CallsCancelEndpointReenablesButtonAndRendersCancelledBadgeInHistory()
    {
        var activeId = Guid.NewGuid();
        _httpHandler.ActiveStatus = new ActiveBackupStatusDto(
            IsRunning: true,
            BackupId: activeId,
            BackupType: BackupType.GoogleDriveArchive,
            StartedAt: DateTime.UtcNow.AddSeconds(-8));

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        _httpHandler.OnCancelCalled = () =>
        {
            _httpHandler.ActiveStatus = new ActiveBackupStatusDto(false, null, null, null);
            _httpHandler.CancelResult = new BackupCancelResultDto(true, activeId, "Backup job cancelled.");
            _httpHandler.History =
            [
                new BackupRecordDto(
                    Id: activeId,
                    BackupType: BackupType.GoogleDriveArchive,
                    Status: BackupStatus.Cancelled,
                    FileCount: 6,
                    TotalSizeBytes: 1_730_000,
                    DestinationPath: "gdrive:LegalERP_Test/Lawyer_Archive",
                    ErrorMessage: null,
                    CreatedAt: DateTime.UtcNow.AddSeconds(-8),
                    CompletedAt: DateTime.UtcNow)
            ];
        };

        var cut = RenderComponent<BackupManagement>(parameters => parameters
            .Add(p => p.PollingInterval, TimeSpan.FromSeconds(30)));

        var cancelButton = cut.WaitForElement("[data-testid='cancel-backup-btn']");
        await cut.InvokeAsync(() => cancelButton.Click());

        cut.WaitForAssertion(() =>
        {
            _httpHandler.CancelCallCount.Should().Be(1);
            _mockToast.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.AtLeastOnce);
            cut.Find("button.ios-btn-primary").HasAttribute("disabled").Should().BeFalse();
            cut.FindAll("[data-testid='cancel-backup-btn']").Should().BeEmpty();
            cut.Find("table.ios-table").TextContent.Should().Contain("Cancelled");
        });
    }

    [Fact]
    public async Task CancelBackup_WhenDismissedInConfirmDialog_DoesNotCallCancelEndpointAndRemainsRunning()
    {
        var activeId = Guid.NewGuid();
        _httpHandler.ActiveStatus = new ActiveBackupStatusDto(
            IsRunning: true,
            BackupId: activeId,
            BackupType: BackupType.GoogleDriveArchive,
            StartedAt: DateTime.UtcNow.AddSeconds(-8));

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(false);

        var cut = RenderComponent<BackupManagement>(parameters => parameters
            .Add(p => p.PollingInterval, TimeSpan.FromSeconds(30)));

        var cancelButton = cut.WaitForElement("[data-testid='cancel-backup-btn']");
        await cut.InvokeAsync(() => cancelButton.Click());

        _httpHandler.CancelCallCount.Should().Be(0);
        cut.Find("button.ios-btn-primary").HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("[data-testid='cancel-backup-btn']").Should().ContainSingle();
    }

    private sealed class RecordingBackupHttpMessageHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public BackupStatusSummaryDto Summary { get; set; } = new(
            LastArchiveDate: DateTime.UtcNow.AddDays(-1),
            LastArchiveFileCount: 10,
            LastArchiveSizeBytes: 1024,
            LastDatabaseSnapshotDate: null,
            LastDatabaseSnapshotSizeBytes: 0,
            TotalBackupsRun: 1);

        public List<BackupRecordDto> History { get; set; } = [];

        public ActiveBackupStatusDto ActiveStatus { get; set; } = new(false, null, null, null);

        public BackupStartResultDto StartResult { get; set; } = new(true, Guid.NewGuid(), "Started");

        public BackupCancelResultDto CancelResult { get; set; } = new(true, Guid.NewGuid(), "Cancelled");

        public HttpStatusCode StartStatusCode { get; set; } = HttpStatusCode.OK;

        public int ExportCallCount { get; private set; }

        public int CancelCallCount { get; private set; }

        public Action? OnExportCalled { get; set; }

        public Action? OnCancelCalled { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.EndsWith("/api/backups/summary", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, Summary));
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/api/backups/active", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, ActiveStatus));
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/api/backups", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, History));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/api/backups/export-archive", StringComparison.OrdinalIgnoreCase))
            {
                ExportCallCount++;
                OnExportCalled?.Invoke();
                return Task.FromResult(JsonResponse(StartStatusCode, StartResult));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/api/backups/cancel", StringComparison.OrdinalIgnoreCase))
            {
                CancelCallCount++;
                OnCancelCalled?.Invoke();
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, CancelResult));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T payload)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            };
        }
    }
}
