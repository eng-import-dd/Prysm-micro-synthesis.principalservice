using System.Text.RegularExpressions;
using FluentValidation;
using ValidationConstants = Synthesis.Validators.Utilities.Constants;

namespace Synthesis.PrincipalService.Validators
{
    public class EmailValidator : AbstractValidator<string>
    {
        private readonly string _emailPropertyName;

        // this is the same Regex used in the EmailAddressAttribute class
        private static readonly Regex EmailRegex = new Regex(ValidationConstants.EmailPattern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        public EmailValidator()
        {
            _emailPropertyName = "Email";
            InitiaizeRules();
        }

        public EmailValidator(string titleOfEmailProperty)
        {
            _emailPropertyName = titleOfEmailProperty;
            InitiaizeRules();
        }

        private void InitiaizeRules() 
        {
            RuleFor(email => email)
                .NotNull()
                .WithMessage($"The {_emailPropertyName} address cannot be null.");

            RuleFor(email => email)
                .NotEmpty()
                .WithMessage($"The {_emailPropertyName} address cannot be empty.");

            RuleFor(email => email)
                .Must(IsFormatValid)
                .WithMessage($"The {_emailPropertyName} address is not properly formatted.");
        }

        public static bool IsFormatValid(string emailString)
        {
            if (EmailRegex.Match(emailString).Length > 0)
            {
                return true;
            }

            return false;
        }
    }
}
