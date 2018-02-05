using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public class PasswordValidator : AbstractValidator<string>
    {
        private const int MinPasswordLength = 6;

        public PasswordValidator()
        {
            RuleFor(password => password.Length)
                .GreaterThanOrEqualTo(MinPasswordLength).WithMessage($"The Password must be at least {MinPasswordLength} characters long");
        }
    }
}
