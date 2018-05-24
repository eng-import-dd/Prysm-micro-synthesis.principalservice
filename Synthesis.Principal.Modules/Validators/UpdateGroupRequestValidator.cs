using System;
using FluentValidation;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Validators
{
    public class UpdateGroupRequestValidator : AbstractValidator<Group>
    {
        public UpdateGroupRequestValidator()
        {
            RuleFor(request => request.Id)
                .NotNull().WithMessage("The Group Id property must not be null")
                .NotEqual(Guid.Empty).WithMessage("The Group Id property must not be empty");

            RuleFor(request => request.TenantId)
                .NotEqual(Guid.Empty).WithMessage("The Tenant Id property must not be empty");

            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The Group Name property must not be empty")
                .MaximumLength(100).WithMessage("The Group Name must be less than 100 characters long");
        }
    }
}