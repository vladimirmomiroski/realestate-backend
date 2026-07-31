namespace RealEstate.Application.Common;

public sealed record PagedResponse<TResult>(
    IReadOnlyList<TResult> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;

    public static PagedResponse<TResult> From<TSource>(
        PagedResult<TSource> result,
        Func<TSource, TResult> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);

        IReadOnlyList<TResult> items = result.Items
            .Select(map)
            .ToList();

        return new PagedResponse<TResult>(
            items,
            result.Page,
            result.PageSize,
            result.TotalCount);
    }
}
