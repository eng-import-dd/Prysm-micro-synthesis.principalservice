using FluentValidation;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Validators
{
    public class GuestCreationRequestValidator : AbstractValidator<User>
    {
        public GuestCreationRequestValidator()
        {
            RuleFor(request => request.Email)
                .NotNull().WithMessage($"{nameof(User.Email)} cannot be null")
                .SetValidator(new EmailValidator(nameof(User.Email)));

            RuleFor(request => request.FirstName)
                .NotNull().WithMessage($"{nameof(User.FirstName)} cannot be null")
                .SetValidator(new NameValidator(nameof(User.FirstName)));

            RuleFor(request => request.LastName)
                .NotNull().WithMessage($"{nameof(User.LastName)} cannot be null")
                .SetValidator(new NameValidator(nameof(User.LastName)));

            RuleFor(request => request.ProjectAccessCode)
                .NotNull().WithMessage($"{nameof(User.ProjectAccessCode)} cannot be null")
                .SetValidator(new ProjectAccessCodeValidator());
        }
    }
}
