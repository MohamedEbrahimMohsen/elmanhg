namespace Core.DDD.Models;

public static class PageCalculator
{
    public static long Offset(int pageNumber, int pageSize) => ((long)pageNumber - 1) * pageSize;

    public static long TotalPages(long totalItems, int pageSize) => pageSize <= 0 ? 0 : totalItems / pageSize + (totalItems % pageSize == 0 ? 0 : 1);

    // Queryable.Skip takes an int; an offset past the last row or past int range can only produce an empty page.
    public static bool IsPastEnd(long offset, long totalItems) => offset >= totalItems || offset > int.MaxValue;
}
