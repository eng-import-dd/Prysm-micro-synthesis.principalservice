using FluentValidation;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class CreateUserRequestValidator : AbstractValidator<UserRequest>
    {
        public CreateUserRequestValidator()
        {
            RuleFor(request => request.FirstName)
                .NotNull().WithMessage($"{nameof(UserRequest.FirstName)} cannot be null")
                .SetValidator(new NameValidator(nameof(UserRequest.FirstName)));

            RuleFor(request => request.LastName)
                .NotNull().WithMessage($"{nameof(UserRequest.LastName)} cannot be null")
                .SetValidator(new NameValidator(nameof(UserRequest.LastName)));

            RuleFor(request => request.Email)
                .NotNull().WithMessage($"{nameof(UserRequest.Email)} cannot be null")
                .SetValidator(new EmailValidator(nameof(UserRequest.Email)));

            RuleFor(request => request.UserName)
                .NotNull().WithMessage($"{nameof(UserRequest.UserName)} cannot be null")
                .SetValidator(new UserNameValidator());
        }
    }
}
