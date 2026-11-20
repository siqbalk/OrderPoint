using FluentValidation;

namespace Inventory.Application.Features.WriteOffStock;

public sealed class WriteOffStockValidator : AbstractValidator<WriteOffStockCommand>
{
    public WriteOffStockValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(10_000_000);
    }
}
