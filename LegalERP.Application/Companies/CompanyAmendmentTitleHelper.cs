using System.Collections.Generic;
using System.Linq;

namespace LegalERP.Application.Companies;

public static class CompanyAmendmentTitleHelper
{
    private static readonly string[] ArabicOrdinals =
    {
        "الأول", "الثاني", "الثالث", "الرابع", "الخامس",
        "السادس", "السابع", "الثامن", "التاسع", "العاشر"
    };

    public static string GetNextAmendmentTitle(IEnumerable<CompanyAmendmentDto>? amendments)
    {
        var list = amendments?.ToList() ?? new List<CompanyAmendmentDto>();
        var nextSeq = list.Count == 0 ? 1 : list.Max(a => a.SequenceNumber) + 1;
        return GetOrdinalTitle(nextSeq);
    }

    public static string GetOrdinalTitle(int sequenceNumber)
    {
        if (sequenceNumber >= 1 && sequenceNumber <= ArabicOrdinals.Length)
        {
            return $"عقد التعديل {ArabicOrdinals[sequenceNumber - 1]}";
        }

        return $"عقد التعديل {sequenceNumber}";
    }
}
