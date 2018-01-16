using FluentValidation;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class CreateUserRequestValidator : AbstractValidator<UserRequest>
    {
        public CreateUserRequestValidator()
        {
            RuleFor(request => request.FirstName).SetValidator(new NameValidator("FirstName"));
            RuleFor(request => request.LastName).SetValidator(new NameValidator("LastName"));
            RuleFor(request => request.Email).SetValidator(new EmailValidator("Email"));
            RuleFor(request => request.UserName).SetValidator(new UserNameValidator());
        }
    }
}
