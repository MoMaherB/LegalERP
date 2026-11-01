using System.Threading;
using System.Threading.Tasks;

namespace LegalERP.Application.Backups;

public interface IBackupJobCoordinator
{
    Task<BackupStartResultDto> StartArchiveBackupAsync(CancellationToken ct = default);
    Task<BackupStartResultDto> StartDeveloperBackupAsync(CancellationToken ct = default);
    Task<BackupCancelResultDto> CancelActiveBackupAsync(CancellationToken ct = default);
    Task<ActiveBackupStatusDto> GetActiveBackupStatusAsync(CancellationToken ct = default);
    Task<int> ReconcileStaleRunningBackupsAsync(CancellationToken ct = default);
}
