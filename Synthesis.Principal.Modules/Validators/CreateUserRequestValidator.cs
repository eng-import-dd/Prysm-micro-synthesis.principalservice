using FluentValidation;
using Synthesis.PrincipalService.InternalApi.Models;

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

            RuleFor(request => request.Username)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.Username)} cannot be null")
                .NotEmpty().WithMessage($"{nameof(CreateUserRequest.Username)} cannot be empty");

            RuleFor(request => request.Username)
                .SetValidator(new UserNameValidator()).When(u => u.Username != null && u.Username.Contains("@"))
                .SetValidator(new EmailValidator()).When(u => u.Username != null && !u.Username.Contains("@"));
        }
    }
}
