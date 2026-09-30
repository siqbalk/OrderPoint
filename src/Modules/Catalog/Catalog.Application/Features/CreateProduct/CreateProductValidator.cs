using FluentValidation;

namespace Catalog.Application.Features.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("SKU may contain only letters, digits, '.', '_' and '-'.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Price).GreaterThan(0).LessThan(10_000_000).PrecisionScale(12, 2, ignoreTrailingZeros: true);
    }
}
