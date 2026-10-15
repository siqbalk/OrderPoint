using FluentValidation;

namespace Identity.Application.Features.ListUsers;

public sealed class ListUsersValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}
