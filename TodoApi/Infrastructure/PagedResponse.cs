namespace TodoApi.Infrastructure;

/// <summary>
/// Pagination response model.
/// </summary>
public record PagedResponse<TResponse>(
    IEnumerable<TResponse> Data,
    int Page,
    int PageSize,
    int TotalCount
)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
