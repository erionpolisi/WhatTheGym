namespace Gym.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResult<T> Empty(int page, int pageSize) => new([], page, pageSize, 0);
}

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var p = page.GetValueOrDefault(1);
        var s = pageSize.GetValueOrDefault(DefaultPageSize);
        return (Math.Max(1, p), Math.Clamp(s, 1, MaxPageSize));
    }
}
