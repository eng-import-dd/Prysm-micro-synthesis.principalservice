using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public class GeUserNamesByIdsValidator : AbstractValidator<IEnumerable<Guid>>
    {
        public GeUserNamesByIdsValidator()
        {
            RuleForEach(model => model.AsEnumerable())
                .NotEmpty()
                .WithMessage("None of the Guids may be empty.")
                .OverridePropertyName("Id");
        }
    }
}