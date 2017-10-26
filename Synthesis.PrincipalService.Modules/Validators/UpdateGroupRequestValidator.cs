using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentValidation;
using Synthesis.PrincipalService.Dao.Models;

namespace Synthesis.PrincipalService.Validators
{
    public class UpdateGroupRequestValidator : AbstractValidator<Group>
    {
        public UpdateGroupRequestValidator()
        {
            RuleFor(request => request.Id)
                .NotEmpty().WithMessage("The Group Id property must not be empty")
                .NotNull().WithMessage("The Group Id property must not be null");

            RuleFor(request => request.TenantId)
                .NotEmpty().WithMessage("The Tenant Id property must not be empty")
                .NotNull().WithMessage("The Tenant Id property must not be null");

            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The Group Name property must not be empty")
                .MaximumLength(100).WithMessage("The Group Name must be less than 100 characters long");
        }
    }
}
