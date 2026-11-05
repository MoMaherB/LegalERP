using System;
using System.Collections.Generic;

namespace LegalERP.Application.Common;

public record PaginationParams
{
    private readonly int _pageNumber = 1;
    private readonly int _pageSize = 25;

    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = Math.Max(1, value);
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Clamp(value, 1, 100);
    }

    public int Skip => (PageNumber - 1) * PageSize;
    public int Take => PageSize;
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
