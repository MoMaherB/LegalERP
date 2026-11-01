using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Backups;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LegalERP.Infrastructure.Services;

public class RcloneProcessRunner : IRcloneRunner
{
    private readonly IConfiguration _config;
    private readonly ILogger<RcloneProcessRunner>? _logger;
    private string? _cachedExecutablePath;

    public RcloneProcessRunner(IConfiguration config, ILogger<RcloneProcessRunner>? logger = null)
    {
        _config = config;
        _logger = logger;
    }

    public string? ResolveExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_cachedExecutablePath) && File.Exists(_cachedExecutablePath))
        {
            return _cachedExecutablePath;
        }

        // 1. Explicit configuration or env var
        var explicitPath = _config["Rclone:ExecutablePath"] ?? Environment.GetEnvironmentVariable("RCLONE_EXECUTABLE_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            _cachedExecutablePath = explicitPath;
            return _cachedExecutablePath;
        }

        // 2. Windows specific candidate paths and PATH probing
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Probe PATH first
            var pathCandidate = FindExecutableOnPath("rclone.exe");
            if (pathCandidate != null)
            {
                _cachedExecutablePath = pathCandidate;
                return _cachedExecutablePath;
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                // WinGet Links
                var linksPath = Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "rclone.exe");
                if (File.Exists(linksPath))
                {
                    _cachedExecutablePath = linksPath;
                    return _cachedExecutablePath;
                }

                // WinGet Packages
                var packagesDir = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
                if (Directory.Exists(packagesDir))
                {
                    try
                    {
                        var matches = Directory.GetFiles(packagesDir, "rclone.exe", SearchOption.AllDirectories);
                        if (matches.Length > 0)
                        {
                            _cachedExecutablePath = matches[0];
                            return _cachedExecutablePath;
                        }
                    }
                    catch
                    {
                        // Ignore scan exceptions
                    }
                }
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                var pfRclone = Path.Combine(programFiles, "Rclone", "rclone.exe");
                if (File.Exists(pfRclone))
                {
                    _cachedExecutablePath = pfRclone;
                    return _cachedExecutablePath;
                }
            }

            // Fallback to system command name
            _cachedExecutablePath = "rclone.exe";
            return _cachedExecutablePath;
        }

        // 3. Linux candidate paths and PATH probing
        var linuxPathCandidate = FindExecutableOnPath("rclone");
        if (linuxPathCandidate != null)
        {
            _cachedExecutablePath = linuxPathCandidate;
            return _cachedExecutablePath;
        }

        var linuxPaths = new[] { "/usr/bin/rclone", "/usr/local/bin/rclone" };
        foreach (var p in linuxPaths)
        {
            if (File.Exists(p))
            {
                _cachedExecutablePath = p;
                return _cachedExecutablePath;
            }
        }

        // Fallback to system command name
        _cachedExecutablePath = "rclone";
        return _cachedExecutablePath;
    }

    private static string? FindExecutableOnPath(string executableName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVar)) return null;

        foreach (var rawDir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var dir = rawDir.Trim();
            if (string.IsNullOrWhiteSpace(dir)) continue;

            try
            {
                var candidate = Path.Combine(dir, executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore illegal path characters in PATH environment variable
            }
        }

        return null;
    }

    private const string StatsArguments = "--tpslimit 4 --transfers 2 --checkers 4 --stats 1s --stats-one-line --stats-log-level NOTICE";

    public async Task<RcloneExecutionResult> SyncDirectoryAsync(
        string localDirectoryPath,
        string remoteDestination,
        CancellationToken ct = default,
        Action<BackupProgressSnapshotDto>? onProgress = null)
    {
        var args = $"sync \"{localDirectoryPath}\" \"{remoteDestination}\" {StatsArguments}";
        return await ExecuteProcessAsync(args, ct, onProgress);
    }

    public async Task<RcloneExecutionResult> CopyFileAsync(
        string localFilePath,
        string remoteDestinationDirectory,
        CancellationToken ct = default,
        Action<BackupProgressSnapshotDto>? onProgress = null)
    {
        var args = $"copy \"{localFilePath}\" \"{remoteDestinationDirectory}\" {StatsArguments}";
        return await ExecuteProcessAsync(args, ct, onProgress);
    }

    private static void HandleOutputLine(
        string? line,
        StringBuilder buffer,
        Action<BackupProgressSnapshotDto>? onProgress)
    {
        if (line == null) return;
        lock (buffer)
        {
            buffer.AppendLine(line);
        }

        if (onProgress != null && RcloneStatsParser.TryParseStatsLine(line, out var snapshot) && snapshot != null)
        {
            onProgress(snapshot);
        }
    }

    private async Task<RcloneExecutionResult> ExecuteProcessAsync(
        string arguments,
        CancellationToken ct,
        Action<BackupProgressSnapshotDto>? onProgress = null)
    {
        var exePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return new RcloneExecutionResult(false, -1, string.Empty, "rclone executable could not be resolved.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            process.OutputDataReceived += (_, e) => HandleOutputLine(e.Data, stdOut, onProgress);
            process.ErrorDataReceived += (_, e) => HandleOutputLine(e.Data, stdErr, onProgress);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore process kill failure
                }
                throw;
            }

            var exitCode = process.ExitCode;
            var success = exitCode == 0;

            if (!success)
            {
                _logger?.LogWarning("rclone exited with code {ExitCode}. Arguments: {Args}. Error: {StdErr}",
                    exitCode, arguments, stdErr.ToString());
            }

            return new RcloneExecutionResult(
                Success: success,
                ExitCode: exitCode,
                StandardOutput: stdOut.ToString().Trim(),
                StandardError: stdErr.ToString().Trim());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute rclone with arguments {Args}", arguments);
            return new RcloneExecutionResult(
                Success: false,
                ExitCode: -1,
                StandardOutput: string.Empty,
                StandardError: ex.Message);
        }
    }
}
