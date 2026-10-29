using System.Threading;
using System.Threading.Tasks;

namespace LegalERP.Application.Backups;

public record RcloneExecutionResult(
    bool Success,
    int ExitCode,
    string StandardOutput,
    string StandardError);

public interface IRcloneRunner
{
    Task<RcloneExecutionResult> SyncDirectoryAsync(
        string localDirectoryPath,
        string remoteDestination,
        CancellationToken ct = default);

    Task<RcloneExecutionResult> CopyFileAsync(
        string localFilePath,
        string remoteDestinationDirectory,
        CancellationToken ct = default);
}
