namespace Core.DDD.Models;

public class PageData<T>
{
    public List<T> Items { get; set; } = [];
    public long PageNumber { get; set; }
    public long PageSize { get; set; }
    public long TotalItems { get; set; }
    public long TotalPages { get; set; }

    public PageData<TOut> Map<TOut>(Func<T, TOut> selector) => new() { Items = Items.Select(selector).ToList(), PageNumber = PageNumber, PageSize = PageSize, TotalItems = TotalItems, TotalPages = TotalPages };
}
