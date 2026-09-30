using FluentValidation;

namespace Identity.Application.Features.RegisterTenant;

public sealed class RegisterTenantValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantValidator()
    {
        RuleFor(x => x.TenantName).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(x => x.OwnerEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.OwnerDisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).ValidPassword();
    }
}
