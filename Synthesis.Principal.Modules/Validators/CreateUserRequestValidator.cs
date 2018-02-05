using FluentValidation;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
    {
        public CreateUserRequestValidator()
        {
            RuleFor(request => request.FirstName)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.FirstName)} cannot be null")
                .SetValidator(new NameValidator(nameof(CreateUserRequest.FirstName)));

            RuleFor(request => request.LastName)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.LastName)} cannot be null")
                .SetValidator(new NameValidator(nameof(CreateUserRequest.LastName)));

            RuleFor(request => request.Email)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.Email)} cannot be null")
                .SetValidator(new EmailValidator(nameof(CreateUserRequest.Email)));

            RuleFor(request => request.UserName)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.UserName)} cannot be null")
                .SetValidator(new UserNameValidator());
        }
    }
}
