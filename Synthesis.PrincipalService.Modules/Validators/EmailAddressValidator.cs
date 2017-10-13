using System;
using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public class EmailAddressValidator : AbstractValidator<string>
    {
        public EmailAddressValidator()
        {
            RuleFor(e => e).EmailAddress();
        }
    }
}
