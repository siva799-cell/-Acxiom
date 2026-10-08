namespace AcxiomCRM.Services;

public sealed record ServiceResult<T>(T? Data, string? Error)
{
    public bool Succeeded => Error is null;
    public static ServiceResult<T> Success(T data) => new(data, null);
    public static ServiceResult<T> Failure(string error) => new(default, error);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int PageCount => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
