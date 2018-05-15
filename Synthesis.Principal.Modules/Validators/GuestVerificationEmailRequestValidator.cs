using FluentValidation;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Validators
{
    public class GuestVerificationEmailRequestValidator : AbstractValidator<GuestVerificationEmailRequest>
    {
        public GuestVerificationEmailRequestValidator()
        {
            RuleFor(request => request.Email)
                .NotNull().WithMessage($"{nameof(GuestVerificationEmailRequest.Email)} cannot be null")
                .SetValidator(new EmailValidator(nameof(User.Email)));

            RuleFor(request => request.FirstName)
                .NotNull().WithMessage($"{nameof(GuestVerificationEmailRequest.FirstName)} cannot be null")
                .SetValidator(new NameValidator(nameof(User.FirstName)));

            RuleFor(request => request.LastName)
                .NotNull().WithMessage($"{nameof(GuestVerificationEmailRequest.LastName)} cannot be null")
                .SetValidator(new NameValidator(nameof(User.LastName)));
        }
    }
}
