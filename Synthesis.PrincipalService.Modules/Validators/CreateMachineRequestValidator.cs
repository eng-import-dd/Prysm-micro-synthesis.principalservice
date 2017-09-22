using FluentValidation;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class CreateMachineRequestValidator : AbstractValidator<CreateMachineRequest>
    {
        public CreateMachineRequestValidator()
        {
            RuleFor(request => request.MachineId).NotEmpty().WithMessage("The MachineId property must not be empty");
            RuleFor(request => request.MachineKey).NotEmpty().WithMessage("The MachineKey property must not be empty");
            RuleFor(request => request.ModifiedBy).NotEmpty().WithMessage("The DateCreated property must not be empty");

        }
    }
}
