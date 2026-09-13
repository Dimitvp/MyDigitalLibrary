namespace MyDigitalLibrary.Application.Common;

public static class PageRequest
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 1000;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var normalizedPage = page is > 0 ? page.Value : 1;
        var normalizedPageSize = pageSize switch
        {
            null or <= 0 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize.Value,
        };

        return (normalizedPage, normalizedPageSize);
    }
}
