using FluentAssertions;
using LegalERP.Application.Backups;
using LegalERP.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LegalERP.Tests.Backups;

public class BackupRemoteOptionsResolverTests : IDisposable
{
    private readonly string _tempEnvFile;

    public BackupRemoteOptionsResolverTests()
    {
        _tempEnvFile = Path.Combine(Path.GetTempPath(), $"test_env_{Guid.NewGuid():N}.env");
    }

    public void Dispose()
    {
        if (File.Exists(_tempEnvFile))
        {
            File.Delete(_tempEnvFile);
        }
    }

    [Fact]
    public void Resolve_WhenNoConfigAndNoEnvFile_ReturnsDefaultRemotes()
    {
        var config = new ConfigurationBuilder().Build();

        var options = BackupRemoteOptionsResolver.Resolve(config, envFilePath: "non_existent_file.env");

        options.LawyerRemote.Should().Be(BackupRemoteOptions.DefaultLawyerRemote);
        options.DeveloperRemote.Should().Be(BackupRemoteOptions.DefaultDeveloperRemote);
    }

    [Fact]
    public void Resolve_WhenConfigurationProvidesValues_UsesConfiguration()
    {
        var initialData = new Dictionary<string, string?>
        {
            ["Backup:RcloneLawyerRemote"] = "gdrive:CustomLawyerRemote",
            ["Backup:RcloneDevRemote"] = "gdrive:CustomDevRemote"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(initialData)
            .Build();

        var options = BackupRemoteOptionsResolver.Resolve(config, envFilePath: "non_existent_file.env");

        options.LawyerRemote.Should().Be("gdrive:CustomLawyerRemote");
        options.DeveloperRemote.Should().Be("gdrive:CustomDevRemote");
    }

    [Fact]
    public void Resolve_WhenEnvFileHasSingleAndDoubleQuotedValues_TrimsQuotesAndLoadsRemotes()
    {
        File.WriteAllLines(_tempEnvFile, new[]
        {
            "# Some comment",
            "RCLONE_LAWYER_REMOTE='gdrive:LegalERP_Test/Lawyer_Archive'",
            "RCLONE_DEV_REMOTE=\"gdrive:LegalERP_Test/Dev_SystemBackup\""
        });

        var config = new ConfigurationBuilder().Build();

        var options = BackupRemoteOptionsResolver.Resolve(config, envFilePath: _tempEnvFile);

        options.LawyerRemote.Should().Be("gdrive:LegalERP_Test/Lawyer_Archive");
        options.DeveloperRemote.Should().Be("gdrive:LegalERP_Test/Dev_SystemBackup");
    }

    [Fact]
    public void Resolve_WhenConfigurationOverridesEnvFile_ConfigurationTakesPrecedence()
    {
        File.WriteAllLines(_tempEnvFile, new[]
        {
            "RCLONE_LAWYER_REMOTE='gdrive:FromEnv/Lawyer'",
            "RCLONE_DEV_REMOTE='gdrive:FromEnv/Dev'"
        });

        var initialData = new Dictionary<string, string?>
        {
            ["Backup:RcloneLawyerRemote"] = "gdrive:FromConfig/Lawyer"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(initialData)
            .Build();

        var options = BackupRemoteOptionsResolver.Resolve(config, envFilePath: _tempEnvFile);

        options.LawyerRemote.Should().Be("gdrive:FromConfig/Lawyer");
        options.DeveloperRemote.Should().Be("gdrive:FromEnv/Dev");
    }
}
