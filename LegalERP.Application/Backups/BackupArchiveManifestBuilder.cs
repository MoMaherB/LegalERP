using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LegalERP.Domain.Entities;

namespace LegalERP.Application.Backups;

public record BackupArchiveEntry(Document SourceDocument, string RelativeDirectory, string FileName);

public record BackupArchiveManifest(
    IReadOnlyList<string> Directories,
    IReadOnlyList<BackupArchiveEntry> Entries);

public static class BackupArchiveManifestBuilder
{
    public static BackupArchiveManifest Build(
        IEnumerable<Company> companies,
        IEnumerable<Document> companyGeneralDocs,
        IEnumerable<Client> clients,
        IEnumerable<Case> cases,
        IEnumerable<Document> caseGeneralDocs,
        IEnumerable<Attorney> attorneys)
    {
        var entries = new List<BackupArchiveEntry>();
        var usedFileNamesByDir = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        var companyDocsList = companyGeneralDocs.ToList();
        var caseDocsList = caseGeneralDocs.ToList();

        // 1. Companies & Contracts
        foreach (var comp in companies)
        {
            var folderName = SanitizeName(!string.IsNullOrWhiteSpace(comp.FileNumber)
                ? $"{comp.FileNumber} - {comp.CompanyName}"
                : comp.CompanyName);

            var compDir = Path.Combine("companies", folderName);

            var companyIdent = comp.FileNumber ?? comp.CompanyName;

            AddEntryIfPresent(entries, usedFileNamesByDir, comp.IncorporationDocument, compDir, $"عقد التأسيس_{companyIdent}");
            AddEntryIfPresent(entries, usedFileNamesByDir, comp.PartnersIdsDocument, compDir, $"بطاقات الشركاء_{companyIdent}");
            AddEntryIfPresent(entries, usedFileNamesByDir, comp.PartnersAttorneysDocument, compDir, $"توكيلات الشركاء_{companyIdent}");

            if (comp.Amendments != null && comp.Amendments.Count > 0)
            {
                var amendDir = Path.Combine(compDir, "التعديلات");

                foreach (var amend in comp.Amendments.OrderBy(a => a.SequenceNumber))
                {
                    AddEntryIfPresent(entries, usedFileNamesByDir, amend.Document, amendDir, $"عقد التعديل رقم {amend.SequenceNumber}");
                }
            }

            var generalDocs = companyDocsList.Where(d => d.OwnerId == comp.Id).ToList();
            if (generalDocs.Count > 0)
            {
                var recordsDir = Path.Combine(compDir, "سجلات وتراخيص الشركة");

                foreach (var gdoc in generalDocs)
                {
                    AddEntryIfPresent(entries, usedFileNamesByDir, gdoc, recordsDir, Path.GetFileNameWithoutExtension(gdoc.FileName));
                }
            }
        }

        // 2. Clients & IDs
        foreach (var client in clients)
        {
            var folderName = SanitizeName(!string.IsNullOrWhiteSpace(client.FileNumber)
                ? $"{client.FileNumber} - {client.FullName}"
                : client.FullName);

            var clientDir = Path.Combine("clients", folderName);

            AddEntryIfPresent(entries, usedFileNamesByDir, client.NationalIdDocument, clientDir, $"صورة الهوية_{client.FileNumber ?? client.FullName}");
        }

        // 3. Cases, Memos & Evidence
        foreach (var c in cases)
        {
            var folderName = SanitizeName(!string.IsNullOrWhiteSpace(c.CourtName)
                ? $"قضية {c.CaseNumber} - {c.CourtName}"
                : $"قضية {c.CaseNumber}");

            var caseDir = Path.Combine("cases", folderName);

            if (c.Memos != null && c.Memos.Count > 0)
            {
                var memosDir = Path.Combine(caseDir, "المذكرات القانونية");

                foreach (var memo in c.Memos)
                {
                    var memoTitle = string.IsNullOrWhiteSpace(memo.Title) ? "مذكرة دفاع" : memo.Title;
                    AddEntryIfPresent(entries, usedFileNamesByDir, memo.Document, memosDir, memoTitle);
                }
            }

            var attachedCaseDocs = caseDocsList.Where(d => d.OwnerId == c.Id).ToList();
            if (attachedCaseDocs.Count > 0)
            {
                var docsDir = Path.Combine(caseDir, "المستندات والمستخرجات");

                foreach (var doc in attachedCaseDocs)
                {
                    AddEntryIfPresent(entries, usedFileNamesByDir, doc, docsDir, Path.GetFileNameWithoutExtension(doc.FileName));
                }
            }
        }

        // 4. Powers of Attorney (التوكيلات)
        foreach (var att in attorneys)
        {
            AddEntryIfPresent(entries, usedFileNamesByDir, att.Document, "attornies", $"توكيل_{att.AttorneyNumber}");
        }

        // Only include directories that actually contain at least one file entry
        var directories = entries
            .Select(e => e.RelativeDirectory)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new BackupArchiveManifest(directories, entries);
    }

    private static void AddEntryIfPresent(
        List<BackupArchiveEntry> entries,
        Dictionary<string, HashSet<string>> usedFileNamesByDir,
        Document? doc,
        string relativeDir,
        string rawBaseName)
    {
        if (doc == null) return;

        var ext = Path.GetExtension(doc.FileName);
        var sanitizedBase = SanitizeName(rawBaseName);
        var uniqueFileName = ResolveUniqueFileName(usedFileNamesByDir, relativeDir, sanitizedBase, ext);
        entries.Add(new BackupArchiveEntry(doc, relativeDir, uniqueFileName));
    }

    private static string ResolveUniqueFileName(
        Dictionary<string, HashSet<string>> usedFileNamesByDir,
        string relativeDir,
        string sanitizedBaseName,
        string extension)
    {
        if (!usedFileNamesByDir.TryGetValue(relativeDir, out var usedNames))
        {
            usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            usedFileNamesByDir[relativeDir] = usedNames;
        }

        var candidate = $"{sanitizedBaseName}{extension}";
        int counter = 2;
        while (!usedNames.Add(candidate))
        {
            candidate = $"{sanitizedBaseName} ({counter}){extension}";
            counter++;
        }

        return candidate;
    }

    public static string SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "بدون اسم";

        // Replace illegal filesystem characters: / \ : * ? " < > |
        var sanitized = Regex.Replace(name.Trim(), @"[/\\:*?""<>|]", "-");
        // Remove duplicate spaces and hyphens
        sanitized = Regex.Replace(sanitized, @"\s+", " ");
        sanitized = Regex.Replace(sanitized, @"-+", "-").Trim(' ', '-', '.');

        return string.IsNullOrWhiteSpace(sanitized) ? "ملف" : sanitized;
    }
}
