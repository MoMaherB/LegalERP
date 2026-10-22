using FluentAssertions;
using LegalERP.Application.Cases;
using LegalERP.Domain.Enums;
using Xunit;

namespace LegalERP.Tests.Cases;

public class CasePartyRoleHelperTests
{
    [Theory]
    [InlineData(CaseType.Criminal, PartyRole.Defendant, "DefendantCriminal")]
    [InlineData(CaseType.Criminal, PartyRole.Victim, "VictimCriminal")]
    [InlineData(CaseType.Civil, PartyRole.Defendant, "DefendantCivil")]
    [InlineData(CaseType.Civil, PartyRole.Victim, "PlaintiffCivil")]
    [InlineData(CaseType.PersonalStatus, PartyRole.Defendant, "DefendantCivil")]
    [InlineData(CaseType.PersonalStatus, PartyRole.Victim, "PlaintiffCivil")]
    [InlineData(CaseType.Administrative, PartyRole.Defendant, "DefendantCivil")]
    [InlineData(CaseType.Administrative, PartyRole.Victim, "PlaintiffCivil")]
    public void GetRoleResourceKey_ReturnsExpectedResourceKey_ForCaseTypeAndPartyRole(
        CaseType caseType,
        PartyRole partyRole,
        string expectedKey)
    {
        var result = CasePartyRoleHelper.GetRoleResourceKey(caseType, partyRole);

        result.Should().Be(expectedKey);
    }
}
