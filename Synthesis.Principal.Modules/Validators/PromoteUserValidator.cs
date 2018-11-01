using FluentValidation;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Validators
{
    public class PromoteUserValidator : AbstractValidator<User>
    {
        public PromoteUserValidator()
        {
            RuleFor(u => u.Id).NotNull();

            RuleFor(request => request.Email)
                .NotNull().WithMessage($"{nameof(User.Email)} cannot be null")
                .SetValidator(new EmailValidator(nameof(User.Email)));
        }
    }
}
