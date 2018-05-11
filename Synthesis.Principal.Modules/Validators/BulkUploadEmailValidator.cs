using System.Text.RegularExpressions;
using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public class BulkUploadEmailValidator : AbstractValidator<string>
    {
        // This Regex is exclusively used in the UserService class for Bulk upload - It removes support for double quotes and unicode characters in the email ids as we don't allow those for user login
        private readonly Regex _regexForBulkUpload = new Regex(@"^(([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~])+(\.([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~])+)*)@((([a-z]|\d)|(([a-z]|\d)([a-z]|\d|-|\.|_|~)*([a-z]|\d)))\.)+(([a-z])|(([a-z])([a-z]|\d|-|\.|_|~)*([a-z])))\.?$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        public BulkUploadEmailValidator()
        {
            RuleFor(email => email)
                .NotEmpty()
                .WithMessage("The Email address cannot be empty.");

            RuleFor(email => email)
                .Must(IsFormatValid)
                .WithMessage("The Email address is not properly formatted.");
        }

        public bool IsFormatValid(string emailString)
        {
            return _regexForBulkUpload.Match(emailString).Length > 0;
        }
    }
}
