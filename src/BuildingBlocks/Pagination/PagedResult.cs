namespace BuildingBlocks.Pagination;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> selector)
        => new(Items.Select(selector).ToList(), Page, PageSize, TotalCount);
}

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
