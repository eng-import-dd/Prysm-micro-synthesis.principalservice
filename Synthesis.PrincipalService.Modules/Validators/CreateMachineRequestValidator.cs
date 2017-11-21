using FluentValidation;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Validators
{
    public class CreateMachineRequestValidator : AbstractValidator<CreateMachineRequest>
    {
        public CreateMachineRequestValidator()
        {
            RuleFor(request => request.MachineKey)
                .NotEmpty().WithMessage("The MachineKey property must not be empty")
                .MaximumLength(20).WithMessage("The MachineKey must be less than 20 characters long")
                .Matches(@"^[0-9a-zA-Z]").WithMessage("MachineKey may only contain alpha-numeric characters.");
        }
    }
}
