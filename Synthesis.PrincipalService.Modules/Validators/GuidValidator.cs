using System;
using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public abstract class GuidValidator : FluentValidation.AbstractValidator<Guid>
  {
        public GuidValidator(string name)
        {
            RuleFor(guid => guid).NotEqual(Guid.Empty).WithMessage($"The {name} must not be empty");
        }
    }

    public class PrincipalServiceIdValidator : GuidValidator
    {
        public PrincipalServiceIdValidator() : base("Id") { }
    }
}
