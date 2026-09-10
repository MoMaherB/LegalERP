using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Backups;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using LegalERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LegalERP.Infrastructure.Services;

public class BackupArchiveService : IBackupArchiveService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<BackupArchiveService> _logger;
    private readonly string _storageBasePath;

    public BackupArchiveService(ApplicationDbContext db, IConfiguration config, ILogger<BackupArchiveService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;

        // Base directory where uploads currently reside
        var configuredPath = _config["Storage:BasePath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            _storageBasePath = Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(Directory.GetCurrentDirectory(), configuredPath);
        }
        else
        {
            var apiPath = Path.Combine(Directory.GetCurrentDirectory(), "LegalERP.Api", "wwwroot", "uploads");
            if (Directory.Exists(apiPath))
            {
                _storageBasePath = apiPath;
            }
            else
            {
                _storageBasePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            }
        }
    }

    public async Task<BackupResultDto> GenerateHumanReadableArchiveAsync(string? customTargetPath = null, CancellationToken ct = default)
    {
        var targetBase = !string.IsNullOrWhiteSpace(customTargetPath)
            ? customTargetPath
            : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "exports", "legal_archive");

        var record = new BackupRecord
        {
            BackupType = BackupType.GoogleDriveArchive,
            Status = BackupStatus.Running,
            DestinationPath = targetBase,
            CreatedAt = DateTime.UtcNow
        };

        _db.BackupRecords.Add(record);
        await _db.SaveChangesAsync(ct);

        int totalFilesCopied = 0;
        long totalBytesCopied = 0;

        try
        {
            Directory.CreateDirectory(targetBase);

            // 1. Companies & Contracts
            var companies = await _db.Companies
                .Include(c => c.IncorporationDocument)
                .Include(c => c.Amendments).ThenInclude(a => a.Document)
                .ToListAsync(ct);

            var companyDocs = await _db.Documents
                .Where(d => d.OwnerType == "companydoc" && !d.IsDeleted)
                .ToListAsync(ct);

            var companiesFolder = Path.Combine(targetBase, "companies");
            Directory.CreateDirectory(companiesFolder);

            foreach (var comp in companies)
            {
                var folderName = SanitizeName(!string.IsNullOrWhiteSpace(comp.FileNumber) 
                    ? $"{comp.FileNumber} - {comp.CompanyName}" 
                    : comp.CompanyName);

                var compDir = Path.Combine(companiesFolder, folderName);
                Directory.CreateDirectory(compDir);

                // A. Incorporation Document
                if (comp.IncorporationDocument != null)
                {
                    var ext = Path.GetExtension(comp.IncorporationDocument.FileName);
                    var cleanFileName = SanitizeName($"عقد التأسيس_{comp.FileNumber ?? comp.CompanyName}") + ext;
                    if (LinkOrCopyFile("company", comp.Id, comp.IncorporationDocument.StoredFileName, compDir, cleanFileName, out var size))
                    {
                        totalFilesCopied++;
                        totalBytesCopied += size;
                    }
                }

                // B. Amendments
                if (comp.Amendments != null && comp.Amendments.Count > 0)
                {
                    var amendDir = Path.Combine(compDir, "التعديلات");
                    Directory.CreateDirectory(amendDir);

                    var sortedAmends = comp.Amendments.OrderBy(a => a.SequenceNumber).ToList();
                    foreach (var amend in sortedAmends)
                    {
                        if (amend.Document != null)
                        {
                            var ext = Path.GetExtension(amend.Document.FileName);
                            var amendName = SanitizeName($"عقد التعديل رقم {amend.SequenceNumber}") + ext;
                            if (LinkOrCopyFile("company", comp.Id, amend.Document.StoredFileName, amendDir, amendName, out var size))
                            {
                                totalFilesCopied++;
                                totalBytesCopied += size;
                            }
                        }
                    }
                }

                // C. General Company Records & Licenses
                var generalDocs = companyDocs.Where(d => d.OwnerId == comp.Id).ToList();
                if (generalDocs.Count > 0)
                {
                    var recordsDir = Path.Combine(compDir, "سجلات وتراخيص الشركة");
                    Directory.CreateDirectory(recordsDir);

                    foreach (var gdoc in generalDocs)
                    {
                        var cleanFileName = SanitizeName(Path.GetFileNameWithoutExtension(gdoc.FileName)) + Path.GetExtension(gdoc.FileName);
                        if (LinkOrCopyFile("companydoc", comp.Id, gdoc.StoredFileName, recordsDir, cleanFileName, out var size))
                        {
                            totalFilesCopied++;
                            totalBytesCopied += size;
                        }
                    }
                }
            }

            // 2. Clients & IDs
            var clients = await _db.Clients
                .Include(c => c.NationalIdDocument)
                .ToListAsync(ct);

            var clientsFolder = Path.Combine(targetBase, "clients");
            Directory.CreateDirectory(clientsFolder);

            foreach (var client in clients)
            {
                var folderName = SanitizeName(!string.IsNullOrWhiteSpace(client.FileNumber)
                    ? $"{client.FileNumber} - {client.FullName}"
                    : client.FullName);

                var clientDir = Path.Combine(clientsFolder, folderName);
                Directory.CreateDirectory(clientDir);

                if (client.NationalIdDocument != null)
                {
                    var ext = Path.GetExtension(client.NationalIdDocument.FileName);
                    var cleanFileName = SanitizeName($"صورة الهوية_{client.FileNumber ?? client.FullName}") + ext;
                    if (LinkOrCopyFile("client", client.Id, client.NationalIdDocument.StoredFileName, clientDir, cleanFileName, out var size))
                    {
                        totalFilesCopied++;
                        totalBytesCopied += size;
                    }
                }
            }

            // 3. Cases, Memos & Evidence
            var cases = await _db.Cases
                .Include(c => c.Memos).ThenInclude(m => m.Document)
                .ToListAsync(ct);

            var caseDocs = await _db.Documents
                .Where(d => d.OwnerType == "case" && !d.IsDeleted)
                .ToListAsync(ct);

            var casesFolder = Path.Combine(targetBase, "cases");
            Directory.CreateDirectory(casesFolder);

            foreach (var c in cases)
            {
                var folderName = SanitizeName(!string.IsNullOrWhiteSpace(c.CourtName)
                    ? $"قضية {c.CaseNumber} - {c.CourtName}"
                    : $"قضية {c.CaseNumber}");

                var caseDir = Path.Combine(casesFolder, folderName);
                Directory.CreateDirectory(caseDir);

                // Memos
                if (c.Memos != null && c.Memos.Count > 0)
                {
                    var memosDir = Path.Combine(caseDir, "المذكرات القانونية");
                    Directory.CreateDirectory(memosDir);

                    foreach (var memo in c.Memos)
                    {
                        if (memo.Document != null)
                        {
                            var ext = Path.GetExtension(memo.Document.FileName);
                            var memoName = SanitizeName(string.IsNullOrWhiteSpace(memo.Title) ? "مذكرة دفاع" : memo.Title) + ext;
                            if (LinkOrCopyFile("memo", memo.Id, memo.Document.StoredFileName, memosDir, memoName, out var size))
                            {
                                totalFilesCopied++;
                                totalBytesCopied += size;
                            }
                        }
                    }
                }

                // General Case Documents
                var attachedCaseDocs = caseDocs.Where(d => d.OwnerId == c.Id).ToList();
                if (attachedCaseDocs.Count > 0)
                {
                    var docsDir = Path.Combine(caseDir, "المستندات والمستخرجات");
                    Directory.CreateDirectory(docsDir);

                    foreach (var doc in attachedCaseDocs)
                    {
                        var cleanFileName = SanitizeName(Path.GetFileNameWithoutExtension(doc.FileName)) + Path.GetExtension(doc.FileName);
                        if (LinkOrCopyFile("case", c.Id, doc.StoredFileName, docsDir, cleanFileName, out var size))
                        {
                            totalFilesCopied++;
                            totalBytesCopied += size;
                        }
                    }
                }
            }

            // 4. Powers of Attorney (التوكيلات)
            var attorneys = await _db.Attorneys
                .Include(a => a.Document)
                .ToListAsync(ct);

            var attFolder = Path.Combine(targetBase, "attornies");
            Directory.CreateDirectory(attFolder);

            foreach (var att in attorneys)
            {
                if (att.Document != null)
                {
                    var ext = Path.GetExtension(att.Document.FileName);
                    var cleanFileName = SanitizeName($"توكيل_{att.AttorneyNumber}") + ext;
                    if (LinkOrCopyFile("attorney", att.Id, att.Document.StoredFileName, attFolder, cleanFileName, out var size))
                    {
                        totalFilesCopied++;
                        totalBytesCopied += size;
                    }
                }
            }

            // Mark completed
            record.Status = BackupStatus.Completed;
            record.FileCount = totalFilesCopied;
            record.TotalSizeBytes = totalBytesCopied;
            record.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return new BackupResultDto(true, "Legal archive generated successfully.", totalFilesCopied, totalBytesCopied, targetBase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate human-readable archive.");
            record.Status = BackupStatus.Failed;
            record.ErrorMessage = ex.Message;
            record.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return new BackupResultDto(false, $"Failed to generate archive: {ex.Message}", totalFilesCopied, totalBytesCopied, null);
        }
    }

    public async Task<(Stream FileStream, string FileName, long FileSizeBytes)> CreateDatabaseSnapshotAsync(CancellationToken ct = default)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"legalerp_db_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dump");
        var pgDumpPath = FindPgDumpExecutable();

        if (string.IsNullOrWhiteSpace(pgDumpPath) || !File.Exists(pgDumpPath))
        {
            throw new InvalidOperationException("pg_dump executable not found on the system. Please ensure PostgreSQL client tools are installed.");
        }

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connString);

        var startInfo = new ProcessStartInfo
        {
            FileName = pgDumpPath,
            Arguments = $"-h {builder.Host} -p {builder.Port} -U {builder.Username} -d {builder.Database} -F c -f \"{tempFile}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrEmpty(builder.Password))
        {
            startInfo.EnvironmentVariables["PGPASSWORD"] = builder.Password;
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new Exception($"pg_dump failed (exit code {process.ExitCode}): {error}");
        }

        var fileInfo = new FileInfo(tempFile);
        var fileStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);

        var record = new BackupRecord
        {
            BackupType = BackupType.DatabaseSnapshot,
            Status = BackupStatus.Completed,
            FileCount = 1,
            TotalSizeBytes = fileInfo.Length,
            DestinationPath = "Direct Stream",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        _db.BackupRecords.Add(record);
        await _db.SaveChangesAsync(ct);

        return (fileStream, Path.GetFileName(tempFile), fileInfo.Length);
    }

    public async Task<List<BackupRecordDto>> GetBackupHistoryAsync(int take = 20, CancellationToken ct = default)
    {
        return await _db.BackupRecords
            .OrderByDescending(b => b.CreatedAt)
            .Take(take)
            .Select(b => new BackupRecordDto(
                b.Id,
                b.BackupType,
                b.Status,
                b.FileCount,
                b.TotalSizeBytes,
                b.DestinationPath,
                b.ErrorMessage,
                b.CreatedAt,
                b.CompletedAt
            ))
            .ToListAsync(ct);
    }

    public async Task<BackupStatusSummaryDto> GetStatusSummaryAsync(CancellationToken ct = default)
    {
        var lastArchive = await _db.BackupRecords
            .Where(b => b.BackupType == BackupType.GoogleDriveArchive && b.Status == BackupStatus.Completed)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var lastDb = await _db.BackupRecords
            .Where(b => b.BackupType == BackupType.DatabaseSnapshot && b.Status == BackupStatus.Completed)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var totalRuns = await _db.BackupRecords.CountAsync(ct);

        return new BackupStatusSummaryDto(
            lastArchive?.CompletedAt ?? lastArchive?.CreatedAt,
            lastArchive?.FileCount ?? 0,
            lastArchive?.TotalSizeBytes ?? 0,
            lastDb?.CompletedAt ?? lastDb?.CreatedAt,
            lastDb?.TotalSizeBytes ?? 0,
            totalRuns
        );
    }

    // ── Helper Methods ────────────────────────────────────────────────────────

    private bool LinkOrCopyFile(string ownerType, Guid ownerId, string storedFileName, string targetDir, string desiredFileName, out long fileSize)
    {
        fileSize = 0;
        var sourcePath = Path.Combine(_storageBasePath, ownerType.ToLower(), ownerId.ToString(), storedFileName);

        if (!File.Exists(sourcePath))
        {
            // Fallback check in flat uploads
            sourcePath = Path.Combine(_storageBasePath, storedFileName);
            if (!File.Exists(sourcePath)) return false;
        }

        var fileInfo = new FileInfo(sourcePath);
        fileSize = fileInfo.Length;

        // Resolve duplicate names in target directory
        var targetPath = GetUniqueFilePath(targetDir, desiredFileName);

        try
        {
            // Try hardlink first (takes 0 extra disk space)
            if (File.Exists(targetPath)) File.Delete(targetPath);
            CreateHardLinkSafe(targetPath, sourcePath);
            return true;
        }
        catch
        {
            // Fallback to copy if hardlinks fail across volume boundaries
            try
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy file from {Source} to {Target}", sourcePath, targetPath);
                return false;
            }
        }
    }

    private static string GetUniqueFilePath(string directory, string fileName)
    {
        var targetPath = Path.Combine(directory, fileName);
        if (!File.Exists(targetPath)) return targetPath;

        var nameOnly = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        int counter = 2;

        while (File.Exists(targetPath))
        {
            targetPath = Path.Combine(directory, $"{nameOnly} ({counter}){ext}");
            counter++;
        }

        return targetPath;
    }

    private static string SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "بدون اسم";

        // Replace illegal filesystem characters: / \ : * ? " < > |
        var sanitized = Regex.Replace(name.Trim(), @"[/\\:*?""<>|]", "-");
        // Remove duplicate spaces and hyphens
        sanitized = Regex.Replace(sanitized, @"\s+", " ");
        sanitized = Regex.Replace(sanitized, @"-+", "-").Trim(' ', '-', '.');

        return string.IsNullOrWhiteSpace(sanitized) ? "ملف" : sanitized;
    }

    private static void CreateHardLinkSafe(string newFileName, string existingFileName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!CreateHardLinkWindows(newFileName, existingFileName, IntPtr.Zero))
            {
                File.Copy(existingFileName, newFileName, true);
            }
        }
        else
        {
            // Unix / Linux link()
            if (link(existingFileName, newFileName) != 0)
            {
                File.Copy(existingFileName, newFileName, true);
            }
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkWindows(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    [DllImport("libc", SetLastError = true)]
    private static extern int link(string oldpath, string newpath);

    private static string? FindPgDumpExecutable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var paths = new[]
            {
                @"C:\Program Files\PostgreSQL\18\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\17\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\16\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\15\bin\pg_dump.exe"
            };

            foreach (var p in paths)
            {
                if (File.Exists(p)) return p;
            }
        }
        else
        {
            var unixPaths = new[] { "/usr/bin/pg_dump", "/usr/local/bin/pg_dump" };
            foreach (var p in unixPaths)
            {
                if (File.Exists(p)) return p;
            }
        }

        return "pg_dump";
    }
}
