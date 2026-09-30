using FluentValidation;

namespace Inventory.Application.Features.AdjustStock;

public sealed class AdjustStockValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.QuantityOnHand).GreaterThanOrEqualTo(0).LessThanOrEqualTo(10_000_000);
    }
}
