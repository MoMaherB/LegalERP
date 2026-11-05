using LegalERP.Application.Common;
using Microsoft.AspNetCore.Http;

namespace LegalERP.Api.Common;

public static class HttpResponseExtensions
{
    public static void SetPaginationHeaders<T>(this HttpResponse? response, PagedResult<T> paged)
    {
        if (response?.Headers is null) return;
        response.Headers["X-Total-Count"] = paged.TotalCount.ToString();
        response.Headers["X-Page-Number"] = paged.PageNumber.ToString();
        response.Headers["X-Page-Size"] = paged.PageSize.ToString();
        response.Headers["X-Total-Pages"] = paged.TotalPages.ToString();
    }
}
