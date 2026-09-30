namespace Catalog.Domain;

/// <summary>
/// A sellable item. Catalog is the source of truth for names and prices:
/// Sales reads the current price through <c>Catalog.Contracts.IProductCatalog</c>
/// when an order is placed, instead of trusting a price sent by the client.
/// </summary>
public sealed class Product
{
    private Product() { }

    public Guid Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public DateTimeOffset? UpdatedOnUtc { get; private set; }

    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public static Product Create(string sku, string name, string? description, decimal price, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU is required.", nameof(sku));

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = NormalizeSku(sku),
            IsActive = true,
            CreatedOnUtc = now,
        };
        product.SetDetails(name, description, price);
        return product;
    }

    public void Update(string name, string? description, decimal price, DateTimeOffset now)
    {
        SetDetails(name, description, price);
        UpdatedOnUtc = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedOnUtc = now;
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedOnUtc = now;
    }

    private void SetDetails(string name, string? description, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));
        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be positive.");

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
    }
}
