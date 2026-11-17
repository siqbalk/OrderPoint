using BuildingBlocks.Security;
using FluentValidation;

namespace Identity.Application.Features.ChangeUserRole;

public sealed class ChangeUserRoleValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum()
            .NotEqual(TenantRole.Owner).WithMessage("A tenant has exactly one owner; choose Admin or Member.");
    }
}
