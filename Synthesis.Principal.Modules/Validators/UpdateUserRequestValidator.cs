﻿using FluentValidation;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Validators
{
    public class UpdateUserRequestValidator : AbstractValidator<User>
    {
        public UpdateUserRequestValidator()
        {
            RuleFor(request => request.FirstName)
                .NotEmpty().WithMessage("The FirstName property must not be empty")
                .MaximumLength(100).WithMessage("The FirstName must be less than 100 characters long");

            RuleFor(request => request.LastName)
                .NotEmpty().WithMessage("The LastName property must not be empty")
                .MaximumLength(100).WithMessage("The LastName must be less than 100 characters long");

            RuleFor(request => request.Email)
                .NotEmpty().WithMessage("The Email property must not be empty")
                .MaximumLength(100).WithMessage("The Email must be less than 100 characters long")
                .EmailAddress().WithMessage("Invalid email address");

            RuleFor(request => request.Username)
                .NotEmpty().WithMessage("The Username property must not be empty")
                .MaximumLength(100).WithMessage("The Username must be less than 100 characters long")
                .Matches(@"^[0-9a-zA-Z@\-\._]{1,100}$").WithMessage("Username may only contain alpha-numeric characters as well . @ _ -");
        }
    }
}
