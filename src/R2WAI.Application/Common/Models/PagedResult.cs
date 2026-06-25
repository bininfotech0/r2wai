namespace R2WAI.Application.Common.Models;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => (Page * PageSize) < TotalCount;
    public bool HasPreviousPage => Page > 1;
}
