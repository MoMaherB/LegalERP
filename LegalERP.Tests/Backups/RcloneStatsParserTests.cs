using FluentAssertions;
using LegalERP.Application.Backups;
using Xunit;

namespace LegalERP.Tests.Backups;

public class RcloneStatsParserTests
{
    [Fact]
    public void TryParseStatsLine_WithActiveTransferStatsLine_ParsesPercentageBytesSpeedAndEta()
    {
        var line = "2026/10/30 14:05:12 NOTICE:     5.600 MiB / 12.410 MiB, 45%, 1.120 MiB/s, ETA 6s";

        var parsed = RcloneStatsParser.TryParseStatsLine(line, out var snapshot);

        parsed.Should().BeTrue();
        snapshot.Should().NotBeNull();
        snapshot!.Percentage.Should().Be(45);
        snapshot.TransferredText.Should().Be("5.600 MiB");
        snapshot.TotalText.Should().Be("12.410 MiB");
        snapshot.TransferredBytes.Should().Be(5_872_025);
        snapshot.TotalBytes.Should().Be(13_012_828);
        snapshot.SpeedText.Should().Be("1.120 MiB/s");
        snapshot.EtaText.Should().Be("6s");
    }

    [Fact]
    public void TryParseStatsLine_WithIdleZeroTransferStatsLine_ParsesZeroProgressGracefully()
    {
        var line = "2026/10/30 14:05:01 NOTICE:           0 B / 0 B, -, 0 B/s, ETA -";

        var parsed = RcloneStatsParser.TryParseStatsLine(line, out var snapshot);

        parsed.Should().BeTrue();
        snapshot.Should().NotBeNull();
        snapshot!.Percentage.Should().Be(0);
        snapshot.TransferredBytes.Should().Be(0);
        snapshot.TotalBytes.Should().Be(0);
        snapshot.TransferredText.Should().Be("0 B");
        snapshot.TotalText.Should().Be("0 B");
        snapshot.SpeedText.Should().Be("0 B/s");
        snapshot.EtaText.Should().Be("-");
    }

    [Fact]
    public void TryParseStatsLine_WithNonStatsLine_ReturnsFalse()
    {
        var line = "2026/10/30 14:05:01 ERROR : some random rclone log message";

        var parsed = RcloneStatsParser.TryParseStatsLine(line, out var snapshot);

        parsed.Should().BeFalse();
        snapshot.Should().BeNull();
    }
}
