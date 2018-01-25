using FluentValidation;
using System;

namespace Synthesis.PrincipalService.Validators
{
    public abstract class GuidValidator : AbstractValidator<Guid>
    {
        protected GuidValidator(string name)
        {
            RuleFor(guid => guid).NotEmpty().WithMessage($"The {name} must not be empty");
        }
    }
}