using FluentValidation;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Validators
{
    public class PrincipalValidator : AbstractValidator<Principal>
    {
        public PrincipalValidator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The Name property must not be empty");
        }
    }
}
