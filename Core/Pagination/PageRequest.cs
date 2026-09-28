using Core.Entities;

namespace Core.Pagination;

public sealed class PageRequest : IDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
