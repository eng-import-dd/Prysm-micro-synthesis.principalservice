using System;
using FluentValidation;
using Synthesis.PrincipalService.Dao.Models;

namespace Synthesis.PrincipalService.Validators
{
    public class PrincipalserviceValidator : AbstractValidator<Principalservice>
    {
        public PrincipalserviceValidator()
        {
            RuleFor(request => request.Id)
                .NotEqual(Guid.Empty).WithMessage("The Id must not be empty");
        }
    }
}
