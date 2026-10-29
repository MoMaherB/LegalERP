using System;
using System.Collections.Generic;
using System.IO;
using LegalERP.Application.Backups;
using Microsoft.Extensions.Configuration;

namespace LegalERP.Infrastructure.Services;

public static class BackupRemoteOptionsResolver
{
    public static BackupRemoteOptions Resolve(IConfiguration? configuration = null, string? envFilePath = null)
    {
        var envValues = LoadEnvFile(envFilePath);

        var lawyerRemote = ResolveValue(
            configuration,
            envValues,
            configKey: "Backup:RcloneLawyerRemote",
            envKey: "RCLONE_LAWYER_REMOTE",
            defaultFallback: BackupRemoteOptions.DefaultLawyerRemote);

        var devRemote = ResolveValue(
            configuration,
            envValues,
            configKey: "Backup:RcloneDevRemote",
            envKey: "RCLONE_DEV_REMOTE",
            defaultFallback: BackupRemoteOptions.DefaultDeveloperRemote);

        return new BackupRemoteOptions
        {
            LawyerRemote = lawyerRemote,
            DeveloperRemote = devRemote
        };
    }

    private static string ResolveValue(
        IConfiguration? configuration,
        Dictionary<string, string> envValues,
        string configKey,
        string envKey,
        string defaultFallback)
    {
        string? val = configuration?[configKey]
            ?? Environment.GetEnvironmentVariable(envKey)
            ?? (envValues.TryGetValue(envKey, out var envVal) ? envVal : null);

        return string.IsNullOrWhiteSpace(val) ? defaultFallback : val.Trim();
    }

    private static Dictionary<string, string> LoadEnvFile(string? specificPath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var path = specificPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = FindSolutionRootEnvFile();
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return result;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

            var equalsIdx = line.IndexOf('=');
            if (equalsIdx <= 0) continue;

            var key = line[..equalsIdx].Trim();
            var val = line[(equalsIdx + 1)..].Trim();

            // Strip single or double enclosing quotes
            if ((val.StartsWith('"') && val.EndsWith('"')) || (val.StartsWith('\'') && val.EndsWith('\'')))
            {
                if (val.Length >= 2)
                {
                    val = val[1..^1];
                }
            }

            result[key] = val;
        }

        return result;
    }

    private static string? FindSolutionRootEnvFile()
    {
        // Check current directory first
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        var envPath = SearchUpwardForEnv(current);
        if (envPath != null) return envPath;

        // Check base directory of running application
        var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
        return SearchUpwardForEnv(baseDir);
    }

    private static string? SearchUpwardForEnv(DirectoryInfo? startDir)
    {
        var current = startDir;
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            // Stop if reached git root or solution file parent
            if (File.Exists(Path.Combine(current.FullName, "LegalERP.sln")))
            {
                // If .env is in solution root, we already checked candidate
                break;
            }

            current = current.Parent;
        }

        return null;
    }
}
