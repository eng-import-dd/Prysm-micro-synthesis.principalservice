using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public class ProjectAccessCodeValidator : AbstractValidator<string>
    {
        public ProjectAccessCodeValidator()
        {
            RuleFor(accessCode => accessCode)
                .NotEqual(string.Empty).WithMessage("The ProjectAccessCode must not be empty");

            RuleFor(accessCode => accessCode)
                .Length(10).WithMessage("The ProjectAccessCode field must be 10 characters in length");

            RuleFor(accessCode => accessCode)
                .Must(x => int.TryParse(x, out var _))
                .WithMessage("The ProjectAccessCode must be a number");
        }
    }
}
