using System.Text.RegularExpressions;
using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public class BulkUploadEmailValidator : AbstractValidator<string>
    {
        private readonly string _emailPropertyName;

        // This Regex is exclusively used in the UserService class for Bulk upload - It removes support for double quotes and unicode characters in the email ids as we don't allow those for user login
        private static Regex RegexForBulkUpload = new Regex(@"^(([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~])+(\.([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~])+)*)@((([a-z]|\d)|(([a-z]|\d)([a-z]|\d|-|\.|_|~)*([a-z]|\d)))\.)+(([a-z])|(([a-z])([a-z]|\d|-|\.|_|~)*([a-z])))\.?$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        public BulkUploadEmailValidator()
        {
            _emailPropertyName = "Email";
            InitiaizeRules();
        }

        public BulkUploadEmailValidator(string titleOfEmailProperty)
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
            if (RegexForBulkUpload.Match(emailString).Length > 0)
            {
                return true;
            }

            return false;
        }
    }
}
