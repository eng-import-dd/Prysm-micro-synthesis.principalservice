using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public class UserNameValidator : AbstractValidator<string>
    {
        public UserNameValidator()
        {
            RuleFor(u => u)
                .NotEmpty().WithMessage("The Username property must not be empty")
                .MaximumLength(100).WithMessage("The Username must be less than 100 characters long")
                .Matches(@"^[0-9a-zA-Z@\-\._]{1,100}$").WithMessage("Username may only contain alpha-numeric characters as well . @ _ -");
        }
    }
}
