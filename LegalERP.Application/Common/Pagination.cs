using System;
using System.Collections.Generic;

namespace LegalERP.Application.Common;

public record PaginationParams
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;

    public int Skip => Math.Max(0, (PageNumber - 1) * PageSize);
    public int Take => Math.Clamp(PageSize, 1, 100);
}

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
