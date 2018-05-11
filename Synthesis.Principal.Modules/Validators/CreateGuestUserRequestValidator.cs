using FluentValidation;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Validators
{
    public class CreateGuestUserRequestValidator : AbstractValidator<CreateUserRequest>
    {
        public CreateGuestUserRequestValidator()
        {
            RuleFor(request => request.Email)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.Email)} cannot be null")
                .SetValidator(new EmailValidator(nameof(User.Email)));

            RuleFor(request => request.FirstName)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.FirstName)} cannot be null")
                .SetValidator(new NameValidator(nameof(User.FirstName)));

            RuleFor(request => request.LastName)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.LastName)} cannot be null")
                .SetValidator(new NameValidator(nameof(User.LastName)));

            RuleFor(request => request.TenantId)
                .Empty().WithMessage($"{nameof(CreateUserRequest.TenantId)} must be empty or null").When(r => r.TenantId != null);
        }
    }
}
