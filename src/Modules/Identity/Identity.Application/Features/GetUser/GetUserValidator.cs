using FluentValidation;

namespace Identity.Application.Features.GetUser;

public sealed class GetUserValidator : AbstractValidator<GetUserQuery>
{
    public GetUserValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
