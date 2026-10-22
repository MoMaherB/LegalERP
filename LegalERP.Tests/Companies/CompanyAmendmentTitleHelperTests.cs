using FluentAssertions;
using LegalERP.Application.Companies;
using Xunit;

namespace LegalERP.Tests.Companies;

public class CompanyAmendmentTitleHelperTests
{
    [Fact]
    public void GetNextAmendmentTitle_WhenNoAmendmentsExist_ReturnsFirstAmendmentTitle()
    {
        // Arrange
        var amendments = Enumerable.Empty<CompanyAmendmentDto>();

        // Act
        var result = CompanyAmendmentTitleHelper.GetNextAmendmentTitle(amendments);

        // Assert
        result.Should().Be("عقد التعديل الأول");
    }

    [Theory]
    [InlineData(1, "عقد التعديل الأول")]
    [InlineData(2, "عقد التعديل الثاني")]
    [InlineData(3, "عقد التعديل الثالث")]
    [InlineData(4, "عقد التعديل الرابع")]
    [InlineData(5, "عقد التعديل الخامس")]
    [InlineData(6, "عقد التعديل السادس")]
    [InlineData(7, "عقد التعديل السابع")]
    [InlineData(8, "عقد التعديل الثامن")]
    [InlineData(9, "عقد التعديل التاسع")]
    [InlineData(10, "عقد التعديل العاشر")]
    [InlineData(11, "عقد التعديل 11")]
    [InlineData(25, "عقد التعديل 25")]
    public void GetOrdinalTitle_GivenSequenceNumber_ReturnsExpectedArabicTitle(int sequence, string expectedTitle)
    {
        var result = CompanyAmendmentTitleHelper.GetOrdinalTitle(sequence);
        result.Should().Be(expectedTitle);
    }

    [Fact]
    public void GetNextAmendmentTitle_WithNonSequentialAmendments_ComputesFromMaximumSequence()
    {
        // Arrange: sequences 1, 5, 2 (unordered) -> max is 5, next is 6 ("عقد التعديل السادس")
        var amendments = new List<CompanyAmendmentDto>
        {
            new(Guid.NewGuid(), 1, "Title 1", null, null, null),
            new(Guid.NewGuid(), 5, "Title 5", null, null, null),
            new(Guid.NewGuid(), 2, "Title 2", null, null, null)
        };

        // Act
        var result = CompanyAmendmentTitleHelper.GetNextAmendmentTitle(amendments);

        // Assert
        result.Should().Be("عقد التعديل السادس");
    }
}
