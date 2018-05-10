using FluentValidation;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class CreateUserRequestValidator : AbstractValidator<User>
    {
        public CreateUserRequestValidator()
        {
            RuleFor(request => request.FirstName)
                .NotNull().WithMessage($"{nameof(User.FirstName)} cannot be null")
                .SetValidator(new NameValidator(nameof(User.FirstName)));

            RuleFor(request => request.LastName)
                .NotNull().WithMessage($"{nameof(User.LastName)} cannot be null")
                .SetValidator(new NameValidator(nameof(User.LastName)));

            RuleFor(request => request.Email)
                .NotNull().WithMessage($"{nameof(User.Email)} cannot be null")
                .SetValidator(new EmailValidator(nameof(User.Email)));

            RuleFor(request => request.Username)
                .NotNull().WithMessage($"{nameof(User.Username)} cannot be null")
                .SetValidator(new UserNameValidator());

            RuleFor(request => request.TenantId)
                .NotEmpty().WithMessage($"{nameof(User.TenantId)} cannot be an empty Guid");
        }
    }
}
