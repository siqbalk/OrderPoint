using FluentValidation;

namespace Identity.Application.Features.EnableUser;

public sealed class EnableUserValidator : AbstractValidator<EnableUserCommand>
{
    public EnableUserValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
