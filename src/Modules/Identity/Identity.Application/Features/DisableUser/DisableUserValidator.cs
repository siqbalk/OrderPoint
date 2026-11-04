using FluentValidation;

namespace Identity.Application.Features.DisableUser;

public sealed class DisableUserValidator : AbstractValidator<DisableUserCommand>
{
    public DisableUserValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
