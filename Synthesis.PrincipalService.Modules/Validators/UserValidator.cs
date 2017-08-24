using FluentValidation;
using Synthesis.PrincipalService.Dao.Models;

namespace Synthesis.PrincipalService.Validators
{
    public class UserValidator : AbstractValidator<User>
    {
        public UserValidator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The Name property must not be empty");
        }
    }
}
