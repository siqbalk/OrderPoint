using FluentValidation;

namespace Catalog.Application.Features.GetProductBySku;

public sealed class GetProductBySkuValidator : AbstractValidator<GetProductBySkuQuery>
{
    public GetProductBySkuValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
    }
}
