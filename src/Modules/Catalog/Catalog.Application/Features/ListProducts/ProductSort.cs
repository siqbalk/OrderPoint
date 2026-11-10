namespace Catalog.Application.Features.ListProducts;

/// <summary>Order of the product list; ties are broken by name, then id, so paging is stable.</summary>
public enum ProductSort
{
    Name = 0,
    PriceAscending = 1,
    PriceDescending = 2,
    Newest = 3,
}
