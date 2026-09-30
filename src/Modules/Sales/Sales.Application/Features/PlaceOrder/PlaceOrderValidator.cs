using FluentValidation;

namespace Sales.Application.Features.PlaceOrder;

public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public const int MaxLines = 50;

    public PlaceOrderValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress().MaximumLength(256);

        RuleFor(x => x.Lines).NotEmpty()
            .Must(lines => lines.Count <= MaxLines).WithMessage($"An order may have at most {MaxLines} lines.")
            .Must(lines => lines.Select(l => l.Sku?.Trim().ToUpperInvariant()).Distinct().Count() == lines.Count)
            .WithMessage("Each SKU may appear only once; combine the quantities into one line.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Sku).NotEmpty().MaximumLength(50);
            line.RuleFor(l => l.Quantity).GreaterThan(0).LessThanOrEqualTo(10_000);
        });
    }
}
