using FluentValidation;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class GuestCreationRequestValidator : AbstractValidator<CreateUserRequest>
    {
        public GuestCreationRequestValidator()
        {
            RuleFor(request => request.Email)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.Email)} cannot be null")
                .SetValidator(new EmailValidator(nameof(CreateUserRequest.Email)));

            RuleFor(request => request.FirstName)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.FirstName)} cannot be null")
                .SetValidator(new NameValidator(nameof(CreateUserRequest.FirstName)));

            RuleFor(request => request.LastName)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.LastName)} cannot be null")
                .SetValidator(new NameValidator(nameof(CreateUserRequest.LastName)));

            RuleFor(request => request.Password)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.Password)} cannot be null")
                .SetValidator(new PasswordValidator());

            RuleFor(request => request.ProjectAccessCode)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.ProjectAccessCode)} cannot be null")
                .SetValidator(new ProjectAccessCodeValidator());

            RuleFor(request => request.PasswordConfirmation)
                .NotNull().WithMessage($"{nameof(CreateUserRequest.PasswordConfirmation)} cannot be null")
                .Equal(x => x.Password).WithMessage($"The Password and {nameof(CreateUserRequest.PasswordConfirmation)} must match");
        }
    }
}
