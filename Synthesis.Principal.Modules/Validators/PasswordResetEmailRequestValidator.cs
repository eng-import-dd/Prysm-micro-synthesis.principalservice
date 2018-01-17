using System;
using FluentValidation;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class PasswordResetEmailRequestValidator : AbstractValidator<PasswordResetEmailRequest>
    {
        public PasswordResetEmailRequestValidator()
        {
            RuleFor(request => request.FirstName)
                .SetValidator(new NameValidator(nameof(PasswordResetEmailRequest.FirstName)));

            RuleFor(request => request.Email)
                .NotNull().WithMessage("The Email property must not be null")
                .NotEmpty().WithMessage("The Email property must not be empty")
                .MaximumLength(100).WithMessage("The Email must be less than 100 characters long")
                .EmailAddress().WithMessage("Invalid email address");

            RuleFor(request => request.Link)
                .NotNull().WithMessage("The Link property must not be null")
                .NotEmpty().WithMessage("The Link property must not be empty")
                .Must(BeAValidUrl).WithMessage("Link must be a valid http link");
        }

        private static bool BeAValidUrl(string arg)
        {
            return Uri.TryCreate(arg, UriKind.Absolute, out _);
        }
    }
}
