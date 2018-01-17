using FluentValidation;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class GuestCreationRequestValidator : AbstractValidator<GuestCreationRequest>
    {
        public GuestCreationRequestValidator()
        {
            RuleFor(request => request.Email)
                .NotNull().WithMessage($"{nameof(GuestCreationRequest.Email)} cannot be null")
                .SetValidator(new EmailValidator(nameof(GuestCreationRequest.Email)));

            RuleFor(request => request.FirstName)
                .NotNull().WithMessage($"{nameof(GuestCreationRequest.FirstName)} cannot be null")
                .SetValidator(new NameValidator(nameof(GuestCreationRequest.FirstName)));

            RuleFor(request => request.LastName)
                .NotNull().WithMessage($"{nameof(GuestCreationRequest.LastName)} cannot be null")
                .SetValidator(new NameValidator(nameof(GuestCreationRequest.LastName)));

            RuleFor(request => request.Password)
                .NotNull().WithMessage($"{nameof(GuestCreationRequest.Password)} cannot be null")
                .SetValidator(new PasswordValidator());

            RuleFor(request => request.ProjectAccessCode)
                .NotNull().WithMessage($"{nameof(GuestCreationRequest.ProjectAccessCode)} cannot be null")
                .SetValidator(new ProjectAccessCodeValidator());

            RuleFor(request => request.PasswordConfirmation)
                .NotNull().WithMessage($"{nameof(GuestCreationRequest.PasswordConfirmation)} cannot be null")
                .Equal(x => x.Password).WithMessage($"The Password and {nameof(GuestCreationRequest.PasswordConfirmation)} must match");
        }
    }
}
