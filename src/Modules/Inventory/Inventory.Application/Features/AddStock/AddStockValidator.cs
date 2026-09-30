using FluentValidation;

namespace Inventory.Application.Features.AddStock;

public sealed class AddStockValidator : AbstractValidator<AddStockCommand>
{
    public AddStockValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
