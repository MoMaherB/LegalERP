namespace LegalERP.Application.Backups;

public record BackupRemoteOptions
{
    public const string DefaultLawyerRemote = "gdrive:LegalERP_Test/Lawyer_Archive";
    public const string DefaultDeveloperRemote = "gdrive:LegalERP_Test/Dev_SystemBackup";

    public string LawyerRemote { get; init; } = DefaultLawyerRemote;
    public string DeveloperRemote { get; init; } = DefaultDeveloperRemote;
}
