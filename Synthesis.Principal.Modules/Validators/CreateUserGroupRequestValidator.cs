using FluentValidation;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class UserGroupValidator : AbstractValidator<UserGroup>
    {
        public UserGroupValidator()
        {
            RuleFor(request => request.UserId)
                .NotEmpty().WithMessage("User Id must not be empty.");

            RuleFor(request => request.GroupId)
                .NotEmpty().WithMessage("Group Id must not be empty.");
        }
    }
}
