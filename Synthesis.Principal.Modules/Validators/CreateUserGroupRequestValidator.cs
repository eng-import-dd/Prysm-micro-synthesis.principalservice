using System;
using FluentValidation;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class CreateUserGroupRequestValidator : AbstractValidator<CreateUserGroupRequest>
    {
        public CreateUserGroupRequestValidator()
        {
            RuleFor(request => request.UserId)
                .NotEmpty().WithMessage("User Id must not be empty.");

            RuleFor(request => request.GroupId)
                .NotEmpty().WithMessage("Group Id must not be empty.");
        }
    }
}
