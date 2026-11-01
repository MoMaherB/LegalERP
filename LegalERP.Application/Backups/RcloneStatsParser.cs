using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LegalERP.Application.Backups;

public static partial class RcloneStatsParser
{
    private static readonly Regex StatsOneLineRegex = new(
        @"(?<transferred>\d+(?:\.\d+)?\s*[KMGTPE]?i?B)\s*/\s*(?<total>\d+(?:\.\d+)?\s*[KMGTPE]?i?B),\s*(?<pct>-|\d+%?),\s*(?<speed>[^,]+),\s*ETA\s*(?<eta>\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SizeValueRegex = new(
        @"^(?<num>\d+(?:\.\d+)?)\s*(?<unit>[KMGTPE]?i?B)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParseStatsLine(string? line, out BackupProgressSnapshotDto? snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = StatsOneLineRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        var transferredText = match.Groups["transferred"].Value.Trim();
        var totalText = match.Groups["total"].Value.Trim();
        var pctGroup = match.Groups["pct"].Value.Trim().TrimEnd('%');
        var speedText = match.Groups["speed"].Value.Trim();
        var etaText = match.Groups["eta"].Value.Trim();

        int percentage = 0;
        if (pctGroup != "-" && int.TryParse(pctGroup, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPct))
        {
            percentage = Math.Clamp(parsedPct, 0, 100);
        }

        long transferredBytes = ParseSizeToBytes(transferredText);
        long totalBytes = ParseSizeToBytes(totalText);

        snapshot = new BackupProgressSnapshotDto(
            Percentage: percentage,
            TransferredBytes: transferredBytes,
            TotalBytes: totalBytes,
            TransferredText: transferredText,
            TotalText: totalText,
            SpeedText: speedText,
            EtaText: etaText);

        return true;
    }

    private static long ParseSizeToBytes(string sizeText)
    {
        var match = SizeValueRegex.Match(sizeText.Trim());
        if (!match.Success)
        {
            return 0;
        }

        if (!double.TryParse(match.Groups["num"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return 0;
        }

        var unit = match.Groups["unit"].Value.ToUpperInvariant();
        double multiplier = unit switch
        {
            "B" => 1d,
            "KB" => 1_000d,
            "KIB" => 1_024d,
            "MB" => 1_000_000d,
            "MIB" => 1_024d * 1_024d,
            "GB" => 1_000_000_000d,
            "GIB" => 1_024d * 1_024d * 1_024d,
            "TB" => 1_000_000_000_000d,
            "TIB" => 1_024d * 1_024d * 1_024d * 1_024d,
            _ => 1d
        };

        return (long)(value * multiplier);
    }
}
