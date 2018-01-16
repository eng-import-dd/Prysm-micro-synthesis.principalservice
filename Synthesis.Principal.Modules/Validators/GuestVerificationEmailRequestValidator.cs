using FluentValidation;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class GuestVerificationEmailRequestValidator : AbstractValidator<GuestVerificationEmailRequest>
    {
        public GuestVerificationEmailRequestValidator()
        {
            RuleFor(request => request.Email).SetValidator(new EmailValidator("Email"));
            RuleFor(request => request.FirstName).SetValidator(new NameValidator("FirstName"));
            RuleFor(request => request.LastName).SetValidator(new NameValidator("LastName"));
            RuleFor(request => request.ProjectAccessCode).SetValidator(new ProjectAccessCodeValidator());
        }
    }
}
