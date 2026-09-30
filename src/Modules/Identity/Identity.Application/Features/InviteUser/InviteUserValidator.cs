using BuildingBlocks.Security;
using FluentValidation;

namespace Identity.Application.Features.InviteUser;

public sealed class InviteUserValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role).IsInEnum()
            .NotEqual(TenantRole.Owner).WithMessage("A tenant has exactly one owner; invite users as Admin or Member.");
    }
}
