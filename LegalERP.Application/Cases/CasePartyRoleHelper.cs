using LegalERP.Domain.Enums;

namespace LegalERP.Application.Cases;

public static class CasePartyRoleHelper
{
    public static string GetRoleResourceKey(CaseType caseType, PartyRole partyRole)
    {
        if (caseType == CaseType.Criminal)
        {
            return partyRole == PartyRole.Defendant ? "DefendantCriminal" : "VictimCriminal";
        }

        return partyRole == PartyRole.Defendant ? "DefendantCivil" : "PlaintiffCivil";
    }
}
