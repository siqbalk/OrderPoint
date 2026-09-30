using FluentValidation;

namespace Identity.Application.Features.AcceptInvitation;

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).ValidPassword();
    }
}
