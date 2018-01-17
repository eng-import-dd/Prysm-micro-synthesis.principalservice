using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Synthesis.PrincipalService.Validators
{
    public class GetUsersByIdValidator : AbstractValidator<IEnumerable<Guid>>
    {
        public GetUsersByIdValidator()
        {
            RuleFor(model => model.Any())
                .Equal(true)
                .WithMessage("A minimum of 1 Guid must be provided");

            RuleForEach(model => model.AsEnumerable())
                .NotEqual(Guid.Empty)
                .WithMessage("None of the Guids may be empty.")
                .OverridePropertyName("Id");
        }
    }
}
