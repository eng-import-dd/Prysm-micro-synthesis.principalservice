using FluentValidation;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class GuestVerificationEmailRequestValidator : AbstractValidator<GuestVerificationEmailRequest>
    {
        public GuestVerificationEmailRequestValidator()
        {
            RuleFor(request => request.Email)
                .NotNull().WithMessage($"{nameof(GuestCreationRequest.Email)} cannot be null")
                .SetValidator(new EmailValidator(nameof(GuestVerificationEmailRequest.Email)));

            RuleFor(request => request.FirstName)
                .NotNull().WithMessage($"{nameof(GuestCreationRequest.FirstName)} cannot be null")
                .SetValidator(new NameValidator(nameof(GuestVerificationEmailRequest.FirstName)));

            RuleFor(request => request.LastName)
                .NotNull().WithMessage($"{nameof(GuestCreationRequest.LastName)} cannot be null")
                .SetValidator(new NameValidator(nameof(GuestVerificationEmailRequest.LastName)));

            RuleFor(request => request.ProjectAccessCode)
                .NotNull().WithMessage($"{nameof(GuestCreationRequest.ProjectAccessCode)} cannot be null")
                .SetValidator(new ProjectAccessCodeValidator());
        }
    }
}
