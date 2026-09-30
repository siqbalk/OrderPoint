using FluentValidation;

namespace Identity.Application.Features.ChangePlan;

public sealed class ChangePlanValidator : AbstractValidator<ChangePlanCommand>
{
    public ChangePlanValidator()
    {
        RuleFor(x => x.Plan).IsInEnum();
    }
}
