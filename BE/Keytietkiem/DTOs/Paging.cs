namespace Keytietkiem.DTOs;

public record PagingQuery(int Page = 1, int PageSize = 10);
public record PagedResult<T>(IEnumerable<T> Items, int Total, int Page, int PageSize);
