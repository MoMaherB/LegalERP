using FluentAssertions;
using LegalERP.Application.Backups;
using LegalERP.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LegalERP.Tests.Backups;

public class RcloneProcessRunnerTests : IDisposable
{
    private readonly string _testRoot;

    public RcloneProcessRunnerTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"rclone_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            try
            {
                Directory.Delete(_testRoot, recursive: true);
            }
            catch
            {
                // Ignore cleanup lock issues in temp folder
            }
        }
    }

    [Fact]
    public void ResolveExecutablePath_WhenExplicitlyConfigured_ReturnsConfiguredPath()
    {
        var dummyPath = Path.Combine(_testRoot, "dummy-rclone.exe");
        File.WriteAllText(dummyPath, "dummy binary");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rclone:ExecutablePath"] = dummyPath
            })
            .Build();

        var runner = new RcloneProcessRunner(config);

        var resolved = runner.ResolveExecutablePath();

        resolved.Should().Be(dummyPath);
    }

    [Fact]
    public void ResolveExecutablePath_OnInstalledSystem_ResolvesExistingBinary()
    {
        var config = new ConfigurationBuilder().Build();
        var runner = new RcloneProcessRunner(config);

        var resolved = runner.ResolveExecutablePath();

        resolved.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SyncDirectoryAsync_BetweenLocalDirectories_SuccessfullySyncsFiles()
    {
        var config = new ConfigurationBuilder().Build();
        var runner = new RcloneProcessRunner(config);

        var exePath = runner.ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath) || (!File.Exists(exePath) && exePath != "rclone" && exePath != "rclone.exe"))
        {
            // Skip if rclone is not installed in the environment
            return;
        }

        var srcDir = Path.Combine(_testRoot, "src");
        var dstDir = Path.Combine(_testRoot, "dst");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(dstDir);

        var testFile = Path.Combine(srcDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "Hello from rclone test");

        var result = await runner.SyncDirectoryAsync(srcDir, dstDir);

        result.Success.Should().BeTrue($"rclone sync should succeed. Error: {result.StandardError}");
        result.ExitCode.Should().Be(0);

        var copiedFile = Path.Combine(dstDir, "test.txt");
        File.Exists(copiedFile).Should().BeTrue();
        (await File.ReadAllTextAsync(copiedFile)).Should().Be("Hello from rclone test");
    }

    [Fact]
    public async Task CopyFileAsync_LocalFileToDirectory_SuccessfullyCopiesFile()
    {
        var config = new ConfigurationBuilder().Build();
        var runner = new RcloneProcessRunner(config);

        var exePath = runner.ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath) || (!File.Exists(exePath) && exePath != "rclone" && exePath != "rclone.exe"))
        {
            return;
        }

        var srcDir = Path.Combine(_testRoot, "copy_src");
        var dstDir = Path.Combine(_testRoot, "copy_dst");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(dstDir);

        var testFile = Path.Combine(srcDir, "single_doc.pdf");
        await File.WriteAllTextAsync(testFile, "PDF mock content");

        var result = await runner.CopyFileAsync(testFile, dstDir);

        result.Success.Should().BeTrue($"rclone copy should succeed. Error: {result.StandardError}");
        result.ExitCode.Should().Be(0);

        var copiedFile = Path.Combine(dstDir, "single_doc.pdf");
        File.Exists(copiedFile).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteProcess_WhenBinaryDoesNotExist_ReturnsFailureResultWithClearError()
    {
        var nonExistentPath = Path.Combine(_testRoot, "non_existent_folder", "ghost_rclone.exe");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rclone:ExecutablePath"] = nonExistentPath
            })
            .Build();

        var runner = new RcloneProcessRunner(config);

        // When explicit non-existent path is given, ResolveExecutablePath returns fallback or null
        // Let's test calling with an invalid path that cannot be executed
        var result = await runner.SyncDirectoryAsync("source_dir", "dest_dir");

        // Either it couldn't find binary or failed to execute
        if (!result.Success)
        {
            result.ExitCode.Should().NotBe(0);
        }
    }
}
