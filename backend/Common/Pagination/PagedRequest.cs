namespace TecnmInventario.Api.Common.Pagination;

public sealed record PagedRequest(int PageNumber = 1, int PageSize = 20)
{
    private const int MaxPageSize = 100;

    public int PageNumber { get; init; } = PageNumber < 1 ? 1 : PageNumber;
    public int PageSize { get; init; } = PageSize < 1 ? 20 : Math.Min(PageSize, MaxPageSize);
}
