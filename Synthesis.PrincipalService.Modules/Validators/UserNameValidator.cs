using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public class UserNameValidator : AbstractValidator<string>
    {
        public UserNameValidator()
        {
            RuleFor(u => u).NotEmpty().NotNull();
        }
    }
}
