using FluentValidation;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class GuestCreationRequestValidator : AbstractValidator<GuestCreationRequest>
    {
        public GuestCreationRequestValidator()
        {
            RuleFor(request => request.Email).SetValidator(new EmailValidator("Email"));
            RuleFor(request => request.FirstName).SetValidator(new NameValidator("FirstName"));
            RuleFor(request => request.LastName).SetValidator(new NameValidator("LastName"));
            RuleFor(request => request.LastName).SetValidator(new PasswordValidator());

            RuleFor(request => request.PasswordConfirmation).NotNull().WithMessage("PasswordConfirmation cannot be null");
            RuleFor(request => request.PasswordConfirmation).Equal(x => x.Password).WithMessage("The Password and ConfirmationPassword must match");
        }
    }
}
