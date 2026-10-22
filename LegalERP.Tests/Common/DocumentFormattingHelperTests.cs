using FluentAssertions;
using LegalERP.Web.Components.Shared;
using Xunit;

namespace LegalERP.Tests.Common;

public class DocumentFormattingHelperTests
{
    [Theory]
    [InlineData(0, "0.0 KB")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(2048, "2.0 KB")]
    [InlineData(512, "0.5 KB")]
    public void FormatFileSize_GivenByteCount_ReturnsExpectedKilobyteString(long bytes, string expected)
    {
        var result = EntityDocumentsSection.FormatFileSize(bytes);

        result.Should().Be(expected);
    }
}
